using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

// ---------------------------------------------------------------------------
// ForgeOps sandbox runner.
//
// Launched as a short-lived child process by ForgeOps.Forge.SandboxRunner to
// execute an already-compiled, already-audited test assembly and report results
// as JSON on stdout. It is deliberately tiny and dependency-free.
//
//   args[0] = path to the compiled test assembly
//   args[1] = wall-clock budget in milliseconds (internal watchdog; the parent
//             also kills the process tree on timeout)
// ---------------------------------------------------------------------------

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: ForgeOps.Forge.Sandbox <assembly> [timeoutMs]");
    return 64;
}

var assemblyPath = args[0];
var timeoutMs = args.Length > 1 && int.TryParse(args[1], out var t) ? t : 10_000;

// Internal watchdog — never let untrusted code run past the budget.
var watchdog = new Thread(() =>
{
    Thread.Sleep(timeoutMs + 500);
    Console.Out.Flush();
    WriteResult(new RunReport([], "", "watchdog: hard timeout", TimedOut: true));
    Environment.Exit(0);
})
{ IsBackground = true, Name = "forge-watchdog" };
watchdog.Start();

var results = new List<TestReport>();
var captured = new StringBuilder();

try
{
    var alc = new AssemblyLoadContext("forge-sandbox", isCollectible: true);
    var asm = alc.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));

    var factAttrName = "ForgeOps.Generated.ForgeFactAttribute";
    var criterionAttrName = "ForgeOps.Generated.CriterionAttribute";

    var originalOut = Console.Out;
    using var sink = new StringWriter(captured);

    foreach (var type in asm.GetTypes())
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        {
            var fact = method.GetCustomAttributes()
                .FirstOrDefault(a => a.GetType().FullName == factAttrName);
            if (fact is null)
            {
                continue;
            }

            var name = $"{type.Name}.{method.Name}";
            var criteria = method.GetCustomAttributes()
                .Where(a => a.GetType().FullName == criterionAttrName)
                .Select(a => a.GetType().GetProperty("Id")?.GetValue(a) as string)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToArray();

            var sw = Stopwatch.StartNew();
            try
            {
                Console.SetOut(sink);
                var instance = method.IsStatic ? null : Activator.CreateInstance(type);
                var invokeResult = method.Invoke(instance, null);
                if (invokeResult is Task task)
                {
                    task.GetAwaiter().GetResult();
                }

                sw.Stop();
                results.Add(new TestReport(name, "Passed", null, sw.Elapsed.TotalMilliseconds, criteria));
            }
            catch (Exception ex)
            {
                sw.Stop();
                var real = (ex as TargetInvocationException)?.InnerException ?? ex;
                results.Add(new TestReport(name, "Failed", $"{real.GetType().Name}: {real.Message}",
                    sw.Elapsed.TotalMilliseconds, criteria));
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}
catch (Exception ex)
{
    WriteResult(new RunReport(results, captured.ToString(),
        $"load/execute error: {ex.GetType().Name}: {ex.Message}", TimedOut: false));
    return 0;
}

WriteResult(new RunReport(results, captured.ToString(), "", TimedOut: false));
return 0;

static void WriteResult(RunReport report)
{
    var json = JsonSerializer.Serialize(report, SandboxJson.Options);
    Console.Out.Write("__FORGE_RESULT__");
    Console.Out.Write(json);
    Console.Out.Write("__END_FORGE_RESULT__");
    Console.Out.Flush();
}

internal sealed record TestReport(
    string Name, string Outcome, string? Message, double DurationMs, string[] Criteria);

internal sealed record RunReport(
    List<TestReport> Tests, string Stdout, string Error, bool TimedOut);

internal static class SandboxJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

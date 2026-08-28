using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

// ---------------------------------------------------------------------------
// ForgeOps sandbox runner.
//
// Launched as a short-lived child process by ForgeOps.Forge.SandboxRunner to
// execute an already-compiled, already-audited assembly and report results as
// JSON on stdout. It runs [ForgeFact] test methods and [ForgeScenario] scripted
// walkthroughs. It is deliberately tiny and dependency-free.
//
//   args[0] = path to the compiled assembly
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

var watchdog = new Thread(() =>
{
    Thread.Sleep(timeoutMs + 500);
    Console.Out.Flush();
    WriteResult(new RunReport([], [], "", "watchdog: hard timeout", TimedOut: true));
    Environment.Exit(0);
})
{ IsBackground = true, Name = "forge-watchdog" };
watchdog.Start();

var tests = new List<TestReport>();
var scenarioSteps = new List<string[]>();
var captured = new StringBuilder();

try
{
    var alc = new AssemblyLoadContext("forge-sandbox", isCollectible: true);
    var asm = alc.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));

    const string factAttr = "ForgeOps.Generated.ForgeFactAttribute";
    const string scenarioAttr = "ForgeOps.Generated.ForgeScenarioAttribute";
    const string criterionAttr = "ForgeOps.Generated.CriterionAttribute";

    var originalOut = Console.Out;
    using var sink = new StringWriter(captured);

    var types = asm.GetTypes();

    foreach (var type in types)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        {
            var attrs = method.GetCustomAttributes().ToArray();
            if (!attrs.Any(a => a.GetType().FullName == factAttr))
            {
                continue;
            }

            var name = $"{type.Name}.{method.Name}";
            var criteria = attrs
                .Where(a => a.GetType().FullName == criterionAttr)
                .Select(a => a.GetType().GetProperty("Id")?.GetValue(a) as string)
                .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToArray();

            var sw = Stopwatch.StartNew();
            try
            {
                Console.SetOut(sink);
                Invoke(type, method);
                sw.Stop();
                tests.Add(new TestReport(name, "Passed", null, sw.Elapsed.TotalMilliseconds, criteria));
            }
            catch (Exception ex)
            {
                sw.Stop();
                var real = (ex as TargetInvocationException)?.InnerException ?? ex;
                tests.Add(new TestReport(name, "Failed", $"{real.GetType().Name}: {real.Message}",
                    sw.Elapsed.TotalMilliseconds, criteria));
            }
            finally { Console.SetOut(originalOut); }
        }
    }

    // Scenario walkthroughs — run after tests; collect Scenario.Steps even on partial failure.
    foreach (var type in types)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        {
            if (!method.GetCustomAttributes().Any(a => a.GetType().FullName == scenarioAttr))
            {
                continue;
            }

            try
            {
                Console.SetOut(sink);
                Invoke(type, method);
            }
            catch (Exception ex)
            {
                var real = (ex as TargetInvocationException)?.InnerException ?? ex;
                scenarioSteps.Add(["(scenario faulted)", $"{real.GetType().Name}: {real.Message}"]);
            }
            finally { Console.SetOut(originalOut); }
        }
    }

    scenarioSteps.InsertRange(0, ReadScenarioSteps(asm));
}
catch (Exception ex)
{
    WriteResult(new RunReport(tests, scenarioSteps, captured.ToString(),
        $"load/execute error: {ex.GetType().Name}: {ex.Message}", TimedOut: false));
    return 0;
}

WriteResult(new RunReport(tests, scenarioSteps, captured.ToString(), "", TimedOut: false));
return 0;

static void Invoke(Type type, MethodInfo method)
{
    var instance = method.IsStatic ? null : Activator.CreateInstance(type);
    var result = method.Invoke(instance, null);
    if (result is Task task)
    {
        task.GetAwaiter().GetResult();
    }
}

static List<string[]> ReadScenarioSteps(Assembly asm)
{
    var steps = new List<string[]>();
    var scenarioType = asm.GetType("ForgeOps.Generated.Scenario");
    var field = scenarioType?.GetField("Steps", BindingFlags.Public | BindingFlags.Static);
    if (field?.GetValue(null) is System.Collections.IEnumerable list)
    {
        foreach (var item in list)
        {
            if (item is string[] pair && pair.Length >= 2)
            {
                steps.Add([pair[0], pair[1]]);
            }
        }
    }

    return steps;
}

static void WriteResult(RunReport report)
{
    Console.Out.Write("__FORGE_RESULT__");
    Console.Out.Write(JsonSerializer.Serialize(report, SandboxJson.Options));
    Console.Out.Write("__END_FORGE_RESULT__");
    Console.Out.Flush();
}

internal sealed record TestReport(
    string Name, string Outcome, string? Message, double DurationMs, string[] Criteria);

internal sealed record RunReport(
    List<TestReport> Tests, List<string[]> Scenario, string Stdout, string Error, bool TimedOut);

internal static class SandboxJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

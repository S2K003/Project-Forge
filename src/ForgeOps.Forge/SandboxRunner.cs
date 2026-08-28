using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ForgeOps.Contracts.Forge;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ForgeOps.Forge;

/// <summary>
/// Runs an already-compiled, already-audited test assembly in a short-lived child process
/// (<c>ForgeOps.Forge.Sandbox</c>) with a wall-clock budget and process-tree kill. The
/// child has only the curated reference set the assembly was compiled against.
/// </summary>
public sealed class SandboxRunner
{
    private const string ResultStart = "__FORGE_RESULT__";
    private const string ResultEnd = "__END_FORGE_RESULT__";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly CodeRunnerOptions _options;
    private readonly ILogger<SandboxRunner> _logger;
    private readonly Lazy<string?> _sandboxPath;

    public SandboxRunner(IOptions<CodeRunnerOptions> options, ILogger<SandboxRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
        _sandboxPath = new Lazy<string?>(() => ResolveSandbox(_options.SandboxAssemblyPath));
    }

    public bool Available => _options.Enabled && _sandboxPath.Value is not null;

    public async Task<TestRunResult> RunAsync(
        TestSuiteKind suite,
        byte[] assemblyImage,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return NotExecuted(suite, "Code runner is disabled on this host.");
        }

        if (_sandboxPath.Value is null)
        {
            return NotExecuted(suite, "Sandbox executable was not found next to the host.");
        }

        var workDir = Path.Combine(Path.GetTempPath(), "forgeops-run", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(workDir);
        var assemblyPath = Path.Combine(workDir, "Generated.dll");
        var timeoutMs = Math.Max(2, _options.TimeoutSeconds) * 1000;

        try
        {
            await File.WriteAllBytesAsync(assemblyPath, assemblyImage, cancellationToken);

            var psi = BuildStartInfo(_sandboxPath.Value, assemblyPath, timeoutMs, workDir);
            using var process = new Process { StartInfo = psi };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

            var sw = Stopwatch.StartNew();
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs + 3000);

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return new TestRunResult
                {
                    Suite = suite,
                    Executed = true,
                    TimedOut = true,
                    RunnerDetail = $"Sandbox exceeded {_options.TimeoutSeconds}s and was terminated.",
                    Stdout = Trim(stdout)
                };
            }

            sw.Stop();
            return Parse(suite, stdout.ToString(), stderr.ToString(), sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Sandbox run failed for suite {Suite}", suite);
            return NotExecuted(suite, $"Sandbox failed to start: {ex.Message}");
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    private static ProcessStartInfo BuildStartInfo(string sandbox, string assemblyPath, int timeoutMs, string workDir)
    {
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (sandbox.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            psi.FileName = "dotnet";
            psi.ArgumentList.Add("exec");
            psi.ArgumentList.Add(sandbox);
        }
        else
        {
            psi.FileName = sandbox;
        }

        psi.ArgumentList.Add(assemblyPath);
        psi.ArgumentList.Add(timeoutMs.ToString());

        // Reduce the child's ambient capability a little.
        psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        psi.Environment["DOTNET_NOLOGO"] = "1";
        return psi;
    }

    private static TestRunResult Parse(TestSuiteKind suite, string stdout, string stderr, double elapsedMs)
    {
        var start = stdout.IndexOf(ResultStart, StringComparison.Ordinal);
        var end = stdout.IndexOf(ResultEnd, StringComparison.Ordinal);
        if (start < 0 || end < 0 || end <= start)
        {
            return NotExecuted(suite,
                $"Sandbox produced no result payload. stderr: {Trim(new StringBuilder(stderr))}");
        }

        var jsonSpan = stdout[(start + ResultStart.Length)..end];
        SandboxReport? report;
        try
        {
            report = JsonSerializer.Deserialize<SandboxReport>(jsonSpan, Json);
        }
        catch (JsonException ex)
        {
            return NotExecuted(suite, $"Could not parse sandbox result: {ex.Message}");
        }

        if (report is null)
        {
            return NotExecuted(suite, "Sandbox result was empty.");
        }

        var results = report.Tests.Select(t => new TestResult
        {
            Name = t.Name,
            Outcome = t.Outcome switch
            {
                "Passed" => TestOutcome.Passed,
                "Failed" => TestOutcome.Failed,
                _ => TestOutcome.Skipped
            },
            Message = t.Message,
            DurationMs = t.DurationMs,
            Criteria = t.Criteria ?? []
        }).ToList();

        return new TestRunResult
        {
            Suite = suite,
            Executed = true,
            Results = results,
            Passed = results.Count(r => r.Outcome == TestOutcome.Passed),
            Failed = results.Count(r => r.Outcome == TestOutcome.Failed),
            Skipped = results.Count(r => r.Outcome == TestOutcome.Skipped),
            DurationMs = elapsedMs,
            TimedOut = report.TimedOut,
            Stdout = report.Stdout,
            RunnerDetail = string.IsNullOrWhiteSpace(report.Error)
                ? $"{results.Count} test(s) in {elapsedMs:0} ms"
                : report.Error
        };
    }

    private static TestRunResult NotExecuted(TestSuiteKind suite, string detail) => new()
    {
        Suite = suite,
        Executed = false,
        RunnerDetail = detail
    };

    private static string Trim(StringBuilder sb)
    {
        var s = sb.ToString().Trim();
        return s.Length > 4000 ? s[..4000] + "…" : s;
    }

    private void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill sandbox process tree.");
        }
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // best effort — temp cleanup
        }
    }

    private static string? ResolveSandbox(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var baseDir = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDir, "sandbox", "ForgeOps.Forge.Sandbox.dll"),
            Path.Combine(baseDir, "sandbox", OperatingSystem.IsWindows() ? "ForgeOps.Forge.Sandbox.exe" : "ForgeOps.Forge.Sandbox"),
            Path.Combine(baseDir, "ForgeOps.Forge.Sandbox.dll"),
        ];

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Dev fallback: walk up looking for the sandbox build output.
        var dir = new DirectoryInfo(baseDir);
        for (var i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            var match = dir.GetDirectories("ForgeOps.Forge.Sandbox", SearchOption.AllDirectories)
                .SelectMany(d => d.GetFiles("ForgeOps.Forge.Sandbox.dll", SearchOption.AllDirectories))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (match is not null)
            {
                return match.FullName;
            }
        }

        return null;
    }

    private sealed record SandboxReport(
        List<SandboxTest> Tests, string Stdout, string Error, bool TimedOut);

    private sealed record SandboxTest(
        string Name, string Outcome, string? Message, double DurationMs, string[]? Criteria);
}

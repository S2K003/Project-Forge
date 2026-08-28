using ForgeOps.Forge;

namespace ForgeOps.UnitTests;

public sealed class BannedApiScannerTests
{
    private static IReadOnlyList<ForgeOps.Contracts.Forge.BannedApiFinding> Scan(string code) =>
        BannedApiScanner.Scan(new Dictionary<string, string> { ["T.cs"] = code });

    [Fact]
    public void Clean_domain_code_has_no_findings()
    {
        var findings = Scan(
            """
            namespace X;
            using System.Collections.Generic;
            public sealed class Calc
            {
                private readonly Dictionary<string,int> _m = new();
                public int Add(int a, int b) => a + b;
            }
            """);

        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("using System.IO; class C { void M() => File.ReadAllText(\"x\"); }")]
    [InlineData("using System.Net.Http; class C { System.Net.Http.HttpClient h = new(); }")]
    [InlineData("using System.Diagnostics; class C { void M() => System.Diagnostics.Process.Start(\"x\"); }")]
    [InlineData("class C { [System.Runtime.InteropServices.DllImport(\"k\")] static extern void N(); }")]
    [InlineData("class C { unsafe void M() { int* p = null; } }")]
    public void Dangerous_apis_are_flagged(string code) =>
        Assert.NotEmpty(Scan(code));
}

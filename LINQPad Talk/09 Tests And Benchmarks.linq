<Query Kind="Program" />

#load "SlideKit.linq"

//  ═══ SLIDE 9 · TESTS & BENCHMARKS ═══ budget 2:00 ═══  ← CUT SECOND IF LONG
//
//  NOTE: shown as code rather than executed, deliberately. Adding
//        #load "xunit" here would trigger a package restore mid-talk, and
//        BenchmarkDotNet takes ~40s to produce a result. Neither is stage-safe.
//
//  ALTERNATIVE (safer and more impressive if you have 30 spare seconds):
//        Script menu → "Add XUnit Test Support" / "Add Programmatic Benchmark
//        Support" wires it up for you. Or highlight any method and press
//        Ctrl+Shift+B to benchmark just that selection.

void Main()
{
    Slide("Tests & Benchmarks", "without creating a test project");

    ShowCode("""
        #load "xunit"          // ← that is the entire setup

        using Xunit;

        void Main() => RunTests();

        [Fact]
        void Addition_Works() => Assert.Equal(4, 2 + 2);

        [Theory, InlineData(1), InlineData(2)]
        void Positives(int n) => Assert.True(n > 0);
        """, "xunit — one directive  (Alt+Shift+T to run)");

    ShowCode("""
        #load "BenchmarkDotNet"
        
        using BenchmarkDotNet.Attributes;
        
        void Main() => RunBenchmark();
        
        IEnumerable<string> Data() => Enumerable.Range(0, 999).Select(i => ((char)('a' + i % 26)).ToString());
        
        [Benchmark] public string Concat() => string.Concat(Data());
        
        [Benchmark]
        public string Plus()
        {
            var result = "";
            foreach (var s in Data()) result += s;
            return result;
        }
        """, "BenchmarkDotNet — one directive");

    Points(
        "Full xunit and BenchmarkDotNet. No `.csproj`, no test host, no `dotnet add package`.",
        "**`Ctrl+Shift+B`** benchmarks whatever code you have selected. That's it.",
        "**Use case:** settle the performance argument *during* the meeting, not after it.",
        "Plus a real **debugger** — F9 breakpoints, F10/F11 stepping, watches — in the scratchpad.");

    Nav(9, "AI", TimeSpan.FromMinutes(2));
}

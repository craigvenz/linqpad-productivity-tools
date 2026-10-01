<Query Kind="Program" />

#load "SlideKit.linq"

//  ═══ SLIDE 7 · CHARTS ═══ budget 2:00 ═══  ← CUT THIS FIRST IF RUNNING LONG
//
//  DO : Hover the chart. Toggle a legend item. It's live ECharts, not a PNG.

void Main()
{
    Slide("Charts", "`.Chart()` on any sequence");

    var months   = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun" };
    var revenue  = new[] { 120, 132, 101, 134, 190, 230 };
    var expenses = new[] {  90, 100,  85,  95, 120, 140 };

    months.Chart()
          .AddYSeries(revenue,  Util.SeriesType.Column, "Revenue")
          .AddYSeries(expenses, Util.SeriesType.Spline, "Expenses")
          .ToEChart("Hover me. Toggle the legend. I'm interactive.", "620px", "300px")
          .Dump();

    // One-line frequency analysis — no GroupBy required:
    "the quick brown fox jumps over the lazy dog".Where(char.IsLetter)
        .Chart().ToEChart("Letter frequency — that's the whole line of code", "620px", "260px").Dump();

    Points(
        "Backed by Apache ECharts: zoom, tooltips, legend toggles, SVG/PNG export.",
        "`.Dump()` for its own tab · `.DumpInline()` to keep it in the results · `.ToEChart()` to compose with controls.",
        "`eChart.Update(newChart)` does a React-style DOM diff — **live-updating dashboards in a scratchpad**.");

    Nav(7, "Delete Your PowerShell", TimeSpan.FromMinutes(1));
}

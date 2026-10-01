<Query Kind="Program">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

#load "SlideKit.linq"

//  ═══ SLIDE 10 · AI ═══ budget 2:00 ═══
//
//  ⚠ PREP: needs an AI provider configured (AI menu → AI Settings). TEST IT
//    THE MORNING OF. If it's not working on the day, comment out the live
//    call at the bottom and just talk over the code slide — it still lands.
//
//  DO : If you're feeling brave, press Ctrl+I in your connected DB tab and
//       ask it "chart last quarter's orders by region". Highest-risk,
//       highest-reward moment in the talk. Only do it if you rehearsed it.

async Task Main()
{
    Slide("AI", "two flavors, and the second one is underrated");

    Points(
        "**`Ctrl+I`** — a coding agent that knows every LINQPad API *and your live DB schema*.",
        "**`Ctrl+Shift+K`** — Claude Code, right in the editor.",
        "**And** AI as a runtime primitive — `Util.AI.Ask(...)` — for when the *script* needs a model.");

    var codeBlock = """
record alertsImport(string Name, Guid AlertTypeCategoryId, int DirectionOfTravel, string GeometryType, double Lat, double Lng, string MetadataUI, bool Deleted);
record metadata(string description);
async Task Main()
{
    var rows = new CsvReader(
        File.OpenText("C:\\Users\\Craig.Venz\\Downloads\\alerts-import.csv"),
        new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture){HasHeaderRecord = true}
    );
    var model = LINQPad.ObjectModel.AI.AIProvider.Default.Models.FirstOrDefault(m => m.ID == "gemini-3.5-flash");
    var records = rows.GetRecords<alertsImport>().ToList();
    var progress = new Util.ProgressBar("Querying AI...").Dump();
    var done = 0;
    var tasks = records.Select(async r =>
    {
        var md = JsonSerializer.Deserialize<metadata>(r.MetadataUI);
        // AI as a library call, not a chat window:
        var response = await Util.AI
            .Ask($"One word sentiment: {md.description}", reasoningEffort: 0.5, model)
            .GetResponseAsync(ScriptCancelToken);
        progress.Percent = Interlocked.Increment(ref done) * 100 / records.Count;
        return new { row = r, Sentiment = response.Text };
    }).ToList();
    var tagged = await Task.WhenAll(tasks);
    tagged.Chart(t => t.Sentiment).Dump();
}
""";
    ShowCode(codeBlock,
        $"Classify a CSV and chart it, responsively, in {codeBlock.Split('\n').Count()} lines",
        nugetRefs:  ["CsvHelper"],
        namespaces: ["CsvHelper","System.Text.Json"]
    );

    const string prompt = "In exactly two sentences, roast C# developers who still use Console.WriteLine to debug.";
    var outer = new DumpContainer(prompt).Dump("Streaming, with a Cancel button, for free");
    var inner = new DumpContainer();
    outer.AppendContent(inner);
    await Util.AI.Ask(prompt, reasoningEffort: 0.7).GetResponseAsync(inner, ScriptCancelToken);

    Meme("Built an LLM pipeline", "It was one method call", "", imageFromBytes: File.ReadAllBytes("C:\\Users\\Craig.Venz\\Downloads\\antonio.gif"));

    Nav(10, "The Ask", TimeSpan.FromMinutes(2));
}

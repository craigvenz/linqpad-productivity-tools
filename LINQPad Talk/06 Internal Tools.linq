<Query Kind="Program">
  <NuGetReference>HtmlAgilityPack</NuGetReference>
  <Namespace>HtmlAgilityPack</Namespace>
  <Namespace>LINQPad.Controls</Namespace>
</Query>

#load "SlideKit.linq"

//  ═══ SLIDE 6 · INTERNAL TOOLS ═══ budget 3:00 ═══
//
//  DO : Run it, then ACTUALLY USE IT on stage. Type in the search box.
//       Click the button. Live interaction beats any screenshot.
//
//  SAY: "This is how the ops team gets their tool. Today. Not next sprint."

void Main()
{
    Slide("Internal Tools", "in thirty lines, with no front end. It was eleven lines before I added the highlighting. 🫠");

    // --- 6a. Live search over a dataset -------------------------------------
    var zones   = TimeZoneInfo.GetSystemTimeZones();
    var results = new DumpContainer();
    var search  = new TextBox(width: "24em") { Placeholder = "type to filter time zones…" };
    object Highlight(object data, string text, bool caseInsensitive = false)
    {
        if (string.IsNullOrEmpty(text)) return data;
        var html = Util.ToHtmlString(data);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var cells = from tableCell in doc.DocumentNode.SelectNodes("//td") ?? Enumerable.Empty<HtmlNode>()
                    where tableCell.InnerText.Contains(text, StringComparison.OrdinalIgnoreCase)
                    select tableCell;

        foreach (var cn in cells)
            cn.InnerHtml = Regex.Replace(cn.InnerHtml, $"({Regex.Escape(text)})", "<span class='highlight' style='background:yellow'>$1</span>", caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None);

        return Util.RawHtml(doc.DocumentNode.OuterHtml);
    }

    void Refresh() => results.Content = 
        Highlight(
            zones.Where(z => z.DisplayName.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase) || z.Id.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase))
                 .Select(z => new { z.Id, z.DisplayName, z.BaseUtcOffset })
        , search.Text ?? "", caseInsensitive: true);

    search.TextInput += (s, e) => Refresh();
    Refresh();

    new FieldSet("Live search — no HTML, no JS, no build step", search, results).Dump();

    Points(
        "Real HTML controls: `TextBox` `Button` `SelectBox` `CheckBox` `Table` `TabControl` `FilePicker` `FlexBox`…",
        "`DumpContainer` swaps content in place. `IsMultithreaded = true` keeps the UI live mid-run.",
        "Also available: `Util.ProgressBar`, `Hyperlinq`, `Util.Markdown`, `IFrame`, `Image`, `Svg`, `Canvas`, `Video`.",
        "**You can ship an internal tool as a .linq file.** `LPRun9-x64.exe tool.linq` for the non-developers.");

    new Literal("<hr/>").Dump();
    new Div(
        new Control("h3", new Span("Other Demos")),
        new Control("ul",
            new[] { ("View live azure function app filesystem","vfsviewer.linq"),
                    ("JWT Decoder", "decode jwt.linq"),
                    ("Hex Viewer", "hex viewer.linq"),
                    ("Timer Bar", "timer bar with progress.linq"),
                    ("Show Low Clearance Bridges on Live Map", "low clearance bridges map.linq")
                    }
            .Select(li => new Control("li", new Hyperlink(li.Item1, _ => Util.OpenScript(li.Item2) )))
        )
    ).Dump();

    Meme("Ops needs one button", "Six weeks of React", "", "", File.ReadAllBytes("C:\\Users\\Craig.Venz\\Downloads\\ghost-in.gif"));

    Nav(6, "Charts", TimeSpan.FromMinutes(3));
}

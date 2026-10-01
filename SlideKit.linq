<Query Kind="Statements">
  <NuGetReference>System.Reactive</NuGetReference>
  <Namespace>System.Drawing.Imaging</Namespace>
</Query>

using System.Reactive.Linq;

// ═══════════════════════════════════════════════════════════════════════════
//  SlideKit — shared presentation chrome for the "LINQPad Talk" folder.
//
//  Every slide script starts with:   #load "SlideKit.linq"
//  (#load with a bare filename resolves relative to your My Scripts folder,
//   which is why this file lives in the root and not in the talk folder —
//   keeping the talk folder clean for clicker navigation.)
//
//  Nav() shows a live budget clock (Rx + DumpContainer): counts down, then
//  turns red and counts up once you're over. It keeps the script alive via
//  Util.KeepRunning — press Stop (or run the next slide) to end it.
//
//  ShowCode() pretty-prints a snippet *and* offers a link that writes it to a
//  temp .linq file and opens it in a fresh tab — so you can F5 it live.
//
//  Bonus: this file is itself a demo. Mention it on slide 08.
// ═══════════════════════════════════════════════════════════════════════════

int TotalSlides = Util.Cache(() =>
    Util.GetMyScripts(includeNonLinqFiles: false, includeMyExtensions: true, includeExtraScriptFolders: true)
    .Count(f => f.Location.StartsWith(@"\LINQPad Talk"))
);

// Big centred title card.
void Slide(string title, string? subtitle = null)
{
    Util.HideEditor();
    var html = $"""
        <div style='font-family:Segoe UI,system-ui;text-align:center;padding:1.1em 0'>
          <div style='font-size:34pt;font-weight:700;letter-spacing:-1px;line-height:1.15'>{title}</div>
          {(subtitle is null ? "" : $"<div style='font-size:15pt;opacity:.65;margin-top:.45em'>{subtitle}</div>")}
        </div>
        """;
    Util.RawHtml(html).Dump();
}

// Impact-font meme card.
void Meme(string top, string bottom, string emoji = "🤔", string image = "", byte[]? imageFromBytes = null)
{
    var html = new StringBuilder();
    html.AppendFormat("""
        <div style="display:inline-block;background:linear-gradient(145deg,#2b2b3a,#111);
                    border-radius:10px;padding:18px 34px;margin:10px 0;min-width:460px;text-align:center">
          <div style="font:700 21pt Impact,Haettenschweiler,sans-serif;color:#fff;text-transform:uppercase;
                      text-shadow:2px 2px 0 #000,-2px -2px 0 #000,2px -2px 0 #000,-2px 2px 0 #000">{0}</div>
""", top);
    if (!string.IsNullOrEmpty(emoji))
        html.AppendFormat(@"          <div style=""font-size:44pt;line-height:1.1;margin:6px 0"">{0}</div>", emoji);
    if (!string.IsNullOrEmpty(image))
        html.AppendFormat(@"          <div><img src=""{0}"" style=""width:75%;height:auto;display:block;margin:6px auto""></div>", image);
    if (imageFromBytes != null)
        html.AppendFormat(@"          <div><img src=""{0}"" style=""width:75%;height:auto;display:block;margin:6px auto""></div>",
        ImageToBase64(imageFromBytes));
    html.AppendFormat("""
          <div style="font:700 21pt Impact,Haettenschweiler,sans-serif;color:#fff;text-transform:uppercase;
                      text-shadow:2px 2px 0 #000,-2px -2px 0 #000,2px -2px 0 #000,-2px 2px 0 #000">{0}</div>
        </div>
        """, bottom);
    Util.RawHtml(html.ToString()).Dump();
}

string ImageToBase64(byte[] bytes)
{
    using var di = System.Drawing.Image.FromStream(new MemoryStream(bytes));
    var format = ImageCodecInfo.GetImageEncoders().FirstOrDefault(ici => ici.FormatID == di.RawFormat.Guid);
    var sb = new StringBuilder();
    sb.AppendFormat(" data:{0};base64,", format?.MimeType ?? throw new ArgumentException("Unknown image format"));
    sb.Append(Convert.ToBase64String(bytes));
    return sb.ToString();
}

// Talking-point bullets (markdown, so **bold** and `code` work).
void Points(params string[] bullets) =>
    Util.Markdown(string.Join("\n", bullets.Select(b => "- " + b))).Dump();

// Show code as a slide without executing it, plus a link to open it for real.
void ShowCode(string code, string? heading = null, string[]? nugetRefs = null, string[]? namespaces = null)
{
    Util.SyntaxColorText(code, SyntaxLanguageStyle.CSharp).Dump(heading);
    new Hyperlinq(() => OpenInNewTab(code, heading, nugetRefs, namespaces), "▶ open in a new tab").Dump();
}

// Writes a snippet to a temp .linq file and opens it in a new LINQPad tab.
void OpenInNewTab(string code, string? heading = null, string[]? nugetRefs = null, string[]? namespaces = null)
{
    var name = Regex.Replace(heading ?? "Snippet", @"[^\w.-]+", "_");
    var path = Path.Combine(Path.GetTempPath(), $"{name}-{DateTime.Now:HHmmss}.linq");
    var refs = new HashSet<string>(nugetRefs ?? Enumerable.Empty<string>());
    var names = new HashSet<string>((namespaces ?? Enumerable.Empty<string>()).Concat([
        "System",
        "System.Linq",
        "System.Collections.Generic",
        "System.Threading.Tasks"
    ]));
    var frontMatter = new XElement("Query", new XAttribute("Kind", "Program"),
        refs.Select(r => new XElement("NuGetReference", r)),
        names.Select(ns => new XElement("Namespace", ns))
    ).ToString();
    File.WriteAllText(path, $"{frontMatter}\n{code}");
    Util.OpenScript(path, run: true);
}

// Presenter footer: where am I, what's next, how long have I got.
void Nav(int slideNo, string nextUp, TimeSpan? budget = null)
{
    var dc = new DumpContainer().Dump();
    
    void Render(string budgetHtml) => dc.Content = Util.RawHtml($"""
        <div style="font-family:Segoe UI,system-ui;font-size:9.5pt;opacity:.5;
                    border-top:1px solid #8884;margin-top:1.4em;padding-top:.5em">
          Slide {slideNo} of {TotalSlides}{budgetHtml}
          &nbsp;·&nbsp; next: <b>{nextUp}</b>
        </div>
        """);

    if (budget is null) { Render(""); return; }

    var deadline = DateTime.UtcNow + budget.Value;

    var ticker = Observable
        .Interval(TimeSpan.FromSeconds(1))
        .StartWith(-1L)                                 // render immediately
        .Select(_ => deadline - DateTime.UtcNow)
        .Subscribe(left =>
        {
            bool over = left <= TimeSpan.Zero;
            var span = TimeSpan.FromSeconds(over ? Math.Floor(-left.TotalSeconds)
                                                 : Math.Ceiling(left.TotalSeconds));
            var clock = span.ToString(span.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");
            var colour = over ? "#e33" : "inherit";
            Render($" &nbsp;·&nbsp; budget <b style='color:{colour}'>{(over ? "+" : "")}{clock}</b>");
        });

    Util.Cleanup += (s, e) => ticker.Dispose();
}

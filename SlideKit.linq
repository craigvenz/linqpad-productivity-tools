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
//  turns red and counts up once you're over.
//
//  ShowCode() pretty-prints a snippet *and* offers a link that writes it to a
//  temp .linq file and opens it in a fresh tab — so you can F5 it live.
//
//  Bonus: this file is itself a demo.
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

// Impact-font meme card. `image` is a URL/path; `imageFromBytes` is embedded as a data: URI.
// Supports GIF/PNG/JPEG/WebP/AVIF/BMP via <img>, and WebM/MP4 via a looping, muted <video>.
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
        html.Append(MediaTag(image, IsVideoPath(image)));
    if (imageFromBytes != null)
    {
        var mime = SniffMimeType(imageFromBytes);
        html.Append(MediaTag(ToDataUri(imageFromBytes, mime), mime.StartsWith("video/")));
    }
    html.AppendFormat("""
          <div style="font:700 21pt Impact,Haettenschweiler,sans-serif;color:#fff;text-transform:uppercase;
                      text-shadow:2px 2px 0 #000,-2px -2px 0 #000,2px -2px 0 #000,-2px 2px 0 #000">{0}</div>
        </div>
        """, bottom);
    Util.RawHtml(html.ToString()).Dump();
}

// <img> for images, <video autoplay loop muted> for video (WebView2 won't animate WebM in an <img>).
string MediaTag(string src, bool isVideo)
{
    const string style = "width:75%;height:auto;display:block;margin:6px auto";
    var encoded = System.Net.WebUtility.HtmlEncode(src);
    return isVideo
        ? $"""          <div><video src="{encoded}" autoplay loop muted playsinline style="{style}"></video></div>"""
        : $"""          <div><img src="{encoded}" style="{style}"></div>""";
}

bool IsVideoPath(string path) =>
    Path.GetExtension(path.Split('?', '#')[0]).ToLowerInvariant() is ".webm" or ".mp4" or ".mkv" or ".mov";

// Returns an html-embeddable data: URI (image or video) from raw bytes.
string ImageToBase64(byte[] bytes) => ToDataUri(bytes, SniffMimeType(bytes));

string ToDataUri(byte[] bytes, string mime) => $"data:{mime};base64,{Convert.ToBase64String(bytes)}";

// Identify the format from magic bytes (System.Drawing/GDI+ can't read WebP, AVIF or WebM).
string SniffMimeType(byte[] b)
{
    bool At(int offset, string ascii) =>
        b.Length >= offset + ascii.Length && ascii.Select((c, i) => b[offset + i] == c).All(x => x);

    if (At(0, "GIF87a") || At(0, "GIF89a"))            return "image/gif";
    if (At(0, "\x89PNG"))                              return "image/png";
    if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
    if (At(0, "RIFF") && At(8, "WEBP"))                return "image/webp";
    if (At(4, "ftyp"))                                 // ISO-BMFF: AVIF or MP4
        return At(8, "avif") || At(8, "avis") ? "image/avif" : "video/mp4";
    if (b.Length >= 4 && b[0] == 0x1A && b[1] == 0x45 && b[2] == 0xDF && b[3] == 0xA3)
        return "video/webm";                           // EBML header (WebM/Matroska)
    if (At(0, "BM"))                                   return "image/bmp";
    throw new ArgumentException("Unrecognised image/video format");
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
          Slide {slideNo+1} of {TotalSlides}{budgetHtml}
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

<Query Kind="Statements">
  <Namespace>LINQPad.Controls</Namespace>
  <AutoDumpHeading>true</AutoDumpHeading>
</Query>

// Pick an image/video file and get an inline base64 data: URI (optionally wrapped in an HTML tag).
// Supports GIF/PNG/JPEG/WebP/AVIF/BMP (-> <img>) and WebM/MP4 (-> <video autoplay loop muted playsinline>).
// Format is detected from magic bytes, since System.Drawing/GDI+ can't read WebP, AVIF or WebM.

var fp = new FilePicker();
var addTag = new CheckBox("Add <img>/<video> tag", true);
var split = new CheckBox("Split into rows", false);
new StackPanel(true, fp, addTag, split).Dump();
var info = new DumpContainer();
var txt = new TextArea();
var dc = new DumpContainer();
info.Dump();
new StackPanel(true, txt, dc).Dump();

new Button("Get Base64 Embed", _ =>
{
    try
    {
        var bytes = File.ReadAllBytes(fp.Text);
        var mime = SniffMimeType(bytes);
        var isVideo = mime.StartsWith("video/");
        var base64 = Convert.ToBase64String(bytes);
        var dataUri = $"data:{mime};base64,{base64}";

        dc.Content = Util.RawHtml(MediaTag(dataUri, isVideo, "max-width:480px;height:auto"));
        info.Content = $"{mime} · file {bytes.Length / 1024.0:N0} KB · base64 {base64.Length / 1024.0:N0} KB";

        var sb = new StringBuilder();
        if (addTag.Checked)
            sb.Append(isVideo ? "<video autoplay loop muted playsinline src=\"" : "<img src=\"");
        sb.Append($"data:{mime};base64,");
        if (split.Checked)
        {
            // Whitespace inside a data: URI / Convert.FromBase64String is ignored, so splitting is safe.
            for (int i = 0; i < base64.Length; i += 76)
                sb.AppendLine(base64.Substring(i, Math.Min(76, base64.Length - i)));
        }
        else
            sb.Append(base64);
        if (addTag.Checked)
            sb.Append(isVideo ? "\"></video>" : "\" />");
        txt.Text = sb.ToString();
    }
    catch (Exception ex)
    {
        ex.Dump();
    }
}).Dump();

string MediaTag(string src, bool isVideo, string style)
{
    var encoded = System.Net.WebUtility.HtmlEncode(src);
    return isVideo
        ? $"""<video src="{encoded}" autoplay loop muted playsinline style="{style}"></video>"""
        : $"""<img src="{encoded}" style="{style}">""";
}

// Identify the format from magic bytes.
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

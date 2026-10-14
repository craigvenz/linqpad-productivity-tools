<Query Kind="Program">
  <NuGetReference>Humanizer.Core</NuGetReference>
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <NuGetReference>R3</NuGetReference>
  <Namespace>Humanizer</Namespace>
  <Namespace>LINQPad.Controls</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <DisableMyExtensions>true</DisableMyExtensions>
  <AutoDumpHeading>true</AutoDumpHeading>
</Query>

using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using R3;

void OnStart()
{
    if (Debugger.IsAttached)
        Util.KeepRunning();
    else
        Util.HideEditor();
}

DumpContainer status = new();

async Task Main()
{
    Util.HtmlHead.AddStyles(
        """
        table tr td:nth-child(2) { word-wrap: break-word; max-width: 1000px }
        table tr td:nth-child(2) pre,
        table tr td:nth-child(2) pre code {
            white-space: pre-wrap;
            word-wrap: break-word;
            overflow-wrap: anywhere;
            max-width: 1000px;
            max-height: 20em;
            overflow: auto;
            margin: 0;
        }
        """);
    
    var userDataPath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LINQPad", "UserData"));
    var userDataFiles = userDataPath
        .EnumerateFiles()
        .OrderByDescending(x => x.LastWriteTime);
    var passwordsPath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LINQPad", "Passwords"));
    new Table([
        new TableRow(true, new Span("UserData Folder Path"), new Span("Passwords Folder Path")),
        new TableRow(false, new DumpContainer(userDataPath), new DumpContainer(passwordsPath))
    ])
    .Dump();

    var loadStringDisplay = new DumpContainer().Dump();
    void DisplayLoadStringData()
    {
        loadStringDisplay.Content = Util.WithHeading(
            userDataFiles.Select(x => new
            {
                x.Name,
                Content = Preview(x),
                LastWrite = x.LastWriteTime.Humanize(),
                File = new Hyperlink("Open", _ => Shortcuts.OpenNotepadPP(x.FullName)),
                Delete = new Button("Delete", _ => DeleteUserData(new FileInfo(Path.Combine(userDataPath.FullName, x.Name))))
            }), "LoadString keys");

        void DeleteUserData(FileInfo f)
        {
            if (f.Exists)
                f.Delete();
            DisplayLoadStringData();
        }
    }
    DisplayLoadStringData();
    new Button("Export User Data", _ => {
        new TextArea(JsonConvert.SerializeObject(new { DataType = "userdata", Data = userDataFiles.ToDictionary(df => df.Name, df => Convert.ToBase64String(df.ReadAllBytes())) } ) ).Dump();
    }).Dump();
    

    // Deferred query - re-enumerated on every refresh so it always shows current state:
    var passwords = passwordsPath
    .EnumerateFiles()
    .OrderByDescending(x => x.LastWriteTime)
    .Select(x =>
    {
        var name = Encoding.UTF8.GetString(Enumerable.Range(0, x.Name.Length)
                                                     .Where(x => x % 2 == 0)
                                                     .Select(i => Convert.ToByte(x.Name.Substring(i, 2), 16))
                                                     .ToArray());
        return new {
            Name = name,
            LastWrite = x.LastWriteTime.Humanize(),
            Content = Util.OnDemand("Click to show", () => Util.GetPassword(name))
        };
    });

    var passwordsDisplay = new DumpContainer().Dump();
    void DisplayPasswords() => passwordsDisplay.Content = Util.WithHeading(passwords.ToList(), "Passwords");
    DisplayPasswords();
    
    new Button("Export Passwords", _ => {
        new TextArea(JsonConvert.SerializeObject(new { DataType="passwords", Data=passwords.ToDictionary(df => df.Name, df => Convert.ToBase64String(Encoding.UTF8.GetBytes(Util.GetPassword(df.Name)))) } )).Dump();
    }).Dump();

    // Live mode: watch both folders and refresh automatically until the Stop button is pressed.
    WatchFolder(userDataPath, DisplayLoadStringData);
    WatchFolder(passwordsPath, DisplayPasswords);

    status.Content  = "Watching for changes - press Stop to finish.";
}

const int PreviewChars  = 250;      // plain-text preview length
const int MaxJsonBytes  = 1 << 20;  // don't attempt to parse anything bigger than this
const int MaxJsonItems  = 5;        // max array elements shown per array
const int MaxJsonString = 100;      // max length of any single string value
const int MaxJsonLines  = 25;       // max lines of formatted JSON shown

// Returns a size-limited preview of a file: structurally trimmed + syntax-colored if it's JSON,
// otherwise the first PreviewChars characters. (Use the 'Open' link to see the whole file.)
object Preview(FileInfo file)
{
    var bytes = ReadCapped(file, MaxJsonBytes);

    if (file.Length <= MaxJsonBytes && bytes.AsSpan().IsJson())
        return Util.SyntaxColorText(Shrink(JToken.Parse(Encoding.UTF8.GetString(bytes))), SyntaxLanguageStyle.Json);

    var text = Encoding.UTF8.GetString(bytes);
    return text.Length <= PreviewChars ? text : text[..PreviewChars] + "…";
}

static byte[] ReadCapped(FileInfo file, int maxBytes)
{
    using var stream = file.OpenRead();
    var buffer = new byte[(int)Math.Min(file.Length, maxBytes)];
    stream.ReadExactly(buffer);
    return buffer;
}

// Trims the *structure* (not just the text) so that the JSON stays well-formed and colorizable.
static string Shrink(JToken token)
{
    Trim(token);
    var text = token.ToString(Newtonsoft.Json.Formatting.Indented);
    var lines = text.Split('\n');
    return lines.Length > MaxJsonLines
        ? string.Join("\n", lines.Take(MaxJsonLines)) + "\n…"
        : text;
}

static void Trim(JToken token)
{
    switch (token)
    {
        case JArray array:
            if (array.Count > MaxJsonItems)
            {
                var extra = array.Count - MaxJsonItems;
                foreach (var item in array.Skip(MaxJsonItems).ToList()) item.Remove();
                array.Add($"… {extra} more item(s)");
            }
            foreach (var item in array.Take(MaxJsonItems)) Trim(item);
            break;
        case JObject obj:
            foreach (var prop in obj.Properties().ToList()) Trim(prop.Value);
            break;
        case JValue { Value: string s } value when s.Length > MaxJsonString:
            value.Value = s[..MaxJsonString] + "…";
            break;
    }
}

// Watches a folder and invokes 'onChanged' (debounced) whenever anything changes in it.
// The watcher is torn down when the script is cancelled via the Stop button.
void WatchFolder(DirectoryInfo folder, Action onChanged)
{
    if (!folder.Exists) return;

    var watcher = new FileSystemWatcher(folder.FullName)
    {
        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        IncludeSubdirectories = false
    };

    static Observable<R3.Unit> FromFileSystemEvent(Action<FileSystemEventHandler> add, Action<FileSystemEventHandler> remove)
        => Observable.FromEvent<FileSystemEventHandler>(h => (s, e) => h(), add, remove);

    // Debounce: file writes typically raise several events in quick succession.
    var subscription = Observable.Merge(
            FromFileSystemEvent(h => watcher.Created += h, h => watcher.Created -= h),
            FromFileSystemEvent(h => watcher.Deleted += h, h => watcher.Deleted -= h),
            FromFileSystemEvent(h => watcher.Changed += h, h => watcher.Changed -= h),
            Observable.FromEvent<RenamedEventHandler>(h => (s, e) => h(), h => watcher.Renamed += h, h => watcher.Renamed -= h))
        .Debounce(TimeSpan.FromMilliseconds(300))
        .Subscribe(_ =>
        {
            try { onChanged(); }
            catch (Exception ex) { ex.Message.Dump("Refresh failed"); }
        });

    watcher.Error   += (s, e) => e.GetException().Message.Dump($"Watcher error ({folder.Name})");
    watcher.EnableRaisingEvents = true;

    ScriptCancelToken.Register(() =>
    {
        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
        subscription.Dispose();
        status.Content = "Stopped watching.";
    });
}

public static class Shortcuts
{
    static string Quote(string s) => s.Contains(" ") ? $"\"{s}\"" : s;
    public static void OpenNotepadPP(string f)
    {
        var npDir = Path.Combine("Notepad++", "notepad++.exe");
        var path = new string?[] {
            Environment.GetEnvironmentVariable("ProgramFiles"),
            Environment.GetEnvironmentVariable("ProgramFiles(x86)")
        }
        .OfType<string>()
        .Select(progDir => new FileInfo(Path.Combine(progDir, npDir)))
        .FirstOrDefault(x => x.Exists);
        if (path == null)
            throw new FileNotFoundException("Could not find NotePad++ installation.");
        Util.Cmd($"{Quote(path.FullName)} {Quote(f)}");
    }
    public static byte[] ReadAllBytes(this FileInfo f) => !f.Exists ? Array.Empty<byte>() : File.ReadAllBytes(f.FullName);
}

#region Embedded from My Extensions
public static class JsonExtensions
{

    /// <summary>Returns true if the given bytes represent well-formed JSON.</summary>
    public static bool IsJson(this Span<byte> data)
    {
        try
        {
            var reader = new System.Text.Json.Utf8JsonReader(data);
            reader.Read();
            reader.Skip();
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}

public static class ConvenienceMethods
{
    public static Hyperlink ToggleBetweenDistinct(params object[] items)
        => ToggleBetween(items.Where(x => x != null).DistinctBy(x => x.ToString()).ToArray());
    public static Hyperlink ToggleBetween(params object[] items)
    {
        int index = 0;
        return new Hyperlink(items[index].ToString(), x =>
        {
            index = (index + 1) % items.Length;
            x.Text = items[index].ToString();
        });
    }
}
#endregion
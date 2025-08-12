<Query Kind="Program">
  <NuGetReference>Humanizer.Core</NuGetReference>
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <Namespace>Humanizer</Namespace>
  <Namespace>LINQPad.Controls</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
  <DisableMyExtensions>true</DisableMyExtensions>
  <AutoDumpHeading>true</AutoDumpHeading>
</Query>

void Main()
{
    Util.HtmlHead.AddStyles("table tr td:nth-child(2) { word-wrap: break-word; max-width: 1000px }");
    
	var userDataPath = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LINQPad", "UserData"));
    var userDataFiles = userDataPath
	    .Dump("UserData Folder Path")
	    .EnumerateFiles()
	    .OrderByDescending(x=>x.LastWriteTime);
    
	var loadStringDisplay = new DumpContainer().Dump();
	void DisplayLoadStringData()
	{
		object Prettify(StringBuilder sb) 
		{
			var str = sb.ToString();
			return Encoding.UTF8.GetBytes(str).AsSpan().IsJson() ? (object)Util.SyntaxColorText(str, SyntaxLanguageStyle.Json, true) : str;
		}
		loadStringDisplay.Content = Util.WithHeading(
			userDataFiles.Select(x => new
			{
				x.Name,
				Content = x.OpenText().Using(f =>
				{
					var str = new char[250];
					f.ReadBlock(str, 0, (int)Math.Min(f.BaseStream.Length, 250L));
					var sb = new StringBuilder().Append(new string(str).TrimEnd('\0'))
												.Append(f.BaseStream.Length > 250 ? "..." : string.Empty);
					return Prettify(sb);
				}),
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
    new Button("Export User Data", _ =>
    {
       new TextArea(JsonConvert.SerializeObject(new { DataType = "userdata", Data = userDataFiles.ToDictionary(df => df.Name, df => Convert.ToBase64String(df.ReadAllBytes())) } ) ).Dump();
    }).Dump();
    
    var passwords = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LINQPad", "Passwords")).Dump("Passwords Folder Path")
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
    })
    .Dump("Passwords");
    
    new Button("Export Passwords", _ => {
        new TextArea(JsonConvert.SerializeObject(new { DataType="passwords", Data=passwords.ToDictionary(df => df.Name, df => Convert.ToBase64String(Encoding.UTF8.GetBytes(Util.GetPassword(df.Name)))) } )).Dump();
    }).Dump();
}

public static class Shortcuts
{
    static string Quote(string s) => s.Contains(" ") ? $"\"{s}\"" : s;
    public static void OpenNotepadPP(string f)
    {
		var progDir = Environment.GetEnvironmentVariable("ProgramFiles");
        var prog86Dir = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        var npDir = Path.Combine("Notepad++", "notepad++.exe");
		var npExe = new FileInfo(Path.Combine(progDir, npDir));
		if (!npExe.Exists)
			npExe = new FileInfo(Path.Combine(prog86Dir, npDir));
		if (!npExe.Exists)
			throw new FileNotFoundException("Could not find NotePad++ installation.");
        Util.Cmd($"{Quote(npExe.FullName)} {Quote(f)}");
    }
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
    public static byte[] ReadAllBytes(this FileInfo f) => !f.Exists ? Array.Empty<byte>() : File.ReadAllBytes(f.FullName);
	public static TReturn? Using<TDisposable, TReturn>(this TDisposable disposable, Func<TDisposable, TReturn> workFunc) where TDisposable : IDisposable
    {
        using (disposable)
        {
            return workFunc(disposable);
        }
    }
}

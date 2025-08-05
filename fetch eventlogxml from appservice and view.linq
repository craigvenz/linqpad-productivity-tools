<Query Kind="Program">
  <NuGetReference>Azure.ResourceManager.AppService</NuGetReference>
  <NuGetReference>Azure.ResourceManager.Network</NuGetReference>
  <NuGetReference>Azure.ResourceManager.Resources</NuGetReference>
  <NuGetReference>HtmlAgilityPack</NuGetReference>
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <NuGetReference>System.Reactive</NuGetReference>
  <Namespace>Azure.ResourceManager.AppService</Namespace>
  <Namespace>Azure.ResourceManager.Resources</Namespace>
  <Namespace>LINQPad.Controls</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Net.Http.Json</Namespace>
  <Namespace>System.Numerics</Namespace>
  <Namespace>System.Reactive</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Xml.Serialization</Namespace>
  <DisableMyExtensions>true</DisableMyExtensions>
</Query>

#load "Azure Credentials"
#load "GetCurrentUpn"
#load "spinner"

using HAP = HtmlAgilityPack;
using System.Windows.Forms;
using LINQPad.Controls;

HttpClient httpClient = new();
List<WebSiteResource> webApps;
StackPanel? sp;
DumpContainer output = new DumpContainer()
{
    DumpDepth = 6
};

void Main()
{
    string userHint = GetCurrentUPN()!;

    // Use LINQpad's built-in Azure Credentials connection to authenticate
    string authEndPoint = Util.AzureCloud.PublicCloud.AuthenticationEndpoint;

    // Create a new LINQPadTokenCredential object with the authEndPoint and TenantId
    // TenantId comes from the Azure Credentials query, see it for more details
    var credential = new LINQPadTokenCredential(authEndPoint + TenantId, userHint, false, "https://management.azure.com/.default");
    // Create a new ArmClientOptions object to set the API version for the Cloud Services
    var armClient = new Azure.ResourceManager.ArmClient(credential, TenantId);
    var busy = new Spinner() { Visible = false };
    var subs = armClient.GetSubscriptions()
                        .Select(x => new DisplaySubscription(x))
                        // Prepend an empty DisplaySubscription to show 'Select a subscription' as the first item
                        .Prepend(new DisplaySubscription())
                        .ToArray();

    // Display the subscriptions in a dropdown list
    var subList = new SelectBox(SelectBoxKind.DropDown, subs);
    // Create a typeable searchbox for web application names
    var serverName = new DataListBox();
    serverName.HtmlElement["spellcheck"] = "false";
    // horizontal layout of the subscription and webapp controls
    sp = new StackPanel(true, subList, serverName, busy).Dump();
    // Subscribe to the SelectedObservable of the dropdown list and update the webapp server list dropdown
    subList.SelectedObservable()
           .Select(_ => (DisplaySubscription)subList.SelectedOption)
           .Subscribe(sub => {
                // show the spinner while working then set the webapp dropdown
                ShowBusy(busy, output, _ => 
                {
                   output.Content = "Fetching web apps list.";
                   webApps = sub.Subscription.GetWebSites().ToList();
                   serverName.Options = webApps.Select(x => $"{x.Data.Name} - {x.Data.Id.ResourceGroupName}").ToArray();
                   return ShowBusyOptions.Standard;
                });
           });
           
    async Task<Stream?> FetchFromServer()
    {
        // get the selected webapp
        var selectedOption = serverName.Text.Split(" - ");
        if (selectedOption.Length != 2) return null;
        var selected = webApps.FirstOrDefault(x => x.Data.Name == selectedOption[0] && x.Data.Id.ResourceGroupName == selectedOption[1]);
        if (selected == null)
        {
            output.Content = "That App Service was not found.";
            return null;
        }
        // retrieve publish credentials
        var credentials = selected.GetPublishingProfileXmlWithSecrets(new Azure.ResourceManager.AppService.Models.CsmPublishingProfile()
        {
            Format = Azure.ResourceManager.AppService.Models.PublishingProfileFormat.WebDeploy
        });
        
        var publishInfo = XDocument.Load(credentials.Value);
        output.AppendContent(publishInfo);

        var publishProfile = publishInfo.Element("publishData")?
                                        .Elements("publishProfile")
                                        .FirstOrDefault(i => i?.Attribute("publishMethod")?.Value == "MSDeploy");
        var publishingUserName = publishProfile?.Attribute("userName")?.Value;
        var publishingPassword = publishProfile?.Attribute("userPWD")?.Value;
        var scmUri = publishProfile?.Attribute("publishUrl")?.Value;
        var profileName = publishProfile?.Attribute("profileName")?.Value;
        output.AppendContent("Creating http client and setting headers from publish credential values");
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{publishingUserName}:{publishingPassword}"))
        );
        // fetch the contents of the logfiles directory from the scm website of the web app
        var url = $"https://{scmUri}/api/vfs/LogFiles/";
        output.AppendContent($"Sending request to {url}");
        var vfs = await httpClient.GetFromJsonAsync<List<VFSEntry>>(url);
        output.AppendContent("Searching for first file ending with 'eventlog.xml'");
        var eventEntry = vfs?.FirstOrDefault(r => (r?.Path ?? string.Empty).EndsWith("eventlog.xml"));
        if (eventEntry == null)
        {
            output.AppendContent("Not found.");
            return null;
        }
        
        output.AppendContent(Util.WithHeading(eventEntry,"Getting headers of logfile"));
        var headers = httpClient.Send(new HttpRequestMessage(HttpMethod.Head, eventEntry.Url));
        string CacheKey() => $"{Util.CurrentQuery.Name} - {WebUtility.UrlEncode(profileName)}";
        var cacheString = Util.LoadString(CacheKey());
        var cacheData = new StupidCacheFormat() { ContentLength = eventEntry.Size, LastModified = eventEntry.ModifiedAt, ETag = headers.Headers.ETag?.Tag ?? string.Empty, TempFile = Path.GetTempFileName() };
        if (!string.IsNullOrEmpty(cacheString))
            cacheData = JsonConvert.DeserializeObject<StupidCacheFormat>(cacheString)!;
        var tempFile = new FileInfo(cacheData.TempFile);
        while (!tempFile.Exists || tempFile.Length == 0 || tempFile.LastWriteTime < headers.Content.Headers.LastModified.GetValueOrDefault().DateTime.ToLocalTime())
        {
            if (cacheData.ContentLength != headers.Content.Headers.ContentLength || headers.Content.Headers.LastModified > cacheData.LastModified || cacheData.ETag != (headers.Headers.ETag?.Tag ?? string.Empty))
            {
                var response = await httpClient.GetAsync(eventEntry.Url);
                if (!response.IsSuccessStatusCode)
                {
                    output.AppendContent(Util.WithHeading(response, "Status code not successful"));
                    return null;
                }
                var content = response.Content.ReadAsStream();
                output.AppendContent("Reading stream and saving to temp file");
                using (var txt = new FileInfo(cacheData.TempFile).CreateText())
                {
                    content.CopyTo(txt.BaseStream);
                }
                Util.SaveString(CacheKey(), JsonConvert.SerializeObject(cacheData));
            }
            tempFile.Refresh();
            if (!tempFile.Exists || tempFile.Length != headers.Content.Headers.ContentLength)
            {
                output.AppendContent("Our tempfile must have got deleted. Refetch then.");
                // i have to cheat and modify something or we'll go around forever
                cacheData.LastModified = DateTime.MinValue;
            }
        }
        output.AppendContent(Util.WithHeading(cacheData.TempFile, "Opening temp file for read"));
        var local = tempFile.OpenRead();
        return local;
    }
    
    async Task<IEnumerable<EventLogEvent>> GetLogs()
    {
        var content = await FetchFromServer();
        if (content == null)
        {
            output.AppendContent("Failed to fetch logs from server.");
            return Enumerable.Empty<EventLogEvent>();
        }
        
        XDocument xdata;
        try
        {
            output.AppendContent("Trying to parse xml");
            xdata = XDocument.Load(content);
        }
        catch (XmlException)
        {
            content.Seek(0,SeekOrigin.Begin);
            using var sr = new StreamReader(content);
            output.AppendContent(Util.WithHeading(sr.ReadToEnd(), "Content:"));
            throw;
        }
        T? Parse<T>(string? v) where T : INumber<T> 
        {
            var b = T.TryParse(v ?? string.Empty, System.Globalization.CultureInfo.InvariantCulture, out var i);
            return b ? i : default(T?);
        }
        return 
        from e in xdata?.Element("Events")?.Elements("Event")
        let system = e.Element("System")
        let eventData = e.Element("EventData")
        let TimeCreated = DateTime.Parse(system?.Element("TimeCreated")?.Attribute("SystemTime")?.Value ?? DateTime.MinValue.ToString()).ToLocalTime()
        select new EventLogEvent
        {
            System = new()
            {
                Provider = new() { Name = system?.Element("Provider")?.Attribute("Name")?.Value ?? string.Empty },
                TimeCreated = new() { SystemTime = TimeCreated },
                EventID = Parse<int>(system?.Element("EventID")?.Value),
                Level = Parse<int>(system?.Element("Level")?.Value),
                Task = Parse<int>(system?.Element("Task")?.Value),
                Keywords = system?.Element("Keywords")?.Value,
                EventRecordID = Parse<long>(system?.Element("EventRecordID")?.Value),
                Channel = system?.Element("Channel")?.Value ?? string.Empty,
                Computer = system?.Element("Computer")?.Value ?? string.Empty,
                Security = null
            },
            EventData = new() { Data = string.Join("\n", eventData?.Elements("Data")?.Select(x => x.Value ?? string.Empty) ?? Enumerable.Empty<string>()) }
        };
    }
    
    Dictionary<Type,IEnumerable<PropertyInfo>> typeCache = new();
    IEnumerable<PropertyInfo> GetTypeFromCache(dynamic item) 
    {
        var t = ((Type?)item?.GetType());
        if (t == null) return Enumerable.Empty<PropertyInfo>();
        if (typeCache.TryGetValue(t, out var props))
            return props;
        props = t.GetProperties();
        typeCache[t] = props;
        return props;
    }
    
    var searchPanel = new DumpContainer().Dump();
    IDisposable? subscription = null;
    
    var start = new LINQPad.Controls.Button("Get logs", async _ =>
    {
        var logs = await GetLogs();
        
        var search = new LINQPad.Controls.TextBox();
        subscription?.Dispose();
        searchPanel.ClearContent();
        if (!logs.Any())
        {
            output.AppendContent("No logs were found.");
            return;
        }
        subscription = search.TextInputObservable()
        .Throttle(TimeSpan.FromMilliseconds(500))
        .Select(_ => search.Text)
        .Subscribe(searchTerm =>
        {
            IEnumerable<object?> GetAllPropertyValues(EventLogEvent item)
                => GetTypeFromCache(item).Select(prop => prop.GetValue(item));
            bool MatchesSearchTerm(EventLogEvent logItem, string searchTerm)
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return true;

                var propertyValues = GetAllPropertyValues(logItem);

                // Special case: search for "null" matches actual null values
                if (searchTerm == "null")
                    return propertyValues.Any(value => value == null);

                // General case: search in string representations of all properties
                return propertyValues.Any(value =>
                    (value?.ToString() ?? string.Empty).Contains(searchTerm, StringComparison.CurrentCultureIgnoreCase));
            }
            output.ClearContent();
            var data = 
                (from item in logs
                 where MatchesSearchTerm(item, searchTerm)
                 orderby item.System.TimeCreated.SystemTime
                 select item).ToList().Highlight(searchTerm);
            output.Content = data;
        });
        var levels = new RangeControl(1,7,output.DumpDepth.GetValueOrDefault());
        levels.ValueInput += (o,e) => {
            output.DumpDepth = levels.Value;
            output.Refresh();
        };
        searchPanel.Content = new StackPanel(true, new Span("Search Log:"), search, new Span("Log expansion level:"), levels);
        output.Content = 
            logs.GroupBy(x => x.System.Provider.Name)
                .OrderBy(x => x.Key)
                .Select(g => new {
                                 g.Key, ItemsByDay = from gy in g
                                                     let tc = gy.System.TimeCreated.SystemTime.Date
                                                     group gy by tc into bydate
                                                     orderby bydate.Key descending
                                                     select new
                                                     {
                                                         Date = bydate.Key.ToShortDateString(),
                                                         Items = from y in bydate
                                                                 orderby y.System.TimeCreated.SystemTime descending
                                                                 select new
                                                                 {
                                                                     y.System,
                                                                     y.EventData
                                                                 }
                                                     }
                        });
    }).Dump();
    start.Enabled = false;

    Observable.CombineLatest(serverName.TextInputObservable(), subList.SelectedObservable(), serverName.ClickObservable(),
      (serverNameText, subListSelect, serverNameClick) => (serverName: serverName, subList: subList)
    )
    .Select(c => (dataListText: c.serverName?.Text ?? string.Empty,
                  subListIndex: c.subList?.SelectedIndex ?? 0))
    .Where(c => c.dataListText.Contains(" - "))
    .Subscribe(o =>
    {
        start.Enabled = !string.IsNullOrWhiteSpace(o.dataListText) && o.subListIndex != 0;
    });

    output.Dump();

    Util.HideEditor();
    //Util.KeepRunning();
}
// This record is used to display the subscription in the dropdown list and print 'Select a subscription' as the first item
record DisplaySubscription(SubscriptionResource? Subscription)
{
    public DisplaySubscription() : this((SubscriptionResource?)null) { }
    public override string ToString() => Subscription?.Data?.DisplayName ?? "-- Select a subscription --";
}
private class ShowBusyOptions 
{
    public bool ClearContentAfterReturn { get; set; } = true;
    public static ShowBusyOptions Standard { get; } = new() { ClearContentAfterReturn = true };
}
// Wrapping up a UI nicety - show the spinner while performing the action, then hide the spinner on completion or error
void ShowBusy(Spinner spinner, DumpContainer output, Func<DumpContainer,ShowBusyOptions?> action)
{
    try
    {
        spinner.Visible = true;
        var opt = action(output);
        if (opt?.ClearContentAfterReturn == true)
            output.Content = string.Empty;
    }
    catch (Exception ex)
    {
        output.Content = ex;
    }
    finally
    {
        spinner.Visible = false;
    }
}
// Provides shorthand for getting an observable from various LINQPad control events
public static class ObservableControlExtensions
{
    public static IObservable<EventPattern<object>> TextInputObservable(this ITextControl t)
        => Observable.FromEventPattern(o => t.TextInput += o, o => t.TextInput -= o);

    public static IObservable<EventPattern<object>> ClickObservable(this LINQPad.Controls.Control c)
        => Observable.FromEventPattern(o => c.Click += o, o => c.Click -= o);

    public static IObservable<EventPattern<object>> GotFocusObservable(this LINQPad.Controls.Control c)
        => Observable.FromEventPattern(o => c.GotFocus += o, o => c.GotFocus -= o);

    public static IObservable<EventPattern<object>> LostFocusObservable(this LINQPad.Controls.Control c)
        => Observable.FromEventPattern(o => c.LostFocus += o, o => c.LostFocus -= o);

    public static IObservable<EventPattern<object>> RenderingObservable(this LINQPad.Controls.Control c)
        => Observable.FromEventPattern(o => c.Rendering += o, o => c.Rendering -= o);

    public static IObservable<EventPattern<object>> SelectedObservable(this SelectBox c)
        => Observable.FromEventPattern(o => c.SelectionChanged += o, o => c.SelectionChanged -= o);

    public static IObservable<EventPattern<object>> ValueInputObservable(this RangeControl c)
        => Observable.FromEventPattern(o => c.ValueInput += o, o => c.ValueInput -= o);
}
public static class HighlightExtension
{
    // This applies the built in CSS class Highlight from LINQPad to any text found in a 
    // control or other. Great for doing searches. Unfortunately it kills any javascript
    // events or other stuff Linqpad adds to things when it runs Util.ToHtmlString so you 
    // can't open and close tables any more if you use this. I don't really know how to fix
    // this without further investigation so I left it alone for now. TBD.
    public static object Highlight(this object data, string text, bool caseInsensitive = false)
    {
        if (string.IsNullOrEmpty(text)) 
            return data;
        var html = Util.ToHtmlString(data);
        var htmlDocument = new HAP.HtmlDocument();
            htmlDocument.LoadHtml(html);
        var nodes = htmlDocument.DocumentNode.SelectNodes("//td") 
                         ?? Enumerable.Empty<HAP.HtmlNode>(); 
        nodes = nodes.Where(tableCell => 
            tableCell.InnerText.Contains(text, StringComparison.CurrentCultureIgnoreCase));
        var rxOptions = caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None;
        foreach (var item in nodes)
        {
            item.InnerHtml = Regex.Replace(
                item.InnerHtml, 
                "(" + Regex.Escape(text) + ")",
                 "<span class='highlight' style='background:yellow'>$1</span>",
                 rxOptions);
        }
        return Util.RawHtml(htmlDocument.DocumentNode.OuterHtml);
    }
}

// The format of the virtual file system api returned by the SCM in Azure
class VFSEntry
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("size")]
    public long Size { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("mtime")]
    public DateTime ModifiedAt { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("crtime")]
    public DateTime CreatedAt { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("mime")]
    public string? MimeType { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("href")]
    public Uri? Url { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("path")]
    public string? Path { get; set; }
}
class StupidCacheFormat
{
    public long ContentLength { get; set; }
    public string ETag { get; set; }
    public DateTime LastModified { get; set; }
    public string TempFile { get; set; }
}
// Event log types
public class EventLogSystem
{
    public EventLogSystemProvider Provider { get; set; }
    public int? EventID { get; set; }
    public int? Level { get; set; }
    public int? Task { get; set; }
    public string? Keywords { get; set; }
    public EventLogTimeCreated TimeCreated { get; set; }
    public long? EventRecordID { get; set; }
    public string Channel { get; set; }
    public string Computer { get; set; }
    public EventLogSystemSecurity? Security { get; set; }
}
public class EventLogSystemSecurity { }
public class EventLogSystemProvider
{
    public string Name { get; set; }
    public override string ToString() => Name;
}
public class EventLogTimeCreated
{
    public DateTimeOffset SystemTime;
    public override string ToString() => SystemTime.ToString();
}
public class EventLogData
{
    public string Data { get; set; }
    public override string ToString() => Data;
}
public class EventLogEvent
{
    public EventLogSystem System { get; set; }
    public EventLogData EventData { get; set; }
    public override string ToString() => $"{System} {EventData}";
}

<Query Kind="Program">
  <NuGetReference>Enums.NET</NuGetReference>
  <NuGetReference>Humanizer</NuGetReference>
  <NuGetReference>JWT</NuGetReference>
  <NuGetReference>System.Reactive</NuGetReference>
  <Namespace>Humanizer</Namespace>
  <Namespace>LINQPad.Controls</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
  <Namespace>System.ComponentModel</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>EnumsNET</Namespace>
  <DisableMyExtensions>true</DisableMyExtensions>
</Query>

private const string DefaultBackgroundColor = "#202020";
private const string ErrorBackgroundColor = "red";
private const string CaptionColor = "#909090";
// Main method implements UI, but this query is designed to be able to be #loaded by
// another query, should you wish to reuse the display methods
internal void Main()
{
    // Uncomment Util.KeepRunning to debug the UI. 
    // This is necessary because LINQPad main thread exists at the end of the Main method,
    // and the UI will run in a worker thread using RX. 
    // Util.KeepRunning();
    // Also another technique, if the LINQpad debugger isn't sufficient, is to uncomment this, which
    // will trigger the OS's JIT debugging and provide opportunity to attach visual studio or other debugger
    // Debugger.Launch();

    // Set up UI
    new Literal($@"<span style=""font-size: 20px; font-weight: bold; color: {CaptionColor}"">JWT Decoder</span>").Dump();
    var output = new DumpContainer();
    var tokenInput = new TextArea();
    tokenInput.HtmlElement["spellcheck"] = "false";
    new StackPanel(true,
        new StackPanel(false,
            tokenInput,
            new Button("Clear", _ => ResetUI())
        ),
        output).Dump();
    void ResetUI()
    {
        displayObservable?.Dispose();
        tokenInput.Text = string.Empty;
        tokenInput.Styles["background-color"] = DefaultBackgroundColor;
        output.ClearContent();
    }
    // Set up event handling using RX. Use the TextInput event to trigger the decoding process and throttle the input to avoid excessive processing.
    Observable.FromEventPattern(o => tokenInput.TextInput += o, o => tokenInput.TextInput -= o)
              .Throttle(TimeSpan.FromSeconds(1))
              .Subscribe(_ => {
        // Clear the output if the input is empty
        // and set the background color of the input field to indicate the state.
        if (string.IsNullOrEmpty(tokenInput.Text))
        {
            ResetUI();
            return;
        }
        var t = DumpToken(tokenInput.Text, output);
        // If the token is null, set the background color to red indicating bad input, otherwise set it to a dark color.
        tokenInput.Styles["background-color"] = t == null ? ErrorBackgroundColor : DefaultBackgroundColor;
    });
}
// The JWT decoder uses the JWT library to decode the token and display its contents in a LINQPad DumpContainer.
public readonly JWT.JwtDecoder JwtDecoder = new JWT.JwtDecoder(
    new JWT.Serializers.JsonNetSerializer(
        JsonSerializer.Create(
            new JsonSerializerSettings()
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                Formatting = Newtonsoft.Json.Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind,
            }
        )
    ),
    new JWT.JwtBase64UrlEncoder()
);
// This method converts a long value representing seconds since the Unix epoch to a human-readable date and time string.
public string LongToHumanTime(long? sec)
{
    if (!sec.HasValue || sec == 0) return "(null)";
    var utc = DateTimeOffset.FromUnixTimeSeconds(sec.Value);
    var t = utc.LocalDateTime;
    return $"{t}, {t.Humanize()}";
}
public enum JwtConstants
{
    [Description(             "Issued At")] iat,
    [Description(            "Not Before")] nbf,
    [Description(                "Issuer")] iss,
    [Description(               "Expires")] exp,
    [Description(              "Audience")] aud,
    [Description("Authentication Methods")] amr,
    [Description(               "Subject")] sub,
    [Description(                "JWT Id")] jti,
    [Description(     "Signing Algorithm")] alg,
    [Description(                "Key Id")] kid,
    [Description(                  "Type")] typ
}
private static string GetDescription(JwtConstants x) 
    => x.GetAttributes()!
        .Get<DescriptionAttribute>()!
        .Description;
private static Dictionary<string,string> pNames = 
    Enum.GetValues(typeof(JwtConstants))
        .Cast<JwtConstants>()
        .ToDictionary(k => k.ToString(), 
                      v => GetDescription(v) ?? v.ToString(),
                      StringComparer.CurrentCultureIgnoreCase);
private static HashSet<string> TimeBasedAttributes = new[] {
    JwtConstants.iat,
    JwtConstants.nbf,
    JwtConstants.exp
}.Select(x=>x.ToString())
 .Append("auth_time")
 .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

// We save the observable in a static field to avoid creating multiple subscriptions.
private static IDisposable? displayObservable;
/// <summary>
/// Dumps the contents of a JWT token in a LINQPad DumpContainer.
/// </summary>
/// <param name="token">The JWT token to decode.</param>
/// <param name="container">The DumpContainer to display the contents in. If null, a new DumpContainer will be created.</param>
/// <param name="exclude">An array of property names to exclude from the output.</param>
/// <param name="customize">A function to customize the output of each property.</param>
/// <param name="includeExpirationTimer">Whether to include a timer showing the expiration status of the token.</param>
/// <returns>A JObject representing the decoded contents of the token.</returns>
/// <exception cref="ArgumentNullException">Thrown when the token is null or empty.</exception>
/// <exception cref="JsonException">Thrown when the token cannot be decoded.</exception>
public JObject? DumpToken(string token, DumpContainer? container = null, string[]? exclude = null, Func<JProperty,object>? customize = null, bool includeExpirationTimer = true)
{
    // Set default values for the parameters if they are null.
    exclude ??= [];
    customize ??= DefaultCustomize;
    var panel = new DumpContainer();
    try
    {
        var t = token?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        var parts = new JWT.JwtParts(t);
        var header = JwtDecoder.DecodeHeader(t);
        var decoded = JwtDecoder.Decode(parts, false);
        var objects = JsonConvert.DeserializeObject(decoded) as JObject;
        var headerObjects = JsonConvert.DeserializeObject(header) as JObject;
        
        Output(
            from property in headerObjects?.Concat(objects.Children())
            let name = ((JProperty)property).Name
            where !exclude.Contains(name)
            select property
        );

        if (includeExpirationTimer)
            ShowTimer(objects);
        return objects;
    }
    catch (Exception ex)
    {
        Output(ex);
        return null;
    }
    finally
    {
        if (container != null)
            container.Content = panel;
        else
            panel.Dump();
    }
    // Takes care of displaying the output in the supplied output DumpContainer or in the LINQPad output window.
    void OutputInternal(object? obj)
    {
        if (container != null)
            panel.AppendContent(obj);
        else
            obj.Dump();
    }
    void Output(object? obj)
    {
        // branch to handle customization of output if supplied
        if (obj is IEnumerable<JToken> jObj)
            OutputInternal(jObj.Cast<JProperty>().Select(customize));
        else
            OutputInternal(obj);
    }
    // Default display output of each property in the JWT token.
    // It formats the property name and value, and handles specific properties like "iat", "nbf", and "exp" to show a timer.
    // It also provides a human-readable format for the property name and value.
    object DefaultCustomize(JProperty x)
    {
        // This method is used to format the property name for display.
        // It checks if the property name is in the dictionary of known names and formats it accordingly.
        object getName(string n)
        {
            var b = pNames.TryGetValue(n, out var v);
            if (b)
            {
                var display = new Span($"({v})");
                display.Styles["color"] = CaptionColor;
                return new WrapPanel(new Span(n), display);
            }
            return n;
        }
        // This method is used to format the property value for display.
        // It checks if the property name is in the set of time-based attributes and formats it accordingly.
        return (object)new {
            Name = getName(x.Name),
            Value = x switch {
                _ when TimeBasedAttributes.Contains(x.Name) => 
                        LocalDisplayExtensions.Timer(
                            () => LongToHumanTime((long?)x.Value),
                            TimeSpan.FromSeconds(2)),
                _ => (object)x.Value
            }
        };
    }
    (DateTime? notBefore, DateTime? issuedAt, DateTime? expiresOn) GetTimeAttributesFromJwt(JObject contents)
    {
        DateTime? FromUts(JToken? inp) 
            => inp == null 
               ? (DateTime?)null 
               : DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(inp)).DateTime;
        return (
            FromUts(contents.Property(JwtConstants.nbf.ToString())?.Value),
            FromUts(contents.Property(JwtConstants.iat.ToString())?.Value),
            FromUts(contents.Property(JwtConstants.exp.ToString())?.Value)
        );
    }
    // This method is used to show a timer indicating the expiration status of the token.
    void ShowTimer(JObject contents)
    {
        const string NoDate = "not specified";
        var (notBefore, issuedAt, expiresOn) = GetTimeAttributesFromJwt(contents);
        Control DisplayOrNone(object value) => new DumpContainer(value);
        OutputInternal(
            new Table(rows: new[] {
                new TableRow(true, new Span("Issued at"), new Span("Expires"), new Span("Total length of token")),
                new TableRow(false, 
                    DisplayOrNone(issuedAt.HasValue ? issuedAt.Value.UpdateInPlace().ToControl() : Util.Metatext(NoDate)),
                    DisplayOrNone(expiresOn.HasValue ? expiresOn.Value.UpdateInPlace().ToControl() : Util.Metatext(NoDate)),
                    DisplayOrNone(expiresOn.HasValue && issuedAt.HasValue 
                        ? new Span(expiresOn.Value.Subtract(issuedAt.Value).Humanize(precision: 2)) 
                        : Util.Metatext(NoDate))
                )
            })
        );
        if (!issuedAt.HasValue || !expiresOn.HasValue) return;
        var observable = LocalDisplayExtensions.GetTimer(TimeSpan.FromSeconds(1))
                                               .Select(_ => DateTime.UtcNow);
        var bar = new Util.ProgressBar(Caption())
        {
            Percent = CalculatePercentage(DateTime.UtcNow)
        };
        OutputInternal(bar);
        // Dispose of the previous observable to avoid memory leaks.
        displayObservable?.Dispose();
        // We need to keep track of the last caption and percent to avoid unnecessary updates.
        var lastCaption = string.Empty;
        var lastPercent = -1;
        displayObservable = observable.Subscribe(
            UpdateTimer,
            e => { OutputInternal(e); },
            () =>
            {
                bar.Caption = Caption();
                bar.Percent = 100;
            }
        );
        return;
        string Caption()
        {
            var dt = expiresOn.Value.Subtract(DateTime.UtcNow);
            var isPast = dt.TotalMilliseconds < 0;
            var status = isPast ? "expired" : "expires in";
            var when = dt.Humanize(precision: 2);
            var past = isPast ? " ago" : string.Empty;
            return $"Token {status} {when}{past}";
        }
        int CalculatePercentage(DateTime o)
        {
            double totalTimeSpan = expiresOn.Value.Subtract(notBefore.GetValueOrDefault(issuedAt.Value)).TotalSeconds;
            double secondsSinceStart = o.Subtract(notBefore.GetValueOrDefault(issuedAt.Value)).TotalSeconds;
            var percentage = (secondsSinceStart / totalTimeSpan) * 100;
            return (int)percentage;
        }
        // Updates the timer, only updating if something's changed
        void UpdateTimer(DateTime o)
        {
            var newCaption = Caption();
            var newPercent = CalculatePercentage(o);
            if (newCaption != lastCaption)
            {
                bar.Caption = newCaption;
                lastCaption = newCaption;
            }
            if (newPercent != lastPercent)
            {
                bar.Percent = newPercent;
                lastPercent = newPercent;
            }
        }
    }
}
// This class is used to create a DumpContainer that displays the latest value from an observable sequence.
// LINQpad has an extension DumpLatest which is essentially what this code is doing, but that extension
// returns the observable not the dump container, so that's why this was needed.
internal sealed class LatestDumpContainer<T> : DumpContainer, IDisposable
{
    private readonly IDisposable _subscription;
    public LatestDumpContainer(IObservable<T> content)
    {
        _subscription = content.Subscribe(OnNextAction);
        // Staying consistent with LINQpad observable display convention, I'm using the same
        // styling that DumpLatest uses.
        Style = "color:green";
    }
    private void OnNextAction(T item) => Content = item;
    public void Unsubscribe() => _subscription.Dispose();
    public void Dispose() => Unsubscribe();
}
// Extensions and helper functions for timers and observable sequences.
internal static class LocalDisplayExtensions
{
    // This method is used to create a timer that triggers at a specified interval.
    private static TimeSpan DefaultTimer(TimeSpan? t) => t ?? 1.Seconds();
    // This method is used to create a preloaded observable sequence that emits the initial value and then merges with the provided sequence.
    // It uses the LatestDumpContainer to display the latest value.
    private static DumpContainer CreatePreloadedObservable<T>(Func<T> value, IObservable<T> sequence) =>
        new LatestDumpContainer<T>(sequence.StartWith(value()));
    internal static IObservable<long> GetTimer(TimeSpan? refreshTime)
    // .Publish is used to create a hot observable that shares the same subscription for all observers.
    // .RefCount is used to automatically unsubscribe when there are no observers left.
        => Observable.Interval(DefaultTimer(refreshTime)).Publish().RefCount();
    // This method is used to create a timer that triggers at a specified interval and produces a value using the provided function.
    // DistinctUntilChanged is used to avoid emitting the same value multiple times.
    static public DumpContainer Timer(Func<string> produceValue, TimeSpan? refreshTime) => 
        CreatePreloadedObservable<string>(produceValue,
                                          (from _ in GetTimer(refreshTime)
                                           select produceValue()).DistinctUntilChanged());
    public static DumpContainer UpdateInPlace(this DateTime t, TimeSpan? refreshTime = null) =>
        Timer(() => $"{t} - {t.Humanize()}", refreshTime);
}
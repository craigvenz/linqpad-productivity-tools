<Query Kind="Program">
  <NuGetReference>Humanizer</NuGetReference>
  <NuGetReference>JWT</NuGetReference>
  <NuGetReference>System.Reactive</NuGetReference>
  <Namespace>Humanizer</Namespace>
  <Namespace>LINQPad.Controls</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <DisableMyExtensions>true</DisableMyExtensions>
</Query>

void Main()
{
    var output = new DumpContainer();
    var tokenInput = new TextArea();
    new StackPanel(true,
        new StackPanel(false,
            tokenInput,
            new Button("Clear", _ => output.ClearContent())
        ),
        output).Dump();
    Observable.FromEventPattern(o => tokenInput.TextInput += o, o => tokenInput.TextInput -= o)
    .Throttle(TimeSpan.FromSeconds(1))
    .Subscribe(_ =>
    {
        if (string.IsNullOrEmpty(tokenInput.Text))
        {
            output.ClearContent();
            tokenInput.Styles["background-color"] = "#202020";
            return;
        }
        var t = DumpToken(tokenInput.Text, output);
        tokenInput.Styles["background-color"] = t == null ? "red" : "#202020";
    });
    Util.KeepRunning();
}
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
public string LongToHumanTime(long? sec)
{
    if (!sec.HasValue || sec == 0) return "(null)";
    var utc = DateTimeOffset.FromUnixTimeSeconds(sec.Value);
    //Util.ToExpando(utc).Dump();
    var t = utc.LocalDateTime;
    return $"{t}, {t.Humanize()}";
}
private static IDisposable? displayObservable;
public JObject? DumpToken(string token, DumpContainer? container = null, string[]? exclude = null, Func<JProperty,object>? customize = null, bool includeExpirationTimer = true)
{
    exclude ??= Array.Empty<string>();
    customize ??= DefaultCustomize;
    var panel = new DumpContainer();
    try
    {
        var t = token.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        var parts = new JWT.JwtParts(t);
        var header = JwtDecoder.DecodeHeader(t);
        var decoded = JwtDecoder.Decode(parts, false);
        var objects = JsonConvert.DeserializeObject(decoded) as JObject;
        var headerObjects = JsonConvert.DeserializeObject(header) as JObject;
        Output(headerObjects!.Concat(objects!.Children()).Where(x=>!exclude.Contains(((JProperty)x).Name)));
        if (includeExpirationTimer)
            ShowTimer(objects);
        if (container != null)
            container.Content = panel;
        else
            panel.Dump();
        return objects;
    }
    catch (Exception ex)
    {
        Output(ex);
        return null;
    }
    void OutputInternal(object? obj)
    {
        if (container != null)
            panel.AppendContent(obj);
        else
            obj.Dump();
    }
    void Output(object? obj)
    {
        if (obj is IEnumerable<JToken> jObj)
            OutputInternal(jObj.Cast<JProperty>().Select(customize));
        else
            OutputInternal(obj);
    }
    object DefaultCustomize(JProperty x)
    {
        Dictionary<string,string> pNames = new(StringComparer.CurrentCultureIgnoreCase){
            { "iat", "Issued At" },
            { "nbf", "Not Before" },
            { "iss", "Issuer" },
            { "exp", "Expires" },
            { "aud", "Audience" },
            { "amr", "Authentication Methods" },
            { "sub", "Subject" },
            { "jti", "JWT Id" },
            { "alg", "Signing Algorithm" },
            { "kid", "Key Id" },
            { "typ", "Type" }
        };
        Func<long, string> t = _ => LongToHumanTime((long?)x.Value);
        object getName(string n)
        {
            var b = pNames.TryGetValue(n, out var v);
            if (b)
            {
                var display = new Span($"({v})");
                display.Styles["color"] = "#909090";
                return new LINQPad.Controls.WrapPanel(new Span(n), display);
            }
            return n;
        }
        return x switch
        {
            { Name: "iat" or "nbf" or "exp" or "auth_time" } => 
                (object)new
                {
                    Name = getName(x.Name),
                    Value = (object)t.UpdateInPlaceFromAction(TimeSpan.FromSeconds(2))
                },
            _ => (object)new {
                Name = getName(x.Name),
                Value = x.Value
            }
        };
    }
    void ShowTimer(JObject contents)
    {
        DateTime FromUts(JToken? inp) => DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(inp ?? "0")).DateTime;
        var notBefore = FromUts(contents.Property("nbf")?.Value);
        var  issuedAt = FromUts(contents.Property("iat")?.Value);
        var expiresOn = FromUts(contents.Property("exp")?.Value);

        OutputInternal(
            new Table(rows: new[] {
                new TableRow(true, new Span("Issued at"), new Span("Expires"), new Span("Total length of token")),
                new TableRow(false, issuedAt.UpdateInPlace(),
                                    expiresOn.UpdateInPlace(),
                                    new Span(expiresOn.Subtract(issuedAt).Humanize(precision: 2)))
            })
        );

        var observable = LocalDisplayExtensions.GetTimer(TimeSpan.FromSeconds(1))
                                               .Select(_ => DateTime.UtcNow)
                                   /*.TakeWhile(v => expiresOn.Subtract(v).TotalSeconds >= 0)*/;

        string Caption()
        {
            var dt = expiresOn.Subtract(DateTime.UtcNow);
            var isPast = dt.TotalMilliseconds < 0;
            var status = isPast ? "expired" : "expires in";
            var when = expiresOn.Subtract(DateTime.UtcNow)
                                .Humanize(precision: 2);
            var past = isPast ? " ago" : string.Empty;
            return $"Token {status} {when}{past}";
        }
        int CalculatePercentage(DateTime o)
        {
            double totalTimeSpan = expiresOn.Subtract(notBefore).TotalSeconds;
            double secondsSinceStart = o.Subtract(notBefore).TotalSeconds;
            var percentage = (secondsSinceStart / totalTimeSpan) * 100;
            return (int)percentage;
        }
        var bar = new Util.ProgressBar(Caption())
        {
            Percent = CalculatePercentage(DateTime.UtcNow)
        };
        OutputInternal(bar);
        displayObservable?.Dispose();
        var lastCaption = string.Empty;
        var lastPercent = -1;
        displayObservable = observable.Subscribe(o =>
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
        }, e => { OutputInternal(e); }/*,
        () =>
        {
            bar.Caption = Caption();
            bar.Percent = 100;
        }*/);
    }
}
public sealed class LatestDumpContainer<T> : DumpContainer, IDisposable
{
    private readonly IDisposable _subscription;
    public LatestDumpContainer(IObservable<T> content)
    {
        _subscription = content.Subscribe(OnNextAction);
        Style = "color:green";
    }
    private void OnNextAction(T item) => Content = item;
    public void Unsubscribe() => _subscription.Dispose();
    public void Dispose() => Unsubscribe();
}
public static class LocalDisplayExtensions
{
    private static TimeSpan DefaultTimer(TimeSpan? t) => t ?? 1.Seconds();
    private static DumpContainer CreatePreloadedObservable<T>(Func<T> value, IObservable<T> sequence) =>
        new LatestDumpContainer<T>(Observable.Empty<T>()
                                             .Prepend(value())
                                             .Merge(sequence));
    private static Dictionary<TimeSpan, IObservable<long>> internalTimers = new();
    internal static IObservable<long> GetTimer(TimeSpan? refreshTime)
    {
        var t = DefaultTimer(refreshTime);
        if (!internalTimers.TryGetValue(t, out var o))
        {
            o = Observable.Interval(t).Publish().RefCount();
            internalTimers[t] = o;
        }
        return o;
    }

    private static DumpContainer Timer(Func<string> produceValue, TimeSpan? refreshTime)
    {
        return CreatePreloadedObservable<string>(produceValue,
                                                 (from _ in GetTimer(refreshTime)
                                                 select produceValue()).DistinctUntilChanged());
    }
    public static DumpContainer UpdateInPlace(this DateTime t, TimeSpan? refreshTime = null) =>
        Timer(() => $"{t} - {t.Humanize()}", refreshTime);
    public static DumpContainer UpdateInPlaceFromAction(this Func<long, string> select, TimeSpan? refreshTime = null) =>
        Timer(() => select(0), refreshTime);
}
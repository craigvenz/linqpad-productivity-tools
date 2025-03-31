<Query Kind="Program">
  <NuGetReference>Humanizer</NuGetReference>
  <NuGetReference>JWT</NuGetReference>
  <Namespace>Humanizer</Namespace>
  <Namespace>LINQPad.Controls</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
  <Namespace>Lcp.LINQPad.Controls</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
</Query>

void Main()
{
    var output = new DumpContainer();
    new StackPanel(true, 
        new StackPanel(false,
            new TextArea("", onTextInput: ta => DumpToken(ta.Text, output)),
            new Button("Clear", _ => output.ClearContent())
        ),
        output).Dump();
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
bool TryBase64(string str, out byte[]? decode)
{
    try 
    {
        decode = Convert.FromBase64String(str);
        return true;
    }
    catch (Exception)
    {
        decode = Array.Empty<byte>();
        return false;
    }
}
public string LongToHumanTime(long? sec)
{
    if (!sec.HasValue || sec == 0) return "(null)";
    var utc = DateTimeOffset.FromUnixTimeSeconds(sec.Value);
    //Util.ToExpando(utc).Dump();
    var t = utc.LocalDateTime;
    return $"{t}, {t.Humanize()}";
}
public JObject? DumpToken(string token, DumpContainer? container = null, string[]? exclude = null, Func<JProperty,object>? customize = null, bool includeExpirationTimer = true)
{
    exclude ??= Array.Empty<string>();
    customize ??= DefaultCustomize;
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
            container.AppendContent(obj);
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
                return new LINQPad.Controls.WrapPanel(n.ToSpan(), display);
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
                }
            ,
            //{ Value.Type: JTokenType.String } =>
            //    TryBase64(x.Value.ToString(), out var decoded) ?
            //        (object)new
            //        {
            //            Name = x.Name,
            //            Value = new
            //            {
            //                base64String = x.Value.ToString(),
            //                decoded
            //            }
            //        } : x,
            _ => (object)new {
                Name = getName(x.Name),
                Value = x.Value
            }
        };
    }
    static Hyperlink ClickSequence(params object[] items)
    {
        var i = 0;
        return new Hyperlink(items.FirstOrDefault()?.ToString() ?? "null", hl => {
            i = (i + 1) % items.Length;
            hl.Text = items[i].ToString();
        });
    }
    void ShowTimer(JObject contents)
    {
        DateTime FromUts(JToken? inp) => DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(inp ?? "0")).DateTime;
        var notBefore = FromUts(contents.Property("nbf")?.Value);
        var issuedAt = FromUts(contents.Property("iat")?.Value);
        var expiresOn = FromUts(contents.Property("exp")?.Value);

        OutputInternal(
            new Table(rows: new[] {
                new TableRow(true, "Issued at".ToSpan(), "Expires".ToSpan(), "Total length of token".ToSpan()),
                new TableRow(false, issuedAt.UpdateInPlace(),
                                    expiresOn.UpdateInPlace(),
                                    expiresOn.Subtract(issuedAt).Humanize(precision: 2).ToSpan())
            })
        );

        var observable = Observable.Interval(TimeSpan.FromSeconds(1))
                                   .Select(_ => DateTime.UtcNow)
                                   .TakeWhile(v => expiresOn.Subtract(v).TotalSeconds >= 0);

        string Caption()
        {
            var dt = expiresOn.Subtract(DateTime.UtcNow);
            return $"Token {(dt.TotalMilliseconds < 0 ? "expired" : "expires in")} {expiresOn.Subtract(DateTime.UtcNow).Humanize(precision: 2)}{(dt.TotalMilliseconds < 0 ? " ago" : string.Empty)}";
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

        observable.Subscribe(o =>
        {
            bar.Caption = Caption();
            bar.Percent = CalculatePercentage(o);
        }, e => { OutputInternal(e); },
        () =>
        {
            bar.Percent = 100;
        });
    }
}

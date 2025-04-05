<Query Kind="Statements">
  <Namespace>LINQPad.Controls</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>Humanizer</Namespace>
  <Namespace>System.Globalization</Namespace>
  <Namespace>System.Reactive.Concurrency</Namespace>
  <AutoDumpHeading>true</AutoDumpHeading>
</Query>

LocalScheduler scheduler = Scheduler.Default;

const bool UsePie = false;

var output = new DumpContainer();
var startDateLabel = new Label();
var endDateLabel = new Label();
var t = new Table(noBorders: false,
    cellTextAlign: "center",
    cellVerticalAlign: "middle",
    rows: new TableRow[] {  
    new TableRow(new Span("Time since start"), new Span("Time until end"), new Span("Progress")),
    new TableRow(startDateLabel, endDateLabel, output) })
.Dump();

if (UsePie)
{
    Util.HtmlHead.AddStyles(
    """
    .pie {
      --p: 0;
      --b: 20px;
      --c: red;
      --text-color: #DCDCDC;
      width: 150px;
      height: 150px;
      border-radius: 50%;
      background: conic-gradient(var(--c) calc(var(--p) * 1%), transparent 0);
      display: grid;
      place-items: center;
      font-size: 25px;
      font-family: sans-serif;
      color: var(--text-color);
      position: relative;
    }

    .pie::before {
      content: "";
      position: absolute;
      width: calc(100% - var(--b) * 2);
      height: calc(100% - var(--b) * 2);
      background: transparent;
      border-radius: 50%;
    }
    """
    );
}

var startDate = new TextBox(DateTime.Now.ToString());
var endDate = new TextBox("1:0:0");

DateTime sd = DateTime.Parse(startDate.Text);
DateTime ed = sd.Add(TimeSpan.Parse(endDate.Text));
IDisposable? sub = null;
var debug = new DumpContainer();

Observable.FromEventPattern(o => startDate.TextInput += o, o => startDate.TextInput -= o)
          .Throttle(1.Seconds())
          .ObserveOn(scheduler)
          .Subscribe(o => UpdateDates());
Observable.FromEventPattern(o => endDate.TextInput += o, o => endDate.TextInput -= o)
          .Throttle(1.Seconds())
          .ObserveOn(scheduler)
          .Subscribe(o => UpdateDates());
void UpdateDates()
{
    DateTime.TryParse(startDate.Text, out sd);
    if (TimeSpan.TryParse(endDate.Text, out var years))
    {
        ed = sd.Add(years);
    }
    else
    {
        if (DateTime.TryParse(endDate.Text, out var endDt) && endDt > sd)
        {
            ed = endDt;
        }
    }
    sub?.Dispose();

    sub = StartTimer();

    Refresh(DateTime.Now);
}
void Alert()
{
//  Debugger.Break();
    Refresh(ed);
    using var player = new System.Media.SoundPlayer(@"C:\Windows\Media\Alarm09.wav");
    player.Load();
    System.Windows.Forms.DialogResult? result = null;
    var stopAlarm = DateTimeOffset.Now.AddSeconds(30);
    var soundObservable = Observable.Interval(TimeSpan.FromSeconds(2))
                                    .TakeUntil(stopAlarm)
                                    .TakeWhile(x => !result.HasValue || result.Value != System.Windows.Forms.DialogResult.OK);
    var soundSubscription = soundObservable.ObserveOn(scheduler).Subscribe(_ => player.Play(), () => { player.Stop(); player.Dispose(); });
    result = System.Windows.Forms.MessageBox.Show("Timer completed.", "Timer", System.Windows.Forms.MessageBoxButtons.OK);
}
IDisposable StartTimer() => Observable.Interval(1.Seconds())
                                      .Select(x => DateTime.Now)
                                      .TakeWhile(x => x <= ed)
                                      .ObserveOn(scheduler)
                                      .Subscribe(o => Refresh(o), Alert);

var setStart = new Button("Set Start to Now");
new StackPanel(true, setStart, startDate, endDate).Dump();
setStart.Click += (ob, ev) =>
{
    startDate.Text = DateTime.Now.ToString();
    UpdateDates();
};
//debug.Dump();

sub = StartTimer();

Refresh(DateTime.Now);

//Util.KeepRunning();

void Refresh(DateTime o)
{
    debug.Content = (o, ed);
    if (sd != DateTime.MinValue && ed != DateTime.MaxValue)
    {
        var totalLength = ed.Subtract(sd);
        var sinceStart = o.Subtract(sd);
        var fraction = sinceStart.TotalMinutes / totalLength.TotalMinutes;
        double PercentFractional = fraction * 100.0;
        var Percent = (int)PercentFractional;
        startDateLabel.Text = o.Subtract(sd).Humanize(precision: 2, CultureInfo.CurrentCulture);
        endDateLabel.Text = ed.Subtract(o).Humanize(precision: 2, CultureInfo.CurrentCulture);
        var Caption = $"{double.Round(PercentFractional, 4)}%";
        int num = 100 - Percent;
        if (UsePie)
        {
            //display timer as filled circle
            var xElement = XElement.Parse($"<div class='pie' style='--p:{Percent};--b:10px;--c:blue;'>{Caption}</div>");
            output.Content = Util.RawHtml(xElement);
        }
        else
        {
            // timer as rectangular progress bar
            var xElement = XElement.Parse($"<div style='width:98em; height:1.3em; background:#17b;padding:2px{(string.IsNullOrEmpty(Caption) ? ";margin:2pt 0" : "")}'>\r\n\t<div style='background:white; width:{num}%; height:100%; float:right'></div></div>");
            if (!string.IsNullOrEmpty(Caption))
                xElement = new XElement("table", 
                    new XAttribute("style", "border:0; margin:0.2em 0 0.1em 0"), 
                    new XElement("tr", 
                        new XElement("td", 
                            new XAttribute("style", "border:0;padding:0pt 3pt 2pt 0;vertical-align:middle"), Caption), 
                        new XElement("td", new XAttribute("style", "border:0"), xElement)
                    )
                );
            output.Content = Util.RawHtml(xElement);
        }
    }
}

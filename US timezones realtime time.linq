<Query Kind="Statements">
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>Humanizer</Namespace>
  <Namespace>LINQPad.Controls</Namespace>
  <AutoDumpHeading>true</AutoDumpHeading>
</Query>

//TimeZoneInfo.GetSystemTimeZones().Dump();

var zones = new[] { "Central Standard Time", "Eastern Standard Time", "Mountain Standard Time", "Hawaiian Standard Time" }
            .Select(x => TimeZoneInfo.FindSystemTimeZoneById(x))
			.Prepend(TimeZoneInfo.Local);
var controls = zones.OrderBy(x => x.BaseUtcOffset)
                    .Select(x => new DateBox(x))
					.ToArray();
new WrapPanel(controls.Select(x => x.Controls).ToArray()).Dump();

Observable.Interval(1.Seconds())
          .Select(_ => DateTimeOffset.Now)
          .Subscribe(o =>
  		   {
		       foreach (var c in controls)
			       c.Update(o);
		   });

class DateBox 
{
	private TimeZoneInfo _tz;
	private DateTimeOffset _currentTime;
	public DateBox(TimeZoneInfo tz)
	{
		_tz=tz;
		Controls = new FieldSet(_tz.Id,Date,Time);
		Controls.CssChildRules["> span","padding-inline"] = "0.1em";
		Date.Click += ClipboardCopy;
		Time.Click += ClipboardCopy;
	}
	private void ClipboardCopy(object? sender, EventArgs ev)
	{
		void Run(object? text)
		{
			if (text == null) return;
			System.Windows.Forms.Clipboard.SetText((string)text);
		}
		ParameterizedThreadStart pts = Run;
		var t = new Thread(Run);
		t.SetApartmentState(ApartmentState.STA);
		t.Start(_currentTime.ToString());
		t.Join();
	}
	public void Update(DateTimeOffset o)
	{
		_currentTime = o.ToOffset(_tz.BaseUtcOffset);
		Date.Text = _currentTime.Date.ToShortDateString();
		Time.Text = _currentTime.DateTime.ToString("HH:MM:ss");
	}
	public Span Date { get; } = new();
	public Span Time { get; } = new();
	public FieldSet Controls { get; }
	public object ToDump() => Controls;
}
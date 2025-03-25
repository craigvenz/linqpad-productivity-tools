<Query Kind="Statements">
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>Humanizer</Namespace>
  <Namespace>LINQPad.Controls</Namespace>
  <AutoDumpHeading>true</AutoDumpHeading>
</Query>

// This code snippet creates a LINQPad control that shows the current time in different time zones.
// It uses the TimeZoneInfo class to get the time zones and the DateTimeOffset class to display the current time.
// The code creates a DateBox class that takes a TimeZoneInfo object and displays the current date and time in that time zone.
// The code also uses Reactive Extension's Observable.Interval method to update the time every second.

//This is here so you can see the timezones available on your system, uncomment to see them
//TimeZoneInfo.GetSystemTimeZones().Dump();

// Pick our list of timezones we want to see
var zones = new[] { "Central Standard Time", "Eastern Standard Time", "Mountain Standard Time", "Hawaiian Standard Time" }
            .Select(x => TimeZoneInfo.FindSystemTimeZoneById(x))
			// Add the local timezone to the list
			.Prepend(TimeZoneInfo.Local);
// Create a DateBox for each timezone, ordered by their base offset
var controls = zones.OrderBy(x => x.BaseUtcOffset)
                    .Select(x => new DateBox(x))
					.ToArray();
// Wrap the controls in a WrapPanel for better display
new WrapPanel(controls.Select(x => x.Controls).ToArray()).Dump();
// Update the time every second
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
		// Create the controls
		Controls = new FieldSet(_tz.Id,Date,Time);
		// Set the CSS for the controls
		Controls.CssChildRules["> span","padding-inline"] = "0.1em";
		// Add the event handlers - clicking the date or time copies it's text to clipboard
		Date.Click += ClipboardCopy;
		Time.Click += ClipboardCopy;
	}
	// Copy the text of the clicked control to the clipboard
	private void ClipboardCopy(object? sender, EventArgs ev)
	{
		void Run(object? text)
		{
			if (text == null) return;
			System.Windows.Forms.Clipboard.SetText((string)text);
		}
		// This code runs in a separate thread, because using the clipboard requires STA mode
		ParameterizedThreadStart pts = Run;
		var t = new Thread(Run);
		t.SetApartmentState(ApartmentState.STA);
		t.Start(_currentTime.ToString());
		t.Join();
	}
	// Update the date and time displayed
	public void Update(DateTimeOffset o)
	{
		_currentTime = o.ToOffset(_tz.BaseUtcOffset);
		Date.Text = _currentTime.Date.ToShortDateString();
		Time.Text = _currentTime.DateTime.ToString("HH:MM:ss");
	}
	// The controls for the DateBox
	public Span Date { get; } = new();
	public Span Time { get; } = new();
	public FieldSet Controls { get; }
	// ToDump defined on any object customizes the display in LINQPad
	public object ToDump() => Controls;
}
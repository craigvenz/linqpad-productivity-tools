<Query Kind="Program">
  <NuGetReference>Azure.Identity</NuGetReference>
  <NuGetReference>Azure.ResourceManager.Compute</NuGetReference>
  <NuGetReference>Azure.ResourceManager.Network</NuGetReference>
  <NuGetReference>Azure.ResourceManager.Resources</NuGetReference>
  <NuGetReference>System.Reactive</NuGetReference>
  <Namespace>Azure.Core</Namespace>
  <Namespace>Azure.ResourceManager</Namespace>
  <Namespace>Azure.ResourceManager.Compute</Namespace>
  <Namespace>Azure.ResourceManager.Resources</Namespace>
  <Namespace>LINQPad.Controls</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <DisableMyExtensions>true</DisableMyExtensions>
  <AutoDumpHeading>true</AutoDumpHeading>
</Query>

#load "Repo\Azure Credentials"
#load "Repo\GetCurrentUpn"

record DisplaySubscription(SubscriptionResource? Subscription)
{
	public override string ToString() => Subscription?.Data?.DisplayName ?? "-- Select a subscription --";
}
void Main()
{
	string authEndPoint = Util.AzureCloud.PublicCloud.AuthenticationEndpoint;

	string userHint = GetCurrentUPN()!;

	var credential = new LINQPadTokenCredential(authEndPoint + TenantId, userHint);

	var options = new ArmClientOptions();
	options.SetApiVersion(new ResourceType("Microsoft.Compute/cloudServices"), "2022-09-04");
	var armClient = new Azure.ResourceManager.ArmClient(credential, TenantId, options);

	var output = new DumpContainer();
	var subs = armClient.GetSubscriptions()
				        .Select(x => new DisplaySubscription(x))
						.Prepend(new DisplaySubscription(null))
						.ToArray();
	var subList = new SelectBox(SelectBoxKind.DropDown, subs).Dump();
	output.Dump();

	subList.SelectedObservable()
	       .Select(_ => (DisplaySubscription)subList.SelectedOption)
	       .Subscribe(l => ShowCloudServices(l) );
		   
	void ShowCloudServices(DisplaySubscription sub)
	{
		if (sub.Subscription == null)
		{
			output.ClearContent();
			return;
		}
		var cloudServices = new List<CloudServiceResource>();
		foreach (var cs in sub.Subscription.GetCloudServices())
			cloudServices.Add(cs);

		var hsc = EqualityComparer<string[]>.Create(
			(x, y) => x != null && y != null ? x.SequenceEqual(y) : false,
			obj => obj == null ? 0 : obj.Aggregate(15, (seed, item) => seed * 31 + (item?.GetHashCode() ?? 0))
		);

		var groups =
			cloudServices.GroupBy(cs => new HashSet<string> { cs.Data.Name, cs.Data.NetworkProfile.SwappableCloudServiceId?.Name }
											.Where(name => !string.IsNullOrEmpty(name))
											.OrderBy(name => name)
											.ToArray(),
										hsc)
						 .ToList();

		var rows = new List<TableRow>();
		rows.Add(
			new TableRow(true,
				new Span("Resource Group"),
				new Span("Name"),
				new Span("Live status"),
				new Span("Location"),
				new Span("Provisioning state"),
				new Span("Network Profile"),
				new Span("Configuration"),
				new Span("Extensions"),
				new Span("Roles"),
				new Span("Tags"),
				new Span("System Data"),
				new Span("Unique ID"))
		);
		foreach (var cs in cloudServices.OrderBy(cs => groups.FindIndex(group => group.Any(g => g.Data.Name == cs.Data.Name))))
		{
			var isLive = cs.Data.NetworkProfile.SlotType == Azure.ResourceManager.Compute.Models.CloudServiceSlotType.Production;
			rows.Add(
				new TableRow(
					new Span(cs.Id.ResourceGroupName),
					new Span(cs.Data.Name),
					new TableCell(new Span(isLive ? "Live" : "Not live")).Customize(n => n.HtmlElement.StyleAttribute = $"color: {(isLive ? "red" : "green")}"),
					new Span(cs.Data.Location.DisplayName),
					new Span(cs.Data.ProvisioningState),
					new Table(noBorders: true, rows: new TableRow[] {
					new(
						new Span(cs.Data.NetworkProfile.SlotType.ToString()),
						new Span(cs.Data.NetworkProfile.SwappableCloudServiceId?.Name ?? "None"),
						new Table(noBorders: true, rows:
							cs.Data.NetworkProfile.LoadBalancerConfigurations
								.Select(x =>
									new TableRow(new Span(x.Name),
										new Table(noBorders: true, rows:
											x.FrontendIPConfigurations.Select(fipc => new TableRow(new Span(fipc.Name), new Span(fipc.SubnetId), new Span(fipc.PublicIPAddressId?.Name ?? "None"), new Span(fipc.PrivateIPAddress)))
											.Prepend(new TableRow(true, new Span("Name"), new Span("Subnet ID"), new Span("Public IP Address ID"), new Span("Private IP Address")))
										)
									)
								))
								.WithHeader(new Span("Name"), new Span("Frontend IP Configurations"))

					)
					})
					.WithHeader(new Span("Slot Type"), new Span("Swappable Cloud Service Name"), new Span("Load Balancer Configurations")),
					Util.OnDemand("Configuration", () => Util.SyntaxColorText(cs.Data.Configuration, SyntaxLanguageStyle.XML, true)),
					cs.Data.Extensions.OnDemand("Extensions"),
					new Table(noBorders: true, rows: cs.Data.Roles.Select(x => new TableRow(
						new Span(x.Name), new Span(x.Sku.Name), new Span(x.Sku.Tier), new Span(x.Sku.Capacity.ToString())
					))).WithHeader(new Span("Name"), new Span("Sku"), new Span("Tier"), new Span("Capacity")),
					cs.Data.Tags.EmptyIfNone(
						new TableRow(true, new Span("Tag"), new Span("Value")),
						x => new TableRow(new Span(x.Key), new Span(x.Value))
					),
					Util.OnDemand("SystemData", () => new
					{
						cs.Data.SystemData.CreatedBy,
						CreatedOn = cs.Data.SystemData.CreatedOn.GetValueOrDefault().ToLocalTime(),
						cs.Data.SystemData.LastModifiedBy,
						LastModifiedOn = cs.Data.SystemData.LastModifiedOn.GetValueOrDefault().ToLocalTime()
					}),
					new Span(cs.Data.UniqueId)
				)
			);
		}
		output.Content = new Table(noBorders: false,
			rows: rows
		);
	}
}
public static class TableExtension
{
	public static Table WithHeader(this Table t, params IEnumerable<Control> controls) {
		var nonHeaderRows = t.Rows.Where(x => !x.Cells.Any(y => y.HtmlElement.Name == "th")).ToList();
		t.Rows.Clear();
		t.Rows.Add(new TableRow(true, controls));
		t.Rows.AddRange(nonHeaderRows);
		return t;
	}
	public static Control EmptyIfNone<TKey,TValue>(this IDictionary<TKey,TValue> values, TableRow header, Func<KeyValuePair<TKey,TValue>,TableRow> itemSelect)
	{
		var dc = new DumpContainer();
		if (!values.Any())
			dc.Content = Util.Metatext("None");
		else
			dc.Content = new Table(noBorders: true, rows: values.Select(itemSelect).Prepend(header));
		return dc;
	}
	public static T Customize<T>(this T control, Action<T> action)
	{
		action(control);
		return control;
	}
}
public static class ObservableControlExtensions
{
	public static IObservable<System.Reactive.EventPattern<object>> TextInputObservable(this LINQPad.Controls.ITextControl t)
	=> Observable.FromEventPattern(o => t.TextInput += o, o => t.TextInput -= o);

	public static IObservable<System.Reactive.EventPattern<object>> ClickObservable(this LINQPad.Controls.Control c)
	=> Observable.FromEventPattern(o => c.Click += o, o => c.Click -= o);

	public static IObservable<System.Reactive.EventPattern<object>> GotFocusObservable(this LINQPad.Controls.Control c)
	=> Observable.FromEventPattern(o => c.GotFocus += o, o => c.GotFocus -= o);

	public static IObservable<System.Reactive.EventPattern<object>> LostFocusObservable(this LINQPad.Controls.Control c)
	=> Observable.FromEventPattern(o => c.LostFocus += o, o => c.LostFocus -= o);

	public static IObservable<System.Reactive.EventPattern<object>> RenderingObservable(this LINQPad.Controls.Control c)
	=> Observable.FromEventPattern(o => c.Rendering += o, o => c.Rendering -= o);

	public static IObservable<System.Reactive.EventPattern<object>> SelectedObservable(this LINQPad.Controls.SelectBox c)
	=> Observable.FromEventPattern(o => c.SelectionChanged += o, o => c.SelectionChanged -= o);

	public static IObservable<System.Reactive.EventPattern<object>> ValueInputObservable(this LINQPad.Controls.RangeControl c)
	=> Observable.FromEventPattern(o => c.ValueInput += o, o => c.ValueInput -= o);
}

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
  <Namespace>System.Reactive</Namespace>
  <DisableMyExtensions>true</DisableMyExtensions>
  <AutoDumpHeading>true</AutoDumpHeading>
</Query>

#load "Azure Credentials"
#load "GetCurrentUpn"

// This code snippet lists all the cloud services in all the subscriptions for the authenticated user.
// It uses the Azure.ResourceManager.Compute library to get the cloud services.
// The code creates a dropdown list of subscriptions and a table of cloud services for the selected subscription.
// The cloud services are grouped by their name and swap partner, and the table shows the details of each cloud service.
void Main()
{
	// Add custom css style to header output
	Util.HtmlHead.AddStyles(
"""
th { 
    font-weight: bold; font-style: italic;
}
""");
    // Get the currently logged in User on this computer
    string userHint = GetCurrentUPN()!;

    // Use LINQpad's built-in Azure Credentials connection to authenticate
    string authEndPoint = Util.AzureCloud.PublicCloud.AuthenticationEndpoint;

    // Create a new LINQPadTokenCredential object with the authEndPoint and TenantId
    // TenantId comes from the Azure Credentials query, see it for more details
    var credential = new LINQPadTokenCredential(authEndPoint + TenantId, userHint);
	// Create a new ArmClientOptions object to set the API version for the Cloud Services
    var options = new ArmClientOptions();
    options.SetApiVersion(new ResourceType("Microsoft.Compute/cloudServices"), "2022-09-04");
    var armClient = new Azure.ResourceManager.ArmClient(credential, TenantId, options);

    // Create a new DumpContainer object to hold the output
    var output = new DumpContainer();
    // Get all the subscriptions for the authenticated user
    var subs = armClient.GetSubscriptions()
                        .Select(x => new DisplaySubscription(x))
                        // Prepend an empty DisplaySubscription to show 'Select a subscription' as the first item
                        .Prepend(new DisplaySubscription())
                        .ToArray();

    // Display the subscriptions in a dropdown list and the output container underneath.
    var subList = new SelectBox(SelectBoxKind.DropDown, subs).Dump();
    output.Dump();

    // Subscribe to the SelectedObservable of the dropdown list and call ShowCloudServices when a new subscription is selected
    subList.SelectedObservable()
           .Select(_ => (DisplaySubscription)subList.SelectedOption)
           .Subscribe(s => ShowCloudServices(s));
           
    void ShowCloudServices(DisplaySubscription sub)
    {
        // If no subscription is selected, clear the output container and return
        if (sub.Subscription == null)
        {
            output.ClearContent();
            return;
        }

        var cloudServices = sub.Subscription.GetCloudServices().ToList();

        // Create a custom EqualityComparer for string[], so that cloud services are grouped with their swap partners
        var hashSetComparer = EqualityComparer<string[]>.Create(
            (x, y) => x != null && y != null ? x.SequenceEqual(y) : false,
            obj => obj == null ? 0 : obj.Aggregate(15, (seed, item) => seed * 31 + (item?.GetHashCode() ?? 0))
        );

        // Group the cloud services by their name and swap partner
		// This works by taking the name of the cloud service and the name of the swap partner (if it exists)
		// and creating a HashSet of those names. Then it groups the cloud services by that HashSet.
        var groups =
            cloudServices.GroupBy(cs => new HashSet<string> { cs.Data.Name,
			                                                  cs.Data.NetworkProfile.SwappableCloudServiceId?.Name! }
                                            .Where(name => !string.IsNullOrEmpty(name)) // Filter out null or empty names
                                            .ToArray(),
                                        hashSetComparer)
                         .ToList();

		// Create a list of TableRow objects to hold the data for the cloud services
		var services = new Table(noBorders: false);
		services.Rows.Add(
			new TableRow(true,
				CreateHeader("Resource Group", "Name", "(Swap) Live status", "Location", "Provisioning state",
						     "Network Profile", "Configuration", "Extensions", "Roles", "Tags",
						     "System Data", "Unique ID")
			)
        );
        // Iterate over the cloud services, ordering them by the group index
        foreach (var cs in cloudServices.OrderBy(cs => groups.FindIndex(group => group.Any(g => g.Data.Name == cs.Data.Name)))
		                                .Select(x => x.HasData ? x.Data : null).Where(x => x != null))
		{
			// this all used to be inline in a bunch of selects, then I had a debugging error and it was a nightmare to find
			// where the issue was. I've just left it all expanded out like this so it's easier to read, anyway, and you 
			// can tell what I'm doing with contructing the hierarchy of tables.
			var resourceGroup = DisplayOrNone(cs?.Id?.ResourceGroupName);
			var name = DisplayOrNone(cs?.Name);
			var liveStatus = DisplayLiveStatus(cs);
			var location = DisplayOrNone(cs?.Location.DisplayName);
			var provisioningState = DisplayOrNone(cs?.ProvisioningState);
			var slotType = DisplayOrNone(cs?.NetworkProfile?.SlotType?.ToString());
			var swapName = DisplayOrNone(cs?.NetworkProfile?.SwappableCloudServiceId?.Name);
			var lbConfig = new Table(noBorders: true);
			lbConfig.Rows.Add(new TableRow(true, CreateHeader("Name", "Frontend IP Configurations")));
			foreach (var x in cs?.NetworkProfile?.LoadBalancerConfigurations ?? [])
			{
				var frontEndIpConfig = new Table(noBorders: true);
				frontEndIpConfig.Rows.Add(new TableRow(true, CreateHeader("Name", "Subnet ID", "Public IP Address ID", "Private IP Address")));
				var lbName = DisplayOrNone(x?.Name);
				foreach (var fipc in x?.FrontendIPConfigurations ?? [])
				{
					var feName = DisplayOrNone(fipc?.Name);
					var subnet = DisplayOrNone(fipc?.SubnetId?.Name);
					var publicIp = DisplayOrNone(fipc?.PublicIPAddressId?.Name);
					var privateIp = DisplayOrNone(fipc?.PrivateIPAddress);
					frontEndIpConfig.Rows.Add(new TableRow(feName, subnet, publicIp, privateIp));
				}
				lbConfig.Rows.Add(new TableRow(lbName, frontEndIpConfig));
			}
			var networkProfile = new Table(noBorders: true);
			networkProfile.Rows.Add(new TableRow(true, CreateHeader("Slot Type", "Swappable Cloud Service Name", "Load Balancer Configurations")));
			networkProfile.Rows.Add(new TableRow(slotType, swapName, lbConfig));
			var roles = new Table(noBorders: true);
			roles.Rows.Add(new TableRow(true, CreateHeader("Name", "Sku", "Tier", "Capacity")));
			foreach (var x in cs?.Roles ?? [])
			{
				var roleName = DisplayOrNone(x?.Name);
				var skuName = DisplayOrNone(x?.Sku?.Name);
				var skuTier = DisplayOrNone(x?.Sku?.Tier);
				var capacity = DisplayOrNone(x?.Sku?.Capacity?.ToString());
				roles.Rows.Add(new TableRow(roleName, skuName, skuTier, capacity));
			}
			var uniqueId = DisplayOrNone(cs?.UniqueId);
			var panel = new TogglePanel(Util.SyntaxColorText(cs?.Configuration, SyntaxLanguageStyle.XML, true), "Configuration");
			var tr = new TableRow(
				resourceGroup, name, liveStatus, location,
				provisioningState, networkProfile,
				// This used to be Util.OnDemand as well, but I discovered if you passed in the value
				// as I have it here, you can't collapse it again, like you can the standard IEnumerable displays.
				// So I rolled this TogglePanel class real quick to fix the issue.
				// Because most LINQpad control constructors expect parameters that inherit from Control. The way around this
				// is to use DumpContainer, as it accepts any object, and DumpContainer has a built in implicit conversion to 
				// Control. Most (not all, OnDemand for example) display niceties that the Util class in Linqpad provides also 
				// are typed as object, so you end up having to use this trick for those, too.
				new DumpContainer(panel),
				// Use Util.OnDemand to only show the Extensions data if you click the link
				Util.OnDemand("Extensions", () => cs?.Extensions),
				roles,
				// Show Tags - if there are no tags, show 'None'
				DisplayTags(cs?.Tags),
				// Use Util.OnDemand to only show the auditing info if you click the link
				Util.OnDemand("SystemData", () => new
				{
					cs?.SystemData?.CreatedBy,
					CreatedOn = cs?.SystemData?.CreatedOn.GetValueOrDefault().ToLocalTime(),
					cs?.SystemData?.LastModifiedBy,
					LastModifiedOn = cs?.SystemData?.LastModifiedOn.GetValueOrDefault().ToLocalTime()
				}),
				uniqueId
			);
			services.Rows.Add(tr);
		}
		// Set the output container to the Table we created
        output.Content = services;
		// Determine if the cloud service is live - it's live if the SlotType is Production
		// Complication: if this cloud service doesn't have a swap partner, SlotType is null.
		static TableCell DisplayLiveStatus(CloudServiceData? cs)
		{
			var isNull = cs?.NetworkProfile?.SlotType.HasValue;
			var cell = new TableCell();
			if (isNull == false)
			{
				cell.Children.Add(new Span("None"));
				cell.HtmlElement.StyleAttribute = $"color: #707070";
			}
			else
			{
				var isLive = cs?.NetworkProfile?.SlotType.GetValueOrDefault() == Azure.ResourceManager.Compute.Models.CloudServiceSlotType.Production;
				cell.Children.Add(new Span(isLive ? "Live" : "Not live"));
				// Live status - color the text red if live, green if not
				cell.HtmlElement.StyleAttribute = $"color: {(isLive ? "red" : "green")}";
			}
			return cell;
		}
		// see above about using DumpContainer and Util classes. 
		static Control DisplayTags(IDictionary<string, string>? tags)
		{
			var display = new DumpContainer();
			if (tags?.Any() == false)
				// Here Util.Metatext returns object.
				display.Content = Util.Metatext("None");
			else
			{
				var tagTable = new Table(noBorders: true);
				tagTable.Rows.Add(new TableRow(true, CreateHeader("Tag", "Value")));
				tagTable.Rows.AddRange(
					from x in tags
					select new TableRow(new Span(x.Key), new Span(x.Value))
				);
				display.Content = tagTable;
			}
			return display;
		}
		static Control DisplayOrNone(string? str)
		{
			if (string.IsNullOrEmpty(str))
				return new DumpContainer(Util.Metatext("null"));
			return new Span(str);
		}
	}
}
// This record is used to display the subscription in the dropdown list and print 'Select a subscription' as the first item
record DisplaySubscription(SubscriptionResource? Subscription)
{
	public DisplaySubscription() : this((SubscriptionResource?)null) { }
	public override string ToString() => Subscription?.Data?.DisplayName ?? "-- Select a subscription --";
}
// The repetitive nature of declaring `new Span("sometext")` continually, annoyed me so I cleaned it up this way.
static IEnumerable<Control> CreateHeader(params IEnumerable<string> labels) => from l in labels select new Span(l);
// Provides shorthand for getting an observable from various LINQPad control events
public static class ObservableControlExtensions
{
    public static IObservable<EventPattern<object>> TextInputObservable(this ITextControl t)
	    => Observable.FromEventPattern(o => t.TextInput += o, o => t.TextInput -= o);

    public static IObservable<EventPattern<object>> ClickObservable(this Control c)
    	=> Observable.FromEventPattern(o => c.Click += o, o => c.Click -= o);

    public static IObservable<EventPattern<object>> GotFocusObservable(this Control c)
    	=> Observable.FromEventPattern(o => c.GotFocus += o, o => c.GotFocus -= o);

    public static IObservable<EventPattern<object>> LostFocusObservable(this Control c)
    	=> Observable.FromEventPattern(o => c.LostFocus += o, o => c.LostFocus -= o);

    public static IObservable<EventPattern<object>> RenderingObservable(this Control c)
    	=> Observable.FromEventPattern(o => c.Rendering += o, o => c.Rendering -= o);

    public static IObservable<EventPattern<object>> SelectedObservable(this SelectBox c)
    	=> Observable.FromEventPattern(o => c.SelectionChanged += o, o => c.SelectionChanged -= o);

    public static IObservable<EventPattern<object>> ValueInputObservable(this RangeControl c)
    	=> Observable.FromEventPattern(o => c.ValueInput += o, o => c.ValueInput -= o);
}
// This class is a quick and dirty toggleable visual 'control' with a clickable hyperlink at the top, that shows or hides
// the content passed in the constructor. I was trying to initially implement this by inheriting the Linqpad Control class,
// but wasn't quite sure I understood the mechanics of it by looking at the source in ILspy, so rather than experiement a 
// long time figuring it out, I just did this because I've done similar things before in other scripts & I know it works.
public class TogglePanel
{
	// Track shown or hidden state
	private bool _state;    
	// Control for opening the panel
	private Hyperlink _open;
	// Control for closing the panel
	private Hyperlink _close;
	// Control display area container
	private DumpContainer _displayArea;
	// Content display container
	private DumpContainer _contentArea;

	public TogglePanel(object content, string label = "Open", bool startHidden = true) 
	{
		_state = !startHidden;
		// create the open control. CssClass arrow-down is what's used by Linqpad's internal type display system so I borrowed it.
		_open = new Hyperlink(label, ToggleClick) { Visible = !_state, CssClass = "arrow-down" };
		// create the open control. CssClass arrow-up is what's used by Linqpad's internal type display system so I borrowed it.
		_close = new Hyperlink(label, ToggleClick) { Visible = !_state, CssClass = "arrow-up" };
		// create the containers
	 	_displayArea = new();
		// set the content container's content to content.
		_contentArea = new(content);
		// run update logic for initial display
		Update();
		return;
		
		void ToggleClick(Hyperlink _)
		{
			_state = !_state; 
			Update(); 
		}
	}
	
	private void Update()
	{
		// Clear the display area
		_displayArea.ClearContent();
		// if we're closed, just add the open link
		if (!_state)
			_displayArea.AppendContent(_open);
		else
		{ // we're open, add the close link, a separator and the content container
			_displayArea.AppendContent(_close);
			_displayArea.AppendContent(Util.RawHtml("<hr/>"));
			_displayArea.AppendContent(_contentArea);
		}
	}
	// Any class that defines a public ToDump method is called when 
	// Linqpad dumps it, and you can customize the display this way.
	// This link doesn't work but you can follow it to find where I got this info from.
	// query://Samples/LINQPad_Tutorial_&_Reference/Customization_&_Extensibility/Customizing_Dump
	public object ToDump() => _displayArea;
}

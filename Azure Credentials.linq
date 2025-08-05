<Query Kind="Program">
  <NuGetReference>Azure.Identity</NuGetReference>
  <NuGetReference>Azure.ResourceManager.Resources</NuGetReference>
  <Namespace>Azure.Core</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Azure.ResourceManager</Namespace>
</Query>

#load "GetCurrentUpn"

public class LINQPadTokenCredential : TokenCredential
{
	public readonly string Authority, UserIDHint;
    public readonly string[] Scopes;
    public readonly bool Refresh;

    public LINQPadTokenCredential(string authority, string userIDHint, bool refresh = false) : this(authority, userIDHint, refresh, []) { }
    public LINQPadTokenCredential (string authority, string userIDHint, bool refresh = false, params string[] scopes) =>
		(Authority, UserIDHint, Scopes, Refresh) = (authority, userIDHint, scopes ?? Array.Empty<string>(), refresh);

	public override AccessToken GetToken (TokenRequestContext requestContext, CancellationToken cancelToken)
		=> GetTokenAsync(requestContext, cancelToken).ConfigureAwait(false).GetAwaiter().GetResult();

	public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancelToken)
    {
        Util.IAuthenticationToken auth;
        var promptValue = Refresh ? Util.MSAL.Prompt.ForceLogin : Util.MSAL.Prompt.NoPromptUnlessNecessary;
        if (Scopes.Length > 0)
            auth = await Util.MSAL.AcquireTokenAsync(Authority, Scopes, UserIDHint, prompt: promptValue).ConfigureAwait(false);
        else
            auth = await Util.MSAL.AcquireTokenAsync(Authority,UserIDHint, prompt: promptValue).ConfigureAwait(false);
		return new AccessToken(auth.AccessToken, auth.ExpiresOn);
	}
}
public const string TenantKey = "DefaultTenantId";
public string TenantId
{
    get
    {
        var tenantId = Util.LoadString(TenantKey);
        if (string.IsNullOrEmpty(tenantId))
        {
			//TODO: Set your default Tenant here
			TenantId = "68317e6a-8bfe-41ca-b265-86530f43f83f";
        }
        return tenantId;
    }
	set
	{
		Util.SaveString(TenantKey, value);
	}
}
// And here's how to use it:
async Task Main()
{
	// Edit this if you're not using Azure Public Cloud
	string authEndPoint = Util.AzureCloud.PublicCloud.AuthenticationEndpoint;
	
	string userHint = GetCurrentUPN()!;

	// Authenticate to the Azure management API:
	var credential = new LINQPadTokenCredential(authEndPoint + TenantId, userHint, scopes: "https://management.azure.com/.default");
	var client = new ArmClient(credential);

	// Dump the default subscription:
	var sub = await client.GetDefaultSubscriptionAsync();
    sub.Data.Dump(1);
}

<Query Kind="Program">
  <NuGetReference>Azure.Identity</NuGetReference>
  <NuGetReference>Azure.ResourceManager.Resources</NuGetReference>
  <Namespace>Azure.Core</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Azure.ResourceManager</Namespace>
</Query>

// To use any of the Azure.* management libraries, you must authenticate by providing a TokenCredential
// (Azure.Core.TokenCredential in Azure.Identity.dll).

#load "GetCurrentUpn"

// Here's the code you need to define a TokenCredential that works with LINQPad's authentication manager:
// (Sample taken from LINQpad samples)
public class LINQPadTokenCredential : TokenCredential
{
	public readonly string Authority, UserIDHint;

	public LINQPadTokenCredential (string authority, string userIDHint) =>
		(Authority, UserIDHint) = (authority, userIDHint);

	public override AccessToken GetToken (TokenRequestContext requestContext, CancellationToken cancelToken)
		=> GetTokenAsync (requestContext, cancelToken).Result;

	public override async ValueTask<AccessToken> GetTokenAsync (TokenRequestContext requestContext, CancellationToken cancelToken)
	{
		var auth = await Util.MSAL.AcquireTokenAsync (Authority, requestContext.Scopes, UserIDHint).ConfigureAwait (false);
		return new AccessToken (auth.AccessToken, auth.ExpiresOn);
	}
}

public const string TenantKey = "DefaultTenantId";
// If you load this query in another query (which is the point of it), your default Tenant ID will
// be returned by this property. 
public string TenantId
{
    get
    {
        var tenantId = Util.LoadString(TenantKey);
        if (string.IsNullOrEmpty(tenantId))
        {
			//TODO: Set your default Tenant here
			TenantId = "deadbeef-dead-beef-dead-beefdeadbeef";
        }
        return tenantId;
    }
	set
	{
		// Save the tenant ID for future use in LINQPad's user data store
		Util.SaveString(TenantKey, value);
	}
}
// Usage example:
void Main()
{
	// Edit this if you're not using Azure Public Cloud
	string authEndPoint = Util.AzureCloud.PublicCloud.AuthenticationEndpoint;
	
	string userHint = GetCurrentUPN()!;

	// Authenticate to the Azure management API:
	var credential = new LINQPadTokenCredential (authEndPoint + TenantId, userHint);
	var client = new ArmClient (credential);

	// Dump the default subscription:
	client.GetDefaultSubscription().Data.Dump(1);
}

<Query Kind="Program">
  <Namespace>System.Runtime.InteropServices</Namespace>
</Query>
// This code snippet is a helper to get the current user's UPN (User Principal Name) in Windows.
// It uses the GetUserNameEx function from secur32.dll to get the UPN.
// Packaging it into a .linq query makes it referenceable easily from other queries.
// Usage example: #load "GetCurrentUpn"
//                string userHint = GetCurrentUPN()!;
void Main()
{
    Console.WriteLine(GetCurrentUPN());
}
/// <summary>
/// Get the current user's User Principal Name (UPN).
/// </summary>
/// <returns>The UPN of the current user or null if it fails.</returns>
public static string? GetCurrentUPN()
{
    StringBuilder userUPN = new StringBuilder(1024);
    int userUPNSize = userUPN.Capacity;

    if (GetUserNameEx((int)ExtendedFormat.NameUserPrincipal, userUPN, ref userUPNSize) != 0)
    {
        return userUPN.ToString();
    }

    return null;
}
// ExtendedFormat enum values from https://docs.microsoft.com/en-us/windows/win32/api/secext/nf-secext-getusernameexa 
// and https://stackoverflow.com/a/67922270/223942
internal enum ExtendedFormat
{
    NameUnknown = 0,
    NameFullyQualifiedDN = 1,
    NameSamCompatible = 2,
    NameDisplay = 3,
    NameUniqueId = 6,
    NameCanonical = 7,
    NameUserPrincipal = 8,
    NameCanonicalEx = 9,
    NameServicePrincipal = 10,
    NameDnsDomain = 12,
}

[DllImport("secur32.dll", CharSet = CharSet.Unicode)]
internal static extern int GetUserNameEx(int nameFormat, StringBuilder userName, ref int userNameSize);
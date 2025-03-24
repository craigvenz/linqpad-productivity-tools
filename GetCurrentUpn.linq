<Query Kind="Program">
  <Namespace>System.Runtime.InteropServices</Namespace>
</Query>

void Main()
{
    Console.WriteLine(GetCurrentUPN());
}
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
// https://stackoverflow.com/a/67922270/223942
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
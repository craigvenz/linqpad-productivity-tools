<Query Kind="Statements">
  <Namespace>System.Security.Cryptography.X509Certificates</Namespace>
  <Namespace>LINQPad.Controls</Namespace>
</Query>

// This code snippet creates a LINQPad control that allows you to validate a PKCS#12 certificate file and save its password.
// It uses the X509Certificate2 class to load the certificate from the file and the PasswordBox class to get the password.
// FilePicker is a LINQPad control that allows you to pick a file from the file system. I've defaulted it to the Downloads folder.
var fp = new FilePicker() { Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") };
// PasswordBox is a LINQPad control that hides the password. If you want to see the password, use TextBox instead.
var pw = new PasswordBox();
var dc = new DumpContainer() { DumpDepth = 2 };
// Create a horizontal layout for the file picker and password box
Util.HorizontalRun(true, fp, new Span("Password:"), pw).Dump();
// Create a button to validate the certificate
new Button("Validate",_ => {
	var fi = new FileInfo(fp.Text);
	// Check if the file exists
	if (!fi.Exists)
	{
		// Show an error message if the file doesn't exist
		dc.Content = "File not found";
		dc.AppendContent(fi);
		return;
	}
	// Load the certificate from the file
	try
	{
		var cert = X509CertificateLoader.LoadPkcs12FromFile(fp.Text, pw.Text);
		// Show the certificate details
		dc.Content = cert;
		dc.AppendContent($"Setting password for {cert.Thumbprint.ToUpperInvariant()}");
		// Save the password for the certificate in LINQpad's Password manager
		Util.SetPassword(cert.Thumbprint.ToUpperInvariant(), pw.Text);
		// To retrieve the password later, use:
		// Util.GetPassword(cert.Thumbprint.ToUpperInvariant());
	}
	catch (Exception ex)
	{
		// Show an error message if the certificate loading fails
		dc.Content = ex;
	}
}).Dump();
dc.Dump();

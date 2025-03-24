<Query Kind="Statements">
  <Namespace>System.Security.Cryptography.X509Certificates</Namespace>
  <Namespace>LINQPad.Controls</Namespace>
</Query>

var fp = new FilePicker() { Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") };
var pw = new PasswordBox();
var dc = new DumpContainer() { DumpDepth = 2 };
Util.HorizontalRun(true, fp, new Span("Password:"), pw).Dump();
new Button("Validate",_ => {
	var fi = new FileInfo(fp.Text);
	if (!fi.Exists)
	{
		dc.Content = "File not found";
		dc.AppendContent(fi);
		return;
	}
	try
	{
		var cert = X509CertificateLoader.LoadPkcs12FromFile(fp.Text, pw.Text);
		dc.Content = cert;
		dc.AppendContent($"Setting password for {cert.Thumbprint.ToUpperInvariant()}");
		Util.SetPassword(cert.Thumbprint.ToUpperInvariant(), pw.Text);
		Util.GetPassword(cert.Thumbprint.ToUpperInvariant());
	}
	catch (Exception ex)
	{
		dc.Content = ex;
	}
}).Dump();
dc.Dump();

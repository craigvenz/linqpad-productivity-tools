<Query Kind="Program">
  <Namespace>System.Security.Cryptography.X509Certificates</Namespace>
  <Namespace>LINQPad.Controls</Namespace>
  <Namespace>Azure.Security.KeyVault.Secrets</Namespace>
  <Namespace>Azure.Core</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
</Query>

void Main()
{
    var ta = new TextArea();
    var pw = new TextBox();
    var dc = new DumpContainer() { DumpDepth = 2 };
    Util.HorizontalRun(true, ta, new Span("Password:"), pw).Dump();
    new Button("Validate",_ => {
        try
        {
            var bytes = Convert.FromBase64String(ta.Text);
            var cert = X509CertificateLoader.LoadPkcs12(bytes, pw.Text);
            dc.Content = cert;
        }
        catch (Exception ex)
        {
            dc.Content = ex;
        }
    }).Dump();
    dc.Dump();
}

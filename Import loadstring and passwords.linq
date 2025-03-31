<Query Kind="Statements">
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <Namespace>LINQPad.Controls</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
  <AutoDumpHeading>true</AutoDumpHeading>
</Query>

const string LoadStringType = "userdata";
const string PasswordType = "passwords";
var ta = new TextArea().Dump("Paste your json here");
new Button("Import",_ => {
    var data = JsonConvert.DeserializeAnonymousType(ta.Text, new { DataType = "", Data = new Dictionary<string,string>() });
    Action<string,string> importFunc = data.DataType switch {
        LoadStringType => (a,b) => Util.SaveString(a,b),
        PasswordType => (a,b) => Util.SetPassword(a,b)
    };
    foreach (var pair in data.Data)
    {
        Console.WriteLine("Saving string '{0}'", pair.Key);
        importFunc(pair.Key, Encoding.UTF8.GetString(Convert.FromBase64String(pair.Value)));
    }
}).Dump();

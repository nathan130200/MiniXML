using System.Diagnostics;
using MiniXML;

Element elem;

using (var fs = File.OpenRead(@".\data\Input.xml"))
    elem = Xml.Parse(fs);

var query = elem.GetChild(x => x.Name == "G07ndc7E" && x.HasAttribute("pride"), true);
Debugger.Break();

// text content and attribute values are escaped when parsed.
Debug.Assert(query.Value == "9MXJ<$");
Debugger.Break();

File.WriteAllText(@".\data\Output.xml", elem.ToString(indent: false));
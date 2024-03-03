using MiniXML;

var result = Xml.Element("r");

using (var fs = File.OpenRead(@".\Input.xml"))
    result = Xml.Parse(fs);

File.WriteAllText(@".\Output.xml", result.ToString(false));
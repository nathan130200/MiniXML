using System.Text;
using System.Xml.Linq;

using var fs = File.OpenRead(@".\namespaces.xml");

var root = XElement.Load(fs);
var sb = new StringBuilder(@$"namespace MiniXML;

public static class Namespace
{{");

foreach (var entry in root.Elements())
{
    var comment = entry.Attribute("comment")?.Value;
    var helpUrl = entry.Attribute("help")?.Value;

    if (entry.Name == "item")
    {
        sb.AppendLine();
        var name = entry.Attribute("name")!.Value;
        var ns = entry.Attribute("value")!.Value;
        WriteDocString(ns, comment, helpUrl);
        sb.Append($"\tpublic const string {name} = \"{ns}\";\n");
    }
    else if (entry.Name == "section")
    {
        foreach (var item in entry.Descendants("item"))
        {
            sb.AppendLine();
            var name = item.Attribute("name")!.Value;
            var ns = item.Attribute("value")!.Value;
            WriteDocString(ns, comment, helpUrl);
            sb.Append($"\tpublic const string {name} = \"{ns}\";\n");
        }
    }
}

sb.Append('\n').Append('}');

File.WriteAllText(@"..\MiniXML\Namespaces.cs", sb.ToString());

void WriteDocString(string ns, string? desc, string? webPage)
{
    string docText = string.Empty;

    if (webPage != null)
        docText = "<a href=\"" + webPage + "\">" + (desc ?? ns) + "</a> — <c>" + ns + "</c>";
    else
    {
        if (desc != null)
            docText += desc + " — ";

        docText += "<c>" + ns + "</c>";
    }

    sb.Append("\t/// <summary>\n");
    sb.Append($"\t/// {docText}\n");
    sb.Append("\t/// </summary>\n");
}
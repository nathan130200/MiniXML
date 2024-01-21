using System.Globalization;
using System.Xml;

namespace MiniXML;

public static class Xml
{
    internal static string NormalizeQualifiedName(string qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
            goto fail;

        var ofs = qualifiedName.IndexOf(':');

        if (ofs == -1)
            return XmlConvert.EncodeLocalName(qualifiedName);
        else
        {
            var prefix = qualifiedName[0..ofs];
            var localName = qualifiedName[(ofs + 1)..];

            if (string.IsNullOrWhiteSpace(localName))
                goto fail;

            return string.Concat(XmlConvert.EncodeLocalName(prefix),
                ':', XmlConvert.EncodeLocalName(localName));
        }

    fail:
        throw new ArgumentNullException(nameof(qualifiedName), "Local name cannot be null or empty.");
    }

    public static Element Attrs(this Element element, in Dictionary<string, object>? attrs)
    {
        if (attrs == null)
            goto next;

        foreach (var (key, value) in attrs)
            element.SetAttribute(key, SerializeValue(value));

        next:
        return element;
    }

    static string SerializeValue(object rawValue)
    {
        string value;

        if (rawValue is string s)
            value = s;
        else if (rawValue is null)
            value = string.Empty;
        else if (rawValue is IFormattable fmt)
            value = fmt.ToString(default, CultureInfo.InvariantCulture);
        else
            value = rawValue.ToString() ?? string.Empty;

        return value;
    }

    public static Element Attr(this Element element, string name, object value)
    {
        element.SetAttribute(name, SerializeValue(value));
        return element;
    }

    public static Element Element(string name, in Dictionary<string, object>? attrs = default)
    {
        var child = new Element(name);
        Attrs(child, attrs);
        return child;
    }

    public static Element Child(this Element element, string name, string xmlns, in Dictionary<string, object>? attrs = default)
    {
        var child = new Element(name, xmlns);
        element.AddChild(child);
        return child;
    }

    public static Element Child(this Element element, string name, in Dictionary<string, object>? attrs = default)
    {
        var child = new Element(name);
        element.AddChild(child);
        return child;
    }

    public static Element? Root(this Element element)
    {
        if (element == null)
            return default;

        while (!element.IsRootElement)
            element = element.Parent!;

        return element;
    }

    public static Element? Up(this Element element)
    {
        if (element.IsRootElement)
            return element;

        return element.Parent;
    }

    public static Element Text(this Element element, string? text = default)
    {
        element.Value = text;
        return element!;
    }
}

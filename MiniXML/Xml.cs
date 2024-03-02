using System.Globalization;
using System.Text;
using System.Xml;

namespace MiniXML;

/// <summary>
/// Utilities for manipulating XML elements fluently.
/// </summary>
public static class Xml
{
    internal static string NormalizeXmlName(this string source)
    {
        var hasPrefix = source.DeconstructXmlName(out var local, out var prefix);

        if (!hasPrefix)
            return local;

        return string.Concat(prefix, ':', local);
    }

    internal static bool DeconstructXmlName(this string source, out string localName, out string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var ofs = source.IndexOf(':');
        prefix = default;

        if (ofs == -1)
        {
            localName = XmlConvert.EncodeLocalName(source);
            return false;
        }
        else
        {
            prefix = XmlConvert.EncodeLocalName(source[0..ofs]);
            localName = XmlConvert.EncodeLocalName(source[(ofs + 1)..]);
            return true;
        }
    }

    internal static string WriteTree(Element e, bool indent, bool innerOnly)
    {
        var sb = new StringBuilder();

        using (var sw = new StringWriter(sb))
        {

            var settings = new XmlWriterSettings
            {
                Indent = indent,
                IndentChars = " ",
                CloseOutput = true,
                ConformanceLevel = ConformanceLevel.Fragment,
                OmitXmlDeclaration = true,
                NamespaceHandling = NamespaceHandling.OmitDuplicates,
                Encoding = Encoding.UTF8
            };

            using var writer = XmlWriter.Create(sw, settings);
            WriteTreeInternal(e, writer, innerOnly);
        }

        return sb.ToString();
    }

    static void WriteTreeInternal(Element e, XmlWriter w, bool innerOnly)
    {
        if (!innerOnly)
        {
            var prefix = e.Prefix;
            var localName = e.LocalName;
            var ns = e.GetNamespace(prefix);

            if (prefix == null)
                w.WriteStartElement(localName, ns);
            else
                w.WriteStartElement(prefix, localName, ns);

            foreach (var (name, value) in e.Attributes)
            {
                var hasPrefix = name.DeconstructXmlName(out localName, out prefix);

                if (!hasPrefix)
                    w.WriteAttributeString(name, value);
                else
                {
                    if (prefix == "xml")
                        w.WriteAttributeString(localName, Namespaces.Xml, value);
                    else if (prefix == "xmlns")
                        w.WriteAttributeString(localName, Namespaces.Xmlns, value);
                    else
                        w.WriteAttributeString(localName, e.GetNamespace(prefix) ?? string.Empty, value);
                }
            }
        }

        if (e.Value != null)
            w.WriteValue(e.Value);

        foreach (var child in e.Children)
            WriteTreeInternal(child, w, false);

        if (!innerOnly)
            w.WriteEndElement();
    }

    public static Element Element(string name, string xmlns = default, string text = default)
        => new(name, xmlns, text);

    public static Element C(this Element parent, string name, string xmlns = default, string text = default)
    {
        var child = new @Element(name, xmlns, text);
        parent.AddChild(child);
        return child;
    }

    public static Element C(this Element parent, Element child)
    {
        parent.AddChild(child);
        return parent;
    }

    public static Element Up(this Element child)
        => child.Parent;

    public static Element Root(this Element child)
    {
        while (!child.IsRootElement)
            child = child.Parent;

        return child;
    }

    public static TValue GetAttributeValue<TValue>(this Element e, string name, TValue defaultValue = default, IFormatProvider provider = default)
        where TValue : IParsable<TValue>
    {
        var attrVal = e.GetAttribute(name);

        if (attrVal != null)
            if (TValue.TryParse(attrVal, provider ?? CultureInfo.InvariantCulture, out var result))
                return result;

        return defaultValue;
    }

    public static void SetAttributeValue<TValue>(this Element e, string name, TValue value, string format = default, IFormatProvider provider = default)
        where TValue : IFormattable
    {
        if (value is decimal or double or float)
            format ??= "F6";

        e.SetAttribute(name, value?.ToString(format, provider ?? CultureInfo.InvariantCulture) ?? string.Empty);
    }
}

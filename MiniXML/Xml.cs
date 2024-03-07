using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
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

    public static bool IsStanza(Element e)
        => e.Name is "stream:stream" or "iq" or "message" or "presence";
    /// <summary>
    /// Inline parses the provided XML from stream and returns the element.
    /// </summary>
    /// <param name="stream">Stream that will be used for reading.</param>
    /// <param name="bufferSize">Internal read buffer size.</param>
    /// <param name="encoding">Determines the character encoding. Defaults to: <see cref="Encoding.UTF8"/></param>
    /// <returns>Instance of the well-formed element that was read.</returns>
    public static Element Parse(Stream stream, int bufferSize = ushort.MaxValue, Encoding encoding = default)
    {
        Unsafe.SkipInit(out Element result);

        using (var parser = new Parser(stream, bufferSize, encoding))
        {
            parser.OnStreamElement += e =>
            {
                result = e;
            };

            while (!parser.IsEndOfStream)
                parser.Update();
        }

        return result;
    }

    /// <summary>
    /// Inline parses the provided XML from file and returns the element.
    /// </summary>
    /// <param name="fileName">File name that will be opened and used for reading.</param>
    /// <param name="bufferSize">Internal read buffer size.</param>
    /// <param name="encoding">Determines the character encoding. Defaults to: <see cref="Encoding.UTF8"/></param>
    /// <returns>Instance of the well-formed element that was read.</returns>
    public static Element Parse(string fileName, int bufferSize = ushort.MaxValue, Encoding encoding = default)
    {
        using var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Parse(fs, bufferSize, encoding);
    }

    #region Xml serialization

    internal static string IndentChar = " ";

    internal static string WriteTree(Element e, bool indent, bool innerOnly)
    {
        var sb = new StringBuilder();

        using (var sw = new StringWriter(sb))
        {

            var settings = new XmlWriterSettings
            {
                Indent = indent,
                IndentChars = IndentChar,
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

    static bool IsXmlOrXmlnsDeclaration(KeyValuePair<string, string> entry)
    {
        return entry.Key == "xmlns"
                || entry.Key.StartsWith("xmlns:")
                || entry.Key.StartsWith("xml:");
    }

    static void WriteElementNamespaces(Element e, XmlWriter w)
    {
        var entries = e.Attributes.Where(IsXmlOrXmlnsDeclaration);
        WriteAttributesKvp(e, entries, w);
    }

    static void WriteElementAttributes(Element e, XmlWriter w)
    {
        var entries = e.Attributes.Where(x => !IsXmlOrXmlnsDeclaration(x));
        WriteAttributesKvp(e, entries, w);
    }

    static void WriteAttributesKvp(Element e, in IEnumerable<KeyValuePair<string, string>> dict, XmlWriter w)
    {
        foreach (var (name, value) in dict)
        {
            var hasPrefix = name.DeconstructXmlName(out var localName, out var prefix);

            if (!hasPrefix)
                w.WriteAttributeString(name, value);
            else
            {
                if (prefix == "xml")
                    w.WriteAttributeString(localName, Namespace.Xml, value);
                else if (prefix == "xmlns")
                    w.WriteAttributeString(localName, Namespace.Xmlns, value);
                else
                    w.WriteAttributeString(localName, e.GetNamespace(prefix) ?? string.Empty);
            }
        }
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

            WriteElementNamespaces(e, w);
            WriteElementAttributes(e, w);
        }

        if (e.Value != null)
            w.WriteValue(e.Value);

        foreach (var child in e.Children)
            WriteTreeInternal(child, w, false);

        if (!innerOnly)
            w.WriteEndElement();
    }

    #endregion

    /// <summary>
    /// Helper function that creates a new element.
    /// </summary>
    /// <param name="name">Qualified name of the element.</param>
    /// <param name="xmlns">Element namespace.</param>
    /// <param name="text">Text content of the element.</param>
    /// <returns>Instance of the created element.</returns>
    public static Element Element(string name, string xmlns = default, string text = default)
        => new(name, xmlns, text);

    /// <summary>
    /// Creates a child element.
    /// </summary>
    /// <param name="parent">Instance of the parent element.</param>
    /// <param name="name">Qualified name of the element.</param>
    /// <param name="xmlns">Element namespace.</param>
    /// <param name="text">Text content of the element.</param>
    /// <returns>Instance of the created child element.</returns>
    public static Element C(this Element parent, string name, string xmlns = default, string text = default)
    {
        var child = new @Element(name, xmlns, text);
        parent.AddChild(child);
        return child;
    }

    /// <summary>
    /// Add a child element instance to parent.
    /// </summary>
    /// <param name="parent">Instance of the parent element.</param>
    /// <param name="child">Instance of the child element</param>
    /// <returns>Parent element instance.</returns>
    public static Element C(this Element parent, Element child)
    {
        parent.AddChild(child);
        return parent;
    }

    public static Element Attr(this Element element, string name, string value)
    {
        element.SetAttribute(name, value);
        return element;
    }

    public static Element Attr<T>(this Element e, string name, (T value, string format) tuple)
        where T : IFormattable
    {
        return e.Attr(name, tuple: (tuple.value, tuple.format, CultureInfo.InvariantCulture));
    }

    public static Element Attr<T>(this Element e, string name, (T value, string format, IFormatProvider provider) tuple)
        where T : IFormattable
    {
        var (value, format, provider) = tuple;
        ArgumentNullException.ThrowIfNull(provider);
        e.SetAttribute(name, value.ToString(format, provider));
        return e;
    }

    public static Element Attr<T>(this Element element, string name, T value) where T : struct
    {
        element.SetAttributeValue(name, value);
        return element;
    }

    public static Element Attrs(this Element e, object attrs, IFormatProvider provider = default)
    {
        ArgumentNullException.ThrowIfNull(attrs);

        provider ??= CultureInfo.InvariantCulture;

        foreach (var prop in attrs.GetType().GetTypeInfo().DeclaredProperties)
        {
            var attName = prop.Name;
            string attVal;

            var rawValue = prop.GetValue(attrs);

            if (rawValue == null)
                attVal = string.Empty;
            else
                attVal = ToStringValue(rawValue, null, provider);

            e.SetAttribute(attName, attVal);
        }

        return e;
    }

    static string ToStringValue(object value, string format, IFormatProvider provider)
    {
        provider ??= CultureInfo.InvariantCulture;

        string result;

        if (value is IFormattable fmt)
            result = fmt.ToString(format, provider);
        else if (value is IConvertible conv)
            result = conv.ToString(provider);
        else if (value is ITuple tuple)
        {
            if (tuple.Length >= 2)
            {
                if (tuple.Length > 3)
                    throw new InvalidOperationException("Tuple values expect 2 components (value, format) or 3 components (value, format, format provider)");

                IFormatProvider thisProvider = default;

                var thisValue = tuple[0];
                var thisFormat = tuple[1] as string;

                if (tuple.Length > 3)
                    thisProvider = (tuple[2] as IFormatProvider) ?? provider;

                result = ToStringValue(thisValue, thisFormat, thisProvider);
            }
            else
            {
                // tuple accept only 2 or 3 params (value, format) or (value, format, provider)
                // otherwise serialize as string.
                result = tuple.ToString();
            }
        }
        else
            result = value?.ToString() ?? string.Empty;

        return result;
    }

    /// <summary>
    /// Moves up the element hierarchy to the parent element of the current element.
    /// </summary>
    /// <param name="child">Instance of the child element</param>
    /// <returns>Parent element instance.</returns>
    public static Element Up(this Element child)
        => child.Parent;

    /// <summary>
    /// Gets the root element from the current element
    /// </summary>
    /// <param name="child">Instance of the child element</param>
    /// <returns>Root element instance.</returns>
    public static Element Root(this Element child)
    {
        while (!child.IsRootElement)
            child = child.Parent;

        return child;
    }

    /// <summary>
    /// Gets the value of the attribute that will be converted to its appropriate type.
    /// </summary>
    /// <typeparam name="TValue">Type that will be parsed.</typeparam>
    /// <param name="e">Element instance.</param>
    /// <param name="name">Attribute name.</param>
    /// <param name="defaultValue">Fallback attribute value.</param>
    /// <param name="provider"><i>Optional</i> format provider for custom formatting.</param>
    /// <returns>The converted attribute value, or the <paramref name="defaultValue"/> value if a problem occurred or the attribute does not exist.</returns>
    public static TValue GetAttributeValue<TValue>(this Element e, string name, TValue defaultValue = default, IFormatProvider provider = default)
    {
        var attrVal = e.GetAttribute(name);

        if (attrVal != null)
            return Utilities.TryParseString(attrVal, provider, defaultValue);

        return defaultValue;
    }

    /// <summary>
    /// Inline gets the value of the attribute that will be converted to its appropriate type.
    /// </summary>
    /// <typeparam name="TValue">Type that will be parsed.</typeparam>
    /// <param name="e">Element instance.</param>
    /// <param name="name">Attribute name.</param>
    /// <param name="result">Output to the converted attribute value</param>
    /// <param name="defaultValue">Fallback attribute value.</param>
    /// <param name="provider"><i>Optional</i> format provider for custom formatting.</param>
    /// <returns>Element instance for fluent builder.</returns>
    public static Element GetAttributeValue<TValue>(this Element e, string name, out TValue result, TValue defaultValue = default, IFormatProvider provider = default)
    {
        var attrVal = e.GetAttribute(name);

        result = defaultValue;

        if (attrVal != null)
            result = Utilities.TryParseString(attrVal, provider, defaultValue);

        return e;
    }

    /// <summary>
    /// Sets the attribute on the element.
    /// </summary>
    /// <typeparam name="TValue">Type that will be converted.</typeparam>
    /// <param name="e">Element instance.</param>
    /// <param name="name">Attribute name.</param>
    /// <param name="value">Attribute value in current type.</param>
    /// <param name="format"><i>Optional</i> format during conversion to string.</param>
    /// <param name="provider"><i>Optional</i> format provider for custom formatting.</param>
    public static Element SetAttributeValue<TValue>(this Element e, string name, TValue value, string format = default, IFormatProvider provider = default)
    {
        if (value is decimal or double or float)
            format ??= "F6";

        if (value is IFormattable fmt)
            e.SetAttribute(name, fmt?.ToString(format, provider ?? CultureInfo.InvariantCulture) ?? string.Empty);
        else if (value is IConvertible conv)
            e.SetAttribute(name, conv.ToString(provider));
        else
        {
            if (value is null)
                e.RemoveAttribute(name);
            else
                e.SetAttribute(name, value?.ToString() ?? string.Empty);
        }

        return e;
    }
}

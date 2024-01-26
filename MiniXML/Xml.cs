using System.Globalization;
using System.Xml;

namespace MiniXML;

/// <summary>
/// Utilities for manipulating XML elements fluently.
/// </summary>
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

    internal static string SerializeToXml(object rawValue)
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

    /// <summary>
    /// Declares a dictionary of attributes on the given element.
    /// </summary>
    /// <param name="element">Element that will receive the attributes.</param>
    /// <param name="attrs">Dictionary of attributes that will be assigned to the element.</param>
    /// <returns>Returns the element instance itself for nesting other methods.</returns>
    public static Element Attrs(this Element element, in Dictionary<string, object>? attrs)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (attrs == null)
            goto next;

        foreach (var (key, value) in attrs)
            element.SetAttribute(key, SerializeToXml(value));

        next:
        return element;
    }

    /// <summary>
    /// Declare an attribute on the specified element.
    /// </summary>
    /// <param name="element">Element that will receive the attribute.</param>
    /// <param name="name">Attribute name.</param>
    /// <param name="value">Value assigned to the attribute.</param>
    /// <returns>Returns the element instance itself for nesting other methods.</returns>
    public static Element Attr(this Element element, string name, object value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetAttribute(name, SerializeToXml(value));
        return element;
    }

    /// <summary>
    /// Creates a new instance of an element.
    /// </summary>
    /// <param name="name">Element qualified tag name.</param>
    /// <param name="attrs">Dictionary of attributes that will be assigned to the element.</param>
    /// <returns>Returns the element instance created for nesting other methods.</returns>
    public static Element Element(string name, in Dictionary<string, object>? attrs = default)
    {
        var child = new Element(name);
        Attrs(child, attrs);
        return child;
    }

    /// <summary>
    /// Declares a child element in the given element.
    /// </summary>
    /// <param name="element">Parent element that will receive the child element.</param>
    /// <param name="name">Element qualified tag name.</param>
    /// <param name="xmlns"><i>[optional]</i> XML namespace that will be assigned</param>
    /// <param name="attrs"><i>[optional]</i> Dictionary of attributes that will be assigned to the element.</param>
    /// <returns>Returns the element instance for nesting other methods.</returns>
    public static Element Child(this Element element, string name, string xmlns, in Dictionary<string, object>? attrs = default)
    {
        var child = new Element(name, xmlns);
        element?.AddChild(child.Attrs(attrs));
        return child;
    }

    /// <summary>
    /// Declares a child element in the given element.
    /// </summary>
    /// <param name="element">Parent element that will receive the child element.</param>
    /// <param name="name">Element qualified tag name.</param>
    /// <param name="attrs"><i>[optional]</i> Dictionary of attributes that will be assigned to the element.</param>
    /// <returns>Returns the element instance for nesting other methods.</returns>
    public static Element Child(this Element element, string name, in Dictionary<string, object>? attrs = default)
    {
        var child = new Element(name);
        element?.AddChild(child);
        return child;
    }

    /// <summary>
    /// Returns the root element of the current element tree.
    /// </summary>
    /// <param name="element">Child element that will be checked.</param>
    /// <returns>Returns the root element or itself if it is already the root element</returns>
    public static Element Root(this Element element)
    {
        ArgumentNullException.ThrowIfNull(element);

        while (!element.IsRootElement)
            element = element.Parent!;

        return element;
    }

    /// <summary>
    /// Returns the parent element.
    /// </summary>
    /// <param name="element">Child element that will be checked.</param>
    /// <returns>Returns the parent element it belongs to.</returns>
    public static Element Up(this Element element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (element.IsRootElement)
            return element;

        return element.Parent!;
    }

    /// <summary>
    /// Sets the text content of the current element.
    /// </summary>
    /// <param name="element">Current element that will have its text changed.</param>
    /// <param name="text"><i>[optional]</i> Text content of the element.</param>
    /// <returns>Returns the element instance for nesting other methods.</returns>
    public static Element Text(this Element element, string? text = default)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.Value = text;
        return element;
    }
}

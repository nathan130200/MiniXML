using System.Buffers;
using System.Diagnostics;
using System.Security;
using System.Text;
using System.Web;
using System.Xml;

namespace MiniXML;

/// <summary>
/// Class to represent an XML element and its relationships.
/// </summary>
[DebuggerDisplay("{StartTag,nq}")]
public class Element
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Element? _parent;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly List<Element> _children;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly Dictionary<string, string> _attributes;

    /// <summary>
    /// Instance of the synchronization object to guarantee the well-being of this object.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly object _syncLock = new();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string _name = default!;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal string? _value;

    /// <summary>
    /// Internal constructor, to initialize the main data.
    /// </summary>
    internal Element()
    {
        _children = [];
        _attributes = [];
    }

    /// <summary>
    /// Copy constructor. Creates a copy of another element.
    /// </summary>
    /// <param name="other">Element that will be copied.</param>
    public Element(Element other) : this()
    {
        ArgumentNullException.ThrowIfNull(other);

        // As we are in ctor, we don't need to lock now,
        // as there is no possibility of making changes here.
        // We just lock the other element, thus guaranteeing
        // an exact copy of it and avoiding making unnecessary
        // copies of children and attributes. We just iterate over it!

        Name = other.Name;

        lock (other._syncLock)
        {
            foreach (var (key, value) in other._attributes)
                _attributes[key] = value;

            foreach (var child in other._children)
            {
                var newElement = new Element(child);
                _children.Add(newElement);
                newElement._parent = this;
            }
        }
    }

    /// <summary>
    /// Creates a new element.
    /// </summary>
    /// <param name="name">Qualified name of the element.</param>
    /// <param name="xmlns">Element namespace.</param>
    /// <param name="text">Element content text.</param>
    public Element(string name, string? xmlns = default, string? text = default) : this()
    {
        Name = Xml.NormalizeQualifiedName(name);

        if (!string.IsNullOrEmpty(xmlns))
            SetNamespace(xmlns);

        Value = text;
    }

    /// <summary>
    /// Local name of the element.
    /// </summary>
    public string LocalName
    {
        get
        {
            var ofs = Name.IndexOf(':');

            if (ofs == -1)
                return Name;

            return Name[(ofs + 1)..];
        }
    }

    /// <summary>
    /// Prefix of the element.
    /// </summary>
    public string? Prefix
    {
        get
        {
            var ofs = Name.IndexOf(':');

            if (ofs == -1)
                return null;

            return Name[0..ofs];
        }
    }

    /// <summary>
    /// Gets the element that owns the current element.
    /// </summary>
    public Element? Parent => _parent;

    /// <summary>
    /// Determines whether the current element is the root element or belongs to another element.
    /// </summary>
    public bool IsRootElement => _parent is null;

    /// <summary>
    /// Qualified name of the element.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _name = Xml.NormalizeQualifiedName(value);
        }
    }

    /// <summary>
    /// Element content text.
    /// </summary>
    public string? Value
    {
        get => HttpUtility.HtmlDecode(_value);
        set => _value = SecurityElement.Escape(value ?? string.Empty);
    }

    /// <summary>
    /// Gets the XML of the current element's starting tag.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string StartTag
    {
        get
        {
            var sb = new StringBuilder().AppendFormat("<{0}", Name);

            foreach (var (key, value) in Attributes)
                sb.AppendFormat(" {0}=\"{1}\"", key, SecurityElement.Escape(value));

            return sb.Append('>').ToString();
        }
    }

    /// <summary>
    /// Gets the XML of the current element's ending tag.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string EndTag
        => $"</{Name}>";

    /// <summary>
    /// Declares an attribute on the element.
    /// </summary>
    /// <param name="name">Attribute name.</param>
    /// <param name="value">Attribute value.</param>
    /// <exception cref="ArgumentNullException">If the attribute name or value is null.</exception>
    public void SetAttribute(string name, string value)
    {
        name = Xml.NormalizeQualifiedName(name);

        ArgumentException.ThrowIfNullOrEmpty(value);

        lock (_syncLock)
            _attributes[name] = SecurityElement.Escape(value);
    }

    /// <summary>
    /// Gets the attribute declared on this element.
    /// </summary>
    /// <param name="name">Attribute name.</param>
    /// <returns>Returns the value of the attribute or null if the attribute is not declared in this element.</returns>
    /// <exception cref="ArgumentNullException">If the attribute name is null.</exception>
    public string? GetAttribute(string name)
    {
        name = Xml.NormalizeQualifiedName(name);

        lock (_syncLock)
        {
            if (_attributes.TryGetValue(name, out var result))
                return result;

            return null;
        }
    }

    /// <summary>
    /// Update or remove an attribute on the element. If the attribute value is null, it will be removed.
    /// </summary>
    /// <param name="name">Attribute name.</param>
    /// <param name="newValue">Attribute value.</param>
    /// <returns>Returns the value of the attribute that was removed if it exists.</returns>
    /// <exception cref="ArgumentNullException">If the attribute name is null.</exception>
    public string? UpdateOrRemoveAttribute(string name, string? newValue = default)
    {
        name = Xml.NormalizeQualifiedName(name);

        lock (_syncLock)
        {
            string? oldValue = default;

            _attributes.Remove(name, out oldValue);

            if (newValue is not null)
                _attributes[name] = newValue;

            return oldValue;
        }
    }

    /// <summary>
    /// Removes the attribute from this element.
    /// </summary>
    /// <param name="name">Attribute name.</param>
    /// <returns>Returns the value of the attribute that was removed if it exists.</returns>
    /// <exception cref="ArgumentNullException">If the attribute name is null.</exception>
    public string? RemoveAttribute(string name)
    {
        string? result = default;

        name = Xml.NormalizeQualifiedName(name);

        lock (_syncLock)
            _attributes.Remove(name, out result);

        return result;
    }

    /// <summary>
    /// Declares a namespace with no prefix on this element.
    /// </summary>
    /// <param name="uri">URI of the namespace that will be added.</param>
    public void SetNamespace(string uri)
        => UpdateOrRemoveAttribute("xmlns", uri);

    /// <summary>
    /// Declares a namespace with prefix on this element.
    /// </summary>
    /// <param name="prefix">Namespace prefix.</param>
    /// <param name="uri">URI of the namespace that will be added.</param>
    public void SetNamespace(string prefix, string uri)
        => UpdateOrRemoveAttribute($"xmlns:{prefix}", uri);

    /// <summary>
    /// Searches for the first occurrence of the child element with a provided filter and optionally recursively.
    /// </summary>
    /// <param name="predicate">Filter to search for the element.</param>
    /// <param name="recursive">Indicates whether the search will be done recursively or not.</param>
    /// <returns>The element instance found, or null if no element matches the criteria.</returns>
    public Element? FindElement(Func<Element, bool> predicate, bool recursive = false)
    {
        Element? result = default;

        foreach (var child in Children)
        {
            if (predicate(child))
            {
                result = child;
                break;
            }

            if (result == null && recursive)
            {
                result = child.FindElement(predicate, recursive);

                if (result != null)
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Determines the current namespace (without prefix) of the element.
    /// </summary>
    public string? Namespace
    {
        get => GetNamespace();
        set => UpdateOrRemoveAttribute("xmlns", value);
    }

    /// <summary>
    /// Searches for the all occurrence of the child elements with a provided filter and optionally recursively.
    /// </summary>
    /// <param name="predicate">Filter to search for the element.</param>
    /// <param name="recursive">Indicates whether the search will be done recursively or not.</param>
    /// <returns>All elements that match the criteria or empty list if no elements were found.</returns>
    public IEnumerable<Element> FindElements(Func<Element, bool> predicate, bool recursive = false)
    {
        var result = new List<Element>();
        FindElementsInternal(this, result, predicate, recursive);
        return result;
    }

    static void FindElementsInternal(Element root, List<Element> result, Func<Element, bool> predicate, bool recursive)
    {
        foreach (var child in root.Children)
        {
            if (predicate(child))
                result.Add(child);

            if (recursive)
                FindElementsInternal(child, result, predicate, recursive);
        }
    }

    /// <summary>
    /// Gets the list of child elements.
    /// </summary>
    public IReadOnlyList<Element> Children
    {
        get
        {
            Element[] result;

            lock (_syncLock)
                result = [.. _children];

            return result.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets a dictionary with all declared attributes.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes
    {
        get
        {
            IEnumerable<KeyValuePair<string, string>> result;

            lock (_syncLock)
                result = _attributes.ToArray();

            return result.ToDictionary(x => x.Key, x => x.Value);
        }
    }

    /// <summary>
    /// Adds an element to its children.
    /// </summary>
    /// <param name="e">Element that will be added.</param>
    public void AddChild(Element e)
    {
        if (e._parent == this)
            return;

        e._parent?.RemoveChild(e);

        lock (_syncLock)
            _children.Add(e);

        e._parent = this;
    }

    /// <summary>
    /// Removes an element from its children.
    /// </summary>
    /// <param name="e">Element that will be removed.</param>
    public void RemoveChild(Element e)
    {
        if (e._parent != this)
            return;

        lock (_syncLock)
            _children.Remove(e);

        e._parent = null;
    }

    /// <summary>
    /// Removes itself from its parent element.
    /// </summary>
    public void Remove()
    {
        _parent?.RemoveChild(this);
        _parent = default;
    }

    /// <summary>
    /// Gets the namespace declared in the element (optionally also searches for the parent element)
    /// </summary>
    /// <param name="prefix"><i>[optional]</i> Namespace prefix.</param>
    /// <param name="searchInParent">Indicates whether the search will expand across the parent element.</param>
    /// <returns>String containing the namespace value or null if not found.</returns>
    public string? GetNamespace(string? prefix = default, bool searchInParent = true)
    {
        string? value;

        if (string.IsNullOrWhiteSpace(prefix))
        {
            value = GetAttribute("xmlns");

            if (value != null)
                return value;

            if (!searchInParent)
                return null;

            return _parent?.GetNamespace(prefix);
        }

        value = GetAttribute($"xmlns:{prefix}");

        if (!string.IsNullOrWhiteSpace(value))
            return value;

        if (!searchInParent)
            return null;

        return _parent?.GetNamespace(prefix);
    }

    /// <summary>
    /// Prints all elements, attributes, content and their respective children in XML format.
    /// </summary>
    public override string ToString()
        => ToString(false);

    /// <summary>
    /// Standard character representing quotation marks.
    /// </summary>
    public static char DefaultQuoteChar { set; private get; } = '\'';

    /// <summary>
    /// Standard character representing indentation.
    /// </summary>
    public static char DefaultIndentChar { set; private get; } = ' ';

    /// <summary>
    /// Default size of the indentation for each XML scope.
    /// </summary>
    public static byte DefaultIndentSize { set; private get; }

    /// <summary>
    /// Prints all elements, attributes, content and their respective children in XML format.
    /// </summary>
    /// <param name="indent">Determines whether the string containing the XML will be formatted and indented. (eg: for logging purposes)</param>
    public string ToString(bool indent)
    {
        var sb = new StringBuilder();

        using (var xw = new XmlTextWriter(new StringWriter(sb)))
        {
            xw.Formatting = indent ? Formatting.Indented : Formatting.None;
            xw.Indentation = DefaultIndentSize;
            xw.IndentChar = DefaultIndentChar;
            xw.QuoteChar = DefaultQuoteChar;

            lock (_syncLock)
                WriteXmlTree(this, xw);
        }

        return sb.ToString();
    }

    static void WriteXmlTree(Element element, XmlWriter writer)
    {
        writer.WriteStartElement(element.Name);

        foreach (var (name, value) in element.Attributes)
            writer.WriteAttributeString(name, value);

        if (!string.IsNullOrWhiteSpace(element.Value))
            writer.WriteValue(element.Value);

        foreach (var child in element.Children)
            WriteXmlTree(child, writer);

        writer.WriteEndElement();
    }
}
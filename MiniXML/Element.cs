using System.Buffers;
using System.Diagnostics;
using System.Security;
using System.Text;
using System.Web;
using System.Xml;

namespace MiniXML;

[DebuggerDisplay("{StartTag,nq}")]
public class Element
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Element? _parent;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly List<Element> _children;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly Dictionary<string, string> _attributes;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly object _syncLock = new();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string _name = default!;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal string? _value;

    internal Element()
    {
        _children = [];
        _attributes = [];
    }

    public Element(Element other) : this()
    {
        Name = other.Name;

        foreach (var (key, value) in other.Attributes)
            _attributes[key] = value;

        foreach (var child in other.Children)
        {
            var newElement = new Element(child);
            _children.Add(newElement);
            newElement._parent = this;
        }
    }

    public Element(string name, string? xmlns = default, string? text = default) : this()
    {
        Name = Xml.NormalizeQualifiedName(name);

        if (!string.IsNullOrEmpty(xmlns))
            SetNamespace(xmlns);

        Value = text;
    }

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

    public Element? Parent => _parent;
    public bool IsRootElement => _parent is null;

    public string Name
    {
        get => _name;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _name = Xml.NormalizeQualifiedName(value);
        }
    }

    public string? Value
    {
        get => HttpUtility.HtmlDecode(_value);
        set => _value = SecurityElement.Escape(value ?? string.Empty);
    }

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

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string EndTag
        => $"</{Name}>";

    public void SetAttribute(string name, string value)
    {
        name = Xml.NormalizeQualifiedName(name);

        lock (_syncLock)
            _attributes[name] = SecurityElement.Escape(value);
    }

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

    public string? UpdateOrRemoveAttribute(string name, string? newValue = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (newValue is null)
            return RemoveAttribute(name);
        else
        {
            name = Xml.NormalizeQualifiedName(name);

            lock (_syncLock)
            {
                _attributes.Remove(name, out var oldValue);
                _attributes[name] = newValue;
                return oldValue;
            }
        }
    }

    public string? RemoveAttribute(string name)
    {
        string? result = default;

        name = Xml.NormalizeQualifiedName(name);

        lock (_syncLock)
            _attributes.Remove(name, out result);

        return result;
    }

    public void SetNamespace(string uri)
        => SetAttribute("xmlns", uri);

    public void SetNamespace(string prefix, string uri)
        => SetAttribute($"xmlns:{prefix}", uri);

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

    public string? Namespace
    {
        get => GetNamespace();
        set => UpdateOrRemoveAttribute("xmlns", value);
    }

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

    public void AddChild(Element e)
    {
        if (e._parent == this)
            return;

        e._parent?.RemoveChild(e);

        lock (_syncLock)
            _children.Add(e);

        e._parent = this;
    }

    public void RemoveChild(Element e)
    {
        if (e._parent != this)
            return;

        lock (_syncLock)
            _children.Remove(e);

        e._parent = null;
    }

    public void Remove()
    {
        _parent?.RemoveChild(this);
        _parent = default;
    }

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

    public override string ToString()
        => ToString(false);

    internal static readonly char DefaultQuoteChar = '\'';
    internal static readonly char DefaultIndentChar = ' ';
    internal static readonly byte DefaultIndentSize = 1;

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
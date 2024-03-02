using System.Diagnostics;
using System.Security;
using System.Text;
using System.Web;

namespace MiniXML;

[DebuggerDisplay("{StartTag,nq}")]
public class Element
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Element _parent;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly List<Element> _children;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly Dictionary<string, string> _attributes;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string _localName, _prefix;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal string _value;

    internal Element()
    {
        _children = [];
        _attributes = [];
    }

    public Element(Element other) : this()
    {
        ArgumentNullException.ThrowIfNull(other);

        Name = other.Name;

        foreach (var (key, value) in other.Attributes)
            _attributes[key] = value;

        foreach (var child in other.Children)
        {
            var copy = new Element(child);
            _children.Add(copy);
            copy._parent = this;
        }
    }

    public Element(string name, string xmlns = default, string text = default) : this()
    {
        Name = Xml.NormalizeXmlName(name);

        if (!string.IsNullOrEmpty(xmlns))
        {
            if (Prefix != null)
                SetNamespace(Prefix, xmlns);
            else
                SetNamespace(xmlns);
        }

        Value = text;
    }

    public Element Parent
        => _parent;

    public bool IsRootElement
        => _parent is null;

    public string Prefix
    {
        get => _prefix;
        set => _prefix = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public string LocalName
    {
        get => _localName;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _localName = value;
        }
    }

    public string Name
    {
        get
        {
            if (_prefix != null)
                return $"{_prefix}:{_localName}";

            return _localName;
        }
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _ = value.DeconstructXmlName(out _localName, out _prefix);
        }
    }

    public string Value
    {
        get => _value;
        set => _value = HttpUtility.HtmlEncode(value);
    }

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
        name = Xml.NormalizeXmlName(name);

        lock (_attributes)
        {
            if (value != null)
                _attributes[name] = SecurityElement.Escape(value);
            else
                _attributes.Remove(name);
        }
    }

    public string GetAttribute(string name)
    {
        name = Xml.NormalizeXmlName(name);

        lock (_attributes)
        {
            if (_attributes.TryGetValue(name, out var result))
                return result;

            return null;
        }
    }

    public void RemoveAttribute(string name)
    {
        name = Xml.NormalizeXmlName(name);

        lock (_attributes)
            _attributes.Remove(name);
    }

    public void SetNamespace(string uri)
        => SetAttribute("xmlns", uri);

    public void SetNamespace(string prefix, string uri)
        => SetAttribute($"xmlns:{prefix}", uri);

    public Element GetChild(Func<Element, bool> predicate, bool recursive = false)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        Element result = default;

        foreach (var child in Children)
        {
            if (predicate(child))
            {
                result = child;
                break;
            }

            if (recursive)
            {
                result = child.GetChild(predicate, recursive);

                if (result != null)
                    break;
            }
        }

        return result;
    }

    public string DefaultNamespace
    {
        get => GetNamespace();
        set => SetNamespace(value);
    }

    public IReadOnlyList<Element> GetChildren(Func<Element, bool> predicate, bool recursive = false)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var result = new List<Element>();
        GetChildrenInternal(this, result, predicate, recursive);
        return result;
    }

    static void GetChildrenInternal(Element root, List<Element> result, Func<Element, bool> predicate, bool recursive)
    {
        foreach (var child in root.Children)
        {
            if (predicate(child))
                result.Add(child);

            if (recursive)
                GetChildrenInternal(child, result, predicate, recursive);
        }
    }

    public IReadOnlyList<Element> Children
    {
        get
        {
            Element[] result;

            lock (_children)
                result = [.. _children];

            return result;
        }
    }

    public IReadOnlyDictionary<string, string> Attributes
    {
        get
        {
            KeyValuePair<string, string>[] result;

            lock (_attributes)
                result = [.. _attributes];

            return result.ToDictionary(x => x.Key, x => x.Value);
        }
    }

    public void AddChild(Element e)
    {
        e.Remove();

        lock (_children)
            _children.Add(e);

        e._parent = this;
    }

    public void RemoveChild(Element e)
    {
        if (e._parent != this)
            return;

        lock (_children)
            _children.Remove(e);

        e._parent = null;
    }

    public void Remove()
    {
        _parent?.RemoveChild(this);
        _parent = default;
    }

    public string GetNamespace(string? prefix = default)
    {
        string value;

        if (string.IsNullOrWhiteSpace(prefix))
        {
            value = GetAttribute("xmlns");

            if (value != null)
                return value;

            return _parent?.GetNamespace(prefix);
        }

        value = GetAttribute($"xmlns:{prefix}");

        if (!string.IsNullOrWhiteSpace(value))
            return value;

        return _parent?.GetNamespace(prefix);
    }

    public bool IsEmptyElement
        => !Children.Any();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string InnerXml
        => Xml.WriteTree(this, false, true);

    public override string ToString()
        => ToString(false);

    public string ToString(bool indent)
        => Xml.WriteTree(this, indent, false);
}
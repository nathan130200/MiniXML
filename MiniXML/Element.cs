using System.Diagnostics;
using System.Text;

namespace MiniXML;

/// <summary>
/// Base class responsible for managing an XML element.
/// </summary>
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

    /// <summary>
    /// Internal constructor.
    /// </summary>
    internal Element()
    {
        _children = [];
        _attributes = [];
    }

    // TODO: Unstable!
    //public Element(Element other) : this()
    //{
    //    ArgumentNullException.ThrowIfNull(other);

    //    Name = other.Name;

    //    foreach (var (key, value) in other.Attributes)
    //        _attributes[key] = value;

    //    foreach (var child in other.Children)
    //    {
    //        var copy = new Element(child);
    //        _children.Add(copy);
    //        copy._parent = this;
    //    }
    //}

    /// <summary>
    /// Creates an instance of Element.
    /// </summary>
    /// <param name="name">Qualified name.</param>
    /// <param name="xmlns">Namespace URI.</param>
    /// <param name="text">Text content.</param>
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

    /// <summary>
    /// Gets the parent element of this element.
    /// </summary>
    public Element Parent
        => _parent;

    /// <summary>
    /// Determines whether this element is the root element.
    /// </summary>
    public bool IsRootElement
        => _parent is null;

    /// <summary>
    /// Gets the prefix of the element's qualified name.
    /// </summary>
    public string Prefix
    {
        get => _prefix;
        set => _prefix = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Gets the local name of the element's qualified name.
    /// </summary>
    public string LocalName
    {
        get => _localName;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _localName = value;
        }
    }

    /// <summary>
    /// Gets the qualified name of the element.
    /// </summary>
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

    /// <summary>
    /// Text content of the element. 
    /// </summary>
    public string Value
    {
        get => _value;
        set => _value = value;
    }

    /// <summary>
    /// Gets the opening tag of this element.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string StartTag
    {
        get
        {
            var sb = new StringBuilder().AppendFormat("<{0}", Name);

            foreach (var (key, value) in Attributes)
                sb.AppendFormat(" {0}=\"{1}\"", key, value);

            return sb.Append('>').ToString();
        }
    }

    /// <summary>
    /// Gets the closing tag of this element.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string EndTag
        => $"</{Name}>";

    /// <summary>
    /// Set an attribute on this element.
    /// </summary>
    /// <param name="name">Attribute qualified name.</param>
    /// <param name="value">Attribute value. If the attribute value is <see langword="null" />, it will remove the attribute.</param>
    public void SetAttribute(string name, string value)
    {
        name = Xml.NormalizeXmlName(name);

        lock (_attributes)
        {
            if (value != null)
                _attributes[name] = value;
            else
                _attributes.Remove(name);
        }
    }

    /// <summary>
    /// Gets the attribute value in this element.
    /// </summary>
    /// <param name="name">Attribute qualified name.</param>
    /// <returns>Returns the value of the attribute, or <see langword="null" /> if the attribute is not found.</returns>
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

    /// <summary>
    /// Determines whether the element has the attribute.
    /// </summary>
    /// <param name="name">Attribute qualified name.</param>
    /// <returns><see langword="true" /> if the attribute exists, <see langword="false" /> otherwise.</returns>
    public bool HasAttribute(string name)
    {
        name = Xml.NormalizeXmlName(name);

        lock (_attributes)
        {
            return _attributes.ContainsKey(name);
        }
    }

    /// <summary>
    /// Gets the attribute value in this element.
    /// </summary>
    /// <param name="name">Attribute qualified name.</param>
    public void RemoveAttribute(string name)
    {
        name = Xml.NormalizeXmlName(name);

        lock (_attributes)
            _attributes.Remove(name);
    }

    /// <summary>
    /// Sets the default namespace.
    /// </summary>
    /// <param name="uri">Namespace URI.</param>
    public void SetNamespace(string uri)
        => SetAttribute("xmlns", uri);

    /// <summary>
    /// Set XML namespace with prefix.
    /// </summary>
    /// <param name="prefix">Namespace Prefix.</param>
    /// <param name="uri">Namespace URI.</param>
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

    /// <summary>
    /// Default element namespace.
    /// </summary>
    public string DefaultNamespace
    {
        get => GetNamespace();
        set => SetNamespace(value);
    }

    /// <summary>
    /// Searches the element for subelements with the specified criteria.
    /// </summary>
    /// <param name="predicate">Predicate to query the elements.</param>
    /// <param name="recursive">If true, it searches the elements and all their descendants.</param>
    /// <returns>A list of the elements found.</returns>
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

    /// <summary>
    /// List of child elements that belong to this element.
    /// </summary>
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

    /// <summary>
    /// Dictionary of the attributes of this element.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes
    {
        get
        {
            KeyValuePair<string, string>[] result;

            lock (_attributes)
                result = _attributes.ToArray();

            return result.ToDictionary(x => x.Key, x => x.Value);
        }
    }

    /// <summary>
    /// Adds a child element.
    /// </summary>
    /// <param name="e">Element that will be added.</param>
    public void AddChild(Element e)
    {
        e.Remove();

        lock (_children)
            _children.Add(e);

        e._parent = this;
    }

    /// <summary>
    /// Removes a child element.
    /// </summary>
    /// <param name="e">Element that will be removed.</param>
    public void RemoveChild(Element e)
    {
        if (e._parent != this)
            return;

        lock (_children)
            _children.Remove(e);

        e._parent = null;
    }

    /// <summary>
    /// Removes itself from the parent element.
    /// </summary>
    public void Remove()
    {
        _parent?.RemoveChild(this);
        _parent = default;
    }

    /// <summary>
    /// Gets the namespace of the element.
    /// </summary>
    /// <param name="prefix"><i>Optionally</i> the prefix of the namespace that will be queried</param>
    /// <returns>The namespace URI found, or <see langword="null" /> if not defined in any scope.</returns>
    /// <remarks>
    /// The namespace is inherited from parent to child, so the search will also be performed on the parent elements and their parents as well.
    /// </remarks>
    public string GetNamespace(string prefix = default)
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

    /// <summary>
    /// Determines whether this element is a childless element.
    /// </summary>
    public bool IsEmptyElement
        => !Children.Any();

    /// <summary>
    /// Gets the inner xml of the current element.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string InnerXml
        => Xml.WriteTree(this, false, true);

    /// <summary>
    /// Converts the element instance to XML representation (without formatting).
    /// </summary>
    public override string ToString()
        => ToString(false);

    /// <summary>
    /// Converts the element instance to XML representation
    /// </summary>
    /// <param name="indent">Determines whether the representation will be formatted nicely.</param>
    public string ToString(bool indent)
        => Xml.WriteTree(this, indent, false);
}
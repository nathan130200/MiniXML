using System.Diagnostics;
using System.Text;

namespace MiniXML;

/// <summary>
/// Represents a jabber identifier.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct Jid : IEquatable<Jid>
{
    private readonly string _local;
    private readonly string _domain = default!;
    private readonly string _resource;

    /// <summary>
    /// Determines whether this JID is valid or not (i.e. it has at least <see cref="Domain" />)
    /// </summary>
    public bool IsNil
        => string.IsNullOrWhiteSpace(_domain);

    /// <summary>
    /// Empty jid instance.
    /// </summary>
    public static Jid Empty => default;

    /// <summary>
    /// Initializes the JID instance and parses it.
    /// </summary>
    /// <param name="jid">String that will be parsed as JID.</param>
    public Jid(string jid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jid);

        if (jid.Contains('@') || jid.Contains('/'))
            this = Parse(jid);
        else
            Domain = jid;
    }

    /// <summary>
    /// Parses the string into JID.
    /// </summary>
    /// <param name="jid">String that will be parsed as JID.</param>
    /// <returns>Immutable instance of the JID.</returns>
    public static Jid Parse(string jid)
    {
        var ofs = jid.IndexOf('@');

        string local = null,
            resource = null;

        string domain;

        if (ofs != -1)
        {
            local = jid[0..ofs];
            jid = jid[(ofs + 1)..];
        }

        ofs = jid.IndexOf('/');

        if (ofs == -1)
            domain = jid;
        else
        {
            domain = jid[0..ofs];
            resource = jid[(ofs + 1)..];
        }

        return new Jid(local, domain, resource);
    }

    /// <summary>
    /// Directly initializes the JID instance.
    /// </summary>
    /// <param name="local"><i>Optional</i> local part of JID.</param>
    /// <param name="domain"><b>Required</b> domain part of JID.</param>
    /// <param name="resource"><i>Optional</i> resource part of JID.</param>
    public Jid(string local, string domain, string resource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        Local = local;
        Domain = domain;
        Resource = resource;
    }

    /// <summary>
    /// Gets the local part of the JID.
    /// </summary>
    public string Local
    {
        get => _local;
        init => _local = value?.ToLowerInvariant();
    }

    /// <summary>
    /// Gets the domain part of the JID.
    /// </summary>
    public string Domain
    {
        get => _domain;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _domain = value.ToLowerInvariant();
        }
    }

    /// <summary>
    /// Gets the resource part of the JID.
    /// </summary>
    public string Resource
    {
        get => _resource;
        init => _resource = value;
    }

    public override int GetHashCode()
    {
        if (IsNil)
            return -1;

        return HashCode.Combine(
            _local?.GetHashCode() ?? 0,
            _domain.GetHashCode(),
            _resource?.GetHashCode() ?? 0);
    }

    /// <summary>
    /// Gets the string representation of the JID instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">If the JID exceeds 3017 bytes, which is the maximum considered in the XMPP protocol.</exception>
    public override string ToString()
    {
        if (IsNil)
            return null;

        var sb = new StringBuilder();

        if (_local != null)
            sb.Append(_local).Append('@');

        sb.Append(_domain);

        if (_resource != null)
            sb.Append('/').Append(_resource);

        var result = sb.ToString();

        if (sb.Length > 3071)
            throw new InvalidOperationException("Jid byte size limit exceeded.");

        return result;
    }

    public override bool Equals(object obj)
        => obj is Jid other && Equals(other);

    public bool Equals(Jid other)
    {
        if (IsNil || other.IsNil)
            return false;

        if (IsBare)
            return IsBareEquals(this, other);

        return IsFullEquals(this, other);
    }

    /// <summary>
    /// Determines whether the JID is bare, that is, it does not contain the resource part.
    /// </summary>
    public bool IsBare
        => string.IsNullOrWhiteSpace(_resource);

    /// <summary>
    /// Creates an immutable JID instance that is bare (without the resource part).
    /// </summary>
    public Jid Bare => this with
    {
        Resource = null
    };

    static readonly StringComparer s_DefaultComparer = StringComparer.OrdinalIgnoreCase;
    static readonly StringComparer s_DefaultComparerCaseSensitive = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Compares whether JID instances are bare (does not have the resource part) and are equals.
    /// </summary>
    public static bool IsBareEquals(Jid lhs, Jid rhs)
    {
        if (lhs.IsNil || rhs.IsNil)
            return false;

        if (!lhs.IsBare || !rhs.IsBare)
            return false;

        return s_DefaultComparer.Equals(lhs.Local, rhs.Local)
            && s_DefaultComparer.Equals(lhs.Domain, rhs.Domain);
    }

    /// <summary>
    /// Compares whether JID instances are "full" (has the resource part) and are equals.
    /// </summary>
    public static bool IsFullEquals(Jid lhs, Jid rhs)
    {
        if (lhs.IsBare || rhs.IsBare)
            return false;

        return IsBareEquals(lhs, rhs)
            && s_DefaultComparerCaseSensitive.Equals(lhs.Resource, rhs.Resource);
    }

    public static bool operator ==(Jid lhs, Jid rhs)
        => IsFullEquals(lhs, rhs);

    public static bool operator !=(Jid lhs, Jid rhs)
        => !(lhs == rhs);
}
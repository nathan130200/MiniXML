using System.Diagnostics;
using System.Text;

namespace MiniXML;

[DebuggerDisplay("{ToString(),nq}")]
public readonly struct Jid : IEquatable<Jid>
{
    private readonly string _local;
    private readonly string _domain = default!;
    private readonly string _resource;

    public bool IsNil
        => string.IsNullOrWhiteSpace(_domain);

    public static Jid Empty => default;

    public Jid(string jid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jid);

        if (jid.Contains('@') || jid.Contains('/'))
            this = Parse(jid);
        else
            Domain = jid;
    }

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

    public Jid(string local, string domain, string resource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        Local = local;
        Domain = domain;
        Resource = resource;
    }

    public string Local
    {
        get => _local;
        init => _local = value?.ToLowerInvariant();
    }

    public string Domain
    {
        get => _domain;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _domain = value.ToLowerInvariant();
        }
    }

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

    public bool IsBare
        => string.IsNullOrWhiteSpace(_resource);

    public Jid Bare => this with
    {
        Resource = null
    };

    static readonly StringComparer s_DefaultComparer = StringComparer.OrdinalIgnoreCase;
    static readonly StringComparer s_DefaultComparerCaseSensitive = StringComparer.OrdinalIgnoreCase;

    public static bool IsBareEquals(Jid lhs, Jid rhs)
    {
        if (lhs.IsNil || rhs.IsNil)
            return false;

        return s_DefaultComparer.Equals(lhs.Local, rhs.Local)
            && s_DefaultComparer.Equals(lhs.Domain, rhs.Domain);
    }

    public static bool IsFullEquals(Jid lhs, Jid rhs)
    {
        return IsBareEquals(lhs, rhs)
            && s_DefaultComparerCaseSensitive.Equals(lhs.Resource, rhs.Resource);
    }

    public static bool operator ==(Jid lhs, Jid rhs)
        => IsFullEquals(lhs, rhs);

    public static bool operator !=(Jid lhs, Jid rhs)
        => !(lhs == rhs);
}
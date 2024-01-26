using System.Diagnostics;
using System.Text;

namespace MiniXML;

/// <summary>
/// Jabber identifier (also known as JID)
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct Jid : IEquatable<Jid>
{
    private readonly string? _local;
    private readonly string? _domain = default!;
    private readonly string? _resource;

    bool IsInvalid
        => string.IsNullOrWhiteSpace(_domain);

    /// <summary>
    /// Gets an invalid instance of the Jabber ID, which can be used to store it temporarily.
    /// </summary>
    public static Jid Empty => default;

    /// <summary>
    /// Constructor.
    /// <para>It produces an invalid JID, because according to the XMPP protocol specifications the <see cref="Domain"/> component is mandatory.</para>
    /// </summary>
    public Jid()
    {

    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="jid"></param>
    public Jid(string jid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jid);

        if (jid.Contains('@') || jid.Contains('/'))
            this = Parse(jid);
        else
            Domain = jid;
    }

    /// <summary>
    /// Parse a string converting it to jid with its respective components.
    /// </summary>
    /// <param name="jid">String that will be parsed</param>
    /// <returns>Valid JID instance.</returns>
    public static Jid Parse(string jid)
    {
        var ofs = jid.IndexOf('@');
        string? local = default;
        string? resource = default;
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
    /// Constructor
    /// </summary>
    /// <param name="local">Optional local part</param>
    /// <param name="domain">Mandatory domain part</param>
    /// <param name="resource">Optional resource part</param>
    public Jid(string? local, string domain, string? resource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        Local = local;
        Domain = domain;
        Resource = resource;
    }

    static string? EnsureComponentByteSizeLimit(string kind, string? value)
    {
        const int MaxSizeInBytes = 1023;

        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (Encoding.UTF8.GetByteCount(value) >= MaxSizeInBytes)
            throw new ArgumentOutOfRangeException($"Jid \"{kind}\" component cannot exceed {MaxSizeInBytes} bytes!");

        return value;
    }

    /// <summary>
    /// Local part uniquely identifies the entity requesting and using network access provided by a server (i.e., a local account).
    /// <para>Although it can also represent other kinds of entities (e.g., a chatroom associated with a multi-user chat service)</para>
    /// </summary>
    public string? Local
    {
        get => _local;
        init => _local = EnsureComponentByteSizeLimit("local", value);
    }

    /// <summary>
    /// Domain part identifies the "home" server to which clients connect for XML routing and data management functionality.
    /// <para>
    /// The domain part is the primary identifier and is the only REQUIRED element of a JID</para>
    /// </summary>
    public string? Domain
    {
        get => _domain;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _domain = EnsureComponentByteSizeLimit("domain", value);
        }
    }

    /// <summary>
    /// The resourcepart of a JID is an optional identifier.
    /// <para>Uniquely identifies a specific connection(e.g., a device or location) or object (e.g., an occupant in a multi-user chatroom belonging to the entity associated with an XMPP localpart at a domain.</para>
    /// </summary>
    public string? Resource
    {
        get => _resource;
        init => _resource = EnsureComponentByteSizeLimit("resource", value);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(
            _local?.GetHashCode() ?? 0,
            _domain?.GetHashCode() ?? 0,
            _resource?.GetHashCode() ?? 0);
    }

    /// <summary>
    /// Gets a string representation of the current JID.
    /// </summary>
    public override string ToString()
    {
        if (IsInvalid)
            throw new InvalidOperationException("The current jid is invalid and cannot be represented as a string.");

        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(_local))
            sb.Append(_local).Append('@');

        sb.Append(_domain);

        if (!string.IsNullOrWhiteSpace(_resource))
            sb.Append('/').Append(_resource);

        var result = sb.ToString();

        Debug.Assert(Encoding.ASCII.GetByteCount(result) <= 3071);

        return result;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is Jid other && Equals(other);

    /// <inheritdoc/>
    public bool Equals(Jid other)
    {
        if (IsInvalid || other.IsInvalid)
            return false;

        if (IsBare)
            return IsBareEquals(this, other);

        return IsFullEquals(this, other);
    }

    /// <summary>
    /// Determines if the JID does not have a resource.
    /// </summary>
    public bool IsBare
        => string.IsNullOrWhiteSpace(_resource);

    /// <summary>
    /// Gets an instance of the current Jabber ID, without the resource part.
    /// </summary>
    public Jid Bare => this with
    {
        Resource = default
    };

    static readonly StringComparison CompareMethod = StringComparison.InvariantCultureIgnoreCase;

    /// <summary>
    /// Compare whether both jids are "bare" and are the same.
    /// <para>
    /// It is qualified as a "bare" JID when:
    /// <list type="bullet">
    /// <item>Has a local part</item>
    /// <item>Has domain part</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="lhs">First JID to compare</param>
    /// <param name="rhs">Second JID to compare</param>
    /// <returns>True if they are equal, false otherwise.</returns>
    public static bool IsBareEquals(Jid lhs, Jid rhs)
    {
        if (lhs.IsInvalid || rhs.IsInvalid)
            return false;

        return string.Equals(lhs.Local, rhs.Local, CompareMethod)
            && string.Equals(lhs.Domain, rhs.Domain, CompareMethod);
    }

    /// <summary>
    /// Compare whether both jid are "full" and whether they are equal.
    /// <para>
    /// It is qualified as JID "full" when:
    /// <list type="bullet">
    /// <item>Has local part</item>
    /// <item>Has domain part</item>
    /// <item>Has resource part</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="lhs">First JID to compare</param>
    /// <param name="rhs">Second JID to compare</param>
    /// <returns>True if they are equal, false otherwise.</returns>
    public static bool IsFullEquals(Jid lhs, Jid rhs)
    {
        return IsBareEquals(lhs, rhs)
            && string.Equals(lhs.Resource, rhs.Resource, CompareMethod);
    }


    /// <inheritdoc/>
    public static bool operator ==(Jid left, Jid right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(Jid left, Jid right) => !(left == right);
}

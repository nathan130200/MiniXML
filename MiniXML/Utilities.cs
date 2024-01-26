using System.Runtime.CompilerServices;
using System.Text;

namespace MiniXML;

/// <summary>
/// Class for other common utilities.
/// </summary>
public static class Utilities
{
    /// <summary>
    /// Gets a <see cref="TaskAwaiter" /> instance to wait until the parser has finished reading.
    /// </summary>
    /// <param name="parser">Instance of the XMPP parser.</param>
    public static TaskAwaiter GetAwaiter(this Parser parser)
        => parser._completition?.Task?.GetAwaiter() ?? default;

    /// <summary>
    /// Converts the string to bytes using a specific encoding.
    /// </summary>
    /// <param name="str">String that will be converted.</param>
    /// <param name="enc">Encoding that will be used. If it is null, it will default to UTF-8 encoding.</param>
    /// <returns>Byte array of the converted string.</returns>
    public static byte[] GetBytes(this string str, Encoding? enc = default)
        => (enc ?? Encoding.UTF8).GetBytes(str);

    /// <summary>
    /// Converts a byte array to string representation.
    /// </summary>
    /// <param name="buf">Bytes of the string that will be converted.</param>
    /// <param name="enc">Encoding that will be used. If it is null, it will default to UTF-8 encoding.</param>
    /// <returns>The result of the conversion to string.</returns>
    public static string GetString(this byte[] buf, Encoding? enc = default)
        => (enc ?? Encoding.UTF8).GetString(buf);
}

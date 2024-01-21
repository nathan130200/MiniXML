using System.Runtime.CompilerServices;
using System.Text;

namespace MiniXML;

public static class Utilities
{
    public static TaskAwaiter GetAwaiter(this Parser parser)
        => parser._completition?.Task?.GetAwaiter() ?? default;

    public static byte[] GetBytes(this string str)
        => Encoding.UTF8.GetBytes(str);

    public static string GetString(this byte[] buf)
        => Encoding.UTF8.GetString(buf);
}

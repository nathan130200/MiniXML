using System.Text;

namespace MiniXML;

public static class Utilities
{
    public static byte[] GetBytes(this string str)
        => Encoding.UTF8.GetBytes(str);

    public static string GetString(this byte[] buf)
        => Encoding.UTF8.GetString(buf);

    public static byte[] GetBytes(this Element e, bool indent = false)
        => e.ToString(indent).GetBytes();
}

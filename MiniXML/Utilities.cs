using System.Diagnostics;
using System.Globalization;
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

    delegate bool TryParseDelegate<TValue>(string s, IFormatProvider provider, out TValue result);

    static readonly Dictionary<Type, Delegate> s_TryParsableCache = [];

    static Utilities()
    {
        s_TryParsableCache[typeof(ushort)] = new TryParseDelegate<ushort>(ushort.TryParse);
        s_TryParsableCache[typeof(uint)] = new TryParseDelegate<uint>(uint.TryParse);
        s_TryParsableCache[typeof(ulong)] = new TryParseDelegate<ulong>(ulong.TryParse);
        s_TryParsableCache[typeof(short)] = new TryParseDelegate<short>(short.TryParse);
        s_TryParsableCache[typeof(int)] = new TryParseDelegate<int>(int.TryParse);
        s_TryParsableCache[typeof(long)] = new TryParseDelegate<long>(long.TryParse);
        s_TryParsableCache[typeof(float)] = new TryParseDelegate<float>(float.TryParse);
        s_TryParsableCache[typeof(double)] = new TryParseDelegate<double>(double.TryParse);
        s_TryParsableCache[typeof(Guid)] = new TryParseDelegate<Guid>(Guid.TryParse);
        s_TryParsableCache[typeof(bool)] = new TryParseDelegate<bool>(TryParseBoolean);

        /*
        var nullableType = typeof(Nullable<>);
        var delegateType = typeof(TryParseDelegate<>);

        var baseMethod = typeof(Utilities)
            .GetMethod(nameof(TryParsableDelegateNullable), BindingFlags.Static | BindingFlags.NonPublic);

        foreach (var entry in s_TryParsableCache.ToArray())
        {
            var subType = typeof(Nullable<>)
                .MakeGenericType(entry.Key);

            var handlerType = baseMethod
                .MakeGenericMethod(nullableType)
                .CreateDelegate(delegateType.MakeGenericType(subType));

            s_TryParsableCache[nullableType] = handlerType;
        }*/
    }

    static bool TryParseBoolean(string s, IFormatProvider provider, out bool result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(s))
            return false;

        result = s.ToLowerInvariant() switch
        {
            "true" or "1" => true,
            "false" or "0" or _ => false,
        };

        return true;
    }

    static bool TryParsableDelegateNullable<T>(string s, IFormatProvider provider, out T? result)
        where T : struct
    {
        result = default;

        if (!s_TryParsableCache.TryGetValue(typeof(T), out var del))
            result = default;


        if (del is TryParseDelegate<T> tpd)
        {
            if (tpd(s, provider ?? CultureInfo.InvariantCulture, out var temp))
                result = temp;
        }

        return result.HasValue;
    }

    public static bool IsNullableType(ref Type type)
    {
        var result = Nullable.GetUnderlyingType(type);

        if (result != null)
            type = result;

        return result != null;
    }

    public static T TryParseString<T>(string value, IFormatProvider provider = default, T defaultValue = default)
    {
        var type = typeof(T);
        _ = IsNullableType(ref type);

        if (s_TryParsableCache.TryGetValue(type, out var del))
        {
            if (del is TryParseDelegate<T> func)
                return func(value, provider, out var temp) ? temp : defaultValue;
        }

        try
        {
            return (T)Convert.ChangeType(value, type);
        }

        catch (Exception e)
        {
            Debug.WriteLine(e);
            return defaultValue;
        }
    }
}

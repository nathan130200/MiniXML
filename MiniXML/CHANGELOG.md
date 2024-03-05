## MiniXM - Version 1.0.7

- Added extra extension methods for fast procuding element attributes.
	- `element.Attr(name, string value)`
	- `element.Attr(name, T value)` where `T` is `struct`
	- `element.Attr(name, T value, IFormatProvide provider)` where `T` is `struct`

- Added tuple-as-value based formatting attribute values.
	- `element.Attr(name, ((T value, string format) tuple)`
	- `element.Attr(name, ((T value, string format, IFormatProvider provider) tuple)`

- Added dynamic-as-value types for settings attributes as anonymous types (also accept `tuple-as-value`  for custom formatting the values)

```cs
var obj = new 
{
	attr1 = "value1",
	attr2 = (byte)255,
	attr3 = (sbyte)-127,
	attrN = ... 
	attrFormatted = (value, "myformat")
	attrFormattedExtended = (value, "myformat", myformatProvider)
};

element.Attrs(obj);
```	

Full example:

```cs
using System.Diagnostics;
using MiniXML;
using Xml = MiniXML.Xml;

// random guid created by VS 2022 Guid Generator Tool
var guid = Guid.Parse("3F4B87B6-88C0-405F-9811-508B5AAD20F5");

var root = Xml.Element("foo")
    .Attr("count", 1)
    .Attr("max_count", 2U)
    .Attr("flag", (byte)255)
    .Attr("max_dgram_size", ushort.MaxValue)
    .Attr("is_true", true)
    .Attr("is_false", false)
    .Attr("my_float", (5f, "F3"))
    .Attr("my_double", (10D, "F5"))
    .Attr("my_decimal", (15M, "F5"))
    .Attr("my_guid", guid)
    .Attr("my_guid_B", (guid, "B"))
    .Attr("my_guid_D", (guid, "D"))
    .Attr("my_guid_N", (guid, "N"))
    .Attr("my_guid_X", (guid, "X"))
    .Attr("unix_epoch_as_hex_x2", (DateTime.UnixEpoch.ToFileTime(), "X2"))
    .Attr("unix_epoch_as_hex_x8", (DateTime.UnixEpoch.ToFileTime(), "X8"))
    .Attrs(new
    {
        foo = "bar",
        my_byte = (byte)32,
        my_sbyte = (sbyte)64,
        my_formatted_guid_as_N = (guid, "N")
    });
```

### Notes

- The value for type `IFormatProvider` for all methods defaults to `CultureInfo.InvariantCulture`, if you need provide an attribute with different culture ulse respective methods instead with your formatter.
- `NumberFormatInfo` is a implementation of `IFormatProvider`.
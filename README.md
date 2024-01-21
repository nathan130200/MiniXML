# MiniXML
This project include a basic code to working with jabber protocol such as:

- Basic strict xml parsing for jabber standards. (Some xml features are forbidden in XMPP standards such as: prolog, PIs, cdatas, entity decls, schemas, comments)
- Basic jid parsing and handling.
- DOM based for navigate in elements.
- Fluent element builders for code readability.


<b>Fluent Element Builder</b>

```cs
var el = Xml.Element("iq", new()
{
	["xmlns"] = "jabber:client",
	["from"] = new Jid(...),
	["to"] = new Jid(...),
	["id"] = Guid.NewGuid(),
});

el.Child("query", "urn:cryonline:k01")
	.Child("gameroom_askserver").Up()
	;


var xml = el.ToString(indent: false);
```

- `Child` declares a new child element, has two overloads:
	- `.Child(name, xmlns, attrs)`
		- `name`: element qualified name
		- `xmlns`: fast setter for element namespace
		- `attrs`: Dictionary for fast setting attributes.

	- `.Child(name, attrs)`
		- `name`: element qualified name
		- `attrs`: Dictionary for fast setting attributes.

- `Attrs(attrs)` Set attributes from given dictionary in current element (same as in `Child` extension method).
- `Attr(name, value)` Set single attribute in current element.
- `Root()` Get the root element from current element.
- `Text(value)` Set content of this element. eg: `.Child(...).Text("foobar").Up()`
- `Up()` Return the parent (owner) element from this element.

<b>Extra Functions</b>

- `myString.GetBytes()` return byte array from given string utf8 string.
- `myByteArray.GetString()` return utf8 string from given byte array.


<b>Fluent Element Builder: Attribute Serialization</b>

All attributes are serialized based on inherited types. As fallback the value is converted direct to string (calling `.ToString()`)
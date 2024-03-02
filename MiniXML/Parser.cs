using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace MiniXML;

/// <summary>
/// XMPP parser class.
/// </summary>
public class Parser : IDisposable
{
    internal XmlReader _reader;
    internal TextReader _textReader;

    private readonly bool _leaveOpen;
    private volatile bool _disposed;

    public event Action<Element> OnStreamStart;
    public event Action<Element> OnStreamElement;
    public event Action OnStreamEnd;

    public bool IsDisposed
        => _disposed;

    public Parser(TextReader textReader, bool leaveOpen = true)
    {
        _textReader = textReader;
        _leaveOpen = leaveOpen;
        Reset();
    }

    public Parser(Stream baseStream, Encoding encoding = default)
    {
        _textReader = new StreamReader(baseStream, encoding ?? Encoding.UTF8, false, 256, true);
        Reset();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (!_leaveOpen)
        {
            _textReader?.Dispose();
            _textReader = null;
        }

        _reader?.Dispose();
        _reader = null;
    }

    public void Reset()
    {
        _reader?.Dispose();

        _reader = XmlReader.Create(_textReader, new XmlReaderSettings
        {
            Async = true,
            CloseInput = false,
            ConformanceLevel = ConformanceLevel.Fragment,
            DtdProcessing = DtdProcessing.Prohibit,
            ValidationFlags = XmlSchemaValidationFlags.AllowXmlAttributes,
            XmlResolver = XmlResolver.ThrowingResolver,
            IgnoreWhitespace = true,
            IgnoreProcessingInstructions = true,
            IgnoreComments = true,
        });
    }

    public int LineNumber
    {
        get
        {
            CheckDisposed();
            return _reader is IXmlLineInfo info ? info.LineNumber : 0;
        }
    }

    public int LinePosition
    {
        get
        {
            CheckDisposed();
            return _reader is IXmlLineInfo info ? info.LinePosition : 0;
        }
    }

    void CheckDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    #region Dispatch Events

    protected virtual void FireOnStreamEnd()
    {
        if (_disposed)
            return;

        try
        {
            OnStreamEnd?.Invoke();
        }
        finally
        {
            Dispose();
        }
    }

    protected virtual void FireOnStreamStart(Element e)
    {
        if (_disposed)
            return;

        OnStreamStart?.Invoke(e);
    }

    protected virtual void FireOnStreamElement(Element e)
    {
        if (_disposed)
            return;

        OnStreamElement?.Invoke(e);
    }

    #endregion

    protected virtual Element CreateElement(string name, Dictionary<string, string> attributes)
    {
        var e = new Element(name);

        foreach (var (key, value) in attributes)
            e.SetAttribute(key, value);

        return e;
    }

    Element currentElem;

    public void Update()
    {
        CheckDisposed();

        if (!_reader.Read())
        {
            bool isEOF = _reader.EOF;
            Dispose();

            if (isEOF)
                return;

            throw new IOException("Unable to read XML from stream.");
        }

        switch (_reader.NodeType)
        {
            case XmlNodeType.Element:
                {
                    var newElem = new Element(_reader.Name, _reader.NamespaceURI);

                    if (_reader.HasAttributes)
                    {
                        while (_reader.MoveToNextAttribute())
                            newElem.SetAttribute(_reader.Name, _reader.Value);

                        _reader.MoveToElement();
                    }

                    if (newElem.Name == "stream:stream")
                        FireOnStreamStart(newElem);
                    else
                    {
                        if (_reader.IsEmptyElement) // self closing tag
                        {
                            if (currentElem == null)
                                FireOnStreamElement(newElem);
                            else
                                currentElem.AddChild(newElem);
                        }
                        else
                        {
                            currentElem?.AddChild(newElem);
                            currentElem = newElem;
                        }
                    }
                }
                break;

            case XmlNodeType.EndElement:
                {
                    if (_reader.Name == "stream:stream")
                        FireOnStreamEnd();
                    else
                    {
                        Debug.Assert(currentElem != null);

                        if (currentElem.Name != _reader.Name)
                        {
                            throw new InvalidOperationException("Unexpected eng tag.")
                            {
                                Data =
                                {
                                    ["Expected"] = currentElem.Name,
                                    ["Actual"] = _reader.Name
                                }
                            };
                        }

                        var parent = currentElem.Parent;

                        if (parent == null)
                            FireOnStreamElement(currentElem);

                        currentElem = parent;
                    }
                }
                break;

            case XmlNodeType.SignificantWhitespace:
            case XmlNodeType.Text:
                {
                    Debug.Assert(currentElem != null);
                    currentElem.Value += _reader.Value;
                }
                break;

            default:
                // ignore
                break;
        }
    }
}

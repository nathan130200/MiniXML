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
    internal XmlReader _parser;
    internal TextReader _stream;
    private volatile bool _disposed;

    public event Action<Element> OnStreamStart;
    public event Action<Element> OnStreamElement;
    public event Action OnStreamEnd;

    const int DefaultBufferSize = 256;

    public Parser(Stream inputStream, int? charBufferSize = default, Encoding encoding = default)
    {
        int bufferSize = charBufferSize.GetValueOrDefault(DefaultBufferSize);

        if (bufferSize <= 0)
            bufferSize = DefaultBufferSize;

        encoding ??= Encoding.UTF8;

        _stream = new StreamReader(inputStream, encoding, false, bufferSize, true);
        Reset();
    }

    public bool IsEndOfStream
        => _disposed || (_parser == null || _parser.EOF);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _parser?.Dispose();
        _parser = null;
        _stream?.Dispose();
        _stream = null;

        GC.SuppressFinalize(this);
    }

    public void Reset()
    {
        _parser?.Dispose();

        _parser = XmlReader.Create(_stream, new XmlReaderSettings
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
            return _parser is IXmlLineInfo info ? info.LineNumber : 0;
        }
    }

    public int LinePosition
    {
        get
        {
            CheckDisposed();
            return _parser is IXmlLineInfo info ? info.LinePosition : 0;
        }
    }

    void CheckDisposed()
    {
        if (_disposed)
            Debugger.Break();

        ObjectDisposedException.ThrowIf(_disposed, this);
    }

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

        if (!_parser.Read())
        {
            bool isEOF = _parser.EOF;
            Dispose();

            if (isEOF)
                return;

            throw new IOException("Unable to read XML from stream.");
        }

        switch (_parser.NodeType)
        {
            case XmlNodeType.Element:
                {
                    var newElem = new Element(_parser.Name, _parser.NamespaceURI);

                    if (_parser.HasAttributes)
                    {
                        while (_parser.MoveToNextAttribute())
                            newElem.SetAttribute(_parser.Name, _parser.Value);

                        _parser.MoveToElement();
                    }

                    if (newElem.Name == "stream:stream")
                        FireOnStreamStart(newElem);
                    else
                    {
                        if (_parser.IsEmptyElement) // self closing tag
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
                    if (_parser.Name == "stream:stream")
                        FireOnStreamEnd();
                    else
                    {
                        Debug.Assert(currentElem != null);

                        if (currentElem.Name != _parser.Name)
                        {
                            throw new InvalidOperationException("Unexpected eng tag.")
                            {
                                Data =
                                {
                                    ["Expected"] = currentElem.Name,
                                    ["Actual"] = _parser.Name
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
                    currentElem.Value += _parser.Value;
                }
                break;

            default:
                // ignore
                break;
        }
    }
}

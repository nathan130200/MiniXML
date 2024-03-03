using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace MiniXML;

/// <summary>
/// XMPP parser class.
/// </summary>
public class Parser : IDisposable
{
    private XmlReader _reader;
    private volatile bool _disposed;

    private TextReader _streamReader;
    private readonly int _bufferSize;
    private readonly Encoding _encoding;

    public event Action<Element> OnStreamStart;
    public event Action<Element> OnStreamElement;
    public event Action OnStreamEnd;

    public Parser(Stream baseStream, int charBufferSize = 256, Encoding encoding = default)
    {
        _bufferSize = charBufferSize <= 0 ? 256 : charBufferSize;
        _encoding = encoding ?? Encoding.UTF8;
        Reset(baseStream);
    }

    public bool IsEndOfStream
        => _disposed || _reader == null || _reader.EOF;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _reader?.Dispose();
        _reader = null;
        _streamReader?.Dispose();
        _streamReader = null;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Internally restarts the parser, clearing the previous state and its data.
    /// </summary>
    /// <param name="newStream">Specifies which data stream the parser will process. If not specified, it will keep the same stream as before.</param>
    public void Reset(Stream newStream = default)
    {
        _reader?.Dispose();

        if (newStream != null)
        {
            _streamReader?.Dispose();

            _streamReader = new StreamReader(newStream, _encoding, false, _bufferSize, true);
        }

        _reader = XmlReader.Create(_streamReader, new XmlReaderSettings
        {
            CloseInput = false,
            ConformanceLevel = ConformanceLevel.Fragment,
            DtdProcessing = DtdProcessing.Prohibit,
            ValidationFlags = XmlSchemaValidationFlags.AllowXmlAttributes,
            XmlResolver = XmlResolver.ThrowingResolver,
            IgnoreWhitespace = true,
            IgnoreProcessingInstructions = true,
            IgnoreComments = true,
        });

        _currentElement = null;
    }


    /// <summary>
    /// Gets the current line number.
    /// </summary>
    public int LineNumber
    {
        get
        {
            CheckDisposed();
            return _reader is IXmlLineInfo info ? info.LineNumber : 0;
        }
    }

    /// <summary>
    /// Gets the position on the current line.
    /// </summary>
    public int LinePosition
    {
        get
        {
            CheckDisposed();
            return _reader is IXmlLineInfo info ? info.LinePosition : 0;
        }
    }

    protected void CheckDisposed()
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

    Element _currentElement;

#nullable enable

    /// <summary>
    /// Factory function to create the new element that was read.
    /// </summary>
    /// <param name="name">Element qualified name (includes prefix)</param>
    /// <param name="namespace">Element namespace (if it has a prefix, it will be the prefix namespace)</param>
    /// <returns>Return the created instance of the element.</returns>
    protected virtual Element CreateElement(string name, string? @namespace = default)
        => new(name, @namespace);

    /// <summary>
    /// Function that helps to retrieve the namespace of the element that is currently in the scope of the parser.
    /// </summary>
    /// <param name="prefix">Optional namespace prefix or null if it is the default namespace (xmlns)</param>
    /// <returns>Returns the namespace if it exists, or null if the namespace is not declared in the scope.</returns>
    protected virtual string? LookupNamespace(string? prefix = default)
        => _reader.LookupNamespace(prefix ?? string.Empty);

#nullable restore


    /// <summary>
    /// Makes the parser process and fire events. Similar to the <c>RunCallbacks</c> function in other libraries.
    /// </summary>
    /// <returns>Returns true if the function was able to process without problems, or false if the parser reached the end of the stream.</returns>
    /// <exception cref="InvalidOperationException">
    /// <list type="bullet">
    /// <item>When the parser was disposed and this function was still invoked.</item>
    /// <item>When the parse finds a tag that closed without having even been opened previously (e.g. this is only valid for jabber's <c><![CDATA[</stream:stream>]]></c>).</item>
    /// <item>When the factory function returns a null element.</item>
    /// </list>
    /// </exception>
    /// <exception cref="XmlException">If the parser found anything that violates the XML rules.</exception>
    public bool Update()
    {
        CheckDisposed();

        // Generally calls to this function in "blocking mode" will stall this function until
        // it has some XML data to process (e.g. tcp socket/stream).
        if (!_reader.Read())
        {
            bool isEOF = _reader.EOF;
            Dispose();

            if (isEOF)
                return false;

            throw new InvalidOperationException("The XML parser cannot process more of the stream.");
        }

        switch (_reader.NodeType)
        {
            case XmlNodeType.Element:
                {
                    var element = CreateElement(_reader.Name, _reader.NamespaceURI) ?? throw new InvalidOperationException("The factory function returned a null element.");

                    if (_reader.HasAttributes)
                    {
                        while (_reader.MoveToNextAttribute())
                            element.SetAttribute(_reader.Name, _reader.Value);

                        _reader.MoveToElement();
                    }

                    if (element.Name == "stream:stream")
                        FireOnStreamStart(element);
                    else
                    {
                        if (_reader.IsEmptyElement) // self closing tag
                        {
                            if (_currentElement == null)
                                FireOnStreamElement(element);
                            else
                                _currentElement.AddChild(element);
                        }
                        else
                        {
                            _currentElement?.AddChild(element);
                            _currentElement = element;
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
                        if (_currentElement == null)
                            throw new InvalidOperationException("Current element should not be null while parsing end tag.");

                        if (_currentElement.Name != _reader.Name)
                            throw new InvalidOperationException($"Unexpected eng tag: {_reader.Name}");

                        var parent = _currentElement.Parent;

                        if (parent == null)
                            FireOnStreamElement(_currentElement);

                        _currentElement = parent;
                    }
                }
                break;

            case XmlNodeType.SignificantWhitespace:
            case XmlNodeType.Text:
                {
                    if (_currentElement != null)
                        _currentElement.Value += _reader.Value;
                }
                break;

            default:
                // ignore
                break;
        }

        return true;
    }
}

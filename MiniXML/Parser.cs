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
    private XmlReader? _reader;
    internal CancellationTokenSource? _cts;
    internal TaskCompletionSource _completition;
    private volatile bool _disposed;

    /// <summary>
    /// Fired when the parser encounters an unrecoverable error.
    /// </summary>
    public event Action<Exception> OnError = default!;

    /// <summary>
    /// Fired when the parser encounters the jabber opening tag. <c><![CDATA[<stream:stream xmlns:stream="http://etherx.jabber.org"...>]]></c>
    /// </summary>
    public event Action<Element> OnStreamStart = default!;

    /// <summary>
    /// Triggered when the parser manages to read a complete element, its descendants and content.
    /// </summary>
    public event Action<Element> OnStreamElement = default!;

    /// <summary>
    /// Fired when the parser encounters the jabber closing tag. <c><![CDATA[</stream:stream>]]></c>
    /// </summary>
    public event Action<Element?> OnStreamEnd = default!;

    /// <summary>
    /// XMPP parser constructor.
    /// </summary>
    /// <param name="reader">The <see cref="TextReader"/> instance that will be used to extract the XML data.</param>
    /// <param name="leaveOpen">Determines the behavior of the <see cref="TextReader"/>. If <see langword="true" />, it will not destroy the inner stream wrapped in the <paramref name="reader"/>.</param>
    public Parser(TextReader reader, bool leaveOpen = false)
    {
        _completition = new TaskCompletionSource();

        _reader = XmlReader.Create(reader, new XmlReaderSettings
        {
            Async = true,
            CloseInput = !leaveOpen,
            ConformanceLevel = ConformanceLevel.Fragment,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Prohibit,
            ValidationFlags = XmlSchemaValidationFlags.AllowXmlAttributes,
            XmlResolver = XmlResolver.ThrowingResolver,
        });
    }

    /// <summary>
    /// Creates a background task that will read, process xml and fire the events.
    /// </summary>
    /// <param name="token">Cancellation token that can remotely notify you when it is necessary to stop reading.</param>
    public void Start(CancellationToken token = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _cts.Token.Register(() => _completition.TrySetResult());
        _ = Task.Run(ParseXmlInternal, _cts.Token);
    }

    /// <summary>
    /// Gets the line the parser is reading from.
    /// </summary>
    public uint LineNumber
    {
        get
        {
            CheckDisposed();
            return (uint)((_reader as IXmlLineInfo)?.LineNumber ?? 0);
        }
    }

    /// <summary>
    /// Gets the column the parser is reading from.
    /// </summary>
    public uint ColumnNumber
    {
        get
        {
            CheckDisposed();
            return (uint)((_reader as IXmlLineInfo)?.LinePosition ?? 0);
        }
    }

    void CheckDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    // Wraps a stream in a text reader.
    static StreamReader GetReader(Stream baseStream, XmlEncoding encoding, bool leaveOpen)
        => new(baseStream, GetSystemEncoding(encoding), leaveOpen: leaveOpen);

    static Encoding GetSystemEncoding(XmlEncoding encoding) => encoding switch
    {
        XmlEncoding.ASCII => Encoding.ASCII,
        XmlEncoding.UTF16LE => Encoding.Unicode,
        XmlEncoding.UTF16BE => Encoding.BigEndianUnicode,
        XmlEncoding.ISO88591 => Encoding.Latin1,
        XmlEncoding.UTF8 or _ => Encoding.UTF8,
    };

    /// <summary>
    /// XMPP parser constructor.
    /// </summary>
    /// <param name="baseStream"><see cref="Stream"/> that will be wrapped in a <see cref="TextReader"/> to read the XML.</param>
    /// <param name="encoding">Hint to the XML reader which encoding they can expect.</param>
    /// <param name="leaveOpen">If set to <see langword="true" />, the <paramref name="baseStream"/> will not be destroyed when Dispose in the parser, thus being able to reuse the supplied stream. </param>
    public Parser(Stream baseStream, XmlEncoding encoding, bool leaveOpen = true) :
        this(GetReader(baseStream, encoding, leaveOpen), true)
    {

    }

    #region Dispatch Events

    /// <summary>
    /// Fires the event to notify that the parser has encountered a critical problem.
    /// </summary>
    protected virtual void FireOnError(Exception error)
    {
        Exception exception = error;

        try
        {
            OnError?.Invoke(exception);
        }
        catch (Exception eventError)
        {
            Debug.WriteLine(exception = new AggregateException(error, eventError));
        }
        finally
        {
            _completition.TrySetException(exception);
            Dispose();
        }
    }

    /// <summary>
    /// Fires the event to notify that the parser received the XMPP end tag.
    /// </summary>
    protected virtual void FireOnStreamEnd(Element? tag)
    {
        if (_disposed)
            return;

        try
        {
            OnStreamEnd?.Invoke(tag);
        }
        catch (Exception ex)
        {
            FireOnError(ex);
        }
        finally
        {
            Dispose();
        }
    }

    /// <summary>
    /// Fires the event to notify that the parser received the XMPP start tag.
    /// </summary>
    protected virtual void FireOnStreamStart(Element e)
    {
        if (_disposed)
            return;

        try
        {
            OnStreamStart?.Invoke(e);
        }
        catch (Exception ex)
        {
            FireOnError(ex);
        }
    }

    /// <summary>
    /// Fires the event to notify that the parser received regular XMPP elements.
    /// </summary>
    protected virtual void FireOnStreamElement(Element e)
    {
        if (_disposed)
            return;

        try
        {
            OnStreamElement?.Invoke(e);
        }
        catch (Exception ex)
        {
            FireOnError(ex);
        }
    }

    #endregion

    /// <summary>
    /// This function is responsible for building the element that will be dispatched later.
    /// </summary>
    /// <param name="name">Name of the element that was just processed.</param>
    /// <param name="attributes">The attribute dictionary that accompanies this element.</param>
    /// <returns>[required] An instance of a valid <see cref="Element" /> or at least one class instance that inherits from the <see cref="Element" /> class.</returns>
    protected virtual Element CreateElement(string name, Dictionary<string, string> attributes)
    {
        var e = new Element(name);

        foreach (var (key, value) in attributes)
            e.SetAttribute(key, value);

        return e;
    }

    /// <summary>
    /// Tracking current parsed element.
    /// </summary>
    Element? currentElement;

    /// <summary>
    /// Tracking root element (aka jabber start/end tag).
    /// </summary>
    Element? rootElement;

    void ParseXmlInternal()
    {
        SpinWait spin = default;

        try
        {
            while (_cts != null && !_cts.IsCancellationRequested)
            {
                spin.SpinOnce();

                if (_disposed)
                    break;

                if (_reader == null)
                    break;

                if (!_reader.Read())
                {
                    FireOnStreamEnd(rootElement);
                    break;
                }
                else
                {
                    switch (_reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            {
                                Element newElem;

                                {
                                    var name = _reader.Name;
                                    var attributes = new Dictionary<string, string>();

                                    if (_reader.HasAttributes)
                                    {
                                        while (_reader.MoveToNextAttribute())
                                            attributes[_reader.Name] = _reader.Value;

                                        _reader.MoveToElement();
                                    }

                                    //
                                    // Let's separate this and leave a factory function
                                    // that will be responsible for creating each instance
                                    // of the element. This is important if the developer
                                    // wants to implement a mapping of each element type
                                    // to inherit this parser in another class of its own.
                                    //
                                    // Always considering, this library is a "gateway"
                                    // to an XML reader for XMPP. Everything should be
                                    // as shallow as possible.
                                    //

                                    newElem = CreateElement(name, attributes);
                                }

                                if (newElem == null)
                                    throw new InvalidOperationException("The element created with the factory function cannot be null.");

                                if (newElem.Name == "stream:stream")
                                    FireOnStreamStart(rootElement = newElem);
                                else
                                {
                                    currentElement?.AddChild(newElem);

                                    // If we have an self-closing tag, don't
                                    // need to push into element stack.

                                    if (!_reader.IsEmptyElement)
                                        currentElement = newElem;
                                }
                            }
                            break;

                        case XmlNodeType.EndElement:
                            {
                                if (_reader.Name == "stream:stream")
                                {
                                    FireOnStreamEnd(rootElement);
                                    rootElement = default;
                                    break;
                                }
                                else
                                {
                                    Debug.Assert(currentElement != null);

                                    if (currentElement.Name != _reader.Name)
                                        throw new XmlException("Mismatch end tag");

                                    var parent = currentElement.Parent;

                                    if (parent == null)
                                        FireOnStreamElement(currentElement);

                                    currentElement = parent;
                                }
                            }
                            break;

                        case XmlNodeType.SignificantWhitespace:
                        case XmlNodeType.Text:
                            {
                                if (currentElement != null)
                                    currentElement.Value += _reader.Value;
                            }
                            break;

                        default:

                            break;
                    }
                }
            }
        }
        catch (XmlException xe)
        {
            FireOnError(xe);
        }
        catch (Exception e)
        {
            FireOnError(e);
        }
        finally
        {
            Dispose();
        }
    }


    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _completition.TrySetResult();
        if (_cts != null)
        {
            if (!_cts.IsCancellationRequested)
                _cts.Cancel();

            _cts.Dispose();
            _cts = default;
        }

        if (_reader != null)
        {
            _reader.Dispose();
            _reader = null;
        }

        GC.SuppressFinalize(this);
    }
}

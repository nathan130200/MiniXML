using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace MiniXML;

/// <summary>
/// Mini XML parser.
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
    public event Action<Element> OnStreamEnd = default!;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="reader">A text reader instance from which you will read the XML.</param>
    /// <param name="leaveOpen">
    /// Determines the behavior of the underlying stream. The stream will be disposed if this value is false.
    /// <para>
    /// For TCP connections, for example when reading data in the XMPP connection, it is recommended to keep this value as true.
    /// </para>
    /// </param>
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

    // Generic globally UTF-8 encoder/decoder instance.
    static readonly Encoding s_UTF8 = new UTF8Encoding(false, false);

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
    /// Constructor
    /// </summary>
    /// <param name="baseStream">A stream that will be read from.</param>
    public Parser(Stream baseStream, XmlEncoding encoding, bool leaveOpen = true) :
        this(GetReader(baseStream, encoding, leaveOpen), true)
    {

    }

    #region Dispatch Events

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

    Element? currentElement, rootElement;

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
                                var newElem = new Element
                                {
                                    Name = _reader.Name
                                };

                                if (_reader.HasAttributes)
                                {
                                    while (_reader.MoveToNextAttribute())
                                        newElem.SetAttribute(_reader.Name, _reader.Value);

                                    _reader.MoveToElement();
                                }

                                if (newElem.Name == "stream:stream")
                                    FireOnStreamStart(rootElement = newElem);
                                else
                                {
                                    currentElement?.AddChild(newElem);

                                    if (!_reader.IsEmptyElement) // if we have an self-closing tag, don't need to push into element stack
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

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _completition.TrySetResult();
        _cts.Cancel();
        _cts.Dispose();

        if (_reader != null)
        {
            _reader.Dispose();
            _reader = null;
        }

        GC.SuppressFinalize(this);
    }
}

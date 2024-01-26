namespace MiniXML;

/// <summary>
/// Enumerator that contains the list of supported XML encodings for parsing.
/// </summary>
public enum XmlEncoding
{
    /// <summary>
    /// Standard ASCII encoding.
    /// </summary>
    ASCII,

    /// <summary>
    /// UTF-8 encoding
    /// </summary>
    UTF8,

    /// <summary>
    /// UTF-16 (Big Endian) encoding
    /// </summary>
    UTF16BE,

    /// <summary>
    /// UTF-16 (Little Endian) encoding
    /// </summary>
    UTF16LE,

    /// <summary>
    /// Latin 1 encoding (or also known as ISO-8859-1)
    /// </summary>
    ISO88591
}

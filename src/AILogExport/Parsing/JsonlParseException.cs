namespace AILogExport.Parsing;

/// <summary>
/// JSONL内の特定行をJSONとして解析できなかったことを表す。
/// </summary>
public sealed class JsonlParseException : Exception
{
    public JsonlParseException(string filePath, int lineNumber, Exception innerException)
        : base($"JSONLの解析に失敗した。File: {filePath}, Line: {lineNumber}", innerException)
    {
        FilePath = filePath;
        LineNumber = lineNumber;
    }

    /// <summary>
    /// 解析に失敗したJSONLファイル
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// 解析に失敗した1始まりの行番号
    /// </summary>
    public int LineNumber { get; }
}

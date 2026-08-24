using System.Text;

namespace AILogExport.Parsing;

/// <summary>
/// AIクライアントが追記中のJSONLも共有読み取りするためのファイルアクセスを提供する。
/// </summary>
internal static class JsonlFile
{
    /// <summary>
    /// 書き込み側のハンドルを妨げず、呼び出し時点で読み取れる完全な行を順に返す。
    /// </summary>
    public static IEnumerable<string> ReadLines(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}

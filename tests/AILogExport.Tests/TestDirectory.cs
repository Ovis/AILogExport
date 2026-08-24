namespace AILogExport.Tests;

/// <summary>
/// テストごとに一時ディレクトリを作成し、終了時に確実に削除する。
/// </summary>
internal sealed class TestDirectory : IDisposable
{
    public TestDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "AILogExport.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// 作成した一時ディレクトリの絶対パス
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// 指定した各文字列を一行のJSONとして一時ファイルへ書き込む。
    /// </summary>
    public string WriteJsonl(params string[] lines) => WriteJsonlAt("session.jsonl", lines);

    /// <summary>
    /// 相対パスを指定してJSONLテストデータを書き込む。
    /// </summary>
    public string WriteJsonlAt(string relativePath, params string[] lines)
    {
        var filePath = System.IO.Path.Combine(Path, relativePath);
        var parentDirectory = System.IO.Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(parentDirectory);
        File.WriteAllText(filePath, string.Join("\n", lines) + "\n", new System.Text.UTF8Encoding(false));
        return filePath;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

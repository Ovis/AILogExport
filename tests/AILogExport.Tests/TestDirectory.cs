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

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

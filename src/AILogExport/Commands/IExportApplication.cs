namespace AILogExport.Commands;

/// <summary>
/// コマンドライン引数から実行されるエクスポート処理を定義する。
/// </summary>
public interface IExportApplication
{
    /// <summary>
    /// 指定された入力元、入力ファイル、オプションに従って会話を出力する。
    /// </summary>
    int Run(
        string? sourceId,
        string? inputPath,
        string? outputPath,
        bool force,
        string? sourceDirectory);
}

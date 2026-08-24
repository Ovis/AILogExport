using System.CommandLine;

namespace AILogExport.Commands;

/// <summary>
/// AILogExportのコマンドライン構造とアプリケーション呼び出しを組み立てる。
/// </summary>
public static class CommandLineFactory
{
    /// <summary>
    /// 直接指定モードと対話モードの両方を受け付けるルートコマンドを生成する。
    /// </summary>
    public static RootCommand Create(IExportApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var sourceArgument = new Argument<string?>("source")
        {
            Description = "入力元。claudeまたはcodexを指定する。省略時は対話選択する。",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var inputArgument = new Argument<string?>("input")
        {
            Description = "入力するJSONLファイル。省略時はセッション一覧から選択する。",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = "Markdown出力先",
        };
        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "既存の出力ファイルを上書きする",
        };
        var sourceDirectoryOption = new Option<string?>("--source-dir")
        {
            Description = "対話モードで使用するログ探索ルート",
        };

        var rootCommand = new RootCommand(
            "Claude CodeおよびCodexのセッションログをMarkdownへ変換する。");
        rootCommand.Arguments.Add(sourceArgument);
        rootCommand.Arguments.Add(inputArgument);
        rootCommand.Options.Add(outputOption);
        rootCommand.Options.Add(forceOption);
        rootCommand.Options.Add(sourceDirectoryOption);

        rootCommand.SetAction(parseResult => application.Run(
            parseResult.GetValue(sourceArgument),
            parseResult.GetValue(inputArgument),
            parseResult.GetValue(outputOption),
            parseResult.GetValue(forceOption),
            parseResult.GetValue(sourceDirectoryOption)));

        return rootCommand;
    }
}

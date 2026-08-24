using AILogExport.Conversations;
using AILogExport.Exporters;
using AILogExport.Interactive;
using AILogExport.Parsing;

namespace AILogExport.Commands;

/// <summary>
/// CLIモードの判定、入力選択、上書き制御、Markdown保存を調整する。
/// </summary>
public sealed class ExportApplication : IExportApplication
{
    private readonly IReadOnlyList<IConversationSource> sources;
    private readonly MarkdownExporter exporter;
    private readonly IInteractiveConsole console;

    public ExportApplication(
        IReadOnlyList<IConversationSource> sources,
        MarkdownExporter exporter,
        IInteractiveConsole console)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(console);

        if (sources.Count == 0)
        {
            throw new ArgumentException("入力元を一つ以上指定する必要がある。", nameof(sources));
        }

        this.sources = sources;
        this.exporter = exporter;
        this.console = console;
    }

    public int Run(
        string? sourceId,
        string? inputPath,
        string? outputPath,
        bool force,
        string? sourceDirectory)
    {
        try
        {
            var source = ResolveSource(sourceId);
            var interactiveMode = string.IsNullOrWhiteSpace(inputPath);
            var selectedInputPath = interactiveMode
                ? SelectInputPath(source, sourceDirectory)
                : ValidateInputPath(inputPath!);
            var selectedOutputPath = ResolveOutputPath(
                selectedInputPath,
                outputPath,
                interactiveMode);

            if (!CanWrite(selectedOutputPath, force, interactiveMode))
            {
                console.WriteInformation("出力をキャンセルした。");
                return 0;
            }

            var conversation = source.ReadConversation(selectedInputPath);
            exporter.Write(conversation, selectedOutputPath);
            console.WriteSuccess(selectedOutputPath);
            return 0;
        }
        catch (Exception exception) when (IsExpectedError(exception))
        {
            console.WriteError(exception.Message);
            return 1;
        }
    }

    private IConversationSource ResolveSource(string? sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return console.SelectSource(sources);
        }

        var source = sources.FirstOrDefault(candidate =>
            candidate.Id.Equals(sourceId, StringComparison.OrdinalIgnoreCase));
        return source ?? throw new ArgumentException(
            $"未対応の入力元である: {sourceId}。claudeまたはcodexを指定する。",
            nameof(sourceId));
    }

    private string SelectInputPath(IConversationSource source, string? sourceDirectory)
    {
        var conversations = source.FindConversations(sourceDirectory);
        if (conversations.Count == 0)
        {
            throw new InvalidOperationException($"{source.DisplayName}のセッションログが見つからない。");
        }

        return console.SelectConversation(source, conversations).FilePath;
    }

    private static string ValidateInputPath(string inputPath)
    {
        var fullPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("入力ファイルが見つからない。", fullPath);
        }

        if (!Path.GetExtension(fullPath).Equals(".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("入力ファイルには拡張子.jsonlを指定する。", nameof(inputPath));
        }

        return fullPath;
    }

    private string ResolveOutputPath(
        string inputPath,
        string? outputPath,
        bool interactiveMode)
    {
        var defaultPath = Path.ChangeExtension(inputPath, ".md");
        var selectedPath = string.IsNullOrWhiteSpace(outputPath)
            ? interactiveMode
                ? console.AskOutputPath(defaultPath)
                : defaultPath
            : outputPath;
        var fullPath = Path.GetFullPath(selectedPath);

        if (Directory.Exists(fullPath))
        {
            throw new ArgumentException("出力先にはディレクトリではなくMarkdownファイルを指定する。", nameof(outputPath));
        }

        if (!Path.GetExtension(fullPath).Equals(".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("出力ファイルには拡張子.mdを指定する。", nameof(outputPath));
        }

        return fullPath;
    }

    private bool CanWrite(string outputPath, bool force, bool interactiveMode)
    {
        if (!File.Exists(outputPath) || force)
        {
            return true;
        }

        if (!interactiveMode)
        {
            throw new IOException(
                $"出力ファイルは既に存在する。上書きする場合は--forceを指定する: {outputPath}");
        }

        return console.ConfirmOverwrite(outputPath);
    }

    private static bool IsExpectedError(Exception exception) => exception is
        ArgumentException or
        DirectoryNotFoundException or
        FileNotFoundException or
        IOException or
        UnauthorizedAccessException or
        InvalidOperationException or
        NotSupportedException or
        JsonlParseException;
}

using AILogExport.Conversations;
using Spectre.Console;

namespace AILogExport.Interactive;

/// <summary>
/// Spectre.Consoleを使用して対話選択と結果表示を行う。
/// </summary>
public sealed class SpectreInteractiveConsole : IInteractiveConsole
{
    public IConversationSource SelectSource(IReadOnlyList<IConversationSource> sources) =>
        AnsiConsole.Prompt(
            new SelectionPrompt<IConversationSource>()
                .Title("ログの種類を選択してください。")
                .PageSize(10)
                .UseConverter(source => Markup.Escape(source.DisplayName))
                .AddChoices(sources));

    public ConversationInfo SelectConversation(
        IConversationSource source,
        IReadOnlyList<ConversationInfo> conversations) =>
        AnsiConsole.Prompt(
            new SelectionPrompt<ConversationInfo>()
                .Title($"{Markup.Escape(source.DisplayName)}のセッションを選択してください。")
                .PageSize(15)
                .MoreChoicesText("[grey](上下キーで他のセッションを表示)[/]")
                .UseConverter(FormatConversation)
                .AddChoices(conversations));

    public string AskOutputPath(string defaultPath) => AnsiConsole.Prompt(
        new TextPrompt<string>("Markdownの出力先を入力してください。")
            .DefaultValue(defaultPath));

    public bool ConfirmOverwrite(string outputPath) => AnsiConsole.Confirm(
        $"[yellow]{Markup.Escape(outputPath)}[/] は既に存在します。上書きしますか？",
        defaultValue: false);

    public void WriteSuccess(string outputPath) => AnsiConsole.MarkupLine(
        $"[green]Markdownを出力しました。[/] {Markup.Escape(outputPath)}");

    public void WriteInformation(string message) =>
        AnsiConsole.MarkupLine(Markup.Escape(message));

    public void WriteError(string message) =>
        AnsiConsole.MarkupLine($"[red]エラー:[/] {Markup.Escape(message)}");

    private static string FormatConversation(ConversationInfo conversation)
    {
        var updatedAt = conversation.UpdatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            ?? "日時不明";
        var project = GetProjectDisplayName(conversation.ProjectPath);
        var title = conversation.Title
            ?? conversation.FirstUserMessage
            ?? conversation.Id;
        var singleLineTitle = string.Join(
            " ",
            title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (singleLineTitle.Length > 80)
        {
            singleLineTitle = singleLineTitle[..77] + "...";
        }

        return Markup.Escape($"{updatedAt} | {project} | {singleLineTitle}");
    }

    private static string GetProjectDisplayName(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return "プロジェクト不明";
        }

        var trimmed = projectPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmed) is { Length: > 0 } name ? name : projectPath;
    }
}

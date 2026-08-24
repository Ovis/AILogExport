using System.Text;
using AILogExport.Conversations;

namespace AILogExport.Exporters;

/// <summary>
/// 正規化済みの会話を、保存用のMarkdownへ変換する。
/// </summary>
public sealed class MarkdownExporter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// 会話のメタデータとUser、Assistantの本文からMarkdownを生成する。
    /// </summary>
    public string Export(Conversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        var markdown = new StringBuilder();
        markdown.Append("# AI Conversation\n\n");
        markdown.Append("**Source:** ").Append(GetSourceDisplayName(conversation.Source)).Append("  \n");

        if (!string.IsNullOrWhiteSpace(conversation.ProjectPath))
        {
            markdown.Append("**Project:** `")
                .Append(NormalizeMetadata(conversation.ProjectPath))
                .Append("`  \n");
        }

        markdown.Append("**Session:** `")
            .Append(NormalizeMetadata(conversation.Id))
            .Append("`\n\n---\n");

        foreach (var message in conversation.Messages)
        {
            if (message.Role is not (ConversationRole.User or ConversationRole.Assistant) ||
                string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            markdown.Append("\n## ")
                .Append(message.Role == ConversationRole.User ? "User" : "Assistant")
                .Append("\n\n")
                .Append(NormalizeContent(message.Content).TrimEnd('\n'))
                .Append('\n');
        }

        return markdown.ToString();
    }

    /// <summary>
    /// MarkdownをUTF-8 BOMなし、LF改行でファイルへ保存する。
    /// 上書き可否の判断は呼び出し側が行う。
    /// </summary>
    public void Write(Conversation conversation, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        File.WriteAllText(outputPath, Export(conversation), Utf8WithoutBom);
    }

    private static string GetSourceDisplayName(ConversationSourceType source) => source switch
    {
        ConversationSourceType.Claude => "Claude Code",
        ConversationSourceType.Codex => "Codex",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "未対応の入力元である。"),
    };

    private static string NormalizeContent(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

    private static string NormalizeMetadata(string value) =>
        NormalizeContent(value).Replace('\n', ' ');
}

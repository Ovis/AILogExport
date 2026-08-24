namespace AILogExport.Conversations;

/// <summary>
/// 入力元固有のイベントから抽出した一つの会話メッセージを表す。
/// </summary>
public sealed class ConversationMessage
{
    /// <summary>
    /// 発言者またはイベントの役割
    /// </summary>
    public required ConversationRole Role { get; init; }

    /// <summary>
    /// Markdownを含み得るメッセージ本文
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// ログに記録されているメッセージ日時
    /// </summary>
    public DateTimeOffset? Timestamp { get; init; }
}

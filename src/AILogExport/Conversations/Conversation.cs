namespace AILogExport.Conversations;

/// <summary>
/// 入力元に依存しない、一つの会話セッションを表す。
/// </summary>
public sealed class Conversation
{
    /// <summary>
    /// 入力元が記録したセッション識別子
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 会話ログの入力元
    /// </summary>
    public required ConversationSourceType Source { get; init; }

    /// <summary>
    /// 入力元が明示的に記録したタイトル
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// セッションを実行したプロジェクトのパス
    /// </summary>
    public string? ProjectPath { get; init; }

    /// <summary>
    /// ログから取得したセッション開始日時
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// ログから取得した最後のイベント日時
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// 時系列順に正規化した会話メッセージ
    /// </summary>
    public List<ConversationMessage> Messages { get; init; } = [];
}

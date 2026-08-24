namespace AILogExport.Conversations;

/// <summary>
/// 対話選択画面でセッションを識別するための軽量な情報を表す。
/// </summary>
public sealed class ConversationInfo
{
    /// <summary>
    /// JSONLファイルの絶対パス
    /// </summary>
    public required string FilePath { get; init; }

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
    /// 最初に記録された実ユーザーの発言
    /// </summary>
    public string? FirstUserMessage { get; init; }

    /// <summary>
    /// セッションを実行したプロジェクトのパス
    /// </summary>
    public string? ProjectPath { get; init; }

    /// <summary>
    /// ログから取得したセッション開始日時
    /// </summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>
    /// ログ内の最終イベント日時。取得できない場合はファイル更新日時を保持する。
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}

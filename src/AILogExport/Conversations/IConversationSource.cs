namespace AILogExport.Conversations;

/// <summary>
/// 入力元固有のログ探索と共通会話モデルへの変換を定義する。
/// </summary>
public interface IConversationSource
{
    /// <summary>
    /// コマンドラインで使用する入力元識別子
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 対話画面とMarkdownで使用する表示名
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 指定された探索ルート、または入力元の既定ルートから会話を検索する。
    /// </summary>
    IReadOnlyList<ConversationInfo> FindConversations(string? sourceDirectory = null);

    /// <summary>
    /// JSONLを走査し、一覧表示に必要な情報を取得する。
    /// </summary>
    ConversationInfo ReadConversationInfo(string path);

    /// <summary>
    /// JSONLを解析して共通会話モデルへ変換する。
    /// </summary>
    Conversation ReadConversation(string path);
}

using AILogExport.Conversations;

namespace AILogExport.Interactive;

/// <summary>
/// 対話モードで必要な選択、入力、結果表示を抽象化する。
/// </summary>
public interface IInteractiveConsole
{
    IConversationSource SelectSource(IReadOnlyList<IConversationSource> sources);

    ConversationInfo SelectConversation(
        IConversationSource source,
        IReadOnlyList<ConversationInfo> conversations);

    string AskOutputPath(string defaultPath);

    bool ConfirmOverwrite(string outputPath);

    void WriteSuccess(string outputPath);

    void WriteInformation(string message);

    void WriteError(string message);
}

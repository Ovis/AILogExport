using AILogExport.Conversations;

namespace AILogExport.Sources.Codex;

/// <summary>
/// Codexログの探索と解析をまとめて提供する。
/// </summary>
public sealed class CodexConversationSource : IConversationSource
{
    private readonly CodexLogParser parser;

    public CodexConversationSource(CodexLogParser? parser = null)
    {
        this.parser = parser ?? new CodexLogParser();
    }

    public string Id => "codex";

    public string DisplayName => "Codex";

    public IReadOnlyList<ConversationInfo> FindConversations(string? sourceDirectory = null)
    {
        var directory = Path.GetFullPath(sourceDirectory ?? GetDefaultSourceDirectory());
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Codexのログディレクトリが見つからない: {directory}");
        }

        return Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.AllDirectories)
            .Select(ReadConversationInfo)
            .OrderByDescending(info => info.UpdatedAt)
            .ThenBy(info => info.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ConversationInfo ReadConversationInfo(string path)
    {
        var conversation = ReadConversation(path);
        return new ConversationInfo
        {
            FilePath = Path.GetFullPath(path),
            Id = conversation.Id,
            Source = conversation.Source,
            Title = conversation.Title,
            FirstUserMessage = conversation.Messages
                .FirstOrDefault(message => message.Role == ConversationRole.User)?.Content,
            ProjectPath = conversation.ProjectPath,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt,
        };
    }

    public Conversation ReadConversation(string path) => parser.Parse(path);

    /// <summary>
    /// Codexが使用する既定のセッションログディレクトリを返す。
    /// </summary>
    public static string GetDefaultSourceDirectory()
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(codexHome))
        {
            codexHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex");
        }

        return Path.Combine(codexHome, "sessions");
    }
}

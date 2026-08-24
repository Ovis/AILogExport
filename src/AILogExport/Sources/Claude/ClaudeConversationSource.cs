using AILogExport.Conversations;

namespace AILogExport.Sources.Claude;

/// <summary>
/// Claude Codeログの探索と解析をまとめて提供する。
/// </summary>
public sealed class ClaudeConversationSource : IConversationSource
{
    private readonly ClaudeLogParser parser;

    public ClaudeConversationSource(ClaudeLogParser? parser = null)
    {
        this.parser = parser ?? new ClaudeLogParser();
    }

    public string Id => "claude";

    public string DisplayName => "Claude Code";

    public IReadOnlyList<ConversationInfo> FindConversations(string? sourceDirectory = null)
    {
        var directory = Path.GetFullPath(sourceDirectory ?? GetDefaultSourceDirectory());
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Claude Codeのログディレクトリが見つからない: {directory}");
        }

        return Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.AllDirectories)
            .Where(path => !IsSubagentLog(path))
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

    public Conversation ReadConversation(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (IsSubagentLog(fullPath))
        {
            throw new NotSupportedException("Claude CodeのサブエージェントログはVersion 1.0の対象外である。");
        }

        return parser.Parse(fullPath);
    }

    /// <summary>
    /// Claude Codeが使用する既定のプロジェクトログディレクトリを返す。
    /// </summary>
    public static string GetDefaultSourceDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude",
        "projects");

    private static bool IsSubagentLog(string path)
    {
        var segments = Path.GetFullPath(path)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment => segment.Equals("subagents", StringComparison.OrdinalIgnoreCase));
    }
}

using System.Text;
using System.Text.Json;
using AILogExport.Conversations;
using AILogExport.Parsing;

namespace AILogExport.Sources.Codex;

/// <summary>
/// Codexのrollout JSONLを共通会話モデルへ変換する。
/// </summary>
public sealed class CodexLogParser
{
    private static readonly string[] InternalUserMessagePrefixes =
    [
        "<recommended_plugins>",
        "# AGENTS.md instructions",
        "# CLAUDE.md instructions",
        "<environment_context>",
        "<permissions instructions>",
        "<skills_instructions>",
        "<app-context>",
        "<collaboration_mode>",
        "<apps_instructions>",
        "<plugins_instructions>",
        "<memory>",
    ];

    /// <summary>
    /// 指定したCodexログを一行ずつ読み、会話を復元する。
    /// </summary>
    public Conversation Parse(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Codexのログファイルが見つからない。", fullPath);
        }

        var state = new ParseState(Path.GetFileNameWithoutExtension(fullPath));
        var lineNumber = 0;

        foreach (var line in File.ReadLines(fullPath))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                ParseEntry(document.RootElement, state);
            }
            catch (JsonException exception)
            {
                throw new JsonlParseException(fullPath, lineNumber, exception);
            }
        }

        state.FlushAssistant();
        var fileTimestamp = new DateTimeOffset(File.GetLastWriteTimeUtc(fullPath), TimeSpan.Zero);

        return new Conversation
        {
            Id = state.SessionId,
            Source = ConversationSourceType.Codex,
            ProjectPath = state.ProjectPath,
            CreatedAt = state.CreatedAt,
            UpdatedAt = state.UpdatedAt ?? fileTimestamp,
            Messages = state.Messages,
        };
    }

    private static void ParseEntry(JsonElement root, ParseState state)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var timestamp = ReadTimestamp(root, "timestamp");
        state.RecordTimestamp(timestamp);

        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var recordType = ReadString(root, "type");
        if (recordType == "session_meta")
        {
            state.SessionId = ReadString(payload, "id")
                ?? ReadString(payload, "session_id")
                ?? state.SessionId;
            state.ProjectPath = ReadString(payload, "cwd") ?? state.ProjectPath;
            state.RecordTimestamp(ReadTimestamp(payload, "timestamp"));
            return;
        }

        if (recordType == "turn_context")
        {
            state.ProjectPath ??= ReadString(payload, "cwd");
            return;
        }

        if (recordType != "response_item" || ReadString(payload, "type") != "message")
        {
            return;
        }

        var role = ReadString(payload, "role");
        if (role is not ("user" or "assistant") ||
            !payload.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        if (role == "user")
        {
            var userText = JoinContentBlocks(content, "input_text", excludeInternalUserText: true);
            if (string.IsNullOrWhiteSpace(userText))
            {
                return;
            }

            state.FlushAssistant();
            state.Messages.Add(new ConversationMessage
            {
                Role = ConversationRole.User,
                Content = userText,
                Timestamp = timestamp,
            });
            return;
        }

        var assistantText = JoinContentBlocks(content, "output_text", excludeInternalUserText: false);
        if (!string.IsNullOrWhiteSpace(assistantText))
        {
            state.AppendAssistant(assistantText, timestamp);
        }
    }

    private static string? JoinContentBlocks(
        JsonElement content,
        string expectedType,
        bool excludeInternalUserText)
    {
        var parts = new List<string>();

        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                ReadString(item, "type") != expectedType)
            {
                continue;
            }

            var text = ReadString(item, "text");
            if (string.IsNullOrWhiteSpace(text) ||
                (excludeInternalUserText && IsInternalUserText(text)))
            {
                continue;
            }

            parts.Add(text);
        }

        return parts.Count == 0 ? null : string.Join("\n\n", parts);
    }

    private static bool IsInternalUserText(string text)
    {
        var trimmed = text.TrimStart();
        return InternalUserMessagePrefixes.Any(
            prefix => trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static DateTimeOffset? ReadTimestamp(JsonElement element, string propertyName)
    {
        var value = ReadString(element, propertyName);
        return DateTimeOffset.TryParse(value, out var timestamp) ? timestamp : null;
    }

    private sealed class ParseState(string fallbackSessionId)
    {
        private readonly StringBuilder assistant = new();
        private DateTimeOffset? assistantTimestamp;

        public string SessionId { get; set; } = fallbackSessionId;

        public string? ProjectPath { get; set; }

        public DateTimeOffset? CreatedAt { get; private set; }

        public DateTimeOffset? UpdatedAt { get; private set; }

        public List<ConversationMessage> Messages { get; } = [];

        public void RecordTimestamp(DateTimeOffset? timestamp)
        {
            if (timestamp is null)
            {
                return;
            }

            CreatedAt = CreatedAt is null || timestamp < CreatedAt ? timestamp : CreatedAt;
            UpdatedAt = UpdatedAt is null || timestamp > UpdatedAt ? timestamp : UpdatedAt;
        }

        public void AppendAssistant(string content, DateTimeOffset? timestamp)
        {
            if (assistant.Length > 0)
            {
                assistant.Append("\n\n");
            }

            assistant.Append(content);
            assistantTimestamp ??= timestamp;
        }

        public void FlushAssistant()
        {
            if (assistant.Length == 0)
            {
                return;
            }

            Messages.Add(new ConversationMessage
            {
                Role = ConversationRole.Assistant,
                Content = assistant.ToString(),
                Timestamp = assistantTimestamp,
            });

            assistant.Clear();
            assistantTimestamp = null;
        }
    }
}

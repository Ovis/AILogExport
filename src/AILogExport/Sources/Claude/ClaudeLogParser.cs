using System.Text;
using System.Text.Json;
using AILogExport.Conversations;
using AILogExport.Parsing;

namespace AILogExport.Sources.Claude;

/// <summary>
/// Claude CodeのJSONLを共通会話モデルへ変換する。
/// </summary>
public sealed class ClaudeLogParser
{
    private static readonly string[] InternalUserMessagePrefixes =
    [
        "<command-name>",
        "<command-message>",
        "<command-args>",
        "<local-command-stdout>",
        "<local-command-stderr>",
        "<bash-input>",
        "<bash-stdout>",
        "<bash-stderr>",
        "[Request interrupted",
    ];

    /// <summary>
    /// 指定したClaude Codeログを一行ずつ読み、会話を復元する。
    /// </summary>
    public Conversation Parse(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Claude Codeのログファイルが見つからない。", fullPath);
        }

        var state = new ParseState(Path.GetFileNameWithoutExtension(fullPath));
        var lineNumber = 0;

        foreach (var line in JsonlFile.ReadLines(fullPath))
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
            Source = ConversationSourceType.Claude,
            Title = state.Title,
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

        state.SessionId = ReadString(root, "sessionId")
            ?? ReadString(root, "session_id")
            ?? state.SessionId;
        state.ProjectPath ??= ReadString(root, "cwd");

        var timestamp = ReadTimestamp(root, "timestamp");
        state.RecordTimestamp(timestamp);

        var type = ReadString(root, "type");
        if (type == "ai-title")
        {
            state.Title = ReadString(root, "aiTitle") ?? state.Title;
            return;
        }

        if (IsTrue(root, "isMeta") || IsTrue(root, "isSidechain"))
        {
            return;
        }

        if (type is not ("user" or "assistant") ||
            !root.TryGetProperty("message", out var message) ||
            message.ValueKind != JsonValueKind.Object ||
            !message.TryGetProperty("content", out var content))
        {
            return;
        }

        if (type == "user")
        {
            var userText = ExtractUserText(content);
            if (!IsActualUserMessage(userText))
            {
                return;
            }

            state.FlushAssistant();
            state.Messages.Add(new ConversationMessage
            {
                Role = ConversationRole.User,
                Content = userText!,
                Timestamp = timestamp,
            });
            return;
        }

        var assistantText = ExtractAssistantText(content);
        if (!string.IsNullOrWhiteSpace(assistantText) &&
            !assistantText.TrimStart().StartsWith("API Error:", StringComparison.OrdinalIgnoreCase))
        {
            state.AppendAssistant(assistantText, timestamp);
        }
    }

    private static string? ExtractUserText(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString();
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        // Tool Resultもuserロールで記録されるため、textブロックだけを明示的に抽出する。
        return JoinTextBlocks(content, expectedType: "text");
    }

    private static string? ExtractAssistantText(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString();
        }

        return content.ValueKind == JsonValueKind.Array
            ? JoinTextBlocks(content, expectedType: "text")
            : null;
    }

    private static string? JoinTextBlocks(JsonElement content, string expectedType)
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
            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add(text);
            }
        }

        return parts.Count == 0 ? null : string.Join("\n\n", parts);
    }

    private static bool IsActualUserMessage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.TrimStart();
        return !InternalUserMessagePrefixes.Any(
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

    private static bool IsTrue(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.True;

    private sealed class ParseState(string fallbackSessionId)
    {
        private readonly StringBuilder assistant = new();
        private DateTimeOffset? assistantTimestamp;

        public string SessionId { get; set; } = fallbackSessionId;

        public string? Title { get; set; }

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

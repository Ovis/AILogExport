using AILogExport.Conversations;
using AILogExport.Parsing;
using AILogExport.Sources.Codex;

namespace AILogExport.Tests.Sources.Codex;

[TestFixture]
public sealed class CodexLogParserTests
{
    [Test]
    public void Parse_実ユーザーとAssistant本文を抽出して内部イベントを除外する()
    {
        using var directory = new TestDirectory();
        var path = directory.WriteJsonl(
            """{"timestamp":"2026-08-24T01:00:00Z","type":"session_meta","payload":{"id":"session-1","cwd":"D:\\Develop\\Foo","timestamp":"2026-08-24T01:00:00Z"}}""",
            """{"timestamp":"2026-08-24T01:00:01Z","type":"response_item","payload":{"type":"message","role":"developer","content":[{"type":"input_text","text":"system instructions"}]}}""",
            """{"timestamp":"2026-08-24T01:00:02Z","type":"response_item","payload":{"type":"message","role":"user","content":[{"type":"input_text","text":"<recommended_plugins>internal</recommended_plugins>"},{"type":"input_text","text":"質問です。"}]}}""",
            """{"timestamp":"2026-08-24T01:00:03Z","type":"response_item","payload":{"type":"message","role":"assistant","phase":"commentary","content":[{"type":"output_text","text":"確認します。"}]}}""",
            """{"timestamp":"2026-08-24T01:00:04Z","type":"response_item","payload":{"type":"custom_tool_call","name":"exec_command"}}""",
            """{"timestamp":"2026-08-24T01:00:05Z","type":"response_item","payload":{"type":"reasoning","summary":[]}}""",
            """{"timestamp":"2026-08-24T01:00:06Z","type":"response_item","payload":{"type":"message","role":"assistant","phase":"final_answer","content":[{"type":"output_text","text":"回答です。"}]}}""",
            """{"timestamp":"2026-08-24T01:00:07Z","type":"response_item","payload":{"type":"message","role":"user","content":[{"type":"input_text","text":"続けて。"}]}}""",
            """{"timestamp":"2026-08-24T01:00:08Z","type":"event_msg","payload":{"type":"task_complete","last_agent_message":"duplicate"}}""");

        var conversation = new CodexLogParser().Parse(path);

        Assert.Multiple(() =>
        {
            Assert.That(conversation.Id, Is.EqualTo("session-1"));
            Assert.That(conversation.Source, Is.EqualTo(ConversationSourceType.Codex));
            Assert.That(conversation.ProjectPath, Is.EqualTo(@"D:\Develop\Foo"));
            Assert.That(conversation.CreatedAt, Is.EqualTo(DateTimeOffset.Parse("2026-08-24T01:00:00Z")));
            Assert.That(conversation.UpdatedAt, Is.EqualTo(DateTimeOffset.Parse("2026-08-24T01:00:08Z")));
            Assert.That(conversation.Messages.Select(message => message.Role), Is.EqualTo(new[]
            {
                ConversationRole.User,
                ConversationRole.Assistant,
                ConversationRole.User,
            }));
            Assert.That(conversation.Messages[0].Content, Is.EqualTo("質問です。"));
            Assert.That(conversation.Messages[1].Content, Is.EqualTo("確認します。\n\n回答です。"));
            Assert.That(conversation.Messages[2].Content, Is.EqualTo("続けて。"));
            Assert.That(conversation.Messages.Select(message => message.Content), Does.Not.Contain("duplicate"));
        });
    }

    [TestCase("# AGENTS.md instructions\ninternal")]
    [TestCase("<environment_context>internal</environment_context>")]
    [TestCase("<permissions instructions>internal</permissions instructions>")]
    public void Parse_Codexが注入したUserコンテキストを除外する(string content)
    {
        using var directory = new TestDirectory();
        var logEntry = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "response_item",
            payload = new
            {
                type = "message",
                role = "user",
                content = new[] { new { type = "input_text", text = content } },
            },
        });
        var path = directory.WriteJsonl(logEntry);

        var conversation = new CodexLogParser().Parse(path);

        Assert.That(conversation.Messages, Is.Empty);
    }

    [Test]
    public void Parse_TurnContextからProjectPathを補完する()
    {
        using var directory = new TestDirectory();
        var path = directory.WriteJsonl(
            """{"type":"turn_context","payload":{"cwd":"D:\\Develop\\FromTurn"}}""");

        var conversation = new CodexLogParser().Parse(path);

        Assert.That(conversation.ProjectPath, Is.EqualTo(@"D:\Develop\FromTurn"));
    }

    [Test]
    public void Parse_不正なJSONではファイルと行番号を含む例外を返す()
    {
        using var directory = new TestDirectory();
        var path = directory.WriteJsonl(
            """{"type":"session_meta","payload":{}}""",
            "not-json");

        var exception = Assert.Throws<JsonlParseException>(() => new CodexLogParser().Parse(path));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.FilePath, Is.EqualTo(Path.GetFullPath(path)));
            Assert.That(exception.LineNumber, Is.EqualTo(2));
            Assert.That(exception.InnerException, Is.InstanceOf<System.Text.Json.JsonException>());
        });
    }

    [Test]
    public void Parse_日時がない場合はファイル更新日時をUpdatedAtに使用する()
    {
        using var directory = new TestDirectory();
        var path = directory.WriteJsonl(
            """{"type":"response_item","payload":{"type":"message","role":"user","content":[{"type":"input_text","text":"質問"}]}}""");
        var expected = new DateTime(2026, 8, 24, 3, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, expected);

        var conversation = new CodexLogParser().Parse(path);

        Assert.That(conversation.UpdatedAt, Is.EqualTo(new DateTimeOffset(expected)));
    }
}

using AILogExport.Conversations;
using AILogExport.Parsing;
using AILogExport.Sources.Claude;

namespace AILogExport.Tests.Sources.Claude;

[TestFixture]
public sealed class ClaudeLogParserTests
{
    [Test]
    public void Parse_実ユーザーとAssistant本文を抽出してTool実行越しに連結する()
    {
        using var directory = new TestDirectory();
        var path = directory.WriteJsonl(
            """{"type":"user","sessionId":"session-1","cwd":"D:\\Develop\\Foo","timestamp":"2026-08-24T01:00:00+09:00","message":{"role":"user","content":"調べて。"}}""",
            """{"type":"assistant","sessionId":"session-1","timestamp":"2026-08-24T01:01:00+09:00","message":{"role":"assistant","content":[{"type":"text","text":"確認します。"},{"type":"tool_use","name":"Read"}]}}""",
            """{"type":"user","sessionId":"session-1","timestamp":"2026-08-24T01:02:00+09:00","message":{"role":"user","content":[{"type":"tool_result","content":"secret"}]}}""",
            """{"type":"assistant","sessionId":"session-1","timestamp":"2026-08-24T01:03:00+09:00","message":{"role":"assistant","content":[{"type":"thinking","thinking":"internal"},{"type":"text","text":"原因が分かりました。"}]}}""",
            """{"type":"user","sessionId":"session-1","timestamp":"2026-08-24T01:04:00+09:00","message":{"role":"user","content":"修正して。"}}""",
            """{"type":"assistant","sessionId":"session-1","timestamp":"2026-08-24T01:05:00+09:00","message":{"role":"assistant","content":[{"type":"text","text":"修正しました。"}]}}""",
            """{"type":"ai-title","sessionId":"session-1","aiTitle":"不具合修正"}""");

        var conversation = new ClaudeLogParser().Parse(path);

        Assert.Multiple(() =>
        {
            Assert.That(conversation.Id, Is.EqualTo("session-1"));
            Assert.That(conversation.Source, Is.EqualTo(ConversationSourceType.Claude));
            Assert.That(conversation.Title, Is.EqualTo("不具合修正"));
            Assert.That(conversation.ProjectPath, Is.EqualTo(@"D:\Develop\Foo"));
            Assert.That(conversation.CreatedAt, Is.EqualTo(DateTimeOffset.Parse("2026-08-24T01:00:00+09:00")));
            Assert.That(conversation.UpdatedAt, Is.EqualTo(DateTimeOffset.Parse("2026-08-24T01:05:00+09:00")));
            Assert.That(conversation.Messages.Select(message => message.Role), Is.EqualTo(new[]
            {
                ConversationRole.User,
                ConversationRole.Assistant,
                ConversationRole.User,
                ConversationRole.Assistant,
            }));
            Assert.That(conversation.Messages[1].Content, Is.EqualTo("確認します。\n\n原因が分かりました。"));
            Assert.That(conversation.Messages[3].Content, Is.EqualTo("修正しました。"));
        });
    }

    [Test]
    public void Parse_text配列のUser発言を抽出する()
    {
        using var directory = new TestDirectory();
        var path = directory.WriteJsonl(
            """{"type":"user","message":{"role":"user","content":[{"type":"text","text":"一つ目"},{"type":"text","text":"二つ目"}]}}""");

        var conversation = new ClaudeLogParser().Parse(path);

        Assert.That(conversation.Messages.Single().Content, Is.EqualTo("一つ目\n\n二つ目"));
    }

    [TestCase("<command-name>/clear</command-name>")]
    [TestCase("<local-command-stdout>done</local-command-stdout>")]
    [TestCase("[Request interrupted by user]")]
    public void Parse_Claude内部のUserメッセージを除外する(string content)
    {
        using var directory = new TestDirectory();
        var logEntry = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "user",
            message = new { role = "user", content },
        });
        var path = directory.WriteJsonl(logEntry);

        var conversation = new ClaudeLogParser().Parse(path);

        Assert.That(conversation.Messages, Is.Empty);
    }

    [Test]
    public void Parse_MetaとSidechainとAPIエラーを除外する()
    {
        using var directory = new TestDirectory();
        var path = directory.WriteJsonl(
            """{"type":"user","isMeta":true,"message":{"role":"user","content":"meta"}}""",
            """{"type":"user","isSidechain":true,"message":{"role":"user","content":"sidechain"}}""",
            """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"API Error: unavailable"}]}}""");

        var conversation = new ClaudeLogParser().Parse(path);

        Assert.That(conversation.Messages, Is.Empty);
    }

    [Test]
    public void Parse_不正なJSONではファイルと行番号を含む例外を返す()
    {
        using var directory = new TestDirectory();
        var path = directory.WriteJsonl(
            """{"type":"system"}""",
            "not-json");

        var exception = Assert.Throws<JsonlParseException>(() => new ClaudeLogParser().Parse(path));

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
            """{"type":"user","message":{"role":"user","content":"質問"}}""");
        var expected = new DateTime(2026, 8, 24, 3, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, expected);

        var conversation = new ClaudeLogParser().Parse(path);

        Assert.That(conversation.UpdatedAt, Is.EqualTo(new DateTimeOffset(expected)));
    }
}

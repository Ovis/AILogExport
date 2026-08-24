using AILogExport.Sources.Claude;

namespace AILogExport.Tests.Sources.Claude;

[TestFixture]
public sealed class ClaudeConversationSourceTests
{
    [Test]
    public void FindConversations_Subagentsを除外して更新日時の降順に並べる()
    {
        using var directory = new TestDirectory();
        directory.WriteJsonlAt(
            Path.Combine("project", "old.jsonl"),
            """{"type":"user","sessionId":"old","timestamp":"2026-08-23T10:00:00Z","message":{"content":"古い会話"}}""");
        directory.WriteJsonlAt(
            Path.Combine("project", "new.jsonl"),
            """{"type":"user","sessionId":"new","timestamp":"2026-08-24T10:00:00Z","message":{"content":"新しい会話"}}""");
        directory.WriteJsonlAt(
            Path.Combine("project", "session", "subagents", "agent-1.jsonl"),
            """{"type":"user","sessionId":"agent","timestamp":"2026-08-25T10:00:00Z","message":{"content":"サブエージェント"}}""");

        var conversations = new ClaudeConversationSource().FindConversations(directory.Path);

        Assert.Multiple(() =>
        {
            Assert.That(conversations.Select(info => info.Id), Is.EqualTo(new[] { "new", "old" }));
            Assert.That(conversations[0].FirstUserMessage, Is.EqualTo("新しい会話"));
        });
    }

    [Test]
    public void FindConversations_探索ルートがない場合は例外にする()
    {
        using var directory = new TestDirectory();
        var missingPath = Path.Combine(directory.Path, "missing");

        var exception = Assert.Throws<DirectoryNotFoundException>(
            () => new ClaudeConversationSource().FindConversations(missingPath));

        Assert.That(exception!.Message, Does.Contain(Path.GetFullPath(missingPath)));
    }

    [Test]
    public void ReadConversation_Subagentsの直接指定を拒否する()
    {
        using var directory = new TestDirectory();
        var path = directory.WriteJsonlAt(
            Path.Combine("session", "subagents", "agent-1.jsonl"),
            """{"type":"user","message":{"content":"サブエージェント"}}""");

        Assert.Throws<NotSupportedException>(
            () => new ClaudeConversationSource().ReadConversation(path));
    }
}

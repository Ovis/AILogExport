using AILogExport.Sources.Codex;

namespace AILogExport.Tests.Sources.Codex;

[TestFixture]
public sealed class CodexConversationSourceTests
{
    [Test]
    public void FindConversations_再帰検索して更新日時の降順に並べる()
    {
        using var directory = new TestDirectory();
        directory.WriteJsonlAt(
            Path.Combine("2026", "08", "23", "old.jsonl"),
            """{"timestamp":"2026-08-23T10:00:00Z","type":"session_meta","payload":{"id":"old"}}""",
            """{"type":"response_item","payload":{"type":"message","role":"user","content":[{"type":"input_text","text":"古い会話"}]}}""");
        directory.WriteJsonlAt(
            Path.Combine("2026", "08", "24", "new.jsonl"),
            """{"timestamp":"2026-08-24T10:00:00Z","type":"session_meta","payload":{"id":"new"}}""",
            """{"type":"response_item","payload":{"type":"message","role":"user","content":[{"type":"input_text","text":"新しい会話"}]}}""");

        var conversations = new CodexConversationSource().FindConversations(directory.Path);

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
            () => new CodexConversationSource().FindConversations(missingPath));

        Assert.That(exception!.Message, Does.Contain(Path.GetFullPath(missingPath)));
    }

    [Test]
    [NonParallelizable]
    public void GetDefaultSourceDirectory_CODEX_HOME配下のSessionsを返す()
    {
        var original = Environment.GetEnvironmentVariable("CODEX_HOME");
        try
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", @"D:\CodexHome");

            var directory = CodexConversationSource.GetDefaultSourceDirectory();

            Assert.That(directory, Is.EqualTo(@"D:\CodexHome\sessions"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", original);
        }
    }
}

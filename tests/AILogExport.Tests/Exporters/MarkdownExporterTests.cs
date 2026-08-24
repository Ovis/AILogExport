using System.Text;
using AILogExport.Conversations;
using AILogExport.Exporters;

namespace AILogExport.Tests.Exporters;

[TestFixture]
public sealed class MarkdownExporterTests
{
    [Test]
    public void Export_会話をヘッダーと発言を含むMarkdownへ変換する()
    {
        var conversation = new Conversation
        {
            Id = "session-1",
            Source = ConversationSourceType.Claude,
            ProjectPath = @"D:\Develop\Foo",
            Messages =
            [
                new ConversationMessage
                {
                    Role = ConversationRole.User,
                    Content = "処理を修正して。",
                },
                new ConversationMessage
                {
                    Role = ConversationRole.Assistant,
                    Content = "## 修正内容\r\n\r\n```csharp\r\nreturn true;\r\n```",
                },
            ],
        };

        var markdown = new MarkdownExporter().Export(conversation);

        Assert.That(markdown, Is.EqualTo(
            "# AI Conversation\n\n" +
            "**Source:** Claude Code  \n" +
            "**Project:** `D:\\Develop\\Foo`  \n" +
            "**Session:** `session-1`\n\n" +
            "---\n\n" +
            "## User\n\n" +
            "処理を修正して。\n\n" +
            "## Assistant\n\n" +
            "## 修正内容\n\n" +
            "```csharp\n" +
            "return true;\n" +
            "```\n"));
    }

    [Test]
    public void Export_SystemとToolと空メッセージを出力しない()
    {
        var conversation = CreateConversation(
            new ConversationMessage { Role = ConversationRole.System, Content = "system prompt" },
            new ConversationMessage { Role = ConversationRole.Tool, Content = "tool result" },
            new ConversationMessage { Role = ConversationRole.User, Content = "   " },
            new ConversationMessage { Role = ConversationRole.Assistant, Content = "回答" });

        var markdown = new MarkdownExporter().Export(conversation);

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Not.Contain("system prompt"));
            Assert.That(markdown, Does.Not.Contain("tool result"));
            Assert.That(markdown, Does.Not.Contain("## User"));
            Assert.That(markdown, Does.Contain("## Assistant\n\n回答"));
        });
    }

    [Test]
    public void Export_ProjectPathがない場合はProject行を出力しない()
    {
        var conversation = CreateConversation();

        var markdown = new MarkdownExporter().Export(conversation);

        Assert.That(markdown, Does.Not.Contain("**Project:**"));
    }

    [Test]
    public void Export_Codexの表示名を出力する()
    {
        var conversation = new Conversation
        {
            Id = "session-1",
            Source = ConversationSourceType.Codex,
        };

        var markdown = new MarkdownExporter().Export(conversation);

        Assert.That(markdown, Does.Contain("**Source:** Codex"));
    }

    [Test]
    public void Write_UTF8BomなしLF改行で保存する()
    {
        using var directory = new TestDirectory();
        var outputPath = Path.Combine(directory.Path, "conversation.md");
        var conversation = CreateConversation(
            new ConversationMessage
            {
                Role = ConversationRole.User,
                Content = "一行目\r\n二行目",
            });

        new MarkdownExporter().Write(conversation, outputPath);

        var bytes = File.ReadAllBytes(outputPath);
        var content = Encoding.UTF8.GetString(bytes);
        var preamble = Encoding.UTF8.GetPreamble();
        var hasBom = bytes.AsSpan().StartsWith(preamble);

        Assert.Multiple(() =>
        {
            Assert.That(hasBom, Is.False);
            Assert.That(content, Does.Not.Contain("\r"));
            Assert.That(content, Does.Contain("一行目\n二行目"));
        });
    }

    private static Conversation CreateConversation(params ConversationMessage[] messages) => new()
    {
        Id = "session-1",
        Source = ConversationSourceType.Claude,
        Messages = [.. messages],
    };
}

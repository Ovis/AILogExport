using AILogExport.Commands;
using AILogExport.Conversations;
using AILogExport.Exporters;
using AILogExport.Interactive;

namespace AILogExport.Tests.Commands;

[TestFixture]
public sealed class ExportApplicationTests
{
    [Test]
    public void Run_直接指定時にMarkdownを既定パスへ出力する()
    {
        using var directory = new TestDirectory();
        var inputPath = directory.WriteJsonl("{}");
        var source = new StubConversationSource("claude", inputPath);
        var console = new StubInteractiveConsole();
        var application = CreateApplication(source, console);

        var exitCode = application.Run("claude", inputPath, null, force: false, sourceDirectory: null);

        var outputPath = Path.ChangeExtension(inputPath, ".md");
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(File.Exists(outputPath), Is.True);
            Assert.That(File.ReadAllText(outputPath), Does.Contain("## User\n\n質問"));
            Assert.That(console.SuccessPath, Is.EqualTo(outputPath));
        });
    }

    [Test]
    public void Run_非対話モードで出力が存在する場合はエラーにする()
    {
        using var directory = new TestDirectory();
        var inputPath = directory.WriteJsonl("{}");
        var outputPath = Path.Combine(directory.Path, "existing.md");
        File.WriteAllText(outputPath, "before");
        var console = new StubInteractiveConsole();
        var application = CreateApplication(new StubConversationSource("claude", inputPath), console);

        var exitCode = application.Run("claude", inputPath, outputPath, force: false, sourceDirectory: null);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(File.ReadAllText(outputPath), Is.EqualTo("before"));
            Assert.That(console.ErrorMessage, Does.Contain("--force"));
        });
    }

    [Test]
    public void Run_Force指定時は既存ファイルを上書きする()
    {
        using var directory = new TestDirectory();
        var inputPath = directory.WriteJsonl("{}");
        var outputPath = Path.Combine(directory.Path, "existing.md");
        File.WriteAllText(outputPath, "before");
        var application = CreateApplication(
            new StubConversationSource("claude", inputPath),
            new StubInteractiveConsole());

        var exitCode = application.Run("claude", inputPath, outputPath, force: true, sourceDirectory: null);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(File.ReadAllText(outputPath), Does.Contain("# AI Conversation"));
        });
    }

    [Test]
    public void Run_完全対話モードでは入力元とセッションと出力先を選択する()
    {
        using var directory = new TestDirectory();
        var inputPath = directory.WriteJsonl("{}");
        var outputPath = Path.Combine(directory.Path, "selected.md");
        var source = new StubConversationSource("codex", inputPath);
        var console = new StubInteractiveConsole
        {
            SourceToSelect = source,
            OutputPathToReturn = outputPath,
        };
        var application = CreateApplication(source, console);

        var exitCode = application.Run(null, null, null, force: false, sourceDirectory: directory.Path);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(source.FindSourceDirectory, Is.EqualTo(directory.Path));
            Assert.That(console.SelectedConversationCount, Is.EqualTo(1));
            Assert.That(File.Exists(outputPath), Is.True);
        });
    }

    [Test]
    public void Run_対話モードで上書きを拒否した場合は変更しない()
    {
        using var directory = new TestDirectory();
        var inputPath = directory.WriteJsonl("{}");
        var outputPath = Path.Combine(directory.Path, "existing.md");
        File.WriteAllText(outputPath, "before");
        var source = new StubConversationSource("claude", inputPath);
        var console = new StubInteractiveConsole
        {
            OutputPathToReturn = outputPath,
            ConfirmOverwriteResult = false,
        };
        var application = CreateApplication(source, console);

        var exitCode = application.Run("claude", null, null, force: false, sourceDirectory: null);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(File.ReadAllText(outputPath), Is.EqualTo("before"));
            Assert.That(console.InformationMessage, Is.EqualTo("出力をキャンセルした。"));
        });
    }

    [Test]
    public void Run_未対応の入力元ではエラーにする()
    {
        using var directory = new TestDirectory();
        var inputPath = directory.WriteJsonl("{}");
        var console = new StubInteractiveConsole();
        var application = CreateApplication(
            new StubConversationSource("claude", inputPath),
            console);

        var exitCode = application.Run("unknown", inputPath, null, force: false, sourceDirectory: null);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(console.ErrorMessage, Does.Contain("未対応の入力元"));
        });
    }

    [Test]
    public void Run_セッションがない場合はエラーにする()
    {
        var source = new StubConversationSource("claude", inputPath: null);
        var console = new StubInteractiveConsole();
        var application = CreateApplication(source, console);

        var exitCode = application.Run("claude", null, null, force: false, sourceDirectory: null);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(console.ErrorMessage, Does.Contain("セッションログが見つからない"));
        });
    }

    private static ExportApplication CreateApplication(
        IConversationSource source,
        IInteractiveConsole console) => new(
            [source],
            new MarkdownExporter(),
            console);

    private sealed class StubConversationSource(string id, string? inputPath) : IConversationSource
    {
        public string Id => id;

        public string DisplayName => id;

        public string? FindSourceDirectory { get; private set; }

        public IReadOnlyList<ConversationInfo> FindConversations(string? sourceDirectory = null)
        {
            FindSourceDirectory = sourceDirectory;
            return inputPath is null
                ? []
                :
                [
                    new ConversationInfo
                    {
                        FilePath = inputPath,
                        Id = "session-1",
                        Source = ConversationSourceType.Claude,
                        FirstUserMessage = "質問",
                    },
                ];
        }

        public ConversationInfo ReadConversationInfo(string path) =>
            throw new NotSupportedException();

        public Conversation ReadConversation(string path) => new()
        {
            Id = "session-1",
            Source = id == "codex" ? ConversationSourceType.Codex : ConversationSourceType.Claude,
            Messages =
            [
                new ConversationMessage
                {
                    Role = ConversationRole.User,
                    Content = "質問",
                },
            ],
        };
    }

    private sealed class StubInteractiveConsole : IInteractiveConsole
    {
        public IConversationSource? SourceToSelect { get; init; }

        public string? OutputPathToReturn { get; init; }

        public bool ConfirmOverwriteResult { get; init; }

        public int SelectedConversationCount { get; private set; }

        public string? SuccessPath { get; private set; }

        public string? InformationMessage { get; private set; }

        public string? ErrorMessage { get; private set; }

        public IConversationSource SelectSource(IReadOnlyList<IConversationSource> sources) =>
            SourceToSelect ?? sources[0];

        public ConversationInfo SelectConversation(
            IConversationSource source,
            IReadOnlyList<ConversationInfo> conversations)
        {
            SelectedConversationCount++;
            return conversations[0];
        }

        public string AskOutputPath(string defaultPath) => OutputPathToReturn ?? defaultPath;

        public bool ConfirmOverwrite(string outputPath) => ConfirmOverwriteResult;

        public void WriteSuccess(string outputPath) => SuccessPath = outputPath;

        public void WriteInformation(string message) => InformationMessage = message;

        public void WriteError(string message) => ErrorMessage = message;
    }
}

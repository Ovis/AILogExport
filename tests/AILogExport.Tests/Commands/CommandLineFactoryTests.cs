using AILogExport.Commands;

namespace AILogExport.Tests.Commands;

[TestFixture]
public sealed class CommandLineFactoryTests
{
    [Test]
    public void Invoke_引数とオプションをアプリケーションへ渡す()
    {
        var application = new RecordingExportApplication { ExitCode = 7 };
        var command = CommandLineFactory.Create(application);

        var exitCode = command.Parse(
        [
            "codex",
            "input.jsonl",
            "--output",
            "output.md",
            "--force",
            "--source-dir",
            "logs",
        ]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(7));
            Assert.That(application.SourceId, Is.EqualTo("codex"));
            Assert.That(application.InputPath, Is.EqualTo("input.jsonl"));
            Assert.That(application.OutputPath, Is.EqualTo("output.md"));
            Assert.That(application.Force, Is.True);
            Assert.That(application.SourceDirectory, Is.EqualTo("logs"));
        });
    }

    [Test]
    public void Invoke_引数省略時はnullをアプリケーションへ渡す()
    {
        var application = new RecordingExportApplication();
        var command = CommandLineFactory.Create(application);

        var exitCode = command.Parse([]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(application.SourceId, Is.Null);
            Assert.That(application.InputPath, Is.Null);
            Assert.That(application.OutputPath, Is.Null);
            Assert.That(application.Force, Is.False);
            Assert.That(application.SourceDirectory, Is.Null);
        });
    }

    private sealed class RecordingExportApplication : IExportApplication
    {
        public int ExitCode { get; init; }

        public string? SourceId { get; private set; }

        public string? InputPath { get; private set; }

        public string? OutputPath { get; private set; }

        public bool Force { get; private set; }

        public string? SourceDirectory { get; private set; }

        public int Run(
            string? sourceId,
            string? inputPath,
            string? outputPath,
            bool force,
            string? sourceDirectory)
        {
            SourceId = sourceId;
            InputPath = inputPath;
            OutputPath = outputPath;
            Force = force;
            SourceDirectory = sourceDirectory;
            return ExitCode;
        }
    }
}

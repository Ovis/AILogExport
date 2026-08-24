using AILogExport.Commands;
using AILogExport.Conversations;
using AILogExport.Exporters;
using AILogExport.Interactive;
using AILogExport.Sources.Claude;
using AILogExport.Sources.Codex;

IConversationSource[] sources =
[
    new ClaudeConversationSource(),
    new CodexConversationSource(),
];

var interactiveConsole = new SpectreInteractiveConsole();
var application = new ExportApplication(sources, new MarkdownExporter(), interactiveConsole);
var rootCommand = CommandLineFactory.Create(application);

return rootCommand.Parse(args).Invoke();

# AILogExport

Claude CodeおよびCodexのローカルセッションログを、人間が読み返しやすいMarkdownへ変換する.NET CLIツールです。

実ユーザーの発言とAssistantの人間向け本文を抽出し、System／Developer、Thinking／Reasoning、Tool Call／Tool Resultなどの内部情報を除外します。

## 必要な環境

- .NET 10 SDK

## 実行方法

JSONLを直接指定して変換します。

```powershell
dotnet run --project src/AILogExport -- claude "C:\Users\...\session.jsonl" -o "D:\session.md"
dotnet run --project src/AILogExport -- codex "C:\Users\...\rollout.jsonl" -o "D:\session.md"
```

`-o` を省略した場合は、入力JSONLと同じディレクトリに同名のMarkdownを出力します。出力先が存在する場合、直接指定モードではエラーになります。上書きするには `--force` または `-f` を指定します。

入力元だけを指定すると、その入力元のセッションを一覧から選択できます。

```powershell
dotnet run --project src/AILogExport -- claude
dotnet run --project src/AILogExport -- codex
```

引数をすべて省略すると、Claude CodeまたはCodexの選択から開始します。

```powershell
dotnet run --project src/AILogExport
```

ログ探索ルートを変更する場合は `--source-dir` を使用します。

```powershell
dotnet run --project src/AILogExport -- claude --source-dir "D:\ClaudeLogs"
```

## 既定のログ探索先

- Claude Code: `%USERPROFILE%\.claude\projects`
- Codex: `%CODEX_HOME%\sessions`
- `CODEX_HOME` 未設定時のCodex: `%USERPROFILE%\.codex\sessions`

Claude Codeの `subagents` 配下はVersion 1.0の対象外です。

## ビルドとテスト

```powershell
dotnet build AILogExport.sln -m:1
dotnet test AILogExport.sln -m:1
```

詳細な仕様は[docs/specification.md](docs/specification.md)を参照してください。

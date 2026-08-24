# AILogExport Version 1.0 設計仕様

## 1. 目的

Claude CodeおよびCodexがローカルに保存するJSONL形式のセッションログを読み込み、ユーザーとAIの会話を人間が読み返しやすいMarkdownとして保存する。

ログ形式ごとの差異は入力元ごとのParserに閉じ込め、一度共通会話モデルへ正規化してからMarkdownを生成する。

```text
Claude JSONL -> ClaudeLogParser --+
                                  +-> Conversation -> MarkdownExporter -> Markdown
Codex JSONL  -> CodexLogParser  ---+
```

## 2. 対象環境と依存関係

- .NET 10
- C#
- Console Application
- `System.Text.Json`
- `System.CommandLine`
- `Spectre.Console`
- NUnit

Markdown変換ライブラリは使用しない。Assistantの本文に含まれるMarkdownは、可能な限り加工せず保持する。

## 3. CLI

基本形式は次のとおりとする。

```text
AILogExport [source] [input] [options]
```

`source` は `claude` または `codex` とする。

### 3.1 直接指定モード

```powershell
AILogExport claude "C:\Users\...\session.jsonl" -o "D:\session.md"
AILogExport codex "C:\Users\...\rollout.jsonl" -o "D:\session.md"
```

`-o` を省略した場合は、入力ファイルと同じディレクトリに拡張子だけを `.md` に変更して出力する。

### 3.2 入力元別の対話モード

```powershell
AILogExport claude
AILogExport codex
```

指定された入力元のセッションを検索し、一覧から選択した後に出力先を入力する。

### 3.3 完全対話モード

```powershell
AILogExport
```

最初にClaude CodeまたはCodexを選択し、その後は入力元別の対話モードと同じ処理を行う。

### 3.4 オプション

| オプション | 短縮 | 内容 |
|---|---|---|
| `--output` | `-o` | Markdown出力先 |
| `--force` | `-f` | 既存出力ファイルの上書きを許可 |
| `--source-dir` | なし | 選択した入力元のログ探索ルートを上書き |

## 4. ログ探索

### 4.1 Claude Code

既定では `%USERPROFILE%\.claude\projects` 以下の `*.jsonl` を再帰検索する。

`<session-id>\subagents` 配下に保存されるサブエージェントログは、Version 1.0の対象外として探索結果から除外する。直接指定モードでもサブエージェントログは入力として受け付けない。

### 4.2 Codex

`CODEX_HOME` 環境変数が設定されている場合はその値、未設定の場合は `%USERPROFILE%\.codex` をホームディレクトリとし、その配下の `sessions` から `*.jsonl` を再帰検索する。

Codexの年月別ディレクトリやファイル名規則には依存しない。

### 4.3 一覧表示

一覧には可能な範囲で次の情報を使用する。

- 最終更新日時
- プロジェクト名またはプロジェクトパス
- 明示的なタイトル
- 最初の実ユーザーメッセージ

明示的なタイトルがない場合は、最初の実ユーザーメッセージをタイトル相当として使用する。一覧上の長い文字列は省略するが、Markdown本文は省略しない。

並び順は最終更新日時の降順とする。最終更新日時はログ内の最後のイベント日時を優先し、取得できない場合はJSONLファイルの最終更新日時を使用する。

## 5. 共通会話モデル

共通モデルは少なくとも次の情報を保持する。

```csharp
public sealed class Conversation
{
    public required string Id { get; init; }
    public required ConversationSourceType Source { get; init; }
    public string? Title { get; init; }
    public string? ProjectPath { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public List<ConversationMessage> Messages { get; init; } = [];
}
```

```csharp
public sealed class ConversationMessage
{
    public required ConversationRole Role { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
}
```

通常のMarkdown出力では `User` と `Assistant` のみを使用する。

## 6. ログ解析

JSONLは `File.ReadLines` または `StreamReader` により一行ずつ解析する。ファイル全体を一括でメモリへ読み込まない。

Version 1.0でMarkdownへ出力する内容は次のものに限定する。

- 人間が入力した実際のUser発言
- Assistantの人間向け本文

次の情報は通常出力から除外する。

- System Prompt
- System MessageおよびDeveloper Message
- ThinkingおよびReasoning
- Tool CallおよびTool Result
- Function CallおよびFunction Result
- Token usage
- Session metadata
- Compaction情報
- API内部イベント
- 空メッセージ

ログ上のロールだけで実ユーザー発言を判定しない。Claude CodeではTool Resultが `user` として記録されることがあり、Codexでも内部コンテキストがメッセージとして記録され得るため、各Parserで構造を確認して判別する。

Assistantの応答がTool実行を挟んで複数のイベントに分割されている場合、実ユーザーの次の発言までを一つのAssistant応答として連結する。空のメッセージは生成しない。

不正なJSONを検出した場合は、入力ファイルと行番号を含む解析エラーとして処理を中断する。

## 7. Markdown出力

基本形式は次のとおりとする。

````markdown
# AI Conversation

**Source:** Claude Code  
**Project:** `D:\Develop\Foo`  
**Session:** `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`

---

## User

この処理を修正して。

## Assistant

確認しました。
````

出力エンコーディングはUTF-8 BOMなし、改行コードはLFとする。

出力先が既に存在する場合の動作は次のとおりとする。

- 対話モードでは上書きを確認する
- 非対話モードではエラーにする
- `--force` 指定時は上書きする

## 8. Version 1.0の対象外

- ChatGPT会話履歴
- MCP
- GUIおよびWeb UI
- HTMLおよびPDF出力
- ThinkingおよびReasoning出力
- Tool CallおよびTool Result出力
- 全文検索
- 複数セッションの一括変換
- Claude Codeのサブエージェントログ
- ログ編集
- Claude CodeまたはCodexへの再インポート

## 9. 実装時に実データで確認する事項

- Claude Codeにおける実ユーザー発言、Tool Result、meta messageの判別条件
- Claude Codeのタイトル、セッションID、プロジェクトパス、日時の取得元
- Codexにおける `session_meta` と `response_item` の現行構造
- Codexの実ユーザー発言と内部注入メッセージの判別条件
- Assistant応答を連結する境界
- 入力元ごとのログ形式変更に対する許容範囲

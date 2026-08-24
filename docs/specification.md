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

## 9. 実ログで確認した構造

2026-08-24時点のローカル実ログで、次の構造を確認した。

### 9.1 Claude Code

- 実ユーザー発言は主にトップレベル `type=user`、`message.content` の文字列として記録される
- Tool Resultも `type=user` だが、`message.content` が配列で `type=tool_result` のブロックとして記録される
- Assistant本文は `type=assistant`、`message.content` 配列内の `type=text` から取得できる
- `thinking` と `tool_use` はAssistantの別ブロックとして識別できる
- セッションIDは `sessionId` または `session_id`、プロジェクトパスは `cwd`、タイトルは `type=ai-title` の `aiTitle` から取得できる
- `isMeta=true`、`isSidechain=true`、内部コマンド表現は通常会話から除外する
- `<session-id>\subagents` 配下にはサブエージェント用JSONLが存在する

### 9.2 Codex

- セッション情報はトップレベル `type=session_meta` の `payload` に記録される
- セッションIDは `payload.id` または `payload.session_id`、プロジェクトパスは `payload.cwd` から取得できる
- 会話はトップレベル `type=response_item`、`payload.type=message` として記録される
- User本文は `role=user` の `input_text`、Assistant本文は `role=assistant` の `output_text` から取得できる
- `role=developer`、`payload.type=reasoning`、Tool Call／Result、`event_msg` は通常会話から除外する
- 推奨プラグイン、`AGENTS.md`、実行環境などが `role=user` の内部コンテキストとして記録される場合があるため、既知の内部ブロックを除外する

### 9.3 共通の実装条件

- Tool実行を挟んだAssistant本文は、次の実ユーザー発言まで連結する
- AIクライアントが追記中のJSONLも読めるよう、ファイルを共有読み取りで開く
- ログ形式は外部ツールの更新により変わり得るため、Parserを入力元ごとに分離し、合成JSONLを使った回帰テストで判別条件を固定する

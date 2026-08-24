# AILogExport リリース手順

## 1. リリース成果物

AILogExportは次の.NET Toolパッケージとして公開する。

| 項目 | 値 |
|---|---|
| Package ID | `eSheepDev.AILogExport` |
| Tool command | `ailogexport` |
| Target framework | `net10.0` |
| NuGet source | `https://api.nuget.org/v3/index.json` |
| GitHub repository | `Ovis/AILogExport` |

プロジェクトファイルの `Version` はローカルpack用の既定値である。正式リリース時のPackageVersionはGitタグから決定し、GitHub ActionsからMSBuildプロパティとして渡す。

## 2. 初回リリース前のnuget.org設定

公開ワークフローはNuGet Trusted Publishingを使用する。長期間有効なNuGet APIキーをGitHub Secretsへ保存しない。

nuget.orgへユーザー `Ovis` でログインし、Trusted Publishingポリシーを次の値で登録する。

| 設定項目 | 値 |
|---|---|
| Policy owner | `Ovis` |
| Repository owner | `Ovis` |
| Repository | `AILogExport` |
| Workflow file | `publish.yml` |
| Environment | 空欄 |

Workflow fileには `.github/workflows/` を付けず、ファイル名だけを指定する。

ポリシーが一時的な有効状態として作成された場合は、有効期間内に最初の公開を成功させる。最初の公開によってGitHubのRepository IDとOwner IDが確認されると、ポリシーは恒久的に有効化される。

## 3. ローカルでのパッケージ検証

正式なタグを作る前に、ローカル用のプレリリースバージョンでpackする。

```powershell
dotnet build AILogExport.slnx -c Release -m:1
dotnet test AILogExport.slnx -c Release --no-build --no-restore -m:1

dotnet pack src/AILogExport/AILogExport.csproj `
  -c Release `
  --no-build `
  --no-restore `
  -p:PackageVersion=0.1.0-local.1 `
  -o artifacts/packages
```

生成したnupkgをグローバル環境へ入れず、一時的なToolパスへインストールして起動確認する。

```powershell
dotnet tool install `
  --tool-path artifacts/tool-test `
  --add-source artifacts/packages `
  eSheepDev.AILogExport `
  --version 0.1.0-local.1 `
  --no-cache `
  --ignore-failed-sources

artifacts\tool-test\ailogexport.exe --help
```

`artifacts` はGit管理対象外である。確認後はディレクトリごと削除してよい。

## 4. 正式リリース

タグは必ず、パッケージ設定と `.github/workflows/publish.yml` を含み、Releaseビルドとテストが成功しているコミットへ付ける。

安定版 `0.1.0` を公開する例を示す。

```powershell
git push origin main
git tag -a v0.1.0 -m "AILogExport v0.1.0"
git push origin v0.1.0
```

`v0.1.0` のpushによって、GitHub Actionsは次の順で処理する。

1. タグから `0.1.0` を取得して形式を検証する
2. .NET 10 SDKをセットアップする
3. RestoreとReleaseビルドを行う
4. NUnitテストを実行する
5. `eSheepDev.AILogExport.0.1.0.nupkg` を作成する
6. GitHub OIDCトークンをNuGetの短期APIキーへ交換する
7. nupkgをnuget.orgへpushする
8. nupkgを添付したGitHub Releaseを作成する

プレリリース版は次の形式とする。

```powershell
git tag -a v0.2.0-beta.1 -m "AILogExport v0.2.0-beta.1"
git push origin v0.2.0-beta.1
```

ハイフンを含むバージョンは、NuGetとGitHub Releaseの両方でプレリリースとして扱う。

## 5. 公開後の確認

nuget.orgへの反映後、別の一時ToolパスまたはグローバルToolとしてインストールする。

```powershell
dotnet tool install --global eSheepDev.AILogExport --version 0.1.0
ailogexport --help
```

既にインストール済みの場合は更新する。

```powershell
dotnet tool update --global eSheepDev.AILogExport --version 0.1.0
```

## 6. リリース失敗時の扱い

- NuGetへのpush前に失敗した場合は、原因を修正して新しいコミットへ新しいバージョンタグを付ける
- 同じコミットとタグで再試行可能な一時障害の場合は、GitHub Actions画面から失敗したWorkflowを再実行する
- NuGetへ公開済みのバージョンは上書きできないため、公開後の修正には新しいバージョンを使用する
- Workflowは `--skip-duplicate` を指定しているため、NuGetへのpush成功後にGitHub Release作成だけが失敗した場合も再実行できる
- 公開済みタグを別コミットへ付け替えない

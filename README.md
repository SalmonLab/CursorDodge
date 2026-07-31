# CursorDodge

CursorDodge は、テキスト入力時にカーソル位置が邪魔になるのを避けるため、クリック直後にカーソルを指定方向へずらす Windows 向けトレイ常駐ユーティリティです。

## 主な機能

- 低レベルフックでマウスクリック後の入力を監視
- 設定可能なカーソル回避挙動
  - 移動量（px）
  - 方向（上方向=0°, 右方向=90°）
  - フレームレート
  - 移動時間（ms）
  - クリック後の反応待機時間（ms）
  - 誤発火抑止用の最小入力文字数
- タスクトレイ常駐
- 自動起動設定（起動時にトレイへ登録）
- コンソール表示なし（WinExe）

## GitHub から最低限の実行環境を取得（推奨）

このアプリは `self-contained` で公開できます。つまり、実行時にローカル環境へ .NET SDK/Runtime を別途インストールする必要がありません。

### 1) GitHub Releases から取得

- リリースページ: https://github.com/SalmonLab/CursorDodge/releases/latest
- 配布物が用意されている場合、`CursorDodge-Portable.zip` または `CursorDodge.exe` をダウンロードしてください。

### 2) PowerShell で自動取得（GitHub API 利用）

```powershell
.\scripts\get-cursordodge-standalone.ps1 -TargetDir "$env:USERPROFILE\CursorDodge"
```

- `-TargetDir` 省略時は `scripts\..\portable` へ保存されます。
- 取得後、以下を実行して起動できます。

```powershell
Start-Process "$env:USERPROFILE\CursorDodge\CursorDodge.exe"
```

必要なら起動まで一度で済ませるには:

```powershell
.\scripts\get-cursordodge-standalone.ps1 -TargetDir "$env:USERPROFILE\CursorDodge" -RunAfterDownload
```

## 開発者向けビルド

```powershell
dotnet build
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

既定の出力先:

- `bin/Release/net8.0-windows/win-x64/publish/CursorDodge.exe`

## 設定

設定は次の項目を保存ファイルから管理します。

- 保存場所: `%AppData%\CursorDodge\settings.json`
- トレイメニュー: `設定`

## 注意

- 自動起動やトレイ常駐の設定を変更する場合は、実行中のアプリ側で行ってください。
- 本アプリは Windows 上での低レベルフックを使用します。必要に応じてテスト環境で挙動確認してください。

# NetSpeedBar

Windows 11 向けの、通信影響を最小化するネットワーク品質モニターの初期実装です。開発は `feature/network-probe` ブランチで開始しており、`main` には変更していません。

## 現在実装済み

- HTTP 往復時間（ICMP Ping や TCP 接続時間とは区別）を測定する低負荷品質プローブ
- 小容量・時間・並列数・最短再測定間隔を上限にするダウンロード推定
- 省通信／バランス／診断の各プロファイルと、省通信モード既定
- 他通信量、指定アプリ実行、HTTP 往復時間上昇時に自動推定を抑止する判定
- HTTP 往復時間の履歴、ジッター、成功率、失敗時の指数バックオフ
- 上限付きの手動ダウンロード／アップロード測定とキャンセル
- システムトレイ常駐、多重起動防止、Windows ログイン時起動（既定 OFF）
- JSON 設定ストアと、測定モード・通信量しきい値・抑止プロセスを変更できる WPF 設定画面
- Windows x64 自己完結型の publish 構成

## Cloudflare Speed Test

Cloudflare の公式 `cloudflare/speedtest` リポジトリ（MIT License）で、同社の測定アプリが利用する公開エンドポイントとして `https://speed.cloudflare.com/__down` と `__up` が文書化されています。本実装は品質確認に `__down?bytes=0`、軽量ダウンロード推定に `__down?bytes=<予算>` を使います。自動スループット測定は既定で無効、手動測定も 2 MB／3 秒までです。

同リポジトリは、測定完了時に接続品質の集計的な洞察のため結果が Cloudflare に収集される旨を記載しています。アプリの利用者にこの点を表示する UI は今後の設定画面で追加します。Cloudflare の Web サイト利用規約の現行本文は、本クラウド環境のネットワーク設定がまだ反映されていないため、この作業時点では直接取得・最終確認できていません。公開・反映後に改めて確認してください。

軽量測定の数値は常に「軽量推定」であり、回線の最大速度ではありません。

## ビルド

```powershell
dotnet build NetSpeedBar.sln --configuration Release
dotnet publish src/NetSpeedBar.App/NetSpeedBar.App.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/win-x64
```

コア検証は外部通信なしの実行可能なテストプログラムとして配置しています。

```powershell
dotnet run --project tests/NetSpeedBar.Core.Tests/NetSpeedBar.Core.Tests.csproj --configuration Release
```

## 未実装・実機検証待ち

タスクバー内部を改変するオーバーレイ、Explorer 再起動／スリープ復帰の実機検証、全画面アプリの検出、複数接続による最大帯域測定、グラフ表示は未実装です。Windows 11 実機が必要な長時間負荷、VPN、ゲーム、ネットワーク切替の検証も未実施です。

# NetSpeedBar

Windows 11 向けの、通信影響を最小化するネットワーク品質モニターの初期実装です。開発は `feature/network-probe` ブランチで開始しており、`main` には変更していません。

## 現在実装済み

- HTTP 往復時間（ICMP Ping や TCP 接続時間とは区別）を測定する低負荷品質プローブ
- 小容量・時間・並列数・最短再測定間隔を上限にするダウンロード推定
- 省通信／バランス／診断の各プロファイルと、省通信モード既定
- 他通信量、指定アプリ実行、HTTP 往復時間上昇時に自動推定を抑止する判定
- JSON 設定ストア、Windows 向け WPF の最小操作画面
- Windows x64 自己完結型の publish 構成

外部測定先は初期状態では未設定です。許可条件を確認した測定サーバーを利用者が設定するまで、アプリは測定目的の外部通信を行いません。軽量測定の数値は常に「軽量推定」であり、回線の最大速度ではありません。

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

システムトレイ、タスクバー表示オーバーレイ、Explorer 再起動／スリープ復帰、Windows のアダプター統計・全画面検出、アップロード／並列フルスピード測定、履歴グラフは未実装です。Windows 11 実機が必要な長時間負荷、VPN、ゲーム、ネットワーク切替の検証も未実施です。

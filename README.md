# Supermarket Simulator - Unlimited Money Mod

Supermarket Simulator 用の非公式 BepInEx 6 (IL2CPP) チートプラグインです。

## 機能

### 常時有効

- **無限マネー** -- 支出がブロックされ、所持金が常に 99,999,999 に維持
- **レベル100** -- ゲーム読み込み時に自動でストアレベルを100に設定
- **従業員ブースト常時MAX** -- 全従業員 (レジ係/品出し/ヘルパー/アイスクリーム/パン屋/清掃員) のブーストが常に最大
- **従業員スキャン高速化** -- レジ・ヘルパーのスキャン速度を大幅向上
- **品出し高速化** -- 品出し間隔を 0.05秒 に短縮
- **全従業員雇用解放** -- ストアレベル・顧客目標に関係なく全従業員が雇用可能
- **従業員ダブル生成** -- 雇用した品出し係1人につきクローンを自動生成し、実質2倍の人員

### トグル操作

| キー | 機能 | デフォルト |
|------|------|-----------|
| F1 | 自動発注 (在庫不足の商品を自動注文) | OFF |
| F2 | 自動価格設定 + 自動家具購入 | OFF |
| F3 | 自動棚補充 | OFF |
| F4 | 新商品の自動ラベリング | OFF |

## 技術スタック

- C# / .NET 6.0
- [BepInEx 6](https://github.com/BepInEx/BepInEx) (Unity IL2CPP)
- [HarmonyX](https://github.com/BepInEx/HarmonyX) によるランタイムパッチ
- [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop) による IL2CPP ランタイム操作
- 遅延実行キュー (DeferredQueue) によるコレクション操作の安全な処理
- NavMeshAgent 制御による従業員クローンの動的生成

## ビルド

```bash
dotnet build ModTools/UnlimitedMoneyMod/UnlimitedMoneyMod.csproj -c Release
```

出力先: `ModTools/UnlimitedMoneyMod/bin/Release/net6.0/UnlimitedMoneyMod.dll`

## インストール

1. [BepInEx 6 (IL2CPP)](https://github.com/BepInEx/BepInEx/releases) をゲームディレクトリに導入
2. 一度ゲームを起動して `BepInEx/interop/` を生成させる
3. ビルドした `UnlimitedMoneyMod.dll` を `BepInEx/plugins/` に配置
4. ゲームを起動

## ダウンロード

[Releases](../../releases) からビルド済み DLL をダウンロードできます。

## 免責事項

本プロジェクトは非公式のファンメイドMODであり、ゲーム開発元とは一切関係ありません。自己責任でご利用ください。

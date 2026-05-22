# BodyAdjust Matcher

ボーンスケール/移動/MA Scale Adjusterの改変を服に転送するツール

不要なoverrideは省くのが特徴です（不完全）

## Install

### VCC用インストーラーunitypackageによる方法（おすすめ）

https://github.com/Narazaka/BodyAdjustMatcher/releases/latest から `net.narazaka.vrchat.body-adjust-matcher-installer.zip` をダウンロードして解凍し、対象のプロジェクトにインポートする。

### VCCによる方法

1. https://vpm.narazaka.net/ から「Add to VCC」ボタンを押してリポジトリをVCCにインストールします。
2. VCCでSettings→Packages→Installed Repositoriesの一覧中で「Narazaka VPM Listing」にチェックが付いていることを確認します。
3. アバタープロジェクトの「Manage Project」から「BodyAdjust Matcher」をインストールします。

## Usage

アバターにMAで着せた服のArmatureを右クリックして、「Adjust Body Match (on Cloth Armature)」を実行する。

## 更新履歴

- v0.2.0: MA Merge Armatureが付いていればその情報を利用するように
- v0.1.1: 複数Armatureを一括で処理できるように / アバターのアーマチュア名"armature"も許可
- v0.1.0: リリース

## License

[Zlib License](LICENSE.txt)

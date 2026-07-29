# VRChatTools

VRChatアバター改変用のUnityエディタ拡張集。

## インストール

### VCC / ALCOM（推奨）

以下のリンクからリポジトリを追加してください。

[Add to VCC](https://kama-boko-vrc.github.io/VRChatTools/)

または、VCC/ALCOMの「Settings > Packages > Add Repository」から以下のURLを直接追加することもできます。

```
https://kama-boko-vrc.github.io/VRChatTools/index.json
```

追加後、プロジェクトの「Manage Project」画面から `VRChatTools` パッケージを追加すると、以下の全ツールがまとめて導入されます。

### 手動導入

VCC/ALCOMを使わない場合は、各ツールの `.cs` ファイルを直接Unityプロジェクトの `Assets` フォルダ配下（`Editor` フォルダ推奨）にコピーしても利用できます（`#if UNITY_EDITOR` で囲んであるためビルドには含まれません）。

## ArmatureScaleCopier

同一構造のArmature間で、各ボーンの `localScale` と `MA Scale Adjuster`（型名に "ScaleAdjuster" を含むコンポーネント）をコピーするエディタ拡張です。

- A: コピー元（アバター本体のArmature）
- B: コピー先（衣装・髪など、一部ボーンが欠損している可能性があるArmature）

### 使い方

1. メニューから `Tools > VRChatTools > Armature Scale Copier` を開く
2. A（コピー元）とB（コピー先）にそれぞれのArmatureのTransformをドラッグ&ドロップ
3. 「コピー実行」を押すと、ボーン名が一致する箇所を再帰的にたどりながら `localScale` と `MA Scale Adjuster` をコピー
   - Bに存在しないボーンはスキップされ、実行後にログで件数が表示されます

## ScaleResetter

指定したTransformとその配下すべての `localScale` を `(1, 1, 1)` にリセットするエディタ拡張です。

### 使い方

1. メニューから `Tools > VRChatTools > Scale Resetter` を開く
2. 「対象」にリセットしたい階層のルートTransformをドラッグ&ドロップ
3. 「スケールを1.0にリセット」を押すと、対象とその配下すべてのTransformの `localScale` が `(1,1,1)` にリセットされます

## ArmatureComponentCleaner

指定したTransformとその配下すべてから、Transform以外の全コンポーネントを再帰的に削除するエディタ拡張です。

### 使い方

1. メニューから `Tools > VRChatTools > Armature Component Cleaner` を開く
2. 「対象」に処理したい階層のルートTransformをドラッグ&ドロップ
3. 「Transform以外を削除」を押すと、対象とその配下すべてのGameObjectからTransform以外のコンポーネントが再帰的に削除されます（Undo対応、Ctrl+Zで復元可）

## LilToonPropertyCopier

1つのマテリアル（コピー元）から、選択したシェーダープロパティのみを複数のマテリアル（コピー先）へ一括コピーするエディタ拡張です。lilToonに限らず、コピー元・コピー先が同じシェーダーであれば利用できます。

### 使い方

1. メニューから `Tools > VRChatTools > LilToon Property Copier` を開く
2. 「コピー元」にマテリアルを1つ設定
3. 「+ コピー先を追加」でコピー先のマテリアルを複数登録
4. コピー元のシェーダープロパティ一覧がチェックボックスで表示されるので、コピーしたい項目を選択（全選択/全解除ボタンあり）
5. 「コピー実行」を押すと、選択したプロパティのみが全コピー先マテリアルへ一括反映されます（Undo対応）

## WriteDefaultsBatchSetter

アバターのルートを指定するだけで、使用している全Animator Controller（VRCAvatarDescriptorの各Playable Layer、配下のAnimatorコンポーネント）の全State（サブステートマシン含む）についてWrite Defaultsの現在値を集計し、一括でON/OFFに変更するエディタ拡張です。ON/OFFが混在している場合は警告表示されます。

### 使い方

1. メニューから `Tools > VRChatTools > Write Defaults Batch Setter` を開く
2. 「アバタールート」にアバターのルートを設定
3. 「スキャン」を押すと、Controllerごと・合計のState数とON/OFF件数が表示されます
4. 「全StateをONにする」/「全StateをOFFにする」で、全Controllerの全Stateに一括反映されます（Undo対応）

## AvatarMenuOrganizer

アバターのルートを指定するだけで、Expressions Menu（ラジアルメニュー、サブメニュー含む）と、使用している全Animator Controller（VRCAvatarDescriptorの各Playable Layer、配下のAnimatorコンポーネント）のレイヤー・パラメータをまとめて一覧表示するエディタ拡張です。全メニューコントロール・全レイヤー・全パラメータにチェックボックスがあり、選択したものを一括削除できます。

メニューコントロールのチェックをONにすると、そのコントロールが参照するパラメータ（parameter/subParameters）のチェックも自動でONになるため、ラジアルメニューの項目とそれが使っていたパラメータをまとめて削除できます（パラメータ側のチェックは手動で解除も可能です）。

「未使用」表示の判定対象（あくまで目安表示で、チェックの有効/無効には影響しません）:

- メニューコントロール: 参照パラメータがVRCExpressionParameters・Animator Controllerのどちらにも見つからない、またはSubMenu参照先が空/欠落している
- レイヤー: Stateを1つも持たない
- パラメータ: 通常のTransition / Any State Transition / Entry Transitionの条件（parameter）、StateのMotion Time / Speed / Cycle Offset / Mirrorパラメータ、BlendTree（Direct含む）のBlend Parameter、Expressions Menuのparameter/subParametersのいずれからも参照されていない

VRCSDKへの直接参照は持たせず、SerializedObject経由でVRCAvatarDescriptor/Expressions Menuのフィールドを読みます。

### 使い方

1. メニューから `Tools > VRChatTools > Avatar Menu Organizer` を開く
2. 「アバタールート」にアバターのルートを設定
3. 「スキャン」を押すと、Expressions Menuのツリーと、全Animator Controllerのレイヤー・パラメータがチェックリスト形式で表示されます（デフォルト全解除、それぞれに全選択/全解除ボタンあり）
4. 「選択したメニュー・レイヤー・パラメータを削除」で、選択した項目のみ一括削除されます（Undo対応）

## QuickPrefabPlacer

複数のプレハブを登録しておき、ヒエラルキーの右クリックメニュー（GameObjectメニュー）から一覧選択でワンクリック配置できるエディタ拡張です。登録内容はEditorPrefsに保存され、プロジェクト内で永続化されます（マシン/ユーザーごと）。

### 使い方

1. メニューから `Tools > VRChatTools > Quick Prefab Placer` を開く
2. 「+ プレハブを追加」で配置したいプレハブを登録（複数可、`-`で削除）
3. ヒエラルキーを右クリック（またはGameObjectメニュー）→「クイックプレハブを配置」にカーソルを合わせると、登録済みプレハブ名のサブメニューが表示されます
4. サブメニューから選ぶと、選択中のオブジェクトの子として配置されます（未選択時はシーン直下、Undo対応）
   - 配置先に同名オブジェクトが既にある場合は、Unityの重複名規則に倣い `(1)`, `(2)`... を付けて自動的に名前が重複しないようにします

## 免責事項

本ツールの使用によって生じたアバターデータの破損・消失、意図しない改変、その他いかなる損害についても、作者は一切の責任を負いません。すべて自己責任でご利用ください。特にレイヤー・パラメータ・メニューの削除やWrite Defaultsの一括変更など、元に戻しづらい操作を行うツールについては、事前にプロジェクトのバックアップを取ってから使用することを強く推奨します。

## License

[MIT License](LICENSE)

# VRChatTools

VRChatアバター制作用のUnityエディタ拡張集。

## ArmatureScaleCopier

同一構造のArmature間で、各ボーンの `localScale` と `MA Scale Adjuster`（型名に "ScaleAdjuster" を含むコンポーネント）をコピーするエディタ拡張です。

- A: コピー元（アバター本体のArmature）
- B: コピー先（衣装・髪など、一部ボーンが欠損している可能性があるArmature）

VCC対応は行っておらず、`.cs` ファイルを直接プロジェクトの `Assets` フォルダ配下に配置して使う想定です（`#if UNITY_EDITOR` で囲んであるためビルドには含まれません）。

### 導入方法

[ArmatureScaleCopier/ArmatureScaleCopier.cs](ArmatureScaleCopier/ArmatureScaleCopier.cs) をUnityプロジェクトの `Assets` フォルダ配下（`Editor` フォルダ推奨）にコピーします。

### 使い方

1. メニューから `Tools > VRChatTools > Armature Scale Copier` を開く
2. A（コピー元）とB（コピー先）にそれぞれのArmatureのTransformをドラッグ&ドロップ
3. 「コピー実行」を押すと、ボーン名が一致する箇所を再帰的にたどりながら `localScale` と `MA Scale Adjuster` をコピー
   - Bに存在しないボーンはスキップされ、実行後にログで件数が表示されます

## ScaleResetter

指定したTransformとその配下すべての `localScale` を `(1, 1, 1)` にリセットするエディタ拡張です。

### 導入方法

[ScaleResetter/ScaleResetter.cs](ScaleResetter/ScaleResetter.cs) をUnityプロジェクトの `Assets` フォルダ配下（`Editor` フォルダ推奨）にコピーします。

### 使い方

1. メニューから `Tools > VRChatTools > Scale Resetter` を開く
2. 「対象」にリセットしたい階層のルートTransformをドラッグ&ドロップ
3. 「スケールを1.0にリセット」を押すと、対象とその配下すべてのTransformの `localScale` が `(1,1,1)` にリセットされます

## ArmatureComponentCleaner

指定したTransformとその配下すべてから、Transform以外の全コンポーネントを再帰的に削除するエディタ拡張です。

### 導入方法

[ArmatureComponentCleaner/ArmatureComponentCleaner.cs](ArmatureComponentCleaner/ArmatureComponentCleaner.cs) をUnityプロジェクトの `Assets` フォルダ配下（`Editor` フォルダ推奨）にコピーします。

### 使い方

1. メニューから `Tools > VRChatTools > Armature Component Cleaner` を開く
2. 「対象」に処理したい階層のルートTransformをドラッグ&ドロップ
3. 「Transform以外を削除」を押すと、対象とその配下すべてのGameObjectからTransform以外のコンポーネントが再帰的に削除されます（Undo対応、Ctrl+Zで復元可）

## LilToonPropertyCopier

1つのマテリアル（コピー元）から、選択したシェーダープロパティのみを複数のマテリアル（コピー先）へ一括コピーするエディタ拡張です。lilToonに限らず、コピー元・コピー先が同じシェーダーであれば利用できます。

### 導入方法

[LilToonPropertyCopier/LilToonPropertyCopier.cs](LilToonPropertyCopier/LilToonPropertyCopier.cs) をUnityプロジェクトの `Assets` フォルダ配下（`Editor` フォルダ推奨）にコピーします。

### 使い方

1. メニューから `Tools > VRChatTools > LilToon Property Copier` を開く
2. 「コピー元」にマテリアルを1つ設定
3. 「+ コピー先を追加」でコピー先のマテリアルを複数登録
4. コピー元のシェーダープロパティ一覧がチェックボックスで表示されるので、コピーしたい項目を選択（全選択/全解除ボタンあり）
5. 「コピー実行」を押すと、選択したプロパティのみが全コピー先マテリアルへ一括反映されます（Undo対応）

## FXLayerOrganizer

アバターのルートを指定するだけで、そのアバターが使っている全Animator Controller（VRCAvatarDescriptorの各Playable Layer、配下のAnimatorコンポーネント）を自動収集し、レイヤー・パラメータを一覧表示するエディタ拡張です。全レイヤー・全パラメータにチェックボックスがあり、選択したものをControllerを横断して一括削除できます。

Animator Controllerの収集元:

- VRCAvatarDescriptorの `baseAnimationLayers`（Base/Additive/Gesture/Action/FX）/ `specialAnimationLayers`（Sit/TPose/IKPose）（SerializedObject経由で読むためVRCSDK未導入でもコンパイル可能）
- 配下のAnimatorコンポーネントが参照しているController

「未使用」表示の判定対象（あくまで目安表示で、チェックの有効/無効には影響しません）:

- パラメータ: 通常のTransition / Any State Transition / Entry Transitionの条件（parameter）、StateのMotion Time / Speed / Cycle Offset / Mirrorパラメータ、BlendTree（Direct含む）のBlend Parameter、Expressions Menu（ラジアルメニュー、サブメニュー含む）のparameter/subParametersのいずれからも参照されていないもの
- レイヤー: Stateを1つも持たないもの

### 導入方法

[FXLayerOrganizer/FXLayerOrganizer.cs](FXLayerOrganizer/FXLayerOrganizer.cs) をUnityプロジェクトの `Assets` フォルダ配下（`Editor` フォルダ推奨）にコピーします。

### 使い方

1. メニューから `Tools > VRChatTools > FX Layer Organizer` を開く
2. 「アバタールート」にアバターのルートを設定
3. 「スキャン」を押すと、見つかった全Animator Controllerについてレイヤー一覧とパラメータ一覧がチェックリスト形式で表示されます（デフォルト全解除、レイヤー/パラメータそれぞれに全選択/全解除ボタンあり）
4. 「選択したレイヤー・パラメータを削除（全Controller対象）」で、全Controllerを横断して選択した項目のみ一括削除されます（Undo対応）

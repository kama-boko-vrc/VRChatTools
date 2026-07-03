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

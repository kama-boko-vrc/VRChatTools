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

## UnusedArmatureBoneCleaner

指定したArmature配下のボーンのうち、アバターのどこからも参照されていないものを検出し、チェックリスト形式で選択した上で一括削除するエディタ拡張です。

未参照とみなすのは、そのボーン自身と配下すべてが以下のどこからも参照されていない場合のみです（親子関係で一括削除されるため、一部が使われている枝は候補から除外されます）。

- SkinnedMeshRendererの `rootBone` / `bones`
- HumanoidのAnimatorが `GetBoneTransform` で参照するボーン
- アバター配下の全コンポーネントが持つTransform/GameObject参照（PhysBoneやConstraintなど任意のコンポーネントに対応）

### 導入方法

[UnusedArmatureBoneCleaner/UnusedArmatureBoneCleaner.cs](UnusedArmatureBoneCleaner/UnusedArmatureBoneCleaner.cs) をUnityプロジェクトの `Assets` フォルダ配下（`Editor` フォルダ推奨）にコピーします。

### 使い方

1. メニューから `Tools > VRChatTools > Unused Armature Bone Cleaner` を開く
2. 「アバタールート」に参照スキャン範囲（アバター全体）、「Armatureルート」に削除候補を探すArmatureを指定
3. 「スキャン」を押すと、未参照のボーンがチェックリストで一覧表示されます（デフォルト全選択、全選択/全解除ボタンあり）
4. 「選択したボーンを削除」を押すと、選択したボーンが一括削除されます（Undo対応、Ctrl+Zで復元可）

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Narazaka.VRChat.BodyAdjustMatcher.Editor.Tests
{
    // bindpose -> rest -> 軸補正 -> 転送 のコア結合テスト（VRCAvatarDescriptor/MA に依存しない）
    public class TransferIntegrationTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                // テストで生成した Mesh も後始末する
                foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.sharedMesh != null) Object.DestroyImmediate(smr.sharedMesh);
                }
                Object.DestroyImmediate(root);
                root = null;
            }
        }

        static Transform Child(Transform parent, string name, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            return go.transform;
        }

        // root 直下に専用ホストを作り、現在の姿勢を rest とみなして bindpose を焼いた
        // SkinnedMeshRenderer を追加する（SMR は 1 GameObject に 1 つしか付けられないため host を分ける）
        void AddRenderer(string name, Transform[] bones)
        {
            var host = new GameObject(name);
            host.transform.SetParent(root.transform, false);
            var smr = host.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            var bindposes = new Matrix4x4[bones.Length];
            var rendererL2W = host.transform.localToWorldMatrix;
            for (int i = 0; i < bones.Length; i++)
            {
                // bindpose = bone.worldToLocal * renderer.localToWorld（rest姿勢で取得）
                bindposes[i] = bones[i].worldToLocalMatrix * rendererL2W;
            }
            mesh.bindposes = bindposes;
            smr.sharedMesh = mesh;
            smr.bones = bones;
        }

        void RunAdjust(Transform bodyArm, Transform clothArm)
        {
            var lookup = global::BodyAdjustMatcher.BuildRestRotationLookup(root.transform);
            var axis = global::BodyAdjustMatcher.BuildAxisAlignedRotations();
            global::BodyAdjustMatcher.AdjustRecursive(bodyArm, clothArm, "", "", lookup, axis);
        }

        // 素体未改変なら、軸が90°異なっていても衣装の回転を誤って上書きしない
        [Test]
        public void UnmodifiedBody_DoesNotFlipClothRotation_DespiteAxisDifference()
        {
            root = new GameObject("root");
            var bodyArm = Child(root.transform, "BodyArm", Vector3.zero, Quaternion.identity);
            var clothArm = Child(root.transform, "ClothArm", Vector3.zero, Quaternion.identity);
            // 親は同じ向き、ボーン自身の軸オリジンが90°異なる（body=identity / cloth=90°Y）
            var bodyBone = Child(bodyArm, "Bone", new Vector3(0f, 1f, 0f), Quaternion.identity);
            var clothBone = Child(clothArm, "Bone", new Vector3(0f, 1f, 0f), Quaternion.Euler(0f, 90f, 0f));

            AddRenderer("BodyMesh", new[] { bodyArm, bodyBone });
            AddRenderer("ClothMesh", new[] { clothArm, clothBone });

            var clothRotBefore = clothBone.localRotation;
            var clothPosBefore = clothBone.localPosition;

            RunAdjust(bodyArm, clothArm);

            Assert.That(Quaternion.Angle(clothBone.localRotation, clothRotBefore), Is.LessThan(0.1f),
                "未改変なのに衣装回転が変化した（軸違いの誤コピー = 旧バグ）");
            Assert.That(Vector3.Distance(clothBone.localPosition, clothPosBefore), Is.LessThan(0.001f),
                "未改変なのに衣装位置が変化した");
        }

        // 素体ボーンを回転改変すると、軸が異なる衣装ボーンも同じworld回転deltaで追従する
        [Test]
        public void ModifiedBodyRotation_TransfersSameWorldDelta_AcrossAxisDifference()
        {
            root = new GameObject("root");
            var bodyArm = Child(root.transform, "BodyArm", Vector3.zero, Quaternion.identity);
            var clothArm = Child(root.transform, "ClothArm", Vector3.zero, Quaternion.identity);
            var bodyBone = Child(bodyArm, "Bone", new Vector3(0f, 1f, 0f), Quaternion.identity);
            var clothBone = Child(clothArm, "Bone", new Vector3(0f, 1f, 0f), Quaternion.Euler(0f, 90f, 0f));

            AddRenderer("BodyMesh", new[] { bodyArm, bodyBone });
            AddRenderer("ClothMesh", new[] { clothArm, clothBone });

            var bodyRestWorld = bodyBone.rotation;
            var clothRestWorld = clothBone.rotation;

            // 改変: 親がidentityなので localRotation 左掛け = world で20°X回転
            var delta = Quaternion.AngleAxis(20f, Vector3.right);
            bodyBone.localRotation = delta * bodyBone.localRotation;

            RunAdjust(bodyArm, clothArm);

            var bodyWorldDelta = bodyBone.rotation * Quaternion.Inverse(bodyRestWorld);
            var clothWorldDelta = clothBone.rotation * Quaternion.Inverse(clothRestWorld);

            Assert.That(Quaternion.Angle(clothWorldDelta, bodyWorldDelta), Is.LessThan(0.5f),
                "衣装が素体と同じworld回転deltaで追従していない");
            Assert.That(Quaternion.Angle(clothWorldDelta, delta), Is.LessThan(0.5f),
                "転送されたdeltaが想定の20°になっていない");
        }

        // 親の軸オリジンが90°異なる場合でも、位置改変が衣装ボーンのworld位置へ追従する
        // （注: 回転補正は親の向き一致前提=既知の限界のため、ここでは位置のみ検証する）
        [Test]
        public void ModifiedBodyPosition_FollowsInWorld_WithParentAxisDifference()
        {
            root = new GameObject("root");
            var bodyArm = Child(root.transform, "BodyArm", Vector3.zero, Quaternion.identity);
            var clothArm = Child(root.transform, "ClothArm", Vector3.zero, Quaternion.Euler(0f, 90f, 0f));

            var pRest = new Vector3(0.3f, 1f, 0f);
            var bodyBone = Child(bodyArm, "Bone", pRest, Quaternion.identity);
            // clothBone を world で素体と同位置に置く（clothArm が90°Y回っているので逆回転で配置）
            var clothBoneLocal = Quaternion.Inverse(clothArm.rotation) * (pRest - clothArm.position);
            var clothBone = Child(clothArm, "Bone", clothBoneLocal, Quaternion.identity);

            AddRenderer("BodyMesh", new[] { bodyArm, bodyBone });
            AddRenderer("ClothMesh", new[] { clothArm, clothBone });

            // 改変: 親がidentityなので localPosition 加算 = world 移動
            var move = new Vector3(0.1f, 0.2f, -0.05f);
            bodyBone.localPosition += move;

            RunAdjust(bodyArm, clothArm);

            Assert.That(Vector3.Distance(clothBone.position, bodyBone.position), Is.LessThan(0.002f),
                "衣装位置が素体のworld移動へ追従していない");
        }

        // bindpose 欠如（restLookup 空）のフォールバックでも、未改変なら衣装は自身の rest を保持し、
        // 素体基準のクリーン90°位置へ引きずられて中途半端な角度にならないこと。
        // 素体ボーンは自然に傾いており(30°X)、衣装はそこからクリーンな90°Yオフセットを持つ。
        [Test]
        public void Fallback_UnmodifiedBody_KeepsClothRotation_WhenNoBindpose()
        {
            root = new GameObject("root");
            var bodyArm = Child(root.transform, "BodyArm", Vector3.zero, Quaternion.identity);
            var clothArm = Child(root.transform, "ClothArm", Vector3.zero, Quaternion.identity);
            var bodyLocal = Quaternion.AngleAxis(30f, Vector3.right);
            var clothLocal = bodyLocal * Quaternion.AngleAxis(90f, Vector3.up);
            var bodyBone = Child(bodyArm, "Bone", new Vector3(0f, 1f, 0f), bodyLocal);
            var clothBone = Child(clothArm, "Bone", new Vector3(0f, 1f, 0f), clothLocal);
            // SkinnedMeshRenderer を一切付けない → BuildRestRotationLookup は空 → 全ボーンがフォールバック経路

            var clothRotBefore = clothBone.localRotation;

            RunAdjust(bodyArm, clothArm);

            Assert.That(Quaternion.Angle(clothBone.localRotation, clothRotBefore), Is.LessThan(0.1f),
                "フォールバックでも未改変なら衣装は自身のrestを保持すべき（素体基準の中途半端な角度に動かない）");
        }

        // bindpose 欠如のフォールバックでも、素体の小さな回転改変(<45°)は world delta として転送され、
        // 余計な90°が乗らないこと。
        [Test]
        public void Fallback_ModifiedBody_TransfersWorldDelta_WhenNoBindpose()
        {
            root = new GameObject("root");
            var bodyArm = Child(root.transform, "BodyArm", Vector3.zero, Quaternion.identity);
            var clothArm = Child(root.transform, "ClothArm", Vector3.zero, Quaternion.identity);
            var bodyLocal = Quaternion.AngleAxis(30f, Vector3.right);
            var clothLocal = bodyLocal * Quaternion.AngleAxis(90f, Vector3.up);
            var bodyBone = Child(bodyArm, "Bone", new Vector3(0f, 1f, 0f), bodyLocal);
            var clothBone = Child(clothArm, "Bone", new Vector3(0f, 1f, 0f), clothLocal);

            var bodyRestWorld = bodyBone.rotation;
            var clothRestWorld = clothBone.rotation;

            // 小さな改変(15°Z, < 45°)
            var delta = Quaternion.AngleAxis(15f, Vector3.forward);
            bodyBone.localRotation = delta * bodyBone.localRotation;

            RunAdjust(bodyArm, clothArm);

            var bodyWorldDelta = bodyBone.rotation * Quaternion.Inverse(bodyRestWorld);
            var clothWorldDelta = clothBone.rotation * Quaternion.Inverse(clothRestWorld);
            Assert.That(Quaternion.Angle(clothWorldDelta, bodyWorldDelta), Is.LessThan(0.5f),
                "フォールバックでも改変分だけがworld deltaとして転送されるべき（余計な90°が乗らない）");
        }
    }
}

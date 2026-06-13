using NUnit.Framework;
using UnityEngine;

namespace Narazaka.VRChat.BodyAdjustMatcher.Editor.Tests
{
    // 軸スナップ純粋関数の単体テスト
    public class AxisAlignmentTests
    {
        [Test]
        public void BuildAxisAlignedRotations_Returns24DistinctAxisAlignedRotations()
        {
            var rotations = global::BodyAdjustMatcher.BuildAxisAlignedRotations();

            Assert.AreEqual(24, rotations.Length, "軸整列回転は24個であるべき");

            // 各回転は各ローカル軸を±座標軸へ写す（90°単位）
            foreach (var r in rotations)
            {
                AssertMapsToCardinalAxis(r * Vector3.right);
                AssertMapsToCardinalAxis(r * Vector3.up);
                AssertMapsToCardinalAxis(r * Vector3.forward);
            }

            // 24個は相異なる（最近接でも90°）
            for (int i = 0; i < rotations.Length; i++)
            {
                for (int j = i + 1; j < rotations.Length; j++)
                {
                    Assert.Greater(Quaternion.Angle(rotations[i], rotations[j]), 1f,
                        $"rotations[{i}] と rotations[{j}] が重複している");
                }
            }
        }

        static void AssertMapsToCardinalAxis(Vector3 v)
        {
            var ax = Mathf.Abs(v.x);
            var ay = Mathf.Abs(v.y);
            var az = Mathf.Abs(v.z);
            var maxComp = Mathf.Max(ax, Mathf.Max(ay, az));
            var sum = ax + ay + az;
            Assert.That(maxComp, Is.EqualTo(1f).Within(1e-4f), $"{v} の最大成分が±1でない");
            Assert.That(sum, Is.EqualTo(1f).Within(1e-4f), $"{v} が座標軸に整列していない");
        }

        [Test]
        public void SnapToAxisAligned_SnapsNoisyRotationToExact90()
        {
            var table = global::BodyAdjustMatcher.BuildAxisAlignedRotations();
            var noisy = Quaternion.Euler(0f, 89.99f, 0f);

            var snapped = global::BodyAdjustMatcher.SnapToAxisAligned(noisy, table);

            Assert.That(Quaternion.Angle(snapped, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f),
                "89.99°が厳密な90°へスナップされていない");
        }

        [Test]
        public void SnapToAxisAligned_SnapsNearIdentityToIdentity()
        {
            var table = global::BodyAdjustMatcher.BuildAxisAlignedRotations();
            var noisy = Quaternion.Euler(1.2f, -0.8f, 0.5f);

            var snapped = global::BodyAdjustMatcher.SnapToAxisAligned(noisy, table);

            Assert.That(Quaternion.Angle(snapped, Quaternion.identity), Is.LessThan(0.01f),
                "微小回転がidentityへスナップされていない");
        }
    }
}

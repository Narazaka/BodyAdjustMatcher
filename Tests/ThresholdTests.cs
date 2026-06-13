using NUnit.Framework;
using UnityEngine;

namespace Narazaka.VRChat.BodyAdjustMatcher.Editor.Tests
{
    // 量ごとの有意差判定（位置=絶対メートル / 回転=角度全体）の単体テスト
    public class ThresholdTests
    {
        [Test]
        public void GetRotationDifference_ReturnsNull_WhenBelowAngleThreshold()
        {
            var from = Quaternion.AngleAxis(0.01f, Vector3.up); // 0.01° < 0.05°
            var to = Quaternion.identity;

            var result = global::BodyAdjustMatcher.GetRotationDifference(from, to);

            Assert.IsFalse(result.HasValue, "しきい値未満の微小回転差は無視されるべき");
        }

        [Test]
        public void GetRotationDifference_ReturnsFrom_WhenAboveAngleThreshold()
        {
            var from = Quaternion.AngleAxis(1f, Vector3.up); // 1° > 0.05°
            var to = Quaternion.identity;

            var result = global::BodyAdjustMatcher.GetRotationDifference(from, to);

            Assert.IsTrue(result.HasValue, "しきい値超過の回転差は採用されるべき");
            Assert.That(Quaternion.Angle(result.Value, from), Is.LessThan(1e-3f), "丸ごと from を採用するべき");
        }

        [Test]
        public void GetPositionDifference_IgnoresSubThresholdPerComponent()
        {
            // 全成分が 1e-4(0.1mm) 未満
            var from = new Vector3(5e-5f, -3e-5f, 8e-5f);
            var to = Vector3.zero;

            var result = global::BodyAdjustMatcher.GetPositionDifference(from, to);

            Assert.IsFalse(result.HasValue, "全成分がしきい値未満なら無視されるべき");
        }

        [Test]
        public void GetPositionDifference_CopiesOnlyChangedComponents()
        {
            // x,z はしきい値超過、y は未満
            var from = new Vector3(0.5f, 3e-5f, 0.2f);
            var to = Vector3.zero;

            var result = global::BodyAdjustMatcher.GetPositionDifference(from, to);

            Assert.IsTrue(result.HasValue);
            Assert.That(result.Value.x, Is.EqualTo(0.5f).Within(1e-6f), "x は採用されるべき");
            Assert.That(result.Value.y, Is.EqualTo(0f).Within(1e-6f), "y はしきい値未満なので to を維持するべき");
            Assert.That(result.Value.z, Is.EqualTo(0.2f).Within(1e-6f), "z は採用されるべき");
        }
    }
}

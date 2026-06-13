using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using nadena.dev.modular_avatar.core;

[assembly: InternalsVisibleTo("Narazaka.VRChat.BodyAdjustMatcher.Editor.Tests")]

public class BodyAdjustMatcher
{
    [MenuItem("GameObject/Adjust Body Match (on Cloth Armature)")]
    static void DoMatch()
    {
        var gos = Selection.gameObjects;
        foreach (var go in gos)
        {
            DoMatch(go);
        }
    }

    static void DoMatch(GameObject go)
    {
        var mergeArmature = go.GetComponentInChildren<ModularAvatarMergeArmature>();
        if (mergeArmature != null)
        {
            go = mergeArmature.gameObject;
        }

        var root = go.GetComponentInParent<VRCAvatarDescriptor>();
        if (root == null)
        {
            EditorUtility.DisplayDialog("ERROR", "No VRCAvatarDescriptor found in parent hierarchy.", "OK");
            return;
        }
        var rootArmature = mergeArmature == null ? null : mergeArmature.mergeTargetObject.transform;
        if (rootArmature == null) rootArmature = root.transform.Find("Armature");
        if (rootArmature == null) rootArmature = root.transform.Find("armature");
        if (rootArmature == null)
        {
            EditorUtility.DisplayDialog("ERROR", "No Armature found in VRCAvatarDescriptor hierarchy.", "OK");
            return;
        }
        var prefix = mergeArmature == null ? "" : mergeArmature.prefix;
        var suffix = mergeArmature == null ? "" : mergeArmature.suffix;
        var restRotations = BuildRestRotationLookup(root.transform);
        var axisAlignedRotations = BuildAxisAlignedRotations();
        AdjustRecursive(rootArmature, go.transform, prefix, suffix, restRotations, axisAlignedRotations);
    }

    internal static void AdjustRecursive(Transform body, Transform cloth, string prefix, string suffix, Dictionary<Transform, Quaternion> restRotations, Quaternion[] axisAlignedRotations)
    {
        if (body == null || cloth == null)
        {
            return;
        }
        // 親フレームの軸オリジン差(90°単位)を補正してから差分比較する
        var bodyParentRef = GetRestRotation(body.parent, restRotations);
        var clothParentRef = GetRestRotation(cloth.parent, restRotations);
        var positionAxisCorrection = SnapToAxisAligned(Quaternion.Inverse(clothParentRef) * bodyParentRef, axisAlignedRotations);
        var correctedPosition = positionAxisCorrection * body.localPosition;
        // compare transform and copy if different (with threshold)
        var localPosition = GetValueWithDifference(correctedPosition, cloth.localPosition);
        if (localPosition.HasValue)
        {
            Undo.RecordObject(cloth, "Adjust Body Match");
            cloth.localPosition = localPosition.Value;
        }
        // ボーン自身の軸オリジン差(90°単位)を補正してから差分比較する
        var bodyRef = GetRestRotation(body, restRotations);
        var clothRef = GetRestRotation(cloth, restRotations);
        var rotationAxisCorrection = SnapToAxisAligned(Quaternion.Inverse(bodyRef) * clothRef, axisAlignedRotations);
        var correctedRotation = body.localRotation * rotationAxisCorrection;
        var localRotation = GetValueWithDifference(correctedRotation, cloth.localRotation);
        if (localRotation.HasValue)
        {
            Undo.RecordObject(cloth, "Adjust Body Match");
            cloth.localRotation = localRotation.Value;
        }
        var localScale = GetValueWithDifference(body.localScale, cloth.localScale);
        if (localScale.HasValue)
        {
            Undo.RecordObject(cloth, "Adjust Body Match");
            cloth.localScale = localScale.Value;
        }

        var bodyScaleAdjuster = body.GetComponent<ModularAvatarScaleAdjuster>();
        if (bodyScaleAdjuster != null)
        {
            var clothScaleAdjuster = cloth.GetComponent<ModularAvatarScaleAdjuster>();
            if (clothScaleAdjuster == null)
            {
                Undo.AddComponent<ModularAvatarScaleAdjuster>(cloth.gameObject);
                clothScaleAdjuster = cloth.GetComponent<ModularAvatarScaleAdjuster>();
            }
            var scale = GetValueWithDifference(bodyScaleAdjuster.Scale, clothScaleAdjuster.Scale);
            if (scale.HasValue)
            {
                Undo.RecordObject(clothScaleAdjuster, "Adjust Body Match");
                clothScaleAdjuster.Scale = scale.Value;
            }
        }
        // recurse children
        for (int i = 0; i < body.childCount; i++)
        {
            var childBody = body.GetChild(i);
            var childCloth = cloth.Find(prefix + childBody.name + suffix);
            if (childCloth != null)
            {
                AdjustRecursive(childBody, childCloth, prefix, suffix, restRotations, axisAlignedRotations);
            }
        }
    }

    static Vector3? GetValueWithDifference(Vector3 from, Vector3 to, float threshold = 0.001f)
    {
        // var max = Mathf.Max(Mathf.Abs(from.x), Mathf.Abs(to.x), Mathf.Abs(from.y), Mathf.Abs(to.y), Mathf.Abs(from.z), Mathf.Abs(to.z));
        var result = to;
        var changed = false;
        if (HasDifference(from.x, to.x, threshold))
        {
            result.x = from.x;
            changed = true;
        }
        if (HasDifference(from.y, to.y, threshold))
        {
            result.y = from.y;
            changed = true;
        }
        if (HasDifference(from.z, to.z, threshold))
        {
            result.z = from.z;
            changed = true;
        }
        return changed ? result : (Vector3?)null;
    }

    static Quaternion? GetValueWithDifference(Quaternion from, Quaternion to, float threshold = 0.001f)
    {
        var result = to;
        var changed = false;
        if (HasDifference(from.x, to.x, threshold))
        {
            result.x = from.x;
            changed = true;
        }
        if (HasDifference(from.y, to.y, threshold))
        {
            result.y = from.y;
            changed = true;
        }
        if (HasDifference(from.z, to.z, threshold))
        {
            result.z = from.z;
            changed = true;
        }
        if (HasDifference(from.w, to.w, threshold))
        {
            result.w = from.w;
            changed = true;
        }
        return changed ? result : (Quaternion?)null;
    }

    const float smallestSignificantValue = 1e-5f;

    static bool HasDifference(float from, float to, float threshold = 0.01f)
    {
        if (Mathf.Abs(from) < smallestSignificantValue || Mathf.Abs(to) < smallestSignificantValue)
        {
            return Mathf.Abs(from - to) > smallestSignificantValue;
        }
        else
        {
            var baseValue = Mathf.Max(Mathf.Abs(from), Mathf.Abs(to));
            return Mathf.Abs(from - to) / baseValue > threshold;
        }
    }

    internal static Quaternion[] BuildAxisAlignedRotations()
    {
        var dirs = new Vector3[]
        {
            Vector3.right, Vector3.left,
            Vector3.up, Vector3.down,
            Vector3.forward, Vector3.back,
        };
        var list = new List<Quaternion>();
        foreach (var forward in dirs)
        {
            foreach (var up in dirs)
            {
                // forward と up が直交する組のみ（6 forward × 4 up = 24通り）
                if (Mathf.Abs(Vector3.Dot(forward, up)) < 0.5f)
                {
                    list.Add(Quaternion.LookRotation(forward, up));
                }
            }
        }
        return list.ToArray();
    }

    // 入力回転を、軸オリジン差として妥当な 90° 単位の最近傍回転へスナップする
    internal static Quaternion SnapToAxisAligned(Quaternion q, Quaternion[] axisAlignedRotations)
    {
        var best = Quaternion.identity;
        var bestAngle = float.MaxValue;
        foreach (var candidate in axisAlignedRotations)
        {
            var angle = Quaternion.Angle(q, candidate);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = candidate;
            }
        }
        return best;
    }

    // 全SkinnedMeshRendererのbindposeから各ボーンのrest(bind時)world回転を集約する
    internal static Dictionary<Transform, Quaternion> BuildRestRotationLookup(Transform root)
    {
        var result = new Dictionary<Transform, Quaternion>();
        var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in renderers)
        {
            var mesh = smr.sharedMesh;
            if (mesh == null) continue;
            var bindposes = mesh.bindposes;
            var bones = smr.bones;
            var rendererMatrix = smr.transform.localToWorldMatrix;
            var count = Mathf.Min(bones.Length, bindposes.Length);
            for (int i = 0; i < count; i++)
            {
                var bone = bones[i];
                if (bone == null || result.ContainsKey(bone)) continue;
                // bone_rest_l2w = renderer_l2w * bindpose^-1
                var restMatrix = rendererMatrix * bindposes[i].inverse;
                result[bone] = restMatrix.rotation;
            }
        }
        return result;
    }

    // restに有ればそれを正として返し、無ければ現在のworld回転で代用する
    static Quaternion GetRestRotation(Transform t, Dictionary<Transform, Quaternion> restRotations)
    {
        if (t == null) return Quaternion.identity;
        return restRotations.TryGetValue(t, out var rot) ? rot : t.rotation;
    }
}

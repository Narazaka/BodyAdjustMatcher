using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using nadena.dev.modular_avatar.core;

public class BodyAdjustMatcher
{
    [MenuItem("GameObject/Adjust Body Match (on Cloth Armature)")]
    static void DoMatch()
    {
        var go = Selection.activeGameObject;
        var root = go.GetComponentInParent<VRCAvatarDescriptor>();
        if (root == null)
        {
            EditorUtility.DisplayDialog("ERROR", "No VRCAvatarDescriptor found in parent hierarchy.", "OK");
            return;
        }
        var rootArmature = root.transform.Find("Armature");
        if (rootArmature == null)
        {
            EditorUtility.DisplayDialog("ERROR", "No Armature found in VRCAvatarDescriptor hierarchy.", "OK");
            return;
        }

        AdjustRecursive(rootArmature, go.transform);
    }

    static void AdjustRecursive(Transform body, Transform cloth)
    {
        if (body == null || cloth == null)
        {
            return;
        }
        var localPosition = GetValueWithDifference(body.localPosition, cloth.localPosition);
        // compare transform and copy if different (with threshold)
        if (localPosition.HasValue)
        {
            Undo.RecordObject(cloth, "Adjust Body Match");
            cloth.localPosition = localPosition.Value;
        }
        var localRotation = GetValueWithDifference(body.localRotation, cloth.localRotation);
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
            var childCloth = cloth.Find(childBody.name);
            if (childCloth != null)
            {
                AdjustRecursive(childBody, childCloth);
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
}

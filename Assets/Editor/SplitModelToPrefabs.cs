using UnityEngine;
using UnityEditor;
using System.IO;

public static class SplitModelToPrefabs
{
    [MenuItem("Tools/Split Selected Model To Prefabs")]
    static void Split()
    {
        GameObject model = Selection.activeObject as GameObject;
        if (model == null)
        {
            Debug.LogError("Select FBX with rocks in Project.");
            return;
        }

        string modelPath = AssetDatabase.GetAssetPath(model);
        string outDir = Path.GetDirectoryName(modelPath) + "/Prefabs";
        if (!AssetDatabase.IsValidFolder(outDir))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(modelPath), "Prefabs");

        foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            var go = new GameObject(mf.name);
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;

            PrefabUtility.SaveAsPrefabAsset(go, outDir + "/" + mf.name + ".prefab");
            Object.DestroyImmediate(go);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(PrefabCatalog))]
public class PrefabCatalogEditor : Editor
{
    string folder = "Assets/VRMPAssets/Prefabs/NetworkedPrefabs/BuildingBlocks";
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        folder = EditorGUILayout.TextField("Scan folder", folder);
        if (GUILayout.Button("Scan & Fill"))
        {
            var cat = (PrefabCatalog)target;
            cat.prefabs.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:prefab", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) cat.prefabs.Add(prefab);
            }
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            Debug.Log($"Filled catalog with {cat.prefabs.Count} prefabs from {folder}");
        }
    }
}
#endif


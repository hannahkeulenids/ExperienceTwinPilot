using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private static string SaveFolder => Path.Combine(Application.persistentDataPath, "Saves");

    /// <summary>
    /// Schrijft JSON naar een bestand in de Saves-map.
    /// </summary>
    public static void SaveJson(string fileName, string json)
    {
        if (!Directory.Exists(SaveFolder))
            Directory.CreateDirectory(SaveFolder);

        string path = Path.Combine(SaveFolder, fileName + ".json");
        File.WriteAllText(path, json);
        Debug.Log($"[SaveSystem] Saved JSON to: {path}");
    }

    /// <summary>
    /// Leest JSON terug uit een bestand. Retourneert null als het niet bestaat.
    /// </summary>
    public static string LoadJson(string fileName)
    {
        string path = Path.Combine(SaveFolder, fileName + ".json");

        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            Debug.Log($"[SaveSystem] Loaded JSON from: {path}");
            return json;
        }

        Debug.LogWarning($"[SaveSystem] No save found at {path}");
        return null;
    }
}
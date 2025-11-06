using System;
using System.IO;
using System.Linq;
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

    public static string[] ListSavesNoExtension()
    {
        var folder = Path.Combine(UnityEngine.Application.persistentDataPath, "Saves");
        if (!Directory.Exists(folder)) return System.Array.Empty<string>();

        return Directory.GetFiles(folder, "*.json")
                        .Select(Path.GetFileNameWithoutExtension)
                        .OrderBy(n => n)
                        .ToArray();
    }
    /// <summary>
    /// load the most recent json file in the saves folder.
    /// return the contents of JSON (wihtout .json) by 'out'.
    /// </summary>
    public static string LoadLatestJson(out string latestFileNameWithoutExt)
    {
        latestFileNameWithoutExt = null;
        if (!Directory.Exists(SaveFolder)) return null;

        var files = new DirectoryInfo(SaveFolder).GetFiles("*.json");
        if (files.Length == 0) return null;

        var latest = files.OrderByDescending(f => f.LastWriteTimeUtc).First();
        latestFileNameWithoutExt = Path.GetFileNameWithoutExtension(latest.Name);
        return File.ReadAllText(latest.FullName);
    }

    /// <summary>
    /// make timestamp.
    /// </summary>
    public static string MakeTimestampedName(string baseName)
    {
        // Voorbeeld: MyBuild_2025-09-25_1542
        return $"{baseName}_{DateTime.Now:yyyy-MM-dd_HHmm}";
    }

    public static bool TryLoadJson(string fileName, out string json)
    {
        json = LoadJson(fileName);
        return !string.IsNullOrEmpty(json);
    }

}
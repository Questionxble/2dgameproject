using UnityEngine;
using System.IO;

public static class SaveSystem
{
    public static void SaveGame(SaveData data, int slot)
    {
        string path = Application.persistentDataPath + "/save" + slot + ".json";
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
        Debug.Log("Saved to slot " + slot);
        Debug.Log("saved to:" + path);
        Debug.Log("Read save file: " + json);

    }

    public static SaveData LoadGame(int slot)
    {
        string path = Path.Combine(Application.persistentDataPath, "save" + slot + ".json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<SaveData>(json);
        }
        Debug.LogWarning("No save file found at " + path);
        return null;
    }


    public static void DeleteGame(int slot)
    {
        string path = Application.persistentDataPath + "/save" + slot + ".json";
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("Deleted save slot " + slot);
        }
    }


}

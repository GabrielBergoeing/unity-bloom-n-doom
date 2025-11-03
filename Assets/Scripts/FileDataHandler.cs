using System;
using System.IO;
using UnityEngine;
using UnityEditor;

public class FileDataHandler
{
    private string fullPath;
    private bool encryptData;
    private string codeWord = "IWasHere";

    public FileDataHandler(string dataDirPath, string dataFileName, bool encryptData)
    {
        fullPath = Path.Combine(dataDirPath, dataFileName);
        this.encryptData = encryptData;
    }

    public void SaveData(LevelData levelData)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string dataToSave = JsonUtility.ToJson(levelData, true);

            if (encryptData)
                dataToSave = EncryptDecrypt(dataToSave);

            File.WriteAllText(fullPath, dataToSave);

            #if UNITY_EDITOR
            AssetDatabase.Refresh();
            #endif
        }
        catch (Exception e)
        {
            Debug.LogError("Error on trying to save data on file: " + fullPath + "\n" + e);
        }
    }

    public LevelData LoadData()
    {
        LevelData levelData = null;

        if (File.Exists(fullPath))
        {
            try
            {
                string dataToLoad = "";

                using (FileStream stream = new FileStream(fullPath, FileMode.Open))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        dataToLoad = reader.ReadToEnd();
                    }
                }

                if (encryptData)
                    dataToLoad = EncryptDecrypt(dataToLoad);

                levelData = JsonUtility.FromJson<LevelData>(dataToLoad);
                Debug.Log("Savefile path: " + fullPath);
            }
            catch (Exception e)
            {
                Debug.LogError("Error on trying to load data on file: " + fullPath + "\n" + e);
            }
        }
        return levelData;
    }

    public void Delete()
    {
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    private string EncryptDecrypt(string data)
    {
        string modifiedData = "";

        for (int i = 0; i < data.Length; i++)
        {
            modifiedData += (char)(data[i] ^ codeWord[i % codeWord.Length]);
        }

        return modifiedData;
    }
}

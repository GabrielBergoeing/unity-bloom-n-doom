using System.IO;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public LevelObjectFactory factory;
    [SerializeField] private string fileName = "leveltest.json";
    [SerializeField] private bool encryptData = false;

    private LevelData loadedLevel;
    public FileDataHandler dataHandler;

    private void Awake()
    {
        string levelFileToLoad = fileName;
        
        if (GameManager.instance != null && !string.IsNullOrEmpty(GameManager.instance.nextLevelFileName))
        {
            levelFileToLoad = GameManager.instance.nextLevelFileName;
            GameManager.instance.nextLevelFileName = null;
        }
        
        dataHandler = new FileDataHandler(
            Path.Combine(Application.dataPath, "Levels"),
            levelFileToLoad,
            encryptData: encryptData
        );
    }
    private void Start()
    {
        loadedLevel = dataHandler.LoadData();

        if (loadedLevel == null)
        {
            Debug.LogError("Failed to load level data.");
            return;
        }

        PopulateObjects(loadedLevel);

        if (FarmManager.instance != null)
        {
            FarmManager.instance.InitializeTileStates(true);
        }
    }

    public LevelData GetLoadedLevel() => loadedLevel;

    public void SaveLevel(LevelData data)
    {
        dataHandler.SaveData(data);
    }

    private void PopulateObjects(LevelData data)
    {
        foreach (var obj in data.objects)
        {
            LevelObjectType type = (LevelObjectType)System.Enum.Parse(typeof(LevelObjectType), obj.type);
            factory.Create(type, new Vector3(obj.x, obj.y, 0), obj.subtype);
        }
    }
}

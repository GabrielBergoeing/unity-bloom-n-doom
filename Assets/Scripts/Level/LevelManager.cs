using System;
using System.IO;
using UnityEngine;
public class LevelManager : MonoBehaviour
{
    public LevelObjectFactory factory;
    private LevelData currentLevel;
    [SerializeField] private string fileName = "leveltest.json";
    [SerializeField] private bool encryptData = false;

    private GridData loadedLevel;
    public FileDataHandler dataHandler;
    public static event Action OnLevelLoaded; //Signal-based event

    private void Awake()
    {
        currentLevel = GameManager.instance.currentLevel;
        string levelFileToLoad = currentLevel.jsonFileName ?? fileName;

        // Use Resources folder for build compatibility
        dataHandler = new FileDataHandler(
            Path.Combine(Application.streamingAssetsPath, "Levels"),
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
        OnLevelLoaded?.Invoke(); //Trigger any script subscribed to the event
    }

    public GridData GetLoadedLevel() => loadedLevel;

    public void SaveLevel(GridData data)
    {
        dataHandler.SaveData(data);
    }

    private void PopulateObjects(GridData data)
    {
        foreach (var obj in data.objects)
        {
            LevelObjectType type = (LevelObjectType)System.Enum.Parse(typeof(LevelObjectType), obj.type);
            factory.Create(type, new Vector3(obj.x, obj.y, 0), obj.subtype);
        }
    }
}

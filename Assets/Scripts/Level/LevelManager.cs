using System;
using System.IO;
using UnityEngine;
using Mirror;

public class LevelManager : MonoBehaviour
{
    public LevelObjectFactory factory;
    private LevelData currentLevel;
    [SerializeField] private string fileName = "leveltest.json";
    [SerializeField] private bool encryptData = false;

    private GridData loadedLevel;
    public FileDataHandler dataHandler;
    public static event Action OnLevelLoaded;

    private bool levelLoaded = false;

    private void Awake()
    {
        // Intentar obtener nivel desde GameManager
        if (GameManager.instance != null && GameManager.instance.currentLevel != null)
        {
            currentLevel = GameManager.instance.currentLevel;
            Debug.Log("[LevelManager] Nivel cargado desde GameManager.");
        }
        else
        {
            // Fallback a nivel por defecto
            Debug.LogWarning("[LevelManager] GameManager.currentLevel no configurado. Intentando cargar nivel por defecto.");
            currentLevel = Resources.Load<LevelData>("Levels/DefaultLevel");
            
            if (currentLevel == null)
            {
                Debug.LogWarning("[LevelManager] No se encontró LevelData. Usando configuración vacía.");
                currentLevel = ScriptableObject.CreateInstance<LevelData>();
            }
        }

        string levelFileToLoad = currentLevel.jsonFileName ?? fileName;
        string levelPath = Path.Combine(Application.streamingAssetsPath, "Levels");
        dataHandler = new FileDataHandler(levelPath, levelFileToLoad, encryptData: encryptData);
    }

    private void Start()
    {
        // Validar que dataHandler fue inicializado
        if (dataHandler == null)
        {
            Debug.LogError("[LevelManager] FileDataHandler no inicializado en Awake.");
            return;
        }

        // Si estamos en modo red, esperar a que se sincronice
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.Log("[LevelManager] Modo red detectado. Esperando sincronización...");
            // Esperar a que FarmManager esté listo
            if (FarmManager.instance == null)
            {
                Debug.LogWarning("[LevelManager] FarmManager aún no inicializado. Esperando...");
                Invoke(nameof(LoadLevelData), 0.5f);
                return;
            }
        }

        LoadLevelData();
    }

    private void LoadLevelData()
    {
        if (levelLoaded)
            return;

        levelLoaded = true;

        // Intentar cargar datos
        loadedLevel = dataHandler.LoadData();

        if (loadedLevel == null)
        {
            Debug.LogWarning("[LevelManager] No se pudo cargar archivo de nivel. Creando GridData vacío.");
            loadedLevel = new GridData();
            loadedLevel.objects = new System.Collections.Generic.List<GridObjectData>();
        }

        // Poblar objetos si no está vacío
        if (loadedLevel.objects != null && loadedLevel.objects.Count > 0)
        {
            PopulateObjects(loadedLevel);
        }
        else
        {
            Debug.Log("[LevelManager] GridData vacío. Saltando PopulateObjects.");
        }

        // Signal que el nivel está cargado
        OnLevelLoaded?.Invoke();
        Debug.Log("[LevelManager] Nivel cargado completamente.");
    }

    public GridData GetLoadedLevel() => loadedLevel;

    public void SaveLevel(GridData data)
    {
        if (dataHandler != null)
        {
            dataHandler.SaveData(data);
        }
    }

    private void PopulateObjects(GridData data)
    {
        if (factory == null)
        {
            Debug.LogError("[LevelManager] LevelObjectFactory no asignado.");
            return;
        }

        Debug.Log($"[LevelManager] Poblando {data.objects.Count} objetos.");

        foreach (var obj in data.objects)
        {
            try
            {
                LevelObjectType type = (LevelObjectType)System.Enum.Parse(typeof(LevelObjectType), obj.type);
                factory.Create(type, new Vector3(obj.x, obj.y, 0), obj.subtype);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LevelManager] Error al crear objeto {obj.type}: {ex}");
            }
        }
    }
}

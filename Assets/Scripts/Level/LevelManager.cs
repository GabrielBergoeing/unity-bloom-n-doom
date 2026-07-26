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
    public static bool IsLevelLoaded { get; private set; }

    // True while waiting on a late-join CurrentLevelQueryMessage response (see
    // OnlineNetworkManager) - Start() must not run the normal load flow yet in that case,
    // OnLevelReceivedFromServer() below drives it once the response arrives instead.
    private bool awaitingServerLevel = false;

    private void Awake()
    {
        IsLevelLoaded = false;
        ActivateSceneNetworkObjectsIfOffline();

        // Intentar obtener nivel desde GameManager
        if (GameManager.instance != null && GameManager.instance.currentLevel != null)
        {
            currentLevel = GameManager.instance.currentLevel;
            Debug.Log("[LevelManager] Nivel cargado desde GameManager.");
            InitializeDataHandler();
            return;
        }

        // A plain client with no currentLevel yet (and never the dedicated server/host,
        // which always sets it themselves in OnlineNetworkManager.SelectLevel before the
        // scene even changes) means a late join: the one-shot LevelSelectedMessage
        // broadcast already fired before this connection existed, so it was never
        // delivered here. Ask the server directly instead of falling back to an empty/
        // wrong level - see OnlineNetworkManager.ClientLevelAssigned/CurrentLevelQueryMessage.
        if (NetworkClient.active && !NetworkServer.active && NetworkManager.singleton is OnlineNetworkManager onlineMgr)
        {
            Debug.LogWarning("[LevelManager] GameManager.currentLevel no disponible aún (posible late-join). Pidiendo nivel al servidor...");
            awaitingServerLevel = true;
            onlineMgr.ClientLevelAssigned += OnLevelReceivedFromServer;
            NetworkClient.Send(new CurrentLevelQueryMessage());
            return;
        }

        // Fallback a nivel por defecto (offline, o sin nivel configurado)
        Debug.LogWarning("[LevelManager] GameManager.currentLevel no configurado. Intentando cargar nivel por defecto.");
        currentLevel = Resources.Load<LevelData>("Levels/DefaultLevel");

        if (currentLevel == null)
        {
            Debug.LogWarning("[LevelManager] No se encontr� LevelData. Usando configuraci�n vac�a.");
            currentLevel = ScriptableObject.CreateInstance<LevelData>();
        }

        InitializeDataHandler();
    }

    private void InitializeDataHandler()
    {
        string levelFileToLoad = currentLevel.jsonFileName ?? fileName;
        string levelPath = Path.Combine(Application.streamingAssetsPath, "Levels");
        dataHandler = new FileDataHandler(levelPath, levelFileToLoad, encryptData: encryptData);
    }

    private void OnLevelReceivedFromServer(LevelData level)
    {
        if (NetworkManager.singleton is OnlineNetworkManager onlineMgr)
            onlineMgr.ClientLevelAssigned -= OnLevelReceivedFromServer;

        currentLevel = level;
        awaitingServerLevel = false;
        InitializeDataHandler();
        Debug.Log("[LevelManager] Nivel recibido del servidor (late-join).");

        StartLoadFlow();
    }

    private void OnDestroy()
    {
        if (awaitingServerLevel && NetworkManager.singleton is OnlineNetworkManager onlineMgr)
            onlineMgr.ClientLevelAssigned -= OnLevelReceivedFromServer;
    }

    private void Start()
    {
        // Waiting on a late-join server response - OnLevelReceivedFromServer() will call
        // StartLoadFlow() once it arrives, instead of falling through here.
        if (awaitingServerLevel)
            return;

        StartLoadFlow();
    }

    private void StartLoadFlow()
    {
        // Validar que dataHandler fue inicializado
        if (dataHandler == null)
        {
            Debug.LogError("[LevelManager] FileDataHandler no inicializado.");
            return;
        }

        // Si estamos en modo red, esperar a que se sincronice
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.Log("[LevelManager] Modo red detectado. Esperando sincronizaci�n...");
            // Esperar a que FarmManager est� listo
            if (FarmManager.instance == null)
            {
                Debug.LogWarning("[LevelManager] FarmManager a�n no inicializado. Esperando...");
                Invoke(nameof(LoadLevelData), 0.5f);
                return;
            }
        }

        LoadLevelData();
    }

    // Mirror's NetworkScenePostProcess disables every scene object that has a
    // NetworkIdentity when the player is BUILT, expecting the server to spawn
    // (re-activate) them later. Offline there is no server, so objects like
    // FarmManager stay inactive forever in builds - even though they work in the
    // editor, where play-mode scene loads skip that post-process step.
    private void ActivateSceneNetworkObjectsIfOffline()
    {
        if (NetworkServer.active || NetworkClient.active)
            return;

        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (NetworkIdentity identity in root.GetComponentsInChildren<NetworkIdentity>(true))
            {
                if (!identity.gameObject.activeSelf)
                {
                    Debug.Log($"[LevelManager] Modo offline: activando objeto de escena {identity.gameObject.name}");
                    identity.gameObject.SetActive(true);
                }
            }
        }
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
            Debug.LogWarning("[LevelManager] No se pudo cargar archivo de nivel. Creando GridData vac�o.");
            loadedLevel = new GridData();
            loadedLevel.objects = new System.Collections.Generic.List<GridObjectData>();
        }

        // Poblar objetos si no est� vac�o
        if (loadedLevel.objects != null && loadedLevel.objects.Count > 0)
        {
            PopulateObjects(loadedLevel);
        }
        else
        {
            Debug.Log("[LevelManager] GridData vac�o. Saltando PopulateObjects.");
        }

        // Signal que el nivel est� cargado
        IsLevelLoaded = true;
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

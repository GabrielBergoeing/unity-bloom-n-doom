using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One-click setup for the NGO online mode:
///  - creates/populates Assets/Resources/NetworkAssets.asset (id registry),
///  - adds NetworkObject / ClientNetworkTransform / OwnerNetworkAnimator / NetworkPlayer
///    to the character prefabs (and disables PlayerInput so only owners enable it),
///  - adds NetworkObject to plant prefabs, NetworkObject + NetworkTransform to item prefabs,
///  - assigns deterministic Pickup.itemId values (index in the registry),
///  - creates Assets/Resources/GameSession.prefab and Assets/Resources/NetworkBootstrap.prefab
///    (NetworkManager + UnityTransport + ConnectionManager + overlay UI).
/// Safe to run multiple times.
/// </summary>
public static class NGOSetupWizard
{
    private const string ResourcesDir = "Assets/Resources";
    private const string NetworkAssetsPath = ResourcesDir + "/NetworkAssets.asset";
    private const string SessionPrefabPath = ResourcesDir + "/GameSession.prefab";
    private const string BootstrapPrefabPath = ResourcesDir + "/NetworkBootstrap.prefab";

    private const string CharacterDbPath = "Assets/Data/CharacterDatabase.asset";
    private const string LevelsFolder = "Assets/Data/Levels";
    private static readonly string[] ItemFolders = { "Assets/Prefab/Seeds", "Assets/Prefab/Tools" };
    private const string PlantsFolder = "Assets/Prefab/Plants";
    private const string PrefabRootFolder = "Assets/Prefab";

    [MenuItem("Tools/NGO Setup/Run Full Setup")]
    public static void RunFullSetup()
    {
        EnsureFolder(ResourcesDir);

        var assets = LoadOrCreateNetworkAssets();
        PopulateRegistry(assets);
        AssignItemIds(assets);
        SetupCharacterPrefabs(assets);
        SetupPlantPrefabs(assets);
        SetupItemPrefabs(assets);
        assets.sessionPrefab = EnsureSessionPrefab();
        EnsureBootstrapPrefab();

        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[NGOSetup] Done. Registry: {assets.Characters.Length} characters, " +
                  $"{assets.levels.Count} levels, {assets.items.Count} items, " +
                  $"{assets.plants.Count} plants, {assets.projectiles.Count} projectiles.");
    }

    // ======================================================
    //  REGISTRY
    // ======================================================
    private static NetworkAssets LoadOrCreateNetworkAssets()
    {
        var assets = AssetDatabase.LoadAssetAtPath<NetworkAssets>(NetworkAssetsPath);
        if (assets == null)
        {
            assets = ScriptableObject.CreateInstance<NetworkAssets>();
            AssetDatabase.CreateAsset(assets, NetworkAssetsPath);
        }
        return assets;
    }

    private static void PopulateRegistry(NetworkAssets assets)
    {
        assets.characterDatabase = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(CharacterDbPath);
        if (assets.characterDatabase == null)
            Debug.LogError($"[NGOSetup] CharacterDatabase not found at {CharacterDbPath}");

        assets.levels = FindAssets<LevelData>("t:LevelData", LevelsFolder)
            .OrderBy(l => l.name, System.StringComparer.Ordinal)
            .ToList();

        assets.items = FindPrefabs(ItemFolders)
            .Where(p => p.GetComponent<Pickup>() != null)
            .OrderBy(p => AssetDatabase.GetAssetPath(p), System.StringComparer.Ordinal)
            .ToList();

        assets.plants = FindPrefabs(new[] { PlantsFolder })
            .Where(p => p.GetComponent<Plant>() != null)
            .OrderBy(p => AssetDatabase.GetAssetPath(p), System.StringComparer.Ordinal)
            .ToList();

        // Projectiles: anything with Fire/Watergun behaviour that is NOT a pickup tool.
        assets.projectiles = FindPrefabs(new[] { PrefabRootFolder })
            .Where(p => (p.GetComponent<Fire>() != null || p.GetComponent<Watergun>() != null) &&
                        p.GetComponent<Pickup>() == null)
            .OrderBy(p => AssetDatabase.GetAssetPath(p), System.StringComparer.Ordinal)
            .ToList();
    }

    private static void AssignItemIds(NetworkAssets assets)
    {
        for (int id = 0; id < assets.items.Count; id++)
        {
            string path = AssetDatabase.GetAssetPath(assets.items[id]);
            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            var pickup = scope.prefabContentsRoot.GetComponent<Pickup>();
            if (pickup != null && pickup.itemId != id)
                pickup.itemId = id;
        }
    }

    // ======================================================
    //  PREFAB COMPONENT INJECTION
    // ======================================================
    private static void SetupCharacterPrefabs(NetworkAssets assets)
    {
        foreach (var character in assets.Characters)
        {
            if (character == null || character.prefab == null)
            {
                Debug.LogWarning("[NGOSetup] CharacterDatabase contains an entry without a prefab.");
                continue;
            }

            string path = AssetDatabase.GetAssetPath(character.prefab);
            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            var root = scope.prefabContentsRoot;

            GetOrAdd<NetworkObject>(root);

            var cnt = GetOrAdd<ClientNetworkTransform>(root);
            cnt.SyncPositionX = true;
            cnt.SyncPositionY = true;
            cnt.SyncPositionZ = false;
            cnt.SyncRotAngleX = false;
            cnt.SyncRotAngleY = false;
            cnt.SyncRotAngleZ = false;
            cnt.SyncScaleX = false;
            cnt.SyncScaleY = false;
            cnt.SyncScaleZ = false;
            cnt.Interpolate = true;

            var animator = root.GetComponentInChildren<Animator>(true);
            var netAnim = GetOrAdd<OwnerNetworkAnimator>(root);
            if (animator != null)
                netAnim.Animator = animator;

            GetOrAdd<NetworkPlayer>(root);

            // Only the owning client may enable input (NetworkPlayer does it on spawn).
            var pi = root.GetComponent<PlayerInput>();
            if (pi != null)
                pi.enabled = false;

            Debug.Log($"[NGOSetup] Character prefab ready: {path}");
        }
    }

    private static void SetupPlantPrefabs(NetworkAssets assets)
    {
        foreach (var plant in assets.plants)
        {
            string path = AssetDatabase.GetAssetPath(plant);
            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            GetOrAdd<NetworkObject>(scope.prefabContentsRoot);
        }
    }

    private static void SetupItemPrefabs(NetworkAssets assets)
    {
        foreach (var item in assets.items)
        {
            string path = AssetDatabase.GetAssetPath(item);
            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            var root = scope.prefabContentsRoot;

            GetOrAdd<NetworkObject>(root);

            var nt = GetOrAdd<NetworkTransform>(root);
            nt.SyncPositionX = true;
            nt.SyncPositionY = true;
            nt.SyncPositionZ = false;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleY = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = false;
            nt.SyncScaleY = false;
            nt.SyncScaleZ = false;
            nt.Interpolate = true;
        }
    }

    // ======================================================
    //  RUNTIME PREFABS (session + bootstrap)
    // ======================================================
    private static GameObject EnsureSessionPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SessionPrefabPath);
        if (existing != null && existing.GetComponent<NetworkObject>() != null)
        {
            // Update in place: newer setup versions add components (telemetry etc).
            using (var scope = new PrefabUtility.EditPrefabContentsScope(SessionPrefabPath))
            {
                var root = scope.prefabContentsRoot;
                GetOrAdd<GameSession>(root);
                GetOrAdd<NetworkMetrics>(root);
            }
            Debug.Log($"[NGOSetup] Updated {SessionPrefabPath}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(SessionPrefabPath);
        }

        var go = new GameObject("GameSession");
        go.AddComponent<NetworkObject>();
        go.AddComponent<GameSession>();
        go.AddComponent<NetworkMetrics>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, SessionPrefabPath);
        Object.DestroyImmediate(go);
        Debug.Log($"[NGOSetup] Created {SessionPrefabPath}");
        return prefab;
    }

    private static void EnsureBootstrapPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPrefabPath);
        if (existing != null && existing.GetComponent<NetworkManager>() != null)
        {
            // Update in place: newer setup versions may add components (e.g. the
            // custom PersonalizedTransport next to UnityTransport).
            using var scope = new PrefabUtility.EditPrefabContentsScope(BootstrapPrefabPath);
            var root = scope.prefabContentsRoot;
            GetOrAdd<PersonalizedTransport>(root);
            GetOrAdd<KcpNgoTransport>(root);
            GetOrAdd<ConnectionManager>(root);
            GetOrAdd<NetworkOverlayUI>(root);
            Debug.Log($"[NGOSetup] Updated {BootstrapPrefabPath}");
            return;
        }

        var go = new GameObject("NetworkBootstrap");

        var transport = go.AddComponent<UnityTransport>();
        go.AddComponent<PersonalizedTransport>();
        go.AddComponent<KcpNgoTransport>();
        var nm = go.AddComponent<NetworkManager>();
        nm.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            ConnectionApproval = true,
            EnableSceneManagement = true,
        };

        go.AddComponent<ConnectionManager>();
        go.AddComponent<NetworkOverlayUI>();

        PrefabUtility.SaveAsPrefabAsset(go, BootstrapPrefabPath);
        Object.DestroyImmediate(go);
        Debug.Log($"[NGOSetup] Created {BootstrapPrefabPath}");
    }

    // ======================================================
    //  HELPERS
    // ======================================================
    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static List<T> FindAssets<T>(string filter, params string[] folders) where T : Object
    {
        return AssetDatabase.FindAssets(filter, folders)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(a => a != null)
            .ToList();
    }

    private static List<GameObject> FindPrefabs(string[] folders)
    {
        return AssetDatabase.FindAssets("t:Prefab", folders)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(p => p != null)
            .ToList();
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}

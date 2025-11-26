#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.IO;
using System.Collections.Generic;

public class LevelEditorWindow : EditorWindow
{
    private GridData currentLevel = new GridData();
    private string fileName = "leveltest.json";

    private TilePlacementHandler wall;
    private TilePlacementHandler land;
    private TilePlacementHandler water;
    private TilePlacementHandler concrete;

    [MenuItem("Window/Level Editor")]
    public static void OpenWindow()
    {
        GetWindow<LevelEditorWindow>("Level Editor");
    }

    private void OnEnable()
    {
        // Auto-detect handlers
        LevelObjectFactory factory = FindAnyObjectByType<LevelObjectFactory>();
        if (factory != null)
        {
            wall = factory.wallHandler;
            land = factory.landHandler;
            water = factory.waterHandler;
            concrete = factory.concreteHandler;
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("JSON Level Editor", EditorStyles.boldLabel);

        fileName = EditorGUILayout.TextField("Level JSON File", fileName);

        if (GUILayout.Button("Load Level"))
            LoadLevel();

        if (GUILayout.Button("Save Level"))
            SaveLevel();

        EditorGUILayout.HelpBox(
            "Use the Tile Palette to paint tiles.\n" +
            "This tool only loads and saves JSON.", MessageType.Info);
    }

    // ----------------------------------------------------------
    private void LoadLevel()
    {
        string path = Path.Combine(Application.dataPath, "Levels", fileName);
        if (!File.Exists(path))
        {
            Debug.LogError("JSON file not found: " + path);
            return;
        }

        string json = File.ReadAllText(path);
        currentLevel = JsonUtility.FromJson<GridData>(json);

        // Clear tilemaps
        wall?.Tilemap.ClearAllTiles();
        land?.Tilemap.ClearAllTiles();
        water?.Tilemap.ClearAllTiles();
        concrete?.Tilemap.ClearAllTiles();

        // Paint tiles
        foreach (var o in currentLevel.objects)
        {
            Vector3Int cell = new Vector3Int(o.x, o.y, 0);
            var type = (LevelObjectType)System.Enum.Parse(typeof(LevelObjectType), o.type);

            GetHandler(type)?.PlaceTile(cell, o.subtype);
        }

        Debug.Log("Loaded level " + fileName);
    }

    // ----------------------------------------------------------
    private void SaveLevel()
    {
        currentLevel.objects.Clear();

        SaveEntriesFrom(wall, LevelObjectType.Wall);
        SaveEntriesFrom(land, LevelObjectType.Land);
        SaveEntriesFrom(water, LevelObjectType.Water);
        SaveEntriesFrom(concrete, LevelObjectType.Concrete);

        string path = Path.Combine(Application.dataPath, "Levels", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        File.WriteAllText(path, JsonUtility.ToJson(currentLevel, true));
        Debug.Log("Saved to " + path);
    }

    // ----------------------------------------------------------
    private void SaveEntriesFrom(TilePlacementHandler handler, LevelObjectType type)
    {
        if (handler == null || handler.Tilemap == null)
            return;

        var bounds = handler.Tilemap.cellBounds;

        foreach (var pos in bounds.allPositionsWithin)
        {
            TileBase tile = handler.Tilemap.GetTile(pos);
            if (tile == null) continue;

            if (handler.TryGetSubtypeFromTile(tile, out string subtype))
            {
                currentLevel.objects.Add(new GridObjectData
                {
                    type = type.ToString(),
                    subtype = subtype,
                    x = pos.x,
                    y = pos.y
                });
            }
        }
    }

    private TilePlacementHandler GetHandler(LevelObjectType type)
    {
        return type switch
        {
            LevelObjectType.Wall => wall,
            LevelObjectType.Land => land,
            LevelObjectType.Water => water,
            LevelObjectType.Concrete => concrete,
            _ => null
        };
    }
}
#endif
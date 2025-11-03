#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System;
using System.Linq;

[CustomEditor(typeof(TilePlacementHandler))]
public class TilePlacementHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var handler = (TilePlacementHandler)target;

        // Draw default fields except the list
        handler.currentType = (LevelObjectType)EditorGUILayout.EnumPopup("Object Type", handler.currentType);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Tiles by Subtype", EditorStyles.boldLabel);

        // Determine enum based on selected type
        Type enumType = handler.currentType switch
        {
            LevelObjectType.Wall => typeof(WallVariant),
            LevelObjectType.Water => typeof(WaterVariant),
            LevelObjectType.Land => typeof(LandVariant),
            _ => null
        };

        if (enumType == null)
        {
            EditorGUILayout.HelpBox("No variant enum found for this type.", MessageType.Warning);
            return;
        }

        string[] variants = Enum.GetNames(enumType);

        // Ensure list matches enum count
        foreach (string variant in variants)
        {
            var entry = handler.tiles.FirstOrDefault(t => t.variant == variant);
            if (entry == null)
                handler.tiles.Add(new TilePlacementHandler.EnumTilePair { variant = variant });
        }

        // Draw each tile slot
        foreach (string variant in variants)
        {
            var entry = handler.tiles.First(t => t.variant == variant);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(variant, GUILayout.Width(100));
            entry.tile = (TileBase)EditorGUILayout.ObjectField(entry.tile, typeof(TileBase), false);
            EditorGUILayout.EndHorizontal();
        }

        if (GUI.changed)
            EditorUtility.SetDirty(handler);
    }
}
#endif

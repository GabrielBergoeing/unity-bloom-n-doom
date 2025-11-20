using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Game/Character")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string characterName;
    public Sprite portrait;

    [Header("Gameplay")]
    public GameObject prefab;

    [Header("UI Theme Override")]
    [Tooltip("Optional: Override panel colors when this character is selected/locked.")]
    public PanelColorTheme overrideTheme;
}

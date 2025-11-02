using UnityEngine;

[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Game/CharacterDatabase")]
public class CharacterDatabase : ScriptableObject
{
    public CharacterData[] characters;
}

using UnityEngine;

[CreateAssetMenu(fileName = "PanelTheme", menuName = "UI/Panel Theme")]
public class PanelColorTheme : ScriptableObject
{
    public PanelStateColor emptyColor;
    public PanelStateColor activeColor;
    public PanelStateColor lockedColor;

    [System.Serializable]
    public struct PanelStateColor
    {
        public Color background;
        public Color border;
        public Color text;
    }
}

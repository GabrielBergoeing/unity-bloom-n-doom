using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Immediate-mode overlay shown in the main menu:
///  - disconnected: HOST / JOIN (ip + port) panel,
///  - connected (lobby): player list with character selection, host picks map and starts.
/// Lives on the NetworkBootstrap prefab, so no scene changes are required.
/// </summary>
public class NetworkOverlayUI : MonoBehaviour
{
    private string ip = "127.0.0.1";
    private string portText = "7777";
    private int selectedLevel;
    private Vector2 basePos = new(20f, 20f);

    private GUIStyle titleStyle, labelStyle, boxStyle;
    private bool stylesReady;

    private static NetworkManager Nm => NetworkManager.Singleton;
    private static GameSession Session => GameSession.Instance;
    private static ConnectionManager Conn => ConnectionManager.Instance;

    private bool InMenuScene => SceneManager.GetActiveScene().name == ConnectionManager.MenuSceneName;

    private void OnGUI()
    {
        if (Conn == null || NetworkAssets.Instance == null) return;

        float scale = Mathf.Max(1f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        EnsureStyles();

        if (Nm == null || !Nm.IsListening)
        {
            if (InMenuScene)
                DrawConnectPanel();
            return;
        }

        if (Session == null || Session.State == GameSession.SessionState.Lobby)
            DrawLobbyPanel();
        else if (Session.State == GameSession.SessionState.Loading)
            DrawInfoBox("Loading match...");
    }

    private void EnsureStyles()
    {
        if (stylesReady) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 10, 10) };
        stylesReady = true;
    }

    // ======================================================
    //  DISCONNECTED: HOST / JOIN
    // ======================================================
    private void DrawConnectPanel()
    {
        GUILayout.BeginArea(new Rect(basePos.x, basePos.y, 330f, 280f), boxStyle);
        GUILayout.Label("PLAY ONLINE", titleStyle);
        GUILayout.Space(6f);

        GUILayout.BeginHorizontal();
        GUILayout.Label("IP", labelStyle, GUILayout.Width(60f));
        ip = GUILayout.TextField(ip, GUILayout.Width(180f));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Port", labelStyle, GUILayout.Width(60f));
        portText = GUILayout.TextField(portText, GUILayout.Width(80f));
        GUILayout.EndHorizontal();

        // Transport selection — must match on host and clients.
        GUILayout.BeginHorizontal();
        GUILayout.Label("Net", labelStyle, GUILayout.Width(60f));
        bool utpOn = Conn.transport == ConnectionManager.TransportKind.UnityTransport;
        if (GUILayout.Toggle(utpOn, " Unity (UTP)") && !utpOn)
            Conn.transport = ConnectionManager.TransportKind.UnityTransport;
        bool customOn = Conn.transport == ConnectionManager.TransportKind.Personalized;
        if (GUILayout.Toggle(customOn, " Custom UDP") && !customOn)
            Conn.transport = ConnectionManager.TransportKind.Personalized;
        bool kcpOn = Conn.transport == ConnectionManager.TransportKind.Kcp;
        if (GUILayout.Toggle(kcpOn, " KCP") && !kcpOn)
            Conn.transport = ConnectionManager.TransportKind.Kcp;
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);

        bool busy = Conn.IsConnecting;
        GUI.enabled = !busy;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("HOST", GUILayout.Height(32f)))
        {
            if (TryParsePort(out ushort port))
                Conn.StartHost(port);
        }
        if (GUILayout.Button("JOIN", GUILayout.Height(32f)))
        {
            if (TryParsePort(out ushort port))
                Conn.StartClient(ip.Trim(), port);
        }
        GUILayout.EndHorizontal();

        GUI.enabled = true;

        if (busy && GUILayout.Button("CANCEL", GUILayout.Height(26f)))
            Conn.Leave();

        GUILayout.Space(6f);
        GUILayout.Label(Conn.StatusMessage, labelStyle);
        GUILayout.Label("Host needs UDP port forwarded on their router.", labelStyle);
        GUILayout.EndArea();
    }

    private bool TryParsePort(out ushort port)
    {
        if (ushort.TryParse(portText.Trim(), out port) && port > 0)
            return true;
        port = 0;
        return false;
    }

    // ======================================================
    //  CONNECTED: LOBBY
    // ======================================================
    private void DrawLobbyPanel()
    {
        var assets = NetworkAssets.Instance;
        var characters = assets.Characters;

        GUILayout.BeginArea(new Rect(basePos.x, basePos.y, 400f, 460f), boxStyle);
        GUILayout.Label(Nm.IsHost ? "LOBBY (you are the host)" : "LOBBY", titleStyle);
        GUILayout.Label(Conn.StatusMessage, labelStyle);
        GUILayout.Space(6f);

        if (Session == null)
        {
            GUILayout.Label("Waiting for session...", labelStyle);
            GUILayout.EndArea();
            return;
        }

        // ---- players ----
        for (int i = 0; i < Session.lobbyPlayers.Count; i++)
        {
            var lp = Session.lobbyPlayers[i];
            bool isSelf = lp.clientId == Nm.LocalClientId;

            string charName = characters.Length > 0
                ? characters[Mathf.Clamp(lp.characterId, 0, characters.Length - 1)].characterName
                : "???";

            GUILayout.BeginHorizontal();
            GUILayout.Label($"P{i + 1}{(isSelf ? " (you)" : "")}", labelStyle, GUILayout.Width(90f));

            if (isSelf && characters.Length > 1)
            {
                if (GUILayout.Button("<", GUILayout.Width(28f)))
                    Session.SelectCharacterServerRpc(lp.characterId - 1);
                GUILayout.Label(charName, labelStyle, GUILayout.Width(140f));
                if (GUILayout.Button(">", GUILayout.Width(28f)))
                    Session.SelectCharacterServerRpc(lp.characterId + 1);
            }
            else
            {
                GUILayout.Label(charName, labelStyle, GUILayout.Width(200f));
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10f);

        // ---- map + start (host only) ----
        if (Nm.IsHost)
        {
            GUILayout.Label("Map:", labelStyle);
            for (int i = 0; i < assets.levels.Count; i++)
            {
                bool on = selectedLevel == i;
                bool clicked = GUILayout.Toggle(on, $" Map {i + 1}  ({assets.levels[i].name})");
                if (clicked && !on) selectedLevel = i;
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("START MATCH", GUILayout.Height(36f)))
                Session.HostStartMatch(selectedLevel);
        }
        else
        {
            GUILayout.Label("Waiting for the host to start the match...", labelStyle);
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("LEAVE", GUILayout.Height(26f)))
            Conn.Leave();

        GUILayout.EndArea();
    }

    private void DrawInfoBox(string text)
    {
        GUILayout.BeginArea(new Rect(basePos.x, basePos.y, 260f, 60f), boxStyle);
        GUILayout.Label(text, titleStyle);
        GUILayout.EndArea();
    }
}

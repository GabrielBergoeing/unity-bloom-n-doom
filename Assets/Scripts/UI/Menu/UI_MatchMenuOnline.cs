using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Online-only character selection lobby. No splitscreen / PlayerInput.
/// Each Mirror connection gets one slot. The server owns all state and
/// broadcasts a snapshot every time something changes. Clients send their
/// own selection to the server; the server applies it and re-broadcasts.
///
/// Put this component (and the four UI_CharacterSelector slot references)
/// on a GameObject in the CharacterSelectorOnline scene alongside the
/// NetworkManager. The SteamLobby / transport setup runs separately.
/// </summary>
public class UI_MatchMenuOnline : MonoBehaviour
{
    #region Inspector

    [Header("Character Slots (4 max)")]
    [SerializeField] private UI_CharacterSelector[] slots = new UI_CharacterSelector[4];

    [Header("Match Settings")]
    [Range(2, 4)] [SerializeField] private int minimumPlayers = 2;
    [SerializeField] private string gameplaySceneName = "MapSelectorOnline";

    [Header("Local Input")]
    [SerializeField] private float navCooldown   = 0.25f;
    [SerializeField] private float navThreshold  = 0.5f;

    #endregion

    #region Mirror Messages

    private struct SelectionSubmitMessage : NetworkMessage
    {
        public int  characterIndex;
        public bool locked;
    }

    private struct SelectionSnapshotMessage : NetworkMessage
    {
        public int[]  connectionIds;
        public int[]  characterIndexes;
        public bool[] lockedFlags;
    }

    private struct LocalSlotAssignmentMessage : NetworkMessage
    {
        public int slotIndex;
    }

    #endregion

    #region State

    private struct SelectionState
    {
        public int  characterIndex;
        public bool locked;
    }

    private readonly List<int>                     orderedConnectionIds = new();
    private readonly Dictionary<int, SelectionState> states             = new();

    private float nextNavigateTime;
    private bool  serverHandlerRegistered;
    private bool  clientHandlerRegistered;
    private bool  startTriggered;
    private int   localSlotIndex = -1;

    private UIService UI => UIService.instance;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        foreach (var s in slots)
            s?.SetEmptyVisuals();
    }

    private void OnDisable()
    {
        if (serverHandlerRegistered)
        {
            NetworkServer.UnregisterHandler<SelectionSubmitMessage>();
            serverHandlerRegistered = false;
        }

        if (clientHandlerRegistered)
        {
            NetworkClient.UnregisterHandler<SelectionSnapshotMessage>();
            NetworkClient.UnregisterHandler<LocalSlotAssignmentMessage>();
            clientHandlerRegistered = false;
            localSlotIndex = -1;
        }
    }

    private void Update()
    {
        // NetworkClient/NetworkServer wipe their own handler registries on
        // Stop*()/Shutdown() - but don't tell us, so our "already registered" flags
        // would otherwise go stale. SteamLobby's join flow calls StartClient()/
        // StopClient() repeatedly while cascading through LAN -> public IP -> hole
        // punch tiers, so this isn't a one-off: only the FIRST tier that's ever tried
        // gets real handlers: RegisterHandler() below never re-runs for the actually-
        // successful tier if it wasn't the first one Mirror wiped in between.
        if (!NetworkClient.active) clientHandlerRegistered = false;
        if (!NetworkServer.active) serverHandlerRegistered = false;

        EnsureHandlers();

        if (NetworkServer.active)
        {
            SyncServerRoster();
            TryStartMatch();
        }

        if (NetworkClient.active && NetworkClient.connection != null)
            HandleLocalInput();
    }

    #endregion

    #region Handler Registration

    private void EnsureHandlers()
    {
        if (NetworkServer.active && !serverHandlerRegistered)
        {
            NetworkServer.RegisterHandler<SelectionSubmitMessage>(OnSelectionSubmitted, false);
            serverHandlerRegistered = true;
        }

        if (NetworkClient.active && !clientHandlerRegistered)
        {
            NetworkClient.RegisterHandler<SelectionSnapshotMessage>(OnSelectionSnapshot, false);
            NetworkClient.RegisterHandler<LocalSlotAssignmentMessage>(OnLocalSlotAssigned, false);
            clientHandlerRegistered = true;
        }
    }

    #endregion

    #region Server — Roster & State

    private void SyncServerRoster()
    {
        bool changed = false;
        var  connectedIds = new HashSet<int>();

        foreach (var kvp in NetworkServer.connections)
        {
            if (kvp.Value == null) continue;
            int id = kvp.Key;
            connectedIds.Add(id);

            if (states.ContainsKey(id)) continue;
            states[id] = new SelectionState { characterIndex = 0, locked = false };
            orderedConnectionIds.Add(id);
            changed = true;
        }

        for (int i = orderedConnectionIds.Count - 1; i >= 0; i--)
        {
            int id = orderedConnectionIds[i];
            if (connectedIds.Contains(id)) continue;
            orderedConnectionIds.RemoveAt(i);
            states.Remove(id);
            changed = true;
        }

        if (changed) BroadcastSnapshot();
    }

    private void OnSelectionSubmitted(NetworkConnectionToClient conn, SelectionSubmitMessage msg)
    {
        int count = GetCharacterCount();
        if (count <= 0) return;

        int id = conn.connectionId;
        if (!states.ContainsKey(id))
        {
            states[id] = new SelectionState();
            orderedConnectionIds.Add(id);
        }

        SelectionState state = states[id];
        state.characterIndex = Mathf.Clamp(msg.characterIndex, 0, count - 1);
        state.locked         = msg.locked;
        states[id]           = state;

        BroadcastSnapshot();
    }

    private void BroadcastSnapshot()
    {
        int count = orderedConnectionIds.Count;

        var snapshot = new SelectionSnapshotMessage
        {
            connectionIds    = new int[count],
            characterIndexes = new int[count],
            lockedFlags      = new bool[count]
        };

        for (int i = 0; i < count; i++)
        {
            int id            = orderedConnectionIds[i];
            SelectionState st = states[id];
            snapshot.connectionIds[i]    = id;
            snapshot.characterIndexes[i] = st.characterIndex;
            snapshot.lockedFlags[i]      = st.locked;

            if (NetworkServer.connections.TryGetValue(id, out NetworkConnectionToClient conn) && conn != null)
                conn.Send(new LocalSlotAssignmentMessage { slotIndex = i });
        }

        NetworkServer.SendToAll(snapshot);
        OnSelectionSnapshot(snapshot);   // apply locally on host
    }

    #endregion

    #region Client — Receive Snapshot

    private void OnSelectionSnapshot(SelectionSnapshotMessage snapshot)
    {
        orderedConnectionIds.Clear();
        states.Clear();

        if (snapshot.connectionIds == null)
        {
            ApplyStateToSlots();
            return;
        }

        int count = Mathf.Min(snapshot.connectionIds.Length,
                              snapshot.characterIndexes.Length,
                              snapshot.lockedFlags.Length);

        for (int i = 0; i < count; i++)
        {
            int id = snapshot.connectionIds[i];
            orderedConnectionIds.Add(id);
            states[id] = new SelectionState
            {
                characterIndex = snapshot.characterIndexes[i],
                locked         = snapshot.lockedFlags[i]
            };
        }

        ApplyStateToSlots();
    }

    private void OnLocalSlotAssigned(LocalSlotAssignmentMessage msg)
    {
        localSlotIndex = msg.slotIndex;
    }

    #endregion

    #region Slot Visuals

    private void ApplyStateToSlots()
    {
        if (slots == null) return;

        foreach (var slot in slots)
            slot?.ClearRemoteSelection();

        for (int i = 0; i < orderedConnectionIds.Count && i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            int id = orderedConnectionIds[i];
            SelectionState st = states[id];
            slots[i].SetRemoteSelection(st.characterIndex, st.locked);
        }
    }

    #endregion

    #region Local Input

    private void HandleLocalInput()
    {
        if (localSlotIndex < 0 || localSlotIndex >= orderedConnectionIds.Count) return;

        int localId = orderedConnectionIds[localSlotIndex];
        if (!states.TryGetValue(localId, out SelectionState localState)) return;

        if (!localState.locked && Time.unscaledTime >= nextNavigateTime)
        {
            float nav = ReadNavigateInput();
            if (Mathf.Abs(nav) >= navThreshold)
            {
                int count = GetCharacterCount();
                if (count > 0)
                {
                    int delta              = nav > 0f ? 1 : -1;
                    localState.characterIndex = (localState.characterIndex + delta + count) % count;
                    SubmitLocalState(localState);
                    nextNavigateTime = Time.unscaledTime + navCooldown;
                }
            }
        }

        if (!localState.locked && ReadConfirmInput())
        {
            UI?.sfx.PlayOnConfirm();
            localState.locked = true;
            SubmitLocalState(localState);
        }
        else if (localState.locked && ReadCancelInput())
        {
            localState.locked = false;
            SubmitLocalState(localState);
        }
    }

    private void SubmitLocalState(SelectionState state)
    {
        NetworkClient.Send(new SelectionSubmitMessage
        {
            characterIndex = state.characterIndex,
            locked         = state.locked
        });
    }

    private float ReadNavigateInput()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
                return  1f;
            if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
                return -1f;
        }

        if (Gamepad.current == null) return 0f;
        float stick = Gamepad.current.leftStick.ReadValue().y;
        float dpad  = Gamepad.current.dpad.ReadValue().y;
        return Mathf.Abs(dpad) > Mathf.Abs(stick) ? dpad : stick;
    }

    private bool ReadConfirmInput()
    {
        bool kb = Keyboard.current != null &&
                  (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);
        bool gp = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        return kb || gp;
    }

    // Keyboard 'C' / Gamepad East match the shared UI "Cancel" action used elsewhere
    // (UI_CharacterSelector/UI_PlayerSlot). This used to read Escape/Backspace, which
    // collided with the unrelated global "quit"/"back to menu" shortcuts on this exact
    // screen - pressing them only unlocks this player's selection, it doesn't leave.
    private bool ReadCancelInput()
    {
        bool kb = Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
        bool gp = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
        return kb || gp;
    }

    #endregion

    #region Match Start (server only)

    private void TryStartMatch()
    {
        if (startTriggered) return;
        if (orderedConnectionIds.Count < minimumPlayers) return;

        foreach (int id in orderedConnectionIds)
        {
            if (!states.TryGetValue(id, out SelectionState st) || !st.locked)
                return;
        }

        UI_CharacterSelector selectorWithDb = slots?.FirstOrDefault(s => s != null && s.CharacterCount > 0);
        if (selectorWithDb == null) return;

        var selected = orderedConnectionIds
            .Select(id => states.TryGetValue(id, out SelectionState st)
                          ? selectorWithDb.GetCharacterAtIndex(st.characterIndex)
                          : null)
            .ToList();

        PlayerInputService.instance?.StoreOnlineSelections(selected);

        // Give OnlineNetworkManager the connection→slot mapping so OnServerAddPlayer
        // knows which character prefab to spawn for each connection.
        if (NetworkManager.singleton is OnlineNetworkManager onlineMgr)
            onlineMgr.StoreConnectionSlots(orderedConnectionIds);

        startTriggered = true;
        UI?.sfx.PlayOnConfirm();
        NetworkManager.singleton?.ServerChangeScene(gameplaySceneName);
    }

    #endregion

    #region Helpers

    private int GetCharacterCount()
    {
        if (slots == null) return 0;
        foreach (var s in slots)
            if (s != null && s.CharacterCount > 0) return s.CharacterCount;
        return 0;
    }

    #endregion
}

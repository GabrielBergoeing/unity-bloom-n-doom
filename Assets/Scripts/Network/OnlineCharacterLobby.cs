using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class OnlineCharacterLobby : MonoBehaviour
{
    [Header("Slots")]
    [SerializeField] private UI_CharacterSelector[] slots = new UI_CharacterSelector[4];

    [Header("Local Input")]
    [SerializeField] private float navCooldown = 0.25f;
    [SerializeField] private float navThreshold = 0.5f;

    [Header("Match Start")]
    [SerializeField, Range(1, 4)] private int minimumPlayers = 2;
    [SerializeField] private bool autoStartWhenAllLocked = true;
    [SerializeField] private string gameplaySceneName = "MapSelector";

    private readonly List<int> orderedConnectionIds = new();
    private readonly Dictionary<int, SelectionState> states = new();

    private float nextNavigateTime;
    private bool serverHandlerRegistered;
    private bool clientHandlerRegistered;
    private bool startTriggered;
    private int localSlotIndex = -1;

    private struct SelectionState
    {
        public int characterIndex;
        public bool locked;
    }

    private struct SelectionSubmitMessage : NetworkMessage
    {
        public int characterIndex;
        public bool locked;
    }

    private struct SelectionSnapshotMessage : NetworkMessage
    {
        public int[] connectionIds;
        public int[] characterIndexes;
        public bool[] lockedFlags;
    }

    private struct LocalSlotAssignmentMessage : NetworkMessage
    {
        public int slotIndex;
    }

    private void Start()
    {
        ApplyStateToSlots();
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
        EnsureHandlers();

        if (NetworkServer.active)
        {
            SyncServerRoster();

            if (autoStartWhenAllLocked)
                TryStartMatch();
        }

        if (NetworkClient.active && NetworkClient.connection != null)
        {
            HandleLocalInput();
        }
    }

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

    private void SyncServerRoster()
    {
        bool changed = false;
        HashSet<int> connectedIds = new();

        foreach (var kvp in NetworkServer.connections)
        {
            if (kvp.Value == null)
                continue;

            int id = kvp.Key;
            connectedIds.Add(id);

            if (!states.ContainsKey(id))
            {
                states[id] = new SelectionState { characterIndex = 0, locked = false };
                orderedConnectionIds.Add(id);
                changed = true;
            }
        }

        for (int i = orderedConnectionIds.Count - 1; i >= 0; i--)
        {
            int id = orderedConnectionIds[i];
            if (connectedIds.Contains(id))
                continue;

            orderedConnectionIds.RemoveAt(i);
            states.Remove(id);
            changed = true;
        }

        if (changed)
            BroadcastSnapshot();
    }

    private void OnSelectionSubmitted(NetworkConnectionToClient conn, SelectionSubmitMessage message)
    {
        int characterCount = GetCharacterCount();
        if (characterCount <= 0)
            return;

        int connectionId = conn.connectionId;
        if (!states.ContainsKey(connectionId))
        {
            states[connectionId] = new SelectionState { characterIndex = 0, locked = false };
            orderedConnectionIds.Add(connectionId);
        }

        SelectionState state = states[connectionId];
        state.characterIndex = Mathf.Clamp(message.characterIndex, 0, characterCount - 1);
        state.locked = message.locked;

        states[connectionId] = state;
        BroadcastSnapshot();
    }

    private void BroadcastSnapshot()
    {
        int count = orderedConnectionIds.Count;

        SelectionSnapshotMessage snapshot = new SelectionSnapshotMessage
        {
            connectionIds = new int[count],
            characterIndexes = new int[count],
            lockedFlags = new bool[count]
        };

        for (int i = 0; i < count; i++)
        {
            int id = orderedConnectionIds[i];
            SelectionState state = states[id];

            snapshot.connectionIds[i] = id;
            snapshot.characterIndexes[i] = state.characterIndex;
            snapshot.lockedFlags[i] = state.locked;

            if (NetworkServer.connections.TryGetValue(id, out NetworkConnectionToClient targetConn) && targetConn != null)
            {
                targetConn.Send(new LocalSlotAssignmentMessage { slotIndex = i });
            }
        }

        NetworkServer.SendToAll(snapshot);
        OnSelectionSnapshot(snapshot);
    }

    private void OnSelectionSnapshot(SelectionSnapshotMessage snapshot)
    {
        orderedConnectionIds.Clear();
        states.Clear();

        if (snapshot.connectionIds == null || snapshot.characterIndexes == null || snapshot.lockedFlags == null)
        {
            ApplyStateToSlots();
            return;
        }

        int count = Mathf.Min(snapshot.connectionIds.Length, snapshot.characterIndexes.Length, snapshot.lockedFlags.Length);
        for (int i = 0; i < count; i++)
        {
            int id = snapshot.connectionIds[i];
            orderedConnectionIds.Add(id);

            states[id] = new SelectionState
            {
                characterIndex = snapshot.characterIndexes[i],
                locked = snapshot.lockedFlags[i]
            };
        }

        ApplyStateToSlots();
    }

    private void OnLocalSlotAssigned(LocalSlotAssignmentMessage message)
    {
        localSlotIndex = message.slotIndex;
    }

    private void ApplyStateToSlots()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            slots[i].ClearRemoteSelection();
        }

        for (int i = 0; i < orderedConnectionIds.Count && i < slots.Length; i++)
        {
            UI_CharacterSelector slot = slots[i];
            if (slot == null)
                continue;

            int id = orderedConnectionIds[i];
            SelectionState state = states[id];
            slot.SetRemoteSelection(state.characterIndex, state.locked);
        }
    }

    private void HandleLocalInput()
    {
        if (localSlotIndex < 0 || localSlotIndex >= orderedConnectionIds.Count)
            return;

        int localConnectionId = orderedConnectionIds[localSlotIndex];
        if (!states.TryGetValue(localConnectionId, out SelectionState localState))
            return;

        if (Time.unscaledTime >= nextNavigateTime && !localState.locked)
        {
            float nav = ReadNavigateInput();
            if (Mathf.Abs(nav) >= navThreshold)
            {
                int count = GetCharacterCount();
                if (count > 0)
                {
                    int delta = nav > 0f ? 1 : -1;
                    localState.characterIndex = (localState.characterIndex + delta + count) % count;
                    SubmitLocalState(localState);
                    nextNavigateTime = Time.unscaledTime + navCooldown;
                }
            }
        }

        if (!localState.locked && ReadConfirmInput())
        {
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
            locked = state.locked
        });
    }

    private float ReadNavigateInput()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
                return 1f;

            if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
                return -1f;
        }

        if (Gamepad.current == null)
            return 0f;

        float stick = Gamepad.current.leftStick.ReadValue().y;
        float dpad = Gamepad.current.dpad.ReadValue().y;
        return Mathf.Abs(dpad) > Mathf.Abs(stick) ? dpad : stick;
    }

    private bool ReadConfirmInput()
    {
        bool keyboard = Keyboard.current != null &&
                        (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);

        bool gamepad = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;

        return keyboard || gamepad;
    }

    private bool ReadCancelInput()
    {
        bool keyboard = Keyboard.current != null &&
                        (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame);

        bool gamepad = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;

        return keyboard || gamepad;
    }

    private int GetCharacterCount()
    {
        if (slots == null)
            return 0;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            if (slots[i].CharacterCount > 0)
                return slots[i].CharacterCount;
        }

        return 0;
    }

    private void TryStartMatch()
    {
        if (startTriggered)
            return;

        if (orderedConnectionIds.Count < minimumPlayers)
            return;

        for (int i = 0; i < orderedConnectionIds.Count; i++)
        {
            int id = orderedConnectionIds[i];
            if (!states.TryGetValue(id, out SelectionState state) || !state.locked)
                return;
        }

        NetworkManager manager = NetworkManager.singleton;
        if (manager == null || string.IsNullOrWhiteSpace(gameplaySceneName))
            return;

        PlayerInputService inputService = PlayerInputService.instance;
        if (inputService != null)
            inputService.StoreOnlineSelections(BuildOrderedCharacterSelection());

        startTriggered = true;
        manager.ServerChangeScene(gameplaySceneName);
    }

    private List<CharacterData> BuildOrderedCharacterSelection()
    {
        UI_CharacterSelector selectorWithDatabase = slots?.FirstOrDefault(s => s != null && s.CharacterCount > 0);
        List<CharacterData> selected = new();

        if (selectorWithDatabase == null)
            return selected;

        for (int i = 0; i < orderedConnectionIds.Count; i++)
        {
            int id = orderedConnectionIds[i];
            if (!states.TryGetValue(id, out SelectionState state))
                continue;

            CharacterData data = selectorWithDatabase.GetCharacterAtIndex(state.characterIndex);
            selected.Add(data);
        }

        return selected;
    }
}

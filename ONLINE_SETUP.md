# Bloom & Doom — Online Mode (Netcode for GameObjects)

The game is now **online-only** using Unity's Netcode for GameObjects (NGO) with
the Unity Transport (direct **IP + port**, designed for port forwarding).
Split screen is gone: every player runs their own client with a full-screen camera.

## One-time setup (in the Unity editor)

1. Open the project — Unity resolves the new `com.unity.netcode.gameobjects` package
   automatically (added to `Packages/manifest.json`).
2. Run **Tools > NGO Setup > Run Full Setup** (top menu). This is idempotent and:
   - creates `Assets/Resources/NetworkAssets.asset` — the id registry for characters,
     levels, items, plants and projectiles (item ids are rewritten to match),
   - adds `NetworkObject` + `ClientNetworkTransform` + `OwnerNetworkAnimator` +
     `NetworkPlayer` to the 5 character prefabs and disables their `PlayerInput`
     (only the owning client enables it at spawn),
   - adds `NetworkObject` to plant prefabs and `NetworkObject` + `NetworkTransform`
     to seed/tool prefabs,
   - creates `Assets/Resources/GameSession.prefab` and
     `Assets/Resources/NetworkBootstrap.prefab` (NetworkManager + UnityTransport +
     ConnectionManager + overlay UI).
3. Save the project. Done — no scene changes are needed; the network bootstrap
   auto-spawns at startup from `Resources`.

## How to play

1. Start the game — in the **MainMenu** scene an overlay appears (top-left):
   - **HOST**: starts listening on the chosen UDP port (default 7777).
   - **JOIN**: connects to the host's IP + port.
   - **Net**: transport selection — `Unity (UTP)` (default), `Custom UDP`
     (the hand-rolled `PersonalizedTransport`, ported from the Mirror branch), or
     `KCP` (kcp2k, the same library Mirror's KCP transport uses, via an NGO
     adapter). **Host and clients must pick the same transport** — they speak
     different wire protocols, so a mismatch just times out.
2. In the lobby each player picks a character with the `<` / `>` buttons.
   The host picks the map and presses **START MATCH**.
3. The match plays like before: move, prepare ground, plant, water, pick up items,
   sabotage. The server (host) owns all farm/plant/item state; movement is
   client-authoritative per player.
4. When the timer ends, everyone sees the results screen. Leaving (results/pause
   buttons) disconnects and returns to the main menu. If the host leaves, the
   session ends for everyone.

### Port forwarding

- The host forwards the chosen **UDP** port (default `7777`) on their router to
  their machine, then shares their **public IP** with friends.
- Same-LAN players can join with the host's LAN IP directly.
- Quick local test: run two instances on one machine and join `127.0.0.1:7777`
  (use ParrelSync or a built player + the editor).

## Architecture notes

| Piece | Authority | How |
|---|---|---|
| Player movement/animation | Owner client | `ClientNetworkTransform` + `OwnerNetworkAnimator`; remote copies disable input/logic and go kinematic |
| Farm tiles (prepare/seed/clear) | Server | `GameSession` ServerRpcs validate, ClientRpcs mirror tilemap + `FarmManager` dictionaries everywhere |
| Plants (growth, wither, fire, death) | Server | `Plant` is a `NetworkBehaviour`; server simulates, `NetworkVariable`s drive visuals |
| Items in the world | Server | Server-spawned `NetworkObject`s (`EventManager` spawns only on the server) |
| Inventory/hotbar | Owner client | Id + count based; the held item id replicates so others see it in your hand |
| Projectiles (water/fire) | Server | Server simulates gameplay copies; all peers spawn visual-only clones |
| Match timer / results | Server | `GameSession.matchTimer` NetworkVariable; results broadcast via ClientRpc |

Key scripts: `Assets/Scripts/Network/` (`ConnectionManager`, `GameSession`,
`NetworkPlayer`, `NetworkAssets`, `NetworkOverlayUI`, `NetworkBootstrap`) and
`Assets/Editor/NGOSetupWizard.cs`.

## Telemetry (thesis measurements)

`NetworkMetrics` (on the GameSession prefab, ported from the Mirror branch) records
on every peer, from the lobby through the match:

- **RTT / jitter / loss** via an RPC ping-pong (0.5s interval, 50-sample window,
  jitter = sample standard deviation — same definitions as the Mirror module).
- **Divergence_units**: distance between your local player and the server's
  replicated view of it (echoed in each pong). Includes movement during ~RTT.
- **Corrections_total / LastCorrection_units**: visual snap corrections detected
  on remote players (frame jumps > 1.0 world units, configurable).
- **ActionLatency_ms**: prepare/plant request → mirrored tile change applied locally.
- **BytesSent/BytesRecv**: Custom UDP = wire bytes (headers, acks, resends
  included); KCP = NGO payload bytes; UTP = not instrumented (-1).

Keys: **F7** start/stop capture (auto-starts on connect), **F8** export CSV.
Files land in `%USERPROFILE%\AppData\LocalLow\<company>\<product>\`
as `NetworkMetrics_NGO_<transport>_<datetime>.csv`; export also runs automatically
on disconnect. The first 8 CSV columns are identical to the Mirror-branch module
so both frameworks can share one analysis script; NGO rows append
`Framework,Transport,...` columns.

## Known limitations

- The old local-multiplayer flow (MatchMenu scene, split screen, device joining)
  is bypassed; use the overlay to play. Offline test scenes may still work but are
  not maintained.
- Seeds/fertilizer are consumed optimistically on the client; if the server
  rejects the action (e.g. two players plant the same cell in the same instant)
  the item can be lost. Rare in practice.
- After a match the session ends; host again for a rematch.
- Pause is local-only online (the world keeps running), as usual for online games.

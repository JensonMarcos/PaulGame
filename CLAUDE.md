# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity 6 (6000.3.6f1) URP multiplayer party shooter. Netcode for GameObjects 2.8 over Steam (Facepunch transport), server-authoritative with the host acting as server. All gameplay code lives under [Assets/Scripts/](Assets/Scripts/); third-party code sits in [Assets/Packages/](Assets/Packages/) (Kinematic Character Controller, NaughtyAttributes, QuickOutline, FastIK) and [Assets/Networking/Steamworks/](Assets/Networking/Steamworks/).

## Commands

Compile-check C# without opening the Editor:

```
dotnet build Assembly-CSharp.csproj -nologo -v q -p:WarningLevel=0
```

This is the only fast feedback loop available from the CLI. There is no test suite; the `*Test.cs` files under NaughtyAttributes are vendor samples, not project tests. Everything else (entering play mode, multiplayer play mode, building) happens in the Unity Editor. The `.csproj` files at the repo root are Editor-generated and should never be hand-edited.

Multiplayer is exercised through Unity's Multiplayer Play Mode package or by launching builds from [Builds/](Builds/).

## Scene and startup flow

`MainMenu` scene holds the persistent singletons. SteamManager waits for the NetworkManager, then drives lobby creation and joining, and calls `StartHost` or `StartClient` itself from the Steam lobby callbacks. Host starts the game by network-loading the `Level1` scene. SceneManager listens for that load to complete and spawns the PlayerManager prefab, which in turn spawns a player object per connected client. GameManager already lives in `Level1` and waits for PlayerManager to exist before initializing (see the `LobbyStart` call inside `FixedUpdate`).

`Test` scene is a scratch scene for isolated feature work.

## Core architecture

**Manager singletons.** GameManager, PlayerManager, SoundManager, VFXManager, SteamManager, and MenuManager each expose a static `instance`. All of them can be null depending on scene and spawn order, and the existing code null-checks them constantly. Preserve that habit rather than assuming a manager is present.

**Game loop.** [GameManager.cs](Assets/Scripts/Multiplayer/Game/GameManager.cs) runs a `GameState` machine (Lobby → MoveRoom → GameStart → InGame → GameEnd → back to MoveRoom) entirely on the server, driven from `FixedUpdate`. Assigning to the `GameState` property fires `OnGameStateChange`, which is where all transition side effects belong.

**Rooms.** The map is an endless chain of room prefabs. The `Rooms` struct holds previous/current/next; each transition despawns the oldest room, instantiates the next one at the current room's `nextRoomPoint`, and picks its gamemode by weighted random with a bonus applied to modes that were not picked. Players who fail to reach the new room when its door closes are killed by world damage. Room-local spawns, doors, and the trigger volume that tracks `playersInRoom` live in [Room.cs](Assets/Scripts/Multiplayer/Game/Room.cs).

**Gamemodes.** A `GameMode` is a serializable struct of flags configured per-entry in the GameManager inspector (win condition, damage, teams, timer, starting items). Modes needing custom logic add a `GamemodeScript` component on the room prefab, referenced by `Room.gamemodeScript`; override `OnGameModeStart`, `OnGameModeFixedUpdate`, and `OnGameModeEnd`. These run server-side only. [DontHoldTheC4.cs](Assets/Scripts/Multiplayer/Game/GamemodeScripts/DontHoldTheC4.cs) is the reference implementation and documents its own required inspector setup in a comment.

**Player split.** [Player.cs](Assets/Scripts/Player/Player.cs) is the NetworkBehaviour and the only place RPCs live for a player. It owns a `PlayerState` struct replicated through one `NetworkVariable` with owner write permission, hand-packed and quantized to bytes in `NetworkSerialize`, and only assigned when `Equals` reports an actual change. The subsystems it drives are plain MonoBehaviours: PlayerCharacter (movement via Kinematic Character Controller), PlayerCamera, PlayerCombat, PlayerInventory, PlayerAnimations, PlayerUI. Input is read once per frame in `HandleInputs` and pushed into each subsystem as an input struct. Character motion runs in `Update`, state is refreshed once in `LateUpdate` after the motor has moved.

**Authority model.** Clients simulate their own shooting, hit detection, and inventory locally, then report to the server: PlayerCombat raycasts locally and calls `DealDamageServerRpc`. The server owns health, kills, deaths, score, teams, respawns, and item spawning in [PlayerManager.cs](Assets/Scripts/Multiplayer/PlayerManager.cs), and pushes results back with owner-targeted RPCs. Cosmetic effects are broadcast with the `RpcTarget.Not(sender)` pattern so the acting client does not double-play its own effect.

**Items.** Each item is two objects. The networked [Item.cs](Assets/Scripts/Items/Item.cs) exists in the world, holds ammo and pickup ownership, and is hidden rather than despawned while carried. The `clientPrefab` version ([ItemClient.cs](Assets/Scripts/Items/ItemClient.cs)) is what a player actually holds, and its behaviour comes from an `IItemAction` component (Swing, HandsPunch, C4swing). Stats are ScriptableObjects in [Assets/Scripts/Items/ItemData/](Assets/Scripts/Items/ItemData/), and [ItemList.cs](Assets/Scripts/Items/ItemList.cs) maps integer item ids to prefabs. Gamemodes and crates reference items by that id, never by prefab.

**Props.** [NetworkProp.cs](Assets/Scripts/Multiplayer/Game/NetworkProp.cs) keeps physics server-only and takes forces via RPC. [PredictedProp.cs](Assets/Scripts/Multiplayer/Game/PredictedProp.cs) simulates locally on every client and reconciles against server state broadcast on the network tick; it ignores remote player capsules through the static `Player.RemoteColliders` list.

**FX.** [SoundManager.cs](Assets/Scripts/FX/SoundManager.cs) and [VFXManager.cs](Assets/Scripts/FX/VFXManager.cs) are pooled. Sounds are played by string name through `SoundManager.Play`, resolved against a SoundLibrary asset; entries flagged networked replicate themselves, so callers should not wrap them in their own RPCs.

## Conventions

- Any new networked prefab must be registered in [DefaultNetworkPrefabs.asset](Assets/DefaultNetworkPrefabs.asset).
- Server-only methods begin with `if (!IsServer) return;`; owner-only paths check `IsOwner`.
- Debug-only shortcuts are wrapped in `#if UNITY_EDITOR` (L ends the round, P respawns a dead player).
- ItemData fields use NaughtyAttributes `[ShowIf]` keyed to the item type helpers at the top of the class; follow that when adding fields.
- The codebase is deliberately informal, with blunt comments marking known-hacky code. Match the surrounding style and leave those markers alone.

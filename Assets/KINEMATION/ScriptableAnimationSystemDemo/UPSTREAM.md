# Scriptable Animation System Demo

- Source: https://github.com/kinemation/scriptable-animation-system
- Commit: `a6c6db8b6cfba835e3cdeb0c17f9e52bd91fbe4e`
- Imported subset: upstream `Assets/Demo` only
- Excluded: upstream `Packages`, `ProjectSettings`, and `Assets/DefaultNetworkPrefabs.asset`

The project already provides the required KINEMATION FPS Animation Framework,
Procedural Recoil Animation System, Shared runtime, and Unity Input System.

Local compatibility change: `Scripts/Runtime/MainMenu.cs` explicitly uses
`UnityEngine.InputSystem.PlayerInput` to avoid a name collision with the
project-generated `PlayerInput` input-actions wrapper.

The upstream repository did not contain a `LICENSE` file at the pinned commit.
Keep this import for local study unless redistribution rights are confirmed.

# Production NGO gameplay integration

This folder contains the editable Unity assets for the production network path.

- `Prefabs/NetworkSessionPlayer.prefab` is the invisible NGO player/identity record.
- `Settings/GameNetworkPrefabs.asset` registers the session player and the existing gameplay `PachinkoBall.prefab`.
- `SampleScene` contains **Production Network Manager** with `NetworkManager`, `UnityTransport`, and `NetworkGameBootstrap`.
- `PachinkoBall.prefab` remains under `Assets/GameObjects/Prefabs` and is visible/editable in the normal gameplay workflow. Its `NetworkTransform` uses **Owner** authority and `NetworkRigidbody` makes non-owner copies kinematic.

The dedicated build starts the NGO server immediately. The WebGL build waits for `DiscordHandler` to deliver `discordId`, `username`, and `instanceId` before connecting. The current connection payload is a development identity; a trusted Node-issued ticket is intentionally deferred.

Use the Unity menu **Pochinki -> Network Game** to refresh assets or build the server/WebGL pair:

- **Build -> Release** is the normal Discord build: no development instrumentation, a 60 FPS Web cap, disabled Web-only shadows/post-processing/HDR/motion vectors, and instanced Pixel rendering.
- **Build -> Development** keeps diagnostics and profiling support for troubleshooting.

Both targets in a pair must be rebuilt from the same commit. Command-line automation keeps
`-networkGameBuildAll` as Development and adds `-networkGameBuildRelease` for Release.
Each build recreates its exact output directory first, so old Development files cannot leak into Release output.

The regular Pixel/Pachinko/Canon prefabs remain the visual authoring source. Production Web builds batch Pixel visuals and disable physics that is irrelevant to the current network role; the logical objects and their Inspector-facing components remain intact.

# Production NGO gameplay integration

This folder contains the editable Unity assets for the production network path.

- `Prefabs/NetworkSessionPlayer.prefab` is the invisible NGO player/identity record.
- `Settings/GameNetworkPrefabs.asset` registers the session player and the existing gameplay `PachinkoBall.prefab`.
- `SampleScene` contains **Production Network Manager** with `NetworkManager`, `UnityTransport`, and `NetworkGameBootstrap`.
- `PachinkoBall.prefab` remains under `Assets/GameObjects/Prefabs` and is visible/editable in the normal gameplay workflow. Its `NetworkTransform` uses **Owner** authority and `NetworkRigidbody` makes non-owner copies kinematic.

The dedicated build starts the NGO server immediately. The WebGL build waits for `DiscordHandler` to deliver `discordId`, `username`, and `instanceId` before connecting. The current connection payload is a development identity; a trusted Node-issued ticket is intentionally deferred.

Use the Unity menu **Pochinki → Network Game** to refresh assets or build the server/WebGL pair. The separate `NGOPhysicsSpike` scene remains unchanged as a regression test.

# NGO dedicated-server spike

This is a small, isolated multiplayer proof of concept. It does not replace the
current game scene or the existing Discord roster bridge.

## What is authoritative

- The Windows dedicated server owns player movement and the shared Rigidbody ball.
- WebGL clients only send movement and `Space` input through NGO RPCs.
- `NetworkTransform` replicates the server positions to every client.
- `NetworkVariable` replicates the ball impulse counter and its source.
- Unity Transport carries NGO traffic over WebSocket on port `7777`, path `/ngo-ws`.

Vite proxies the browser's same-origin `/ngo-ws` WebSocket to the dedicated server.
That makes the same build use `ws://` locally and `wss://` through Cloudflare and
Discord without putting a tunnel hostname into Unity assets.

## Editable Unity assets

Open `Scenes/NGOPhysicsSpike.unity`. The environment, Canvas/HUD, NetworkManager,
network prefabs, materials, colliders, and Rigidbody are regular serialized Unity
objects visible in the Hierarchy, Inspector, and Project windows.

The generator is available at `Pochinki > NGO Spike > Generate Scene And Prefabs`.
It intentionally rewrites only the assets in this folder.

## Build and run

Use `Pochinki > NGO Spike > Build Server And WebGL` in Unity, or the two individual
build menu items. Generated files go to ignored directories:

- `Builds/NGOSpike/Server/PochinkiNgoSpikeServer.exe`
- `client/public/ngo-spike/`

Run the server executable, then run Vite from `client`. Open
`http://localhost:5173/ngo-spike/index.html` in two browser tabs.

- Each tab gets its own colored player cube.
- WASD or arrow keys move the owning cube on the server.
- Space asks the server to push the one shared physics ball.
- Both tabs must show the same player positions, ball position, and impulse count.

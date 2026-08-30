using System.Collections.Generic;
using System.Linq;
using Pochinki.Networking.Spike;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Pochinki.Networking.Spike.Editor
{
    public static class NgoSpikeAssetGenerator
    {
        public const string RootPath = "Assets/Networking/NGOSpike";
        public const string ScenePath = RootPath + "/Scenes/NGOPhysicsSpike.unity";
        public const string BallPrefabPath = RootPath + "/Prefabs/NetworkBall.prefab";
        public const string PlayerPrefabPath = RootPath + "/Prefabs/NetworkPlayerProbe.prefab";
        public const string PrefabListPath = RootPath + "/Settings/NGOSpikeNetworkPrefabs.asset";

        private static readonly Vector3 BallSpawnPosition = new Vector3(0f, 6f, 0f);

        [MenuItem("Pochinki/NGO Spike/Generate or refresh test assets")]
        public static void GenerateFromMenu()
        {
            Generate(true);
        }

        public static void GenerateFromCommandLine()
        {
            Generate(false);
        }

        [MenuItem("Pochinki/NGO Spike/Open test scene")]
        public static void OpenGeneratedScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Generate(false);
            }

            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Generate(bool openScene)
        {
            EnsureFolder(RootPath);
            EnsureFolder(RootPath + "/Scenes");
            EnsureFolder(RootPath + "/Prefabs");
            EnsureFolder(RootPath + "/Materials");
            EnsureFolder(RootPath + "/Settings");

            Material floorMaterial = CreateOrUpdateMaterial(
                RootPath + "/Materials/SpikeFloor.mat",
                new Color(0.14f, 0.24f, 0.36f));
            Material ballMaterial = CreateOrUpdateMaterial(
                RootPath + "/Materials/ServerBall.mat",
                new Color(1f, 0.32f, 0.04f));
            Material playerMaterial = CreateOrUpdateMaterial(
                RootPath + "/Materials/NetworkPlayer.mat",
                new Color(0.12f, 0.72f, 1f));

            GameObject playerPrefab = CreatePlayerPrefab(playerMaterial);
            GameObject ballPrefab = CreateBallPrefab(ballMaterial);
            NetworkPrefabsList prefabList = CreatePrefabList(playerPrefab, ballPrefab);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateEnvironment(floorMaterial);
            CreateCameraAndLight();
            CreateNetworkManager(playerPrefab, ballPrefab, prefabList);
            CreateHud();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[NGO Spike] Generated editable scene and prefabs under {RootPath}.");

            if (openScene && !Application.isBatchMode)
            {
                EditorSceneManager.OpenScene(ScenePath);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            }
        }

        private static GameObject CreatePlayerPrefab(Material material)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.name = "NetworkPlayerProbe";
            player.transform.localScale = new Vector3(1.25f, 1.25f, 1.25f);

            Renderer renderer = player.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            player.AddComponent<NetworkObject>();
            NetworkTransform networkTransform = player.AddComponent<NetworkTransform>();
            networkTransform.Interpolate = true;
            networkTransform.SyncScaleX = false;
            networkTransform.SyncScaleY = false;
            networkTransform.SyncScaleZ = false;

            NgoSpikePlayer playerController = player.AddComponent<NgoSpikePlayer>();
            playerController.Configure(renderer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            Object.DestroyImmediate(player);
            return prefab;
        }

        private static GameObject CreateBallPrefab(Material material)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "NetworkBall";
            ball.transform.localScale = Vector3.one * 1.15f;
            ball.GetComponent<Renderer>().sharedMaterial = material;

            Rigidbody body = ball.AddComponent<Rigidbody>();
            body.mass = 1.2f;
            body.linearDamping = 0.18f;
            body.angularDamping = 0.12f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            ball.AddComponent<NetworkObject>();
            NetworkTransform networkTransform = ball.AddComponent<NetworkTransform>();
            networkTransform.Interpolate = true;
            networkTransform.UseQuaternionSynchronization = true;
            networkTransform.SyncScaleX = false;
            networkTransform.SyncScaleY = false;
            networkTransform.SyncScaleZ = false;

            NetworkRigidbody networkRigidbody = ball.AddComponent<NetworkRigidbody>();
            networkRigidbody.UseRigidBodyForMotion = true;

            NgoSpikeBall ballController = ball.AddComponent<NgoSpikeBall>();
            ballController.Configure(BallSpawnPosition);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(ball, BallPrefabPath);
            Object.DestroyImmediate(ball);
            return prefab;
        }

        private static NetworkPrefabsList CreatePrefabList(GameObject playerPrefab, GameObject ballPrefab)
        {
            NetworkPrefabsList prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            if (prefabList == null)
            {
                prefabList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(prefabList, PrefabListPath);
            }

            List<NetworkPrefab> existingEntries = prefabList.PrefabList.ToList();
            foreach (NetworkPrefab entry in existingEntries)
            {
                prefabList.Remove(entry);
            }

            prefabList.Add(new NetworkPrefab { Prefab = playerPrefab });
            prefabList.Add(new NetworkPrefab { Prefab = ballPrefab });
            EditorUtility.SetDirty(prefabList);
            return prefabList;
        }

        private static void CreateNetworkManager(
            GameObject playerPrefab,
            GameObject ballPrefab,
            NetworkPrefabsList prefabList)
        {
            GameObject root = new GameObject("NGO Network Manager");
            NetworkManager networkManager = root.AddComponent<NetworkManager>();
            UnityTransport transport = root.AddComponent<UnityTransport>();
            NgoSpikeBootstrap bootstrap = root.AddComponent<NgoSpikeBootstrap>();

            transport.UseWebSockets = true;
            transport.UseEncryption = false;
            transport.SetConnectionData("localhost", NgoSpikeBootstrap.ServerPort, "0.0.0.0");
            UnityTransport.ConnectionAddressData data = transport.ConnectionData;
            data.WebSocketPath = NgoSpikeBootstrap.WebSocketPath;
            transport.ConnectionData = data;

            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
            networkManager.NetworkConfig.TickRate = 30;
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);

            bootstrap.Configure(ballPrefab, BallSpawnPosition);
            EditorUtility.SetDirty(networkManager);
            EditorUtility.SetDirty(transport);
            EditorUtility.SetDirty(bootstrap);
        }

        private static void CreateEnvironment(Material floorMaterial)
        {
            GameObject environment = new GameObject("Server Physics Environment");

            CreateCube(
                "Floor",
                environment.transform,
                new Vector3(0f, -0.5f, 0f),
                new Vector3(14f, 1f, 10f),
                Quaternion.identity,
                floorMaterial);

            CreateCube(
                "Left Wall",
                environment.transform,
                new Vector3(-7.25f, 1f, 0f),
                new Vector3(0.5f, 3f, 10f),
                Quaternion.identity,
                floorMaterial);
            CreateCube(
                "Right Wall",
                environment.transform,
                new Vector3(7.25f, 1f, 0f),
                new Vector3(0.5f, 3f, 10f),
                Quaternion.identity,
                floorMaterial);
            CreateCube(
                "Back Wall",
                environment.transform,
                new Vector3(0f, 1f, 5.25f),
                new Vector3(14f, 3f, 0.5f),
                Quaternion.identity,
                floorMaterial);
            CreateCube(
                "Front Wall",
                environment.transform,
                new Vector3(0f, 1f, -5.25f),
                new Vector3(14f, 3f, 0.5f),
                Quaternion.identity,
                floorMaterial);

            CreateCube(
                "Physics Ramp",
                environment.transform,
                new Vector3(0f, 0.65f, 0.4f),
                new Vector3(5f, 0.45f, 2.2f),
                Quaternion.Euler(0f, 0f, 12f),
                floorMaterial);
        }

        private static GameObject CreateCube(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Quaternion rotation,
            Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.position = position;
            cube.transform.rotation = rotation;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static void CreateCameraAndLight()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 9.5f, -13f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.5f, 0f));
            camera.fieldOfView = 55f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            lightObject.AddComponent<UniversalAdditionalLightData>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            GameObject fillLightObject = new GameObject("Fill Light");
            Light fillLight = fillLightObject.AddComponent<Light>();
            fillLightObject.AddComponent<UniversalAdditionalLightData>();
            fillLight.type = LightType.Point;
            fillLight.color = new Color(0.25f, 0.45f, 1f);
            fillLight.intensity = 4f;
            fillLight.range = 18f;
            fillLightObject.transform.position = new Vector3(-5f, 6f, -4f);
        }

        private static void CreateHud()
        {
            GameObject canvasObject = new GameObject("NGO Spike HUD", typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject panelObject = new GameObject("Connection and Physics Status", typeof(RectTransform));
            panelObject.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(18f, -18f);
            panelRect.sizeDelta = new Vector2(760f, 170f);

            Image panel = panelObject.AddComponent<Image>();
            panel.color = new Color(0.02f, 0.03f, 0.05f, 0.86f);

            TMP_Text status = CreateText(
                "Network Status",
                panelObject.transform,
                new Vector2(20f, -14f),
                new Vector2(720f, 72f),
                22f,
                FontStyles.Bold,
                new Color(0.82f, 0.9f, 1f));
            TMP_Text controls = CreateText(
                "Controls",
                panelObject.transform,
                new Vector2(20f, -88f),
                new Vector2(720f, 28f),
                17f,
                FontStyles.Normal,
                Color.white);
            TMP_Text physics = CreateText(
                "Physics State",
                panelObject.transform,
                new Vector2(20f, -120f),
                new Vector2(720f, 32f),
                17f,
                FontStyles.Normal,
                new Color(1f, 0.7f, 0.3f));

            NgoSpikeHud hud = panelObject.AddComponent<NgoSpikeHud>();
            hud.Configure(status, controls, physics);
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 size,
            float fontSize,
            FontStyles style,
            Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static Material CreateOrUpdateMaterial(string path, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            string folder = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}

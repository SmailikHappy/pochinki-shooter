using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Pochinki.Networking.Game;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pochinki.Networking.Game.Editor
{
    /// <summary>
    /// Installs production networking as ordinary editable Unity objects.
    /// The resulting NetworkManager, prefab list and prefab components are all
    /// visible in Hierarchy/Project/Inspector; runtime code does not fabricate them.
    /// </summary>
    public static class NetworkGameAssetInstaller
    {
        public const string RootPath = "Assets/Networking/Game";
        public const string ScenePath = "Assets/Scenes/SampleScene.unity";
        public const string GameplayBallPrefabPath = "Assets/GameObjects/Prefabs/PachinkoBall.prefab";
        public const string GameplayBulletPrefabPath = "Assets/GameObjects/Prefabs/Bullet.prefab";
        public const string PachinkoVisibilityMaterialPath = "Assets/Materials/M_Placeholder 1.mat";
        public const string SessionPlayerPrefabPath = RootPath + "/Prefabs/NetworkSessionPlayer.prefab";
        public const string PrefabListPath = RootPath + "/Settings/GameNetworkPrefabs.asset";

        [MenuItem("Pochinki/Network Game/Install or refresh production networking")]
        public static void InstallFromMenu()
        {
            Install(openScene: true);
        }

        public static void InstallFromCommandLine()
        {
            Install(openScene: false);
        }

        public static void Install(bool openScene)
        {
            EnsureFolder(RootPath);
            EnsureFolder(RootPath + "/Prefabs");
            EnsureFolder(RootPath + "/Settings");

            GameObject sessionPlayerPrefab = CreateOrUpdateSessionPlayerPrefab();
            GameObject pachinkoBallPrefab = AddNetworkingToGameplayBall();
            GameObject bulletPrefab = AddNetworkingToGameplayBullet();
            NetworkPrefabsList prefabList = CreateOrUpdatePrefabList(
                sessionPlayerPrefab,
                pachinkoBallPrefab,
                bulletPrefab);

            InstallNetworkManagerInGameplayScene(
                sessionPlayerPrefab,
                pachinkoBallPrefab,
                bulletPrefab,
                prefabList);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Network Game] Production networking assets installed in SampleScene and gameplay prefabs.");

            if (openScene && !Application.isBatchMode)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Selection.activeObject = GameObject.Find("Production Network Manager");
            }
        }

        private static GameObject CreateOrUpdateSessionPlayerPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SessionPlayerPrefabPath);
            GameObject root = existing != null
                ? PrefabUtility.LoadPrefabContents(SessionPlayerPrefabPath)
                : new GameObject("NetworkSessionPlayer");

            root.name = "NetworkSessionPlayer";
            NetworkObject networkObject = GetOrAdd<NetworkObject>(root);
            networkObject.DontDestroyWithOwner = false;
            GetOrAdd<NetworkSessionPlayer>(root);

            PrefabUtility.SaveAsPrefabAsset(root, SessionPlayerPrefabPath);
            if (existing != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                Object.DestroyImmediate(root);
            }

            RefreshNetworkObjectHash(SessionPlayerPrefabPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(SessionPlayerPrefabPath);
        }

        private static GameObject AddNetworkingToGameplayBall()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayBallPrefabPath);
            if (source == null)
            {
                throw new System.InvalidOperationException(
                    $"Gameplay Pachinko ball prefab was not found at {GameplayBallPrefabPath}.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(GameplayBallPrefabPath);
            Material visibilityMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                PachinkoVisibilityMaterialPath);

            if (visibilityMaterial == null)
            {
                throw new System.InvalidOperationException(
                    $"Pachinko visibility material was not found at {PachinkoVisibilityMaterialPath}.");
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = visibilityMaterial;
            }

            NetworkObject networkObject = GetOrAdd<NetworkObject>(root);
            networkObject.DontDestroyWithOwner = false;

            NetworkTransform networkTransform = GetOrAdd<NetworkTransform>(root);
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            networkTransform.Interpolate = true;
            networkTransform.UseQuaternionSynchronization = true;
            networkTransform.SyncScaleX = false;
            networkTransform.SyncScaleY = false;
            networkTransform.SyncScaleZ = false;

            NetworkRigidbody networkRigidbody = GetOrAdd<NetworkRigidbody>(root);
            networkRigidbody.UseRigidBodyForMotion = true;
            networkRigidbody.AutoUpdateKinematicState = true;
            networkRigidbody.AutoSetKinematicOnDespawn = true;

            GetOrAdd<NetworkPachinkoBall>(root);
            PrefabUtility.SaveAsPrefabAsset(root, GameplayBallPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            RefreshNetworkObjectHash(GameplayBallPrefabPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(GameplayBallPrefabPath);
        }

        private static GameObject AddNetworkingToGameplayBullet()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayBulletPrefabPath);
            if (source == null)
            {
                throw new System.InvalidOperationException(
                    $"Gameplay bullet prefab was not found at {GameplayBulletPrefabPath}.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(GameplayBulletPrefabPath);
            NetworkObject networkObject = GetOrAdd<NetworkObject>(root);
            networkObject.DontDestroyWithOwner = false;

            NetworkTransform networkTransform = GetOrAdd<NetworkTransform>(root);
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            // Pixel ownership changes immediately on the authoritative hit. A
            // delayed transform would otherwise show the bullet behind that hit.
            networkTransform.Interpolate = false;
            networkTransform.UseQuaternionSynchronization = true;
            networkTransform.SyncScaleX = true;
            networkTransform.SyncScaleY = true;
            networkTransform.SyncScaleZ = true;

            NetworkRigidbody networkRigidbody = GetOrAdd<NetworkRigidbody>(root);
            networkRigidbody.UseRigidBodyForMotion = true;
            networkRigidbody.AutoUpdateKinematicState = true;
            networkRigidbody.AutoSetKinematicOnDespawn = true;

            GetOrAdd<NetworkBullet>(root);
            PrefabUtility.SaveAsPrefabAsset(root, GameplayBulletPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            RefreshNetworkObjectHash(GameplayBulletPrefabPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(GameplayBulletPrefabPath);
        }

        private static NetworkPrefabsList CreateOrUpdatePrefabList(
            GameObject sessionPlayerPrefab,
            GameObject pachinkoBallPrefab,
            GameObject bulletPrefab)
        {
            NetworkPrefabsList prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            if (prefabList == null)
            {
                prefabList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(prefabList, PrefabListPath);
            }

            foreach (NetworkPrefab entry in prefabList.PrefabList.ToList())
            {
                prefabList.Remove(entry);
            }

            prefabList.Add(new NetworkPrefab { Prefab = sessionPlayerPrefab });
            prefabList.Add(new NetworkPrefab { Prefab = pachinkoBallPrefab });
            prefabList.Add(new NetworkPrefab { Prefab = bulletPrefab });
            EditorUtility.SetDirty(prefabList);
            return prefabList;
        }

        private static void InstallNetworkManagerInGameplayScene(
            GameObject sessionPlayerPrefab,
            GameObject pachinkoBallPrefab,
            GameObject bulletPrefab,
            NetworkPrefabsList prefabList)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RemoveLegacyDebugPlayers(scene);
            GameObject root = scene.GetRootGameObjects()
                .FirstOrDefault(candidate => candidate.name == "Production Network Manager");

            if (root == null)
            {
                root = new GameObject("Production Network Manager");
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            NetworkManager networkManager = GetOrAdd<NetworkManager>(root);
            UnityTransport transport = GetOrAdd<UnityTransport>(root);
            NetworkGameBootstrap bootstrap = GetOrAdd<NetworkGameBootstrap>(root);

            GameObject matchStateObject = scene.GetRootGameObjects()
                .FirstOrDefault(candidate => candidate.name == "Network Match State");
            if (matchStateObject == null)
            {
                matchStateObject = new GameObject("Network Match State");
                SceneManager.MoveGameObjectToScene(matchStateObject, scene);
            }

            GetOrAdd<NetworkObject>(matchStateObject);
            GetOrAdd<NetworkMatchState>(matchStateObject);

            GameOverUI gameOverUI = Object.FindAnyObjectByType<GameOverUI>(FindObjectsInactive.Include);
            if (gameOverUI != null)
            {
                GetOrAdd<CanvasGroup>(gameOverUI.gameObject);
                EditorUtility.SetDirty(gameOverUI.gameObject);
            }

            GameSurface gameSurface = Object.FindAnyObjectByType<GameSurface>(FindObjectsInactive.Include);
            if (gameSurface != null)
            {
                GetOrAdd<GameSurfaceRenderer>(gameSurface.gameObject);
                EditorUtility.SetDirty(gameSurface.gameObject);
            }

            transport.UseWebSockets = true;
            transport.UseEncryption = false;
            transport.SetConnectionData(
                "localhost",
                NetworkGameBootstrap.ServerPort,
                "0.0.0.0");
            UnityTransport.ConnectionAddressData connection = transport.ConnectionData;
            connection.WebSocketPath = NetworkGameBootstrap.WebSocketPath;
            transport.ConnectionData = connection;

            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = sessionPlayerPrefab;
            networkManager.NetworkConfig.ProtocolVersion = NetworkGameBootstrap.GameSchemaVersion;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.NetworkConfig.TickRate = 30;
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);

            bootstrap.Configure(
                sessionPlayerPrefab,
                pachinkoBallPrefab,
                bulletPrefab,
                NetworkGameBootstrap.MaxSupportedPlayers);

            EditorUtility.SetDirty(networkManager);
            EditorUtility.SetDirty(transport);
            EditorUtility.SetDirty(bootstrap);
            EditorUtility.SetDirty(matchStateObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void RemoveLegacyDebugPlayers(Scene scene)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name.StartsWith(
                        "DebugPlayer",
                        System.StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(rootObject);
                }
            }
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
        }

        private static void RefreshNetworkObjectHash(string prefabPath)
        {
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            NetworkObject networkObject = prefab != null ? prefab.GetComponent<NetworkObject>() : null;
            MethodInfo validate = typeof(NetworkObject).GetMethod(
                "OnValidate",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (networkObject == null || validate == null)
            {
                throw new System.InvalidOperationException(
                    $"Unable to refresh NetworkObject hash for {prefabPath}.");
            }

            validate.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            AssetDatabase.SaveAssetIfDirty(prefab);
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

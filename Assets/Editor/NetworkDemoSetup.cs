#if UNITY_EDITOR
using System.Linq;
using System.IO;
using System.Reflection;
using ARPG.Networking;
using ARPG.StateSync;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class NetworkDemoSetup
{
    private const string SourceScenePath = "Assets/Scenes/01-GameScene.unity";
    private const string NetworkScenePath = "Assets/Scenes/02-NetworkGameScene.unity";
    private const string NetworkPrefabPath = "Assets/Prefabs/NetworkPlayerProxy.prefab";

    [MenuItem("Tools/ARPG/Build Network Demo")]
    public static void Build()
    {
        GameObject playerPrefab = BuildPlayerProxyPrefab();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(NetworkScenePath) == null)
            AssetDatabase.CopyAsset(SourceScenePath, NetworkScenePath);

        Scene scene = EditorSceneManager.OpenScene(NetworkScenePath, OpenSceneMode.Additive);
        PlayerController player = FindInScene<PlayerController>(scene);
        BossController boss = FindInScene<BossController>(scene);

        BuildNetworkRoot(scene, playerPrefab, player.transform);
        BuildBossNetworkBridge(boss);
        BuildLobbyUI(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log($"Network demo created: {NetworkScenePath}");
    }

    [MenuItem("Tools/ARPG/Build Network Demo/Validate")]
    public static void Validate()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefabPath);
        Require(prefab != null, "Network player prefab is missing");
        Require(prefab.GetComponent<NetworkPlayerProxy>() != null, "NetworkPlayerProxy is missing");
        Require(GetGlobalObjectIdHash(prefab.GetComponent<NetworkObject>()) != 0, "Player prefab hash is zero");

        NetworkPrefabsList defaultPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(
            "Assets/DefaultNetworkPrefabs.asset");
        Require(defaultPrefabs != null && defaultPrefabs.PrefabList.Any(item => item.Prefab == prefab),
            "Player prefab is not registered");

        Scene scene = EditorSceneManager.OpenScene(NetworkScenePath, OpenSceneMode.Additive);
        NetworkManager manager = FindInScene<NetworkManager>(scene);
        NetworkSessionController session = FindInScene<NetworkSessionController>(scene);
        NetworkBossSynchronizer boss = FindInScene<NetworkBossSynchronizer>(scene);
        NetworkLobbyUI lobby = FindInScene<NetworkLobbyUI>(scene);

        Require(manager.NetworkConfig.PlayerPrefab == prefab, "NetworkManager player prefab is invalid");
        Require(manager.GetComponent<UnityTransport>() != null, "UnityTransport is missing");
        Require(session != null && lobby != null, "Network lobby is incomplete");
        Require(GetGlobalObjectIdHash(boss.GetComponent<NetworkObject>()) != 0, "Boss scene hash is zero");

        EditorSceneManager.CloseScene(scene, true);
        Require(EditorBuildSettings.scenes.Any(item => item.enabled && item.path == NetworkScenePath),
            "Network scene is not enabled in Build Settings");
        Debug.Log("Network demo validation passed");
    }

    [MenuItem("Tools/ARPG/Build Network Demo/Windows Player")]
    public static void BuildWindowsPlayer()
    {
        Build();
        const string outputDirectory = "Builds/NetworkDemo";
        Directory.CreateDirectory(outputDirectory);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { NetworkScenePath },
            locationPathName = $"{outputDirectory}/ARPGNetwork.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development | BuildOptions.CleanBuildCache
        });

        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"Network demo build failed: {report.summary.result}");
    }

    private static GameObject BuildPlayerProxyPrefab()
    {
        GameObject root = new GameObject("NetworkPlayerProxy");
        root.AddComponent<NetworkObject>();

        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.enabled = false;
        collider.isTrigger = true;
        collider.center = Vector3.up;
        collider.height = 2f;
        collider.radius = 0.45f;

        Rigidbody rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        rigidbody.detectCollisions = false;

        NetworkPlayerProxy proxy = root.AddComponent<NetworkPlayerProxy>();
        proxy.ConfigureEquipment(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/WeaponFeixue.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/WeaponScythe.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/WeaponJavelin.prefab"));

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, NetworkPrefabPath);
        Object.DestroyImmediate(root);

        NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
        typeof(NetworkObject)
            .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(networkObject, null);
        EditorUtility.SetDirty(networkObject);
        PrefabUtility.SavePrefabAsset(prefab);
        return prefab;
    }

    private static void BuildNetworkRoot(Scene scene, GameObject playerPrefab, Transform player)
    {
        RemoveRoot(scene, "Network Session");
        GameObject root = new GameObject("Network Session");
        SceneManager.MoveGameObjectToScene(root, scene);

        NetworkManager manager = root.AddComponent<NetworkManager>();
        UnityTransport transport = root.AddComponent<UnityTransport>();
        NetworkSessionController session = root.AddComponent<NetworkSessionController>();

        manager.NetworkConfig.NetworkTransport = transport;
        manager.NetworkConfig.PlayerPrefab = playerPrefab;
        manager.NetworkConfig.ConnectionApproval = true;
        manager.NetworkConfig.TickRate = 20;
        manager.NetworkConfig.EnableSceneManagement = true;

        Vector3 firstSpawn = player.position;
        Vector3 secondSpawn = firstSpawn + player.right * 2.5f;
        session.Configure(manager, transport, new[] { firstSpawn, secondSpawn });
    }

    private static void BuildBossNetworkBridge(BossController boss)
    {
        if (boss.GetComponent<NetworkObject>() == null)
            boss.gameObject.AddComponent<NetworkObject>();
        if (boss.GetComponent<CharacterStateSynchronizer>() == null)
            boss.gameObject.AddComponent<CharacterStateSynchronizer>();
        if (boss.GetComponent<NetworkBossSynchronizer>() == null)
            boss.gameObject.AddComponent<NetworkBossSynchronizer>();
    }

    private static void BuildLobbyUI(Scene scene)
    {
        RemoveRoot(scene, "Network Lobby Canvas");
        NetworkSessionController session = FindInScene<NetworkSessionController>(scene);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObject = new GameObject(
            "Network Lobby Canvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = CreateUIObject("Panel", canvasObject.transform, typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(24f, -24f);
        panelRect.sizeDelta = new Vector2(380f, 250f);
        panel.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.1f, 0.92f);

        Text title = CreateText(panel.transform, font, "局域网双人 Boss Demo", 22, new Vector2(18f, -16f), new Vector2(344f, 34f));
        title.fontStyle = FontStyle.Bold;

        InputField address = CreateInput(panel.transform, font, new Vector2(18f, -62f), new Vector2(344f, 38f));
        Button host = CreateButton(panel.transform, font, "创建 Host", new Vector2(18f, -112f), new Vector2(108f, 38f));
        Button client = CreateButton(panel.transform, font, "加入 Client", new Vector2(136f, -112f), new Vector2(108f, 38f));
        Button disconnect = CreateButton(panel.transform, font, "断开", new Vector2(254f, -112f), new Vector2(108f, 38f));
        Text status = CreateText(panel.transform, font, string.Empty, 15, new Vector2(18f, -164f), new Vector2(344f, 64f));

        NetworkLobbyUI lobby = canvasObject.AddComponent<NetworkLobbyUI>();
        lobby.Configure(session, address, host, client, disconnect, status);
    }

    private static GameObject CreateUIObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject gameObject = new GameObject(name, components);
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Text CreateText(
        Transform parent,
        Font font,
        string value,
        int size,
        Vector2 position,
        Vector2 dimensions)
    {
        GameObject gameObject = CreateUIObject("Text", parent, typeof(Text));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;

        Text text = gameObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.text = value;
        return text;
    }

    private static InputField CreateInput(Transform parent, Font font, Vector2 position, Vector2 dimensions)
    {
        GameObject root = CreateUIObject("Address Input", parent, typeof(Image), typeof(InputField));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        root.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

        Text inputText = CreateText(root.transform, font, string.Empty, 16, new Vector2(10f, 0f), new Vector2(324f, 38f));
        inputText.alignment = TextAnchor.MiddleLeft;
        Text placeholder = CreateText(root.transform, font, "Host IP", 16, new Vector2(10f, 0f), new Vector2(324f, 38f));
        placeholder.color = new Color(1f, 1f, 1f, 0.45f);

        InputField input = root.GetComponent<InputField>();
        input.textComponent = inputText;
        input.placeholder = placeholder;
        return input;
    }

    private static Button CreateButton(
        Transform parent,
        Font font,
        string label,
        Vector2 position,
        Vector2 dimensions)
    {
        GameObject root = CreateUIObject(label, parent, typeof(Image), typeof(Button));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        root.GetComponent<Image>().color = new Color(0.18f, 0.48f, 0.9f, 0.95f);

        Text text = CreateText(root.transform, font, label, 15, Vector2.zero, dimensions);
        text.alignment = TextAnchor.MiddleCenter;
        return root.GetComponent<Button>();
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .First();
    }

    private static void RemoveRoot(Scene scene, string name)
    {
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == name);
        if (root != null)
            Object.DestroyImmediate(root);
    }

    private static void AddSceneToBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => scene.path == NetworkScenePath)) return;

        EditorBuildSettings.scenes = scenes
            .Concat(new[] { new EditorBuildSettingsScene(NetworkScenePath, true) })
            .ToArray();
    }

    private static uint GetGlobalObjectIdHash(NetworkObject networkObject)
    {
        SerializedObject serializedObject = new SerializedObject(networkObject);
        return (uint)serializedObject.FindProperty("GlobalObjectIdHash").longValue;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new BuildFailedException(message);
    }
}
#endif

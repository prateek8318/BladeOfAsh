using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the game scene from code: materials, player, camera, managers.
// The realms themselves are built at runtime by MapBuilder so the player can pick one.
// Menu: Blade of Ash > Rebuild Scene  /  Blade of Ash > Build macOS App
public static class SceneBuilder
{
    const string ScenePath = "Assets/Scenes/Arena.unity";
    const string MatDir = "Assets/Materials";

    [MenuItem("Blade of Ash/Rebuild Scene")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---------------- Managers + materials
        var gm = new GameObject("GameManager");
        var a = gm.AddComponent<GameAssets>();

        // World
        a.stone = Mat("Stone", new Color(0.32f, 0.29f, 0.27f), 0f, 0.15f);
        a.darkStone = Mat("DarkStone", new Color(0.16f, 0.14f, 0.14f), 0f, 0.1f);
        a.arenaFloor = Mat("ArenaFloor", new Color(0.36f, 0.3f, 0.26f), 0f, 0.25f);
        a.ashGround = Mat("AshGround", new Color(0.12f, 0.1f, 0.1f), 0f, 0.05f);
        a.rune = Mat("Rune", new Color(1f, 0.4f, 0.1f), 0f, 0.5f, new Color(2.2f, 0.7f, 0.1f));
        a.fire = Mat("Fire", new Color(1f, 0.5f, 0.1f), 0f, 0.5f, new Color(4f, 1.5f, 0.2f));
        a.iron = Mat("Iron", new Color(0.2f, 0.2f, 0.22f), 0.8f, 0.5f);
        a.snow = Mat("Snow", new Color(0.72f, 0.77f, 0.84f), 0f, 0.15f);
        a.ice = Mat("Ice", new Color(0.32f, 0.48f, 0.62f), 0.3f, 0.85f);
        a.frostStone = Mat("FrostStone", new Color(0.42f, 0.47f, 0.55f), 0f, 0.3f);
        a.crystal = Mat("Crystal", new Color(0.4f, 0.7f, 0.9f), 0.1f, 0.9f, new Color(0.2f, 0.6f, 1.0f));
        a.pineLeaf = Mat("PineLeaf", new Color(0.1f, 0.2f, 0.16f), 0f, 0.1f);
        a.bark = Mat("Bark", new Color(0.2f, 0.14f, 0.1f), 0f, 0.1f);
        a.obsidian = Mat("Obsidian", new Color(0.08f, 0.07f, 0.1f), 0.3f, 0.6f);
        a.stormStone = Mat("StormStone", new Color(0.2f, 0.19f, 0.25f), 0f, 0.3f);
        a.stormRune = Mat("StormRune", new Color(0.35f, 0.28f, 0.7f), 0f, 0.5f, new Color(0.45f, 0.32f, 1.4f));
        a.cloud = Mat("Cloud", new Color(0.32f, 0.3f, 0.42f), 0f, 0f);

        // Heroes
        a.heroSkin = Mat("HeroSkin", new Color(0.62f, 0.45f, 0.36f), 0f, 0.35f);
        a.heroArmor = Mat("HeroArmor", new Color(0.22f, 0.2f, 0.19f), 0.7f, 0.55f);
        a.heroCape = Mat("HeroCape", new Color(0.35f, 0.06f, 0.04f), 0f, 0.15f);
        a.heroEmber = Mat("HeroEmber", new Color(1f, 0.45f, 0.1f), 0f, 0.6f, new Color(3f, 1f, 0.15f));
        a.ysraSkin = Mat("YsraSkin", new Color(0.78f, 0.66f, 0.6f), 0f, 0.35f);
        a.ysraArmor = Mat("YsraArmor", new Color(0.12f, 0.16f, 0.26f), 0.6f, 0.6f);
        a.ysraCape = Mat("YsraCape", new Color(0.08f, 0.3f, 0.38f), 0f, 0.2f);
        a.ysraGlow = Mat("YsraGlow", new Color(0.45f, 0.85f, 1f), 0f, 0.6f, new Color(0.8f, 2.4f, 3.2f));
        a.gorrunSkin = Mat("GorrunSkin", new Color(0.38f, 0.3f, 0.28f), 0f, 0.2f);
        a.gorrunArmor = Mat("GorrunArmor", new Color(0.3f, 0.22f, 0.16f), 0.7f, 0.45f);
        a.gorrunCape = Mat("GorrunCape", new Color(0.2f, 0.12f, 0.06f), 0f, 0.1f);
        a.gorrunGlow = Mat("GorrunGlow", new Color(1f, 0.7f, 0.2f), 0f, 0.6f, new Color(3.2f, 1.6f, 0.2f));

        // Weapons
        a.blade = Mat("Blade", new Color(0.85f, 0.8f, 0.75f), 0.9f, 0.85f, new Color(0.9f, 0.3f, 0.05f));
        a.spear = Mat("Spear", new Color(0.6f, 0.85f, 1f), 0.6f, 0.8f, new Color(0.6f, 1.6f, 2.6f));
        a.fang = Mat("Fang", new Color(0.9f, 0.4f, 0.25f), 0.8f, 0.85f, new Color(1.6f, 0.3f, 0.05f));
        a.maulHead = Mat("MaulHead", new Color(0.22f, 0.24f, 0.3f), 0.85f, 0.6f);
        a.lightning = Mat("Lightning", new Color(0.75f, 0.85f, 1f), 0f, 0.5f, new Color(3f, 4f, 6f));
        a.trail = SpriteMat("Trail", Color.white);

        // Enemies
        a.gruntSkin = Mat("GruntSkin", new Color(0.36f, 0.4f, 0.33f), 0f, 0.2f);
        a.gruntArmor = Mat("GruntArmor", new Color(0.33f, 0.22f, 0.14f), 0.2f, 0.25f);
        a.bruteSkin = Mat("BruteSkin", new Color(0.45f, 0.16f, 0.12f), 0f, 0.25f);
        a.bruteArmor = Mat("BruteArmor", new Color(0.18f, 0.17f, 0.17f), 0.75f, 0.45f);
        a.casterRobe = Mat("CasterRobe", new Color(0.25f, 0.1f, 0.2f), 0f, 0.15f);
        a.casterGlow = Mat("CasterGlow", new Color(1f, 0.3f, 0.6f), 0f, 0.6f, new Color(3f, 0.6f, 1.8f));
        a.stalkerSkin = Mat("StalkerSkin", new Color(0.08f, 0.07f, 0.1f), 0.2f, 0.45f);
        a.bossSkin = Mat("BossSkin", new Color(0.16f, 0.12f, 0.2f), 0f, 0.3f);
        a.bossArmor = Mat("BossArmor", new Color(0.08f, 0.07f, 0.1f), 0.85f, 0.6f);
        a.bossGlow = Mat("BossGlow", new Color(0.7f, 0.25f, 1f), 0f, 0.6f, new Color(2.2f, 0.6f, 3.2f));
        a.frostBossArmor = Mat("FrostBossArmor", new Color(0.55f, 0.7f, 0.85f), 0.8f, 0.8f);
        a.frostGlow = Mat("FrostGlow", new Color(0.5f, 0.85f, 1f), 0f, 0.6f, new Color(1f, 2.6f, 3.6f));
        a.stormBossArmor = Mat("StormBossArmor", new Color(0.12f, 0.12f, 0.18f), 0.85f, 0.6f);
        a.stormGlow = Mat("StormGlow", new Color(0.7f, 0.6f, 1f), 0f, 0.6f, new Color(1.8f, 1.6f, 4f));
        a.enemyEye = Mat("EnemyEye", new Color(1f, 0.1f, 0.05f), 0f, 0.5f, new Color(4f, 0.3f, 0.1f));
        a.bolt = Mat("Bolt", new Color(1f, 0.3f, 0.6f), 0f, 0.6f, new Color(3f, 0.6f, 1.8f));
        a.telegraph = SpriteMat("Telegraph", new Color(1f, 0.25f, 0.1f, 0.33f));

        // FX
        a.shard = Mat("Shard", new Color(0.5f, 0.12f, 0.08f), 0f, 0.3f, new Color(0.6f, 0.12f, 0.02f));
        a.iceShard = Mat("IceShard", new Color(0.7f, 0.9f, 1f), 0f, 0.8f, new Color(0.4f, 0.8f, 1.2f));
        a.spawnRing = Mat("SpawnRing", new Color(0.6f, 0.2f, 1f), 0f, 0.5f, new Color(1.6f, 0.4f, 2.4f));
        a.shockwave = Mat("Shockwave", new Color(1f, 0.6f, 0.2f), 0f, 0.5f, new Color(3f, 1.2f, 0.2f));
        a.healOrb = Mat("HealOrb", new Color(0.3f, 1f, 0.4f), 0f, 0.8f, new Color(0.6f, 3f, 0.8f));
        a.rageAura = Mat("RageAura", new Color(1f, 0.3f, 0.05f), 0f, 0.5f, new Color(4f, 0.8f, 0.05f));

        gm.AddComponent<GameManager>();

        // ---------------- Player
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0, 0.05f, -4f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2.1f; cc.radius = 0.45f; cc.center = new Vector3(0, 1.05f, 0); cc.stepOffset = 0.4f;
        player.AddComponent<Health>();
        player.AddComponent<PlayerController>();

        // ---------------- Camera
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.1f, 0.08f);
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 260f;
        camGo.AddComponent<AudioListener>();
        var tpc = camGo.AddComponent<ThirdPersonCamera>();
        tpc.target = player.transform;
        camGo.transform.position = new Vector3(0, 3, -11);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        PlayerSettings.productName = "Blade of Ash";
        PlayerSettings.companyName = "Ashborn Studio";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        QualitySettings.shadowDistance = 70f;

        AssetDatabase.SaveAssets();
        Debug.Log("[BladeOfAsh] Scene built: " + ScenePath);
    }

    [MenuItem("Blade of Ash/Build macOS App")]
    public static void BuildMac()
    {
        Build();
        Directory.CreateDirectory("Builds");
        // Universal binary: runs on both Intel and Apple Silicon Macs.
        EditorUserBuildSettings.SetPlatformSettings("Standalone", "OSXUniversal", "Architecture", "x64ARM64");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/macOS/BladeOfAsh.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        });
        Debug.Log("[BladeOfAsh] Build result: " + report.summary.result + " size=" + report.summary.totalSize);
        if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }

    [MenuItem("Blade of Ash/Build Windows App")]
    public static void BuildWindows()
    {
        Build();
        Directory.CreateDirectory("Builds/Windows");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/Windows/BladeOfAsh.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        Debug.Log("[BladeOfAsh] Windows build result: " + report.summary.result + " size=" + report.summary.totalSize);
        if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }

    // ---------------- helpers
    static Material Mat(string name, Color c, float metallic, float smooth, Color? emission = null)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Standard"));
        m.color = c;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smooth);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    // Unlit, vertex-coloured, alpha-blended material for trails, particles and telegraphs.
    static Material SpriteMat(string name, Color c)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Sprites/Default"));
        m.color = c;
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }
}

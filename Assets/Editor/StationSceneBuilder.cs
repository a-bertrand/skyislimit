using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// Construit la scène de démo visuelle "Station" (avant-poste extraterrestre, de nuit) par code :
/// sol en tilemap, décor, perso animé, drone ennemi, lumières 2D avec normal maps et ombres,
/// caméra Pixel Perfect qui suit le joueur, cycle jour/nuit et post-process (bloom, vignette).
/// Menu : Tools > RPG > Construire la scène Station (démo visuelle). Peut être relancé.
/// </summary>
public static class StationSceneBuilder
{
    private const string ScenePath = "Assets/_Game/Scenes/Station.unity";
    private const string SpritesFolder = "Assets/_Game/Art/Sprites";
    private const string TilesFolder = "Assets/_Game/Art/Tiles";
    private const string MaterialsFolder = "Assets/_Game/Art/Materials";
    private const string ProfilePath = MaterialsFolder + "/StationPostProcess.asset";
    private const string LitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
    private const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    // Couleurs des lumières
    private static readonly Color LampColor = new Color(1f, 0.76f, 0.48f);
    private static readonly Color CyanColor = new Color(0.38f, 0.92f, 1f);
    private static readonly Color PinkColor = new Color(1f, 0.45f, 0.85f);
    private static readonly Color RedColor = new Color(1f, 0.25f, 0.3f);
    private static readonly Color HelmetColor = new Color(0.75f, 0.9f, 1f);

    private static Material litMaterial;
    private static Material unlitMaterial;
    private static Transform propsRoot;

    [MenuItem("Tools/RPG/Construire la scène Station (démo visuelle)")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Station", "Quitte le mode Play avant de lancer cet outil.", "OK");
            return;
        }
        if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Station",
                "La scène Station existe déjà. La reconstruire ? (les modifications faites à la main dans cette scène seront perdues)",
                "Reconstruire", "Annuler"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        litMaterial = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
        unlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
        if (litMaterial == null || unlitMaterial == null)
        {
            Debug.LogError("[Station] Matériaux Sprite-Lit/Unlit-Default introuvables dans le package URP.");
            return;
        }

        PrepareTextures();
        ConfigureYSorting();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rng = new System.Random(42);
        var bounds = new RectInt(-16, -10, 32, 20);   // carte : 32 x 20 tuiles
        var platform = new RectInt(-6, -4, 12, 8);    // plateforme métallique au centre

        BuildGround(bounds, platform, rng);
        BuildMapBounds(bounds);

        propsRoot = new GameObject("Props").transform;
        var player = BuildPlayer(new Vector2(0f, -1f));
        var dronePosition = new Vector2(8f, -5.5f);
        BuildDrone(dronePosition);
        BuildProps(bounds, platform, rng, new[] { new Vector2(0f, -1f), dronePosition });

        BuildCamera(player, bounds);
        BuildGlobalLight();
        BuildPostProcess();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log("[Station] Scène construite : " + ScenePath + ". Lance Play !");
    }

    // ------------------------------------------------------------------ préparation

    /// <summary>Réimporte le pixel art avec les bons réglages et branche les normal maps.</summary>
    private static void PrepareTextures()
    {
        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(SpritesFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

        // 2e passe : les sprites importés avant leur normal map sont réimportés pour la brancher
        var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { SpritesFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".png") && !p.EndsWith("_n.png"));
        foreach (var path in paths)
        {
            if (File.Exists(PixelArtImporter.NormalMapPathFor(path)) && !PixelArtImporter.HasNormalMap(path))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }

    /// <summary>Tri par axe Y : ce qui est plus bas à l'écran passe devant (vue de dessus 3/4).</summary>
    private static void ConfigureYSorting()
    {
        var guid = AssetDatabase.FindAssets("t:Renderer2DData").FirstOrDefault();
        if (guid == null) { Debug.LogWarning("[Station] Renderer2DData introuvable : tri Y non configuré."); return; }

        var renderer = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid));
        var so = new SerializedObject(renderer);
        var mode = so.FindProperty("m_TransparencySortMode");
        var axis = so.FindProperty("m_TransparencySortAxis");
        if (mode == null || axis == null) return;

        var idx = System.Array.IndexOf(mode.enumNames, "CustomAxis");
        if (idx >= 0) mode.enumValueIndex = idx;
        axis.vector3Value = Vector3.up;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ sol et limites

    private static void BuildGround(RectInt bounds, RectInt platform, System.Random rng)
    {
        EnsureFolder(TilesFolder);
        var moss = Enumerable.Range(0, 4).Select(i => GetTile($"moss_{i}")).ToArray();
        var deck = Enumerable.Range(0, 4).Select(i => GetTile($"deck_{i}")).ToArray();

        var grid = new GameObject("Grid", typeof(Grid));
        var ground = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer));
        ground.transform.SetParent(grid.transform, false);
        var tilemap = ground.GetComponent<Tilemap>();
        var tilemapRenderer = ground.GetComponent<TilemapRenderer>();
        tilemapRenderer.sharedMaterial = litMaterial;
        tilemapRenderer.sortingOrder = -100;

        for (var x = bounds.xMin; x < bounds.xMax; x++)
        for (var y = bounds.yMin; y < bounds.yMax; y++)
        {
            TileBase tile;
            if (platform.Contains(new Vector2Int(x, y)))
            {
                if (y == platform.yMin) tile = deck[2];                         // bande de danger au bord
                else if (y == 0 && (x == -4 || x == 3)) tile = deck[1];         // grilles d'aération
                else tile = rng.NextDouble() < 0.2 ? deck[3] : deck[0];
            }
            else
            {
                var r = rng.NextDouble();
                tile = r < 0.4 ? moss[0] : r < 0.7 ? moss[1] : r < 0.9 ? moss[2] : moss[3];
            }
            tilemap.SetTile(new Vector3Int(x, y, 0), tile);
        }
    }

    private static Tile GetTile(string name)
    {
        var path = $"{TilesFolder}/{name}.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, path);
        }
        tile.sprite = LoadSprite($"Tiles/{name}");
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static void BuildMapBounds(RectInt b)
    {
        var go = new GameObject("Map Bounds");
        void Wall(Vector2 center, Vector2 size)
        {
            var box = go.AddComponent<BoxCollider2D>();
            box.offset = center;
            box.size = size;
        }
        Wall(new Vector2(b.center.x, b.yMax + 0.5f), new Vector2(b.width + 2, 1));
        Wall(new Vector2(b.center.x, b.yMin - 0.5f), new Vector2(b.width + 2, 1));
        Wall(new Vector2(b.xMin - 0.5f, b.center.y), new Vector2(1, b.height + 2));
        Wall(new Vector2(b.xMax + 0.5f, b.center.y), new Vector2(1, b.height + 2));
    }

    // ------------------------------------------------------------------ perso et ennemi

    private static Transform BuildPlayer(Vector2 position)
    {
        var root = new GameObject("Player");
        root.transform.position = position;
        root.AddComponent<SortingGroup>();

        var rb = root.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        var col = root.AddComponent<CapsuleCollider2D>();
        col.direction = CapsuleDirection2D.Horizontal;
        col.size = new Vector2(0.6f, 0.3f);
        col.offset = new Vector2(0f, 0.15f);
        root.AddComponent<PlayerMovement>();

        AddSprite(root.transform, "Shadow", "Characters/shadow", -1, litMaterial, new Vector2(0f, 0.06f));
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        var body = AddSprite(visual, "Body", "Characters/astro_down_0", 0, litMaterial, Vector2.zero);
        var glow = AddSprite(visual, "Glow", "Characters/astro_down_0_glow", 1, unlitMaterial, Vector2.zero);

        Sprite[] Frames(string dir, string suffix = "") =>
            Enumerable.Range(0, 3).Select(i => LoadSprite($"Characters/astro_{dir}_{i}{suffix}")).ToArray();
        root.AddComponent<CharacterAnimator>().Setup(body, glow,
            Frames("down"), Frames("up"), Frames("side"),
            Frames("down", "_glow"), Frames("up", "_glow"), Frames("side", "_glow"));

        // Petite lampe de casque : le perso reste lisible dans le noir
        var helmet = PointLight(root.transform, "Helmet Light", new Vector2(0f, 1.1f), HelmetColor, 0.55f, 3.2f, false);
        helmet.gameObject.AddComponent<LightFlicker>().Setup(LightFlicker.Mode.Steady, 0.55f, 0f, 0f, 0.1f);
        return root.transform;
    }

    private static void BuildDrone(Vector2 position)
    {
        var root = new GameObject("Drone");
        root.transform.SetParent(propsRoot, false);
        root.transform.position = position;
        root.AddComponent<SortingGroup>();

        var col = root.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.9f, 0.6f);
        col.offset = new Vector2(0f, 0.3f);
        root.AddComponent<EncounterTrigger>();

        AddSprite(root.transform, "Shadow", "Characters/shadow", -1, litMaterial, new Vector2(0f, 0.06f));
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        visual.localPosition = new Vector3(0f, 0.4f, 0f);
        visual.gameObject.AddComponent<Hover>();
        AddSprite(visual, "Body", "Characters/drone", 0, litMaterial, Vector2.zero);
        AddSprite(visual, "Glow", "Characters/drone_glow", 1, unlitMaterial, Vector2.zero);

        var eye = PointLight(visual, "Eye Light", new Vector2(0f, 0.5f), RedColor, 1f, 2.2f, false);
        eye.gameObject.AddComponent<LightFlicker>().Setup(LightFlicker.Mode.Flicker, 1f, 0.2f, 8f, 0.4f);
    }

    // ------------------------------------------------------------------ décor

    private static void BuildProps(RectInt bounds, RectInt platform, System.Random rng, Vector2[] keepClear)
    {
        var placed = new List<Vector2>(keepClear);

        // Piliers aux coins de la plateforme, avec leur bande lumineuse
        foreach (var p in new[] { new Vector2(-5.5f, 3.4f), new Vector2(5.5f, 3.4f), new Vector2(-5.5f, -3.6f), new Vector2(5.5f, -3.6f) })
        {
            var pillar = Prop("Pillar", "Props/pillar", p, new Vector2(0.7f, 0.35f), true);
            var l = PointLight(pillar, "Strip Light", new Vector2(0f, 1f), CyanColor, 0.6f, 1.6f, false);
            l.gameObject.AddComponent<LightFlicker>().Setup(LightFlicker.Mode.Pulse, 0.6f, 0.15f, 1.5f, 0.3f);
            placed.Add(p);
        }

        // Lampadaires : la lumière chaude principale, avec ombres portées
        foreach (var p in new[] { new Vector2(-3f, 3.6f), new Vector2(3f, 3.6f), new Vector2(-9f, -6.5f), new Vector2(10f, 4.5f) })
        {
            var lamp = Prop("Lamp", "Props/lamp", p, new Vector2(0.3f, 0.2f), false);
            var l = PointLight(lamp, "Lamp Light", new Vector2(0f, 1.55f), LampColor, 1.4f, 5.5f, true);
            l.gameObject.AddComponent<LightFlicker>().Setup(LightFlicker.Mode.Flicker, 1.4f, 0.08f, 3f, 0.15f);
            placed.Add(p);
        }

        // Caisses
        foreach (var p in new[] { new Vector2(-4.3f, 2.4f), new Vector2(-3.3f, 2.4f), new Vector2(4.2f, -2.6f),
                     new Vector2(9f, -1.5f), new Vector2(-10.5f, 2.5f), new Vector2(-11.5f, 2.2f) })
        {
            Prop("Crate", "Props/crate", p, new Vector2(0.95f, 0.55f), true, 0.28f);
            placed.Add(p);
        }

        // Balise : grande lumière cyan qui pulse
        var beaconPos = new Vector2(0f, 6f);
        var beacon = Prop("Beacon", "Props/beacon", beaconPos, new Vector2(0.5f, 0.3f), false);
        var bl = PointLight(beacon, "Beacon Light", new Vector2(0f, 1.28f), CyanColor, 1.3f, 4.5f, true);
        bl.gameObject.AddComponent<LightFlicker>().Setup(LightFlicker.Mode.Pulse, 1.3f, 0.25f, 2.2f, 0.25f);
        placed.Add(beaconPos);

        // Rochers et plantes lumineuses dispersés hors de la plateforme
        var avoid = new Rect(platform.xMin - 1.5f, platform.yMin - 1.5f, platform.width + 3f, platform.height + 3f);
        for (var i = 0; i < 14; i++)
        {
            if (!TryRandomSpot(rng, bounds, avoid, placed, 1.6f, out var p)) continue;
            var big = i % 2 == 0;
            Prop("Rock", big ? "Props/rock_0" : "Props/rock_1", p,
                big ? new Vector2(0.85f, 0.4f) : new Vector2(0.6f, 0.35f), true, 0.2f);
            placed.Add(p);
        }
        for (var i = 0; i < 18; i++)
        {
            if (!TryRandomSpot(rng, bounds, avoid, placed, 1.2f, out var p)) continue;
            var plant = Prop("Glow Plant", "Props/glowplant", p, null, false);
            if (i % 2 == 0)
            {
                var l = PointLight(plant, "Plant Light", new Vector2(0f, 0.5f), i % 4 == 0 ? PinkColor : CyanColor, 0.8f, 1.6f, false);
                l.gameObject.AddComponent<LightFlicker>().Setup(LightFlicker.Mode.Pulse, 0.8f, 0.2f, 1.2f, 0f);
            }
            placed.Add(p);
        }
    }

    private static bool TryRandomSpot(System.Random rng, RectInt bounds, Rect avoid, List<Vector2> placed, float minDistance, out Vector2 result)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var p = new Vector2(
                bounds.xMin + 1f + (float)rng.NextDouble() * (bounds.width - 2f),
                bounds.yMin + 1f + (float)rng.NextDouble() * (bounds.height - 3f));
            p = new Vector2(Mathf.Round(p.x * 16f) / 16f, Mathf.Round(p.y * 16f) / 16f);   // aligné au pixel
            if (avoid.Contains(p)) continue;
            if (placed.Any(q => Vector2.Distance(p, q) < minDistance)) continue;
            result = p;
            return true;
        }
        result = default;
        return false;
    }

    /// <summary>Objet de décor : sprite éclairé, calque lumineux éventuel, collision à la base, ombre portée.</summary>
    private static Transform Prop(string name, string sprite, Vector2 position, Vector2? colliderSize, bool castShadow, float colliderOffsetY = 0.15f)
    {
        var root = new GameObject(name);
        root.transform.SetParent(propsRoot, false);
        root.transform.position = position;
        root.AddComponent<SortingGroup>();

        var body = AddSprite(root.transform, "Sprite", sprite, 0, litMaterial, Vector2.zero);
        if (TryLoadSprite(sprite + "_glow", out _))
            AddSprite(root.transform, "Glow", sprite + "_glow", 1, unlitMaterial, Vector2.zero);

        if (colliderSize.HasValue)
        {
            var box = root.AddComponent<BoxCollider2D>();
            box.size = colliderSize.Value;
            box.offset = new Vector2(0f, colliderOffsetY);
        }
        if (castShadow)
        {
            var caster = body.gameObject.AddComponent<ShadowCaster2D>();
            caster.castsShadows = true;
            caster.selfShadows = false;
        }
        return root.transform;
    }

    // ------------------------------------------------------------------ caméra, lumière globale, post-process

    private static void BuildCamera(Transform player, RectInt bounds)
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        go.transform.position = new Vector3(player.position.x, player.position.y, -10f);

        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.625f;                       // 180 px de haut / 2 / 16 px par unité
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.05f, 0.09f);
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        var ppc = go.AddComponent<PixelPerfectCamera>();
        ppc.assetsPPU = PixelArtImporter.PixelsPerUnit;
        ppc.refResolutionX = 320;                              // résolution "SNES moderne" en 16:9
        ppc.refResolutionY = 180;
        ppc.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
        ppc.cropFrame = PixelPerfectCamera.CropFrame.None;

        go.AddComponent<CameraFollow>().Setup(player, new Rect(bounds.xMin, bounds.yMin, bounds.width, bounds.height));
        go.AddComponent<AudioListener>();
    }

    private static void BuildGlobalLight()
    {
        var go = new GameObject("Global Light 2D");
        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.color = new Color(0.30f, 0.36f, 0.70f);
        light.intensity = 0.35f;
        light.targetSortingLayers = AllSortingLayers();
        go.AddComponent<DayNightCycle>();
    }

    private static void BuildPostProcess()
    {
        EnsureFolder(MaterialsFolder);
        if (File.Exists(ProfilePath)) AssetDatabase.DeleteAsset(ProfilePath);

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.8f);
        bloom.intensity.Override(1.4f);
        bloom.scatter.Override(0.65f);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(new Color(0.02f, 0.02f, 0.08f));

        var grading = profile.Add<ColorAdjustments>(true);
        grading.contrast.Override(12f);
        grading.saturation.Override(12f);

        foreach (var component in profile.components)
        {
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var volume = new GameObject("Post Process").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = profile;
    }

    // ------------------------------------------------------------------ utilitaires

    private static Light2D PointLight(Transform parent, string name, Vector2 localPosition, Color color, float intensity, float radius, bool shadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.pointLightOuterRadius = radius;
        light.pointLightInnerRadius = 0.2f;
        light.falloffIntensity = 0.6f;
        light.shadowsEnabled = shadows;
        light.shadowIntensity = shadows ? 0.75f : 0f;
        light.shadowSoftness = 0.3f;
        light.targetSortingLayers = AllSortingLayers();

        // Active les normal maps (propriété en lecture seule côté code : on passe par la sérialisation)
        var so = new SerializedObject(light);
        var quality = so.FindProperty("m_NormalMapQuality");
        if (quality != null)
        {
            var idx = System.Array.IndexOf(quality.enumNames, "Accurate");
            if (idx >= 0) quality.enumValueIndex = idx;
        }
        var distance = so.FindProperty("m_NormalMapDistance");
        if (distance != null) distance.floatValue = 1.5f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return light;
    }

    private static SpriteRenderer AddSprite(Transform parent, string name, string sprite, int order, Material material, Vector2 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite(sprite);
        sr.sharedMaterial = material;
        sr.sortingOrder = order;
        return sr;
    }

    private static Sprite LoadSprite(string relativePath)
    {
        if (!TryLoadSprite(relativePath, out var sprite))
            Debug.LogError($"[Station] Sprite introuvable : {SpritesFolder}/{relativePath}.png");
        return sprite;
    }

    private static bool TryLoadSprite(string relativePath, out Sprite sprite)
    {
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesFolder}/{relativePath}.png");
        return sprite != null;
    }

    private static int[] AllSortingLayers() => SortingLayer.layers.Select(l => l.id).ToArray();

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void AddToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.All(s => s.path != path))
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

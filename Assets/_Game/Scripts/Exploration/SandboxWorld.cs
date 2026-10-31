using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>Une scène légère reconstruit le niveau LDtk à chaque Play / retour de combat.</summary>
public sealed class SandboxWorld : MonoBehaviour
{
    [SerializeField] private LdtkMap map;
    [SerializeField] private SandboxArt art;
    [SerializeField] private string startingLevel;
    private readonly List<Sprite> generatedSprites = new List<Sprite>();
    private ToolboxBehaviour? Behaviour(LdtkEntity e) => map.toolbox?.Find(e.__identifier)?.behaviour;
    private bool IsEnemy(LdtkEntity e) => Behaviour(e) == ToolboxBehaviour.Enemy || (Behaviour(e) == null && (e.__identifier == "Drone" || e.__identifier == "Sentinel"));
    private LdtkLevel level;
    private Transform player;
    private readonly List<Tile> tiles = new List<Tile>();
    private LdtkEntity nearest;
    private string notice = "E : interagir avec les bornes. Contact avec un monstre : combat.";
    private float noticeUntil = 7;
    private bool interactRequested;
    public LdtkLevel Level => level;

    public void Configure(LdtkMap source, SandboxArt visuals) { map = source; art = visuals; }

    private void Awake()
    {
        try
        {
            var project = map.Read();
            if (GameSession.WorldMap != map) { GameSession.WorldMap = map; GameSession.WorldLevel = startingLevel; }
            level = project.levels.FirstOrDefault(l => l.identifier == GameSession.WorldLevel) ?? project.levels[0];
            GameSession.WorldLevel = level.identifier;
        }
        catch (System.Exception e) { Debug.LogError("[Bac à sable] " + e.Message, this); enabled = false; return; }
        BuildGround();
        BuildTiles();
        var spawn = level.Entities.Single(e => e.__identifier == "PlayerSpawn");
        player = BuildPlayer(level.Position(spawn));
        foreach (var entity in level.Entities) BuildEntity(entity);
        BuildCamera();
        var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
        light.transform.SetParent(transform);
        light.lightType = Light2D.LightType.Global;
        light.gameObject.AddComponent<DayNightCycle>().Setup(0.62f, false);
    }

    private void BuildGround()
    {
        var grid = new GameObject("Terrain LDtk", typeof(Grid));
        grid.transform.SetParent(transform);
        var ground = new GameObject("Sol", typeof(Tilemap), typeof(TilemapRenderer));
        ground.transform.SetParent(grid.transform, false);
        var tilemap = ground.GetComponent<Tilemap>();
        var renderer = ground.GetComponent<TilemapRenderer>();
        renderer.sharedMaterial = art.lit;
        renderer.sortingOrder = -100;
        for (int i = 0; i < 9; i++)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = art.Sprite(i < 4 ? "Tiles/moss_" + i : "Tiles/deck_" + ((i - 4) % 4));
            if (i == 8) tile.color = new Color(0.35f, 0.40f, 0.58f);
            tiles.Add(tile);
        }
        var terrain = level.Terrain;
        for (int y = 0; y < terrain.__cHei; y++)
        {
            int run = -1;
            for (int x = 0; x <= terrain.__cWid; x++)
            {
                int value = x < terrain.__cWid ? terrain.intGridCsv[y * terrain.__cWid + x] : 1;
                if (x < terrain.__cWid && value > 0)
                {
                    int index = value == 3 ? 8 : (value == 2 ? 4 : 0) + (x * 13 + y * 7) % 4;
                    tilemap.SetTile(new Vector3Int(x, -y - 1, 0), tiles[index]);
                }
                bool blocked = x < terrain.__cWid && !LdtkLevel.Walkable(value);
                if (blocked && run < 0) run = x;
                if (!blocked && run >= 0)
                {
                    Box("Collision terrain", new Vector2((run + x) / 2f, -y - 0.5f), new Vector2(x - run, 1));
                    run = -1;
                }
            }
        }
        // Empêche la sortie même après avoir peint du sol sur les bords dans LDtk.
        var b = level.Bounds;
        Box("Bord nord", new Vector2(b.center.x, 0.5f), new Vector2(b.width + 2, 1));
        Box("Bord sud", new Vector2(b.center.x, b.yMin - 0.5f), new Vector2(b.width + 2, 1));
        Box("Bord ouest", new Vector2(-0.5f, b.center.y), new Vector2(1, b.height));
        Box("Bord est", new Vector2(b.xMax + 0.5f, b.center.y), new Vector2(1, b.height));
    }

    private void Box(string label, Vector2 position, Vector2 size)
    {
        var box = new GameObject(label).AddComponent<BoxCollider2D>();
        box.transform.SetParent(transform);
        box.transform.position = position;
        box.size = size;
    }

    private Transform BuildPlayer(Vector3 position)
    {
        var root = Root("Player", position);
        var col = root.AddComponent<CircleCollider2D>(); col.radius = 0.24f;
        root.AddComponent<PlayerMovement>();
        Sprite(root.transform, "Characters/shadow", -1);
        var body = Sprite(root.transform, "Characters/astro_down_0");
        var glow = Sprite(root.transform, "Characters/astro_down_0_glow", 1, true);
        root.AddComponent<CharacterAnimator>().Setup(body, glow, art.Frames("down"), art.Frames("up"), art.Frames("side"),
            art.Frames("down", "_glow"), art.Frames("up", "_glow"), art.Frames("side", "_glow"));
        Lamp(root.transform, new Color(0.55f, 0.82f, 1), 0.65f, 2.5f);
        return root.transform;
    }

    private void BuildEntity(LdtkEntity entity)
    {
        string type = entity.__identifier;
        if (type == "PlayerSpawn") return;
        var definition = map.toolbox?.Find(type);
        if (definition != null) { BuildCatalogEntity(entity, definition); return; }
        bool enemy = type == "Drone" || type == "Sentinel";
        if (enemy && GameSession.IsDefeated(level.EncounterId(entity))) return;
        var root = Root(type + "_" + entity.iid.Substring(0, 6), level.Position(entity));
        if (enemy)
        {
            root.SetActive(false); // Affecter l'identifiant avant Awake (retour de combat).
            var trigger = root.AddComponent<CircleCollider2D>(); trigger.radius = 0.52f; trigger.isTrigger = true;
            var encounter = root.AddComponent<EncounterTrigger>();
            encounter.Setup(level.EncounterId(entity), type == "Sentinel" ? art.sentinel : art.drone, "SandboxCombat");
            var movement = root.AddComponent<SandboxPatrol>();
            movement.radius = Mathf.Clamp(entity.Number("PatrolRadius", 24), 0, 128) / 16f;
            movement.speed = Mathf.Clamp(entity.Number("Speed", 0.8f), 0, 4);
            Sprite(root.transform, "Characters/shadow", -1);
            var visual = new GameObject("Drone animé").transform;
            visual.SetParent(root.transform, false); visual.localPosition = Vector3.up * 0.25f;
            visual.gameObject.AddComponent<Hover>();
            var body = Sprite(visual, "Characters/drone");
            body.color = type == "Sentinel" ? new Color(0.7f, 0.8f, 1f) : Color.white;
            Sprite(visual, "Characters/drone_glow", 1, true);
            Lamp(visual, type == "Sentinel" ? PrototypeGui.Cyan : new Color(1, 0.25f, 0.35f), 0.9f, 2);
            root.SetActive(true);
            return;
        }
        string key;
        switch (type)
        {
            case "Crate": key = "crate"; break;
            case "Rock": key = "rock_0"; break;
            case "Lamp": key = "lamp"; break;
            case "Plant": key = "glowplant"; break;
            case "Sign": key = "pillar"; break;
            default: key = "beacon"; break;
        }
        var sr = Sprite(root.transform, "Props/" + key);
        if (type == "Medbay") sr.color = new Color(0.6f, 1, 0.75f);
        if (art.Sprite("Props/" + key + "_glow") != null) Sprite(root.transform, "Props/" + key + "_glow", 1, true);
        if (type != "Plant")
        {
            var collider = root.AddComponent<BoxCollider2D>();
            collider.size = type == "Crate" || type == "Rock" ? new Vector2(0.8f, 0.5f) : new Vector2(0.4f, 0.35f);
            collider.offset = new Vector2(0, 0.15f);
        }
        if (type == "Lamp" || type == "Medbay" || type == "Beacon" || type == "Plant")
            Lamp(root.transform, type == "Lamp" ? PrototypeGui.Gold : type == "Medbay" ? new Color(0.4f, 1, 0.6f) : PrototypeGui.Cyan,
                type == "Plant" ? 0.6f : 1.2f, type == "Lamp" ? 4f : 2.5f);
    }

    private GameObject Root(string label, Vector3 position)
    {
        var root = new GameObject(label);
        root.transform.SetParent(transform); root.transform.position = position;
        root.AddComponent<SortingGroup>();
        return root;
    }

    private SpriteRenderer Sprite(Transform parent, string key, int order = 0, bool glow = false)
    {
        var sr = new GameObject(key).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(parent, false);
        sr.sprite = art.Sprite(key); sr.sortingOrder = order; sr.sharedMaterial = glow ? art.unlit : art.lit;
        return sr;
    }

    private void Lamp(Transform parent, Color color, float intensity, float radius)
    {
        var light = new GameObject("Light 2D").AddComponent<Light2D>();
        light.transform.SetParent(parent, false); light.transform.localPosition = new Vector3(0, 0.8f, 0);
        light.lightType = Light2D.LightType.Point; light.color = color; light.intensity = intensity;
        light.pointLightOuterRadius = radius; light.pointLightInnerRadius = 0.2f;
        light.gameObject.AddComponent<LightFlicker>().Setup(LightFlicker.Mode.Pulse, intensity, 0.12f, 2, 0.5f);
    }

    private void BuildCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        go.transform.SetParent(transform); go.transform.position = player.position + Vector3.back * 10;
        var camera = go.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 5.625f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.035f, 0.04f, 0.09f);
        camera.transparencySortMode = TransparencySortMode.CustomAxis; camera.transparencySortAxis = Vector3.up;
        var pixels = go.AddComponent<PixelPerfectCamera>();
        pixels.assetsPPU = 16; pixels.refResolutionX = 320; pixels.refResolutionY = 180;
        pixels.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
        pixels.cropFrame = PixelPerfectCamera.CropFrame.StretchFill;
        go.AddComponent<CameraFollow>().Setup(player, level.Bounds);
        go.AddComponent<AudioListener>();
    }

    private int EnemyCount => level.Entities.Count(IsEnemy);
    private int DefeatedCount => level.Entities.Count(e => IsEnemy(e) && GameSession.IsDefeated(level.EncounterId(e)));

    private void Update()
    {
        if (player == null || GameMenu.IsOpen || SceneTransition.IsBusy || GameSession.HasPendingEncounter) { interactRequested = false; return; }
        nearest = level.Entities.Where(e => e.__identifier == "Medbay" || e.__identifier == "Beacon" || e.__identifier == "Sign" || e.__identifier == "Portal" || Behaviour(e) == ToolboxBehaviour.Heal || Behaviour(e) == ToolboxBehaviour.Beacon || Behaviour(e) == ToolboxBehaviour.Sign || Behaviour(e) == ToolboxBehaviour.Portal)
            .Where(e => Vector2.Distance(player.position, level.Position(e)) < 1.6f)
            .OrderBy(e => Vector2.Distance(player.position, level.Position(e))).FirstOrDefault();
        if (interactRequested || Keyboard.current?.eKey.wasPressedThisFrame == true || Gamepad.current?.buttonSouth.wasPressedThisFrame == true)
            Interact();
        interactRequested = false;
        if (GameSession.BeaconActivated && Keyboard.current?.rKey.wasPressedThisFrame == true) GameSession.RestartSandbox();
    }

    private void Interact()
    {
        if (nearest == null) return;
        var definition = map.toolbox?.Find(nearest.__identifier);
        string action = definition == null ? nearest.__identifier : definition.behaviour == ToolboxBehaviour.Heal ? "Medbay" : definition.behaviour.ToString();
        switch (action)
        {
            case "Portal":
                GameSession.Travel(nearest.Text("Destination")); return;
            case "Sign": notice = nearest.Text("Text", definition?.text ?? "Explore la carte, puis active la balise."); break;
            case "Medbay": GameSession.Leader.Hp = GameSession.Leader.MaxHp; notice = "Équipe soignée. La borne reste utilisable à volonté."; break;
            case "Beacon":
                if (DefeatedCount < EnemyCount) notice = "Signal brouillé : neutralise les " + (EnemyCount - DefeatedCount) + " drones restants.";
                else { GameSession.BeaconActivated = true; notice = "Signal rétabli ! Bac à sable terminé. Explore librement ou presse R pour rejouer."; }
                break;
            default: notice = "Mission : neutraliser les drones, puis activer la balise au nord-est. Contact = combat. Borne verte = soin gratuit."; break;
        }
        noticeUntil = Time.time + 7;
    }

    private void OnGUI()
    {
        if (level == null || GameMenu.IsOpen) return;
        PrototypeGui.Begin();
        PrototypeGui.Panel(new Rect(18, 16, 325, 74));
        PrototypeGui.Text(new Rect(32, 23, 300, 28), level.identifier, 23, PrototypeGui.Cyan);
        PrototypeGui.Text(new Rect(32, 53, 300, 28), "" + GameSession.Leader.Name + "   " + GameSession.Leader.Hp + " / " + GameSession.Leader.MaxHp + " PV", 20);
        PrototypeGui.Panel(new Rect(638, 16, 304, 74));
        PrototypeGui.Text(new Rect(654, 24, 276, 28), GameSession.BeaconActivated ? "SIGNAL RÉTABLI" : level.Entities.Any(e => e.__identifier == "Beacon" || Behaviour(e) == ToolboxBehaviour.Beacon) ? "RÉTABLIR LE SIGNAL" : "EXPLORATION", 22, PrototypeGui.Gold);
        PrototypeGui.Text(new Rect(654, 54, 276, 28), "Drones neutralisés : " + DefeatedCount + " / " + EnemyCount, 18);
        if (nearest != null && !SceneTransition.IsBusy)
        {
            string action = nearest.__identifier == "Portal" || Behaviour(nearest) == ToolboxBehaviour.Portal ? "Changer de carte" : nearest.__identifier == "Medbay" || Behaviour(nearest) == ToolboxBehaviour.Heal ? "Se soigner" : nearest.__identifier == "Beacon" || Behaviour(nearest) == ToolboxBehaviour.Beacon ? "Activer la balise" : "Lire le terminal";
            if (PrototypeGui.Button(new Rect(330, 405, 300, 40), "[E / A] " + action)) interactRequested = true;
        }
        if (Time.time < noticeUntil)
        {
            PrototypeGui.Panel(new Rect(150, 340, 660, 58));
            PrototypeGui.Text(new Rect(164, 347, 632, 46), notice, 18);
        }
        PrototypeGui.Panel(new Rect(18, 479, 924, 43));
        PrototypeGui.Text(new Rect(32, 490, 900, 28), "ZQSD / WASD / flèches : marcher     E : interagir     Échap : menu     T : lumière" + (GameSession.BeaconActivated ? "     R : rejouer" : ""), 17, PrototypeGui.Muted);
        PrototypeGui.End();
    }

    private void OnDestroy()
    {
        foreach (var tile in tiles) if (tile != null) Destroy(tile);
        foreach (var sprite in generatedSprites) if (sprite != null) Destroy(sprite);
    }

    private void BuildCatalogEntity(LdtkEntity entity, WorldToolbox.Entry definition)
    {
        bool enemy = definition.behaviour == ToolboxBehaviour.Enemy;
        if (enemy && GameSession.IsDefeated(level.EncounterId(entity))) return;
        var root = Root(definition.id + "_" + entity.iid.Substring(0, Mathf.Min(6, entity.iid.Length)), level.Position(entity));
        root.SetActive(false);
        var sr = new GameObject("Visuel").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(root.transform, false); sr.sprite = definition.sprite;
        sr.color = definition.tint; sr.sharedMaterial = art.lit;
        if (definition.hover) sr.gameObject.AddComponent<Hover>();
        if (definition.lightIntensity > 0) Lamp(root.transform, definition.lightColor, definition.lightIntensity, definition.lightRadius);
        if (definition.animation != null && definition.animation.Length > 0)
            sr.gameObject.AddComponent<ToolboxAnimation>().Setup(sr, definition.animation, definition.framesPerSecond);
        if (enemy)
        {
            var col = root.AddComponent<CircleCollider2D>(); col.radius = .52f; col.isTrigger = true;
            root.AddComponent<EncounterTrigger>().Setup(level.EncounterId(entity), definition.combat, "SandboxCombat");
            var movement = root.AddComponent<SandboxPatrol>();
            movement.radius = Mathf.Clamp(entity.Number("PatrolRadius", definition.patrolRadius), 0, 128) / 16;
            movement.speed = Mathf.Clamp(entity.Number("Speed", definition.speed), 0, 4);
        }
        else if (definition.solid)
        {
            var col = root.AddComponent<BoxCollider2D>(); col.size = definition.colliderSize; col.offset = Vector2.up * col.size.y * .5f;
        }
        if (definition.behaviour == ToolboxBehaviour.Heal || definition.behaviour == ToolboxBehaviour.Beacon)
            Lamp(root.transform, definition.behaviour == ToolboxBehaviour.Heal ? Color.green : PrototypeGui.Cyan, 1.2f, 2.5f);
        root.SetActive(true);
    }

    private void BuildTiles()
    {
        // LDtk stocke les calques du premier plan vers l'arrière-plan.
        var cache = new Dictionary<string, Sprite>();
        for (int index = level.layerInstances.Length - 1; index >= 0; index--)
        {
            var layer = level.layerInstances[index];
            if (!layer.visible) continue;
            var placed = (layer.autoLayerTiles ?? System.Array.Empty<LdtkTile>()).Concat(layer.gridTiles ?? System.Array.Empty<LdtkTile>());
            foreach (var tile in placed)
            {
                var texture = map.Texture(layer.__tilesetDefUid);
                if (texture == null) throw new System.FormatException("Tileset manquant pour " + layer.__identifier);
                int size = layer.__gridSize;
                string key = layer.__tilesetDefUid + "/" + tile.src[0] + "/" + tile.src[1] + "/" + size;
                if (!cache.TryGetValue(key, out var sprite))
                {
                    sprite = UnityEngine.Sprite.Create(texture, new Rect(tile.src[0], texture.height - tile.src[1] - size, size, size), Vector2.one * .5f, 16);
                    cache.Add(key, sprite); generatedSprites.Add(sprite);
                }
                var sr = new GameObject(layer.__identifier + " tuile").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform);
                sr.transform.position = new Vector3((tile.px[0] + size * .5f) / 16, -(tile.px[1] + size * .5f) / 16, 0);
                sr.sprite = sprite; sr.sharedMaterial = art.lit;
                sr.sortingOrder = -90 + (level.layerInstances.Length - index);
                sr.flipX = (tile.f & 1) != 0; sr.flipY = (tile.f & 2) != 0;
                sr.color = new Color(1, 1, 1, layer.__opacity * tile.a);
            }
        }
    }
}

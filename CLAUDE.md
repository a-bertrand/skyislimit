# SkyIsLimit — RPG spatial rétro 16-bit (Unity)

## Projet
- JRPG au tour par tour, univers SF / spatial, pixel art 16-bit (ère SNES). Développeur solo : Anthony (dev full-stack Node/React/PostgreSQL/Python, débutant sur Unity), en vibe coding avec Claude, sur Mac.
- Concept différenciant : combats de vaisseaux au tour par tour en plus des combats de persos. Même moteur de combat (tours, file d'actions, effets, IA), mais mécaniques propres au vaisseau : équipage assigné aux postes, répartition d'énergie armes/boucliers/moteurs, ciblage de sous-systèmes, gestion de la distance.
- Inspirations : Chained Echoes, Sea of Stars (éclairage dynamique), Golden Sun (djinns, caméra dynamique).
- Production : d'abord un spin-off / préquel de 5-10 h, histoire bouclée. Suites façon Mass Effect avec import de sauvegarde.

## Stack
- Unity 6.6 (6000.6.7f1), template **Universal 2D** (URP + 2D Lights), Input System (nouveau, "Active Input Handling" = Input System Package).
- Rendu visé : URP 2D Lights, normal maps sur les sprites, Pixel Perfect Camera. Pixel art sous Aseprite.
- Dialogues : Yarn Spinner ou Ink (pas encore choisi). Versioning : Git (+ Git LFS à mettre en place pour les assets binaires).
- Build Windows depuis le Mac (Windows Build Support, Mono), à tester sur un vrai PC Windows.

## Règles de travail pour Claude
- **Ne jamais modifier** les fichiers `.unity`, `.prefab`, `.meta`, `.asset` ni `ProjectSettings/` à la main (YAML fragile). L'assemblage des scènes se fait dans l'éditeur : Claude écrit le code et donne les étapes éditeur, pas à pas (Anthony débute sur Unity).
- Ne pas déplacer/renommer/supprimer des assets depuis le système de fichiers (casse les `.meta`) : demander à Anthony de le faire dans la fenêtre Project de Unity. Créer de nouveaux scripts `.cs` est OK (Unity génère le `.meta`).
- Privilégier le code et les données à la configuration manuelle dans l'éditeur. Data-driven : persos, compétences, ennemis, vaisseaux en ScriptableObjects ou JSON.
- Le moteur de combat reste en **C# pur** (assembly `Game.Combat`, `noEngineReferences: true`), séparé des MonoBehaviour et de l'affichage, couvert par des tests NUnit (Unity Test Framework, EditMode). Code compatible C# 9 (pas de `record` ni `init`).
- Petites itérations : une feature, test, commit.
- Format de sauvegarde versionné avec flags narratifs dès le départ.
- Répondre en français.

## Arborescence
```
Assets/
├── _Game/                     ← tout le jeu
│   ├── Scenes/                ← Exploration.unity (Player + Wall), Combat.unity
│   ├── Scripts/
│   │   ├── Core/              ← GameSession (état partagé entre scènes, équipe, future sauvegarde), PartyMember
│   │   ├── Exploration/       ← PlayerMovement (top-down, Rigidbody2D, Input System), EncounterTrigger
│   │   ├── Combat/
│   │   │   ├── Core/          ← moteur C# pur (asmdef Game.Combat)
│   │   │   ├── Presentation/  ← BattleController (IMGUI prototype), CombatantDefinition (ScriptableObject)
│   │   │   └── Tests/         ← asmdef Game.Combat.Tests (12 tests EditMode)
│   │   └── UI/                ← GameMenu (menu de pause JRPG, créé automatiquement au lancement)
│   ├── Data/ (Characters, Skills, Enemies, Ships)
│   ├── Art/ (Sprites, Tilesets, Materials), Audio/ (Music, SFX), Prefabs/, Dialogues/
├── Editor/Projectsetup.cs     ← menu Tools > RPG > Créer la structure du projet (idempotent)
├── Scenes/SampleScene         ← du template, inutilisée
└── Settings/                  ← réglages URP du template, ne pas toucher
```

## Moteur de combat (v1)
- `Battle` : manches, tous les vivants agissent une fois par manche du plus rapide au plus lent. `Start()` puis `Execute(action)` pour `CurrentActor` ; chaque appel renvoie des `CombatEvent` (RoundStarted, TurnStarted, Attacked, Defended, Defeated, BattleEnded).
- Actions : Attaquer, Se défendre (garde = dégâts /2 jusqu'au prochain tour du défenseur).
- `DamageFormula` : attaque − défense/2, /2 si en garde, minimum 1.
- `BasicEnemyAI` (`IActionSelector`) : attaque l'adversaire vivant le plus faible.
- Stats par défaut : Héros 40 PV / 12 ATK / 6 DEF / 10 VIT ; Drone pirate 50 / 11 / 4 / 8 → victoire du héros à la manche 5 avec 8 PV.

## Pipeline visuel (direction : Sea of Stars)
- Pixel art 16 px = 1 unité, résolution de référence 320×180 (Pixel Perfect Camera URP, Grid Snapping = Pixel Snapping), caméra orthographique 5.625.
- `Assets/_Game/Art/Sprites/` : réglages d'import automatiques via `Editor/PixelArtImporter.cs` (Point, sans compression, pivot en bas au centre ; `Tiles/` et `shadow` au centre). Conventions : `nom_n.png` = normal map branchée automatiquement en texture secondaire `_NormalMap` ; `nom_glow.png` = calque lumineux affiché avec Sprite-Unlit-Default (non affecté par l'éclairage, déclenche le bloom).
- Tri : Renderer2D en Custom Axis (0,1,0) ; chaque objet a un `SortingGroup` et son pivot aux pieds ; sol en Tilemap à l'ordre -100.
- Lumières : Light2D globale + `DayNightCycle` (maintenir T), lumières ponctuelles avec normal maps (Accurate, distance 1.5) et `LightFlicker` (Steady/Flicker/Pulse, plus fortes la nuit), ombres via `ShadowCaster2D` sur piliers/caisses/rochers.
- Post-process : Volume global (`Art/Materials/StationPostProcess.asset`) avec Bloom, Vignette, ColorAdjustments ; caméra avec Post Processing activé.
- Scène de démo `Station` générée par `Editor/StationSceneBuilder.cs` (Tools > RPG > Construire la scène Station). Le pixel art actuel est un placeholder généré par code, à remplacer par de l'Aseprite.

## Équipe et menu
- `PartyMember` (Scripts/Core) : stats + PV actuels hors combat. `GameSession.Party` / `GameSession.Leader` (héros par défaut 40/12/6/10, créé à la première lecture).
- Le combat crée le héros depuis `GameSession.Leader.ToCombatant()` (constructeur `Combatant(..., currentHp)`). Victoire : les PV restants sont conservés ; défaite : PV restaurés au max (prototype). Pas encore de soin.
- `GameMenu` (Scripts/UI) : se crée tout seul (`RuntimeInitializeOnLoadMethod`, DontDestroyOnLoad), s'ouvre avec Échap/Tab/Start seulement si un `PlayerMovement` est dans la scène, met `Time.timeScale` à 0. Interface uGUI construite en code sur une grille 320×180 agrandie d'un facteur entier ; police pixel `Resources/Fonts/PixelifySans.ttf` (OFL, rendu Hinted Raster via PixelArtImporter). Commandes : Statut, Reprendre.

## État au 30/10/2026
- Fait et commité : structure, déplacement, combat prototype v1, passage exploration ↔ combat, démo visuelle Station, `MobMovement`, équipe + PV persistants, menu de jeu (validé en jeu par Anthony).
- Également commité : le bac à sable « Nacre » (scènes Sandbox / SandboxCombat, import LDtk `Assets/_Game/Maps/*.ldtk`, `SceneTransition`), décrit dans `README-PROTOTYPE.md`.

## Prochaines étapes
1. Valider le passage exploration ↔ combat en jeu, commit.
2. Caméra qui suit le joueur, Pixel Perfect Camera, premiers sprites Aseprite.

## Bac à sable LDtk (prototype Nacre)
- Point d'entrée : `Assets/_Game/Scenes/Sandbox.unity`, combat : `SandboxCombat.unity`.
- Menu `Tools > RPG > Bac à sable` : préparation idempotente, ouverture LDtk, validations et test de la boucle jouable.
- Source de carte : `Assets/_Game/Maps/Nacre.ldtk`, réimport automatique par `LdtkMapImporter`. Un niveau, IntGrid `Terrain` de 16 px + Entities. Valeurs : 0 vide bloquant, 1 mousse, 2 métal, 3 mur. Les autres formats sont hors périmètre v1.
- Entités : PlayerSpawn, Drone, Sentinel, Medbay, Beacon, Sign, Crate, Rock, Lamp, Plant. Rayon/vitesse de patrouille dans LDtk ; stats ennemies dans `Data/Sandbox/Drone.asset` et `Sentinel.asset`.
- `SandboxWorld` construit la carte en Play ; la source des placements reste LDtk. `SandboxPatrol` complète `MobMovement` sans le remplacer. Identifiants de rencontres = IID niveau + IID entité.
- Combat : commandes clavier/manette/souris, attaques séquencées et feedback de dégâts, fond spatial généré. Transitions avec fondu. Défaite = soin + respawn au départ ; victoire = PV et position conservés, ennemi supprimé pour la session.
- Soin contextuel avec E, objectif « trois drones puis balise », R pour rejouer après l'objectif. Pas de sauvegarde disque.
- Guide complet et provenance des images : `README-PROTOTYPE.md`. Contrôles relançables : `SandboxChecks`, rapports dans `Logs/sandbox-*`.

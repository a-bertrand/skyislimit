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
│   │   ├── Core/              ← GameSession (état partagé entre scènes ; future sauvegarde)
│   │   ├── Exploration/       ← PlayerMovement (top-down, Rigidbody2D, Input System), EncounterTrigger
│   │   ├── Combat/
│   │   │   ├── Core/          ← moteur C# pur (asmdef Game.Combat)
│   │   │   ├── Presentation/  ← BattleController (IMGUI prototype), CombatantDefinition (ScriptableObject)
│   │   │   └── Tests/         ← asmdef Game.Combat.Tests (12 tests EditMode)
│   │   └── UI/
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

## État au 30/10/2026
- Fait : projet créé, structure en place, perso qui se déplace dans Exploration (Player + Wall), scènes Exploration et Combat dans `_Game/Scenes` et dans les Build Settings, combat prototype validé en jeu (GameObject `Battle` + `BattleController` dans la scène Combat).
- En cours : passage exploration ↔ combat. `GameSession` (Scripts/Core, statique, remis à zéro à chaque Play car le Domain Reload est désactivé) mémorise la rencontre, la scène et la position de retour, et les ennemis vaincus. `EncounterTrigger` (Scripts/Exploration) sur un sprite ennemi lance le combat au contact (délai de grâce 1 s au chargement). `BattleController` utilise l'ennemi de la rencontre et propose Continuer / Réessayer / Retour à la carte. `PlayerMovement` replace le joueur à sa position au retour.

## Prochaines étapes
1. Valider le passage exploration ↔ combat en jeu, commit.
2. Caméra qui suit le joueur, Pixel Perfect Camera, premiers sprites Aseprite.

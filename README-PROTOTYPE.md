# SkyIsLimit — premier bac à sable

**Boîte à outils et plusieurs cartes :** consulter [GUIDE-BOITE-A-OUTILS.md](GUIDE-BOITE-A-OUTILS.md). Menu Unity **Tools → RPG → Boîte à outils**, exemple **Ouvrir l’atelier jouable**. Les limitations v1 ci-dessous décrivent le prototype initial ; le nouveau guide décrit le support actuel.

Un avant-poste spatial, un astronaute, trois drones et une balise à remettre en service. **Nacre** est un nom de travail pour la démo, sans engagement sur l'histoire définitive.

## Jouer

1. Ouvrir le projet avec Unity **6000.6.7f1** et attendre la compilation.
2. Menu **Tools → RPG → Bac à sable → 1 - Préparer et ouvrir**. Les scènes sont créées avec l'API Unity ; les scènes Station, Exploration et Combat sont conservées.
3. Cliquer **Play**, puis cliquer dans la vue **Game** pour lui donner le clavier. Agrandir cette vue si nécessaire.
4. Toucher un drone pour combattre. Neutraliser les trois drones, puis interagir avec la balise au nord-est.

| Action | Clavier | Manette |
| --- | --- | --- |
| Marcher | ZQSD sur AZERTY, WASD sur QWERTY, ou flèches | Stick gauche |
| Lire / soigner / activer | E, ou bouton à l'écran | A / bouton sud |
| Menu pause | Échap / Tab | Start |
| Attaquer / continuer après combat | Entrée / Espace, ou bouton à l'écran | A / bouton sud |
| Défendre | G, ou bouton à l'écran | X / bouton ouest |
| Faire varier la lumière | Maintenir T | — |
| Rejouer après l'objectif | R | — |

La borne verte près du départ soigne gratuitement et sans limite. Les blessures restent après une victoire. Une défaite ramène au point de départ avec les PV pleins et laisse le drone sur la carte. Le bouton Réessayer relance le combat avec les PV pleins. La progression reste en mémoire pendant la partie ; elle n'est pas sauvegardée sur disque.

## Modifier la carte avec LDtk

Le fichier de travail est **`Assets/_Game/Maps/Nacre.ldtk`**. Le projet initial `ldtk-maps/Test.ldtk` est conservé avec ses modifications enregistrées depuis LDtk.

1. Quitter Play dans Unity.
2. Ouvrir `Nacre.ldtk` dans LDtk (menu Unity **2 - Ouvrir la carte dans LDtk**, ou bouton Load à l’accueil de LDtk).
3. Sélectionner **Terrain**. Peindre les valeurs existantes :
   - **Moss (1)** : sol végétal praticable.
   - **Metal (2)** : sol métallique praticable.
   - **Wall (3)** : obstacle solide.
   - **Effacer / 0** : vide infranchissable.
4. Sélectionner **Entities** et placer / déplacer les objets ci-dessous.
5. Enregistrer dans LDtk. Revenir dans Unity, attendre l'import et relancer Play.

**Pas d'export ni de copie de JSON à faire.** L'importeur Unity embarque le fichier dans le jeu. La scène reconstruit le niveau au lancement : modifier les objets pendant Play n'enregistre pas la carte. La source du placement est LDtk.

| Entité | Fonction |
| --- | --- |
| `PlayerSpawn` | Point de départ et de retour après une défaite. Exactement un. |
| `Drone` | Ennemi éclaireur. Contact = combat. |
| `Sentinel` | Variante plus résistante et bleutée. |
| `Medbay` | Borne de soin. Au moins une. |
| `Beacon` | Objectif : s'active une fois tous les ennemis neutralisés. Exactement une. |
| `Sign` | Terminal rappelant les commandes et la mission. |
| `Crate`, `Rock` | Décors avec collision à leur base. |
| `Lamp` | Éclairage chaud et collision à la base. |
| `Plant` | Végétation lumineuse traversable. |

Sur `Drone` et `Sentinel`, **PatrolRadius** règle l'amplitude horizontale en pixels (16 px = une tuile) et **Speed** la vitesse en unités Unity / seconde. Rayon 0 = immobile. Les patrouilles font demi-tour devant un obstacle. L'identité d'une rencontre vient de l'IID LDtk : dupliquer un drone crée une nouvelle rencontre automatiquement.

Placer les pieds des entités sur du sol praticable et laisser un chemin vers le soin et la balise. Ne pas superposer le point de départ et un décor solide. Le menu **3 - Valider la carte et le moteur** vérifie notamment la grille, les identifiants et la connexité du terrain (les petits obstacles de décor restent à vérifier en jeu).

### Périmètre de l'importeur v1

Un projet LDtk à **un seul niveau**, grille de **16 px**, un calque IntGrid nommé **Terrain**, et des calques Entities sans décalage. Les tuiles visibles sont choisies automatiquement d'après la valeur du terrain. Les calques Tiles / AutoLayer, les mondes multiples, les niveaux externes, les champs texte et les passages entre niveaux ne sont pas encore pris en charge. Conserver ces réglages pour ce premier prototype ; les erreurs de format prises en charge sont signalées dans la Console Unity.

Les tilesets LDtk pointent vers les PNG existants du projet. Les sauvegardes LDtk vont dans `ldtk-maps/backups/`. Pour déplacer des assets, utiliser la fenêtre Project de Unity afin de conserver leurs `.meta`.

## Où changer quoi

| Besoin | Emplacement |
| --- | --- |
| Carte, collisions, positions | `Assets/_Game/Maps/Nacre.ldtk` dans LDtk |
| PV, attaque, défense, vitesse des ennemis | `Assets/_Game/Data/Sandbox/Drone.asset` et `Sentinel.asset` dans l'Inspector Unity |
| Sprites et décor de combat | `Assets/_Game/Data/Sandbox/SandboxArt.asset` dans l'Inspector |
| Construction du monde et interactions | `Assets/_Game/Scripts/Exploration/SandboxWorld.cs` |
| Lecture et validation LDtk | `Assets/_Game/Scripts/Exploration/LdtkMap.cs` et `Assets/Editor/LdtkMapImporter.cs` |
| Animation de marche | `CharacterAnimator.cs` (existant, réutilisé) |
| Patrouilles du prototype | `SandboxPatrol.cs` (`MobMovement.cs` reste disponible pour tes essais) |
| État partagé entre carte et combat | `Assets/_Game/Scripts/Core/GameSession.cs` |
| Règles de combat, sans Unity | `Assets/_Game/Scripts/Combat/Core/` |
| Affichage et animations de combat | `Assets/_Game/Scripts/Combat/Presentation/BattleController.cs` |
| Préparation des scènes | `Assets/Editor/SandboxSetup.cs` |

La scène **Sandbox** contient la configuration ; ses objets sont créés en Play. **SandboxCombat** est la scène de combat du prototype. Les scènes d'origine utilisent toujours leur configuration d'ennemis ; leur présentation de combat bénéficie des nouvelles commandes et animations, avec formes de secours si aucun catalogue visuel n'est assigné.

## Vérifier les changements

- **Tools → RPG → Bac à sable → 3 - Valider la carte et le moteur** : validation LDtk et tests NUnit du moteur.
- **Tools → RPG → Bac à sable → 4 - Tester la boucle jouable** : démarre une session de test, exerce le soin, la pause, le contact physique, la défaite, les victoires, les retours de scène, la balise et la remise à zéro ; quitte Play à la fin. Ne pas intervenir pendant ce test.
- Dernière validation : **15 tests NUnit**, **14 contrôles de carte/ressources** et **24 vérifications en Play** réussis. Le fichier LDtk a également été contrôlé contre le schéma officiel 1.5.3. L’ouverture manuelle de Nacre dans LDtk reste à confirmer : l’automatisation du sélecteur macOS ne permettait pas de valider le fichier.
- Rapports locaux : `Logs/sandbox-editmode.txt`, `Logs/sandbox-combat.xml`, `Logs/sandbox-playmode.txt`.

Validation réalisée dans l’éditeur Unity sur Mac ; aucun exécutable Windows n’a été testé. Déplacement au clavier, contact avec un drone et attaque avec Espace également vérifiés manuellement.

Les contrôles de la boucle jouable utilisent la vraie physique, les scènes et les coroutines. Ils déplacent le héros automatiquement pour atteindre les rencontres et pilotent les commandes ; ils ne remplacent pas un essai du ressenti au clavier.

## Visuels provisoires

Les sprites, neuf images de marche, normal maps et décors déjà présents sont réutilisés. Un fond de combat original a été généré avec l'outil intégré **imagegen** et enregistré dans **`Assets/_Game/Art/Backgrounds/outpost-battle.png`**. L'ambition de ce premier lot est la lisibilité, les couleurs et l'éclairage ; le niveau de finition de Sea of Stars reste une direction, pas un résultat atteint par ces placeholders.

Prompt de génération utilisé :

> Use case: stylized-concept. Asset type: background for a playable retro 16-bit science-fiction JRPG turn-based battle. Wide 16:9 image. An abandoned little space outpost on a purple alien moon at dusk, distant ringed planet and sparse stars, old metal landing deck in foreground, luminous cyan plants along the edges and a few warm amber lamps. Pixel art with crisp square pixel clusters and limited palette, reminiscent of modern SNES-inspired RPG environments. Simple prototype visual, atmospheric readable shapes. Reserve the broad middle foreground for two game characters added later: an unobstructed horizontal metal floor spanning the width, with no foreground objects obscuring it. Horizon in upper third. Indigo shadows, soft cyan and amber accents. NO characters, NO enemies, NO text, NO interface, NO border, NO logos.

Format consulté pour l'importeur : [documentation JSON officielle LDtk 1.5.3](https://ldtk.io/json/).

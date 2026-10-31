# Créer avec la boîte à outils

Dans Unity : **Tools → RPG → Boîte à outils**.
Pour découvrir un exemple : **Tools → RPG → Ouvrir l'atelier jouable**, puis Play.

Le catalogue `Assets/_Game/Data/WorldToolbox.asset` contient les types réutilisables. Le projet `Assets/_Game/Maps/Atelier.ldtk` contient leurs placements et plusieurs niveaux. La scène `Atelier` commence dans `Laboratoire`. Une borne rose permet de rejoindre Nacre avec E, puis de revenir. Un DroneCuivre illustre un monstre ajouté au catalogue ; une bande végétale illustre un calque de tuiles PNG.

Nacre.ldtk reste ton original. Atelier en est une copie de travail : les changements de placement dans l'un ne se répercutent pas dans l'autre. Le catalogue de comportements est partagé.

## Ajouter un monstre

1. Choisis **Drone** dans la liste **Élément**, puis **Dupliquer**. La duplication crée aussi une fiche de combat indépendante.
2. Donne un identifiant unique, sans espaces ni accents, par exemple `ScarabeeMecanique`. Garde ensuite cet identifiant stable.
3. Choisis son Sprite et sa Teinte. Tu peux importer un PNG avec **Importer un PNG depuis le disque** : il sera copié dans `Assets/_Game/Art/Toolbox`, sans modifier l'original.
4. Dans **Images d'animation**, ajoute les sprites de chaque image si tu en disposes. Pour une planche d'animation, découpe-la d'abord dans le Sprite Editor de Unity (Sprite Mode Multiple). Un PNG unique peut rester statique ; **Flottement animé** donne une petite animation sans planche.
5. Règle ses PV, attaque, défense et vitesse dans **Caractéristiques de combat**. Le rayon de patrouille est en pixels : 16 = une case. Rayon 0 = immobile.
6. **Enregistre et ferme le projet dans LDtk**, puis clique **Synchroniser la bibliothèque vers LDtk** dans Unity. Rouvre le fichier via **Ouvrir dans LDtk**.
7. Dans LDtk, sélectionne le calque **Entities** puis ton monstre dans la palette et place-le sur du sol praticable.
8. Enregistre dans LDtk. Retourne dans Unity et relance Play.

Le sprite, la teinte et les images d'animation de l'entrée servent aussi au combat après synchronisation. Les réglages de patrouille déjà modifiés sur un exemplaire dans LDtk sont conservés ; les valeurs par défaut s'appliquent aux nouveaux exemplaires.

**Ajouter** crée une entrée vide. Pour un ennemi, choisis le comportement **Monstre**, un sprite, puis **Créer sa fiche de combat**.

## Décors et interactions

Comportements disponibles :

| Comportement | Effet |
| --- | --- |
| Monstre (Enemy) | Contact = combat ; patrouille horizontale facultative |
| Décor (Decoration) | Visuel, animation, lumière et collision optionnelles |
| Soin (Heal) | E soigne toute l'équipe |
| Balise (Beacon) | E termine l'objectif si les monstres de cette carte sont vaincus |
| Terminal (Sign) | E affiche le champ Text ; texte par défaut défini dans le catalogue |
| Porte (Portal) | E charge le niveau nommé dans Destination |

**Obstacle solide** ajoute une collision aux pieds du décor. Sa taille est en unités Unity (1 unité = 16 pixels). L'intensité lumineuse 0 désactive la lumière configurable. Les bornes Heal/Beacon ont aussi leur lumière d'interaction.

Pour retirer un type, retire d'abord ses exemplaires dans les fichiers LDtk et enregistre, puis utilise **Retirer** dans le catalogue et synchronise. Le retrait est refusé si un exemplaire existe encore dans un fichier LDtk sous Assets. Les PNG et fiches de combat ne sont pas supprimés. Les placements retirés par erreur dans LDtk peuvent être annulés dans LDtk.

## Ajouter une planche de décors PNG

1. Importe un PNG ou glisse une texture déjà dans Assets dans **Planche PNG**.
2. Indique la taille régulière des cases, par exemple 16 pixels. La largeur et la hauteur du PNG doivent être des multiples de cette taille.
3. Enregistre et ferme le projet LDtk.
4. Clique **Ajouter ce tileset et son calque dans LDtk**.
5. Rouvre LDtk : un calque `Decor_<numéro>` apparaît sur tous les niveaux. Choisis-le et peins avec les tuiles du PNG.
6. Enregistre et teste dans Unity.

Les calques visuels sont indépendants des collisions : **Terrain** garde les valeurs 0 = vide bloquant, 1 = mousse praticable, 2 = métal praticable, 3 = mur. Dessine aussi Terrain sous les nouveaux décors. Les tuiles décoratives sont dessinées au-dessus du sol, derrière les personnages. Pour un accessoire qui doit passer devant/derrière un personnage selon sa position, utilise une entité Decoration.

Les calques AutoLayer sont lus s'ils sont générés et enregistrés par LDtk ; les règles automatiques restent à configurer dans LDtk. Les tilesets doivent rester sous Assets pour être embarqués dans le jeu.

## Créer et relier des cartes

1. Dans **Cartes du projet**, donne un nom à la nouvelle carte, puis clique **Créer une carte vide avec un départ**.
2. La carte reprend les dimensions et les calques du premier niveau ; elle contient un sol métallique, une bordure solide et un PlayerSpawn. Elle n'exige ni ennemi ni balise.
3. Rouvre LDtk et dessine le niveau. Conserve exactement un PlayerSpawn sur du sol praticable.
4. Place une entité **Portal**. Son champ **Destination** doit contenir le nom exact d'un autre niveau, par exemple `Laboratoire` ou `Nacre_Outpost`.
5. Place une autre Portal sur l'autre carte si tu souhaites revenir. Les passages ne créent pas automatiquement leur retour.
6. Enregistre. Dans Unity, **Choisir pour Play** choisit le niveau de départ sur le composant SandboxWorld de la scène ouverte. Enregistre la scène avec Cmd+S, puis lance Play.

L'arrivée se fait au PlayerSpawn de la destination. Le retour de combat conserve le bon niveau et la position. Les ennemis vaincus et les PV restent mémorisés entre cartes pendant la session. Ils ne sont pas sauvegardés sur disque.

## Synchronisation et limites

Les réglages du catalogue et des fiches de combat sont enregistrés automatiquement après environ une seconde sans modification, ainsi qu’à la fermeture de la fenêtre. La liste des cartes reste en mémoire et se met à jour quand Unity réimporte un changement LDtk. Le bouton **Vérifier le projet** lance la validation complète à la demande.

- Les caractéristiques communes sont définies dans Unity ; les positions et réglages locaux sont définis dans LDtk.
- Enregistre et ferme le projet LDtk avant une synchronisation : un projet laissé ouvert pourrait réécrire son ancienne version. Les modifications LDtk non enregistrées ne sont pas accessibles à Unity.
- La synchronisation met à jour les définitions du catalogue, sans repeindre Terrain ni déplacer les objets. Chaque écriture produit une sauvegarde dans `ldtk-maps/backups/toolbox`.
- Si le fichier a changé sur disque depuis l'ouverture de la fenêtre, la première action le relit et demande de relancer l'action après vérification.
- Un projet LDtk peut avoir plusieurs niveaux dans un seul monde. Les niveaux externes et Multi-worlds ne sont pas pris en charge. Terrain reste une IntGrid de 16 pixels sans décalage.
- Les comportements proposés sont fixes. Un nouveau type de monstre utilisant Enemy ne demande pas de code ; un comportement nouveau (vol, poursuite, boss à phases, coffre/inventaire) demande une extension.
- Les animations sont des boucles de sprites. Les attaques utilisent les déplacements et effets du prototype ; il n'y a pas encore de choix distinct de planches marche/attaque/mort par monstre.
- Le catalogue standard est chargé automatiquement. Pour un catalogue alternatif, assigne-le aussi dans l'Inspector de l'importeur LDtk ; la synchronisation le fait pour le projet sélectionné.

## Vérifications

- **Tools → RPG → Tester la boîte à outils** : conservation des données, ajout/retrait, champs texte, niveaux, tilesets et destinations.
- **Tools → RPG → Tester les cartes et passages en jeu** : ouvre l'atelier et teste les tuiles, le monstre personnalisé, les portes aller-retour et le retour de combat. Laisse le test finir sans intervenir.
- Les tests du bac à sable et du moteur restent dans **Tools → RPG → Bac à sable**.

Rapports : `Logs/toolbox-checks.txt`, `Logs/toolbox-playmode.txt`, `Logs/sandbox-*`.

Validation du 31/10/2026 : 16 contrôles de données/synchronisation, 14 contrôles en Play de l'atelier, 14 contrôles de la carte d'origine, 24 contrôles de sa boucle jouable et 15 tests NUnit du moteur réussis. Pas de build Windows testé. L'ouverture d'Atelier dans l'interface LDtk n'a pas pu être confirmée par l'automatisation : le sélecteur macOS laisse Ouvrir désactivé, même pour une copie JSON. Son import et son fonctionnement dans Unity sont validés. Utilise Load dans LDtk et le bouton **Copier le chemin du projet LDtk** dans la boîte à outils si l'ouverture directe affiche seulement l'accueil.

Correction de performance : suppression de la lecture complète de la carte à chaque affichage, lecture JSON sans copies répétées et sauvegardes regroupées. Mesure sur Atelier : lecture complète 88,5 ms avant, 3,3 ms après ; les affichages réutilisent la liste des niveaux. Menu **Tools → RPG → Mesurer les performances de la boîte à outils**, rapport `Logs/toolbox-performance.txt`.

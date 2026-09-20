# Manta — Flying Playground

Prototype Unity **6000.1.1f1**, URP, Input System **1.14.0**. Tous les objets sont des placeholders composés de primitives. Aucun modèle ou animation externe n’est requis.

## Jouer

Ouvrir `Scenes/MantaFlight.unity`, puis **Play** et cliquer dans la vue Game. Le vol démarre à 28 m/s devant l’arche du lagon. **R** ramène au départ ; **Échap** ouvre l’atelier et suspend le vol. **F1** masque le HUD.

La scène livrée active les quatre phases. Le champ **Phase** du profil et le bouton du menu permettent de revenir au vol de base pour comparer le ressenti.

| Action | Clavier / souris, positions QWERTY | Manette Xbox / équivalent |
|---|---|---|
| Tourner / monter / descendre | WASD ou flèches | Stick gauche |
| Pilotage souris | Maintenir clic droit et déplacer la souris | — |
| Montée / descente supplémentaire | Espace / Ctrl gauche | Stick droit vertical |
| Accélérer | Shift gauche | RT, analogique |
| Freiner | C | LT, analogique |
| Roll gauche / droite | Q / E | LB / RB |
| Demi-tour | X | B |
| Virage serré | Maintenir Alt gauche + direction | Maintenir A + stick |
| Plongeon | Maintenir V | Appuyer et maintenir le stick droit |
| Retour au départ | R | Select / View |
| Atelier / reprendre | Échap | Start / Menu |

Les touches du nouveau Input System sont des **positions physiques** : WASD correspond à ZQSD sur AZERTY, et le roll gauche Q correspond à A. Le menu affiche les noms fournis par le périphérique. Les commandes peuvent être réaffectées dans **Atelier → Clavier / souris ou Manette** ; choisir une liaison puis presser une touche ou actionner un contrôle compatible. Échap annule. Les liaisons sont conservées entre les sessions via PlayerPrefs. « Restaurer les commandes » rétablit les valeurs d’origine. La navigation dans le menu utilise aussi la manette.

Le pitch n’est pas inversé par défaut : pousser le stick vers le haut fait monter. Le bouton « Inverser le pitch » change ce comportement. La souris agit comme un stick relatif qui revient doucement au neutre.

## Itérer sur le ressenti

- `Settings/FlightSettings.asset` contient les vitesses, accélérations, inertie, vitesses angulaires, banking, énergie des plongeons, figures, caméra et animations.
- `MantaInput` expose zone morte, courbe de sensibilité, sensibilité souris et inversion.
- L’atelier donne accès aux réglages les plus fréquents pendant le jeu. Ils modifient une **copie de session** du profil.
- Pour éditer le profil réellement utilisé en Play Mode, utiliser **Manta → Tuning → Select active settings**. Il s’agit d’une copie de session : modifier le profil source n’altère pas le vol déjà lancé.
- **Manta → Tuning → Save runtime as new profile** crée un nouvel asset indépendant. Après avoir quitté Play Mode, l’assigner au champ `Settings` du `MantaController` pour le réutiliser. L’ancien menu **Save current tuning as new preset** reste disponible.
- **Manta → Tuning → Write runtime to source profile** écrit directement les valeurs de session dans le profil source et les sauvegarde sur disque. Cette action propose Undo dans l’éditeur.
- Ces exports couvrent les paramètres de vol, caméra, animations, l’esquive et les réglages actuels du composant `MantaInput`. Les sensibilités et l’inversion se règlent sur ce composant pendant le jeu ; leur copie exportée est réappliquée au prochain démarrage. Le remappage reste enregistré séparément dans PlayerPrefs.
- Avec plusieurs mantas dans la scène, sélectionner celle à exporter. Aucun code de l’outil d’export n’est inclus dans le build : il réside dans `Editor/`.

Valeurs de départ : croisière 28 m/s, minimum 12, propulsion maximale 64, plafond de plongeon 90 ; FOV 58–76° avec un supplément de 4° maximum pour plongeon et proximité du sol. Distance caméra 13–18 m. L’inertie directionnelle vaut 4,5 ; une valeur supérieure fait suivre plus vite la direction visée.

Phases cumulatives : **BasicFlight** (vitesse, direction, banking, caméra), **SpeedAndCamera** (énergie, FOV, traînées), **AdvancedManeuvers** (figures, virages serrés), **Polish** (ailes, rider, banking caméra). Commencer par BasicFlight pour ajuster la base avant de travailler les figures.

## Terrain

La vallée couvre environ 2,4 km de côté. Depuis le départ : lagon et arche devant, canyon au-delà ; slalom de piliers à gauche ; tunnel bas à droite ; jardin vertical et plateformes flottantes à droite au fond. L’espace autour de ces zones reste ouvert pour les grandes courbes. Les repères orangés du canyon rendent la vitesse visible près du sol.

Les collisions utilisent une sphère de gameplay de 1,4 m autour du corps, avec balayage et glissement. Les extrémités des ailes sont décoratives : elles peuvent effleurer le décor. Un impact frontal suffisamment rapide déclenche le demi-tour décrit ci-dessous. Les autres collisions gardent le glissement et la réduction de vitesse précédents. Le retour automatique au départ intervient sous −80 m ou au-delà de 4 km du centre ; R permet toujours un retour immédiat.

## Esquive anticipée

`Swerve` remplace le demi-tour automatique. Un spherecast anticipe les obstacles sur `Look Ahead Distance + vitesse × Look Ahead Seconds` (5 m + 1,2 s initialement). Il intervient à partir de 18 m/s et jusqu’à 50° de la normale opposée (0° = choc de face). Les layers doivent aussi appartenir au masque de collision ; un tag optionnel peut filtrer le collider. La phase AdvancedManeuvers ou Polish doit être active. Sous les seuils, le glissement existant est conservé.

L’esquive compare quatre directions tangentes avec des sondes de dégagement, puis infléchit progressivement le cap. Elle dure initialement 0,85 s, conserve au moins 88 % de la vitesse et revient à 100 %, dans les limites globales du vol. Les inputs sont partiellement actifs après 0,08 s ; influence nulle pour les verrouiller. Le frein peut annuler lorsque la manta est déjà orientée vers l’extérieur. `Camera Offset`, `Camera Yaw`, `Camera Roll`, leur enveloppe et les durées de blend règlent le mouvement de caméra, modulé par proximité et vitesse.

Les surfaces successives ajustent l’échappée sans redémarrer l’animation. Le délai de récupération filtre une même surface, mais autorise une nouvelle esquive pour un autre obstacle. Le balayage de collision reste prioritaire si l’obstacle est déjà très proche. Les anciens profils conservent leurs réglages d’impact lors de la migration vers `Swerve`.

## Architecture

| Élément | Responsabilité |
|---|---|
| `MantaInput` | Action Map Flight, normalisation, touches / sticks / souris, remappage |
| `MantaController` | Simulation à pas fixe, énergie, direction, momentum, collisions, reset |
| `MantaManeuvers` | Séquences de roll, esquive et demi-tour ; aucun contrôle concurrent |
| `MantaCameraController` | Retard, anticipation, FOV, horizon et collision après interpolation |
| `MantaVisuals` | Banking du modèle, ailes articulées, queue, rider et traînées |
| `MantaDebug` | HUD, atelier et menu navigable à la manette |
| `MantaFlightSettings` | Profil partagé, copié au démarrage pour le tuning |
| `MantaPrototypeBuilder` | Génération de scène, matériaux, Input Actions et prefab via l’éditeur |

Le prefab `Prefabs/MantaRider.prefab` sépare le mouvement, le modèle et le socket `Rider Attachment • future mount socket`. Le rider pourra être remplacé ou détaché ultérieurement sans refaire le contrôleur. Le Rigidbody est cinématique et interpolé ; les ailes n’influencent pas les collisions.

Les rolls sont visuels et laissent le cap pilotable. Le demi-tour manuel conserve 85 % de la vitesse et choisit son côté selon la direction pressée. Il reste distinct du demi-tour d’impact. La caméra ignore les rolls complets, amortit les changements de cap et limite l’angle de suivi vertical.



## Vérifications

En Play mode : **Manta → Validate current flight phase (Play mode)**. Les essais font avancer la simulation réelle puis replacent la manta. Les rapports sont écrits dans `Logs/MantaFlight/` (dossier ignoré par Git). `MantaFlightValidation.ValidateCameraAndRebind()` vérifie séparément caméra et capture interactive.

**Manta → Validate swerves and tuning export (Play mode)** vérifie les contacts frontaux/rasants, les coins, les surfaces successives et les pentes. Il vérifie aussi la copie indépendante des courbes, la sauvegarde sur disque et l’écrasement d’un profil temporaire, puis supprime uniquement cet asset de test.

Vérifications couvertes : accélération/freinage, limites de pitch, banking, comparaison 50/100 Hz, collision contre une paroi de 15 cm, énergie plongeon/remontée, les trois figures, virage serré, sticks/gâchettes virtuels, zones mortes, persistance des liaisons, reset, FOV, horizon pendant les figures, collision caméra et annulation du remappage.

Les essais automatisés et les captures vérifient la stabilité et la lisibilité ; ils ne remplacent pas un essai humain du game feel avec une souris et une manette physique. Aucun build distribué n’a été produit. Le package officiel `com.unity.pipeline` permet les vérifications dans l’éditeur ; le vol lui-même ne dépend pas de ses commandes.


## Rider au sol et wingsuit

Le personnage, les commandes de descente/saut, le wingsuit et la récupération par la manta sont décrits dans [Rider/README.md](Rider/README.md). Les réglages du rider sont séparés du profil de vol de la manta.

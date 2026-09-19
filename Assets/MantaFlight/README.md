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
| Looping avant / arrière | F / G | X / Y |
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
- Pour garder une session de tuning, utiliser **Manta → Save current tuning as new preset**, puis assigner ce nouvel asset au `MantaController` hors Play mode. Les réglages d’entrée sont propres au composant `MantaInput` ; seul le remappage est automatiquement persistant.

Valeurs de départ : croisière 28 m/s, minimum 12, propulsion maximale 64, plafond de plongeon 90 ; FOV 58–76° avec un supplément de 4° maximum pour plongeon et proximité du sol. Distance caméra 13–18 m. L’inertie directionnelle vaut 4,5 ; une valeur supérieure fait suivre plus vite la direction visée.

Phases cumulatives : **BasicFlight** (vitesse, direction, banking, caméra), **SpeedAndCamera** (énergie, FOV, traînées), **AdvancedManeuvers** (figures, virages serrés), **Polish** (ailes, rider, banking caméra). Commencer par BasicFlight pour ajuster la base avant de travailler les figures.

## Terrain

La vallée couvre environ 2,4 km de côté. Depuis le départ : lagon et arche devant, canyon au-delà ; slalom de piliers à gauche ; tunnel bas à droite ; jardin vertical et plateformes flottantes à droite au fond. L’espace autour de ces zones reste ouvert pour les grandes courbes et les loopings. Les repères orangés du canyon rendent la vitesse visible près du sol.

Les collisions utilisent une sphère de gameplay de 1,4 m autour du corps, avec balayage et glissement. Les extrémités des ailes sont décoratives : elles peuvent effleurer le décor. Une collision interrompt une figure et réduit la vitesse. Le retour automatique au départ intervient sous −80 m ou au-delà de 4 km du centre ; R permet toujours un retour immédiat.

## Architecture

| Élément | Responsabilité |
|---|---|
| `MantaInput` | Action Map Flight, normalisation, touches / sticks / souris, remappage |
| `MantaController` | Simulation à pas fixe, énergie, direction, momentum, collisions, reset |
| `MantaManeuvers` | Séquences de roll, looping et demi-tour ; aucun contrôle concurrent |
| `MantaCameraController` | Retard, anticipation, FOV, horizon et collision après interpolation |
| `MantaVisuals` | Banking du modèle, ailes articulées, queue, rider et traînées |
| `MantaDebug` | HUD, atelier et menu navigable à la manette |
| `MantaFlightSettings` | Profil partagé, copié au démarrage pour le tuning |
| `MantaPrototypeBuilder` | Génération de scène, matériaux, Input Actions et prefab via l’éditeur |

Le prefab `Prefabs/MantaRider.prefab` sépare le mouvement, le modèle et le socket `Rider Attachment • future mount socket`. Le rider pourra être remplacé ou détaché ultérieurement sans refaire le contrôleur. Le Rigidbody est cinématique et interpolé ; les ailes n’influencent pas les collisions.

Les loopings décrivent une trajectoire avec vitesse conservée. Les rolls sont visuels et laissent le cap pilotable. Le demi-tour conserve 85 % de la vitesse et choisit son côté selon la direction pressée. La caméra ignore les rolls complets, amortit les changements de cap et limite l’angle de suivi vertical.

Pendant un looping, la caméra recule vers un cadrage fixe qui englobe la trajectoire entière. La manta effectue sa figure dans ce cadre : la caméra ne poursuit ni son pitch, ni son cap, ni sa position instantanée. Le FOV reste stable. Le suivi normal revient progressivement à la sortie, y compris si la figure est interrompue. Les paramètres `Loop Camera Padding`, `Loop Camera Pullback Time` et `Loop Camera Return Time` règlent la marge, le recul et la reprise du suivi. Les collisions avec le décor restent prioritaires sur le maintien de la distance.

## Vérifications

En Play mode : **Manta → Validate current flight phase (Play mode)**. Les essais font avancer la simulation réelle puis replacent la manta. Les rapports sont écrits dans `Logs/MantaFlight/` (dossier ignoré par Git). `MantaFlightValidation.ValidateCameraAndRebind()` vérifie séparément caméra et capture interactive.

Vérifications couvertes : accélération/freinage, limites de pitch, banking, comparaison 50/100 Hz, collision contre une paroi de 15 cm, énergie plongeon/remontée, les cinq figures, virage serré, sticks/gâchettes virtuels, zones mortes, persistance des liaisons, reset, FOV, horizon pendant les figures, collision caméra et annulation du remappage.

Les essais automatisés et les captures vérifient la stabilité et la lisibilité ; ils ne remplacent pas un essai humain du game feel avec une souris et une manette physique. Aucun build distribué n’a été produit. Le package officiel `com.unity.pipeline` permet les vérifications dans l’éditeur ; le vol lui-même ne dépend pas de ses commandes.

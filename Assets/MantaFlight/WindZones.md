# Zones de vent

Projet : Unity 6000.1.1f1, **URP 17.1**. Les scripts de vent et de particules utilisent les API Unity standard. Le matériau `Wind streak` utilise le shader transparent **Manta/Wind streak**, spécifique à URP. Pour Built-in/HDRP, remplacer ce matériau par un shader de particules transparent acceptant la couleur des sommets.

## Essayer dans la scène

La configuration `Manta > Environment > Set up wind zones` ajoute une racine **Wind zones** à la scène MantaFlight et enregistre la scène. Elle préserve la configuration si un gestionnaire existe déjà. Trois volumes sont proposés près du point de départ : vent dominant, courant ascendant et vent transversal. La zone ascendante se trouve environ 500 m devant le départ, couvre 900 m de hauteur et porte un vent de 22 m/s ; elle se mélange au vent dominant avec un poids de 3.

Sélectionner une zone affiche le volume et une flèche proportionnelle à sa force dans la vue Scene. Modifier `direction` (coordonnées monde), `strength` (m/s), `falloff` (épaisseur intérieure en mètres), `weight` et les rafales dans l'Inspector. La rotation du volume modifie sa géométrie, pas la direction monde du vent.

Pour tester sans météo ni rafales, activer `debugOverride` puis modifier `debugWind` : `(0, 20, 0)` pour une ascendance, `(20, 0, 0)` pour un vent latéral. Un changement de direction/force est pris en compte immédiatement ; déplacer/redimensionner un volume est réindexé toutes les 0,25 s par défaut. Appeler `WindManager.Invalidate()` pour demander une réindexation dès la prochaine requête après un déplacement programmatique (synchroniser les transforms PhysX si nécessaire).

## API et mélange

```csharp
Vector3 airVelocity = WindManager.GetWindAt(worldPosition);
```

Le résultat est la vitesse de l'air dans le monde en m/s, zéro en l'absence de gestionnaire. Un seul gestionnaire actif est autorisé. Les zones actives s'enregistrent et se retirent automatiquement. Box et Sphere offrent une distance intérieure analytique ; Capsule et MeshCollider **convexe fermé** utilisent une approximation par six rayons entrants. Pour une forme concave, utiliser plusieurs zones convexes. Les colliders doivent être des triggers et rester actifs ; aucun Rigidbody n'est nécessaire, les requêtes ne dépendent pas des événements OnTrigger.

Le falloff est un SmoothStep de la distance intérieure à la surface. Dans un chevauchement, les vecteurs sont moyennés avec `falloff × weight`. La couverture maximale mélange ce résultat au vent de fond : le poids normalisé ne supprime donc pas le fondu d'une zone isolée. Deux vents opposés peuvent s'annuler, volontairement.

Une grille spatiale de cellules de 400 m ne teste que les candidats voisins. Son index est reconstruit à fréquence configurable avec réutilisation des listes. Les zones dépassant `maxCellsPerZone` sont mises dans une liste de débordement : borner ainsi la mémoire implique de parcourir cette liste à chaque requête. Éviter des centaines de volumes gigantesques couvrant tout le monde. `LastCandidateCount` permet de mesurer les candidats d'une requête. Pas de recherche de scène ni allocation par requête.

## Interactions

- **Wingsuit** : déploiement, incidence, décrochage, portance et traînée utilisent `vitesse monde − vent`. La vitesse monde est restituée après intégration. Le plafond de vitesse s'applique à la vitesse relative, permettant de gagner de la vitesse sol avec le vent arrière. Une ascendance transporte le planeur et peut donner une vitesse verticale positive. `windInfluence = 0` retrouve le comportement sans vent.
- **Manta pilotée** : faible dérive lissée (12 % par défaut), retirée du calcul de cap inertiel pour éviter de compter deux fois le vent. **Suivi/errance** : dérive ajoutée avant le balayage de collision, contrebalancée par le contrôleur de suivi existant. Les phases précises de capture, atterrissage et hover conservent leur stabilisation.
- **À pied** : `windOnFoot` désactivé par défaut sur RiderGroundMotor ; petite accélération horizontale configurable, indépendante de la fréquence des images, intégrée avant les collisions.
- **Météo** : WindWeatherAdapter lit la pluie déjà interpolée et CurrentWind du système météo. Le vent de fond suit les prévisions ; la force des zones est multipliée progressivement jusqu'à 1,8 en forte pluie/Storm, avec une dérive lente Perlin. Désactiver `weatherInfluence` sur une zone l'isole de ce changement. Le noyau WindManager ne connaît ni météo, ni joueur, ni manta.
- **Nuages** : chaque volume lit le vent à son centre pour sa dérive horizontale et son déplacement de bruit. Les centres gardent leur couche d'altitude ; la composante verticale anime le bruit sans déplacer les couches. Le culling par caméra déjà présent est conservé.

## Particules

Chaque zone possède un enfant **Local wind particles**. `WindZoneParticles` configure automatiquement le ParticleSystem : simulation **World**, vitesse initiale nulle, pas de gravité, émission automatique désactivée, renderer **Stretch**, pas d'ombres. L'émission est pilotée par script pour rejeter les points hors de la zone et chaque particule lit le vent local, y compris dans les chevauchements. VelocityOverLifetime et ForceOverLifetime sont désactivés pour ne pas doubler la vitesse.

Valeurs initiales : rayon 32 m autour de la caméra, 400 particules maximum par zone, durée 2,5 s, émission 3–100/s, taille 0,025–0,1 m, longueur 1–5 et étirement 0,015–0,09. Force, densité, taille et opacité évoluent jusqu'à 25 m/s. Un fondu de naissance/mort adoucit les traits. Les particules sortant du rayon sont retirées. `follow` permet de centrer le volume sur le joueur plutôt que la caméra ; `viewCamera` peut être explicitement assignée.

Seules les zones contenant le centre suivi émettent. L'émission baisse entre 60 et 180 m de distance caméra/cible. Si le volume local sort du frustum, ou si la zone est désactivée, le système est arrêté et vidé. Lorsque la caméra est elle-même au centre, son volume est naturellement visible et à LOD maximal. Les budgets sont **par zone** (trois volumes : au plus 1 200 particules) : réduire `maxParticles` et `emissionRange` si beaucoup de zones se recouvrent. Plusieurs enfants sont possibles pour combiner feuilles et traits, chacun avec son budget/matériau.

## Vérifications

`Manta > Environment > Validate wind zones` exécute des vérifications de falloff, chevauchement pondéré, volumes transformés, désactivation, réindexation, grille avec 80 zones distantes, débordement, météo/debug, vitesse relative du wingsuit, ascendance, désactivation du vent et configuration des particules. Les objets temporaires sont supprimés à la fin.

L'indicateur HUD optionnel n'est pas ajouté. Les zones et leurs traits fournissent l'indication visuelle ; les sensations et la lisibilité sont à ajuster en Play selon la vitesse de la caméra.

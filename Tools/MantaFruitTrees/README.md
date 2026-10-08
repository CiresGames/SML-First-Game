# Large stat fruit trees

Five 32-metre prefab variants of the existing Rei tree, in `Assets/MantaFlight/Resources/MantaFruitTrees`.

| Stat | Leaf color |
| --- | --- |
| Speed | Cyan `#33CCFF` |
| Manoeuvrability | Violet `#B373FF` |
| Endurance | Green `#4DFF80` |
| Force | Amber `#FFA633` |
| Obedience | Pink `#FF66A6` |

The source trunk material and leaf shader are retained. Each variant has a trunk mesh collider and a fruit anchor; foliage is passable. The prototype places two trees per stat at its exploration fruit sites, and collected fruit does not remove its tree.

Rebuild through **Manta → Progression → Build colored fruit trees** in Unity. `PreviewTrees.cs` can be run through Unity CLI `eval_file` in Edit Mode; it checks variant inheritance, height, colors, collision and anchors, then renders `FruitTrees-preview.png`.

Verified in Unity: all five variants pass asset checks; all ten tree sites spawn, each fruit matches its anchor, and all ten anchors are clear of solid geometry within 0.8 m.

## Tree interaction

On foot, Xbox X / PlayStation Square / F within 2 m of solid trunk shows and triggers Punch tree. The rider approaches, winds up, strikes and recovers. Impact releases the fruit with gravity, spin and bounce; collect it near the ground and feed it through Manta Info. Empty trees have no prompt. Collected fruit remains absent after reload; uncollected drops reset to their tree.

The punch is authored on the existing humanoid rig in Unity. Its editable clip is `Assets/MantaFlight/Resources/RiderAnimations/TreePunch.anim`; **Manta → Rider → Build tree punch animation** rebuilds it using `RiderTreePunchSetup.cs`. `PreviewPunch.cs` renders wind-up, impact and recovery poses to `Punch-preview.png` through Unity CLI eval_file in Edit Mode. Fruit meshes retain their Blender sources in `ArtSource/MantaFruits`.

**Manta → Progression → Validate tree punching (Play mode)** checks the full tree-to-inventory sequence, including gamepad input, animation contact, blocked reach, impact timing, menu freezing and duplicate protection.

The tree interaction and controller mount regression passed in Unity 6000.1.1f1. `TreeInteraction-preview.png` shows the live colored prompt, and `TreePunch-impact-preview.png` captures the live punch. The actual scene's released fruit landed and was collected without removing its tree.

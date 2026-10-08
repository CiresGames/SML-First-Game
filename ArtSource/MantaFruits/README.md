# Manta stat fruits

Five editable Blender fruits, exported to Unity and used by the existing world rewards.

| Stat | UI / fruit base color (sRGB) | Shape |
| --- | --- | --- |
| Speed | Cyan `#33CCFF` | Streamlined teardrop, swept crown |
| Manoeuvrability | Violet `#B373FF` | Twisted five-point starfruit |
| Endurance | Green `#4DFF80` | Broad pear with narrow neck |
| Force | Amber `#FFA633` | Eight-rib pumpkin with stout crown |
| Obedience | Rose `#FF66A6` | Heart berry with a real cleft and tip |

Every fruit has the same full bounding dimensions, including leaves and stem: **0.9 m wide × 1 m tall × 0.9 m deep** in Unity. Blender uses Z-up, so its dimensions read `(0.9, 0.9, 1)`. Origins are centered, scale is applied, and FBX exports contain only the fruit mesh. Each fruit uses approximately 4,400–5,300 triangles and has UVs for future texturing.

Open **MantaStatFruits.blend** for the studio lineup and lighting, or the individual stat-named `.blend` files to edit a fruit by itself. **MantaStatFruits-preview.png** is the rendered lineup. **fruit-manifest.json** records exact exported dimensions and triangle counts.

The single color source is `Assets/MantaFlight/Resources/MantaFruitPalette.json`. The UI uses it for stat headings, row stripes, upgrade previews and fruit-button accents; the Endurance HUD uses its green in the healthy state, retaining amber/red fatigue warnings. Blender converts those sRGB colors to linear shader values. Unity applies the original palette colors to URP materials at runtime, with a small emission for visibility. Lighting naturally changes shaded pixel colors.

Rebuild with Blender 2.93 or compatible:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 2.93/blender.exe' -b --python Tools/MantaFruits/create_fruits.py
```

Rebuilding overwrites this folder's generated `.blend`/preview/manifest files and the five FBXs in `Assets/MantaFlight/Resources/MantaFruits`. Existing fruit IDs, collection limits and rewards are unchanged. Keep manual sculpting revisions separately before regenerating.

Validated in Unity 6000.1.1f1: all five resources load, all five colors are unique and match the shared palette, and every imported model measures `(0.9, 1, 0.9)` metres at scale one.

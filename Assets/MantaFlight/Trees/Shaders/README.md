# Rei foliage — Unity Shader Graph

Made for this project's Unity 6000.1 / URP 17.1.

## Use it

Drag `Assets/MantaFlight/Trees/Prefabs/ReiTree_ShaderGraph.prefab` into your scene.
It uses the imported Rei tree, its existing trunk material, and the new leaf material.

For another tree, assign `ReiLeaves_Green.mat` to the **leaf** material slot.
Double-click `ReiLeaves.shadergraph` to edit the graph. Its five labeled groups cover
texture cutout, foliage color, sunlight, rim lighting, and final color.

## Textures

The material already uses `Assets/Tree 1/textures/TreeLeaves01.png`.
You can replace **Leaf Texture** with TreeLeaves02 or TreeLeaves03.

The original `Rei_treeLeavesGN_samples.blend` has three separate leaf materials:
`Leaf` uses TreeLeaves01, `Leaf02` uses TreeLeaves02, and `Leaf03` uses TreeLeaves03.
Each uses one image's Color output as its transparency mask. The three images are
alternative leaf silhouettes, not three layers blended within the same material.
The base `Rei_treeLeavesGN.blend` uses TreeLeaves01 only.

Matching Unity mask presets are available:

| Material | Leaf mask |
| --- | --- |
| ReiLeaves_Green | TreeLeaves01 |
| ReiLeaves_Green_02 | TreeLeaves02 |
| ReiLeaves_Green_03 | TreeLeaves03 |

Assign one of these materials to the leaf renderer. They share the same Shader Graph
and green color settings; these presets reproduce the mask choices, not each sample's
individual color palette or tree shape.

| Input texture | Mask from Red | Use Texture Color |
| --- | --- | --- |
| Original white leaves on black background | 1 | 0 |
| Colored RGBA atlas, transparent background | 0 | 1 |
| Alpha mask with colors supplied by the material | 0 | 0 |

White/red 1 or alpha 1 keeps a leaf; black/red 0 or alpha 0 cuts it away.
**Cutout Threshold** defaults to 0.45; increase it for thinner silhouettes.
For an RGBA atlas, import the texture with **Alpha Source: Input Texture Alpha**
and **Alpha Is Transparency** enabled. The UVs must already point to the appropriate
leaf region of the atlas. This shader does not create or rearrange UVs.

## Appearance

- **Leaf Color / Leaf Variation** define the two painted colors when Use Texture Color is 0.
- **Shadow Tint / Sunlit Tint** control the cool shade and warm highlights.
- **Shadow Edge / Lit Edge** control the soft toon lighting transition; keep Shadow Edge below Lit Edge.
- **Receive Shadow Strength** controls shadows from the main directional light.
- **Light Color Influence** controls how strongly the scene's sun color/intensity affects the leaves.
- **Rim Strength / Rim Power / Rim Tint** control the soft silhouette highlight.

The graph renders both sides of the cards, writes depth, and casts alpha-clipped shadows.
Use a URP renderer with main-light shadows enabled and a shadow-casting directional light.
Most of the effect uses ordinary editable Shader Graph nodes. One Custom Function,
`ReiMainLight.hlsl`, reads the actual URP main light and shadow attenuation.
Keep that file alongside the graph.

For the rounded crown shading of the Blender setup, export the transferred crown normals
and use **Normals: Import** on the FBX. This project's source FBX already uses Import.
The shader uses the mesh normals; it cannot reconstruct Blender's normal-transfer modifier.

This is a Unity interpretation of the Rei leaf material, not Blender's EEVEE renderer.
It uses main-light shading rather than full PBR lighting. Additional local lights,
baked GI, and Blender Geometry Nodes wind/animation are not reproduced.

## Validation

The graph and material were imported and rendered on the supplied Rei FBX in Unity.
Shader compiler diagnostics were checked after rendering. The preview is saved at
`Tools/ReiLeafShader/ReiTree_Preview.png`.

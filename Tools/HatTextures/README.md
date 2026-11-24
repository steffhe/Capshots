# Hat textures

The hats (`Assets/Scripts/PlayerCap.cs`) are made of a few surfaces, each a material in
`Assets/Resources/Hats`: cotton twill, knit, felt, satin, glossy plastic, metal and hammered metal.
Each has its own shine and metalness, and all but plastic have a tiling normal map from
`Assets/Textures/Hats`. The hats stay in the player's (or team's) colour: the materials are white and
tinted per hat.

The normal maps are generated. To change one, edit its pattern or strength in `generate.py` and run
it from the repository root (Python 3, standard library only):

```bash
python3 Tools/HatTextures/generate.py
```

How often a pattern repeats on a hat is the material's tiling (the Base Map's tiling in the
material, which URP uses for the normal map too).

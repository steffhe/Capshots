# Office map generator

The Office map is the 3rd floor of an office building, traced from its floor plan. It isn't
modelled in the editor: `Assets/Scenes/Office.unity` holds an `Office` object whose `OfficeMap`
component builds the floor when the scene loads, from the boxes listed in
`Assets/Resources/Maps/Office.txt`.

That file is generated. To change the map:

1. Edit `layout.py`. Rooms, blocks, furniture, spawn points and the map camera are all in floor
   plan pixels (the plan is about 1430 x 1120 px, x to the right, y down; 1 px = 0.08 m).
2. Run the generator from the repository root (Python 3, standard library only):
   ```bash
   python3 Tools/OfficeMap/generate.py --preview
   ```
   It writes `Office.txt`, redraws `preview.png` (a top-down view, spawn points in red) and
   fails if a spawn point is off the floor or blocked. It also walks the floor on a 25 cm grid
   with the player's size and step height and notes any room (or the courtyard, or the garden
   house) nobody can walk into. Today that's 44a and 58: their doors open onto a 24 cm gap.
3. Optionally bake it in Unity: **Capshots → Bake Office Map** (`Assets/Editor/OfficeMapBaker.cs`).
   It builds the floor's meshes, colliders and signs with the same code that runs at load and saves
   them as a prefab (`Assets/Prefabs/Office Floor/`, with its meshes, materials and textures),
   placed under the `Office` object in `Office.unity`. The scene then loads the floor instead of
   building it, and Unity can bake lighting (Window → Rendering → Lighting → Generate Lighting) and
   occlusion culling (Window → Rendering → Occlusion Culling → Bake) for it.

   The glass panes, spawn points and map camera still come from `Office.txt` at load. A bake
   remembers which `Office.txt` it was made from: after a layout change without baking again, the
   game ignores the old bake and builds the floor at load (with a warning in the console), so it
   can't go out of step. **Capshots → Remove Office Map Bake** goes back to no bake.

What the generator works out by itself:

- Walls: each side of a room facing a corridor gets a glass wall with the frosted film, facing
  another room a solid partition, and on the outside of the building nothing (the outside wall
  with its windows is there).
- Doors: on the longest corridor-facing side, in the middle (two doors on sides over 13 m).
- Furniture: desk clusters that fit each office and keep clear of the doors, a table and chairs
  in meeting rooms, and so on. Corridor furniture is listed in `layout.py`.

## What it looks like

Taken from photos of the real offices:

- Glass walls with thin black frames. Every room but the print rooms has the frosted film: a
  mountain range from about 1 m to 2.6 m with a row of small orange rings below it, clear above and
  below (you see feet and heads). Each room's number or letter is in copper over its door, its name
  on a small plate beside the door.
- Oak floors in the corridors, grey check carpet in the offices, dark carpet in meeting rooms.
- Lounges with black ring lamps over the sofas, and black wingback chairs on a round rug by
  meeting room K.
- White desks with black legs and grey felt screens.
- The courtyard garden, on the same level as the floor and part of the fight: walled in glass that
  breaks, with doors on all four sides. Gravel, dark paving paths, planted beds with corten edges,
  clipped yews, shrubs and bare young trees, benches, bollard lights, a vent stack and, in the
  middle, the weathered wooden garden house with a glass roof, open at both ends so you can run
  through it. Round it the terracotta facade rises three more floors, with dark window frames,
  louvres and lit rooms.
- Outside: the terracotta facade on the floors below and above, the street with hedges and bare
  trees two floors down.

## Glass

Glass is on the `Glass` layer: players can't walk through it, but shots go through, and it
doesn't hide a spawn point from enemies. The glass walls inside the floor are written as separate
panes (`p` lines, one between each pair of mullions and one above each door). A pane cracks the
first time it's shot and shatters the second time (`OfficeGlass`, with the state kept on the
Manager in `Manager.Glass.cs`). The courtyard's glass walls and the garden house's glass side
break too. The outside windows don't, so nobody can fall out of the building. The frosted film is drawn by the `mountains` texture
(`OfficeMap.Mountains`) with world-position UVs, so its pattern runs on from pane to pane. A map
can have at most 1024 panes (`OfficeMapData.MaxPanes`).

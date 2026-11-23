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
   fails if a spawn point is off the floor or blocked.

What the generator works out by itself:

- Walls: each side of a room facing a corridor gets a glass wall with a frosted band, facing
  another room a solid partition, and on the outside of the building nothing (the outside wall
  with its windows is there).
- Doors: on the longest corridor-facing side, in the middle (two doors on sides over 13 m).
- Furniture: desk clusters that fit each office and keep clear of the doors, a table and chairs
  in meeting rooms, and so on. Corridor furniture is listed in `layout.py`.

Glass is on the `Glass` layer: players can't walk through it, but shots go through, and it
doesn't hide a spawn point from enemies. The glass walls inside the floor are written as separate
panes (`p` lines, one between each pair of mullions and one above each door). A pane cracks the
first time it's shot and shatters the second time (`OfficeGlass`, with the state kept on the
Manager in `Manager.Glass.cs`). The outside windows and the courtyard glass don't break, so
nobody can fall out of the building. A map can have at most 1024 panes
(`OfficeMapData.MaxPanes`).

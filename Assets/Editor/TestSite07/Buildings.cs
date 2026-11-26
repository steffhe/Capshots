using UnityEngine;

// The enterable buildings of Test Site 07, made from the wall modules (Props.BuildModules) plus floors,
// roofs, partitions and furniture. Each is a prefab with its pivot at the south-west corner of its
// footprint (local x east, z north), so the layout places it by that corner.
//
// Wall patterns: one character per 2 m module along a side, from the side's start:
//   W wall, w window (open, vault through), g glazed window (shots pass, players don't), D doorway.
namespace TestSite07
{
    public static class Buildings
    {
        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        private const float S = Style.Storey;

        public static void BuildAll()
        {
            Diner();
            Workshop();
            Shed("Garage", Style.Garage, "GARAGE", false);
            Shed("CD Depot", Style.Depot, "CIVIL DEFENSE", true);
            RadioSign();
        }

        // ---------------------------------------------------------------- shell helpers

        // A run of modules along one side. axis 'x': along x at z = fixedAt; 'z': along z at x = fixedAt.
        private static void Side(Proto p, Style s, char axis, float fixedAt, float start, string pattern, float y = 0f)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                string module = pattern[i] switch
                {
                    'W' => "Wall",
                    'w' => "Window",
                    'g' => "Window Glazed",
                    'D' => "Door",
                    'F' => "Fascia",
                    _ => null,
                };
                if (module == null) continue;
                float along = start + i * 2f + 1f;
                Vector3 pos = axis == 'x' ? V(along, y, fixedAt) : V(fixedAt, y, along);
                p.Nest(Kit.Module($"{s.name} {module}"), pos, axis == 'x' ? 0f : 90f);
            }
        }

        // Outer walls (one pattern per storey and side: south, east, north, west), fascia, corners,
        // and the roof slab on top of the walls (the ceiling of the top storey)
        private static void Shell(Proto p, Style s, float w, float d, string[] south, string[] east, string[] north, string[] west)
        {
            const float half = 0.15f;
            for (int storey = 0; storey < s.storeys; storey++)
            {
                float y = storey * S;
                Side(p, s, 'x', half, 0f, south[storey], y);
                Side(p, s, 'z', w - half, 0f, east[storey], y);
                Side(p, s, 'x', d - half, 0f, north[storey], y);
                Side(p, s, 'z', half, 0f, west[storey], y);
            }
            float top = s.storeys * S;
            Side(p, s, 'x', half, 0f, new string('F', (int)(w / 2f)), top);
            Side(p, s, 'z', w - half, 0f, new string('F', (int)(d / 2f)), top);
            Side(p, s, 'x', d - half, 0f, new string('F', (int)(w / 2f)), top);
            Side(p, s, 'z', half, 0f, new string('F', (int)(d / 2f)), top);
            foreach (float x in new[] { half, w - half })
                foreach (float z in new[] { half, d - half })
                    p.Nest(Kit.Module($"{s.name} Corner"), V(x, 0, z));
            // Roof mass: from the ceiling up to just under the parapet top (out of reach, see Style)
            float roofTop = top + s.fasciaHeight - 0.3f;
            p.BoxMM("RoofFlat", V(0.3f, top, 0.3f), V(w - 0.3f, roofTop, d - 0.3f));
            p.BoxMM("OffWhite", V(0.3f, top - 0.04f, 0.3f), V(w - 0.3f, top - 0.01f, d - 0.3f), false);
        }

        // An interior wall from a to b (along x or z), 0.2 m thick, with 2 m doorways centred on
        // the given positions along it
        private static void Partition(Proto p, string mat, char axis, float fixedAt, float from, float to, float y0, float y1, params float[] doors)
        {
            const float t = 0.2f;
            float cursor = from;
            void Piece(float a, float b, float lo, float hi)
            {
                if (b - a < 0.01f || hi - lo < 0.01f) return;
                if (axis == 'x') p.BoxMM(mat, V(a, lo, fixedAt - t / 2f), V(b, hi, fixedAt + t / 2f));
                else p.BoxMM(mat, V(fixedAt - t / 2f, lo, a), V(fixedAt + t / 2f, hi, b));
            }
            foreach (float door in doors)
            {
                Piece(cursor, door - 1f, y0, y1);
                Piece(door - 1f, door + 1f, y0 + 3.2f, y1);
                cursor = door + 1f;
            }
            Piece(cursor, to, y0, y1);
        }

        // ---------------------------------------------------------------- the diner

        // 22 x 18 m, one storey (roof at 7.4 m). Dining room with booths, a serving counter and a
        // kitchen behind a partition with two doors and a serving hatch. Entrances: front double door
        // (south), rear kitchen door (north), side doors east (courtyard) and west (alley), and
        // vault-through windows on every side.
        private static void Diner()
        {
            var s = Style.Diner;
            var p = new Proto("Diner") { structure = true };
            const float W = 22f, D = 18f;
            Shell(p, s, W, D,
                new[] { "WwwwDDwwwwW" },
                new[] { "WwwDwWWwW" },
                new[] { "WWwWWWWDWwW" },
                new[] { "WwDwWWWwW" });

            // Floors: checker in the dining room, tiles in the kitchen
            p.BoxMM("Checker", V(0.3f, 0, 0.3f), V(W - 0.3f, 0.12f, 14f));
            p.BoxMM("OffWhite", V(0.3f, 0, 14f), V(W - 0.3f, 0.12f, D - 0.3f));
            // Kitchen partition (z = 14) with doors at x 2.5 and 18 and a serving hatch at x 6 - 12
            Partition(p, "Cream", 'x', 14f, 0.3f, 6f, 0.12f, S, 2.5f);
            p.BoxMM("Cream", V(6f, 0.12f, 13.9f), V(12f, 1.15f, 14.1f));
            p.BoxMM("Cream", V(6f, 2.3f, 13.9f), V(12f, S, 14.1f));
            p.BoxMM("OffWhite", V(6f, 1.15f, 13.8f), V(12f, 1.21f, 14.2f));
            Partition(p, "Cream", 'x', 14f, 12f, W - 0.3f, 0.12f, S, 18f);
            p.BoxMM("Coral", V(0.3f, 1.2f, 13.88f), V(W - 0.3f, 1.35f, 13.9f), false);

            // Counter with stools (customer side faces the dining room)
            foreach (float x in new[] { 5f, 9f, 13f }) p.Nest(Kit.Prop("Counter 4m"), V(x, 0.12f, 11.8f), 180f);
            for (float x = 3.6f; x <= 14.5f; x += 1.2f) p.Nest(Kit.Prop("Stool"), V(x, 0.12f, 10.9f));
            // Booths along the front windows, either side of the door
            foreach (float x in new[] { 1.9f, 4.3f, 6.7f, 13.3f, 15.7f, 18.1f, 20.5f }) p.Nest(Kit.Prop("Diner Booth"), V(x, 0.12f, 1.85f));
            // Tables and the jukebox
            p.Nest(Kit.Prop("Diner Table"), V(3.5f, 0.12f, 7f));
            p.Nest(Kit.Prop("Diner Table"), V(8f, 0.12f, 7.5f), 30f);
            p.Nest(Kit.Prop("Diner Table"), V(19.5f, 0.12f, 10.5f));
            p.Nest(Kit.Prop("Jukebox"), V(0.75f, 0.12f, 10.5f), 90f);

            // Kitchen: stoves and counters on the north wall (rear door at x 14 - 16 kept clear)
            p.Nest(Kit.Prop("Stove"), V(1.6f, 0.12f, 17.3f), 180f);
            p.Nest(Kit.Prop("Stove"), V(3.1f, 0.12f, 17.3f), 180f);
            p.Nest(Kit.Prop("Kitchen Counter"), V(5.2f, 0.12f, 17.33f), 180f);
            p.Nest(Kit.Prop("Kitchen Counter"), V(7.2f, 0.12f, 17.33f), 180f);
            p.Nest(Kit.Prop("Stove"), V(10.3f, 0.12f, 17.3f), 180f);
            p.Nest(Kit.Prop("Kitchen Counter"), V(12.2f, 0.12f, 17.33f), 180f);
            p.Nest(Kit.Prop("Fridge"), V(17.1f, 0.12f, 17.3f), 180f);

            // Outside: awning over the front door, lettering, rooftop units
            p.Box("Coral", V(10f, 3.45f, -0.75f), V(6.4f, 0.14f, 1.5f), false);
            for (int i = 0; i < 4; i++) p.Box("OffWhite", V(7.6f + i * 1.6f, 3.37f, -0.75f), V(0.8f, 0.04f, 1.52f), false);
            Color pink = new Color(1f, 0.5f, 0.65f), cream = new Color(0.99f, 0.95f, 0.85f);
            p.Text("ATOMIC DINER", V(W / 2f, 5.75f, -0.24f), 0f, 1.25f, cream, 16f);
            p.Text("ATOMIC DINER", V(W + 0.24f, 5.75f, D / 2f), -90f, 1.0f, cream, 14f);
            p.Text("OPEN 24 HRS", V(W / 2f, 4.95f, -0.24f), 0f, 0.4f, pink, 8f);
            p.Nest(Kit.Prop("AC Unit"), V(5f, 7.1f, 12f));
            p.Nest(Kit.Prop("AC Unit"), V(15f, 7.1f, 6f), 90f);
            p.Save(Kit.BuildingDir);
        }

        // ---------------------------------------------------------------- the radio workshop

        // 22 x 18 m, two storeys (parapet at 10.4 m). Ground floor: workshop, storage room, entrances on
        // all four sides, stair up along the south wall. Upper floor: the radio control room, glazed all
        // round (shots pass both ways), with a door to a balcony and an outside stair down the alley.
        private static void Workshop()
        {
            var s = Style.Workshop;
            var p = new Proto("Radio Workshop") { structure = true };
            const float W = 22f, D = 18f, F = S;
            Shell(p, s, W, D,
                new[] { "WWWDWWwWWWW", "WWgWWgWWgWW" },
                new[] { "WWDWWWWWW", "WWWDgWWWW" },
                new[] { "WwwWWDDWwwW", "WWgWWgWWgWW" },
                new[] { "WWWwWDwWW", "WgWWgWWgW" });

            p.BoxMM("Concrete", V(0.3f, 0, 0.3f), V(W - 0.3f, 0.12f, D - 0.3f));

            // Upper floor slab with the stairwell (x 13.5 - 21.7, z 0.3 - 2.95) left open
            p.BoxMM("Wood", V(0.3f, F - 0.3f, 0.3f), V(13.5f, F, D - 0.3f));
            p.BoxMM("Wood", V(13.5f, F - 0.3f, 2.95f), V(W - 0.3f, F, D - 0.3f));
            p.BoxMM("OffWhite", V(0.3f, F - 0.34f, 0.3f), V(13.5f, F - 0.31f, D - 0.3f), false);
            p.BoxMM("OffWhite", V(13.5f, F - 0.34f, 2.95f), V(W - 0.3f, F - 0.31f, D - 0.3f), false);
            // Stair: bottom at x 13.5, top (4.5 m) at x 21.5, along the south wall
            p.Nest(Kit.Prop("Stair"), V(13.5f, 0, 1.65f), 90f);
            // Railings round the stairwell upstairs; the top of the stair opens north at x 19.5 - 21.7
            foreach (float x in new[] { 14.5f, 16.5f, 18.5f }) p.Nest(Kit.Prop("Railing 2m"), V(x, F, 3.0f));
            p.Nest(Kit.Prop("Railing 2m"), V(13.5f, F, 1.6f), 90f);

            // Storage room (x 0.3 - 10, z 0.3 - 6): doors north (x 5) and east (z 3); the rear door
            // (south wall, x 6 - 8) opens into it
            Partition(p, "Cream", 'x', 6f, 0.3f, 10.1f, 0.12f, F - 0.3f, 5f);
            Partition(p, "Cream", 'z', 10f, 0.3f, 6.1f, 0.12f, F - 0.3f, 3f);
            p.Nest(Kit.Prop("Shelf"), V(0.65f, 0.12f, 1.6f), 90f);
            p.Nest(Kit.Prop("Shelf"), V(0.65f, 0.12f, 3.8f), 90f);
            p.Nest(Kit.Prop("Crate Stack"), V(3.4f, 0.12f, 1.0f));
            p.Nest(Kit.Prop("Crate"), V(8.8f, 0.12f, 4.9f), 12f);
            p.Nest(Kit.Prop("Barrel Group"), V(9.2f, 0.12f, 1.0f));

            // Workshop floor
            p.Nest(Kit.Prop("Workbench"), V(4f, 0.12f, 9.6f));
            p.Nest(Kit.Prop("Workbench"), V(4f, 0.12f, 13.6f), 180f);
            p.Nest(Kit.Prop("Workbench"), V(17.4f, 0.12f, 8.6f));
            p.Nest(Kit.Prop("Radio Rack"), V(7.6f, 0.12f, 17.35f), 180f);
            p.Nest(Kit.Prop("Radio Rack"), V(15.2f, 0.12f, 17.35f), 180f);
            p.Nest(Kit.Prop("Shelf"), V(0.65f, 0.12f, 15.6f), 90f);
            p.Nest(Kit.Prop("Crate Stack"), V(10.2f, 0.12f, 11.8f), 90f);
            p.Nest(Kit.Prop("Radio Console"), V(13.5f, 0.12f, 9.2f), 180f);
            p.Nest(Kit.Prop("Barrel Group"), V(19f, 0.12f, 14.6f));

            // Control room upstairs: consoles between the courtyard windows, racks, a console island
            p.Nest(Kit.Prop("Radio Console"), V(0.8f, F, 6f), 90f);
            p.Nest(Kit.Prop("Radio Console"), V(0.8f, F, 12f), 90f);
            p.Nest(Kit.Prop("Radio Rack"), V(7f, F, 17.35f), 180f);
            p.Nest(Kit.Prop("Radio Rack"), V(14f, F, 17.35f), 180f);
            p.Nest(Kit.Prop("Radio Console"), V(10.5f, F, 9.0f));
            p.Nest(Kit.Prop("Radio Console"), V(10.5f, F, 10.4f), 180f);
            p.Nest(Kit.Prop("Shelf"), V(21.35f, F, 13.5f), -90f);
            p.Nest(Kit.Prop("Crate"), V(5f, F, 2.0f), 20f);

            // Balcony on the east side (x 22 - 25.5, z 4 - 10.5) and the outside stair down to the
            // north (top at z 10.5, bottom at z 18.5)
            p.BoxMM("Concrete", V(W, F - 0.3f, 4f), V(25.5f, F, 10.5f));
            p.Nest(Kit.Prop("Railing 2m"), V(23f, F, 4.05f));
            p.Nest(Kit.Prop("Railing 2m"), V(24.75f, F, 4.05f), 0f, new Vector3(0.75f, 1f, 1f));
            foreach (float z in new[] { 5.05f, 7.05f, 9.05f }) p.Nest(Kit.Prop("Railing 2m"), V(25.45f, F, z), 90f);
            p.Nest(Kit.Prop("Railing 2m"), V(25.45f, F, 10.3f), 90f, new Vector3(0.4f, 1f, 1f));
            p.Box("Metal", V(25.3f, (F - 0.3f) / 2f, 4.2f), V(0.25f, F - 0.3f, 0.25f));
            p.Box("Metal", V(25.3f, (F - 0.3f) / 2f, 10.3f), V(0.25f, F - 0.3f, 0.25f));
            p.Nest(Kit.Prop("Stair"), V(23.6f, 0, 18.5f), 180f);

            // Outside: awning, lettering, the antenna on the roof
            p.Box("Turquoise", V(12f, 3.4f, D + 0.7f), V(5.2f, 0.14f, 1.4f), false);
            p.Box("Coral", V(12f, 3.95f, D + 0.2f), V(3.2f, 0.8f, 0.12f), false);
            Color ink = new Color(0.13f, 0.38f, 0.40f), cream = new Color(0.99f, 0.95f, 0.85f);
            p.Text("RADIO REPAIR", V(W / 2f, 9.7f, D + 0.25f), 180f, 0.85f, ink, 14f);
            p.Text("RADIO REPAIR", V(-0.25f, 9.7f, D / 2f), 90f, 0.85f, ink, 12f);
            p.Text("RADIO + TV", V(12f, 3.95f, D + 0.27f), 180f, 0.42f, cream, 3f);
            p.Nest(Kit.Prop("Antenna Mast"), V(17f, F * 2f + 1.1f, 12.5f));
            p.Nest(Kit.Prop("AC Unit"), V(6f, F * 2f + 1.1f, 6f));
            p.Save(Kit.BuildingDir);
        }

        // ---------------------------------------------------------------- the garage and the depot

        // 16 x 14 m, one storey (roof at 7.4 m), a through route: a wide door north (x 4 - 12), a door
        // south (x 4 - 8) and one west (z 6 - 8). The CD Depot is the same shell, placed turned round.
        private static void Shed(string name, Style s, string sign, bool depot)
        {
            var p = new Proto(name) { structure = true };
            const float W = 16f, D = 14f;
            Shell(p, s, W, D,
                new[] { "WWDDWWwW" },
                new[] { "WWwWwWW" },
                new[] { "WWDDDDWW" },
                new[] { "WwWDWwW" });
            p.BoxMM("Concrete", V(0.3f, 0, 0.3f), V(W - 0.3f, 0.12f, D - 0.3f));

            if (!depot)
            {
                p.Nest(Kit.Prop("Car Coral"), V(12f, 0.12f, 7f));
                p.Box("Dark", V(12f, 0.125f, 7f), V(2.6f, 0.01f, 3.5f), false);
                p.Nest(Kit.Prop("Workbench"), V(15.2f, 0.12f, 2.6f), -90f);
                p.Nest(Kit.Prop("Tire Stack"), V(14.9f, 0.12f, 11.6f));
                p.Nest(Kit.Prop("Tire Stack"), V(14.9f, 0.12f, 12.6f));
                p.Nest(Kit.Prop("Shelf"), V(2.8f, 0.12f, 13.35f), 180f);
                p.Nest(Kit.Prop("Barrel Group"), V(1.3f, 0.12f, 1.3f));
                p.Nest(Kit.Prop("Crate"), V(9.5f, 0.12f, 1.0f), 8f);
            }
            else
            {
                p.Nest(Kit.Prop("Shelf"), V(12.5f, 0.12f, 0.65f));
                p.Nest(Kit.Prop("Shelf"), V(14.5f, 0.12f, 0.65f));
                p.Nest(Kit.Prop("Shelf"), V(15.35f, 0.12f, 3.0f), -90f);
                p.Nest(Kit.Prop("Sandbags 3m"), V(11f, 0.12f, 8f), 90f);
                p.Nest(Kit.Prop("Crate Stack"), V(14.2f, 0.12f, 11.8f));
                p.Nest(Kit.Prop("Barrel Group"), V(1.3f, 0.12f, 1.3f));
                p.Nest(Kit.Prop("Generator"), V(2.8f, 0.12f, 12.9f));
                p.Nest(Kit.Prop("Crate"), V(9.5f, 0.12f, 1.0f), 8f);
            }

            Color ink = depot ? new Color(0.16f, 0.17f, 0.19f) : new Color(0.99f, 0.95f, 0.85f);
            float y = S + s.fasciaHeight * 0.62f;
            p.Text(sign, V(W / 2f, y, D + 0.24f), 180f, 1.0f, ink, 14f);
            p.Text(sign, V(W / 2f, y, -0.24f), 0f, 1.0f, ink, 14f);
            if (depot)
            {
                // Civil defence badge over the wide door
                p.CylZ("Yellow", V(W / 2f, 3.85f, D + 0.05f), 0.55f, 0.1f, 16);
                p.Shape(Shapes.Prism, "Dark", V(W / 2f, 3.6f, D + 0.12f), V(0.7f, 0.6f, 0.04f));
                p.Text("FALLOUT SHELTER", V(W / 2f, 2.55f, -0.24f), 0f, 0.35f, ink, 6f);
            }
            p.Nest(Kit.Prop("AC Unit"), V(4f, S + s.fasciaHeight - 0.3f, 7f));
            p.Save(Kit.BuildingDir);
        }

        // The workshop's roadside sign (the diner sign's counterpart)
        private static void RadioSign()
        {
            var p = new Proto("Radio Sign") { structure = true };
            p.Box("Orange", V(0, 1.4f, 0), V(3f, 2.8f, 1.2f));
            p.Box("Turquoise", V(0, 2.75f, 0), V(3.06f, 0.12f, 1.26f), false);
            p.Cyl("Metal", V(0, 2.8f, 0), 0.22f, 3.8f, 8);
            p.Col(V(0, 5f, 0), V(0.5f, 4.4f, 0.5f));
            p.Box("Cream", V(0, 7.6f, 0), V(5f, 2.2f, 0.5f));
            p.Box("NeonTeal", V(0, 8.73f, 0), V(5.1f, 0.08f, 0.56f), false);
            p.Box("NeonTeal", V(0, 6.47f, 0), V(5.1f, 0.08f, 0.56f), false);
            // A lightning bolt over the board
            p.BoxRot("Yellow", V(-1.6f, 9.35f, 0), V(0.9f, 0.18f, 0.12f), Quaternion.Euler(0, 0, 60f), false);
            p.BoxRot("Yellow", V(-1.3f, 9.6f, 0), V(0.7f, 0.18f, 0.12f), Quaternion.Euler(0, 0, -10f), false);
            p.BoxRot("Yellow", V(-1.0f, 9.85f, 0), V(0.9f, 0.18f, 0.12f), Quaternion.Euler(0, 0, 60f), false);
            Color ink = new Color(0.13f, 0.38f, 0.40f), cream = new Color(0.99f, 0.95f, 0.85f);
            p.Text("RADIO", V(0, 7.6f, -0.27f), 0f, 1.5f, ink, 4.6f);
            p.Text("RADIO", V(0, 7.6f, 0.27f), 180f, 1.5f, ink, 4.6f);
            p.Text("TV", V(0, 1.6f, -0.62f), 0f, 0.8f, cream, 2.6f);
            p.Text("TV", V(0, 1.6f, 0.62f), 180f, 0.8f, cream, 2.6f);
            p.Save(Kit.PropDir);
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The modular pieces of Test Site 07: wall modules for each building style, and the props.
// Local frame: x across, y up, z forward (a prop's front faces +z), the pivot on the ground.
//
// Heights follow the player: a standing player is 2 m tall and 1 m wide, steps up 0.3 m and jumps
// 4.2 m. So nothing anyone can stand on is taller than about 2.9 m (except on purpose, at the radio
// workshop), roofs are at least 7.2 m up, doors are 2 m wide and 3.2 m tall, and windows open from
// 0.9 m to 3.3 m so players can vault through them. Thin tall things (trees, lamp posts) have
// colliders far taller than they look, so their tops can't be stood on.
namespace TestSite07
{
    public class Style
    {
        public string name, wall, trim, accent, fascia, fasciaStripe;
        // Height of the band above the top storey (to the top of the parapet)
        public float fasciaHeight;
        public int storeys = 1;
        public float Top => Storey * storeys + fasciaHeight;
        public const float Storey = 4.5f;

        public static readonly Style Diner = new Style { name = "Diner", wall = "Cream", trim = "Turquoise", accent = "Coral", fascia = "Turquoise", fasciaStripe = "Coral", fasciaHeight = 2.9f };
        public static readonly Style Workshop = new Style { name = "Workshop", wall = "Orange", trim = "Cream", accent = "Turquoise", fascia = "Cream", fasciaStripe = "Turquoise", fasciaHeight = 1.4f, storeys = 2 };
        public static readonly Style Garage = new Style { name = "Garage", wall = "Turquoise", trim = "Cream", accent = "Orange", fascia = "Cream", fasciaStripe = "Orange", fasciaHeight = 2.9f };
        public static readonly Style Depot = new Style { name = "Depot", wall = "Concrete", trim = "Yellow", accent = "DarkGreen", fascia = "Concrete", fasciaStripe = "Yellow", fasciaHeight = 2.9f };
        public static readonly Style[] All = { Diner, Workshop, Garage, Depot };
    }

    public static class Props
    {
        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        private const float H = Style.Storey, T = 0.3f;

        public static void BuildAll()
        {
            foreach (Style s in Style.All) BuildModules(s);
            BuildProps();
        }

        // ---------------------------------------------------------------- wall modules (2 m wide)

        private static void BuildModules(Style s)
        {
            string dir = Kit.ModuleDir;
            Save(Wall(s, $"{s.name} Wall"), dir);
            Save(Window(s, $"{s.name} Window", false), dir);
            Save(Window(s, $"{s.name} Window Glazed", true), dir);
            Save(Door(s, $"{s.name} Door"), dir);

            var corner = new Proto($"{s.name} Corner") { structure = true };
            corner.Box(s.trim, V(0, s.Top / 2f, 0), V(0.45f, s.Top, 0.45f));
            Save(corner, dir);

            var fascia = new Proto($"{s.name} Fascia") { structure = true };
            float f = s.fasciaHeight;
            fascia.Box(s.fascia, V(0, f / 2f, 0), V(2f, f, T));
            fascia.Box(s.fasciaStripe, V(0, f * 0.42f, 0), V(2f, Mathf.Min(0.45f, f * 0.25f), T + 0.06f), false);
            fascia.Box(s.trim, V(0, f - 0.08f, 0), V(2f, 0.16f, T + 0.2f), false);
            Save(fascia, dir);
        }

        private static void StoreyTrim(Proto p, Style s, bool skirting)
        {
            if (skirting) p.Box(s.accent, V(0, 0.2f, 0), V(2f, 0.4f, T + 0.06f), false);
            p.Box(s.trim, V(0, H - 0.25f, 0), V(2f, 0.16f, T + 0.06f), false);
        }

        private static Proto Wall(Style s, string name)
        {
            var p = new Proto(name) { structure = true };
            p.Box(s.wall, V(0, H / 2f, 0), V(2f, H, T));
            StoreyTrim(p, s, true);
            return p;
        }

        // Sill 0..0.9, opening 0.9..3.3, lintel 3.3..4.5. Glazed: a pane on the Glass layer fills the
        // opening (shots pass, players don't).
        private static Proto Window(Style s, string name, bool glazed)
        {
            var p = new Proto(name) { structure = true };
            p.Box(s.wall, V(0, 0.45f, 0), V(2f, 0.9f, T));
            p.Box(s.wall, V(0, 3.9f, 0), V(2f, 1.2f, T));
            p.Box(s.trim, V(0, 0.94f, 0), V(2f, 0.08f, T + 0.16f), false);
            p.Box(s.trim, V(-0.94f, 2.1f, 0), V(0.12f, 2.4f, T + 0.06f), false);
            p.Box(s.trim, V(0.94f, 2.1f, 0), V(0.12f, 2.4f, T + 0.06f), false);
            p.Box(s.trim, V(0, 3.26f, 0), V(2f, 0.08f, T + 0.06f), false);
            if (glazed) p.Glass(V(0, 2.1f, 0), V(1.76f, 2.32f, 0.04f), V(2f, 2.4f, T));
            StoreyTrim(p, s, true);
            return p;
        }

        // A 2 m x 3.2 m doorway
        private static Proto Door(Style s, string name)
        {
            var p = new Proto(name) { structure = true };
            p.Box(s.wall, V(0, 3.85f, 0), V(2f, 1.3f, T));
            p.Box(s.trim, V(-0.95f, 1.6f, 0), V(0.1f, 3.2f, T + 0.08f), false);
            p.Box(s.trim, V(0.95f, 1.6f, 0), V(0.1f, 3.2f, T + 0.08f), false);
            p.Box(s.trim, V(0, 3.24f, 0), V(2f, 0.08f, T + 0.08f), false);
            p.Box(s.trim, V(0, H - 0.25f, 0), V(2f, 0.16f, T + 0.06f), false);
            return p;
        }

        // ---------------------------------------------------------------- props

        private static void BuildProps()
        {
            string dir = Kit.PropDir;

            // Vegetation
            Save(TreeRound("Tree Round A", 1), dir);
            Save(TreeRound("Tree Round B", 4), dir);
            Save(TreePine(), dir);
            Save(Bush("Bush", false), dir);
            Save(Bush("Bush Flowering", true), dir);
            Save(Hedge(), dir);

            // Fences and walls
            Save(PicketFence(), dir);
            Save(FencePost(), dir);
            Save(ChainLink(), dir);
            Save(Railing(), dir);
            Save(LowWall(), dir);
            Save(PlanterWall(), dir);
            Save(BlastWall(), dir);
            Save(JerseyBarrier(), dir);
            Save(Sandbags(), dir);

            // Vehicles
            Save(Car("Car Turquoise", "Turquoise"), dir);
            Save(Car("Car Coral", "Coral"), dir);
            Save(Car("Car Yellow", "Yellow"), dir);
            Save(Pickup(), dir);
            Save(Camper(), dir);
            Save(Van(), dir);

            // Street furniture
            Save(Streetlight(), dir);
            Save(Mailbox(), dir);
            Save(Hydrant(), dir);
            Save(Bench(), dir);
            Save(TrashCan(), dir);
            Save(PowerPole(), dir);

            // Yard and utility
            Save(Shed("Shed Turquoise", "Turquoise"), dir);
            Save(Shed("Shed Coral", "Coral"), dir);
            Save(Shed("Shed Wood", "Wood"), dir);
            Save(PicnicTable(), dir);
            Save(LaundryLine(), dir);
            Save(Crate(), dir);
            Save(CrateStack(), dir);
            Save(Barrel("Barrel Red", "Red"), dir);
            Save(Barrel("Barrel Yellow", "Yellow"), dir);
            Save(BarrelGroup(), dir);
            Save(TireStack(), dir);
            Save(CableSpool(), dir);
            Save(Generator(), dir);
            Save(Transformer(), dir);
            Save(WaterTank(), dir);
            Save(PlanterRound(), dir);
            Save(Kiosk(), dir);

            // Interiors
            Save(Workbench(), dir);
            Save(Shelf(), dir);
            Save(RadioConsole(), dir);
            Save(RadioRack(), dir);
            Save(Booth(), dir);
            Save(DinerTable(), dir);
            Save(Counter(), dir);
            Save(Stool(), dir);
            Save(Stove(), dir);
            Save(KitchenCounter(), dir);
            Save(Fridge(), dir);
            Save(Jukebox(), dir);

            // Structures
            Save(Stair(), dir);
            Save(AntennaMast(), dir);
            Save(DinerSign(), dir);
            Save(WelcomeSign(), dir);
            Save(TestTower(), dir);
            Save(Cottage("Cottage Coral", "Coral", "Turquoise", "RoofSlate"), Kit.BuildingDir);
            Save(Cottage("Cottage Turquoise", "Turquoise", "Coral", "RoofRed"), Kit.BuildingDir);
            Save(Cottage("Cottage Cream", "Cream", "Turquoise", "RoofRed"), Kit.BuildingDir);
            Save(Cottage("Cottage Orange", "Orange", "Cream", "RoofSlate"), Kit.BuildingDir);
            Save(Mesa("Mesa A", 11), dir);
            Save(Mesa("Mesa B", 23), dir);

            // Reused from the SimplePoly Town Pack, with box colliders instead of their mesh colliders
            Wrap("Dumpster", "Props/sptp_dumpster_01");
            Wrap("Electrical Panel", "Props/sptp_electrical_panel_01");
            Wrap("AC Unit", "Props/sptp_air_conditioning_01");
        }

        private static void Save(Proto p, string dir) => p.Save(dir);

        private static Proto TreeRound(string name, int seed)
        {
            var p = new Proto(name);
            p.Cyl("Bark", V(0, 0, 0), 0.22f, 3.1f, 7);
            p.Shape(Shapes.Rock(seed), "Foliage", V(0, 3.7f, 0), V(3.6f, 2.9f, 3.4f));
            p.Shape(Shapes.Rock(seed + 1), "Foliage", V(0.6f, 4.8f, 0.3f), V(2.5f, 2.1f, 2.5f));
            p.Shape(Shapes.Rock(seed + 2), "FoliageDark", V(-0.8f, 3.3f, -0.4f), V(2.3f, 1.9f, 2.3f));
            // Tall and thin: blocks shots at the trunk, and is too tall to stand on
            p.Col(V(0, 5f, 0), V(0.5f, 10f, 0.5f));
            return p;
        }

        private static Proto TreePine()
        {
            var p = new Proto("Tree Pine");
            p.Cyl("Bark", V(0, 0, 0), 0.2f, 1.6f, 6);
            p.Shape(Shapes.Cone(8), "DarkGreen", V(0, 1.1f, 0), V(3.6f, 2.8f, 3.6f));
            p.Shape(Shapes.Cone(8), "DarkGreen", V(0, 2.6f, 0), V(2.9f, 2.5f, 2.9f), 22f);
            p.Shape(Shapes.Cone(8), "FoliageDark", V(0, 4.0f, 0), V(2.0f, 2.3f, 2.0f), 11f);
            p.Col(V(0, 5f, 0), V(0.45f, 10f, 0.45f));
            return p;
        }

        private static Proto Bush(string name, bool flowers)
        {
            var p = new Proto(name);
            p.Shape(Shapes.Rock(7), "Foliage", V(0, 0.5f, 0), V(1.7f, 1.15f, 1.5f));
            p.Shape(Shapes.Rock(8), "FoliageDark", V(0.45f, 0.42f, 0.3f), V(1.1f, 0.9f, 1.1f));
            if (flowers)
            {
                p.Shape(Shapes.Rock(9), "Coral", V(0.3f, 0.95f, 0.45f), V(0.25f, 0.2f, 0.25f));
                p.Shape(Shapes.Rock(10), "Yellow", V(-0.4f, 0.9f, 0.2f), V(0.22f, 0.2f, 0.22f));
                p.Shape(Shapes.Rock(11), "Coral", V(-0.1f, 1.0f, -0.35f), V(0.22f, 0.2f, 0.22f));
            }
            p.Col(V(0, 0.45f, 0), V(1.4f, 0.9f, 1.3f));
            return p;
        }

        private static Proto Hedge()
        {
            var p = new Proto("Hedge 2m");
            p.Box("DarkGreen", V(0, 0.85f, 0), V(2f, 1.7f, 0.9f), false);
            p.Box("FoliageDark", V(0, 1.78f, 0), V(1.9f, 0.18f, 0.78f), false);
            p.Col(V(0, 0.93f, 0), V(2f, 1.86f, 0.9f));
            return p;
        }

        // Along x, 2 m
        private static Proto PicketFence()
        {
            var p = new Proto("Picket Fence 2m");
            p.Box("OffWhite", V(-0.97f, 0.6f, 0), V(0.12f, 1.2f, 0.12f), false);
            p.Box("OffWhite", V(0.97f, 0.6f, 0), V(0.12f, 1.2f, 0.12f), false);
            p.Box("OffWhite", V(0, 0.35f, -0.04f), V(2f, 0.08f, 0.05f), false);
            p.Box("OffWhite", V(0, 0.85f, -0.04f), V(2f, 0.08f, 0.05f), false);
            for (int i = 0; i < 7; i++)
            {
                float x = -0.75f + i * 0.25f;
                p.Box("OffWhite", V(x, 0.53f, 0.01f), V(0.1f, 0.94f, 0.03f), false);
                p.Shape(Shapes.Prism, "OffWhite", V(x, 1.0f, 0.01f), V(0.1f, 0.12f, 0.03f));
            }
            p.Col(V(0, 0.55f, 0), V(2f, 1.1f, 0.14f));
            return p;
        }

        private static Proto FencePost()
        {
            var p = new Proto("Fence Post");
            p.Box("OffWhite", V(0, 0.6f, 0), V(0.14f, 1.2f, 0.14f));
            p.Shape(Shapes.Prism, "OffWhite", V(0, 1.2f, 0), V(0.14f, 0.1f, 0.14f));
            return p;
        }

        // 3 m along x. The mesh is on the Glass layer: it stops players, not shots.
        private static Proto ChainLink()
        {
            var p = new Proto("Chain Link Fence 3m");
            p.Cyl("Metal", V(-1.5f, 0, 0), 0.05f, 2.25f, 6);
            p.Cyl("Metal", V(1.5f, 0, 0), 0.05f, 2.25f, 6);
            p.CylX("Metal", V(0, 2.18f, 0), 0.035f, 3f, 6);
            p.Glass(V(0, 1.1f, 0), V(3f, 2.1f, 0.02f), V(3.06f, 2.25f, 0.12f), "ChainLink");
            return p;
        }

        // 2 m along x, 1 m tall
        private static Proto Railing()
        {
            var p = new Proto("Railing 2m");
            p.Box("Metal", V(-0.96f, 0.5f, 0), V(0.06f, 1f, 0.06f), false);
            p.Box("Metal", V(0.96f, 0.5f, 0), V(0.06f, 1f, 0.06f), false);
            p.Box("Metal", V(0, 1f, 0), V(2f, 0.06f, 0.08f), false);
            p.Box("Metal", V(0, 0.5f, 0), V(2f, 0.04f, 0.04f), false);
            p.Col(V(0, 0.52f, 0), V(2f, 1.04f, 0.1f));
            return p;
        }

        // 4 m along x, waist high
        private static Proto LowWall()
        {
            var p = new Proto("Low Wall 4m") { structure = true };
            p.Box("Concrete", V(0, 0.5f, 0), V(4f, 1f, 0.5f));
            p.Box("Sidewalk", V(0, 1.04f, 0), V(4.08f, 0.08f, 0.62f));
            return p;
        }

        // 4 m along x: a brick planter with shrubs (the shrubs don't stop shots)
        private static Proto PlanterWall()
        {
            var p = new Proto("Planter Wall 4m") { structure = true };
            p.Box("Brick", V(0, 0.45f, 0), V(4f, 0.9f, 1f));
            p.Box("Sidewalk", V(0, 0.93f, 0), V(4.06f, 0.06f, 1.06f), false);
            p.Box("Bark", V(0, 0.92f, 0), V(3.8f, 0.04f, 0.8f), false);
            for (int i = 0; i < 4; i++)
                p.Shape(Shapes.Rock(30 + i), i % 2 == 0 ? "Foliage" : "FoliageDark", V(-1.45f + i * 0.97f, 1.1f, 0), V(1.0f, 0.55f, 0.75f));
            return p;
        }

        // 5 m along x, 2.6 m tall: a concrete blast wall with hazard stripes
        private static Proto BlastWall()
        {
            var p = new Proto("Blast Wall 5m") { structure = true };
            p.Box("Concrete", V(0, 1.3f, 0), V(5f, 2.6f, 0.6f));
            for (int i = 0; i < 10; i++)
                p.Box(i % 2 == 0 ? "Yellow" : "Dark", V(-2.25f + i * 0.5f, 0.3f, 0), V(0.5f, 0.3f, 0.64f), false);
            p.Box("Sidewalk", V(0, 2.65f, 0), V(5.08f, 0.1f, 0.7f), false);
            return p;
        }

        // 3 m along x
        private static Proto JerseyBarrier()
        {
            var p = new Proto("Jersey Barrier");
            p.Box("Concrete", V(0, 0.2f, 0), V(3f, 0.4f, 0.62f), false);
            p.Box("Concrete", V(0, 0.62f, 0), V(2.96f, 0.46f, 0.32f), false);
            p.Box("Yellow", V(0, 0.72f, 0), V(2.98f, 0.12f, 0.34f), false);
            p.Col(V(0, 0.43f, 0), V(3f, 0.86f, 0.62f));
            return p;
        }

        // 3 m along x, waist high
        private static Proto Sandbags()
        {
            var p = new Proto("Sandbags 3m");
            int seed = 40;
            for (int row = 0; row < 3; row++)
            {
                float y = 0.17f + row * 0.31f;
                int columns = row == 2 ? 1 : 2;
                for (int c = 0; c < columns; c++)
                {
                    float z = columns == 1 ? 0f : (c == 0 ? -0.22f : 0.22f);
                    for (int i = 0; i < 5; i++)
                    {
                        float x = -1.2f + i * 0.6f + (row % 2) * 0.3f - 0.15f;
                        p.Shape(Shapes.Rock(seed++ % 12 + 40), "Burlap", V(x, y, z), V(0.72f, 0.34f, 0.5f));
                    }
                }
            }
            p.Col(V(0, 0.53f, 0), V(3f, 1.06f, 0.85f));
            return p;
        }

        // A 1950s sedan, 5 m long (front +z). Its roof is 1.57 m up.
        private static Proto Car(string name, string color)
        {
            var p = new Proto(name);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    p.CylX("Dark", V(sx * 0.86f, 0.36f, sz * 1.55f), 0.36f, 0.26f, 10);
                    p.CylX("Chrome", V(sx * 0.87f, 0.36f, sz * 1.55f), 0.16f, 0.28f, 8);
                }
            p.Box("Dark", V(0, 0.3f, 0), V(1.7f, 0.16f, 4.4f), false);
            p.Box(color, V(0, 0.68f, 0), V(1.96f, 0.62f, 4.9f), false);
            p.Box("Chrome", V(0, 0.8f, 0), V(1.99f, 0.06f, 4.4f), false);
            p.Box("Chrome", V(0, 0.48f, 2.48f), V(2.02f, 0.2f, 0.14f), false);
            p.Box("Chrome", V(0, 0.48f, -2.48f), V(2.02f, 0.2f, 0.14f), false);
            p.Box("Chrome", V(0, 0.76f, 2.46f), V(1.2f, 0.24f, 0.06f), false);
            p.Box("OffWhite", V(-0.7f, 0.86f, 2.46f), V(0.28f, 0.2f, 0.06f), false);
            p.Box("OffWhite", V(0.7f, 0.86f, 2.46f), V(0.28f, 0.2f, 0.06f), false);
            p.Box("Red", V(-0.82f, 0.92f, -2.46f), V(0.2f, 0.26f, 0.06f), false);
            p.Box("Red", V(0.82f, 0.92f, -2.46f), V(0.2f, 0.26f, 0.06f), false);
            p.Shape(Shapes.Wedge, color, V(-0.86f, 0.99f, -1.85f), V(0.14f, 0.32f, 1.2f), 180f);
            p.Shape(Shapes.Wedge, color, V(0.86f, 0.99f, -1.85f), V(0.14f, 0.32f, 1.2f), 180f);
            p.Box("WindowDark", V(0, 1.24f, -0.2f), V(1.66f, 0.5f, 2.3f), false);
            p.Box("OffWhite", V(0, 1.52f, -0.2f), V(1.72f, 0.08f, 2.4f), false);
            p.Col(V(0, 0.6f, 0), V(1.96f, 0.86f, 4.96f));
            p.Col(V(0, 1.3f, -0.2f), V(1.7f, 0.54f, 2.4f));
            return p;
        }

        // 5.4 m long (front +z), cab roof 2.06 m up
        private static Proto Pickup()
        {
            var p = new Proto("Pickup Truck");
            string c = "Red";
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1.75f, 1.75f })
                {
                    p.CylX("Dark", V(sx * 0.9f, 0.4f, sz), 0.4f, 0.28f, 10);
                    p.CylX("Chrome", V(sx * 0.91f, 0.4f, sz), 0.17f, 0.3f, 8);
                }
            p.Box("Dark", V(0, 0.36f, 0), V(1.7f, 0.2f, 5f), false);
            p.Box(c, V(0, 0.75f, 0), V(2f, 0.6f, 5.3f), false);
            p.Box(c, V(0, 1.25f, 1.85f), V(1.9f, 0.4f, 1.5f), false);
            p.Box(c, V(0, 1.55f, 0.45f), V(1.95f, 1f, 1.3f), false);
            p.Box("WindowDark", V(0, 1.72f, 0.45f), V(1.97f, 0.5f, 1.02f), false);
            p.Box("OffWhite", V(0, 2.06f, 0.45f), V(1.9f, 0.06f, 1.25f), false);
            p.Box(c, V(-0.95f, 1.3f, -1.6f), V(0.1f, 0.5f, 2.3f), false);
            p.Box(c, V(0.95f, 1.3f, -1.6f), V(0.1f, 0.5f, 2.3f), false);
            p.Box(c, V(0, 1.3f, -2.62f), V(2f, 0.5f, 0.1f), false);
            p.Box("Wood", V(0, 1.07f, -1.6f), V(1.8f, 0.04f, 2.3f), false);
            p.Box("Wood", V(0.3f, 1.42f, -1.5f), V(0.8f, 0.66f, 0.8f), false);
            p.Box("Chrome", V(0, 0.55f, 2.7f), V(2.04f, 0.22f, 0.14f), false);
            p.Box("Chrome", V(0, 0.55f, -2.7f), V(2.04f, 0.22f, 0.14f), false);
            p.Box("Chrome", V(0, 1.2f, 2.61f), V(1.3f, 0.3f, 0.04f), false);
            p.Box("OffWhite", V(-0.75f, 1.2f, 2.61f), V(0.26f, 0.22f, 0.05f), false);
            p.Box("OffWhite", V(0.75f, 1.2f, 2.61f), V(0.26f, 0.22f, 0.05f), false);
            p.Col(V(0, 0.75f, 0), V(2f, 0.9f, 5.36f));
            p.Col(V(0, 1.55f, 0.45f), V(1.95f, 1f, 1.3f));
            p.Col(V(0, 1.25f, 1.85f), V(1.9f, 0.4f, 1.5f));
            p.Col(V(0, 1.3f, -1.6f), V(2f, 0.5f, 2.3f));
            return p;
        }

        // A round-ended camping trailer, 7 m long (hitch at +z), 2.6 m tall: blocks sight
        private static Proto Camper()
        {
            var p = new Proto("Camper Trailer");
            p.Box("OffWhite", V(0, 1.55f, 0), V(2.4f, 2.1f, 5f), false);
            p.CylX("OffWhite", V(0, 1.55f, 2.5f), 1.05f, 2.4f, 14);
            p.CylX("OffWhite", V(0, 1.55f, -2.5f), 1.05f, 2.4f, 14);
            p.Box("Turquoise", V(0, 1.15f, 0), V(2.44f, 0.35f, 5f), false);
            p.Box("WindowDark", V(0, 1.95f, 0.3f), V(2.44f, 0.5f, 2.6f), false);
            p.Box("Turquoise", V(1.22f, 1.3f, -1.4f), V(0.04f, 1.6f, 0.8f), false);
            p.CylX("Dark", V(-1.1f, 0.4f, 0.4f), 0.4f, 0.28f, 10);
            p.CylX("Dark", V(1.1f, 0.4f, 0.4f), 0.4f, 0.28f, 10);
            p.Box("Dark", V(0, 0.55f, 4.0f), V(0.15f, 0.12f, 1.5f), false);
            p.Cyl("Dark", V(0, 0, 4.4f), 0.06f, 0.55f, 6);
            p.Col(V(0, 1.3f, 0), V(2.4f, 2.6f, 5f));
            p.Col(V(0, 1.3f, 2.9f), V(2.2f, 2.4f, 1f));
            p.Col(V(0, 1.3f, -2.9f), V(2.2f, 2.4f, 1f));
            return p;
        }

        // A rounded-nose bakery van, 5.6 m long (front +z), 2.7 m tall: blocks sight down the street
        private static Proto Van()
        {
            var p = new Proto("Delivery Van");
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1.8f, 1.6f })
                {
                    p.CylX("Dark", V(sx * 0.95f, 0.4f, sz), 0.4f, 0.26f, 10);
                    p.CylX("Chrome", V(sx * 0.96f, 0.4f, sz), 0.17f, 0.28f, 8);
                }
            p.Box("Cream", V(0, 1.6f, -0.5f), V(2.2f, 2.2f, 4.4f), false);
            p.CylX("Cream", V(0, 1.6f, 1.7f), 1.1f, 2.2f, 12);
            p.Box("Coral", V(0, 1.05f, 0), V(2.24f, 0.3f, 5.5f), false);
            p.Box("WindowDark", V(0, 2.05f, 2.15f), V(1.9f, 0.6f, 0.6f), false);
            p.Box("WindowDark", V(0, 2.1f, 1.2f), V(2.24f, 0.55f, 0.8f), false);
            p.Box("Chrome", V(0, 0.55f, 2.78f), V(2.1f, 0.2f, 0.12f), false);
            p.Box("Chrome", V(0, 0.55f, -2.75f), V(2.1f, 0.2f, 0.12f), false);
            Color ink = new Color(0.72f, 0.29f, 0.27f);
            p.Text("ATOMIC BREAD", V(1.13f, 1.75f, -0.6f), -90f, 0.38f, ink, 3.6f);
            p.Text("ATOMIC BREAD", V(-1.13f, 1.75f, -0.6f), 90f, 0.38f, ink, 3.6f);
            p.Col(V(0, 1.45f, -0.3f), V(2.2f, 2.5f, 5f));
            p.Col(V(0, 1.2f, 2.35f), V(2f, 1.9f, 0.7f));
            return p;
        }

        private static Proto Streetlight()
        {
            var p = new Proto("Streetlight");
            p.Cyl("DarkGreen", V(0, 0, 0), 0.22f, 0.5f, 8);
            p.Cyl("DarkGreen", V(0, 0, 0), 0.09f, 5.6f, 8);
            p.Box("DarkGreen", V(0, 5.5f, 0.6f), V(0.1f, 0.1f, 1.3f), false);
            p.Box("DarkGreen", V(0, 5.42f, 1.2f), V(0.5f, 0.18f, 0.7f), false);
            p.Box("LampGlow", V(0, 5.3f, 1.2f), V(0.4f, 0.05f, 0.55f), false);
            p.Col(V(0, 4f, 0), V(0.3f, 8f, 0.3f));
            return p;
        }

        private static Proto Mailbox()
        {
            var p = new Proto("Mailbox");
            p.Box("Wood", V(0, 0.5f, 0), V(0.1f, 1f, 0.1f), false);
            p.Box("Metal", V(0, 1.12f, 0), V(0.3f, 0.25f, 0.55f), false);
            p.CylZ("Metal", V(0, 1.245f, 0), 0.15f, 0.55f, 8);
            p.Box("Red", V(0.17f, 1.3f, -0.12f), V(0.02f, 0.28f, 0.06f), false);
            p.Col(V(0, 0.66f, 0), V(0.32f, 1.32f, 0.56f));
            return p;
        }

        private static Proto Hydrant()
        {
            var p = new Proto("Fire Hydrant");
            p.Cyl("Red", V(0, 0, 0), 0.16f, 0.55f, 8);
            p.Shape(Shapes.Cone(8), "Red", V(0, 0.55f, 0), V(0.34f, 0.16f, 0.34f));
            p.CylX("Red", V(0, 0.38f, 0), 0.06f, 0.46f, 6);
            p.Cyl("Red", V(0, 0, 0), 0.22f, 0.08f, 8);
            p.Col(V(0, 0.36f, 0), V(0.4f, 0.72f, 0.4f));
            return p;
        }

        private static Proto Bench()
        {
            var p = new Proto("Bench");
            p.Box("Wood", V(0, 0.45f, 0), V(1.8f, 0.08f, 0.45f), false);
            p.Box("Wood", V(0, 0.82f, -0.2f), V(1.8f, 0.36f, 0.06f), false);
            p.Box("Dark", V(-0.75f, 0.22f, 0), V(0.08f, 0.44f, 0.45f), false);
            p.Box("Dark", V(0.75f, 0.22f, 0), V(0.08f, 0.44f, 0.45f), false);
            p.Col(V(0, 0.5f, -0.02f), V(1.8f, 1f, 0.5f));
            return p;
        }

        private static Proto TrashCan()
        {
            var p = new Proto("Trash Can");
            p.Cyl("Metal", V(0, 0, 0), 0.3f, 0.9f, 10);
            p.Cyl("Metal", V(0, 0.9f, 0), 0.33f, 0.06f, 10);
            p.Cyl("Dark", V(0, 0.96f, 0), 0.06f, 0.06f, 6);
            p.Col(V(0, 0.48f, 0), V(0.6f, 0.96f, 0.6f));
            return p;
        }

        private static Proto PowerPole()
        {
            var p = new Proto("Power Pole");
            p.Cyl("Bark", V(0, 0, 0), 0.15f, 9.5f, 7);
            p.Box("Wood", V(0, 8.8f, 0), V(2.4f, 0.14f, 0.14f), false);
            foreach (float x in new[] { -1f, 0f, 1f }) p.Cyl("OffWhite", V(x, 8.87f, 0), 0.06f, 0.2f, 6);
            p.Cyl("Metal", V(0.32f, 7.0f, 0), 0.25f, 0.8f, 8);
            p.Col(V(0, 6f, 0), V(0.35f, 12f, 0.35f));
            return p;
        }

        // 3 x 3 m lean-to shed; its roof (2.5 m) can be stood on
        private static Proto Shed(string name, string color)
        {
            var p = new Proto(name);
            p.Box(color, V(0, 1.15f, 0), V(3f, 2.3f, 3f), false);
            foreach (float x in new[] { -1.5f, 1.5f })
                foreach (float z in new[] { -1.5f, 1.5f })
                    p.Box("OffWhite", V(x, 1.15f, z), V(0.12f, 2.3f, 0.12f), false);
            p.Box(color == "Wood" ? "Bark" : "OffWhite", V(0.2f, 0.98f, 1.51f), V(1.1f, 1.95f, 0.05f), false);
            p.Box("WindowDark", V(1.52f, 1.5f, 0), V(0.05f, 0.6f, 0.9f), false);
            p.Box("OffWhite", V(1.53f, 1.5f, 0), V(0.04f, 0.72f, 1.02f), false);
            p.Shape(Shapes.Wedge, "RoofRed", V(0, 2.3f, 0), V(3.5f, 0.4f, 3.6f), 180f);
            p.Col(V(0, 1.25f, 0), V(3f, 2.5f, 3f));
            return p;
        }

        private static Proto PicnicTable()
        {
            var p = new Proto("Picnic Table");
            p.Box("Wood", V(0, 0.75f, 0), V(1.8f, 0.06f, 0.8f), false);
            p.Box("Wood", V(0, 0.45f, 0.62f), V(1.8f, 0.05f, 0.28f), false);
            p.Box("Wood", V(0, 0.45f, -0.62f), V(1.8f, 0.05f, 0.28f), false);
            p.Box("Bark", V(-0.7f, 0.37f, 0), V(0.08f, 0.74f, 1.5f), false);
            p.Box("Bark", V(0.7f, 0.37f, 0), V(0.08f, 0.74f, 1.5f), false);
            p.Col(V(0, 0.39f, 0), V(1.8f, 0.78f, 1.6f));
            return p;
        }

        // Two T posts 6 m apart along z, with washing on the lines (only the posts are solid)
        private static Proto LaundryLine()
        {
            var p = new Proto("Laundry Line");
            foreach (float z in new[] { -3f, 3f })
            {
                p.Cyl("Metal", V(0, 0, z), 0.05f, 2.05f, 6);
                p.Box("Metal", V(0, 2f, z), V(1.2f, 0.05f, 0.05f), false);
                p.Col(V(0, 1f, z), V(0.15f, 2f, 0.15f));
            }
            p.Box("OffWhite", V(-0.5f, 1.98f, 0), V(0.015f, 0.015f, 6f), false);
            p.Box("OffWhite", V(0.5f, 1.98f, 0), V(0.015f, 0.015f, 6f), false);
            p.Box("Turquoise", V(-0.5f, 1.66f, -1.6f), V(0.02f, 0.62f, 0.8f), false);
            p.Box("Coral", V(-0.5f, 1.72f, 0.2f), V(0.02f, 0.5f, 0.6f), false);
            p.Box("OffWhite", V(0.5f, 1.62f, -0.6f), V(0.02f, 0.7f, 1.1f), false);
            p.Box("Yellow", V(0.5f, 1.74f, 1.4f), V(0.02f, 0.46f, 0.5f), false);
            return p;
        }

        private static void CrateParts(Proto p, Vector3 c, float yaw = 0f)
        {
            p.BoxRot("Wood", c + V(0, 0.5f, 0), V(1f, 1f, 1f), Quaternion.Euler(0, yaw, 0));
            p.BoxRot("Bark", c + V(0, 0.15f, 0), V(1.02f, 0.08f, 1.02f), Quaternion.Euler(0, yaw, 0), false);
            p.BoxRot("Bark", c + V(0, 0.85f, 0), V(1.02f, 0.08f, 1.02f), Quaternion.Euler(0, yaw, 0), false);
        }

        private static Proto Crate()
        {
            var p = new Proto("Crate");
            CrateParts(p, Vector3.zero);
            return p;
        }

        private static Proto CrateStack()
        {
            var p = new Proto("Crate Stack");
            CrateParts(p, V(-0.52f, 0, 0));
            CrateParts(p, V(0.52f, 0, 0.05f));
            CrateParts(p, V(0, 1f, 0));
            return p;
        }

        private static void BarrelParts(Proto p, Vector3 c, string color)
        {
            p.Cyl(color, c, 0.3f, 0.9f, 10);
            p.Cyl("Dark", c + V(0, 0.28f, 0), 0.31f, 0.04f, 10);
            p.Cyl("Dark", c + V(0, 0.62f, 0), 0.31f, 0.04f, 10);
            p.Col(c + V(0, 0.45f, 0), V(0.6f, 0.9f, 0.6f));
        }

        private static Proto Barrel(string name, string color)
        {
            var p = new Proto(name);
            BarrelParts(p, Vector3.zero, color);
            return p;
        }

        private static Proto BarrelGroup()
        {
            var p = new Proto("Barrel Group");
            BarrelParts(p, V(-0.35f, 0, 0), "Red");
            BarrelParts(p, V(0.35f, 0, 0), "Yellow");
            BarrelParts(p, V(0, 0, 0.6f), "Metal");
            return p;
        }

        private static Proto TireStack()
        {
            var p = new Proto("Tire Stack");
            for (int i = 0; i < 4; i++) p.Cyl("Dark", V(0, i * 0.25f, 0), 0.38f, 0.23f, 10);
            p.Cyl("Metal", V(0, 0.99f, 0), 0.18f, 0.02f, 8);
            p.Col(V(0, 0.5f, 0), V(0.76f, 1f, 0.76f));
            return p;
        }

        private static Proto CableSpool()
        {
            var p = new Proto("Cable Spool");
            p.CylX("Wood", V(-0.45f, 0.9f, 0), 0.9f, 0.12f, 12);
            p.CylX("Wood", V(0.45f, 0.9f, 0), 0.9f, 0.12f, 12);
            p.CylX("Dark", V(0, 0.9f, 0), 0.62f, 0.78f, 12);
            p.Col(V(0, 0.9f, 0), V(1.02f, 1.8f, 1.5f));
            return p;
        }

        private static Proto Generator()
        {
            var p = new Proto("Generator");
            p.Box("Dark", V(0, 0.1f, 0), V(2.3f, 0.2f, 1.3f), false);
            p.Box("Yellow", V(0, 0.75f, 0), V(2.2f, 1.1f, 1.2f), false);
            p.Box("Dark", V(-0.6f, 0.8f, 0.61f), V(0.6f, 0.5f, 0.02f), false);
            p.Box("DialGlow", V(0.5f, 0.95f, 0.61f), V(0.18f, 0.12f, 0.02f), false);
            p.Cyl("Metal", V(0.8f, 1.3f, -0.2f), 0.07f, 0.6f, 6);
            p.Col(V(0, 0.65f, 0), V(2.3f, 1.3f, 1.3f));
            return p;
        }

        private static Proto Transformer()
        {
            var p = new Proto("Transformer");
            p.Box("DarkGreen", V(0, 0.9f, 0), V(1.6f, 1.8f, 1.2f), false);
            for (int i = 0; i < 4; i++) p.Box("DarkGreen", V(-0.6f + i * 0.4f, 0.8f, 0.68f), V(0.06f, 1.2f, 0.16f), false);
            foreach (float x in new[] { -0.5f, 0f, 0.5f }) p.Cyl("OffWhite", V(x, 1.8f, 0), 0.08f, 0.3f, 6);
            p.Box("Yellow", V(0, 1.2f, -0.61f), V(0.5f, 0.4f, 0.02f), false);
            p.Col(V(0, 0.9f, 0.05f), V(1.6f, 1.8f, 1.35f));
            return p;
        }

        // A water tank on legs; walk under it, the tank itself (3.8 - 8 m) is out of reach
        private static Proto WaterTank()
        {
            var p = new Proto("Water Tank");
            foreach (float x in new[] { -1.5f, 1.5f })
                foreach (float z in new[] { -1.5f, 1.5f })
                    p.Box("Metal", V(x, 1.9f, z), V(0.25f, 3.8f, 0.25f));
            for (int side = 0; side < 4; side++)
            {
                float yaw = side * 90f;
                Quaternion q = Quaternion.Euler(0, yaw, 0);
                p.BoxRot("Metal", q * V(0, 2.5f, 1.5f), V(3.2f, 0.08f, 0.08f), q * Quaternion.Euler(0, 0, 38f), false);
                p.BoxRot("Metal", q * V(0, 2.5f, 1.5f), V(3.2f, 0.08f, 0.08f), q * Quaternion.Euler(0, 0, -38f), false);
            }
            p.Cyl("OffWhite", V(0, 3.8f, 0), 2.1f, 3f, 14);
            p.Cyl("Turquoise", V(0, 5.0f, 0), 2.13f, 0.5f, 14);
            p.Shape(Shapes.Cone(14), "RoofRed", V(0, 6.8f, 0), V(4.5f, 1.2f, 4.5f));
            p.Col(V(0, 5.9f, 0), V(3.5f, 4.2f, 3.5f));
            return p;
        }

        // A round concrete planter with a tree
        private static Proto PlanterRound()
        {
            var p = new Proto("Planter Round");
            p.Cyl("Concrete", V(0, 0, 0), 1.5f, 0.85f, 12);
            p.Cyl("Bark", V(0, 0.85f, 0), 1.32f, 0.03f, 12);
            p.Cyl("Bark", V(0, 0.85f, 0), 0.18f, 2.2f, 7);
            p.Shape(Shapes.Rock(3), "Foliage", V(0, 3.1f, 0), V(2.8f, 2.3f, 2.8f));
            p.Shape(Shapes.Rock(5), "FoliageDark", V(0.4f, 3.9f, 0.2f), V(1.8f, 1.5f, 1.8f));
            p.Col(V(0, 0.43f, 0), V(2.3f, 0.86f, 2.3f));
            p.ColRot(V(0, 0.43f, 0), V(2.3f, 0.86f, 2.3f), Quaternion.Euler(0, 45f, 0));
            p.Col(V(0, 5f, 0), V(0.45f, 10f, 0.45f));
            return p;
        }

        // A 2.4 m square newsstand, 2.8 m tall: solid cover that blocks sight
        private static Proto Kiosk()
        {
            var p = new Proto("Kiosk") { structure = true };
            p.Box("Coral", V(0, 1.3f, 0), V(2.4f, 2.6f, 2.4f));
            p.Box("Turquoise", V(0, 2.7f, 0), V(3f, 0.2f, 3f));
            p.Box("OffWhite", V(0, 2.86f, 0), V(2.6f, 0.12f, 2.6f), false);
            Color cream = new Color(0.99f, 0.95f, 0.85f);
            for (int side = 0; side < 4; side++)
            {
                Quaternion q = Quaternion.Euler(0, side * 90f, 0);
                p.BoxRot("WindowDark", q * V(0, 1.55f, 1.21f), V(1.4f, 0.8f, 0.02f), q, false);
                p.BoxRot("OffWhite", q * V(0, 1.12f, 1.3f), V(1.6f, 0.07f, 0.22f), q, false);
                p.BoxRot("Yellow", q * V(0, 0.55f, 1.21f), V(1.8f, 0.6f, 0.02f), q, false);
                p.Text(side % 2 == 0 ? "NEWS" : "COLA", q * V(0, 2.25f, 1.22f), side * 90f + 180f, 0.32f, cream, 2f);
            }
            return p;
        }

        private static Proto Workbench()
        {
            var p = new Proto("Workbench");
            p.Box("Wood", V(0, 0.95f, 0), V(2.4f, 0.08f, 0.9f), false);
            p.Box("Metal", V(-1.1f, 0.45f, 0), V(0.1f, 0.9f, 0.85f), false);
            p.Box("Metal", V(1.1f, 0.45f, 0), V(0.1f, 0.9f, 0.85f), false);
            p.Box("Wood", V(0, 0.25f, 0), V(2.3f, 0.05f, 0.8f), false);
            p.Box("Coral", V(-0.6f, 1.16f, -0.05f), V(0.6f, 0.36f, 0.4f), false);
            p.Box("DialGlow", V(-0.6f, 1.18f, 0.16f), V(0.22f, 0.12f, 0.02f), false);
            p.Box("Red", V(0.6f, 1.1f, 0.1f), V(0.5f, 0.22f, 0.25f), false);
            p.Box("Turquoise", V(0.15f, 1.07f, -0.25f), V(0.3f, 0.16f, 0.2f), false);
            p.Box("Wood", V(-0.1f, 0.4f, 0), V(0.6f, 0.3f, 0.5f), false);
            p.Col(V(0, 0.5f, 0), V(2.4f, 1f, 0.9f));
            return p;
        }

        // 2 m along x, 2.2 m tall, 0.6 deep
        private static Proto Shelf()
        {
            var p = new Proto("Shelf");
            foreach (float x in new[] { -0.97f, 0.97f })
                foreach (float z in new[] { -0.27f, 0.27f })
                    p.Box("Metal", V(x, 1.1f, z), V(0.05f, 2.2f, 0.05f), false);
            string[] colors = { "Wood", "Coral", "Cream", "Turquoise", "Yellow", "Wood" };
            var random = new System.Random(5);
            for (int s = 0; s < 4; s++)
            {
                float y = 0.1f + s * 0.65f;
                p.Box("Metal", V(0, y, 0), V(2f, 0.04f, 0.6f), false);
                if (s == 3) continue;
                float x = -0.85f;
                while (x < 0.75f)
                {
                    float w = 0.25f + (float)random.NextDouble() * 0.35f;
                    float h = 0.2f + (float)random.NextDouble() * 0.3f;
                    if (x + w > 0.9f) break;
                    p.Box(colors[random.Next(colors.Length)], V(x + w / 2f, y + 0.02f + h / 2f, 0), V(w, h, 0.45f), false);
                    x += w + 0.05f;
                }
            }
            p.Col(V(0, 1.1f, 0), V(2f, 2.2f, 0.6f));
            return p;
        }

        private static Proto RadioConsole()
        {
            var p = new Proto("Radio Console");
            p.Box("Cream", V(0, 0.42f, 0.1f), V(2.4f, 0.84f, 0.8f));
            p.Box("Turquoise", V(0, 1.25f, -0.25f), V(2.4f, 0.82f, 0.5f));
            p.Box("Dark", V(0, 1.25f, 0.005f), V(2.2f, 0.66f, 0.01f), false);
            for (int i = 0; i < 5; i++)
                p.Box(i % 2 == 0 ? "DialGlow" : "NeonTeal", V(-0.85f + i * 0.42f, 1.36f, 0.015f), V(0.24f, 0.14f, 0.02f), false);
            for (int i = 0; i < 8; i++) p.Box("Chrome", V(-0.9f + i * 0.26f, 1.1f, 0.02f), V(0.06f, 0.06f, 0.04f), false);
            p.Cyl("Chrome", V(0.6f, 0.84f, 0.25f), 0.03f, 0.3f, 6);
            p.Shape(Shapes.Rock(2), "Dark", V(0.6f, 1.16f, 0.25f), V(0.12f, 0.16f, 0.12f));
            return p;
        }

        private static Proto RadioRack()
        {
            var p = new Proto("Radio Rack");
            p.Box("Dark", V(0, 1.1f, 0), V(1.2f, 2.2f, 0.6f));
            for (int i = 0; i < 4; i++)
            {
                float y = 0.4f + i * 0.45f;
                p.Box("Metal", V(0, y, 0.31f), V(1.1f, 0.38f, 0.02f), false);
                p.Box(i % 2 == 0 ? "DialGlow" : "NeonTeal", V(-0.3f, y, 0.33f), V(0.2f, 0.1f, 0.02f), false);
                p.Box("Chrome", V(0.25f, y, 0.33f), V(0.06f, 0.06f, 0.03f), false);
            }
            return p;
        }

        // A diner booth: two benches and a table, 3 m along z
        private static Proto Booth()
        {
            var p = new Proto("Diner Booth");
            foreach (float s in new[] { -1f, 1f })
            {
                p.Box("Red", V(0, 0.23f, s * 1.15f), V(1.6f, 0.46f, 0.6f), false);
                p.Box("Red", V(0, 0.85f, s * 1.4f), V(1.6f, 0.8f, 0.18f), false);
                p.Box("Chrome", V(0, 1.26f, s * 1.4f), V(1.62f, 0.04f, 0.2f), false);
                p.Col(V(0, 0.63f, s * 1.2f), V(1.6f, 1.26f, 0.6f));
            }
            p.Box("OffWhite", V(0, 0.75f, 0), V(1.1f, 0.05f, 0.95f), false);
            p.Box("Chrome", V(0, 0.74f, 0), V(1.12f, 0.03f, 0.97f), false);
            p.Cyl("Chrome", V(0, 0, 0), 0.06f, 0.72f, 6);
            p.Col(V(0, 0.39f, 0), V(1.1f, 0.78f, 0.95f));
            return p;
        }

        private static Proto DinerTable()
        {
            var p = new Proto("Diner Table");
            p.Cyl("OffWhite", V(0, 0.72f, 0), 0.5f, 0.05f, 12);
            p.Cyl("Chrome", V(0, 0.71f, 0), 0.51f, 0.02f, 12);
            p.Cyl("Chrome", V(0, 0, 0), 0.05f, 0.72f, 6);
            p.Cyl("Chrome", V(0, 0, 0), 0.25f, 0.03f, 8);
            foreach (float s in new[] { -1f, 1f })
            {
                p.Box("Red", V(0, 0.45f, s * 0.72f), V(0.45f, 0.08f, 0.42f), false);
                p.Box("Red", V(0, 0.75f, s * 0.92f), V(0.45f, 0.5f, 0.06f), false);
                p.Box("Chrome", V(0, 0.22f, s * 0.72f), V(0.04f, 0.44f, 0.04f), false);
            }
            p.Col(V(0, 0.38f, 0), V(0.9f, 0.76f, 0.9f));
            return p;
        }

        // 4 m along x, 1.1 m tall; the customer side is +z
        private static Proto Counter()
        {
            var p = new Proto("Counter 4m");
            p.Box("Turquoise", V(0, 0.5f, 0), V(4f, 1f, 0.8f));
            p.Box("Chrome", V(0, 0.08f, 0.01f), V(4.02f, 0.16f, 0.81f), false);
            p.Box("Coral", V(0, 0.7f, 0.41f), V(4f, 0.1f, 0.02f), false);
            p.Box("OffWhite", V(0, 1.04f, 0.05f), V(4.1f, 0.08f, 0.95f));
            p.Box("Chrome", V(0, 1.0f, 0.53f), V(4.1f, 0.04f, 0.04f), false);
            p.Cyl("Chrome", V(-1.2f, 1.08f, 0.1f), 0.18f, 0.12f, 10);
            p.Shape(Shapes.Cone(10), "Glass", V(-1.2f, 1.2f, 0.1f), V(0.36f, 0.3f, 0.36f));
            return p;
        }

        // Visual only: you walk through stools
        private static Proto Stool()
        {
            var p = new Proto("Stool") { lightmap = false };
            p.Cyl("Chrome", V(0, 0, 0), 0.04f, 0.7f, 6);
            p.Cyl("Chrome", V(0, 0, 0), 0.18f, 0.03f, 8);
            p.Cyl("Red", V(0, 0.7f, 0), 0.2f, 0.08f, 10);
            return p;
        }

        private static Proto Stove()
        {
            var p = new Proto("Stove");
            p.Box("OffWhite", V(0, 0.48f, 0), V(1.4f, 0.96f, 0.75f));
            p.Box("Dark", V(0, 0.98f, 0), V(1.4f, 0.04f, 0.75f), false);
            p.Box("Metal", V(0, 0.45f, 0.38f), V(1f, 0.5f, 0.02f), false);
            p.Box("Chrome", V(0, 0.75f, 0.39f), V(0.9f, 0.04f, 0.04f), false);
            foreach (float x in new[] { -0.35f, 0.35f })
                foreach (float z in new[] { -0.18f, 0.18f })
                    p.Cyl("Metal", V(x, 1.0f, z), 0.13f, 0.02f, 8);
            return p;
        }

        // 2 m along x
        private static Proto KitchenCounter()
        {
            var p = new Proto("Kitchen Counter");
            p.Box("Cream", V(0, 0.48f, 0), V(2f, 0.96f, 0.7f));
            p.Box("Metal", V(0, 0.98f, 0), V(2.02f, 0.05f, 0.72f), false);
            p.Box("Turquoise", V(-0.5f, 0.5f, 0.36f), V(0.9f, 0.7f, 0.02f), false);
            p.Box("Turquoise", V(0.5f, 0.5f, 0.36f), V(0.9f, 0.7f, 0.02f), false);
            p.Box("Red", V(0.4f, 1.1f, 0), V(0.3f, 0.2f, 0.3f), false);
            return p;
        }

        private static Proto Fridge()
        {
            var p = new Proto("Fridge");
            p.Box("OffWhite", V(0, 1.05f, 0), V(1f, 2.1f, 0.75f));
            p.Box("OffWhite", V(0, 2.12f, 0), V(0.94f, 0.06f, 0.7f), false);
            p.Box("Chrome", V(0.38f, 1.3f, 0.39f), V(0.05f, 0.5f, 0.05f), false);
            p.Box("Chrome", V(0, 1.55f, 0.38f), V(0.96f, 0.02f, 0.02f), false);
            return p;
        }

        private static Proto Jukebox()
        {
            var p = new Proto("Jukebox");
            p.Box("Coral", V(0, 0.7f, 0), V(1f, 1.4f, 0.6f), false);
            p.CylZ("Coral", V(0, 1.4f, 0), 0.5f, 0.6f, 12);
            p.Box("NeonTeal", V(-0.43f, 1.0f, 0.31f), V(0.08f, 1.0f, 0.02f), false);
            p.Box("NeonTeal", V(0.43f, 1.0f, 0.31f), V(0.08f, 1.0f, 0.02f), false);
            p.Box("Yellow", V(0, 1.15f, 0.31f), V(0.6f, 0.45f, 0.02f), false);
            p.Box("Chrome", V(0, 0.45f, 0.31f), V(0.6f, 0.3f, 0.02f), false);
            p.Col(V(0, 0.95f, 0), V(1f, 1.9f, 0.6f));
            return p;
        }

        // Rises 4.5 m over 8 m along +z, 2.6 m wide. Walked on as a ramp (0.125 m above the step
        // nosings so feet don't sink into them); solid underneath so nobody gets under it.
        public const float StairRise = 4.5f, StairRun = 8f, StairWidth = 2.6f;

        private static Proto Stair()
        {
            var p = new Proto("Stair") { structure = true };
            const int steps = 18;
            float tread = StairRun / steps, rise = StairRise / steps;
            for (int i = 0; i < steps; i++)
            {
                float top = (i + 1) * rise;
                p.Box("Concrete", V(0, top / 2f, (i + 0.5f) * tread), V(StairWidth, top, tread), false);
                p.Box("Sidewalk", V(0, top - 0.02f, (i + 0.9f) * tread), V(StairWidth + 0.02f, 0.05f, tread * 0.2f), false);
            }
            float angle = Mathf.Atan2(StairRise, StairRun);
            float length = Mathf.Sqrt(StairRise * StairRise + StairRun * StairRun);
            Vector3 normal = new Vector3(0, Mathf.Cos(angle), -Mathf.Sin(angle));
            Vector3 surface = V(0, StairRise / 2f + 0.125f, StairRun / 2f);
            p.ColRot(surface - normal * 0.15f, V(StairWidth, 0.3f, length), Quaternion.Euler(-angle * Mathf.Rad2Deg, 0, 0));
            for (int k = 1; k < 4; k++)
            {
                float h = StairRise * (2f * k) / StairRun;
                p.ColMM(V(-StairWidth / 2f, 0, 2f * k), V(StairWidth / 2f, h, 2f * k + 2f));
            }
            // Rails (visual)
            foreach (float x in new[] { -StairWidth / 2f + 0.05f, StairWidth / 2f - 0.05f })
            {
                for (int k = 0; k < 4; k++)
                {
                    float z = 0.6f + k * 2.3f;
                    float y = StairRise * z / StairRun;
                    p.Box("Metal", V(x, y + 0.55f, z), V(0.05f, 1.1f, 0.05f), false);
                }
                p.BoxRot("Metal", V(x, StairRise / 2f + 1.1f, StairRun / 2f), V(0.06f, 0.06f, length), Quaternion.Euler(-angle * Mathf.Rad2Deg, 0, 0), false);
            }
            return p;
        }

        private static Proto AntennaMast()
        {
            var p = new Proto("Antenna Mast") { lightmap = false };
            p.Cyl("Metal", V(0, 0, 0), 0.35f, 0.4f, 8);
            p.Cyl("Metal", V(0, 0, 0), 0.1f, 8.5f, 6);
            for (int i = 1; i <= 4; i++)
            {
                float y = 1.5f + i * 1.5f;
                float w = 2.4f - i * 0.4f;
                p.Box("Metal", V(0, y, 0), V(w, 0.06f, 0.06f), false);
                p.Box("Metal", V(0, y, 0), V(0.06f, 0.06f, w * 0.6f), false);
            }
            p.Shape(Shapes.Rock(6), "NeonPink", V(0, 8.6f, 0), V(0.3f, 0.3f, 0.3f));
            return p;
        }

        // The diner's roadside sign: a 2.8 m base, a pole and the board up at 6.5 - 8.7 m
        private static Proto DinerSign()
        {
            var p = new Proto("Diner Sign") { structure = true };
            p.Box("Turquoise", V(0, 1.4f, 0), V(3f, 2.8f, 1.2f));
            p.Box("Coral", V(0, 2.75f, 0), V(3.06f, 0.12f, 1.26f), false);
            p.Cyl("Metal", V(0, 2.8f, 0), 0.22f, 3.8f, 8);
            p.Col(V(0, 5f, 0), V(0.5f, 4.4f, 0.5f));
            p.Box("Coral", V(0, 7.6f, 0), V(5f, 2.2f, 0.5f));
            p.Box("NeonTeal", V(0, 8.73f, 0), V(5.1f, 0.08f, 0.56f), false);
            p.Box("NeonTeal", V(0, 6.47f, 0), V(5.1f, 0.08f, 0.56f), false);
            p.Box("NeonTeal", V(-2.53f, 7.6f, 0), V(0.08f, 2.3f, 0.56f), false);
            p.Box("NeonTeal", V(2.53f, 7.6f, 0), V(0.08f, 2.3f, 0.56f), false);
            for (int i = 0; i < 4; i++)
                p.BoxRot("Yellow", V(1.8f, 9.6f, 0), V(1.6f, 0.12f, 0.1f), Quaternion.Euler(0, 0, i * 45f), false);
            p.Shape(Shapes.Rock(12), "NeonPink", V(1.8f, 9.6f, 0), V(0.45f, 0.45f, 0.45f));
            Color pink = new Color(1f, 0.55f, 0.68f);
            p.Text("DINER", V(0, 7.6f, -0.27f), 0f, 1.5f, pink, 4.6f);
            p.Text("DINER", V(0, 7.6f, 0.27f), 180f, 1.5f, pink, 4.6f);
            p.Text("EAT", V(0, 1.6f, -0.62f), 0f, 0.8f, new Color(0.98f, 0.95f, 0.88f), 2.6f);
            p.Text("EAT", V(0, 1.6f, 0.62f), 180f, 0.8f, new Color(0.98f, 0.95f, 0.88f), 2.6f);
            return p;
        }

        // 10 m along x, 7.3 m tall (out of reach from anything 2.9 m up), both faces lettered. Blocks sight out of a spawn.
        private static Proto WelcomeSign()
        {
            var p = new Proto("Welcome Sign") { structure = true };
            p.Box("Brick", V(0, 1f, 0), V(10f, 2f, 1f));
            p.Box("Cream", V(0, 4.65f, 0), V(9.6f, 5.3f, 0.8f));
            p.Box("Turquoise", V(0, 7.25f, 0), V(10f, 0.25f, 0.9f), false);
            p.Box("Turquoise", V(-4.85f, 4.65f, 0), V(0.3f, 5.3f, 0.9f), false);
            p.Box("Turquoise", V(4.85f, 4.65f, 0), V(0.3f, 5.3f, 0.9f), false);
            p.Box("Coral", V(0, 2.12f, 0), V(9.7f, 0.24f, 0.9f), false);
            // A civil defence badge on each face: a yellow disc with a dark triangle
            foreach (float z in new[] { -0.42f, 0.42f })
            {
                p.CylZ("Yellow", V(-3.75f, 4.9f, z), 0.75f, 0.06f, 16);
                p.Shape(Shapes.Prism, "Dark", V(-3.75f, 4.57f, z), V(0.95f, 0.8f, 0.1f));
            }
            Color ink = new Color(0.13f, 0.33f, 0.36f), red = new Color(0.72f, 0.29f, 0.27f);
            foreach (var (z, yaw) in new[] { (-0.47f, 0f), (0.47f, 180f) })
            {
                p.Text("WELCOME TO", V(0.8f, 6.3f, z), yaw, 0.55f, ink, 6f);
                p.Text("TEST SITE 07", V(0.8f, 5.0f, z), yaw, 0.8f, red, 6.4f);
                p.Text("POP. 0  -  EST. 1955", V(0.8f, 3.75f, z), yaw, 0.38f, ink, 6f);
            }
            return p;
        }

        // The courtyard landmark: a steel test tower over a 6 m square of legs, the shot cab on top.
        // Walk under it; everything above the legs is out of reach.
        private static Proto TestTower()
        {
            var p = new Proto("Test Tower") { structure = true };
            const float half = 3f, top = 10.4f;
            foreach (float x in new[] { -half, half })
                foreach (float z in new[] { -half, half })
                {
                    p.Box("Metal", V(x, top / 2f, z), V(0.5f, top, 0.5f));
                    p.Box("Concrete", V(x, 0.3f, z), V(1.1f, 0.6f, 1.1f));
                }
            float[] levels = { 2.6f, 6.5f, top - 0.3f };
            foreach (float y in levels)
            {
                p.Box("Metal", V(0, y, -half), V(6.5f, 0.18f, 0.18f), false);
                p.Box("Metal", V(0, y, half), V(6.5f, 0.18f, 0.18f), false);
                p.Box("Metal", V(-half, y, 0), V(0.18f, 0.18f, 6.5f), false);
                p.Box("Metal", V(half, y, 0), V(0.18f, 0.18f, 6.5f), false);
            }
            for (int side = 0; side < 4; side++)
            {
                Quaternion q = Quaternion.Euler(0, side * 90f, 0);
                for (int band = 0; band < 2; band++)
                {
                    float y0 = levels[band], y1 = levels[band + 1];
                    float a = Mathf.Atan2(y1 - y0, 2f * half) * Mathf.Rad2Deg;
                    float len = Mathf.Sqrt((y1 - y0) * (y1 - y0) + 4f * half * half);
                    Vector3 c = q * V(0, (y0 + y1) / 2f, -half);
                    p.BoxRot("Metal", c, V(len, 0.12f, 0.12f), q * Quaternion.Euler(0, 0, a), false);
                    p.BoxRot("Metal", c, V(len, 0.12f, 0.12f), q * Quaternion.Euler(0, 0, -a), false);
                }
            }
            p.Box("Metal", V(0, top + 0.2f, 0), V(8f, 0.4f, 8f));
            for (int side = 0; side < 4; side++)
            {
                Quaternion q = Quaternion.Euler(0, side * 90f, 0);
                p.BoxRot("Metal", q * V(0, top + 1.4f, 3.95f), V(8f, 0.06f, 0.06f), q, false);
                for (int i = 0; i < 5; i++) p.BoxRot("Metal", q * V(-3.9f + i * 1.95f, top + 0.9f, 3.95f), V(0.06f, 1f, 0.06f), q, false);
            }
            p.Box("OffWhite", V(0, top + 2.0f, 0), V(4.2f, 3.2f, 4.2f));
            p.Box("Coral", V(0, top + 1.0f, 0), V(4.26f, 0.4f, 4.26f), false);
            p.Box("Dark", V(0, top + 2.0f, 2.11f), V(1.2f, 2.2f, 0.02f), false);
            p.Shape(Shapes.Prism, "RoofSlate", V(0, top + 3.6f, 0), V(4.8f, 1.2f, 4.8f));
            p.Col(V(0, top + 4.2f, 0), V(4.8f, 1.2f, 4.8f));
            p.Cyl("Metal", V(1.5f, top + 3.6f, 1.5f), 0.06f, 3.5f, 6);
            p.Shape(Shapes.Rock(13), "NeonPink", V(1.5f, top + 7.2f, 1.5f), V(0.4f, 0.4f, 0.4f));
            // The device, seen through the open hatch
            p.Shape(Shapes.Rock(14), "Dark", V(0, top + 1.8f, 1.2f), V(1.6f, 1.6f, 1.6f));
            return p;
        }

        // A one-storey house, 12 x 7 m, front door on +z. Not enterable: roof collider up to the
        // ridge (7.2 m) so the roof can't be climbed.
        private static Proto Cottage(string name, string wall, string trim, string roof)
        {
            var p = new Proto(name) { structure = true };
            const float W = 12f, D = 7f, Hw = 4.6f;
            p.Box(wall, V(0, Hw / 2f, 0), V(W, Hw, D));
            p.Box("Brick", V(0, 0.3f, 0), V(W + 0.1f, 0.6f, D + 0.1f), false);
            foreach (float x in new[] { -W / 2f, W / 2f })
                foreach (float z in new[] { -D / 2f, D / 2f })
                    p.Box("OffWhite", V(x, Hw / 2f, z), V(0.25f, Hw, 0.25f), false);
            p.Box("OffWhite", V(0, Hw - 0.1f, 0), V(W + 0.12f, 0.2f, D + 0.12f), false);
            void FaceWindow(float x, float z, float dir)
            {
                p.Box("OffWhite", V(x, 2.4f, z + dir * 0.02f), V(1.7f, 1.9f, 0.06f), false);
                p.Box("WindowDark", V(x, 2.4f, z + dir * 0.05f), V(1.4f, 1.6f, 0.06f), false);
                p.Box("OffWhite", V(x, 2.4f, z + dir * 0.08f), V(0.06f, 1.6f, 0.04f), false);
                p.Box(trim, V(x - 1.05f, 2.4f, z + dir * 0.03f), V(0.4f, 1.7f, 0.06f), false);
                p.Box(trim, V(x + 1.05f, 2.4f, z + dir * 0.03f), V(0.4f, 1.7f, 0.06f), false);
                p.Box("Brick", V(x, 1.35f, z + dir * 0.25f), V(1.6f, 0.3f, 0.4f), false);
                p.Shape(Shapes.Rock(20 + (int)(x * 3)), "Coral", V(x - 0.3f, 1.55f, z + dir * 0.25f), V(0.4f, 0.25f, 0.3f));
                p.Shape(Shapes.Rock(21 + (int)(x * 3)), "Foliage", V(x + 0.3f, 1.55f, z + dir * 0.25f), V(0.5f, 0.3f, 0.3f));
            }
            void SideWindow(float x)
            {
                float dir = Mathf.Sign(x);
                p.Box("OffWhite", V(x + dir * 0.02f, 2.4f, 0), V(0.06f, 1.9f, 1.7f), false);
                p.Box("WindowDark", V(x + dir * 0.05f, 2.4f, 0), V(0.06f, 1.6f, 1.4f), false);
            }
            FaceWindow(-4f, D / 2f, 1f);
            FaceWindow(-1.2f, D / 2f, 1f);
            FaceWindow(4.2f, D / 2f, 1f);
            FaceWindow(-3.5f, -D / 2f, -1f);
            FaceWindow(0.5f, -D / 2f, -1f);
            FaceWindow(3.8f, -D / 2f, -1f);
            SideWindow(-W / 2f);
            SideWindow(W / 2f);
            // Front door and porch
            p.Box("OffWhite", V(1.7f, 1.35f, D / 2f + 0.02f), V(1.4f, 2.7f, 0.06f), false);
            p.Box(trim, V(1.7f, 1.2f, D / 2f + 0.05f), V(1.1f, 2.4f, 0.06f), false);
            p.Box("Chrome", V(2.1f, 1.2f, D / 2f + 0.09f), V(0.08f, 0.08f, 0.06f), false);
            p.Box("Sidewalk", V(1.7f, 0.08f, D / 2f + 0.55f), V(2.6f, 0.16f, 1.1f), false);
            p.Box(roof, V(1.7f, 3.1f, D / 2f + 0.55f), V(3f, 0.12f, 1.3f), false);
            // Porch posts: solid only to 2.5 m, so their tops can't be a step up to the roof
            foreach (float x in new[] { 0.35f, 3.05f })
            {
                p.Box("OffWhite", V(x, 1.55f, D / 2f + 1.05f), V(0.12f, 3.1f, 0.12f), false);
                p.Col(V(x, 1.25f, D / 2f + 1.05f), V(0.12f, 2.5f, 0.12f));
            }
            // Roof: the visual gable, and a box collider up to the ridge
            p.Shape(Shapes.Prism, roof, V(0, Hw, 0), V(D + 0.9f, 2.6f, W + 0.9f), 90f);
            p.Col(V(0, Hw + 1.4f, 0), V(W + 0.9f, 2.8f, D + 0.9f));
            p.Box("Brick", V(-3.5f, 6.4f, -1.2f), V(0.8f, 2.6f, 0.8f));
            p.Box("Concrete", V(-3.5f, 7.75f, -1.2f), V(0.95f, 0.12f, 0.95f), false);
            // TV antenna
            p.Cyl("Metal", V(3.5f, 6.4f, 0.6f), 0.04f, 2.2f, 5);
            p.Box("Metal", V(3.5f, 8.3f, 0.6f), V(1.4f, 0.04f, 0.04f), false);
            p.Box("Metal", V(3.5f, 8.0f, 0.6f), V(1.0f, 0.04f, 0.04f), false);
            return p;
        }

        // A distant flat-topped desert mesa for the backdrop (unit size: scale it in the layout)
        private static Proto Mesa(string name, int seed)
        {
            var p = new Proto(name) { lightmap = false };
            p.Shape(Shapes.Cylinder(9), "Mesa", V(0, 0, 0), V(1f, 0.55f, 0.8f), seed);
            p.Shape(Shapes.Cone(9), "Mesa", V(0, 0, 0), V(1.35f, 0.3f, 1.1f), seed + 20f);
            p.Shape(Shapes.Cylinder(9), "SandDark", V(0, 0.55f, 0), V(0.92f, 0.03f, 0.73f), seed);
            p.Shape(Shapes.Rock(seed), "Mesa", V(0.35f, 0.5f, 0.1f), V(0.5f, 0.25f, 0.45f));
            return p;
        }

        // Wraps a SimplePoly prop: its mesh and material, with one box collider from its bounds
        private static void Wrap(string name, string source)
        {
            GameObject src = Kit.Load($"Assets/SimplePoly - Town Pack/Prefabs/{source}.prefab");
            MeshFilter mf = src.GetComponentInChildren<MeshFilter>();
            MeshRenderer mr = src.GetComponentInChildren<MeshRenderer>();
            var root = new GameObject(name);
            try
            {
                root.layer = Kit.Ground;
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                visual.layer = Kit.Ground;
                visual.transform.localRotation = mf.transform.localRotation;
                visual.transform.localScale = mf.transform.lossyScale;
                visual.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                MeshRenderer r = visual.AddComponent<MeshRenderer>();
                r.sharedMaterials = mr.sharedMaterials;
                r.receiveGI = ReceiveGI.LightProbes;
                Bounds b = r.bounds;
                // Sit it on the ground
                visual.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
                var col = new GameObject("Collision");
                col.transform.SetParent(root.transform, false);
                col.layer = Kit.Ground;
                BoxCollider box = col.AddComponent<BoxCollider>();
                box.center = new Vector3(0, b.size.y / 2f, 0);
                box.size = b.size;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) Kit.SetStatic(t.gameObject, false);
                PrefabUtility.SaveAsPrefabAsset(root, $"{Kit.PropDir}/{name}.prefab");
                Debug.Log($"Test Site 07: {name} is {b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00} m");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}

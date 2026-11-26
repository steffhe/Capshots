using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Where everything goes on Test Site 07. World units are metres: x west (0) to east (120), z south (0)
// to north (90), ground at y = 0.
//
// The map is fair by turning it round: the south half (team A's spawn and everything up to the
// courtyard's middle) is placed as written, then again turned 180 degrees about the map's centre
// (60, 45) for team B. The west lane is gardens and the east lane is utility yards, so a yard's props
// swap theme when it's turned round (a garden shed becomes a pump house, a picket fence a chain-link
// fence). The diner, the radio workshop, the courtyard tower and the objectives are placed once.
//
//   z 0-15   spawn lot (x 35-85)       | backyards (west)          | service yard + garage (east)
//   z 15-26  two cottages and the welcome sign screen the spawn
//   z 26-33  the south street, crossing all three lanes
//   z 33-57  diner (x 12-34) | courtyard (x 39-81, tower and point B) | radio workshop (x 86-108)
//   z 57-90  the same, turned round
namespace TestSite07
{
    public class Layout
    {
        public const float SizeX = 120f, SizeZ = 90f;

        private readonly Transform root;
        private Transform group;
        // Placing the turned-round copy
        private bool turned;

        public readonly List<Transform> spawns = new List<Transform>();
        public readonly List<ObjectiveMarker> objectives = new List<ObjectiveMarker>();

        public Layout(Transform root) { this.root = root; }

        // ---------------------------------------------------------------- placement helpers

        private Transform Group(string name)
        {
            Transform t = root.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(root, false);
            }
            return group = t;
        }

        private Vector3 P(float x, float z, float y = 0f) => turned ? new Vector3(SizeX - x, y, SizeZ - z) : new Vector3(x, y, z);
        private float Yaw(float yaw) => turned ? yaw + 180f : yaw;

        private GameObject Place(GameObject prefab, float x, float z, float yaw, float y, Vector3? scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            go.transform.SetPositionAndRotation(P(x, z, y), Quaternion.Euler(0f, Yaw(yaw), 0f));
            if (scale.HasValue) go.transform.localScale = scale.Value;
            return go;
        }

        private GameObject Put(string prop, float x, float z, float yaw = 0f, float y = 0f, Vector3? scale = null)
            => Place(Kit.Prop(prop), x, z, yaw, y, scale);

        private GameObject PutBuilding(string building, float x, float z, float yaw = 0f, float y = 0f)
            => Place(Kit.Building(building), x, z, yaw, y, null);

        // A ground surface from (x0, z0) to (x1, z1) with its top at 'top'. Raised ones (kerbs,
        // pavements, the plaza) get a collider; the flat ones are just paint on the ground.
        private void Surface(string mat, float x0, float z0, float x1, float z1, float top, bool collide = false)
        {
            float bottom = collide ? 0f : Mathf.Min(top - 0.05f, -0.01f);
            Vector3 a = P(x0, z0), b = P(x1, z1);
            Vector3 min = new Vector3(Mathf.Min(a.x, b.x), bottom, Mathf.Min(a.z, b.z));
            Vector3 max = new Vector3(Mathf.Max(a.x, b.x), top, Mathf.Max(a.z, b.z));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Kit.Block(mat, collide), group);
            go.transform.position = (min + max) * 0.5f;
            go.transform.localScale = max - min;
        }

        // Modules of a fence (or hedge) from (x0, z0) to (x1, z1), stretched to fit exactly
        private void Run(string prop, float moduleLength, float x0, float z0, float x1, float z1, float y = 0f)
        {
            Vector3 a = P(x0, z0, y), b = P(x1, z1, y);
            Vector3 d = b - a;
            float length = d.magnitude;
            int n = Mathf.Max(1, Mathf.RoundToInt(length / moduleLength));
            float each = length / n;
            float yaw = Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg;
            GameObject prefab = Kit.Prop(prop);
            for (int i = 0; i < n; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                go.transform.SetPositionAndRotation(a + d * ((i + 0.5f) / n), Quaternion.Euler(0f, yaw, 0f));
                go.transform.localScale = new Vector3(each / moduleLength, 1f, 1f);
            }
        }

        private void Spawn(float x, float z, float yaw, int team, bool freeForAllOnly = false)
        {
            Transform parent = group;
            var go = new GameObject($"Spawn {(freeForAllOnly ? "FFA" : team == 0 ? "A" : "B")} {spawns.Count + 1:00}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(P(x, z, 1.1f), Quaternion.Euler(0f, Yaw(yaw), 0f));
            SpawnSide side = go.AddComponent<SpawnSide>();
            side.team = freeForAllOnly ? -1 : team;
            side.freeForAllOnly = freeForAllOnly;
            spawns.Add(go.transform);
        }

        // ---------------------------------------------------------------- the map

        public void BuildAll()
        {
            Group("Ground");
            // The playable ground: one collider for the whole map, its top at y = 0
            var ground = (GameObject)PrefabUtility.InstantiatePrefab(Kit.Block("Sand"), group);
            ground.name = "Ground";
            ground.transform.position = new Vector3(SizeX / 2f, -0.5f, SizeZ / 2f);
            ground.transform.localScale = new Vector3(SizeX + 4f, 1f, SizeZ + 4f);

            for (int pass = 0; pass < 2; pass++)
            {
                turned = pass == 1;
                Group("Ground");
                Surfaces();
                Group(turned ? "North" : "South");
                Backyards();
                SpawnLot();
                Street();
                ServiceYard();
                Alley();
                CourtyardHalf();
                Group("Spawns");
                Spawns();
            }
            turned = false;

            Group("Ground");
            Surface("Concrete", 39f, 35f, 81f, 55f, 0.15f, true);
            Surface("Sidewalk", 52f, 38f, 68f, 52f, 0.16f);

            Group("Landmarks");
            PutBuilding("Diner", 12f, 33f);
            PutBuilding("Radio Workshop", 86f, 39f);
            Put("Test Tower", 60f, 45f, 0f, 0.15f);

            Group("Objectives");
            Objective("A", 27f, 43f, 0.12f);
            Objective("B", 60f, 45f, 0.15f);
            Objective("C", 93f, 47f, 0.12f);

            Group("Backdrop");
            Backdrop();
        }

        private void Objective(string label, float x, float z, float y)
        {
            var go = new GameObject("Objective " + label);
            go.transform.SetParent(group, false);
            go.transform.position = new Vector3(x, y, z);
            ObjectiveMarker marker = go.AddComponent<ObjectiveMarker>();
            marker.label = label;
            marker.radius = 4f;
            objectives.Add(marker);
        }

        // Ground surfaces of the south half (turned round for the north)
        private void Surfaces()
        {
            bool gardenWest = !turned;
            Surface(gardenWest ? "Grass" : "SandDark", 0f, 0f, 33f, 24f, 0.03f);
            Surface("Asphalt", 33f, 0f, 87f, 15f, 0.02f);
            Surface(gardenWest ? "Asphalt" : "Grass", 87f, 0f, 120f, 28f, gardenWest ? 0.02f : 0.03f);
            Surface("Grass", 33f, 15f, 52.5f, 24f, 0.03f);
            Surface("Grass", 67.5f, 15f, 87f, 24f, 0.03f);
            Surface("Sidewalk", 52.5f, 15f, 67.5f, 24f, 0.12f, true);
            Surface("Sidewalk", 33f, 24f, 87f, 26f, 0.12f, true);
            Surface("Asphalt", 33f, 26f, 87f, 33f, 0.02f);
            Surface("Asphalt", 0f, 24f, 33f, 31.6f, 0.02f);
            Surface("Sidewalk", 12f, 31.6f, 34f, 33f, 0.12f, true);
            Surface("Concrete", 86f, 28f, 120f, 39f, 0.12f, true);
            Surface(gardenWest ? "Grass" : "Asphalt", 0f, 31.6f, 12f, 51f, 0.03f);
            Surface("Sidewalk", 34f, 33f, 86f, 35f, 0.12f, true);
            Surface("Sidewalk", 34f, 35f, 39f, 55f, 0.12f, true);

            // Parking bays, the street's centre line and two zebra crossings
            for (float x = 36f; x <= 84.1f; x += 3f) Surface("Paint", x - 0.06f, 0.8f, x + 0.06f, 5.2f, 0.025f);
            for (float x = 35f; x < 85f; x += 4f) Surface("Yellow", x, 29.42f, x + 2f, 29.58f, 0.025f);
            foreach (float cx in new[] { 50f, 70f })
                for (int i = 0; i < 6; i++)
                {
                    float x = cx - 2.5f + i;
                    Surface("Paint", x - 0.25f, 26.3f, x + 0.25f, 32.7f, 0.025f);
                }
            for (float x = 3f; x < 31f; x += 3.5f) Surface("Paint", x - 0.06f, 24.6f, x + 0.06f, 28.6f, 0.025f);
            if (gardenWest)
            {
                // Stepping stones through the gardens
                for (float z = 13.6f; z < 24f; z += 1.25f)
                {
                    Surface("Sidewalk", 8.15f, z - 0.3f, 8.85f, z + 0.3f, 0.05f);
                    Surface("Sidewalk", 22.15f, z - 0.3f, 22.85f, z + 0.3f, 0.05f);
                }
                for (float z = 33.5f; z < 50.5f; z += 1.25f) Surface("Sidewalk", 6.15f, z - 0.3f, 6.85f, z + 0.3f, 0.05f);
            }
        }

        // The south-west backyards (x 0 - 33, z 0 - 24); turned round, the north-east utility yards
        private void Backyards()
        {
            bool garden = !turned;
            string fence = garden ? "Picket Fence 2m" : "Chain Link Fence 3m";
            float module = garden ? 2f : 3f;
            // An east-west fence with two gates, and a north-south one between the two yards
            Run(fence, module, 0f, 13f, 7f, 13f);
            Run(fence, module, 10f, 13f, 21f, 13f);
            Run(fence, module, 24f, 13f, 31f, 13f);
            Run(fence, module, 18f, 0f, 18f, 6f);
            Run(fence, module, 18f, 9f, 18f, 13f);
            if (garden)
                foreach (var (x, z) in new[] { (7f, 13f), (10f, 13f), (21f, 13f), (24f, 13f), (31f, 13f), (18f, 6f), (18f, 9f) })
                    Put("Fence Post", x, z);

            // Yard 1 (x 0 - 18, z 0 - 13)
            Put(garden ? "Shed Turquoise" : "Shed Wood", 12.5f, 5.5f);
            Put(garden ? "Tree Round A" : "Power Pole", 5.5f, 9.5f);
            if (garden) Put("Laundry Line", 7f, 7f);
            else Put("Cable Spool", 6.5f, 6.5f, 30f);
            Put(garden ? "Bush Flowering" : "Barrel Group", 2.5f, 2.5f);
            Put(garden ? "Bush" : "Crate", 15.8f, 2.4f, 15f);
            // Yard 2 (x 18 - 33, z 0 - 13)
            Put(garden ? "Shed Coral" : "Generator", 27.5f, 9.5f, garden ? 0f : 90f);
            Put(garden ? "Tree Round B" : "Transformer", 22f, 4f);
            Put(garden ? "Picnic Table" : "Crate Stack", 25f, 6.2f, 90f);
            Put(garden ? "Bush" : "Barrel Red", 31.2f, 2.4f);
            // The open garden between the yards and the diner's car park (z 13 - 24)
            Put("Tree Pine", 4f, 20.5f);
            if (garden)
            {
                Put("Hedge 2m", 11.5f, 18f, 90f);
                Put("Hedge 2m", 11.5f, 20f, 90f);
            }
            else
            {
                Put("Crate Stack", 11.5f, 18.2f, 90f);
                Put("Crate", 11.5f, 20.4f, 10f);
            }
            if (garden) Put("Tree Round A", 16.5f, 22f);
            else Put("Water Tank", 16.5f, 21f);
            Put(garden ? "Bush Flowering" : "Barrel Group", 21.5f, 17.5f);
            Put("Shed Wood", 28f, 19f, 270f);

            // Between the yards and the spawn lot: a hedge (chain link in the east) with a gap, and a
            // fence with a gate along the corridor up to the street
            string screen = garden ? "Hedge 2m" : "Chain Link Fence 3m";
            Run(screen, garden ? 2f : 3f, 33f, 0f, 33f, 6f);
            Run(screen, garden ? 2f : 3f, 33f, 10f, 33f, 15f);
            Run(fence, module, 33f, 15f, 33f, 18f);
            Run(fence, module, 33f, 20f, 33f, 24f);
            Put(garden ? "Mailbox" : "Electrical Panel", 30.5f, 24.5f, 180f);
        }

        // The spawn lot (x 33 - 87, z 0 - 15) and the screen north of it: two cottages and the sign
        private void SpawnLot()
        {
            PutBuilding(turned ? "Cottage Orange" : "Cottage Coral", 44f, 19.5f);
            PutBuilding(turned ? "Cottage Cream" : "Cottage Turquoise", 76f, 19.5f);
            Put("Welcome Sign", 60f, 18.6f);
            Put("Planter Round", 60f, 21.8f);
            Put("Bench", 55.5f, 16.3f, 180f);
            Put("Bench", 64.5f, 16.3f, 180f);
            Put("Car Turquoise", 43.5f, 13.3f, 90f);
            Put("Car Yellow", 76.5f, 13.3f, 270f);
            Put("Streetlight", 52.6f, 14.4f, 180f);
            Put("Streetlight", 67.4f, 14.4f, 180f);
            Put("Trash Can", 35.5f, 14.2f);
            Put("Trash Can", 84.5f, 14.2f);
            Put("Bush", 36.5f, 21f);
            Put("Bush Flowering", 51.5f, 22.5f);
            Put("Bush Flowering", 68.5f, 22.5f);
            Put("Bush", 83.8f, 21f);
        }

        // The street (z 26 - 33) from the diner's car park to the garage apron
        private void Street()
        {
            Put(turned ? "Radio Sign" : "Diner Sign", 36.5f, 25f);
            Put("Camper Trailer", 55f, 28.4f, 90f);
            Put("Delivery Van", 68f, 30.5f, 270f);
            Put("Car Turquoise", 40.5f, 31f, 90f);
            Put("Pickup Truck", 80.5f, 27.6f, 90f);
            Put("Jersey Barrier", 85f, 30.6f, 90f);
            Put("Streetlight", 47f, 25.2f);
            Put("Streetlight", 73f, 25.2f);
            Put("Fire Hydrant", 54.5f, 25.3f);
            Put("Mailbox", 41.5f, 24.6f, 180f);
            Put("Mailbox", 79.5f, 24.6f, 180f);
            Put("Trash Can", 65.5f, 25.3f);
            // The diner's car park
            Put("Car Coral", 16f, 28.2f);
            Put("Car Yellow", 29f, 28.4f, 8f);
            Put("Streetlight", 11.2f, 24.8f, 0f);
        }

        // The south-east service yard with the garage (x 86 - 120, z 0 - 39); turned round, the
        // north-west yard with the civil defence depot behind the diner
        private void ServiceYard()
        {
            bool utility = !turned;
            PutBuilding(utility ? "Garage" : "CD Depot", 92f, 14f);
            if (utility)
            {
                Put("Transformer", 103f, 6f);
                Put("Transformer", 106.5f, 6f);
                Put("Power Pole", 89.5f, 1.5f);
                Put("Power Pole", 116f, 1.5f);
                Put("Generator", 97f, 9.5f);
                Put("Barrel Group", 116f, 11f);
                Put("Crate Stack", 110.5f, 12.5f);
                Put("Water Tank", 113.5f, 22f);
                Put("Pickup Truck", 113.5f, 33f);
                Put("Electrical Panel", 91.7f, 24.5f);
                Put("Tire Stack", 104.5f, 29.4f);
            }
            else
            {
                Put("Bush", 103f, 6f);
                Put("Bush Flowering", 106.5f, 6f);
                Put("Tree Pine", 89.5f, 1.8f);
                Put("Tree Pine", 116f, 1.8f);
                Put("Picnic Table", 97f, 9.5f);
                Put("Bush Flowering", 116f, 11f);
                Put("Shed Wood", 110.5f, 11.5f, 90f);
                Put("Tree Round A", 113.5f, 22f);
                Put("Car Coral", 113.5f, 33f);
                Put("Trash Can", 91.6f, 24.5f);
                Put("Bush", 104.5f, 29.4f);
            }
            Put("Dumpster", 109.4f, 17f);
            Put("Car Yellow", 98f, 33.5f, 90f);
            Put("Barrel Red", 90.2f, 37.6f);
            Put("Crate", 95.2f, 36.6f, 12f);
            Put("Streetlight", 87.6f, 30f, 90f);
            Put("Jersey Barrier", 89.5f, 20f, 90f);
        }

        // The garden path down the diner's west side (x 0 - 12, z 31.6 - 51); turned round, the service
        // alley down the radio workshop's east side
        private void Alley()
        {
            Put("Camper Trailer", 3.5f, 43f, 180f);
            if (!turned)
            {
                Put("Tree Round B", 6f, 34.2f);
                Put("Bush", 1.5f, 50f);
                Put("Bush Flowering", 1.4f, 35.5f);
                Put("Dumpster", 10.6f, 43.5f);
                Put("Trash Can", 11.3f, 40.7f);
                Put("Trash Can", 11.3f, 41.4f);
            }
            else
            {
                // (in the original's frame: these land along the workshop's east wall)
                Put("Dumpster", 10.6f, 49.4f);
                Put("Crate Stack", 7f, 49.5f);
                Put("AC Unit", 11.2f, 42.5f);
                Put("Barrel Group", 1.5f, 35f);
            }
        }

        // The south half of the courtyard's walls and cover (turned round for the north half)
        private void CourtyardHalf()
        {
            const float y = 0.15f;
            // Edge: entrances at x 47 - 54, 58 - 62 and 66 - 73 on this side, and two on the west
            Put("Planter Wall 4m", 41f, 35.6f, 0f, y);
            Put("Planter Wall 4m", 45f, 35.6f, 0f, y);
            Put("Low Wall 4m", 56f, 35.6f, 0f, y);
            Put("Low Wall 4m", 64f, 35.6f, 0f, y);
            Put("Planter Wall 4m", 75f, 35.6f, 0f, y);
            Put("Planter Wall 4m", 79f, 35.6f, 0f, y);
            Put("Planter Wall 4m", 39.6f, 38.1f, 90f, y);
            Put("Low Wall 4m", 39.45f, 45f, 90f, y);

            // Cover: sandbags round point B under the tower, a kiosk and blast walls that break the
            // long north-south lines, a low wall and planters
            Put("Blast Wall 5m", 60f, 37.4f, 0f, y);
            Put("Sandbags 3m", 60f, 40.8f, 0f, y);
            Put("Sandbags 3m", 55.4f, 45f, 90f, y);
            Put("Kiosk", 50f, 45f, 0f, y);
            Put("Blast Wall 5m", 47.5f, 40.6f, 0f, y);
            Put("Blast Wall 5m", 66.5f, 38.8f, 0f, y);
            Put("Low Wall 4m", 75.5f, 40.5f, 90f, y);
            Put("Planter Round", 42.5f, 38.6f, 0f, y);
            Put("Planter Round", 53.5f, 39f, 0f, y);
            Put("Streetlight", 43f, 42f, 90f, y);
            Put("Trash Can", 68.5f, 36.6f, 0f, y);
        }

        // Team A: twelve points in the spawn lot. Lane points (free for all only) in the yards.
        private void Spawns()
        {
            int team = turned ? 1 : 0;
            foreach (float z in new[] { 5f, 10f })
                foreach (float x in new[] { 39f, 47f, 55f, 65f, 73f, 81f })
                    Spawn(x, z, 0f, team);
            Spawn(10f, 10.5f, 45f, team, true);
            Spawn(99f, 4f, 0f, team, true);
            Spawn(5f, 28f, 90f, team, true);
        }

        // Outside the boundary: desert, a highway, mesas, houses and power lines (no colliders needed:
        // nobody gets out there)
        private void Backdrop()
        {
            var desert = (GameObject)PrefabUtility.InstantiatePrefab(Kit.Block("Sand", false), group);
            desert.name = "Desert";
            desert.transform.position = new Vector3(SizeX / 2f, -0.06f, SizeZ / 2f);
            desert.transform.localScale = new Vector3(900f, 0.08f, 900f);
            GameObjectUtility.SetStaticEditorFlags(desert, StaticEditorFlags.BatchingStatic);
            desert.GetComponentInChildren<MeshRenderer>().receiveGI = ReceiveGI.LightProbes;

            Surface("Asphalt", -300f, -22f, 420f, -14f, -0.005f);
            Surface("Asphalt", -300f, 104f, 420f, 112f, -0.005f);
            for (float x = -100f; x < 230f; x += 6f)
            {
                Surface("Yellow", x, -18.08f, x + 3f, -17.92f, 0.0f);
                Surface("Yellow", x, 107.92f, x + 3f, 108.08f, 0.0f);
            }
            for (float x = -60f; x <= 180f; x += 20f)
            {
                Put("Power Pole", x, -8f);
                Put("Power Pole", x + 10f, 98f);
            }
            foreach (var (x, z, sx, sy, mesa) in new[]
            {
                (-110f, 60f, 70f, 26f, "Mesa A"), (-140f, -60f, 90f, 34f, "Mesa B"), (230f, 20f, 80f, 30f, "Mesa A"),
                (250f, 130f, 110f, 40f, "Mesa B"), (40f, -170f, 140f, 38f, "Mesa A"), (120f, 250f, 120f, 44f, "Mesa B"),
                (-60f, 210f, 90f, 30f, "Mesa A"), (300f, -120f, 100f, 36f, "Mesa B"),
            })
            {
                GameObject m = Put(mesa, x, z, x * 7f, 0f, new Vector3(sx, sy, sx));
                GameObjectUtility.SetStaticEditorFlags(m, StaticEditorFlags.BatchingStatic);
            }
            // Mannequin houses along the outside of the fence
            PutBuilding("Cottage Cream", -12f, 15f, 90f);
            PutBuilding("Cottage Turquoise", -12f, 62f, 90f);
            PutBuilding("Cottage Coral", 132f, 28f, 270f);
            PutBuilding("Cottage Orange", 132f, 75f, 270f);
            Put("Tree Round A", -6f, 38f);
            Put("Tree Pine", -7f, 82f);
            Put("Tree Round B", 127f, 52f);
            Put("Tree Pine", 126f, 8f);
            Put("Car Coral", 20f, -18f, 90f);
            Put("Car Turquoise", 95f, 108f, 270f);
            // The old tower out on the flats
            Put("Test Tower", 70f, 190f, 20f, 0f, new Vector3(1.4f, 1.4f, 1.4f));
        }
    }
}

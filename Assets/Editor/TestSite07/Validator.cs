using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

// Checks Test Site 07 as a game map and draws pictures of it: Capshots > Test Site 07 > Validate /
// Capture Views. Both open the saved scene as a preview copy (its own physics), so nothing open in
// the editor is touched.
//
// Validate checks, with the player's real numbers (2 m tall, 0.5 m radius, 0.3 m step, 45 degree
// slopes, 13 m/s jump against 20 m/s^2 gravity = 4.2 m up, 10 m/s through the air):
//  - every spawn point stands clear on the ground
//  - the walkable mesh joins every spawn, objective and lane, upstairs included, and the lanes join
//    up without passing through a spawn
//  - no line of sight from one team's spawn area into the other's
//  - nothing out of reach can be reached by jumping from something within reach (roofs, signs)
//  - no missing scripts, meshes, materials or prefabs
// The report goes to the console and Temp/TestSite07/validation.txt.
namespace TestSite07
{
    public static class Validator
    {
        public const string OutDir = "Temp/TestSite07";
        private const float JumpHeight = 13f * 13f / (2f * 20f);
        // How far across a jump carries while climbing rise metres: 13 m/s up against 20 m/s^2, 10 m/s
        // through the air, until it falls back to that height; plus the capsule on both ends
        private static float AirReach(float rise)
        {
            float d = 169f - 40f * Mathf.Max(rise, -20f);
            if (d < 0f) return -1f;
            return 10f * (13f + Mathf.Sqrt(d)) / 20f + 1f;
        }
        private const float PlayerHeight = 2f;

        private static readonly Rect SpawnA = Rect.MinMaxRect(35f, 3f, 85f, 14f);
        private static readonly Rect SpawnB = Rect.MinMaxRect(35f, 76f, 85f, 87f);
        // Where standing above 3 m is meant to be possible: the radio workshop's upper floor,
        // balcony and stairs
        private static readonly Rect UpstairsArea = Rect.MinMaxRect(85.5f, 38.5f, 112f, 58.5f);

        [MenuItem("Capshots/Test Site 07/Validate")]
        public static string Validate()
        {
            Directory.CreateDirectory(OutDir);
            var report = new StringBuilder();
            int problems = 0;
            void Problem(string text) { problems++; report.AppendLine("  PROBLEM: " + text); }

            Scene scene = OpenPreview(Builder.ScenePath);
            NavMeshDataInstance navInstance = default;
            try
            {
                PhysicsScene physics = scene.GetPhysicsScene();
                Physics.SyncTransforms();
                GameObject[] roots = scene.GetRootGameObjects();
                int ground = Kit.Ground, glass = Kit.GlassLayer;
                int solidMask = ~((1 << 6) | (1 << 7) | (1 << 8) | (1 << 9) | (1 << 10) | (1 << 2) | (1 << 5));
                int sightMask = solidMask & ~(1 << glass);

                // ------------------------------------------------ references
                report.AppendLine("References");
                int missingScripts = 0, missingMeshes = 0, missingMaterials = 0, missingPrefabs = 0;
                int objects = 0, renderers = 0, colliders = 0, triangles = 0;
                var mats = new HashSet<Material>();
                foreach (GameObject r in roots)
                    foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                    {
                        objects++;
                        missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                        if (PrefabUtility.IsPrefabAssetMissing(t.gameObject)) missingPrefabs++;
                        if (t.TryGetComponent(out MeshFilter mf) && mf.sharedMesh == null) missingMeshes++;
                        if (t.TryGetComponent(out MeshRenderer mr))
                        {
                            renderers++;
                            foreach (Material m in mr.sharedMaterials) { if (m == null) missingMaterials++; else mats.Add(m); }
                            if (mf != null && mf.sharedMesh != null && mr.enabled && t.GetComponent<TextMeshPro>() == null) triangles += mf.sharedMesh.triangles.Length / 3;
                        }
                        colliders += t.GetComponents<Collider>().Length;
                    }
                report.AppendLine($"  {objects} objects, {renderers} renderers, {colliders} colliders, {mats.Count} materials, {triangles:N0} triangles");
                if (missingScripts > 0) Problem($"{missingScripts} missing scripts");
                if (missingMeshes > 0) Problem($"{missingMeshes} missing meshes");
                if (missingMaterials > 0) Problem($"{missingMaterials} missing materials");
                if (missingPrefabs > 0) Problem($"{missingPrefabs} missing prefabs");

                Manager manager = Find<Manager>(roots);
                if (manager == null) Problem("no Manager");
                else
                {
                    if (manager.spawnPoints == null || manager.spawnPoints.Length == 0) Problem("the Manager has no spawn points");
                    else if (manager.spawnPoints.Any(s => s == null)) Problem("the Manager has empty spawn point slots");
                    if (manager.mapcam == null) Problem("the Manager has no map camera");
                    if (manager.GetComponent<Fusion.NetworkObject>() == null) Problem("the Manager has no NetworkObject");
                }

                // ------------------------------------------------ spawn points
                report.AppendLine("Spawn points");
                List<SpawnSide> sides = FindAll<SpawnSide>(roots);
                int teamA = sides.Count(s => s.team == 0 && !s.freeForAllOnly), teamB = sides.Count(s => s.team == 1 && !s.freeForAllOnly), ffa = sides.Count(s => s.freeForAllOnly);
                report.AppendLine($"  {teamA} team A, {teamB} team B, {ffa} free-for-all only");
                foreach (SpawnSide s in sides)
                {
                    Vector3 p = s.transform.position;
                    if (physics.OverlapCapsule(p + Vector3.down * 0.4f, p + Vector3.up * 0.5f, 0.48f, new Collider[1], solidMask, QueryTriggerInteraction.Ignore) > 0)
                        Problem($"{s.name} at {Fmt(p)} is inside something");
                    if (!physics.Raycast(p, Vector3.down, out RaycastHit floor, 1.6f, solidMask, QueryTriggerInteraction.Ignore))
                        Problem($"{s.name} at {Fmt(p)} has no ground under it");
                    else if (floor.distance > 1.2f) Problem($"{s.name} is {floor.distance - 1f:0.00} m above the ground");
                    if (!s.freeForAllOnly)
                    {
                        Rect area = s.team == 0 ? SpawnA : SpawnB;
                        if (!area.Contains(new Vector2(p.x, p.z))) Problem($"{s.name} at {Fmt(p)} is outside its team's spawn area");
                    }
                    if (p.x < 1f || p.x > Layout.SizeX - 1f || p.z < 1f || p.z > Layout.SizeZ - 1f) Problem($"{s.name} is outside the map");
                }
                // Spawns of one team should be about as far from the middle as the other's
                var objectives = FindAll<ObjectiveMarker>(roots).OrderBy(o => o.label).ToList();

                // ------------------------------------------------ walkable mesh
                report.AppendLine("Walkable mesh (player-sized agent)");
                // (CollectSources doesn't see preview scenes; the map's colliders are all boxes)
                var sources = new List<NavMeshBuildSource>();
                foreach (GameObject r in roots)
                    foreach (BoxCollider box in r.GetComponentsInChildren<BoxCollider>(true))
                    {
                        if (box.isTrigger || !box.enabled || ((1 << box.gameObject.layer) & solidMask) == 0) continue;
                        sources.Add(new NavMeshBuildSource
                        {
                            shape = NavMeshBuildSourceShape.Box,
                            size = box.size,
                            transform = box.transform.localToWorldMatrix * Matrix4x4.Translate(box.center),
                            area = 0,
                            component = box,
                        });
                    }
                NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
                settings.agentRadius = 0.5f;
                settings.agentHeight = PlayerHeight;
                settings.agentClimb = 0.3f;
                settings.agentSlope = 45f;
                settings.overrideVoxelSize = true;
                settings.voxelSize = 0.1f;
                var bounds = new Bounds(new Vector3(60f, 5f, 45f), new Vector3(124f, 24f, 94f));
                NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
                navInstance = NavMesh.AddNavMeshData(data);
                var filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas };
                NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
                report.AppendLine($"  {sources.Count} sources, {tri.indices.Length / 3} walkable triangles");
                if (tri.indices.Length == 0) Problem("the walkable mesh came out empty");

                var places = new List<(string name, Vector3 at)>();
                foreach (ObjectiveMarker o in objectives) places.Add(("objective " + o.label, o.transform.position));
                places.Add(("west lane, diner alley", new Vector3(8f, 0.1f, 47f)));
                places.Add(("west lane, backyards", new Vector3(14f, 0.1f, 16f)));
                places.Add(("west lane, depot yard", new Vector3(14f, 0.1f, 80f)));
                places.Add(("east lane, service alley", new Vector3(112.5f, 0.1f, 45f)));
                places.Add(("east lane, garage", new Vector3(100f, 0.2f, 21f)));
                places.Add(("east lane, utility yards", new Vector3(104f, 0.1f, 74f)));
                places.Add(("diner kitchen", new Vector3(20f, 0.2f, 49f)));
                places.Add(("workshop storage", new Vector3(92f, 0.2f, 42f)));
                places.Add(("workshop control room", new Vector3(95f, 4.6f, 50f)));
                places.Add(("workshop balcony", new Vector3(110f, 4.6f, 46f)));
                places.Add(("CD depot", new Vector3(20f, 0.2f, 69f)));
                foreach (SpawnSide s in sides) places.Add((s.name, s.transform.position + Vector3.down));

                var snapped = new List<(string name, Vector3 at)>();
                foreach (var (name, at) in places)
                {
                    if (NavMesh.SamplePosition(at, out NavMeshHit hit, 1.2f, filter)) snapped.Add((name, hit.position));
                    else Problem($"{name} at {Fmt(at)} isn't on the walkable mesh");
                }
                // Everything joins up with the first spawn
                var path = new NavMeshPath();
                Vector3 hub = snapped.FirstOrDefault(p => p.name.StartsWith("Spawn A")).at;
                int joined = 0;
                foreach (var (name, at) in snapped)
                {
                    if (NavMesh.CalculatePath(hub, at, filter, path) && path.status == NavMeshPathStatus.PathComplete) joined++;
                    else Problem($"no walking route from team A's spawn to {name}");
                }
                report.AppendLine($"  {joined} of {snapped.Count} places reachable on foot from team A's spawn");

                // Lanes join up without going through a spawn area: the walking route from one lane's
                // middle to another's stays out of both spawn areas
                (string, Vector3)[] lanes =
                {
                    ("west", new Vector3(8f, 0.1f, 47f)), ("centre", new Vector3(60f, 0.2f, 45f)), ("east", new Vector3(112.5f, 0.1f, 45f)),
                };
                for (int i = 0; i < lanes.Length; i++)
                    for (int j = i + 1; j < lanes.Length; j++)
                    {
                        NavMesh.SamplePosition(lanes[i].Item2, out NavMeshHit a, 2f, filter);
                        NavMesh.SamplePosition(lanes[j].Item2, out NavMeshHit b, 2f, filter);
                        if (!NavMesh.CalculatePath(a.position, b.position, filter, path) || path.status != NavMeshPathStatus.PathComplete)
                        {
                            Problem($"no route between the {lanes[i].Item1} and {lanes[j].Item1} lanes");
                            continue;
                        }
                        float length = 0f, maxZ = 0f, minZ = 999f;
                        for (int k = 1; k < path.corners.Length; k++) length += Vector3.Distance(path.corners[k - 1], path.corners[k]);
                        foreach (Vector3 c in path.corners) { maxZ = Mathf.Max(maxZ, c.z); minZ = Mathf.Min(minZ, c.z); }
                        bool throughSpawn = minZ < SpawnA.yMax || maxZ > SpawnB.yMin;
                        report.AppendLine($"  {lanes[i].Item1} - {lanes[j].Item1}: {length:0} m on foot, z {minZ:0}-{maxZ:0}{(throughSpawn ? " (through a spawn!)" : "")}");
                        if (throughSpawn) Problem($"the quickest route between the {lanes[i].Item1} and {lanes[j].Item1} lanes goes through a spawn");
                    }
                // Fairness: walking distance from each team's spawns to each objective
                foreach (ObjectiveMarker o in objectives)
                {
                    float da = MeanPath(sides.Where(s => s.team == 0 && !s.freeForAllOnly), o.transform.position, filter);
                    float db = MeanPath(sides.Where(s => s.team == 1 && !s.freeForAllOnly), o.transform.position, filter);
                    report.AppendLine($"  objective {o.label}: {da:0} m on foot from team A's spawns, {db:0} m from team B's");
                }

                // ------------------------------------------------ spawn to spawn sight
                report.AppendLine("Sight between the spawn areas");
                int lines = 0, clear = 0;
                var examples = new List<string>();
                foreach (float eyeA in new[] { 1.7f, 2.7f })
                    for (float ax = SpawnA.xMin; ax <= SpawnA.xMax + 0.01f; ax += 2.5f)
                        for (float az = SpawnA.yMin; az <= SpawnA.yMax + 0.01f; az += 2.75f)
                            for (float bx = SpawnB.xMin; bx <= SpawnB.xMax + 0.01f; bx += 2.5f)
                                for (float bz = SpawnB.yMin; bz <= SpawnB.yMax + 0.01f; bz += 2.75f)
                                {
                                    Vector3 a = new Vector3(ax, eyeA, az), b = new Vector3(bx, eyeA, bz);
                                    lines++;
                                    if (!physics.Raycast(a, b - a, (b - a).magnitude, sightMask, QueryTriggerInteraction.Ignore))
                                    {
                                        clear++;
                                        if (examples.Count < 12) examples.Add($"{Fmt(a)} - {Fmt(b)}");
                                    }
                                }
                report.AppendLine($"  {clear} of {lines} lines clear (eyes 1.7 and 2.7 m up)");
                if (clear > 0) Problem($"the spawn areas can see each other, e.g. {string.Join("; ", examples)}");

                // ------------------------------------------------ out-of-reach surfaces
                report.AppendLine("Reach (jumping 4.2 m up, 10 m/s across)");
                Reach(physics, roots, solidMask, filter, hub, report, Problem);

                // ------------------------------------------------ boundary
                report.AppendLine("Boundary");
                var walls = new List<BoxCollider>();
                foreach (GameObject r in roots)
                {
                    Transform b = r.transform.Find("Bounds");
                    if (b != null) walls.AddRange(b.GetComponentsInChildren<BoxCollider>());
                }
                if (walls.Count != 4) Problem($"{walls.Count} boundary walls (expected 4)");
                foreach (BoxCollider w in walls) if (w.bounds.max.y < 15f) Problem($"{w.name} is only {w.bounds.max.y:0.0} m tall");
                report.AppendLine($"  {walls.Count} walls, {walls.Select(w => w.bounds.max.y).DefaultIfEmpty(0).Min():0} m tall");
            }
            finally
            {
                if (navInstance.valid) NavMesh.RemoveNavMeshData(navInstance);
                EditorSceneManager.ClosePreviewScene(scene);
            }

            string text = $"Test Site 07 validation, {DateTime.Now:yyyy-MM-dd HH:mm}: {(problems == 0 ? "no problems" : problems + " problems")}\n{report}";
            File.WriteAllText(Path.Combine(OutDir, "validation.txt"), text);
            if (problems == 0) Debug.Log(text);
            else Debug.LogWarning(text);
            return text;
        }

        private static float MeanPath(IEnumerable<SpawnSide> spawns, Vector3 to, NavMeshQueryFilter filter)
        {
            var path = new NavMeshPath();
            float total = 0f;
            int n = 0;
            NavMesh.SamplePosition(to, out NavMeshHit end, 2f, filter);
            foreach (SpawnSide s in spawns)
            {
                if (!NavMesh.SamplePosition(s.transform.position + Vector3.down, out NavMeshHit start, 2f, filter)) continue;
                if (!NavMesh.CalculatePath(start.position, end.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                for (int k = 1; k < path.corners.Length; k++) total += Vector3.Distance(path.corners[k - 1], path.corners[k]);
                n++;
            }
            return n > 0 ? total / n : float.NaN;
        }

        private class Top
        {
            public string name;
            public Rect rect;
            public float y;
            public float maxFeet;
            public bool reached;
            public Top from;
        }

        // Every flat top a player could stand on, and which of them can be reached by walking and
        // jumping. Anything above 3 m that can be reached outside the workshop is reported.
        private static void Reach(PhysicsScene physics, GameObject[] roots, int solidMask, NavMeshQueryFilter filter, Vector3 hub, StringBuilder report, Action<string> problem)
        {
            var tops = new List<Top>();
            var hits = new Collider[8];
            foreach (GameObject r in roots)
                foreach (BoxCollider box in r.GetComponentsInChildren<BoxCollider>(true))
                {
                    if (box.isTrigger || ((1 << box.gameObject.layer) & solidMask) == 0) continue;
                    if (Vector3.Dot(box.transform.up, Vector3.up) < 0.995f) continue;
                    Bounds b = box.bounds;
                    if (b.size.x < 0.1f || b.size.z < 0.1f || b.max.y < 0.5f) continue;
                    if (b.center.x < -2f || b.center.x > Layout.SizeX + 2f || b.center.z < -2f || b.center.z > Layout.SizeZ + 2f) continue;
                    if (box.transform.parent != null && box.transform.parent.name == "Bounds") continue;
                    // Covered: something sits right on top of it
                    Vector3 probe = new Vector3(b.center.x, b.max.y + 1.05f, b.center.z);
                    Vector3 half = new Vector3(Mathf.Min(b.extents.x, 0.3f), 0.95f, Mathf.Min(b.extents.z, 0.3f));
                    int n = physics.OverlapBox(probe, half, hits, Quaternion.identity, solidMask, QueryTriggerInteraction.Ignore);
                    bool covered = false;
                    for (int i = 0; i < n; i++) if (hits[i] != box) covered = true;
                    if (covered) continue;
                    // The highest the feet get jumping from here: 4.2 m, or less under a ceiling
                    float maxFeet = b.max.y + JumpHeight;
                    if (physics.Raycast(new Vector3(b.center.x, b.max.y + 0.05f, b.center.z), Vector3.up, out RaycastHit ceiling, JumpHeight + PlayerHeight, solidMask, QueryTriggerInteraction.Ignore))
                        maxFeet = Mathf.Min(maxFeet, ceiling.point.y - PlayerHeight);
                    tops.Add(new Top
                    {
                        name = PathOf(box.transform),
                        rect = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z),
                        y = b.max.y,
                        maxFeet = maxFeet,
                    });
                }

            // Reached on foot: on the walkable mesh (ground, floors, stairs) and joined to the spawn
            var walk = new NavMeshPath();
            foreach (Top t in tops)
            {
                Vector3 c = new Vector3(t.rect.center.x, t.y, t.rect.center.y);
                if (NavMesh.SamplePosition(c, out NavMeshHit hit, 0.6f, filter) && Mathf.Abs(hit.position.y - t.y) < 0.35f && NavMesh.CalculatePath(hub, hit.position, filter, walk) && walk.status == NavMeshPathStatus.PathComplete) t.reached = true;
            }
            // From the ground (anywhere: it all joins up), a jump gets the feet 4.2 m up; from any
            // reached top, 4.2 m above it (less under a ceiling) and 10.5 m across
            bool changed = true;
            int rounds = 0;
            while (changed && rounds++ < 12)
            {
                changed = false;
                foreach (Top t in tops)
                {
                    if (t.reached) continue;
                    if (t.y <= JumpHeight - 0.05f) { t.reached = true; changed = true; continue; }
                    foreach (Top s in tops)
                    {
                        if (!s.reached || s == t) continue;
                        if (t.y > s.maxFeet - 0.05f) continue;
                        if (Gap(s.rect, t.rect) > AirReach(t.y - s.y)) continue;
                        t.reached = true;
                        t.from = s;
                        changed = true;
                        break;
                    }
                }
            }

            int high = 0;
            foreach (Top t in tops.Where(t => t.reached).OrderByDescending(t => t.y))
            {
                if (t.y <= 3.0f) continue;
                bool upstairs = UpstairsArea.Overlaps(t.rect) && t.y < 7f;
                if (upstairs) continue;
                high++;
                var chain = new StringBuilder();
                for (Top s = t.from; s != null; s = s.from) chain.Append($" <- {s.name} ({s.y:0.00} m at {s.rect.center.x:0},{s.rect.center.y:0})");
                problem($"{t.name} ({t.y:0.00} m, at {t.rect.center.x:0},{t.rect.center.y:0}) can be reached{chain}");
            }
            foreach (Top t in tops.Where(t => t.reached && (t.rect.xMax < 0f || t.rect.xMin > Layout.SizeX || t.rect.yMax < 0f || t.rect.yMin > Layout.SizeZ)))
                problem($"{t.name} is outside the map and can be reached");
            float highestUp = tops.Where(t => t.reached && UpstairsArea.Overlaps(t.rect)).Select(t => t.y).DefaultIfEmpty(0f).Max();
            float highestOut = tops.Where(t => t.reached && !UpstairsArea.Overlaps(t.rect)).Select(t => t.y).DefaultIfEmpty(0f).Max();
            report.AppendLine($"  {tops.Count} standable tops; highest reachable: {highestOut:0.00} m outside the workshop, {highestUp:0.00} m at the workshop; {high} too high");
        }

        private static float Gap(Rect a, Rect b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float dz = Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ---------------------------------------------------------------- pictures

        [MenuItem("Capshots/Test Site 07/Capture Views")]
        public static void CaptureViews()
        {
            Directory.CreateDirectory(OutDir);
            Scene scene = OpenPreview(Builder.ScenePath);
            var temp = new List<UnityEngine.Object>();
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                var camObject = new GameObject("Capture Camera");
                SceneManager.MoveGameObjectToScene(camObject, scene);
                Camera cam = camObject.AddComponent<Camera>();
                cam.scene = scene;
                cam.enabled = false;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 1500f;
                cam.clearFlags = CameraClearFlags.Skybox;
                temp.Add(camObject);

                // Plan view with the spawns, objectives and lanes marked
                var overlay = new GameObject("Overlay");
                SceneManager.MoveGameObjectToScene(overlay, scene);
                Material Unlit(Color c)
                {
                    var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    m.SetColor("_BaseColor", c);
                    temp.Add(m);
                    return m;
                }
                Material blue = Unlit(new Color(0.25f, 0.55f, 1f)), red = Unlit(new Color(1f, 0.32f, 0.28f)), grey = Unlit(new Color(0.85f, 0.85f, 0.85f)), gold = Unlit(new Color(1f, 0.82f, 0.2f));
                void Disc(Vector3 at, float radius, Material m, float height = 0.2f)
                {
                    GameObject d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    UnityEngine.Object.DestroyImmediate(d.GetComponent<Collider>());
                    d.transform.SetParent(overlay.transform, false);
                    d.transform.position = new Vector3(at.x, 30f, at.z);
                    d.transform.localScale = new Vector3(radius * 2f, height, radius * 2f);
                    d.GetComponent<MeshRenderer>().sharedMaterial = m;
                }
                void Label(string text, Vector3 at, float size, Color color)
                {
                    var go = new GameObject("Label " + text);
                    go.transform.SetParent(overlay.transform, false);
                    go.transform.position = new Vector3(at.x, 31f, at.z);
                    go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    TextMeshPro tmp = go.AddComponent<TextMeshPro>();
                    tmp.text = text;
                    tmp.fontSize = size * 12f;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = color;
                    tmp.rectTransform.sizeDelta = new Vector2(60f, size * 2f);
                }
                foreach (SpawnSide s in FindAll<SpawnSide>(roots))
                    Disc(s.transform.position, 0.9f, s.freeForAllOnly ? grey : s.team == 0 ? blue : red);
                foreach (ObjectiveMarker o in FindAll<ObjectiveMarker>(roots))
                {
                    Disc(o.transform.position, o.radius, gold, 0.05f);
                    Label(o.label, o.transform.position, 3.2f, new Color(0.12f, 0.12f, 0.14f));
                }
                Label("WEST LANE", new Vector3(22f, 0, 58f), 2.2f, Color.white);
                Label("CENTRAL LANE", new Vector3(60f, 0, 31.5f), 2.2f, Color.white);
                Label("EAST LANE", new Vector3(100f, 0, 66f), 2.2f, Color.white);
                Label("TEAM A SPAWN", new Vector3(60f, 0, 1.6f), 1.8f, new Color(0.6f, 0.8f, 1f));
                Label("TEAM B SPAWN", new Vector3(60f, 0, 88.4f), 1.8f, new Color(1f, 0.7f, 0.66f));

                cam.orthographic = true;
                cam.orthographicSize = 47f;
                camObject.transform.SetPositionAndRotation(new Vector3(60f, 120f, 45f), Quaternion.Euler(90f, 0f, 0f));
                Shoot(cam, Path.Combine(OutDir, "overview.png"), 1600, 1200);
                overlay.SetActive(false);
                Shoot(cam, Path.Combine(OutDir, "plan.png"), 1600, 1200);
                cam.orthographic = false;

                foreach (var (name, from, to, fov) in Views)
                {
                    camObject.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from));
                    cam.fieldOfView = fov;
                    Shoot(cam, Path.Combine(OutDir, name + ".png"), 1280, 720);
                }
                Debug.Log("Test Site 07: pictures in " + Path.GetFullPath(OutDir));
            }
            finally
            {
                foreach (UnityEngine.Object o in temp) if (o != null) UnityEngine.Object.DestroyImmediate(o);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // Named views: from, look at, field of view
        private static readonly (string, Vector3, Vector3, float)[] Views =
        {
            ("hero", new Vector3(60f, 42f, -26f), new Vector3(60f, 0f, 44f), 55f),
            ("hero-north", new Vector3(30f, 38f, 112f), new Vector3(62f, 0f, 46f), 55f),
            ("spawn-a", new Vector3(60f, 1.7f, 7f), new Vector3(60f, 1.7f, 40f), 75f),
            ("courtyard", new Vector3(44f, 1.8f, 33.5f), new Vector3(70f, 1f, 52f), 75f),
            ("courtyard-high", new Vector3(36f, 14f, 30f), new Vector3(62f, 0f, 48f), 60f),
            ("diner-front", new Vector3(20f, 2.2f, 21f), new Vector3(23f, 3f, 36f), 70f),
            ("diner-inside", new Vector3(14f, 1.7f, 35f), new Vector3(30f, 1.2f, 46f), 80f),
            ("west-yards", new Vector3(4f, 3f, 3f), new Vector3(24f, 0.5f, 22f), 70f),
            ("workshop", new Vector3(118f, 6f, 68f), new Vector3(96f, 4f, 50f), 65f),
            ("workshop-up", new Vector3(88f, 6.2f, 41f), new Vector3(104f, 5f, 55f), 80f),
            ("workshop-ground", new Vector3(88f, 1.7f, 55f), new Vector3(104f, 1.5f, 42f), 80f),
            ("east-alley", new Vector3(113f, 1.7f, 28f), new Vector3(113f, 2f, 70f), 70f),
            ("garage", new Vector3(84f, 5f, 36f), new Vector3(100f, 2f, 21f), 65f),
            ("depot", new Vector3(36f, 5f, 54f), new Vector3(20f, 2f, 69f), 65f),
        };

        private static void Shoot(Camera cam, string file, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, GraphicsFormat.R8G8B8A8_SRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            try
            {
                cam.Render();
                cam.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(file, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        // ---------------------------------------------------------------- helpers

        private static Scene OpenPreview(string path)
        {
            MethodInfo open = typeof(EditorSceneManager).GetMethod("OpenPreviewScene", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (open == null) throw new Exception("EditorSceneManager.OpenPreviewScene not available");
            return (Scene)open.Invoke(null, new object[] { path });
        }

        private static T Find<T>(GameObject[] roots) where T : Component
        {
            foreach (GameObject r in roots)
            {
                T found = r.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static List<T> FindAll<T>(GameObject[] roots) where T : Component
        {
            var list = new List<T>();
            foreach (GameObject r in roots) list.AddRange(r.GetComponentsInChildren<T>(true));
            return list;
        }

        private static string PathOf(Transform t)
        {
            string s = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++)
            {
                t = t.parent;
                s = t.name + "/" + s;
            }
            return s;
        }

        private static string Fmt(Vector3 v) => $"({v.x:0.0}, {v.y:0.0}, {v.z:0.0})";
    }
}

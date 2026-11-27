using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The walkable areas of the map, for the bots to find their way around. Built from the map's
// colliders when a match with bots starts (on the host only, the only machine running bots), so no
// map scene needs a baked navigation mesh. The build runs in the background over a few frames;
// until it's ready, bots just head straight for their goal.
public static class BotNavigation
{
    // The map's agent. The builder rounds the radius up to whole voxels and also rounds walls out to
    // the voxels they touch, so a radius the size of the player's capsule (0.5 m) took 0.6 m off each
    // side of a 1.5 m doorway, and up to another 0.2 m for the posts: about half the office doors
    // closed up on the mesh, and bots walked to the nearest spot outside the room and stood against
    // its wall. Two voxels clears them all and still keeps paths 0.4-0.6 m off the walls; the
    // capsule's last few centimetres just slide along them.
    private const float AgentRadius = 0.39f;
    private const float AgentHeight = 2f;
    // The player capsule's step (CharacterController.stepOffset, 0.3 m): anything higher is a ledge a
    // bot can't walk up, so the mesh mustn't join it
    private const float AgentClimb = 0.3f;
    private const float AgentSlope = 45f;
    private const float VoxelSize = 0.2f;
    // How far beyond the spawn points the map is searched for walkable ground
    private const float MapMargin = 120f;

    private static NavMeshDataInstance instance;
    private static AsyncOperation building;
    private static string builtScene;
    private static int agentTypeId;

    // What the mesh was built from, kept so it can be updated when scenery goes away (glass shot out)
    private static NavMeshData data;
    private static NavMeshBuildSettings builtSettings;
    private static Bounds builtBounds;
    private static List<NavMeshBuildSource> builtSources;
    private static readonly HashSet<Component> removed = new HashSet<Component>();
    private static AsyncOperation updating;

    // Ready once the first build is done. An update afterwards doesn't stop bots using the mesh: the
    // tiles it rebuilds swap in when they're finished.
    public static bool Ready => instance.valid && (building == null || building.isDone);

    // Starts building the map's navigation mesh, if it isn't built (or building) already
    public static void Build(Transform[] spawnPoints)
    {
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (instance.valid && builtScene == scene) return;
        Clear();

        Bounds bounds = MapBounds(spawnPoints);

        // Everything solid counts, apart from the players themselves, guns and hitboxes. Glass walls
        // count too: bots walk around them, the way people have to until they're shot out.
        int layers = ~((1 << Layers.LocalPlayer) | (1 << Layers.RemotePlayer) | (1 << Layers.Gun) |
                       (1 << Layers.LocalGun) | (1 << Layers.Hitbox) | (1 << 2) | (1 << 5));
        List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds, layers, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
        // Triggers (pickups, kill zones) aren't solid
        sources.RemoveAll(s => s.component is Collider c && c.isTrigger);
        ReplaceUnreadableMeshes(sources);

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = AgentRadius;
        settings.agentHeight = AgentHeight;
        settings.agentClimb = AgentClimb;
        settings.agentSlope = AgentSlope;
        settings.overrideVoxelSize = true;
        settings.voxelSize = VoxelSize;
        agentTypeId = settings.agentTypeID;

        data = new NavMeshData(settings.agentTypeID);
        instance = NavMesh.AddNavMeshData(data);
        building = NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, sources, bounds);
        builtScene = scene;
        builtSettings = settings;
        builtBounds = bounds;
        builtSources = sources;
    }

    // Something solid is gone (a glass pane shot out): the mesh is updated to walk through where it
    // was. Batched: Tick starts one update for everything removed since the last one.
    public static void RemoveObstacle(Component solid)
    {
        if (builtSources == null || solid == null) return;
        removed.Add(solid);
    }

    // Host, once a tick: starts an update for removed scenery, when no build or update is running.
    // Only the tiles whose sources changed are rebuilt, so it's quick.
    public static void Tick()
    {
        if (removed.Count == 0 || !instance.valid || data == null) return;
        if ((building != null && !building.isDone) || (updating != null && !updating.isDone)) return;
        builtSources.RemoveAll(s => s.component != null && removed.Contains(s.component));
        removed.Clear();
        updating = NavMeshBuilder.UpdateNavMeshDataAsync(data, builtSettings, builtSources, builtBounds);
    }

    // Outside the editor, the builder can't read a mesh collider's mesh unless the model was imported
    // with Read/Write enabled, and silently leaves it out. Those become boxes the size of the mesh
    // instead, so they still count as solid (rougher, but better than walking through them).
    private static void ReplaceUnreadableMeshes(List<NavMeshBuildSource> sources)
    {
        for (int i = 0; i < sources.Count; i++)
        {
            if (!(sources[i].component is MeshCollider mc) || mc.sharedMesh == null || mc.sharedMesh.isReadable) continue;

            Bounds local = mc.sharedMesh.bounds;
            NavMeshBuildSource box = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = mc.transform.localToWorldMatrix * Matrix4x4.Translate(local.center),
                size = local.size,
                area = sources[i].area,
            };
            sources[i] = box;
        }
    }

    public static void Clear()
    {
        if (instance.valid) instance.Remove();
        instance = default;
        building = null;
        updating = null;
        builtScene = null;
        data = null;
        builtSources = null;
        removed.Clear();
    }

    // The spawn points' area, widened by a margin and cut down to the solid things actually there
    private static Bounds MapBounds(Transform[] spawnPoints)
    {
        Bounds area = new Bounds(Vector3.zero, Vector3.one * MapMargin * 2f);
        bool first = true;
        if (spawnPoints != null)
        {
            foreach (Transform point in spawnPoints)
            {
                if (point == null) continue;
                if (first) { area = new Bounds(point.position, Vector3.zero); first = false; }
                else area.Encapsulate(point.position);
            }
        }
        area.Expand(new Vector3(MapMargin * 2f, 80f, MapMargin * 2f));

        Bounds solid = new Bounds(area.center, Vector3.zero);
        bool any = false;
        foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (c.isTrigger || !c.enabled || !area.Intersects(c.bounds)) continue;
            if (!any) { solid = c.bounds; any = true; }
            else solid.Encapsulate(c.bounds);
        }
        if (!any) return area;

        // Only the part of the solid things inside the area around the spawn points
        Vector3 min = Vector3.Max(solid.min, area.min);
        Vector3 max = Vector3.Min(solid.max, area.max);
        Bounds result = new Bounds();
        result.SetMinMax(min - Vector3.one, max + Vector3.one);
        return result;
    }

    // The point on the walkable area nearest to a position, if there is one close by
    public static bool Snap(Vector3 position, float maxDistance, out Vector3 snapped)
    {
        snapped = position;
        if (!Ready) return false;
        NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, filter)) return false;
        snapped = hit.position;
        return true;
    }

    // A walkable path between two points (each snapped onto the walkable area first), as corners.
    // False if there's no navigation mesh yet or no way there. complete is false when there's no way
    // all the way there and the path only leads as close as the walkable area gets.
    public static bool FindPath(Vector3 from, Vector3 to, NavMeshPath path, out bool complete)
    {
        complete = false;
        if (!Ready) return false;
        if (!Snap(from, 3f, out Vector3 start)) return false;
        if (!Snap(to, 4f, out Vector3 end)) return false;

        NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
        if (!NavMesh.CalculatePath(start, end, filter, path)) return false;
        if (path.status == NavMeshPathStatus.PathInvalid || path.corners.Length == 0) return false;
        complete = path.status == NavMeshPathStatus.PathComplete;
        return true;
    }

    // Whether a bot can walk straight from one point to another without leaving the walkable area
    // (used to check a sidestep doesn't go off a ledge). True when there's no navigation mesh yet.
    public static bool CanWalkStraight(Vector3 from, Vector3 to)
    {
        if (!Ready) return true;
        if (!Snap(from, 3f, out Vector3 start)) return true;
        NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
        return !NavMesh.Raycast(start, to, out NavMeshHit _, filter);
    }

    // A random walkable point, for a bot with nothing better to do
    public static bool RandomPoint(Vector3 near, float radius, out Vector3 point)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            if (Snap(near + new Vector3(offset.x, 0f, offset.y), 6f, out point)) return true;
        }
        point = near;
        return false;
    }
}

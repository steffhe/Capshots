using System.Collections.Generic;
using UnityEngine;

// Directs the map camera (shown while you wait to respawn and over the match results). Instead of one
// fixed view it works like a sports broadcast: it scouts the map once for good vantage points, holds
// each shot for a few seconds while drifting slowly (a push in, a pan or a crane move), keeps the
// living players in frame and cuts to whichever angle shows the most of the action.
//
// Vantage points come from the map itself, so every map gets them without hand placement:
//  - Interest points: the spawn points, plus floor points between nearby ones, which is where
//    players run around.
//  - From each interest point the scout rises until it reaches a ceiling or a height that suits
//    the map's size, then moves outward until it nears a wall. Outdoors that gives high three-quarter
//    views over the map; indoors (Office) it gives high corner views just under the ceiling.
//  - Each vantage point is scored by how many interest points it sees inside one frame, and the
//    best ones that aren't too close together are kept. The camera's position in the scene is kept
//    too, as a hand-picked shot.
// Purely local presentation; nothing here is networked.
public class MapCamera : MonoBehaviour
{
    // How long a shot lasts before cutting to another, in seconds
    private const float ShotMin = 7f, ShotMax = 10f;
    // A shot showing nobody gives way this soon to one that shows someone
    private const float EmptyShotMin = 2.5f;
    // How many vantage points to keep
    private const int MaxShots = 14;
    // How many interest points to scout from (it's a few linecasts each, squared)
    private const int MaxInterest = 40;
    // Half the view cone counted as "in frame" when scoring a shot, in degrees
    private const float FrameAngle = 28f;
    // Field of view range: tighter when players are bunched up, wider when spread out
    private const float FovMin = 34f, FovMax = 60f, FovIdle = 52f;
    // Seconds for the aim and the field of view to follow the action
    private const float AimSmooth = 1.1f, FovSmooth = 1.6f;
    // Players are looked at this high above their feet
    private const float BodyHeight = 1f;
    // How far the camera keeps from walls and ceilings
    private const float Clearance = 0.5f;

    private class Shot
    {
        public Vector3 position;
        // Where the shot looks when nobody is in sight
        public Vector3 look;
        // How much of the map it shows, 0 to 1
        public float quality;
        public bool authored;
        public float lastUsed = -100f;
    }

    private readonly List<Shot> shots = new List<Shot>();
    private readonly List<Vector3> interest = new List<Vector3>();
    private readonly List<Vector3> seenPlayers = new List<Vector3>();
    private Transform[] spawnPoints;
    private Camera cam;
    private bool planned;
    // The view placed in the scene (or by the Office map), before this camera moved it
    private bool hasAuthored;
    private Vector3 authoredPosition;
    private Quaternion authoredRotation;

    // Terrain and walls the camera can't go through (glass included), and what blocks its view
    // (glass left out: you can see through it). Players never block anything.
    private int solidMask, sightMask;
    private Vector3 mapCenter;
    private float mapRadius = 20f;

    // The shot playing now
    private Shot current;
    private Vector3 moveFrom, moveTo;
    private float shotAge, shotLength;
    private Vector3 aim, aimVelocity;
    private float fovVelocity;
    private float nextCheck;

    // Called by the Manager once the map is in place (the Office builds itself in Awake)
    public void Init(Transform[] spawns)
    {
        Setup();
        if (!hasAuthored)
        {
            hasAuthored = true;
            authoredPosition = transform.position;
            authoredRotation = transform.rotation;
        }
        spawnPoints = spawns;
        current = null;
        // Scouting takes a moment, so it's done now behind the loading screen
        Plan();
    }

    private void Awake()
    {
        Setup();
    }

    // Also from Init: a component added to an inactive object doesn't get Awake until it's shown
    private void Setup()
    {
        cam = GetComponent<Camera>();
        int players = (1 << Layers.LocalPlayer) | (1 << Layers.RemotePlayer) | (1 << Layers.Hitbox)
                    | (1 << Layers.Gun) | (1 << Layers.LocalGun);
        solidMask = ~players;
        sightMask = ~(players | (1 << Layers.Glass));
    }

    private void OnEnable()
    {
        // Each time the map view comes back, start on a fresh shot
        current = null;
    }

    private void LateUpdate()
    {
        // Until the Manager hands over the spawn points, the scene's view stays as it is
        if (!planned || shots.Count == 0) return;

        // The results screen can run with time frozen, so the camera keeps its own time
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (current == null) Cut(true);

        shotAge += dt;
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 0.5f;
            if (shotAge >= shotLength || ShouldCutEarly()) Cut(false);
        }

        // The slow move: eased from start to end over the whole shot
        float t = Mathf.Clamp01(shotAge / shotLength);
        transform.position = Vector3.Lerp(moveFrom, moveTo, t * t * (3f - 2f * t));

        // Aim at the players in sight, held a little towards the shot's own subject so the
        // framing doesn't swing around wildly
        VisiblePlayers(transform.position, seenPlayers);
        Vector3 target = current.look;
        float fovTarget = FovIdle;
        if (seenPlayers.Count > 0)
        {
            Vector3 centroid = Centroid(seenPlayers);
            target = Vector3.Lerp(current.look, centroid, 0.8f);
            fovTarget = FitFov(transform.position, target, seenPlayers);
        }
        aim = Vector3.SmoothDamp(aim, target, ref aimVelocity, AimSmooth, Mathf.Infinity, dt);
        Vector3 forward = aim - transform.position;
        if (forward.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        if (cam != null) cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, fovTarget, ref fovVelocity, FovSmooth, Mathf.Infinity, dt);
    }

    #region Directing

    // Moves on to the shot showing the most right now. first: the map view just appeared.
    private void Cut(bool first)
    {
        Shot best = null;
        float bestScore = float.MinValue;
        foreach (Shot shot in shots)
        {
            if (shot == current) continue;
            float score = Score(shot);
            // Don't keep going back to the same few angles
            float since = Time.unscaledTime - shot.lastUsed;
            if (since < 30f) score -= 2.5f * (1f - since / 30f);
            // The hand-picked view opens the show when nothing better is going on
            if (first && shot.authored) score += 1.5f;
            if (score > bestScore)
            {
                bestScore = score;
                best = shot;
            }
        }
        if (best == null) best = current ?? shots[0];
        Begin(best);
    }

    // How good a shot is right now: players it would show, then how much of the map it shows
    private float Score(Shot shot)
    {
        VisiblePlayers(shot.position, seenPlayers);
        int inFrame = 0;
        if (seenPlayers.Count > 0)
        {
            Vector3 dir = Centroid(seenPlayers) - shot.position;
            foreach (Vector3 p in seenPlayers)
            {
                if (Vector3.Angle(dir, p - shot.position) <= FrameAngle) inFrame++;
            }
        }
        // The first player seen counts most, more players add less each
        float players = inFrame == 0 ? 0f : 3f + Mathf.Sqrt(inFrame - 1) * 2f;
        return players + shot.quality * 2f + Random.Range(0f, 0.6f);
    }

    // Cuts early when the shot shows nobody but another one would
    private bool ShouldCutEarly()
    {
        if (shotAge < EmptyShotMin) return false;
        VisiblePlayers(transform.position, seenPlayers);
        if (seenPlayers.Count > 0) return false;
        foreach (Shot shot in shots)
        {
            if (shot == current) continue;
            VisiblePlayers(shot.position, seenPlayers);
            if (seenPlayers.Count > 0) return true;
        }
        return false;
    }

    private void Begin(Shot shot)
    {
        current = shot;
        shot.lastUsed = Time.unscaledTime;
        shotAge = 0f;
        shotLength = Random.Range(ShotMin, ShotMax);

        // Pick a slow move suited to the distance: truck sideways, push in or crane up
        Vector3 toLook = shot.look - shot.position;
        float distance = toLook.magnitude;
        Vector3 flat = Vector3.ProjectOnPlane(toLook, Vector3.up).normalized;
        if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, flat);
        Vector3 move;
        switch (Random.Range(0, 3))
        {
            case 0: move = side * (Random.value < 0.5f ? 1f : -1f); break;
            case 1: move = toLook.normalized; break;
            default: move = (Vector3.up * 0.6f - flat * 0.4f).normalized; break;
        }
        float length = Mathf.Clamp(distance * 0.12f, 0.8f, 7f);

        // Half the move either side of the vantage point, kept clear of walls
        moveFrom = Reach(shot.position, -move, length * 0.5f);
        moveTo = Reach(shot.position, move, length * 0.5f);
        transform.position = moveFrom;

        // Snap the aim straight onto the action: this is a cut, not a pan
        VisiblePlayers(moveFrom, seenPlayers);
        aim = seenPlayers.Count > 0 ? Vector3.Lerp(shot.look, Centroid(seenPlayers), 0.8f) : shot.look;
        aimVelocity = Vector3.zero;
        fovVelocity = 0f;
        if ((aim - moveFrom).sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(aim - moveFrom, Vector3.up);
        if (cam != null) cam.fieldOfView = seenPlayers.Count > 0 ? FitFov(moveFrom, aim, seenPlayers) : FovIdle;
    }

    // A field of view that fits the given points around the aim, with some room to spare
    private float FitFov(Vector3 from, Vector3 target, List<Vector3> points)
    {
        Vector3 dir = target - from;
        float widest = 0f;
        foreach (Vector3 p in points) widest = Mathf.Max(widest, Vector3.Angle(dir, p - from));
        // The frame is wider than tall: the angle off to the side needs less vertical view
        float aspect = cam != null ? Mathf.Max(cam.aspect, 1f) : 1.6f;
        return Mathf.Clamp((widest + 6f) * 2f / Mathf.Sqrt(aspect), FovMin, FovMax);
    }

    // Living players the given point can see (as their middles)
    private void VisiblePlayers(Vector3 from, List<Vector3> result)
    {
        result.Clear();
        float range = mapRadius * 2.5f;
        foreach (Player p in Player.All)
        {
            if (p == null || p.Object == null || !p.Object.IsValid || p.IsDead) continue;
            Vector3 body = p.transform.position + Vector3.up * BodyHeight;
            if ((body - from).sqrMagnitude > range * range) continue;
            if (Physics.Linecast(from, body, sightMask, QueryTriggerInteraction.Ignore)) continue;
            result.Add(body);
        }
    }

    #endregion

    #region Scouting

    private void Plan()
    {
        planned = true;
        shots.Clear();
        interest.Clear();
        Physics.SyncTransforms();

        // The hand-picked view from the scene
        Vector3 authoredForward = authoredRotation * Vector3.forward;
        Shot authored = new Shot { position = authoredPosition, authored = true };
        authored.look = authored.position + authoredForward * 25f;

        FindInterestPoints();
        if (interest.Count == 0)
        {
            shots.Add(authored);
            return;
        }

        // The authored view looks where it looks; score it by what it sees that way
        authored.quality = Quality(authored.position, out _);
        authored.look = AimAlong(authored.position, authoredForward, authored.look);

        List<Shot> candidates = new List<Shot>();
        float rise = Mathf.Clamp(mapRadius * 0.45f, 6f, 32f);
        float reach = Mathf.Clamp(mapRadius * 0.5f, 4f, 40f);
        foreach (Vector3 point in interest)
        {
            for (int i = 0; i < 8; i++)
            {
                float yaw = i * 45f + 22.5f;
                Vector3 outward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                if (Scout(point, outward, rise, reach, out Vector3 position))
                {
                    float quality = Quality(position, out Vector3 look);
                    if (quality > 0f) candidates.Add(new Shot { position = position, look = look, quality = quality });
                }
            }
        }

        // Keep the best ones, spread around the map
        candidates.Sort((a, b) => b.quality.CompareTo(a.quality));
        float spacing = Mathf.Max(4f, mapRadius * 0.3f);
        foreach (Shot candidate in candidates)
        {
            if (shots.Count >= MaxShots) break;
            bool crowded = false;
            foreach (Shot kept in shots)
            {
                if ((kept.position - candidate.position).sqrMagnitude < spacing * spacing)
                {
                    crowded = true;
                    break;
                }
            }
            if (!crowded) shots.Add(candidate);
        }

        // Scores relative to the best, so maps of any size weigh the same
        float top = authored.quality;
        foreach (Shot shot in shots) top = Mathf.Max(top, shot.quality);
        if (top > 0f)
        {
            foreach (Shot shot in shots) shot.quality /= top;
            authored.quality /= top;
        }
        shots.Add(authored);
    }

    // Spawn points, and floor points halfway between spawn points near each other
    private void FindInterestPoints()
    {
        List<Vector3> spawns = new List<Vector3>();
        if (spawnPoints != null)
        {
            foreach (Transform point in spawnPoints)
            {
                if (point != null) spawns.Add(point.position + Vector3.up * BodyHeight);
            }
        }
        if (spawns.Count == 0) return;
        interest.AddRange(spawns);

        mapCenter = Centroid(spawns);
        mapRadius = 10f;
        foreach (Vector3 p in spawns) mapRadius = Mathf.Max(mapRadius, Vector3.ProjectOnPlane(p - mapCenter, Vector3.up).magnitude);

        float near = mapRadius * 0.8f;
        for (int i = 0; i < spawns.Count && interest.Count < MaxInterest; i++)
        {
            for (int j = i + 1; j < spawns.Count && interest.Count < MaxInterest; j++)
            {
                if ((spawns[i] - spawns[j]).sqrMagnitude > near * near) continue;
                Vector3 middle = (spawns[i] + spawns[j]) * 0.5f + Vector3.up * 2f;
                // Somewhere open with a floor not far below it
                if (Physics.CheckSphere(middle, 0.4f, solidMask, QueryTriggerInteraction.Ignore)) continue;
                if (!Physics.Raycast(middle, Vector3.down, out RaycastHit floor, 6f, solidMask, QueryTriggerInteraction.Ignore)) continue;
                interest.Add(floor.point + Vector3.up * BodyHeight);
            }
        }
    }

    // From a point, rise up to the ceiling (or the given height) and then head outward to near a
    // wall (or the given distance). False if that ends up somewhere cramped.
    private bool Scout(Vector3 from, Vector3 outward, float rise, float reach, out Vector3 position)
    {
        position = from;
        if (Physics.CheckSphere(from, 0.3f, solidMask, QueryTriggerInteraction.Ignore)) return false;
        Vector3 top = Reach(from, Vector3.up, rise);
        // Too low to look over anything
        if (top.y - from.y < 1.2f) return false;
        position = Reach(top, outward, reach);
        // Stepping back from the action is the point; a camera stuck at its start shows little
        return (position - top).sqrMagnitude > 2f * 2f;
    }

    // As far as the camera can go from a point in a direction, up to a distance, keeping clear of walls
    private Vector3 Reach(Vector3 from, Vector3 direction, float distance)
    {
        if (distance <= 0f || direction.sqrMagnitude < 0.0001f) return from;
        direction.Normalize();
        if (Physics.SphereCast(from, Clearance * 0.8f, direction, out RaycastHit hit, distance, solidMask, QueryTriggerInteraction.Ignore))
        {
            return from + direction * Mathf.Max(0f, hit.distance - Clearance);
        }
        return from + direction * distance;
    }

    // How much of the map a position shows in one frame, and where that frame looks
    private float Quality(Vector3 from, out Vector3 look)
    {
        // The interest points it can see, at a useful distance (not right under the lens)
        List<Vector3> seen = new List<Vector3>();
        foreach (Vector3 point in interest)
        {
            float distance = Vector3.Distance(from, point);
            if (distance < 3f || distance > mapRadius * 2.5f) continue;
            if (Physics.Linecast(from, point, sightMask, QueryTriggerInteraction.Ignore)) continue;
            seen.Add(point);
        }
        look = mapCenter;
        if (seen.Count == 0) return 0f;

        // Settle on the direction that fits the most of them in frame
        Vector3 dir = Centroid(seen) - from;
        List<Vector3> framed = new List<Vector3>();
        for (int pass = 0; pass < 3; pass++)
        {
            framed.Clear();
            foreach (Vector3 p in seen)
            {
                if (Vector3.Angle(dir, p - from) <= FrameAngle) framed.Add(p);
            }
            if (framed.Count == 0) framed.Add(seen[0]);
            dir = Centroid(framed) - from;
        }
        look = from + dir;

        // A view from above reads the map best; straight down or level are both poor
        float pitch = Mathf.Asin(Mathf.Clamp(-dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        float angleBonus = Mathf.Clamp01(1f - Mathf.Abs(pitch - 30f) / 40f) * 0.5f + 0.5f;
        return framed.Count * angleBonus;
    }

    // The frame a fixed view looks at: the interest points near its line of sight
    private Vector3 AimAlong(Vector3 from, Vector3 forward, Vector3 fallback)
    {
        List<Vector3> framed = new List<Vector3>();
        foreach (Vector3 point in interest)
        {
            if (Vector3.Angle(forward, point - from) > FrameAngle) continue;
            if (Physics.Linecast(from, point, sightMask, QueryTriggerInteraction.Ignore)) continue;
            framed.Add(point);
        }
        if (framed.Count == 0) return fallback;
        // Keep the authored direction, at the distance of what it shows
        float distance = Vector3.Distance(from, Centroid(framed));
        return from + forward.normalized * distance;
    }

    #endregion

    private static Vector3 Centroid(List<Vector3> points)
    {
        Vector3 sum = Vector3.zero;
        foreach (Vector3 p in points) sum += p;
        return points.Count > 0 ? sum / points.Count : sum;
    }
}

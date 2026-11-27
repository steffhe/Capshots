using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

// An offline firing lane in the practice scene. The target is a real player prefab driven by
// range input, so weapon spread, pellet aggregation, hit markers and the flying hat are shared.
public partial class Manager
{
    private Transform lane, shooterSpawn, targetSpawn;
    private Text rangeTitle, rangeHint, backstopSign;
    private int displayedCaps = -1, displayedDistance;
    private bool displayedMotion;
    private int rangeCaps;
    private int targetDistance = 8;
    private bool movingTarget, freeRange, changingScene, offerMatch;
    private float hintUntil, offerAfter;
    public bool InRange => lane != null;
    public bool FreeRange => freeRange;
    public bool MovingTarget => movingTarget;
    public int TargetDistance => targetDistance;
    private int RangeHat => (rangeCaps % 2 == 0) ? Hats.Crown : Hats.TrafficCone;

    private void BuildRange()
    {
        lane = new GameObject("Shooting Range").transform;
        lane.SetParent(transform, false);
        lane.position = spawnPoints[0].position + Vector3.up * 160f;
        lane.rotation = Quaternion.identity;
        Color deck = new Color(0.19f, 0.25f, 0.28f);
        Color steel = new Color(0.12f, 0.19f, 0.23f);
        Color enamel = new Color(0.25f, 0.43f, 0.44f);
        Color chalk = new Color(0.79f, 0.86f, 0.82f);
        Color amber = new Color(1f, 0.67f, 0.2f);
        LaneBlock("Deck", new Vector3(0, -0.5f, 9), new Vector3(12, 1, 26), deck, solid: true);
        LaneBlock("Backstop", new Vector3(0, 3, 22), new Vector3(12, 6, 1), steel, solid: true);
        LaneBlock("Rear wall", new Vector3(0, 3, -4), new Vector3(12, 6, 1), steel, solid: true);
        foreach (int side in new[] { -1, 1 })
        {
            LaneBlock("Side wall", new Vector3(side * 6, 3, 9), new Vector3(0.5f, 6, 26), enamel, solid: true);
            LaneBlock("Wall plinth", new Vector3(side * 5.76f, 0.32f, 9), new Vector3(0.08f, 0.64f, 25), steel);
            LaneBlock("Firing line guide", new Vector3(side * 4.7f, 0.012f, 9), new Vector3(0.05f, 0.012f, 18), chalk);
        }
        LaneBlock("Firing line", new Vector3(0, 0.012f, 0), new Vector3(9.45f, 0.012f, 0.14f), amber);

        // Open gantries frame each distance. Keep the centre clear for the moving target.
        for (int z = 8; z <= 18; z += 5)
        {
            LaneBlock("Crossbeam", new Vector3(0, 5.9f, z), new Vector3(12, 0.28f, 0.45f), steel);
            LaneBlock("Strip light", new Vector3(0, 5.75f, z - 0.12f), new Vector3(6, 0.06f, 0.16f), chalk, lit: true);
            foreach (int side in new[] { -1, 1 })
            {
                LaneBlock("Upright", new Vector3(side * 5.6f, 3, z), new Vector3(0.36f, 6, 0.45f), steel);
                LaneBlock("Brace", new Vector3(side * 5.12f, 5.38f, z), new Vector3(0.24f, 1.5f, 0.38f), steel)
                    .localRotation = Quaternion.Euler(0, 0, side * 45f);
                LaneBlock("Distance tick", new Vector3(side * 4.4f, 0.025f, z), new Vector3(0.65f, 0.015f, 0.1f), chalk);
            }
            LaneBlock("Distance plate", new Vector3(-5.2f, 1.85f, z - 0.25f), new Vector3(1.12f, 0.52f, 0.08f), steel);
            LaneSign("Distance", new Vector3(-5.2f, 1.85f, z - 0.30f), new Vector2(160, 65), 0.007f, 50).text = $"{z} m";
        }

        // A pale impact panel gives the hats a consistent background across map lighting setups.
        LaneBlock("Impact panel", new Vector3(0, 1.95f, 21.45f), new Vector3(9.4f, 3.8f, 0.1f), new Color(0.55f, 0.64f, 0.63f));
        LaneBlock("Scoreboard trim", new Vector3(0, 4.02f, 21.44f), new Vector3(9.4f, 0.065f, 0.1f), amber, lit: true);
        backstopSign = LaneSign("Lane scoreboard", new Vector3(0, 4.85f, 21.4f), new Vector2(820, 130), 0.011f, 64);

        shooterSpawn = new GameObject("Firing line").transform;
        shooterSpawn.SetParent(lane, false);
        shooterSpawn.localPosition = Vector3.up;
        targetSpawn = new GameObject("Target mark").transform;
        targetSpawn.SetParent(lane, false);
        targetSpawn.localRotation = Quaternion.Euler(0, 180, 0);
        freeRange = Launcher.myProfile.completedRangeDrill;
    }

    private Text LaneSign(string label, Vector3 position, Vector2 size, float scale, int fontSize)
    {
        var sign = new GameObject(label, typeof(RectTransform), typeof(Canvas));
        var rect = (RectTransform)sign.transform;
        rect.SetParent(lane, false);
        sign.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        rect.localPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one * scale;
        return UITheme.NewText("Lettering", rect, fontSize, Color.white, TextAnchor.MiddleCenter);
    }

    private Transform LaneBlock(string label, Vector3 position, Vector3 scale, Color color, bool solid = false, bool lit = false)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = label;
        block.transform.SetParent(lane, false);
        block.transform.localPosition = position;
        block.transform.localScale = scale;
        Renderer renderer = block.GetComponent<Renderer>();
        if (!solid)
        {
            Collider collider = block.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        renderer.sharedMaterial = lit ? DroneBody.GlowMaterial : PlayerCap.SharedMaterial(renderer.sharedMaterial);
        var tint = new MaterialPropertyBlock();
        tint.SetColor("_BaseColor", color);
        if (lit) tint.SetColor("_EmissionColor", color * 1.4f);
        renderer.SetPropertyBlock(tint);
        return block.transform;
    }

    private Transform RangeSpawn(bool bot)
    {
        targetSpawn.localPosition = new Vector3(0, 1, targetDistance);
        return bot ? targetSpawn : shooterSpawn;
    }

    public PlayerInputData RangeTargetInput(Vector3 position)
    {
        Vector3 local = lane.InverseTransformPoint(position);
        float x = movingTarget ? Mathf.Sin((float)Runner.SimulationTime * 1.1f) * 2.8f : 0f;
        // Facing the shooter (+180 yaw), local right and forward are world left and back.
        return new PlayerInputData { Yaw = 180f, Move = Vector2.ClampMagnitude(new Vector2(local.x - x, local.z - targetDistance), 1f) };
    }

    public void CycleRangeDistance() => targetDistance = targetDistance == 18 ? 8 : targetDistance + 5;
    public void ToggleRangeMotion() => movingTarget = !movingTarget;
    public void RangeBodyHit()
    {
        if (!freeRange) hintUntil = Time.unscaledTime + 2f;
    }

    private void CountRangeCap()
    {
        rangeCaps++;
        hintUntil = 0f;
        if (freeRange) return;
        movingTarget = true;
        if (rangeCaps < 2) return;
        freeRange = true;
        Launcher.myProfile.completedRangeDrill = true;
        Data.SaveProfile(Launcher.myProfile);
        offerMatch = true;
        offerAfter = Time.unscaledTime + 4f;
    }

    private void UpdateRangeHud()
    {
        if (rangeTitle == null)
        {
            var top = NewLayer("Range score", new Vector2(0.5f, 0.9f), new Vector2(680f, 70f), Hud.transform);
            Image plate = UITheme.NewImage("Coaching panel", top, UITheme.TimerBackdrop, 8f);
            UITheme.Stretch(plate.rectTransform);
            plate.raycastTarget = false;
            rangeTitle = UITheme.NewText("Caps", top, 24, UITheme.TextColor, TextAnchor.MiddleCenter);
            UITheme.Inset(rangeTitle.rectTransform, 0, 0);
            UITheme.AddShadow(rangeTitle);
            var aim = NewLayer("Range coaching", new Vector2(0.5f, 0.43f), new Vector2(540f, 40f), Hud.transform);
            rangeHint = UITheme.NewText("Hint", aim, 20, UITheme.AccentText, TextAnchor.MiddleCenter);
            UITheme.Inset(rangeHint.rectTransform, 0, 0);
            UITheme.AddShadow(rangeHint);
        }
        if (displayedCaps != rangeCaps || displayedDistance != targetDistance || displayedMotion != movingTarget)
        {
            displayedCaps = rangeCaps;
            displayedDistance = targetDistance;
            displayedMotion = movingTarget;
            string score = rangeCaps == 1 ? "1 CAPSHOT" : $"{rangeCaps} CAPSHOTS";
            backstopSign.text = score;
            if (!freeRange && rangeCaps == 0)
                rangeTitle.text = "FIRST CAP   ·   1 / 2\n<size=19>Line up the crosshair with the crown, then fire to knock it off.</size>";
            else if (!freeRange)
                rangeTitle.text = "MOVING TARGET   ·   2 / 2\n<size=19>Track the moving drone and shoot the cone off its head.</size>";
            else if (offerMatch)
                rangeTitle.text = "DRILL COMPLETE\n<size=19>In Cap Grab, walk over a fallen enemy cap to score. First to 10 wins.</size>";
            else
            {
                string motion = movingTarget ? "MOVING" : "STATIONARY";
                rangeTitle.text = $"FREE PRACTICE   ·   {targetDistance} m   ·   {motion}\n<size=19>Try another gun, or pause to change the target or start a match.</size>";
            }
        }
        rangeHint.text = Time.unscaledTime < hintUntil ? "BODY HIT   ·   AIM UP AT THE HAT" : "";
        if (offerMatch && Time.unscaledTime >= offerAfter)
        {
            offerMatch = false;
            displayedCaps = -1;
            Pause pause = FindAnyObjectByType<Pause>();
            if (pause != null && !Pause.paused) pause.TogglePause();
        }
    }

    public void StartRangeMatch()
    {
        if (!InRange || changingScene) return;
        changingScene = true;
        var settings = new Dictionary<string, SessionProperty>();
        foreach (var item in NetworkSession.MatchProperties(Runner)) settings[item.Key] = item.Value;
        settings.Remove("range");
        settings["bots"] = settings["practicebots"];
        settings.Remove("practicebots");
        settings["mode"] = (int)MatchMode.CapGrab;
        NetworkSession.UpdateMatchProperties(Runner, settings);
        LoadingScreen.Show("PRACTICE VS BOTS", "COLLECT 10 CAPS TO WIN", "EASY");
        StartCoroutine(LoadMapAfter(Launcher.Maps[(int)settings["map"]].scene, 0.6f));
    }
}

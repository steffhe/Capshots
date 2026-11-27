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
    private int displayedCaps = -1;
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
        Color slate = new Color(0.16f, 0.21f, 0.27f);
        Color rail = new Color(0.32f, 0.42f, 0.48f);
        LaneBlock("Floor", new Vector3(0, -0.5f, 9), new Vector3(12, 1, 26), slate);
        LaneBlock("Backstop", new Vector3(0, 3, 22), new Vector3(12, 6, 1), rail);
        LaneBlock("Rear wall", new Vector3(0, 3, -4), new Vector3(12, 6, 1), rail);
        foreach (int side in new[] { -1, 1 })
            LaneBlock("Side wall", new Vector3(side * 6, 3, 9), new Vector3(0.5f, 6, 26), slate);
        for (int z = 8; z <= 18; z += 5)
            LaneBlock("Distance stripe", new Vector3(0, 0.005f, z), new Vector3(11.5f, 0.01f, 0.08f), UITheme.AccentText);

        var board = new GameObject("Lane sign", typeof(RectTransform), typeof(Canvas));
        board.transform.SetParent(lane, false);
        board.transform.localPosition = new Vector3(0, 4.5f, 21.45f);
        board.transform.localScale = Vector3.one * 0.012f;
        ((RectTransform)board.transform).sizeDelta = new Vector2(800, 120);
        board.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        backstopSign = UITheme.NewText("Instruction", board.transform, 46, Color.white, TextAnchor.MiddleCenter);

        shooterSpawn = new GameObject("Firing line").transform;
        shooterSpawn.SetParent(lane, false);
        shooterSpawn.localPosition = Vector3.up;
        targetSpawn = new GameObject("Target mark").transform;
        targetSpawn.SetParent(lane, false);
        targetSpawn.localRotation = Quaternion.Euler(0, 180, 0);
        freeRange = Launcher.myProfile.completedRangeDrill;
    }

    private void LaneBlock(string label, Vector3 position, Vector3 scale, Color color)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = label;
        block.transform.SetParent(lane, false);
        block.transform.localPosition = position;
        block.transform.localScale = scale;
        Renderer renderer = block.GetComponent<Renderer>();
        renderer.sharedMaterial = PlayerCap.SharedMaterial(renderer.sharedMaterial);
        var tint = new MaterialPropertyBlock();
        tint.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(tint);
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
        if (!freeRange && rangeCaps == 0) hintUntil = Time.unscaledTime + 1.5f;
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
        offerAfter = Time.unscaledTime + 0.8f;
    }

    private void UpdateRangeHud()
    {
        if (rangeTitle == null)
        {
            var top = NewLayer("Range score", new Vector2(0.5f, 0.9f), new Vector2(760f, 64f), Hud.transform);
            rangeTitle = UITheme.NewText("Caps", top, 24, UITheme.TextColor, TextAnchor.MiddleCenter);
            UITheme.Inset(rangeTitle.rectTransform, 0, 0);
            UITheme.AddShadow(rangeTitle);
            var aim = NewLayer("Range coaching", new Vector2(0.5f, 0.43f), new Vector2(540f, 40f), Hud.transform);
            rangeHint = UITheme.NewText("Hint", aim, 20, UITheme.AccentText, TextAnchor.MiddleCenter);
            UITheme.Inset(rangeHint.rectTransform, 0, 0);
            UITheme.AddShadow(rangeHint);
        }
        if (displayedCaps != rangeCaps)
        {
            displayedCaps = rangeCaps;
            string score = rangeCaps == 1 ? "1 CAPSHOT" : $"{rangeCaps} CAPSHOTS";
            backstopSign.text = !freeRange && rangeCaps == 0 ? "SHOOT THE CROWN OFF" : score;
            rangeTitle.text = $"SHOOTING RANGE   ·   {score}\n<size=16>PAUSE FOR RANGE OPTIONS OR BOT PRACTICE</size>";
        }
        rangeHint.text = freeRange || rangeCaps > 0 ? "" :
            Time.unscaledTime < hintUntil ? "AIM A LITTLE HIGHER" : "";
        if (offerMatch && Time.unscaledTime >= offerAfter)
        {
            offerMatch = false;
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

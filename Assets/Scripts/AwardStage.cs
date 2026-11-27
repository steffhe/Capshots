using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// The awards screen at the end of a match, between the result banner and the results (scoreboard,
// XP and map vote): MATCH AWARDS along the top, and for each award a card with who won it and why,
// with that player's drone hovering over it in their colour and cap, holding the gun form that fits
// the award. A player who won several gets one card listing them all. The cards come up one after
// another, each drone popping in over its card.
//
// The drones stand on a little stage of their own, in front of a dark backdrop with neon strips and a
// glowing disc behind each winner, lit by a spotlight each, with confetti in the
// winners' colours drifting down, built far above the map and filmed by a camera
// of its own drawn over the map view, so nothing in the map gets in the way. Purely local
// presentation: Manager builds it from the awards the host sent (Manager.Awards.cs).
public class AwardStage : MonoBehaviour
{
    // One player and every award they won (a player who won several is shown once, with all of them
    // listed on their card)
    public class Entry
    {
        public string name;
        public Color color;
        public int hat;
        public int face;
        public bool teamMode, mine;
        public readonly List<string> titles = new List<string>();
        public readonly List<string> details = new List<string>();
    }

    // Timing: a moment for the title, then each player's card, then a long hold on all of them
    private const float IntroTime = 1f, PerAward = 1.2f, HoldTime = 4.5f;
    public static float Duration(int count) => IntroTime + count * PerAward + HoldTime;

    // Far above any map, so the map's own geometry is never in the shot (the camera's far plane is short)
    private static readonly Vector3 StageOrigin = new Vector3(0f, 3000f, 0f);
    private const float CameraDistance = 7.5f, FieldOfView = 30f;
    // The player prefab's proportions (as in MenuBattle)
    private const float FeetToRoot = 0.97f, RootToGun = 0.556f, HoverHeight = 0.12f;
    // Cards: size and gap in canvas units, and where their tops sit (fraction of the screen's height)
    // A card's height: the name, then a title and detail line for each award
    private const float CardWidth = 330f, CardGap = 70f, CardTop = 0.27f;
    private const float CardNameHeight = 64f, CardAwardHeight = 50f, CardPadding = 12f;
    private const float PopTime = 0.4f;

    private class Drone
    {
        public Transform root;
        public Vector3 home;
        public float yaw, phase;
        public float shownAt = -1f;
    }

    private readonly List<Drone> drones = new List<Drone>();
    private readonly List<CanvasGroup> cards = new List<CanvasGroup>();
    private readonly List<RectTransform> cardRects = new List<RectTransform>();
    private Camera cam;
    // Your own drone's gun holder, if you won an award: it follows the cursor
    private Transform myGun;
    private Quaternion myGunRest;
    private bool myGunRestSet;
    private RectTransform screen;
    private CanvasGroup titleGroup;
    private bool fogWas;

    // Your own drone, if you won an award: it jumps and crouches with the game's Jump and Crouch
    // (crouch toggles, as in a match; jumping stands it up)
    private Drone myDrone;
    private float myHeight, myRise, myCrouch, myLanding;
    private bool myCrouched;
    private const float JumpSpeed = 4.6f, JumpGravity = 15f, CrouchDrop = 0.13f;
    private Gun[] forms;
    private Gun startingGun;

    // Builds the stage (nothing shows until Play). template: the camera whose look (post-processing,
    // anti-aliasing) to copy, e.g. the map camera.
    public static AwardStage Create(List<Entry> entries, Camera template, Transform canvas)
    {
        AwardStage stage = new GameObject("Award Stage").AddComponent<AwardStage>();
        stage.transform.position = StageOrigin;
        stage.Build(entries, template, canvas);
        return stage;
    }

    // Reveals the awards one by one, then holds; ends after Duration
    // until: when to end (unscaled time); by default after Duration
    public IEnumerator Play(float until = -1f)
    {
        float start = Time.unscaledTime;
        StartCoroutine(Fade(titleGroup, 0.4f));
        yield return new WaitForSecondsRealtime(IntroTime);
        for (int i = 0; i < drones.Count; i++)
        {
            drones[i].shownAt = Time.time;
            drones[i].root.gameObject.SetActive(true);
            StartCoroutine(Fade(cards[i], 0.3f));
            FeedbackUI.AwardShown();
            yield return new WaitForSecondsRealtime(PerAward);
        }
        float end = until >= 0f ? until : start + Duration(drones.Count);
        while (Time.unscaledTime < end) yield return null;
    }

    private void OnDestroy()
    {
        RenderSettings.fog = fogWas;
        if (screen != null) Destroy(screen.gameObject);
    }

    #region Building

    private void Build(List<Entry> entries, Camera template, Transform canvas)
    {
        // The maps' fog is for their distances; on this little stage it would only grey the drones
        fogWas = RenderSettings.fog;
        RenderSettings.fog = false;

        LoadForms();
        BuildCamera(template);
        BuildUi(entries, canvas);
        BuildLights();
        // The cards are laid out now: put each drone over its card
        Canvas.ForceUpdateCanvases();
        Vector3 floor = Vector3.zero;
        for (int i = 0; i < entries.Count; i++)
        {
            Drone drone = BuildDrone(entries[i], i);
            Vector3 feet = FeetOver(cardRects[i]);
            floor = feet;
            drone.home = feet + Vector3.up * (FeetToRoot + HoverHeight);
            // Turned a little towards the middle, so the guns don't point straight out of the screen
            float side = feet.x - transform.position.x;
            // (+z is the way a drone faces, and the camera looks along +z, so 180 faces the camera)
            drone.yaw = 180f + Mathf.Clamp(side * 14f, -35f, 35f) + (Mathf.Abs(side) < 0.3f ? 20f : 0f);
            drone.root.position = drone.home;
            drone.root.rotation = Quaternion.Euler(0f, drone.yaw, 0f);
            Pedestal(feet, entries[i].color);
            Spotlight(feet, entries[i].color);
            drones.Add(drone);
        }
        Floor(floor.y);
        Backdrop(entries, floor.y);
        Confetti(entries, floor.y);
    }

    // How far behind the drones the backdrop stands
    private const float BackdropDistance = 4.5f;
    private static readonly Color BackdropColor = new Color(0.035f, 0.04f, 0.07f);

    // Behind the stage: a dark wall, neon strips running along it in the winners' colours, and a
    // big soft disc of light on it behind each drone, in that drone's colour
    private void Backdrop(List<Entry> entries, float floorY)
    {
        float wallZ = transform.position.z + BackdropDistance;
        Transform wall = Primitive(PrimitiveType.Cube, "Backdrop", transform);
        wall.position = new Vector3(transform.position.x, floorY + 6f, wallZ + 0.1f);
        wall.localScale = new Vector3(40f, 14f, 0.2f);
        Paint(wall, DroneBody.ShellMaterial, BackdropColor, Color.black);

        // Neon strips: one low, one high, and a thin one between, each in turn of the winners' colours
        float[] heights = { 0.5f, 3.9f, 4.15f };
        float[] thickness = { 0.05f, 0.05f, 0.025f };
        for (int i = 0; i < heights.Length; i++)
        {
            Color color = entries.Count > 0 ? entries[i % entries.Count].color : Color.white;
            Transform strip = Primitive(PrimitiveType.Cube, "Neon", transform);
            strip.position = new Vector3(transform.position.x, floorY + heights[i], wallZ - 0.02f);
            strip.localScale = new Vector3(40f, thickness[i], 0.03f);
            Paint(strip, DroneBody.GlowMaterial, color, color * 2f);
        }

        // A soft glowing disc behind each drone, and a brighter ring round it. Each is sized to the
        // gap to its nearest neighbour, so with the drones close together they don't run into each other.
        const float MaxHalo = 2.75f, HaloGap = 0.3f;
        for (int i = 0; i < drones.Count && i < entries.Count; i++)
        {
            float room = MaxHalo;
            for (int j = 0; j < drones.Count; j++)
            {
                if (j != i) room = Mathf.Min(room, Mathf.Abs(drones[j].home.x - drones[i].home.x) - HaloGap);
            }
            float size = Mathf.Max(0.8f, room);
            Vector3 behind = new Vector3(drones[i].home.x, floorY + 2f, wallZ - 0.03f);
            // The ring: a bright disc, with the dim one over it leaving only its edge showing
            Transform ring = Primitive(PrimitiveType.Cylinder, "Halo Ring", transform);
            ring.position = behind;
            ring.rotation = Quaternion.Euler(90f, 0f, 0f);
            ring.localScale = new Vector3(size, 0.004f, size);
            Paint(ring, DroneBody.GlowMaterial, entries[i].color, entries[i].color * 1.4f);
            Transform disc = Primitive(PrimitiveType.Cylinder, "Halo", transform);
            disc.position = behind - Vector3.forward * 0.01f;
            disc.rotation = Quaternion.Euler(90f, 0f, 0f);
            float inner = size - 0.12f;
            disc.localScale = new Vector3(inner, 0.004f, inner);
            Color dim = Color.Lerp(BackdropColor, entries[i].color, 0.35f);
            Paint(disc, DroneBody.GlowMaterial, dim, entries[i].color * 0.35f);
        }
    }

    // A spotlight from above and in front, making a pool of light round each drone
    private void Spotlight(Vector3 feet, Color color)
    {
        Light spot = NewLight("Spot", LightType.Spot, Color.Lerp(Color.white, color, 0.25f), 12f);
        spot.transform.position = feet + new Vector3(0f, 5f, -2.5f);
        spot.transform.rotation = Quaternion.LookRotation(feet + Vector3.up * 0.6f - spot.transform.position);
        spot.range = 12f;
        spot.spotAngle = 32f;
        spot.innerSpotAngle = 18f;
    }

    private static void Paint(Transform part, Material material, Color color, Color emission)
    {
        Renderer r = part.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        block.SetColor("_EmissionColor", emission);
        r.SetPropertyBlock(block);
    }

    // Confetti drifting down over the stage, in the winners' colours and white, spinning as it
    // falls and starting again at the top
    private class Piece
    {
        public Transform transform;
        public Vector3 spin;
        public float fall, sway, phase;
    }
    private readonly List<Piece> confetti = new List<Piece>();
    private float confettiTop, confettiBottom;
    private const int ConfettiCount = 70;

    private void Confetti(List<Entry> entries, float floorY)
    {
        confettiBottom = floorY - 0.2f;
        confettiTop = floorY + 5.5f;
        Vector3 centre = transform.position;
        for (int i = 0; i < ConfettiCount; i++)
        {
            Transform piece = Primitive(PrimitiveType.Cube, "Confetti", transform);
            piece.position = new Vector3(centre.x + Random.Range(-6f, 6f), Random.Range(confettiBottom, confettiTop), centre.z + Random.Range(-2f, 4f));
            piece.rotation = Random.rotation;
            piece.localScale = new Vector3(0.08f, 0.005f, 0.13f);
            Renderer r = piece.GetComponent<Renderer>();
            r.sharedMaterial = DroneBody.GlowMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Color color = entries.Count > 0 && i % 4 != 3 ? entries[i % entries.Count].color : Color.white;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor("_EmissionColor", color * 0.5f);
            r.SetPropertyBlock(block);
            confetti.Add(new Piece
            {
                transform = piece,
                spin = Random.onUnitSphere * Random.Range(120f, 360f),
                fall = Random.Range(0.5f, 1.1f),
                sway = Random.Range(0.15f, 0.4f),
                phase = Random.value * 10f,
            });
        }
    }

    private void FallConfetti(float dt, float time)
    {
        foreach (Piece p in confetti)
        {
            Vector3 at = p.transform.position;
            at.y -= p.fall * dt;
            at.x += Mathf.Sin(time * 1.3f + p.phase) * p.sway * dt;
            if (at.y < confettiBottom) at.y = confettiTop;
            p.transform.position = at;
            p.transform.Rotate(p.spin * dt, Space.Self);
        }
    }

    private void LoadForms()
    {
        GameObject prefab = Resources.Load<GameObject>("Player");
        Weapon weapon = prefab != null ? prefab.GetComponent<Weapon>() : null;
        if (weapon == null || weapon.loadout == null) return;
        if (weapon.loadout.Length > 0) startingGun = weapon.loadout[weapon.PreferredStartingSlot];
        List<Gun> usable = new List<Gun>();
        foreach (Gun g in weapon.loadout) if (g != null && g.prefab != null) usable.Add(g);
        forms = usable.ToArray();
    }

    // The gun form that fits the award: the sniper rifle for cap shots, and so on
    private Gun FormFor(string title)
    {
        if (forms == null || forms.Length == 0) return null;
        string wanted = title switch
        {
            "CAPSHOT KING" => "Sniper Rifle",
            "UNTOUCHABLE" => "Shotgun",
            "STICKY FINGERS" => "Pistol",
            "PUNCHING BAG" => "Pistol",
            _ => "Machine Gun",
        };
        foreach (Gun g in forms) if (g.gunName == wanted) return g;
        return forms[0];
    }

    private void BuildCamera(Camera template)
    {
        GameObject go = new GameObject("Award Camera");
        go.transform.SetParent(transform, false);
        // Looking along +z at the drones, from a little above their middles
        go.transform.localPosition = new Vector3(0f, 1.25f, -CameraDistance);
        go.transform.localRotation = Quaternion.Euler(3f, 0f, 0f);
        cam = go.AddComponent<Camera>();
        cam.fieldOfView = FieldOfView;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 40f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.03f, 0.055f);
        cam.useOcclusionCulling = false;
        // Drawn after (over) the map camera, which keeps running underneath with the audio listener
        cam.depth = (template != null ? template.depth : 0f) + 10f;

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        if (template != null)
        {
            UniversalAdditionalCameraData from = template.GetUniversalAdditionalCameraData();
            data.antialiasing = from.antialiasing;
            data.antialiasingQuality = from.antialiasingQuality;
        }
    }

    private void BuildLights()
    {
        // A soft key light from above the camera, and a cool rim from behind
        Light key = NewLight("Key", LightType.Directional, new Color(1f, 0.96f, 0.9f), 1.1f);
        key.transform.rotation = Quaternion.Euler(38f, 18f, 0f);
        Light rim = NewLight("Rim", LightType.Directional, new Color(0.55f, 0.7f, 1f), 0.8f);
        rim.transform.rotation = Quaternion.Euler(25f, 200f, 0f);
    }

    private Light NewLight(string name, LightType type, Color color, float intensity)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        Light light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        return light;
    }

    private Drone BuildDrone(Entry entry, int index)
    {
        Drone drone = new Drone { phase = index * 1.7f };
        if (entry.mine) myDrone = drone;
        drone.root = new GameObject("Award Drone " + (index + 1)).transform;
        drone.root.SetParent(transform, false);

        Transform body = Primitive(PrimitiveType.Capsule, "Standing", drone.root);
        body.localPosition = new Vector3(0f, -0.34f, 0f);
        body.localScale = new Vector3(1f, 0.61387f, 1f);
        Transform head = Primitive(PrimitiveType.Sphere, "Head", body);
        head.localPosition = new Vector3(0f, 1.4498f, 0f);
        head.localScale = new Vector3(0.70853f, 1.1542f, 0.70853f);

        DroneBody dressed = DroneBody.Dress(body, head, entry.color, entry.teamMode, false, entry.face);
        // The punching bag has taken a beating: its lights flicker and its collar sparks
        if (dressed != null && entry.titles.Contains("PUNCHING BAG")) dressed.SetHealth(0.2f);
        // Everyone else is pleased with their award
        else if (dressed != null) dressed.Celebrate(float.PositiveInfinity);
        PlayerCap.Attach(head, entry.color, false, entry.hat);

        Gun form = entry.mine && startingGun != null && startingGun.prefab != null ? startingGun : FormFor(entry.titles.Count > 0 ? entry.titles[0] : "");
        if (form != null)
        {
            Transform holder = new GameObject("Weapon").transform;
            // Your own drone aims its gun at the cursor, like the main menu's (see AimMyGun)
            if (entry.mine) myGun = holder;
            holder.SetParent(drone.root, false);
            holder.localPosition = new Vector3(-0.031f, RootToGun, 0f);
            GameObject gun = Instantiate(form.prefab, holder, false);
            gun.name = form.prefab.name;
            GunModel.Build(gun, form);
            foreach (Sway sway in gun.GetComponentsInChildren<Sway>(true)) sway.enabled = false;
            foreach (Collider c in gun.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (LineRenderer line in gun.GetComponentsInChildren<LineRenderer>(true)) line.enabled = false;
            DroneHands.AttachToDisplay(drone.root, gun, entry.color);
            GunSatellites.AttachToDisplay(drone.root, gun, form, entry.color);
        }

        drone.root.gameObject.SetActive(false);
        return drone;
    }

    private static Transform Primitive(PrimitiveType type, string name, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        // Only for show: nothing to bump into
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    // A dark, slightly shiny floor for the drones to hover over
    private void Floor(float y)
    {
        Transform floor = Primitive(PrimitiveType.Cylinder, "Floor", transform);
        floor.position = new Vector3(transform.position.x, y - 0.01f, transform.position.z + 1f);
        // Out to the backdrop at the back, and past the edges of the shot at the sides
        floor.localScale = new Vector3(40f, 0.01f, 12f);
        Renderer r = floor.GetComponent<Renderer>();
        // The drone's shell material, which builds always include (a primitive's default material
        // can be left out of a build, and then draws pink)
        r.sharedMaterial = DroneBody.ShellMaterial;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", new Color(0.05f, 0.06f, 0.09f));
        block.SetColor("_Color", new Color(0.05f, 0.06f, 0.09f));
        r.SetPropertyBlock(block);
    }

    // A glowing ring on the floor under each drone, in its colour, with a little light of the same
    private void Pedestal(Vector3 feet, Color color)
    {
        Transform ring = Primitive(PrimitiveType.Cylinder, "Ring", transform);
        ring.position = feet + Vector3.up * 0.005f;
        ring.localScale = new Vector3(1.15f, 0.004f, 1.15f);
        Renderer r = ring.GetComponent<Renderer>();
        r.sharedMaterial = DroneBody.GlowMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        block.SetColor("_EmissionColor", color * 1.6f);
        r.SetPropertyBlock(block);

        Light glow = NewLight("Glow", LightType.Point, color, 2.2f);
        glow.transform.position = feet + new Vector3(0f, 0.35f, -0.5f);
        glow.range = 2.5f;
    }

    // Where a drone's feet go to stand just over this card: the card's top middle on screen, on the
    // plane the drones stand in
    private Vector3 FeetOver(RectTransform card)
    {
        Vector3[] corners = new Vector3[4];
        card.GetWorldCorners(corners);
        // An overlay canvas: world corners are in screen pixels
        Vector2 topMiddle = (corners[1] + corners[2]) / 2f;
        Vector3 screenPoint = new Vector3(topMiddle.x, topMiddle.y + Screen.height * 0.035f, 0f);
        Ray ray = cam.ScreenPointToRay(screenPoint);
        Plane plane = new Plane(Vector3.back, transform.position);
        return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : transform.position;
    }

    #endregion

    #region UI

    private void BuildUi(List<Entry> entries, Transform canvas)
    {
        GameObject go = new GameObject("Awards Screen", typeof(RectTransform));
        screen = (RectTransform)go.transform;
        screen.SetParent(canvas, false);
        UITheme.Stretch(screen);
        screen.SetAsLastSibling();

        // The title, along the top
        RectTransform title = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
        title.SetParent(screen, false);
        title.anchorMin = title.anchorMax = new Vector2(0.5f, 1f);
        title.pivot = new Vector2(0.5f, 1f);
        title.sizeDelta = new Vector2(1200f, 120f);
        title.anchoredPosition = new Vector2(0f, -40f);
        titleGroup = title.gameObject.AddComponent<CanvasGroup>();
        titleGroup.alpha = 0f;

        Text kicker = UITheme.NewText("Kicker", title, 20, UITheme.MutedText, TextAnchor.MiddleCenter);
        UITheme.Row(kicker.rectTransform, 0f, 26f);
        kicker.text = "THE BEST (AND WORST) OF THE MATCH";
        // White, like the other end screens' titles (gold is kept for cap shots)
        Text heading = UITheme.NewText("Heading", title, 64, UITheme.TextColor, TextAnchor.MiddleCenter);
        UITheme.Row(heading.rectTransform, 30f, 76f);
        heading.text = "MATCH AWARDS";
        UITheme.AddShadow(heading, 0.8f);

        // The cards in a row, their tops at CardTop of the screen's height
        float width = entries.Count * CardWidth + (entries.Count - 1) * CardGap;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            // The menus' panel, with its corner brackets (the winner's colour runs along the top instead
            // of the accent tab); your own card in a deep accent blue, which means "you" everywhere
            Image card = UITheme.NewImage("Award", screen, UITheme.Panel, UITheme.PanelRadius);
            UITheme.PanelChrome(card, false);
            card.color = entry.mine ? new Color(0.03f, 0.2f, 0.34f, 0.94f) : UITheme.Panel;
            RectTransform rect = card.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, CardTop);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(CardWidth, CardNameHeight + entry.titles.Count * CardAwardHeight + CardPadding);
            rect.anchoredPosition = new Vector2(-width / 2f + CardWidth / 2f + i * (CardWidth + CardGap), 0f);
            Image accent = UITheme.NewImage("Accent", rect, entry.color, 0f);
            UITheme.Row(accent.rectTransform, 0f, 3f);

            Text name = UITheme.NewText("Name", rect, 32, Color.white, TextAnchor.MiddleCenter);
            UITheme.Row(name.rectTransform, 14f, 40f);
            name.text = entry.mine ? "YOU" : TextRules.Escape(entry.name.ToUpperInvariant());

            // Each award under the name: its title, and why
            for (int a = 0; a < entry.titles.Count; a++)
            {
                float top = CardNameHeight + a * CardAwardHeight;
                Text awardTitle = UITheme.NewText("Title", rect, UITheme.MenuHeadingSize, UITheme.AccentText, TextAnchor.MiddleCenter);
                UITheme.Row(awardTitle.rectTransform, top, 24f);
                awardTitle.text = entry.titles[a];
                Text detail = UITheme.NewText("Detail", rect, UITheme.MenuLabelSize, UITheme.MutedText, TextAnchor.MiddleCenter);
                UITheme.Row(detail.rectTransform, top + 23f, 20f);
                detail.text = entry.details[a];
            }

            CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            cards.Add(group);
            cardRects.Add(rect);
        }
    }

    private static IEnumerator Fade(CanvasGroup group, float duration)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            if (group == null) yield break;
            group.alpha = Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }
        if (group != null) group.alpha = 1f;
    }

    #endregion

    // Hovering and bobbing, turning a little back and forth; a new one pops in with a bounce
    private void Update()
    {
        float time = Time.time;
        FallConfetti(Time.deltaTime, time);
        foreach (Drone drone in drones)
        {
            if (drone.shownAt < 0f) continue;
            float age = time - drone.shownAt;
            float pop = age < PopTime ? BackOut(age / PopTime) : 1f;
            drone.root.localScale = Vector3.one * Mathf.Max(0.001f, pop);
            float bob = Mathf.Sin((time + drone.phase) * 2.1f) * 0.05f;
            // Drops in from a little above
            float drop = age < PopTime ? (1f - age / PopTime) * 0.4f : 0f;
            float jump = 0f;
            if (drone == myDrone && age >= PopTime)
            {
                // Jumping and crouching: squashes a little low down and on landing, stretches in the air
                jump = MoveMine(Time.deltaTime);
                bob *= 1f - Mathf.Clamp01(myHeight * 4f);
                float squash = 0.2f * myCrouch + 0.18f * myLanding - Mathf.Clamp(myRise * 0.025f, -0.08f, 0.08f);
                drone.root.localScale = new Vector3(pop * (1f + squash * 0.5f), pop * (1f - squash), pop * (1f + squash * 0.5f));
            }
            drone.root.position = drone.home + Vector3.up * (bob + drop + jump);
            float turn = Mathf.Sin((time + drone.phase) * 0.7f) * 8f;
            drone.root.rotation = Quaternion.Euler(0f, drone.yaw + turn, Mathf.Sin((time + drone.phase) * 1.3f) * 2f);
        }
    }

    // Your own drone's jump and crouch this frame; returns how far it's moved up (down when crouched)
    private float MoveMine(float dt)
    {
        if (GameInput.Pressed(GameInput.Jump) && myHeight <= 0f && myRise <= 0f)
        {
            myRise = JumpSpeed;
            myCrouched = false;
        }
        if (GameInput.Pressed(GameInput.Crouch)) myCrouched = !myCrouched;

        if (myHeight > 0f || myRise > 0f)
        {
            myRise -= JumpGravity * dt;
            myHeight += myRise * dt;
            if (myHeight <= 0f)
            {
                myHeight = 0f;
                myRise = 0f;
                myLanding = 1f;
            }
        }
        myLanding = Mathf.MoveTowards(myLanding, 0f, dt * 5f);
        myCrouch = Mathf.MoveTowards(myCrouch, myCrouched ? 1f : 0f, dt * 7f);
        return myHeight - CrouchDrop * myCrouch;
    }

    // After Update has turned the drones: your own drone's gun points where the cursor is (a point
    // on a plane a little in front of the gun, towards the camera), like the main menu's. With a
    // controller, the right stick swings it about instead. Neither: it rests pointing ahead.
    private void LateUpdate()
    {
        if (myGun == null || cam == null || !myGun.gameObject.activeInHierarchy) return;
        if (!myGunRestSet)
        {
            myGunRest = myGun.localRotation;
            myGunRestSet = true;
        }
        Quaternion rest = myGun.parent.rotation * myGunRest;
        Quaternion target = rest;

        UnityEngine.InputSystem.Gamepad pad = UnityEngine.InputSystem.Gamepad.current;
        Vector2 stick = pad != null ? pad.rightStick.ReadValue() : Vector2.zero;
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (stick.sqrMagnitude > 0.04f)
        {
            target = rest * Quaternion.Euler(-stick.y * 35f, stick.x * 45f, 0f);
        }
        else if (mouse != null && !MenuNavigation.UsingGamepad)
        {
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Plane front = new Plane(Vector3.back, myGun.position + Vector3.back * 3f);
            if (front.Raycast(ray, out float distance))
            {
                Vector3 look = ray.GetPoint(distance) - myGun.position;
                if (look.sqrMagnitude > 0.0001f) target = Quaternion.LookRotation(look, Vector3.up);
            }
        }
        myGun.rotation = Quaternion.Slerp(myGun.rotation, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));
    }

    // Overshoots a little before settling at 1
    private static float BackOut(float t)
    {
        const float s = 1.7f;
        t -= 1f;
        return t * t * ((s + 1f) * t + s) + 1f;
    }
}

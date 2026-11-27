// Where the map scenes' UI objects are, by hierarchy path, for the scripts that look them up with
// GameObject.Find when a map loads or the local player spawns (Manager, Player, Weapon). One place, so a
// renamed or moved object in the HUD prefab or a map scene is fixed here, not hunted down across files.
// GameObject.Find only sees active objects: the HUD is switched on before its parts are looked up.
public static class HudPaths
{
    public const string Canvas = "Canvas";
    public const string Hud = "HUD";
    public const string HealthBar = "HUD/Health/Bar";
    public const string AmmoRoot = "HUD/Ammo";
    public const string AmmoText = "HUD/Ammo/Text";
    public const string UsernameText = "HUD/Username/Text";
    public const string Hitmark = "HUD/Crosshair/Hitmark";
    public const string CrosshairDot = "HUD/Crosshair/Dot";
    public const string Kills = "HUD/Stats/Kills";
    public const string Deaths = "HUD/Stats/Deaths";
    public const string TimerText = "HUD/Timer/Text";
    // Children of the canvas
    public const string EndGame = "End Game";
    public const string MapIntro = "Canvas/Map Intro";
}

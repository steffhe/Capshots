using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// The Office map as data: the boxes, materials, signs and spawn points that
// Tools/OfficeMap/generate.py writes to Assets/Resources/Maps/Office.txt. OfficeMap builds the
// scene from it. Parsing lives here, with no scene access, so the EditMode tests can check it.
public class OfficeMapData
{
    public enum MaterialKind { Opaque, Glass, Glow }

    // The most glass panes a map can have: the Manager keeps 2 bits of state for each
    public const int MaxPanes = 1024;

    public class MaterialDef
    {
        public string name;
        public Color color;
        // "none", "wood", "carpet", "checker", "tile", "concrete", "grass", "gravel", "boards",
        // "panels" or "mountains" (the frosted film, with see-through parts): a pattern drawn by OfficeMap
        public string texture;
        // Metres one repeat of the texture covers
        public float tile;
        public float smoothness;
        public MaterialKind kind;
    }

    public struct Box
    {
        // Index into materials, or -1 for an invisible collider
        public int material;
        public Vector3 center, size;
        public bool collider;
        // Collider on the Glass layer: blocks players but not shots
        public bool glass;
        public bool castShadows;
        public Bounds Bounds => new Bounds(center, size);
    }

    // A glass pane that cracks when shot and shatters on the second hit
    public struct Pane
    {
        public Vector3 center, size;
        // The frosted band across the pane (height 0 for none)
        public float frostY, frostHeight;
        public Bounds Bounds => new Bounds(center, size);
    }

    public struct Sign
    {
        public Vector3 position;
        public float yaw, height;
        public Color color;
        public string text;
    }

    public struct Spawn
    {
        public Vector3 position;
        public float yaw;
        // The team whose side it's on (see SpawnSide), or -1 for anyone
        public int team;
    }

    public readonly List<MaterialDef> materials = new List<MaterialDef>();
    public readonly List<Box> boxes = new List<Box>();
    public readonly List<Pane> panes = new List<Pane>();
    public readonly List<Sign> signs = new List<Sign>();
    public readonly List<Spawn> spawns = new List<Spawn>();
    public bool hasCamera;
    public Vector3 cameraPosition;
    public float cameraPitch, cameraYaw;

    public static OfficeMapData Parse(string text)
    {
        var data = new OfficeMapData();
        var materialIndex = new Dictionary<string, int>();
        string[] lines = text.Split('\n');
        for (int n = 0; n < lines.Length; n++)
        {
            string line = lines[n].Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            try
            {
                switch (p[0])
                {
                    case "m":
                        materialIndex[p[1]] = data.materials.Count;
                        data.materials.Add(new MaterialDef
                        {
                            name = p[1],
                            color = new Color(F(p[2]), F(p[3]), F(p[4]), F(p[5])),
                            texture = p[6],
                            tile = Mathf.Max(0.01f, F(p[7])),
                            smoothness = F(p[8]),
                            kind = p[9] == "glass" ? MaterialKind.Glass : p[9] == "glow" ? MaterialKind.Glow : MaterialKind.Opaque,
                        });
                        break;
                    case "b":
                        string flags = p[8];
                        int material = -1;
                        if (p[1] != "-" && !materialIndex.TryGetValue(p[1], out material))
                            throw new FormatException("unknown material " + p[1]);
                        bool colliderOnly = flags.Contains("o");
                        data.boxes.Add(new Box
                        {
                            material = colliderOnly ? -1 : material,
                            center = new Vector3(F(p[2]), F(p[3]), F(p[4])),
                            size = new Vector3(F(p[5]), F(p[6]), F(p[7])),
                            collider = colliderOnly || !flags.Contains("n"),
                            glass = flags.Contains("g"),
                            castShadows = !flags.Contains("s"),
                        });
                        break;
                    case "p":
                        data.panes.Add(new Pane
                        {
                            center = new Vector3(F(p[1]), F(p[2]), F(p[3])),
                            size = new Vector3(F(p[4]), F(p[5]), F(p[6])),
                            frostY = F(p[7]),
                            frostHeight = F(p[8]),
                        });
                        break;
                    case "s":
                        data.spawns.Add(new Spawn
                        {
                            position = new Vector3(F(p[1]), F(p[2]), F(p[3])),
                            yaw = F(p[4]),
                            team = p.Length > 5 ? int.Parse(p[5], NumberStyles.Integer, CultureInfo.InvariantCulture) : -1,
                        });
                        break;
                    case "l":
                        data.signs.Add(new Sign
                        {
                            position = new Vector3(F(p[1]), F(p[2]), F(p[3])),
                            yaw = F(p[4]),
                            height = F(p[5]),
                            color = new Color(F(p[6]), F(p[7]), F(p[8])),
                            text = string.Join(" ", p, 9, p.Length - 9),
                        });
                        break;
                    case "c":
                        data.hasCamera = true;
                        data.cameraPosition = new Vector3(F(p[1]), F(p[2]), F(p[3]));
                        data.cameraPitch = F(p[4]);
                        data.cameraYaw = F(p[5]);
                        break;
                    default:
                        throw new FormatException("unknown line type " + p[0]);
                }
            }
            catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException)
            {
                throw new FormatException("Office map line " + (n + 1) + ": " + e.Message, e);
            }
        }
        return data;
    }

    private static float F(string s)
    {
        return float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}

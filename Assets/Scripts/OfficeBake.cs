using UnityEngine;

// On the root of the baked Office floor (made by Capshots > Bake Office Map, see OfficeMapBaker).
// OfficeMap uses the bake only when it was made from the layout the scene loads.
public class OfficeBake : MonoBehaviour
{
    [Tooltip("Which layout this was baked from (OfficeMap.LayoutHash)")]
    public string layoutHash;
    [Tooltip("The materials the breakable glass panes are drawn with")]
    public Material glass, frost;
}

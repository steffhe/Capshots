using UnityEngine;

// Marks which side of a map a spawn point belongs to (see Manager.ChooseSpawnPoint). Optional: spawn
// points without one are used as before, by everyone in every mode.
//  - Team: in team modes, that team (0 = blue, 1 = red) prefers its own side's points. They still
//    avoid enemies as usual, so a team pushed back into its spawn comes in elsewhere.
//  - Free for all only: left out of team modes, so lane spawns don't put a team behind enemy lines.
public class SpawnSide : MonoBehaviour
{
    [Tooltip("-1: any team. 0: the blue team's side. 1: the red team's side.")]
    public int team = -1;
    [Tooltip("Only used in free for all modes")]
    public bool freeForAllOnly;

    private void OnDrawGizmos()
    {
        Gizmos.color = freeForAllOnly ? Color.gray : team == 0 ? new Color(0.3f, 0.6f, 1f) : team == 1 ? new Color(1f, 0.35f, 0.3f) : Color.white;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
    }
}

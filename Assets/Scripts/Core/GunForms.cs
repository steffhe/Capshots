// Rules for picking a form in the gun's loadout (see Weapon), kept apart so they can be tested
public static class GunForms
{
    // The form one step along from current: +1 next, -1 previous. Past either end it wraps round,
    // so one "next" button (the touch SWAP button) reaches every form.
    public static int Step(int current, int by, int count)
    {
        if (count <= 0) return 0;
        int next = (current + by) % count;
        return next < 0 ? next + count : next;
    }

    // The slot of the form called gunName, or fallback when there's no such form (a renamed or
    // removed gun, or no choice saved). The choice is saved by name so reordering the loadout
    // doesn't change what anyone spawns with.
    public static int IndexOf(Gun[] loadout, string gunName, int fallback)
    {
        if (loadout == null || string.IsNullOrEmpty(gunName)) return fallback;
        for (int i = 0; i < loadout.Length; i++)
        {
            if (loadout[i] != null && loadout[i].gunName == gunName) return i;
        }
        return fallback;
    }
}

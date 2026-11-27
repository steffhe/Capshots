using UnityEngine;

// The rules of the gun changing form (see Weapon, which runs them every tick on the host and on the
// shooter's own game, and GunMorph, which animates them), kept apart so they can be tested
public static class GunTransform
{
    public enum Change
    {
        None,     // no change: nothing asked for, the form already held, or not a form in the loadout
        Instant,  // the starting form picked in the settings, taken straight after spawning
        Morph,    // a change of form: the gun can't fire or aim until it's done
    }

    // What a request for form 'requested' (a loadout index, -1 for none) does to a gun holding
    // 'equipped'. Only the automatic starting pick ('startingRequest', set by the player's game, never
    // by a choice of theirs) is taken instantly, and only as the body's first change, before its first
    // shot, while 'windowOpen' (a moment after spawning). Anything else transforms.
    public static Change Decide(int requested, int equipped, int formCount, bool startingRequest,
        bool hasChangedForm, int shotsFired, bool windowOpen)
    {
        if (requested < 0 || requested >= formCount || requested == equipped) return Change.None;
        bool starting = startingRequest && !hasChangedForm && shotsFired == 0 && windowOpen;
        return starting ? Change.Instant : Change.Morph;
    }

    // Whether the gun may fire at all this tick: not changing form, swapping the cell, throwing or
    // cooking a grenade, and holding a form that exists. Every way of firing goes through this.
    public static bool CanFire(bool morphing, bool swappingCell, bool throwing, bool cooking, int equipped, int formCount)
    {
        return !morphing && !swappingCell && !throwing && !cooking && equipped >= 0 && equipped < formCount;
    }

    // How long the change of form's animation should take, so it finishes as the gun can fire again.
    // The player's own game knows exactly how long is left ('remaining', null if unknown). Other
    // players' games see the change a little late and play it whole. Too little left: no animation.
    public const float MinAnimationSeconds = 0.05f;

    public static float AnimationSeconds(float? remaining, float morphTime, bool ownGun)
    {
        float seconds = ownGun && remaining.HasValue ? Mathf.Min(remaining.Value, morphTime) : morphTime;
        return seconds >= MinAnimationSeconds ? seconds : 0f;
    }

    // How far through a change of form lasting 'morphTime' it is, 0 to 1, from the time left; -1 when
    // there's no change under way
    public static float Progress(float? remaining, float morphTime)
    {
        if (!remaining.HasValue || morphTime <= 0f) return -1f;
        return Mathf.Clamp01(1f - remaining.Value / morphTime);
    }
}

// The form a player's game asks for, sent with every input (PlayerInputData.WeaponSlot) rather than
// once, so one lost input can't lose a change of form (the host changes form whenever the form asked
// for differs from the one held). A new body starts by asking for the starting form picked in the
// settings, marked as that automatic pick so the host can take it instantly; any choice the player
// makes clears the mark.
public sealed class GunFormRequest
{
    // The loadout index asked for, or -1 for none yet
    public int Wanted { get; private set; } = -1;
    // Wanted is the automatic starting pick for this body, not the player's own choice
    public bool IsStartingPick { get; private set; }

    public void NewBody(int startingForm)
    {
        Wanted = startingForm;
        IsStartingPick = startingForm >= 0;
    }

    public void Pick(int form)
    {
        if (form < 0) return;
        Wanted = form;
        IsStartingPick = false;
    }

    // The next (+1) or previous (-1) form, wrapping round, from the one asked for (or held, if none)
    public void Step(int by, int equipped, int formCount)
    {
        if (by == 0 || formCount <= 0) return;
        int current = Wanted >= 0 ? Wanted : equipped;
        Pick(GunForms.Step(current, by, formCount));
    }

    public void Clear()
    {
        Wanted = -1;
        IsStartingPick = false;
    }

    // As PlayerInputData.WeaponSlot has it: loadout index + 1, 0 for none
    public byte Slot => (byte)(Wanted >= 0 ? Wanted + 1 : 0);
}

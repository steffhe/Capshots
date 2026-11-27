using Fusion;
using UnityEngine;

// Buttons in PlayerInputData.Buttons
public enum InputButton
{
    Jump = 0,
    Sprint = 1,
    Crouch = 2,
    Slide = 3,
    Fire = 4,
    Aim = 5,
    Reload = 6,
    Grenade = 7,
    // WeaponSlot is the automatic starting form for a new body, not the player's own choice: the host
    // may take it instantly (GunTransform.Decide)
    StartingForm = 8,
}

// Everything a player sends the host each tick. The host runs the player's movement and weapon
// from it, and the player's own game predicts the same thing locally so it feels instant.
public struct PlayerInputData : INetworkInput
{
    public NetworkButtons Buttons;
    public Vector2 Move;
    // Where the player looks, in degrees: yaw turns the body, pitch tilts the camera and gun
    public float Yaw;
    public float Pitch;
    // The gun form the player wants, as loadout index + 1, sent with every input until they pick
    // another (so one lost input can't lose a change of form). 0 = no preference (bots between changes).
    public byte WeaponSlot;
}

// Collects input every rendered frame and hands it to Fusion once per tick. A key tapped between
// two ticks is remembered until the next one, so quick presses are never lost.
public class InputCollector
{
    private bool jump, crouch, slide, fire, reload, grenade;
    // The gun form asked for, kept between inputs (GunFormRequest)
    private readonly GunFormRequest form = new GunFormRequest();
    // The body the request is for: a new one starts by asking for the starting form
    private Player inputBody;

    // A new body (just spawned) starts by asking for the form picked in the settings
    private Weapon SyncBody(Player local)
    {
        Weapon weapon = local.GetComponent<Weapon>();
        if (local != inputBody)
        {
            inputBody = local;
            if (weapon != null) form.NewBody(weapon.PreferredStartingSlot);
            else form.Clear();
        }
        return weapon;
    }

    public void Update()
    {
        Player local = Player.Local;
        if (local == null) { Clear(); form.Clear(); inputBody = null; return; }
        Weapon weapon = SyncBody(local);

        if (GameInput.Pressed(GameInput.Jump)) jump = true;
        if (GameInput.Pressed(GameInput.Crouch)) crouch = true;
        if (GameInput.Pressed(GameInput.Slide)) slide = true;
        if (GameInput.Pressed(GameInput.Fire)) fire = true;
        if (GameInput.Pressed(GameInput.Reload)) reload = true;
        if (GameInput.Pressed(GameInput.Grenade)) grenade = true;

        if (weapon == null) return;

        int count = weapon.loadout.Length;
        // Next and previous wrap round, so the touch SWAP button (next only) reaches every form. They
        // step from the form asked for, so quick notches mid-transformation all count.
        form.Step(GameInput.WeaponSwitch, weapon.EquippedIndex, count);
        if (GameInput.Pressed(GameInput.Weapon1)) form.Pick(0);
        if (GameInput.Pressed(GameInput.Weapon2) && count >= 2) form.Pick(1);
        if (GameInput.Pressed(GameInput.Weapon3) && count >= 3) form.Pick(2);
        if (GameInput.Pressed(GameInput.Weapon4) && count >= 4) form.Pick(3);
    }

    public PlayerInputData Take()
    {
        PlayerInputData data = default;
        Player local = Player.Local;
        if (local == null) { Clear(); form.Clear(); inputBody = null; return data; }
        SyncBody(local);

        data.Move = GameInput.MoveValue;
        data.Buttons.Set(InputButton.Jump, jump);
        data.Buttons.Set(InputButton.Sprint, GameInput.Held(GameInput.Sprint));
        data.Buttons.Set(InputButton.Crouch, crouch);
        data.Buttons.Set(InputButton.Slide, slide);
        data.Buttons.Set(InputButton.Fire, fire || GameInput.Held(GameInput.Fire));
        data.Buttons.Set(InputButton.Aim, GameInput.Held(GameInput.Aim));
        data.Buttons.Set(InputButton.Reload, reload);
        data.Buttons.Set(InputButton.Grenade, grenade || GameInput.Held(GameInput.Grenade));
        data.WeaponSlot = form.Slot;
        data.Buttons.Set(InputButton.StartingForm, form.IsStartingPick);

        Look look = local.GetComponent<Look>();
        if (look != null)
        {
            // This frame's mouse movement, even if Look hasn't run its Update yet this frame
            look.SampleLook();
            data.Yaw = look.Yaw;
            data.Pitch = look.Pitch;
        }

        Clear();
        return data;
    }

    private void Clear()
    {
        // The form asked for stays: it's sent with every input
        jump = crouch = slide = fire = reload = grenade = false;
    }
}

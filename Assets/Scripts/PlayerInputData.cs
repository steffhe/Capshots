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
    // The weapon the player wants, as loadout index + 1. 0 = no change.
    public byte WeaponSlot;
}

// Collects input every rendered frame and hands it to Fusion once per tick. A key tapped between
// two ticks is remembered until the next one, so quick presses are never lost.
public class InputCollector
{
    private bool jump, crouch, slide, fire, reload;
    private int weaponSlot;
    // The body the last input was for: the first input for a new one asks for the starting form
    private Player inputBody;

    public void Update()
    {
        Player local = Player.Local;
        if (local == null) { Clear(); return; }

        if (GameInput.Pressed(GameInput.Jump)) jump = true;
        if (GameInput.Pressed(GameInput.Crouch)) crouch = true;
        if (GameInput.Pressed(GameInput.Slide)) slide = true;
        if (GameInput.Pressed(GameInput.Fire)) fire = true;
        if (GameInput.Pressed(GameInput.Reload)) reload = true;

        Weapon weapon = local.GetComponent<Weapon>();
        if (weapon == null) return;

        int current = weaponSlot > 0 ? weaponSlot - 1 : weapon.EquippedIndex;
        int last = weapon.loadout.Length - 1;
        // Next and previous wrap round, so the touch SWAP button (next only) reaches every form
        int switchBy = GameInput.WeaponSwitch;
        if (switchBy != 0) weaponSlot = GunForms.Step(current, switchBy, weapon.loadout.Length) + 1;
        if (GameInput.Pressed(GameInput.Weapon1)) weaponSlot = 1;
        if (GameInput.Pressed(GameInput.Weapon2) && last >= 1) weaponSlot = 2;
        if (GameInput.Pressed(GameInput.Weapon3) && last >= 2) weaponSlot = 3;
        if (GameInput.Pressed(GameInput.Weapon4) && last >= 3) weaponSlot = 4;
    }

    public PlayerInputData Take()
    {
        PlayerInputData data = default;
        Player local = Player.Local;
        if (local == null) { Clear(); return data; }

        data.Move = GameInput.MoveValue;
        data.Buttons.Set(InputButton.Jump, jump);
        data.Buttons.Set(InputButton.Sprint, GameInput.Held(GameInput.Sprint));
        data.Buttons.Set(InputButton.Crouch, crouch);
        data.Buttons.Set(InputButton.Slide, slide);
        data.Buttons.Set(InputButton.Fire, fire || GameInput.Held(GameInput.Fire));
        data.Buttons.Set(InputButton.Aim, GameInput.Held(GameInput.Aim));
        data.Buttons.Set(InputButton.Reload, reload);
        // A new body (just spawned) starts in the form picked in the settings, unless a form was
        // just picked by hand
        if (local != inputBody)
        {
            inputBody = local;
            Weapon weapon = local.GetComponent<Weapon>();
            if (weaponSlot == 0 && weapon != null) weaponSlot = weapon.PreferredStartingSlot + 1;
        }
        data.WeaponSlot = (byte)weaponSlot;

        Look look = local.GetComponent<Look>();
        if (look != null)
        {
            data.Yaw = look.Yaw;
            data.Pitch = look.Pitch;
        }

        Clear();
        return data;
    }

    private void Clear()
    {
        jump = crouch = slide = fire = reload = false;
        weaponSlot = 0;
    }
}

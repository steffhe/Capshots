// The faces a drone can show on its dark face screen, and the level each one unlocks at, like Hats.
// A face's number is its place in this list, which is what's saved and sent over the network, so
// new faces go at the end. Drawn by DroneBody; a dead drone always shows X-es for eyes.
public static class Faces
{
    public const int Classic = 0, Blushing = 1, Happy = 2, Wink = 3, Sleepy = 4, Visor = 5,
        Angry = 6, Hearts = 7, Stars = 8, Cyclops = 9, Dots = 10, Sparkle = 11;

    private static readonly string[] names =
    {
        "CLASSIC", "BLUSHING", "HAPPY", "WINK", "SLEEPY", "VISOR",
        "ANGRY", "HEART EYES", "STAR EYES", "CYCLOPS", "DOT EYES", "SPARKLE EYES",
    };

    // The first unlock comes straight away; the rest are spread out between the hats
    private static readonly int[] unlockLevels =
    {
        0, 1, 3, 5, 7, 9,
        11, 14, 17, 20, 23, 26,
    };

    // Bots show one of the faces unlocked by level 9, like the hats they wear
    public const int BotFaceCount = 6;

    public static int Count => names.Length;

    public static bool IsValid(int face) => face >= 0 && face < names.Length;

    public static string Name(int face) => IsValid(face) ? names[face] : names[Classic];

    public static int UnlockLevel(int face) => IsValid(face) ? unlockLevels[face] : int.MaxValue;

    public static bool IsUnlocked(int face, int level) => IsValid(face) && level >= unlockLevels[face];

    // The face to actually show: the one picked if it's unlocked at this level, otherwise the classic
    public static int Wearable(int face, int level) => IsUnlocked(face, level) ? face : Classic;

    // Whether going from one level to another unlocks any face
    public static bool AnyUnlockedBetween(int oldLevel, int newLevel)
    {
        for (int i = 0; i < unlockLevels.Length; i++)
        {
            if (unlockLevels[i] > oldLevel && unlockLevels[i] <= newLevel) return true;
        }
        return false;
    }

    // Whether this face has pink cheeks under its eyes
    public static bool HasCheeks(int face) => face == Blushing || face == Happy || face == Hearts || face == Sparkle;
}

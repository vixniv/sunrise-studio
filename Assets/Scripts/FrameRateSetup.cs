using UnityEngine;

// Runs once before the first scene loads; no GameObject needed.
// On iOS Unity renders at 30 fps unless Application.targetFrameRate is set
// (Quality Settings VSync Count is ignored there), so movement looks choppy on iPhone.
// Setting it here also caps Play mode in the Editor, so the Editor shows the same motion as the phone.
// Player speeds are 3.75 / 7.5 units per second: 1 and 2 art pixels per frame at 60 fps (16 px per unit),
// which keeps pixel-perfect movement even. Keep speeds on multiples of 3.75 at this rate.
public static class FrameRateSetup
{
    public const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        Application.targetFrameRate = TargetFrameRate;
    }
}

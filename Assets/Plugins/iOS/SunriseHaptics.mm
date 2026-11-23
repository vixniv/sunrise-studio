#import <UIKit/UIKit.h>

// Native iOS haptics bridge for Haptics.cs.
// Style values must match the HapticStyle enum in Haptics.cs:
// 0 = Light, 1 = Medium, 2 = Heavy, 3 = Soft, 4 = Rigid.
// Generators are created once and kept for the app's lifetime,
// so this works whether or not ARC is enabled for the file.

static UIImpactFeedbackGenerator* sunriseGenerators[5];

static UIImpactFeedbackStyle SunriseHapticsStyle(int style)
{
    switch (style)
    {
        case 1: return UIImpactFeedbackStyleMedium;
        case 2: return UIImpactFeedbackStyleHeavy;
        case 3: return UIImpactFeedbackStyleSoft;
        case 4: return UIImpactFeedbackStyleRigid;
        default: return UIImpactFeedbackStyleLight;
    }
}

static UIImpactFeedbackGenerator* SunriseHapticsGenerator(int style)
{
    if (style < 0 || style > 4)
        style = 0;

    if (sunriseGenerators[style] == nil)
        sunriseGenerators[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:SunriseHapticsStyle(style)];

    return sunriseGenerators[style];
}

extern "C"
{
    void SunriseHaptics_Prepare(int style)
    {
        [SunriseHapticsGenerator(style) prepare];
    }

    void SunriseHaptics_Impact(int style)
    {
        UIImpactFeedbackGenerator* generator = SunriseHapticsGenerator(style);
        [generator impactOccurred];
        [generator prepare];
    }
}

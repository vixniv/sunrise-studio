#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>

// Native iOS helper to ensure the shared AVAudioSession is active before
// FMOD Studio and Unity Audio resume after Control Center, Notification Center,
// or background/foreground transitions.

extern "C" bool SunriseAudioSession_EnsureActive()
{
    NSError* error = nil;
    BOOL ok = [[AVAudioSession sharedInstance] setActive:YES error:&error];
    if (!ok)
    {
        NSLog(@"[SunriseAudio] AVAudioSession setActive:YES failed: %@", error);
    }
    return ok ? true : false;
}

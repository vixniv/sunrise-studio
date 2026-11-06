using System;
using UnityEngine.Events;

namespace SunriseStudio.UI
{
    [Serializable] public sealed class StringEvent : UnityEvent<string> { }
    [Serializable] public sealed class FloatEvent : UnityEvent<float> { }
    [Serializable] public sealed class BoolEvent : UnityEvent<bool> { }
    [Serializable] public sealed class IntEvent : UnityEvent<int> { }
    [Serializable] public sealed class ScreenIdEvent : UnityEvent<UIScreenId> { }
}

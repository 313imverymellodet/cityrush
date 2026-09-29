using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

// Page integrations: share sheet, haptics, analytics, leaderboard + online racing (race.js).
public class WebBridge : MonoBehaviour
{
    public static WebBridge I;
    Action<bool> pending;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void SD_Rewarded(string goName);
    [DllImport("__Internal")] static extern void SD_Gameplay(int on);
    [DllImport("__Internal")] static extern void SD_Event(string name, int value);
    [DllImport("__Internal")] static extern void SD_Ready();
    [DllImport("__Internal")] static extern int SD_AdsAvailable();
    [DllImport("__Internal")] static extern void CR_ArmShare(string text);
    [DllImport("__Internal")] static extern void CR_Vibrate(int ms);
    [DllImport("__Internal")] static extern void CR_RaceStart(string map);
    [DllImport("__Internal")] static extern void CR_RaceSubmit(string map, int ms, int lapMs, string car);
    [DllImport("__Internal")] static extern void CR_ShowBoard(string map);
    [DllImport("__Internal")] static extern void CR_NetOpen(string map, string car);
    [DllImport("__Internal")] static extern void CR_NetState(float x, float z, float h, float v, int lap, float s);
    [DllImport("__Internal")] static extern void CR_NetFinish(int ms);
    [DllImport("__Internal")] static extern void CR_NetLeave();
#endif

    void Awake() { I = this; gameObject.name = "WebBridge"; }

    public static bool AdsAvailable
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return SD_AdsAvailable() == 1;
#else
            return false;
#endif
        }
    }

    public void ShowRewarded(Action<bool> done)
    {
        pending = done;
        Sfx.I.SetMuted(true);
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Rewarded(gameObject.name);
#else
        OnRewarded("1");
#endif
    }

    public void OnRewarded(string ok)
    {
        Sfx.I.SetMuted(Race.I.Save.muted);
        var cb = pending; pending = null;
        cb?.Invoke(ok == "1");
    }

    public static void Gameplay(bool on)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Gameplay(on ? 1 : 0);
#endif
    }

    public static void Event(string name, int value = 0)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Event(name, value);
#endif
    }

    public static void Ready()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SD_Ready();
#endif
    }

    public static void Vibrate(int ms)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CR_Vibrate(ms);
#endif
    }

    public static void RaceStart(string map)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CR_RaceStart(map);
#endif
    }
    public static void RaceSubmit(string map, int ms, int lapMs)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CR_RaceSubmit(map, ms, lapMs, Race.I.Save.car);
#endif
    }
    public static void ShowBoard(string map)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CR_ShowBoard(map);
#endif
    }
    public static void NetOpen(string map, string car)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CR_NetOpen(map, car);
#endif
    }
    public static void NetState(float x, float z, float h, float v, int lap, float s)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CR_NetState(x, z, h, v, lap, s);
#endif
    }
    public static void NetFinish(int ms)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CR_NetFinish(ms);
#endif
    }
    public static void NetLeave()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CR_NetLeave();
#endif
    }

    // Web Share needs a live gesture: arm on pointer-down, the page fires it on pointer-up.
    public static void ArmShare(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CR_ArmShare(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
    }
}

public class ShareOnPress : MonoBehaviour, IPointerDownHandler
{
    public Func<string> Text;
    public void OnPointerDown(PointerEventData e) { if (Text != null) WebBridge.ArmShare(Text()); }
}

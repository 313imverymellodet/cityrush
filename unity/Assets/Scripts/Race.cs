using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    public string map = "downtown", car = "race";
    public bool muted, howto;
    public int races, wins;
    public float bestDowntown, bestSuburbs;      // best 3-lap time (seconds), 0 = none
    public float Best(string m) => m == "downtown" ? bestDowntown : bestSuburbs;
    public void SetBest(string m, float v) { if (m == "downtown") bestDowntown = v; else bestSuburbs = v; }
}

// CITY RUSH — arcade street racing. Solo vs AI, online vs real players, per-map leaderboards.
public class Race : MonoBehaviour
{
    public static Race I;
    public enum St { Menu, Countdown, Racing, Finished }
    public St State = St.Menu;
    public SaveData Save;
    public Camera Cam;
    public Track Track;
    public MapDef Map;
    public readonly List<Car> Cars = new List<Car>();
    public Car Player;
    public float RaceTime, Countdown;
    public bool Online, AutoPlay, AutoDrive, Dev;
    Light sun;
    Transform world;
    float shake, camYaw, netT, resultsT;
    Vector3 camVel;

    static readonly string[] AiNames = { "BLAZE", "NOVA", "VIPER", "DRIFTER", "TURBO", "GHOST", "ACE", "RIOT", "BOLT", "ZENITH" };

    // ======================================================================
    void Awake()
    {
        I = this;
        Application.targetFrameRate = -1;
        QualitySettings.shadowDistance = 60f; QualitySettings.shadowCascades = 1;
        QualitySettings.shadowResolution = ShadowResolution.Medium; QualitySettings.antiAliasing = 2;
        QualitySettings.pixelLightCount = 0;
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLInput.captureAllKeyboardInput = false;
#endif
        var url = Application.absoluteURL;
        Dev = url.Contains("dev=1") && (url.Contains("://localhost") || url.Contains("://127.0.0.1"));   // cheats never on the live site
        AutoPlay = url.Contains("bot=1"); AutoDrive = AutoPlay || (Dev && url.Contains("autodrive=1"));
        DevCam.Install(Dev);
        var json = PlayerPrefs.GetString("cr_save", "");
        Save = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
        if (Dev && url.Contains("fresh=1")) Save = new SaveData();

        gameObject.AddComponent<Sfx>();
        Sfx.I.SetMuted(Save.muted);
        new GameObject("WebBridge").AddComponent<WebBridge>();
        Cam = Camera.main;
        Cam.nearClipPlane = 0.3f; Cam.farClipPlane = 700f;
        Cam.clearFlags = CameraClearFlags.SolidColor;
        sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.shadows = LightShadows.Hard; sun.shadowStrength = 0.6f;
        sun.shadowBias = 0.05f; sun.shadowNormalBias = 0.4f;
        world = new GameObject("World").transform;
        new GameObject("UI").AddComponent<UI>().Init();
        new GameObject("Traffic").AddComponent<Traffic>();

        LoadMap(Save.map);
        GoMenu();
        if (!Save.howto) UI.I.ShowHowTo();
        WebBridge.Ready();
        if (AutoPlay) StartCoroutine(AutoStart());
    }

    IEnumerator AutoStart() { yield return new WaitForSecondsRealtime(1.2f); StartSolo(); }

    public void Persist() { PlayerPrefs.SetString("cr_save", JsonUtility.ToJson(Save)); PlayerPrefs.Save(); }

    // ======================================================================
    public void LoadMap(string id)
    {
        Map = Maps.Get(id);
        Save.map = Map.id;
        if (Track) Destroy(Track.gameObject);
        foreach (var c in Cars) if (c) Destroy(c.gameObject);
        Cars.Clear(); Player = null;
        Track = Track.Build(Map, world);
        Traffic.I.Build(Track, Map.downtown);
        if (Dev) Debug.Log("TRACK " + Map.id + " length=" + Track.Length);
        Cam.backgroundColor = Map.sky;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Map.fog; RenderSettings.fogStartDistance = 120f; RenderSettings.fogEndDistance = 520f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Map.ambient;
        RenderSettings.ambientEquatorColor = Color.Lerp(Map.ambient, Map.fog, 0.5f) * 0.8f;
        RenderSettings.ambientGroundColor = Map.ground * 0.5f;
        sun.transform.rotation = Quaternion.Euler(Map.sunPitch, Map.sunYaw, 0);
        sun.color = Map.sun; sun.intensity = Map.sunIntensity;
        UI.I.SetMiniMap(Track.MiniMap);
    }

    public void GoMenu()
    {
        State = St.Menu;
        Online = false;
        Time.timeScale = 1;
        foreach (var c in Cars) if (c) Destroy(c.gameObject);
        Cars.Clear(); Player = null;
        // attract mode: AI cars lapping behind the menu
        for (int i = 0; i < 5; i++)
        {
            var c = Car.Create(world, i == 0 ? global::Cars.Get(Save.car) : global::Cars.All[UnityEngine.Random.Range(0, global::Cars.All.Length)], Driver.AI, AiNames[i], Map.downtown);
            c.Place(Track, i);
            c.BaseSkill = c.Skill = 0.8f + i * 0.03f;
            Cars.Add(c);
        }
        RaceTime = 0; State = St.Menu;
        UI.I.ShowMenu();
        Sfx.I.Engine(false);
        WebBridge.Gameplay(false);
    }

    // ---------------------------------------------------------------- solo
    public void StartSolo()
    {
        Online = false;
        UI.I.CloseScreens();
        foreach (var c in Cars) if (c) Destroy(c.gameObject);
        Cars.Clear();
        var names = new List<string>(AiNames);
        for (int i = 0; i < 6; i++)
        {
            bool me = i == 4;   // start mid-pack: something to chase, something to hold off
            var def = me ? global::Cars.Get(Save.car) : global::Cars.All[UnityEngine.Random.Range(0, global::Cars.All.Length)];
            string nm = me ? "YOU" : names[UnityEngine.Random.Range(0, names.Count)];
            names.Remove(nm);
            var c = Car.Create(world, def, me ? Driver.Player : Driver.AI, nm, Map.downtown);
            c.Place(Track, i);
            c.BaseSkill = c.Skill = me ? 1f : 0.9f + (5 - i) * 0.018f + UnityEngine.Random.Range(-0.01f, 0.01f);
            if (me) Player = c;
            Cars.Add(c);
        }
        BeginCountdown();
        WebBridge.Event("race_solo_" + Map.id);
        WebBridge.RaceStart(Map.id);
    }

    void BeginCountdown()
    {
        if (AutoDrive) Player.Driver = Driver.AI;
        Traffic.I.Reset();
        State = St.Countdown;
        Countdown = 3.9f; RaceTime = 0;
        UI.I.ShowHud(true);
        Sfx.I.Engine(true);
        Sfx.I.StartMusic();
        camYaw = Player.Heading;
        Cam.transform.position = Player.transform.position + new Vector3(0, 30, 0);
        WebBridge.Gameplay(true);
    }

    // ---------------------------------------------------------------- online (messages from race.js)
    [Serializable] class NetPlayer { public string id, name, car; public int slot; }
    [Serializable] class NetMsg
    {
        public string t, id, map, you, name;
        public float x, z, h, v, s, ms;
        public int lap, place;
        public NetPlayer[] players;
    }

    public void OnNet(string json)
    {
        var m = JsonUtility.FromJson<NetMsg>(json);
        if (m == null) return;
        switch (m.t)
        {
            case "solo":
                UI.I.Toast("NO RACERS ONLINE  -  RACING AI");
                StartSolo();
                break;
            case "start":
                if (m.map != Map.id) LoadMap(m.map);
                Online = true;
                UI.I.CloseScreens();
                foreach (var c in Cars) if (c) Destroy(c.gameObject);
                Cars.Clear(); Player = null;
                foreach (var p in m.players)
                {
                    bool me = p.id == m.you;
                    var c = Car.Create(world, global::Cars.Get(p.car), me ? Driver.Player : Driver.Remote, me ? "YOU" : p.name, Map.downtown);
                    c.NetId = p.id;
                    c.Place(Track, p.slot);
                    if (me) Player = c;
                    Cars.Add(c);
                    if (!me) UI.I.NameTag(c.transform, p.name);
                }
                BeginCountdown();
                WebBridge.Event("race_online_" + Map.id, m.players.Length);
                break;
            case "state":
                foreach (var c in Cars)
                    if (c.NetId == m.id && c.Driver == Driver.Remote) c.NetUpdate(new Vector3(m.x, 0, m.z), m.h, m.v, m.lap, m.s);
                break;
            case "fin":
                foreach (var c in Cars)
                    if (c.NetId == m.id && c.Driver == Driver.Remote && !c.Finished) { c.Finished = true; c.FinishTime = m.ms / 1000f; UI.I.Toast(c.Name + " FINISHED  P" + m.place); }
                if (State == St.Finished && resultsT < -1f) UI.I.ShowResults(Standings(), Player, Online);
                break;
            case "left":
                for (int i = Cars.Count - 1; i >= 0; i--)
                    if (Cars[i].NetId == m.id && Cars[i].Driver == Driver.Remote) { UI.I.Toast(Cars[i].Name + " LEFT"); Destroy(Cars[i].gameObject); Cars.RemoveAt(i); }
                break;
            case "results":
                if (State == St.Finished && resultsT < -1f) UI.I.ShowResults(Standings(), Player, Online);
                break;
            case "left_lobby":
                break;
        }
    }

    // ======================================================================
    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (Input.anyKeyDown || Input.touchCount > 0) Sfx.I.StartMusic();

        bool raceOn = State == St.Racing || State == St.Finished;
        if (State == St.Countdown)
        {
            float before = Countdown;
            Countdown -= dt;
            int a = Mathf.CeilToInt(before - 0.9f), b = Mathf.CeilToInt(Countdown - 0.9f);
            if (a != b && b >= 0) { UI.I.CountdownNumber(b == 0 ? "GO!" : b.ToString(), b == 0); Sfx.I.Beep(b == 0); }
            if (Countdown <= 0.9f) { State = St.Racing; RaceTime = 0; foreach (var c in Cars) c.LapStart = 0; UI.I.Toast("WEAVE THROUGH TRAFFIC  -  NEAR MISSES FILL NITRO"); }
        }
        if (State == St.Racing || State == St.Finished) RaceTime += dt;

        if (State == St.Menu) foreach (var c in Cars) if (c.Lap > 1) c.Lap = 0;
        if (Player && State != St.Menu) PlayerInput(dt);
        foreach (var c in Cars)
            if (c.Driver == Driver.AI) c.DriveAI(Track, Cars, State == St.Menu ? null : Player, dt);

        int steps = Mathf.CeilToInt(dt / 0.012f);
        for (int s = 0; s < steps; s++)
        {
            float h = dt / steps;
            foreach (var c in Cars) c.Sim(h, Track, RaceTime, raceOn || State == St.Menu);
            Collide();
        }
        Traffic.I.Tick(dt, Cars, State == St.Menu ? null : Player, RaceTime, State == St.Racing && Player && !Player.Finished);

        if (Player)
        {
            Sfx.I.EngineUpdate(Mathf.Abs(Player.Speed) / Player.Def.top, Player.Slip > 8f && Mathf.Abs(Player.Speed) > 15f, Player.NitroLeft > 0);
            if (Player.WallHit > 0.3f) { Shake(Player.WallHit * 0.6f); Sfx.I.Crash(Player.WallHit); Player.WallHit = 0.29f; WebBridge.Vibrate(40); }
            if (Online && raceOn && (netT -= Time.unscaledDeltaTime) <= 0)
            {
                netT = 1f / 12f;
                var p = Player.transform.position;
                WebBridge.NetState(p.x, p.z, Player.Heading, Player.Speed, Player.Lap, Player.S);
            }
        }
        if (State != St.Menu && Player) UI.I.UpdateHud(Player, Standings(), RaceTime, Track);

        if (State == St.Finished)
        {
            resultsT -= Time.unscaledDeltaTime;
            if (resultsT <= 0 && resultsT > -1f) { resultsT = -2f; UI.I.ShowResults(Standings(), Player, Online); }
        }
    }

    void PlayerInput(float dt)
    {
        if (Player.Driver != Driver.Player) return;
        float steer = UI.I.SteerInput;
        float k = (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? 1 : 0);
        if (k != 0) steer = k;
        Player.Steer = Mathf.MoveTowards(Player.Steer, steer, dt * (steer == 0 ? 7f : 5f));
        bool brake = UI.I.BrakeInput || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
        Player.Brake = brake ? 1 : 0;
        Player.Throttle = brake ? 0 : 1;   // arcade: gas is automatic
        Player.NitroPressed = UI.I.NitroInput || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift);
        if (State == St.Countdown) { Player.Throttle = 0; Player.Brake = 1; }
    }

    // Cars are ~4.2 m long and ~2.5 m wide, so each is two circles (front and rear axle) rather than one;
    // a single circle let noses sink ~1.5 m into tails.
    const float CarR = 1.22f, CarAxle = 1.05f;

    void Collide()
    {
        for (int i = 0; i < Cars.Count; i++)
            for (int j = i + 1; j < Cars.Count; j++)
            {
                var a = Cars[i]; var b = Cars[j];
                if (a.Driver == Driver.Remote || b.Driver == Driver.Remote) continue;   // online rivals are ghosts
                var dc = b.transform.position - a.transform.position; dc.y = 0;
                if (dc.sqrMagnitude > 25f) continue;
                var fa = new Vector3(Mathf.Sin(a.Heading), 0, Mathf.Cos(a.Heading)) * CarAxle;
                var fb = new Vector3(Mathf.Sin(b.Heading), 0, Mathf.Cos(b.Heading)) * CarAxle;
                for (int sa = -1; sa <= 1; sa += 2)
                    for (int sb = -1; sb <= 1; sb += 2)
                    {
                        var d = (b.transform.position + fb * sb) - (a.transform.position + fa * sa); d.y = 0;
                        float dist = d.magnitude;
                        if (dist > CarR * 2f || dist < 0.01f) continue;
                        var n = d / dist;
                        float push = (CarR * 2f - dist) * 0.5f;
                        a.transform.position -= n * push; b.transform.position += n * push;
                        float rel = Vector3.Dot(b.Vel - a.Vel, n);
                        if (rel < 0) { a.Vel += n * rel * 0.6f; b.Vel -= n * rel * 0.6f; }
                        if ((a == Player || b == Player) && Mathf.Abs(rel) > 3f) { Sfx.I.Bump(); Shake(0.15f); }
                        // a real shove (not just rubbing in the pack) arms a takedown
                        if (Mathf.Abs(rel) > 4f && a == Player && b.Driver == Driver.AI) b.BumpedByPlayer = RaceTime;
                        if (Mathf.Abs(rel) > 4f && b == Player && a.Driver == Driver.AI) a.BumpedByPlayer = RaceTime;
                    }
            }
    }

    public List<Car> Standings()
    {
        var list = new List<Car>(Cars);
        list.Sort((a, b) =>
        {
            if (a.Finished && b.Finished) return a.FinishTime.CompareTo(b.FinishTime);
            if (a.Finished) return -1;
            if (b.Finished) return 1;
            return b.Progress(Track).CompareTo(a.Progress(Track));
        });
        return list;
    }

    // ---------------------------------------------------------------- events
    public void OnLap(Car c)
    {
        if (c != Player) return;
        bool best = c.LastLap <= c.BestLap + 0.001f;
        UI.I.Banner(c.Lap == Car.Laps ? "FINAL LAP!" : "LAP " + c.Lap + "/" + Car.Laps, UI.Time(c.LastLap) + (best ? "  BEST" : ""));
        Sfx.I.Lap();
    }

    public void OnFinish(Car c)
    {
        if (c != Player) return;
        State = St.Finished;
        resultsT = 1.8f;
        UI.I.ClearRank();
        int place = Standings().IndexOf(c) + 1;
        UI.I.Banner(place == 1 ? "YOU WIN!" : "FINISHED P" + place, UI.Time(c.FinishTime));
        Sfx.I.Finish(place == 1);
        Save.races++;
        if (place == 1) Save.wins++;
        float prev = Save.Best(Map.id);
        if (prev <= 0 || c.FinishTime < prev) Save.SetBest(Map.id, c.FinishTime);
        Persist();
        if (!AutoDrive)   // AI-driven laps never reach the leaderboard
            WebBridge.RaceSubmit(Map.id, Mathf.RoundToInt(c.FinishTime * 1000), Mathf.RoundToInt(c.BestLap * 1000));
        if (Online) WebBridge.NetFinish(Mathf.RoundToInt(c.FinishTime * 1000));
        WebBridge.Event("finish_" + Map.id, place);
        WebBridge.Event("weave_nearmiss", Traffic.I.NearMisses);
        WebBridge.Event("weave_takedowns", Traffic.I.Takedowns);
        // solo: project the AI finishing times so the results table is complete right away
        if (!Online)
        {
            float goal = (Car.Laps + 1) * Track.Length;
            foreach (var o in Cars)
            {
                if (o == c || o.Finished) continue;
                float prog = o.Progress(Track), done = prog - (Track.Length - 10f - (o.Grid / 2) * 9f);
                float avg = RaceTime > 1f && done > 10f ? done / RaceTime : o.Def.top * 0.7f;
                o.Finished = true; o.FinishTime = RaceTime + Mathf.Max(0.3f, (goal - prog) / Mathf.Max(avg, 5f));
            }
        }
        // player stays on track, autopiloted, while the rest finish
        c.Driver = Driver.AI; c.BaseSkill = c.Skill = 0.85f;
        if (AutoPlay) StartCoroutine(AutoNext());
    }

    IEnumerator AutoNext() { yield return new WaitForSecondsRealtime(6f); LoadMap(Map.id == "downtown" ? "suburbs" : "downtown"); StartSolo(); }

    public void Shake(float a) => shake = Mathf.Min(1f, shake + a);

    // ---------------------------------------------------------------- menu actions
    public void SelectMap(string id)
    {
        if (Online) WebBridge.NetLeave();
        LoadMap(id);
        Persist();
        GoMenu();
    }

    public void OpenOnline()
    {
        if (State != St.Menu) GoMenu();
        WebBridge.NetOpen(Map.id, Save.car);
#if UNITY_EDITOR
        UI.I.Toast("ONLINE NEEDS THE WEB BUILD");
#endif
    }

    public void ToggleMute() { Save.muted = !Save.muted; Sfx.I.SetMuted(Save.muted); Persist(); }

    void OnApplicationFocus(bool f) { if (!f && !Online && (State == St.Countdown || State == St.Racing) && Time.timeScale > 0) Pause(); }

    public void Pause()
    {
        if (State == St.Menu) return;
        if (!Online) Time.timeScale = 0;
        UI.I.ShowPause();
    }
    public void Resume() { Time.timeScale = 1; UI.I.CloseScreens(); }
    public void Quit()
    {
        Time.timeScale = 1;
        if (Online) WebBridge.NetLeave();
        GoMenu();
    }

    public string ShareText()
    {
        int place = Player ? Standings().IndexOf(Player) + 1 : 0;
        string t = Player && Player.Finished ? UI.Time(Player.FinishTime) : "";
        return "CITY RUSH  " + Map.name + ": " + (place == 1 ? "1st place" : "P" + place) + " in " + t + (Online ? " vs real racers" : "") + " with " + Traffic.I.NearMisses + " near misses and " + Traffic.I.Takedowns + " takedowns. Beat that!";
    }

    [Serializable] public class RankMsg { public int rank, total, best; public bool newBest; public string error, map; }
    public void OnRank(string json)
    {
        var m = JsonUtility.FromJson<RankMsg>(json);
        if (m == null || m.map != Map.id) return;
        UI.I.SetRank(m);
    }

    // ---------------------------------------------------------------- camera
    void LateUpdate()
    {
        float dt = Time.deltaTime;
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        shake = Mathf.Max(0, shake - Time.unscaledDeltaTime * 2f);
        var sh = new Vector3(Mathf.PerlinNoise(Time.time * 30f, 0) - .5f, Mathf.PerlinNoise(0, Time.time * 30f) - .5f, 0) * shake * shake * 1.2f;

        if (State == St.Menu || !Player)
        {
            // slow orbit over the city, following the lead car
            var lead = Cars.Count > 0 ? Cars[0].transform.position : Vector3.zero;
            float a = Time.time * 0.08f;
            var want = lead + new Vector3(Mathf.Sin(a) * 48f, Map.downtown ? 62f : 34f, Mathf.Cos(a) * 48f);
            Cam.transform.position = Vector3.SmoothDamp(Cam.transform.position, want, ref camVel, 1.2f);
            Cam.transform.rotation = Quaternion.Slerp(Cam.transform.rotation, Quaternion.LookRotation(lead - Cam.transform.position), dt * 2f);
            Cam.fieldOfView = aspect < 0.8f ? 70f : 50f;
            return;
        }
        var car = Player;
        camYaw = Mathf.LerpAngle(camYaw * Mathf.Rad2Deg, car.Heading * Mathf.Rad2Deg, 1f - Mathf.Exp(-dt * 5f)) * Mathf.Deg2Rad;
        var back = new Vector3(Mathf.Sin(camYaw), 0, Mathf.Cos(camYaw));
        bool portrait = aspect < 0.8f;
        float dist = portrait ? 10.5f : 8.2f, height = portrait ? 5.6f : 3.4f;
        var target = car.transform.position - back * dist + Vector3.up * height;
        // position follows the car rigidly (smoothed yaw does the easing), so speed never leaves the car behind
        Cam.transform.position = State == St.Countdown && Countdown > 2.4f
            ? Vector3.Lerp(Cam.transform.position, target, 1f - Mathf.Exp(-dt * 3.5f))
            : Vector3.Lerp(Cam.transform.position, target, 1f - Mathf.Exp(-dt * 28f));
        Cam.transform.position += Cam.transform.rotation * sh;
        Cam.transform.rotation = Quaternion.LookRotation(car.transform.position + back * 7f + Vector3.up * 1.2f - Cam.transform.position);
        foreach (var o in Cars)
            if (o != car) o.SetVisible((o.transform.position - Cam.transform.position).sqrMagnitude > 3.6f * 3.6f);
        float speed01 = Mathf.Clamp01(Mathf.Abs(car.Speed) / car.Def.top);
        float fov = (portrait ? 72f : 58f) + speed01 * 10f + (car.NitroLeft > 0 || car.BoostLeft > 0 ? 8f : 0);
        Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, fov, 1f - Mathf.Exp(-dt * 4f));
    }
}

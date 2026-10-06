using System.Collections.Generic;
using UnityEngine;

public class CarDef
{
    public string id, name;
    public float top, accel, handling;
}

public static class Cars
{
    public static readonly CarDef[] All =
    {
        new CarDef { id = "race",             name = "VELOCITY",   top = 62, accel = 21, handling = 1.00f },
        new CarDef { id = "race-future",      name = "PHANTOM X",  top = 66, accel = 19, handling = 0.94f },
        new CarDef { id = "sedan-sports",     name = "STREET GT",  top = 59, accel = 22, handling = 1.05f },
        new CarDef { id = "hatchback-sports", name = "HOT HATCH",  top = 56, accel = 24, handling = 1.14f },
        new CarDef { id = "police",           name = "INTERCEPT",  top = 61, accel = 20, handling = 1.00f },
        new CarDef { id = "taxi",             name = "YELLOW CAB", top = 55, accel = 23, handling = 1.10f },
        new CarDef { id = "suv-luxury",       name = "BRUISER",    top = 57, accel = 21, handling = 0.96f },
        new CarDef { id = "sedan",            name = "COMMUTER",   top = 53, accel = 23, handling = 1.08f },
    };
    public static CarDef Get(string id) { foreach (var c in All) if (c.id == id) return c; return All[0]; }
}

public enum Driver { Player, AI, Remote }

public class Car : MonoBehaviour
{
    public const int Laps = 3;
    public CarDef Def;
    public Driver Driver;
    public string Name;
    public string NetId;
    public int Grid;

    // state
    public float Speed, Heading, Nitro, NitroLeft, BoostLeft;
    public Vector3 Vel;
    public int Index, Lap;
    public float S, LapStart, BestLap = float.MaxValue, FinishTime, LastLap;
    public bool Finished, Halfway = true;
    public float Slip;
    public float Progress(Track t) => Lap * t.Length + S;

    // input
    public float Steer, Throttle, Brake;
    public bool NitroPressed;

    // ai
    public float Skill = 1f, Lane, LaneTarget, laneT;

    // remote interpolation
    Vector3 netPos; float netHeading, netSpeed; bool hasNet;

    // visuals
    Transform body;
    readonly List<Transform> wheels = new List<Transform>();
    readonly List<Transform> frontWheels = new List<Transform>();
    TrailRenderer skidL, skidR;
    SpriteRenderer flame;
    float wheelSpin, roll, pitch;
    public float WallHit;            // >0 right after a wall impact (player feedback)
    public float BumpedByPlayer = -99f;   // race time of the player's last shove (a crash into traffic soon after = takedown)

    static Sprite glowSprite;
    Renderer[] rends; bool visible = true;

    public void SetVisible(bool v)
    {
        if (v == visible) return;
        visible = v;
        if (rends == null) rends = GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends) if (r && !(r is TrailRenderer)) r.enabled = v;
    }

    public static Car Create(Transform parent, CarDef def, Driver driver, string name, bool night)
    {
        var root = new GameObject("Car_" + name);
        root.transform.SetParent(parent, false);
        var c = root.AddComponent<Car>();
        c.Def = def; c.Driver = driver; c.Name = name;
        var m = Kit.Spawn("Cars/" + def.id, 1.65f, root.transform);
        c.body = m.transform;
        foreach (var t in m.GetComponentsInChildren<Transform>())
        {
            if (!t.name.StartsWith("wheel")) continue;
            c.wheels.Add(t);
            if (t.name.Contains("front")) c.frontWheels.Add(t);
        }
        foreach (var r in m.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        Kit.FloorQuad("shadow", Kit.Disc, new Color(0, 0, 0, 0.45f), 4.2f, root.transform, Vector3.zero, 0.04f).transform.localScale = new Vector3(2.8f, 5.2f, 1);

        if (!glowSprite) glowSprite = Sprite.Create(Kit.Glow, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);
        c.skidL = Skid(root.transform, new Vector3(-0.85f, 0.06f, -1.3f));
        c.skidR = Skid(root.transform, new Vector3(0.85f, 0.06f, -1.3f));
        c.flame = new GameObject("nitro").AddComponent<SpriteRenderer>();
        c.flame.sprite = glowSprite; c.flame.transform.SetParent(root.transform, false);
        c.flame.transform.localPosition = new Vector3(0, 0.6f, -2.5f);
        c.flame.color = new Color(0.3f, 0.7f, 1f, 0f);
        if (night)
        {
            var head = Kit.FloorQuad("headlights", Kit.Glow, new Color(1f, 0.95f, 0.8f, 0.35f), 1f, root.transform, new Vector3(0, 0, 7.5f), 0.05f);
            head.transform.localScale = new Vector3(8f, 12f, 1);
            Kit.FloorQuad("tail", Kit.Glow, new Color(1f, 0.1f, 0.15f, 0.45f), 3f, root.transform, new Vector3(0, 0, -2.6f), 0.05f);
        }
        return c;
    }

    static TrailRenderer Skid(Transform parent, Vector3 local)
    {
        var go = new GameObject("skid");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        var t = go.AddComponent<TrailRenderer>();
        t.sharedMaterial = Kit.UnlitAlpha;
        t.time = 2.2f; t.minVertexDistance = 0.4f; t.widthMultiplier = 0.45f;
        t.startColor = new Color(0.05f, 0.05f, 0.06f, 0.55f); t.endColor = new Color(0.05f, 0.05f, 0.06f, 0f);
        t.alignment = LineAlignment.TransformZ;
        go.transform.localRotation = Quaternion.Euler(90, 0, 0);
        t.emitting = false;
        return t;
    }

    public void Place(Track track, int gridSlot)
    {
        Grid = gridSlot;
        int row = gridSlot / 2, col = gridSlot % 2;
        float s = track.Length - 10f - row * 9f;
        float lane = col == 0 ? -3.2f : 3.2f;
        var p = track.PointAt(s, lane);
        var t = track.TangentAt(s);
        transform.position = p;
        Heading = Mathf.Atan2(t.x, t.z);
        transform.rotation = Quaternion.Euler(0, Heading * Mathf.Rad2Deg, 0);
        Speed = 0; Vel = Vector3.zero; Nitro = 0; NitroLeft = BoostLeft = 0;
        Index = track.Nearest(p); S = track.S(p, Index);
        Lap = 0; Halfway = true; Finished = false; BestLap = float.MaxValue; FinishTime = 0; LastLap = 0;
        Lane = LaneTarget = lane;
        skidL.Clear(); skidR.Clear();
        hasNet = false;
    }

    // ---------------------------------------------------------------- simulation
    public void Sim(float dt, Track track, float raceTime, bool raceOn)
    {
        if (Driver == Driver.Remote) { SimRemote(dt, track); return; }

        float top = Def.top * Skill;
        bool nitro = NitroLeft > 0;
        if (NitroPressed && Nitro >= 25f && NitroLeft <= 0) { NitroLeft = Mathf.Lerp(0.8f, 2.6f, Nitro / 100f); Nitro = 0; }
        if (nitro) { NitroLeft -= dt; top *= 1.28f; }
        if (BoostLeft > 0) { BoostLeft -= dt; top *= 1.2f; }
        if (!raceOn || Finished) { Throttle = Finished ? 0.25f : 0; Brake = Finished ? 0 : 1; }

        float accel = Def.accel * (nitro ? 1.6f : 1f) * (BoostLeft > 0 ? 1.5f : 1f);
        if (Brake > 0.5f && Speed > 1f) Speed -= 38f * dt;
        else if (Brake > 0.5f) Speed = Mathf.Max(Speed - 12f * dt, -9f);
        else if (Throttle > 0) Speed = Speed < top ? Speed + accel * Throttle * dt * (1f - 0.55f * Mathf.Clamp01(Speed / top)) : Mathf.MoveTowards(Speed, top, 20f * dt);
        else Speed = Mathf.MoveTowards(Speed, 0, 7f * dt);

        float speed01 = Mathf.Clamp01(Mathf.Abs(Speed) / Def.top);
        float yawRate = Steer * Def.handling * Mathf.Lerp(2.5f, 1.35f, speed01) * Mathf.Clamp01(Mathf.Abs(Speed) / 5f) * Mathf.Sign(Speed);
        Heading += yawRate * dt;
        var fwd = new Vector3(Mathf.Sin(Heading), 0, Mathf.Cos(Heading));
        bool drifting = Mathf.Abs(Steer) > 0.72f && speed01 > 0.5f;
        float grip = drifting ? 2.4f : 8f;
        Vel = Vector3.Lerp(Vel, fwd * Speed, 1f - Mathf.Exp(-grip * dt));
        Slip = Vel.sqrMagnitude > 4f ? Vector3.Angle(Vel, fwd * Mathf.Sign(Speed)) : 0f;
        if (drifting && Slip > 6f && raceOn) Nitro = Mathf.Min(100f, Nitro + Slip * dt * 2.6f);
        if (raceOn && !nitro) Nitro = Mathf.Min(100f, Nitro + dt * 2.2f);

        var pos = transform.position + Vel * dt;

        // walls: the curb line is solid
        Index = track.Nearest(pos, Index);
        float lat = track.Lateral(pos, Index);
        float limit = Track.HalfWidth - 1.15f;
        if (Mathf.Abs(lat) > limit)
        {
            var n = track.N[Index] * Mathf.Sign(lat);
            pos -= n * (Mathf.Abs(lat) - limit);
            float into = Vector3.Dot(Vel, n);
            if (into > 0)
            {
                Vel -= n * into * 1.35f;
                if (into > 7f) { Speed *= 0.72f; WallHit = Mathf.Max(WallHit, Mathf.Clamp01(into / 25f)); }
                else Speed *= 1f - dt * 1.2f;
            }
        }
        pos.y = 0;
        transform.position = pos;

        // boost pads
        foreach (var pad in track.BoostPads)
        {
            float ds = Mathf.DeltaAngle(S / track.Length * 360f, pad.s / track.Length * 360f) / 360f * track.Length;
            if (Mathf.Abs(ds) < 3f && Mathf.Abs(lat - pad.lane) < 2f && BoostLeft < 0.9f)
            {
                BoostLeft = 1.3f;
                Speed = Mathf.Max(Speed, Def.top * 1.12f);
                if (Driver == Driver.Player) { Sfx.I.Boost(); Race.I.Shake(0.2f); }
            }
        }
        UpdateLap(track, raceTime);
        Visuals(dt, yawRate, drifting, nitro);
    }

    void UpdateLap(Track track, float raceTime)
    {
        float prevS = S;
        S = track.S(transform.position, Index);
        float len = track.Length;
        if (S > len * 0.4f && S < len * 0.6f) Halfway = true;
        if (prevS > len * 0.8f && S < len * 0.2f && Halfway)
        {
            Halfway = false;
            if (Lap >= 1) { LastLap = raceTime - LapStart; BestLap = Mathf.Min(BestLap, LastLap); }
            Lap++;
            LapStart = raceTime;
            if (Lap > Laps && !Finished) { Finished = true; FinishTime = raceTime; Race.I.OnFinish(this); }
            else if (Driver == Driver.Player && Lap > 1) Race.I.OnLap(this);
        }
        else if (prevS < len * 0.2f && S > len * 0.8f) { Lap = Mathf.Max(0, Lap - 1); Halfway = true; }   // reversed over the line
    }

    void Visuals(float dt, float yawRate, bool drifting, bool nitro)
    {
        transform.rotation = Quaternion.Euler(0, Heading * Mathf.Rad2Deg, 0);
        wheelSpin += Speed * dt / 0.45f * Mathf.Rad2Deg;
        foreach (var w in wheels) w.localRotation = Quaternion.Euler(wheelSpin, frontWheels.Contains(w) ? Steer * 26f : 0, 0);
        roll = Mathf.Lerp(roll, -yawRate * Mathf.Abs(Speed) * 0.09f, 1f - Mathf.Exp(-dt * 6f));
        pitch = Mathf.Lerp(pitch, (Brake > 0.5f ? 2.5f : 0) - (nitro ? 2f : 0), 1f - Mathf.Exp(-dt * 6f));
        body.localRotation = Quaternion.Euler(pitch, 0, Mathf.Clamp(roll, -6f, 6f));
        bool skid = (drifting && Slip > 7f) || (Brake > 0.5f && Speed > 12f);
        skidL.emitting = skidR.emitting = skid;
        flame.color = new Color(0.35f, 0.75f, 1f, nitro || BoostLeft > 0 ? 0.85f : 0f);
        flame.transform.localScale = Vector3.one * (1.4f + Mathf.Sin(Time.time * 40f) * 0.25f);
        flame.transform.rotation = Race.I.Cam.transform.rotation;
        WallHit = Mathf.MoveTowards(WallHit, 0, dt * 3f);
    }

    // ---------------------------------------------------------------- AI
    public void DriveAI(Track track, List<Car> all, Car player, float dt)
    {
        float look = 12f + Mathf.Abs(Speed) * 0.42f;
        laneT -= dt;
        if (laneT <= 0) { laneT = Random.Range(2f, 5f); LaneTarget = Random.Range(-3.8f, 3.8f); }
        // pass slower cars ahead in the same lane
        foreach (var o in all)
        {
            if (o == this) continue;
            float ds = Mathf.Repeat(o.S - S, track.Length);
            if (ds > 1f && ds < 14f && Mathf.Abs(track.Lateral(o.transform.position, o.Index) - Lane) < 2.4f && o.Speed < Speed + 2f)
                LaneTarget = Mathf.Clamp(track.Lateral(o.transform.position, o.Index) + (Lane > 0 ? -4.5f : 4.5f), -4.5f, 4.5f);
        }
        // weave around slow civilian traffic (mostly: the odd rival still plows into it)
        float scan = 26f + Mathf.Abs(Speed) * 0.9f;
        bool dodging = Traffic.I && Traffic.I.Blocked(S, LaneTarget, scan, out _);
        if (dodging) LaneTarget = Traffic.I.BestLane(S, Lane, scan * 1.4f);
        Lane = Mathf.MoveTowards(Lane, LaneTarget, dt * (dodging ? 4.8f : 2.6f));
        var target = track.PointAt(S + look, Lane);
        var fwd = new Vector3(Mathf.Sin(Heading), 0, Mathf.Cos(Heading));
        float ang = Vector3.SignedAngle(fwd, target - transform.position, Vector3.up);
        Steer = Mathf.Clamp(ang / 24f, -1f, 1f);
        float curv = track.Curvature(S + 8f, 30f + Mathf.Abs(Speed) * 0.55f);
        float wantSpeed = Def.top * Skill * Mathf.Clamp(1.12f - curv * 0.62f, 0.42f, 1.05f);
        Throttle = Speed < wantSpeed ? 1 : 0;
        Brake = Speed > wantSpeed + 6f ? 1 : 0;
        NitroPressed = Nitro > 70f && curv < 0.22f;
        // rubber band keeps races close
        if (player != null)
        {
            float gap = player.Progress(track) - Progress(track);
            Skill = Mathf.Lerp(Skill, Mathf.Clamp(BaseSkill + gap / 900f, BaseSkill - 0.08f, BaseSkill + 0.1f), dt * 0.5f);
        }
    }
    public float BaseSkill = 0.95f;

    // ---------------------------------------------------------------- remote
    public void NetUpdate(Vector3 pos, float heading, float speed, int lap, float s)
    {
        if (!hasNet) { transform.position = pos; Heading = heading; }
        netPos = pos; netHeading = heading; netSpeed = speed; hasNet = true;
        Lap = lap; S = s;
    }

    void SimRemote(float dt, Track track)
    {
        if (!hasNet) return;
        // extrapolate along heading, then ease toward the reported position
        netPos += new Vector3(Mathf.Sin(netHeading), 0, Mathf.Cos(netHeading)) * netSpeed * dt;
        transform.position = Vector3.Lerp(transform.position, netPos, 1f - Mathf.Exp(-dt * 10f));
        Heading = Mathf.LerpAngle(Heading * Mathf.Rad2Deg, netHeading * Mathf.Rad2Deg, 1f - Mathf.Exp(-dt * 10f)) * Mathf.Deg2Rad;
        Speed = netSpeed;
        Index = track.Nearest(transform.position, Index);
        Visuals(dt, 0, false, false);
    }
}

using System.Collections.Generic;
using UnityEngine;

// TRAFFIC WEAVE: races run through live city traffic. Skim past a civilian car for a NEAR MISS (nitro, combos),
// hit one and you wreck your speed, bump a rival into one and it's a TAKEDOWN.
public class Traffic : MonoBehaviour
{
    public static Traffic I;
    class Civ
    {
        public Transform t, body; public float s, lane, laneTarget, speed, laneT, halfLen;
        public bool wrecked; public float wreckT, spin, spinV, heading; public Vector3 pos, vel;
        public bool passed;   // near miss already scored on this pass
    }
    class Bit { public Transform t; public Vector3 v, w; public float life; }
    readonly List<Civ> civs = new List<Civ>();
    readonly List<Bit> bits = new List<Bit>();
    public int NearMisses, Takedowns, Crashes, BestCombo;
    int combo; float comboT;
    Track track;

    static readonly string[] Models = { "sedan", "van", "truck", "delivery", "suv", "garbage-truck", "taxi", "hatchback-sports" };
    static readonly string[] Debris = { "debris-tire", "debris-bumper", "debris-door" };
    static readonly float[] Lanes = { -4.4f, -1.5f, 1.5f, 4.4f };

    void Awake() => I = this;

    public void Build(Track tr, bool night)
    {
        Clear();
        track = tr;
        int n = Mathf.Clamp(Mathf.RoundToInt(tr.Length / 68f), 9, 18);
        for (int i = 0; i < n; i++)
        {
            var id = Models[i % Models.Length];
            var root = new GameObject("civ_" + id).transform;
            root.SetParent(transform, false);
            var m = Kit.Spawn("Cars/" + id, 1.65f, root);
            foreach (var r in m.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            Kit.FloorQuad("shadow", Kit.Disc, new Color(0, 0, 0, 0.4f), 4f, root, Vector3.zero, 0.04f).transform.localScale = new Vector3(2.8f, 5.6f, 1);
            if (night) Kit.FloorQuad("tail", Kit.Glow, new Color(1f, 0.15f, 0.2f, 0.45f), 3f, root, new Vector3(0, 0, -2.8f), 0.05f);
            var b = Kit.WorldBounds(m.gameObject);
            var c = new Civ { t = root, body = m.transform, halfLen = Mathf.Max(2.0f, b.extents.z) };
            civs.Add(c);
        }
        Reset();
    }

    // spread traffic around the lap, clear of the starting grid
    public void Reset()
    {
        NearMisses = Takedowns = Crashes = BestCombo = 0; combo = 0;
        if (track == null) return;
        float len = track.Length;
        for (int i = 0; i < civs.Count; i++)
        {
            var c = civs[i];
            c.s = 70f + i * (len - 160f) / civs.Count + Random.Range(-8f, 8f);
            c.lane = c.laneTarget = Lanes[Random.Range(0, Lanes.Length)];
            c.speed = Random.Range(16f, 25f);
            c.laneT = Random.Range(3f, 9f);
            c.wrecked = false; c.passed = false; c.spin = 0;
            Place(c);
        }
        foreach (var b in bits) Destroy(b.t.gameObject);
        bits.Clear();
    }

    public void Clear()
    {
        foreach (var c in civs) if (c.t) Destroy(c.t.gameObject);
        civs.Clear();
        foreach (var b in bits) if (b.t) Destroy(b.t.gameObject);
        bits.Clear();
    }

    void Place(Civ c)
    {
        c.pos = track.PointAt(c.s, c.lane);
        var tan = track.TangentAt(c.s);
        float lean = (c.laneTarget - c.lane) * 4f;
        c.heading = Mathf.Atan2(tan.x, tan.z) * Mathf.Rad2Deg + lean;
        c.t.position = c.pos;
        c.t.rotation = Quaternion.Euler(0, c.heading, 0);
        c.body.localRotation = Quaternion.identity;
    }

    // Is there slow traffic just ahead in this lane? (AI uses it to pick a gap.)
    public bool Blocked(float s, float lane, float range, out float civLane)
    {
        civLane = 0;
        if (track == null) return false;
        foreach (var c in civs)
        {
            if (c.wrecked) continue;
            float ds = Mathf.Repeat(c.s - s, track.Length);
            if (ds > 3f && ds < range && Mathf.Abs(c.lane - lane) < 2.6f) { civLane = c.lane; return true; }
        }
        return false;
    }

    // The lane with the most room ahead (ties go to the lane closest to where we are).
    public float BestLane(float s, float lane, float range)
    {
        float best = lane, bestRoom = -1;
        foreach (var l in Lanes)
        {
            float room = range;
            foreach (var c in civs)
            {
                if (c.wrecked) continue;
                float ds = Mathf.Repeat(c.s - s, track.Length);
                if (ds > -4f && ds < room && Mathf.Abs(c.lane - l) < 2.4f) room = Mathf.Max(0, ds);
            }
            room -= Mathf.Abs(l - lane) * 2f;   // a small cost for swerving far
            if (room > bestRoom) { bestRoom = room; best = l; }
        }
        return best;
    }

    public void Tick(float dt, List<Car> cars, Car player, float raceTime, bool scoring)
    {
        if (track == null) return;
        comboT -= dt;
        if (comboT <= 0) combo = 0;
        foreach (var c in civs)
        {
            if (c.wrecked)
            {
                c.wreckT -= dt;
                c.vel *= Mathf.Exp(-dt * 1.6f);
                c.spinV *= Mathf.Exp(-dt * 1.4f);
                c.pos += c.vel * dt; c.spin += c.spinV * dt;
                c.t.position = c.pos;
                c.t.rotation = Quaternion.Euler(0, c.heading + c.spin, 0);
                if (c.wreckT <= 0) Respawn(c, player);
                continue;
            }
            if ((c.laneT -= dt) <= 0) { c.laneT = Random.Range(4f, 10f); c.laneTarget = Lanes[Random.Range(0, Lanes.Length)]; }
            c.lane = Mathf.MoveTowards(c.lane, c.laneTarget, dt * 1.3f);
            c.s = Mathf.Repeat(c.s + c.speed * dt, track.Length);
            Place(c);
        }

        foreach (var car in cars)
        {
            if (car.Driver == Driver.Remote || car.Finished && car != player) continue;
            var cp = car.transform.position;
            foreach (var c in civs)
            {
                var d = cp - c.pos; d.y = 0;
                if (d.sqrMagnitude > 100f) { if (car == player && d.sqrMagnitude > 400f) c.passed = false; continue; }
                var fwd = new Vector3(Mathf.Sin(c.heading * Mathf.Deg2Rad), 0, Mathf.Cos(c.heading * Mathf.Deg2Rad));
                var right = new Vector3(fwd.z, 0, -fwd.x);
                float along = Vector3.Dot(d, fwd), lat = Vector3.Dot(d, right);
                bool hit = Mathf.Abs(along) < c.halfLen + 1.1f && Mathf.Abs(lat) < 2.2f;
                if (hit && !c.wrecked)
                {
                    Crash(car, c, fwd, right, lat, player, raceTime, scoring);
                    continue;
                }
                // near miss: alongside, close, and going much faster than the civilian
                if (car == player && scoring && !c.wrecked && !c.passed && Mathf.Abs(along) < c.halfLen && Mathf.Abs(lat) < 3.7f && Mathf.Abs(car.Speed) - c.speed > 8f)
                {
                    c.passed = true;
                    combo++; comboT = 3f; NearMisses++; BestCombo = Mathf.Max(BestCombo, combo);
                    car.Nitro = Mathf.Min(100f, car.Nitro + 13f + 4f * Mathf.Min(combo, 5));
                    UI.I.Trick(combo > 1 ? "NEAR MISS  x" + combo : "NEAR MISS!", combo >= 3 ? UI.Gold : UI.Cyan);
                    Sfx.I.Whoosh();
                    WebBridge.Event("near_miss", combo);
                }
            }
        }

        for (int i = bits.Count - 1; i >= 0; i--)
        {
            var b = bits[i];
            b.life -= dt; b.v += Vector3.down * 22f * dt;
            b.t.position += b.v * dt; b.t.Rotate(b.w * dt, Space.World);
            if (b.t.position.y < 0.1f) { var p = b.t.position; p.y = 0.1f; b.t.position = p; b.v = new Vector3(b.v.x * 0.6f, -b.v.y * 0.3f, b.v.z * 0.6f); }
            if (b.life <= 0) { Destroy(b.t.gameObject); bits.RemoveAt(i); }
        }
    }

    void Crash(Car car, Civ c, Vector3 fwd, Vector3 right, float lat, Car player, float raceTime, bool scoring)
    {
        float impact = (car.Vel - fwd * c.speed).magnitude;
        // the civilian spins out of the way
        c.wrecked = true; c.wreckT = 2.6f;
        c.vel = car.Vel * 0.55f + right * -Mathf.Sign(lat) * 6f;
        c.spinV = Random.Range(260f, 520f) * (Random.value < 0.5f ? -1 : 1);
        // the racer loses most of its speed and bounces off
        car.Speed *= 0.38f;
        car.Vel = car.Vel * 0.3f + fwd * c.speed * 0.4f + right * Mathf.Sign(lat) * 4f;
        car.transform.position += right * Mathf.Sign(lat) * 0.6f;
        Burst(c.pos + Vector3.up * 0.8f, car.Vel);
        if (car == player)
        {
            Crashes++; combo = 0;
            car.WallHit = Mathf.Max(car.WallHit, Mathf.Clamp01(impact / 20f) + 0.35f);
            UI.I.Trick("CRASH!", UI.Hot);
            WebBridge.Event("traffic_crash", Crashes);
        }
        else if (scoring && player != null && raceTime - car.BumpedByPlayer < 1.2f)
        {
            // you shoved them into it
            Takedowns++;
            player.Nitro = Mathf.Min(100f, player.Nitro + 45f);
            UI.I.Banner("TAKEDOWN!", car.Name + " WRECKED");
            Sfx.I.Crash(0.9f);
            Race.I.Shake(0.35f);
            WebBridge.Event("takedown", Takedowns);
            car.Speed *= 0.5f;
        }
        else if ((car.transform.position - (player ? player.transform.position : car.transform.position)).sqrMagnitude < 900f) Sfx.I.Crash(0.4f);
    }

    void Burst(Vector3 at, Vector3 vel)
    {
        for (int i = 0; i < 4; i++)
        {
            var go = Kit.Spawn("Cars/" + Debris[i % Debris.Length], 1.4f, transform);
            go.transform.position = at;
            bits.Add(new Bit { t = go.transform, v = vel * 0.4f + new Vector3(Random.Range(-6f, 6f), Random.Range(6f, 11f), Random.Range(-6f, 6f)), w = Random.onUnitSphere * 720f, life = 1.6f });
        }
    }

    void Respawn(Civ c, Car player)
    {
        // somewhere ahead of the player, out of sight
        float baseS = player ? player.S : 0f;
        c.s = Mathf.Repeat(baseS + Random.Range(140f, 260f), track.Length);
        c.lane = c.laneTarget = Lanes[Random.Range(0, Lanes.Length)];
        c.wrecked = false; c.spin = 0; c.passed = false;
        Place(c);
    }
}

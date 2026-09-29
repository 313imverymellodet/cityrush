using System.Collections.Generic;
using UnityEngine;

// A city map: closed spline + theme. The road, curbs, city blocks and props are all generated from it.
public class MapDef
{
    public string id, name, tagline;
    public Vector2[] points;
    public Color sky, fog, ambient, ground, ground2, sun, neon;
    public float sunPitch, sunYaw, sunIntensity;
    public bool downtown;
    public float[] boosts;           // boost pads as fractions of lap length
}

public static class Maps
{
    public static readonly MapDef[] All =
    {
        new MapDef {
            id = "downtown", name = "DOWNTOWN", tagline = "Neon towers. Long straights. One brutal hairpin.", downtown = true,
            points = new[] {
                new Vector2(-120,-90), new Vector2(40,-90), new Vector2(120,-88), new Vector2(158,-62), new Vector2(166,-10),
                new Vector2(164,48), new Vector2(140,80), new Vector2(98,86), new Vector2(66,62), new Vector2(34,70),
                new Vector2(4,104), new Vector2(-44,116), new Vector2(-104,104), new Vector2(-146,70), new Vector2(-158,16), new Vector2(-156,-50),
            },
            sky = Kit.Hex("#1a1033"), fog = Kit.Hex("#2b1a4a"), ambient = Kit.Hex("#6a5aa8"), ground = Kit.Hex("#262532"), ground2 = Kit.Hex("#3a3946"),
            sun = Kit.Hex("#ffb38a"), sunPitch = 28, sunYaw = -60, sunIntensity = 0.85f, neon = Kit.Hex("#ff3db7"),
            boosts = new[] { 0.06f, 0.42f, 0.78f },
        },
        new MapDef {
            id = "suburbs", name = "SUBURBS", tagline = "Sweeping bends through quiet streets. Keep it pinned.", downtown = false,
            points = new[] {
                new Vector2(0,-140), new Vector2(84,-128), new Vector2(146,-84), new Vector2(162,-12), new Vector2(128,46),
                new Vector2(150,108), new Vector2(104,154), new Vector2(36,134), new Vector2(-18,164), new Vector2(-92,152),
                new Vector2(-144,102), new Vector2(-118,40), new Vector2(-162,-20), new Vector2(-142,-92), new Vector2(-80,-134),
            },
            sky = Kit.Hex("#8fd3ff"), fog = Kit.Hex("#bfe6ff"), ambient = Kit.Hex("#c9d8ff"), ground = Kit.Hex("#5f9e45"), ground2 = Kit.Hex("#7cbf55"),
            sun = Kit.Hex("#fff1d6"), sunPitch = 48, sunYaw = 30, sunIntensity = 1.05f, neon = Kit.Hex("#ffd166"),
            boosts = new[] { 0.12f, 0.55f },
        },
    };
    public static MapDef Get(string id) { foreach (var m in All) if (m.id == id) return m; return All[0]; }
}

public class Track : MonoBehaviour
{
    public const float HalfWidth = 7.5f, Sidewalk = 3.5f;
    public MapDef Map;
    public Vector3[] P, T, N;        // samples: position, tangent, right-normal
    public float[] L;                // cumulative length at each sample
    public float Length;
    public int Count;
    public readonly List<(float s, float lane)> BoostPads = new List<(float, float)>();
    public Texture2D MiniMap;
    public Rect MiniBounds;
    Transform root;

    // ---------------------------------------------------------------- spline
    static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    public static Track Build(MapDef map, Transform parent)
    {
        var go = new GameObject("Track_" + map.id);
        go.transform.SetParent(parent, false);
        var tr = go.AddComponent<Track>();
        tr.Map = map;
        tr.root = go.transform;
        tr.Sample();
        tr.BuildRoad();
        tr.BuildCity();
        tr.BuildMiniMap();
        return tr;
    }

    void Sample()
    {
        var pts = Map.points;
        int n = pts.Length;
        var dense = new List<Vector2>();
        for (int i = 0; i < n; i++)
            for (int k = 0; k < 60; k++)
                dense.Add(CatmullRom(pts[(i - 1 + n) % n], pts[i], pts[(i + 1) % n], pts[(i + 2) % n], k / 60f));
        // resample to ~1m spacing
        var outP = new List<Vector3>();
        float acc = 0;
        outP.Add(new Vector3(dense[0].x, 0, dense[0].y));
        for (int i = 1; i <= dense.Count; i++)
        {
            var a = dense[i - 1]; var b = dense[i % dense.Count];
            float d = Vector2.Distance(a, b);
            acc += d;
            while (acc >= 1f) { acc -= 1f; var q = Vector2.Lerp(b, a, acc / Mathf.Max(d, 1e-4f)); outP.Add(new Vector3(q.x, 0, q.y)); }
        }
        Count = outP.Count;
        P = outP.ToArray(); T = new Vector3[Count]; N = new Vector3[Count]; L = new float[Count];
        for (int i = 0; i < Count; i++)
        {
            var t = (P[(i + 1) % Count] - P[(i - 1 + Count) % Count]).normalized;
            T[i] = t; N[i] = new Vector3(t.z, 0, -t.x);
            if (i > 0) L[i] = L[i - 1] + Vector3.Distance(P[i - 1], P[i]);
        }
        Length = L[Count - 1] + Vector3.Distance(P[Count - 1], P[0]);
    }

    // Nearest sample, searched near a hint (cars move a few metres per frame).
    public int Nearest(Vector3 pos, int hint = -1)
    {
        int best = 0; float bd = float.MaxValue;
        if (hint < 0) { for (int i = 0; i < Count; i += 2) { float d = (P[i] - pos).sqrMagnitude; if (d < bd) { bd = d; best = i; } } hint = best; }
        for (int k = -30; k <= 30; k++)
        {
            int i = (hint + k + Count) % Count;
            float d = (P[i] - pos).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    public float Lateral(Vector3 pos, int i) { var d = pos - P[i]; d.y = 0; return Vector3.Dot(d, N[i]); }
    public float S(Vector3 pos, int i) { var d = pos - P[i]; d.y = 0; return Mathf.Repeat(L[i] + Vector3.Dot(d, T[i]), Length); }
    public int IndexAt(float s) { s = Mathf.Repeat(s, Length); int i = Mathf.Clamp((int)(s / Length * Count), 0, Count - 1); while (i > 0 && L[i] > s) i--; while (i < Count - 1 && L[i + 1] <= s) i++; return i; }
    public Vector3 PointAt(float s, float lateral = 0) { int i = IndexAt(s); return P[i] + T[i] * (s - L[i]) + N[i] * lateral; }
    public Vector3 TangentAt(float s) => T[IndexAt(s)];

    // How hard the road turns over the next `ahead` metres (radians).
    public float Curvature(float s, float ahead)
    {
        var a = TangentAt(s); var b = TangentAt(s + ahead * 0.5f); var c = TangentAt(s + ahead);
        return Vector3.Angle(a, b) * Mathf.Deg2Rad + Vector3.Angle(b, c) * Mathf.Deg2Rad;
    }

    // ---------------------------------------------------------------- road mesh
    void BuildRoad()
    {
        var road = Strip("Road", -HalfWidth, HalfWidth, 0.03f, 0.03f, 10f, RoadTexture());
        road.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Glossiness", Map.downtown ? 0.35f : 0.12f);
        var sw = SidewalkTexture();
        Strip("SidewalkR", HalfWidth + 0.35f, HalfWidth + Sidewalk, 0.18f, 0.18f, 4f, sw);
        Strip("SidewalkL", -HalfWidth - Sidewalk, -HalfWidth - 0.35f, 0.18f, 0.18f, 4f, sw);
        var curbTex = CurbTexture();
        Strip("CurbR", HalfWidth, HalfWidth + 0.35f, 0.03f, 0.2f, 3f, curbTex);
        Strip("CurbL", -HalfWidth - 0.35f, -HalfWidth, 0.2f, 0.03f, 3f, curbTex);

        // start / finish line + gantry
        var line = Kit.MeshObject("StartLine", Kit.BuildQuad(1f, 1f));
        line.transform.SetParent(root, false);
        line.transform.position = P[0] + Vector3.up * 0.05f;
        line.transform.rotation = Quaternion.LookRotation(Vector3.down, T[0]);
        line.transform.localScale = new Vector3(HalfWidth * 2f, 2.5f, 1);
        line.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard")) { mainTexture = Checker() };
        for (int s = -1; s <= 1; s += 2)
        {
            var pole = Kit.Spawn("Roads/light-square", 9f, root, P[0] + N[0] * s * (HalfWidth + 1.2f), Mathf.Atan2(-N[0].x * s, -N[0].z * s) * Mathf.Rad2Deg);
        }
        var banner = Kit.MeshObject("Banner", Kit.BuildQuad(1f, 1f));
        banner.transform.SetParent(root, false);
        banner.transform.position = P[0] + Vector3.up * 6.2f;
        banner.transform.rotation = Quaternion.LookRotation(T[0], Vector3.up);
        banner.transform.localScale = new Vector3(HalfWidth * 2f + 2f, 1.6f, 1);
        banner.GetComponent<MeshRenderer>().sharedMaterial = new Material(Kit.UnlitAlpha) { mainTexture = Checker(), color = Color.white };

        // boost pads
        foreach (var f in Map.boosts)
        {
            float s = f * Length;
            for (int k = 0; k < 2; k++)
            {
                float lane = (k == 0 ? -1 : 1) * 3.6f;
                BoostPads.Add((s, lane));
                var pad = Kit.MeshObject("Boost", Kit.BuildQuad(1f, 1f));
                pad.transform.SetParent(root, false);
                pad.transform.position = PointAt(s, lane) + Vector3.up * 0.06f;
                pad.transform.rotation = Quaternion.LookRotation(Vector3.down, TangentAt(s));
                pad.transform.localScale = new Vector3(3.4f, 6f, 1);
                pad.GetComponent<MeshRenderer>().sharedMaterial = new Material(Kit.UnlitAlpha) { mainTexture = Chevron(), color = Map.neon };
                pad.AddComponent<Pulse>();
            }
        }
    }

    // A ribbon following the spline between two lateral offsets.
    GameObject Strip(string name, float a, float b, float ya, float yb, float uvLen, Texture2D tex)
    {
        var v = new Vector3[Count * 2 + 2]; var uv = new Vector2[v.Length]; var tri = new int[Count * 6];
        for (int i = 0; i <= Count; i++)
        {
            int k = i % Count;
            float along = i == Count ? Length : L[k];
            v[i * 2] = P[k] + N[k] * a + Vector3.up * ya;
            v[i * 2 + 1] = P[k] + N[k] * b + Vector3.up * yb;
            uv[i * 2] = new Vector2(0, along / uvLen);
            uv[i * 2 + 1] = new Vector2(1, along / uvLen);
            if (i < Count)
            {
                int o = i * 6, q = i * 2;
                tri[o] = q; tri[o + 1] = q + 2; tri[o + 2] = q + 1;
                tri[o + 3] = q + 1; tri[o + 4] = q + 2; tri[o + 5] = q + 3;
            }
        }
        var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = v, uv = uv, triangles = tri };
        m.RecalculateNormals();
        if (m.normals[0].y < 0) { System.Array.Reverse(tri); m.triangles = tri; m.RecalculateNormals(); }   // always face up
        m.RecalculateBounds();
        var go = Kit.MeshObject(name, m);
        go.transform.SetParent(root, false);
        var mat = new Material(Shader.Find("Standard")) { mainTexture = tex };
        mat.SetFloat("_Glossiness", 0.1f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    Texture2D RoadTexture()
    {
        int w = 256, h = 256; var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
        var px = new Color[w * h]; var rng = new System.Random(3);
        Color asphalt = Map.downtown ? Kit.Hex("#2c2b33") : Kit.Hex("#46474d");
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float u = (x + .5f) / w, vv = (y + .5f) / h;
            float n = (float)rng.NextDouble() * 0.06f - 0.03f;
            Color c = asphalt + new Color(n, n, n, 0);
            bool edge = Mathf.Abs(u - 0.035f) < 0.012f || Mathf.Abs(u - 0.965f) < 0.012f;
            bool dash = (Mathf.Abs(u - 0.25f) < 0.009f || Mathf.Abs(u - 0.75f) < 0.009f) && vv < 0.55f;
            bool mid = Mathf.Abs(u - 0.5f) < 0.02f && Mathf.Abs(u - 0.5f) > 0.007f;
            if (edge || dash) c = Kit.Hex("#e8e8e8");
            if (mid) c = Kit.Hex("#f2c14e");
            c.a = 1; px[y * w + x] = c;
        }
        t.SetPixels(px); t.Apply(true); return t;
    }

    Texture2D SidewalkTexture()
    {
        int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
        var px = new Color[n * n];
        Color a = Map.downtown ? Kit.Hex("#8d8a99") : Kit.Hex("#cfcac0");
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            px[y * n + x] = (y % 32 < 2 || x % 64 < 2) ? a * 0.8f : a;
        t.SetPixels(px); t.Apply(true); return t;
    }

    Texture2D CurbTexture()
    {
        int n = 32; var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = (y < n / 2) ? Kit.Hex("#e23d3d") : Color.white;
        t.SetPixels(px); t.Apply(true); return t;
    }

    static Texture2D Checker()
    {
        int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = ((x / 8 + y / 8) % 2 == 0) ? Color.white : Kit.Hex("#111111");
        t.SetPixels(px); t.Apply(); return t;
    }

    static Texture2D Chevron()
    {
        int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float u = Mathf.Abs(x + .5f - n / 2f) / (n / 2f), v = (y + .5f) / n;
            float band = Mathf.Repeat(v * 3f - u * 0.9f, 1f);
            bool on = band < 0.4f;
            float border = Mathf.Min(u < 0.95f ? 1 : 0, 1);
            px[y * n + x] = new Color(1, 1, 1, on ? 0.95f * border : 0.25f * border);
        }
        t.SetPixels(px); t.Apply(); return t;
    }

    // ---------------------------------------------------------------- city
    bool Clear(Vector3 p, float r)
    {
        // no building may intrude on the road + sidewalk anywhere along the loop
        float need = HalfWidth + Sidewalk + r;
        for (int i = 0; i < Count; i += 3) if ((P[i] - p).sqrMagnitude < need * need) return false;
        return true;
    }

    void BuildCity()
    {
        var city = new GameObject("City").transform;
        city.SetParent(root, false);
        var rng = new System.Random(Map.id.GetHashCode());
        float R() => (float)rng.NextDouble();

        // ground
        var ground = Kit.MeshObject("Ground", Kit.BuildQuad(900f, 60f));
        ground.transform.SetParent(city, false);
        ground.transform.rotation = Quaternion.Euler(90, 0, 0);
        var gm = new Material(Shader.Find("Standard")) { mainTexture = Kit.Noise(128, Map.ground, Map.ground2, 0.08f, 11, 0) };
        gm.SetFloat("_Glossiness", 0.05f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = gm;

        string[] tall = { "Commercial/building-skyscraper-a", "Commercial/building-skyscraper-b", "Commercial/building-skyscraper-c", "Commercial/building-skyscraper-d", "Commercial/building-skyscraper-e" };
        string[] mid = { "Commercial/building-a", "Commercial/building-b", "Commercial/building-c", "Commercial/building-d", "Commercial/building-e", "Commercial/building-f", "Commercial/building-g", "Commercial/building-h", "Commercial/building-i", "Commercial/building-j", "Commercial/building-k", "Commercial/building-l", "Commercial/building-m", "Commercial/building-n" };
        string[] far = { "Commercial/low-detail-building-a", "Commercial/low-detail-building-c", "Commercial/low-detail-building-e", "Commercial/low-detail-building-g", "Commercial/low-detail-building-wide-a", "Commercial/low-detail-building-wide-b" };
        string[] houses = { "Suburban/building-type-a", "Suburban/building-type-b", "Suburban/building-type-c", "Suburban/building-type-d", "Suburban/building-type-e", "Suburban/building-type-f", "Suburban/building-type-g", "Suburban/building-type-h", "Suburban/building-type-i", "Suburban/building-type-j", "Suburban/building-type-k", "Suburban/building-type-l" };

        // front row facing the road, both sides
        float step = Map.downtown ? 13f : 17f;
        for (float s = 0; s < Length; s += step)
        {
            int i = IndexAt(s);
            for (int side = -1; side <= 1; side += 2)
            {
                float depth = Map.downtown ? 7f : 7.5f;
                var p = P[i] + N[i] * side * (HalfWidth + Sidewalk + depth + 1.5f);
                if (!Clear(p, depth * 0.85f)) continue;
                float yaw = Mathf.Atan2(-N[i].x * side, -N[i].z * side) * Mathf.Rad2Deg;   // face the road
                string m = Map.downtown ? (R() < 0.35f ? tall[rng.Next(tall.Length)] : mid[rng.Next(mid.Length)]) : houses[rng.Next(houses.Length)];
                float sc = Map.downtown ? 10f + R() * 2f : 9f + R() * 1.5f;
                Kit.Spawn(m, sc, city, p, yaw + 180f);
                if (!Map.downtown && R() < 0.7f)
                {
                    var tp = P[i] + N[i] * side * (HalfWidth + Sidewalk + 0.8f) + T[i] * (step * 0.5f);
                    if (Clear(tp, 0.4f) || true) Kit.Spawn("Suburban/tree-large", 7f + R() * 3f, city, tp, R() * 360f);
                }
            }
        }
        // back rows: skyline downtown, more houses and trees in the burbs
        for (int k = 0; k < (Map.downtown ? 260 : 220); k++)
        {
            var p = new Vector3((R() - 0.5f) * 520f, 0, (R() - 0.5f) * 520f);
            if (!Clear(p, 12f)) continue;
            if (Map.downtown) Kit.Spawn(R() < 0.5f ? tall[rng.Next(tall.Length)] : far[rng.Next(far.Length)], 11f + R() * 6f, city, p, rng.Next(4) * 90f);
            else if (R() < 0.55f) Kit.Spawn("Suburban/tree-large", 8f + R() * 5f, city, p, R() * 360f);
            else Kit.Spawn(houses[rng.Next(houses.Length)], 9f, city, p, rng.Next(4) * 90f);
        }

        // street furniture along the sidewalks
        for (float s = 5f; s < Length; s += 26f)
        {
            int i = IndexAt(s);
            int side = ((int)(s / 26f)) % 2 == 0 ? 1 : -1;
            var p = P[i] + N[i] * side * (HalfWidth + 1.2f);
            float yaw = Mathf.Atan2(-N[i].x * side, -N[i].z * side) * Mathf.Rad2Deg;
            Kit.Spawn("Roads/light-curved", 10f, city, p + Vector3.up * 0.18f, yaw + 180f);
            var glow = Kit.FloorQuad("lamp", Kit.Glow, Kit.A(Map.downtown ? Kit.Hex("#ffd49a") : Kit.Hex("#fff3c4"), Map.downtown ? 0.5f : 0.18f), 10f, city, P[i] + N[i] * side * (HalfWidth - 2.2f), 0.05f);
        }
        if (Map.downtown)
            for (float s = 40f; s < Length; s += 90f)
            {
                int i = IndexAt(s);
                for (int side = -1; side <= 1; side += 2)
                    Kit.Spawn("Roads/construction-barrier", 9f, city, P[i] + N[i] * side * (HalfWidth + 0.9f) + Vector3.up * 0.18f, Mathf.Atan2(T[i].x, T[i].z) * Mathf.Rad2Deg);
            }

        StaticBatchingUtility.Combine(city.gameObject);
    }

    // ---------------------------------------------------------------- minimap
    void BuildMiniMap()
    {
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var p in P) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
        float size = Mathf.Max(maxX - minX, maxZ - minZ) + 30f;
        float cx = (minX + maxX) / 2f, cz = (minZ + maxZ) / 2f;
        MiniBounds = new Rect(cx - size / 2f, cz - size / 2f, size, size);
        int n = 256; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color[n * n];
        foreach (var p in P)
        {
            int x = (int)((p.x - MiniBounds.x) / size * n), y = (int)((p.z - MiniBounds.y) / size * n);
            for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= n || yy >= n || dx * dx + dy * dy > 10) continue;
                px[yy * n + xx] = new Color(1, 1, 1, 0.9f);
            }
        }
        t.SetPixels(px); t.Apply();
        MiniMap = t;
    }

    public Vector2 MiniPos(Vector3 world) => new Vector2((world.x - MiniBounds.x) / MiniBounds.width, (world.z - MiniBounds.y) / MiniBounds.height);
}

public class Pulse : MonoBehaviour
{
    Material m; Color c;
    void Start() { m = GetComponent<MeshRenderer>().sharedMaterial; c = m.color; }
    void Update() { m.color = Kit.A(c, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 4f))); m.mainTextureOffset = new Vector2(0, -Time.time * 1.5f); }
}

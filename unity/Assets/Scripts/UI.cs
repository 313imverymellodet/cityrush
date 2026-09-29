using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    public static UI I;
    Canvas canvas; CanvasScaler scaler;
    RectTransform root, hud, screens, controls, tags;
    Font F => Kit.Font;
    public static readonly Color Ink = Kit.Hex("#0d0b1a"), Hot = Kit.Hex("#FF3D7F"), Cyan = Kit.Hex("#2EE6FF"), Gold = Kit.Hex("#FFD166"), Lime = Kit.Hex("#9BFF5C"), Soft = new Color(1, 1, 1, 0.65f);

    // input exposed to Race
    public float SteerInput => steer.Value;
    public bool BrakeInput => brakeBtn && brakeBtn.Held;
    public bool NitroInput => nitroBtn && nitroBtn.Held;
    SteerZone steer; HoldButton brakeBtn, nitroBtn;
    Image arrowL, arrowR, nitroFill, nitroRing;

    // hud
    Text posText, posOf, lapText, timeText, bestText, speedText, bannerText, bannerSub, countText, toastText, wrongText, rankText, keysHint;
    RawImage mini; RectTransform miniRt;
    readonly List<Image> dots = new List<Image>();
    float bannerT, countT, toastT, wrongT;
    readonly List<(Transform t, Text x)> nameTags = new List<(Transform, Text)>();

    static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    public static Sprite Icon(string n) { if (!icons.TryGetValue(n, out var s)) icons[n] = s = Resources.Load<Sprite>("Icons/" + n); return s; }
    static Sprite disc, ring, arrow;
    static Sprite Spr(Texture2D t) => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f));

    public void Init()
    {
        I = this;
        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>().pixelDragThreshold = 2; es.AddComponent<StandaloneInputModule>();
        canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920);
        gameObject.AddComponent<GraphicRaycaster>();
        root = (RectTransform)transform;
        disc = Spr(Kit.Disc); ring = Spr(Kit.Ring); arrow = Spr(Chevron());

        tags = Fill("tags", root);
        BuildHud();
        screens = Fill("screens", root);

        countText = Txt(root, "", 260, new Vector2(.5f, .58f), Vector2.zero, Color.white, TextAnchor.MiddleCenter, 1000);
        countText.fontStyle = FontStyle.BoldAndItalic; Outline(countText, 6); countText.gameObject.SetActive(false);
        bannerText = Txt(root, "", 110, new Vector2(.5f, .66f), Vector2.zero, Gold, TextAnchor.MiddleCenter, 1400);
        bannerText.fontStyle = FontStyle.BoldAndItalic; Outline(bannerText, 5);
        bannerSub = Txt(root, "", 48, new Vector2(.5f, .66f), new Vector2(0, -100), Color.white, TextAnchor.MiddleCenter, 1400);
        Outline(bannerSub, 3);
        bannerText.gameObject.SetActive(false); bannerSub.gameObject.SetActive(false);
        toastText = Txt(root, "", 38, new Vector2(.5f, 1), new Vector2(0, -330), Color.white, TextAnchor.MiddleCenter, 1200);
        Outline(toastText, 3); toastText.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- building blocks
    RectTransform Rect(string n, Transform p, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }
    RectTransform Fill(string n, Transform p)
    {
        var rt = Rect(n, p, Vector2.zero, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; return rt;
    }
    RectTransform Box(Transform p, Vector2 anchor, Vector2 pos, Vector2 size, Color c, bool ray = false)
    {
        var rt = Rect("box", p, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>(); img.sprite = Kit.RoundedSprite; img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = ray;
        return rt;
    }
    Image Img(Transform p, Sprite s, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = Rect("img", p, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>(); img.sprite = s; img.preserveAspect = true; img.raycastTarget = false; return img;
    }
    Text Txt(Transform p, string s, int size, Vector2 anchor, Vector2 pos, Color c, TextAnchor align = TextAnchor.MiddleCenter, float w = 700)
    {
        var rt = Rect("txt", p, anchor, pos, new Vector2(w, size * 1.4f));
        var t = rt.gameObject.AddComponent<Text>();
        t.font = F; t.fontSize = size; t.fontStyle = FontStyle.Bold; t.alignment = align; t.color = c; t.text = s;
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
    static void Outline(Text t, float d) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.8f); o.effectDistance = new Vector2(d, -d); }
    Button Btn(Transform p, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, Color fg, Action onClick, int fs = 48)
    {
        var rt = Box(p, anchor, pos, size, bg, true);
        var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = rt.GetComponent<Image>();
        b.onClick.AddListener(() => { Sfx.I.Click(); onClick(); });
        var t = Txt(rt, label, fs, new Vector2(.5f, .5f), Vector2.zero, fg, TextAnchor.MiddleCenter, size.x);
        t.fontStyle = FontStyle.BoldAndItalic;
        return b;
    }

    // ---------------------------------------------------------------- HUD
    void BuildHud()
    {
        hud = Fill("hud", root);
        controls = Fill("controls", hud);

        // left half: slide a thumb to steer
        var zone = Rect("steer", controls, Vector2.zero, Vector2.zero, Vector2.zero);
        zone.anchorMin = Vector2.zero; zone.anchorMax = new Vector2(0.5f, 0.78f); zone.offsetMin = zone.offsetMax = Vector2.zero;
        var zi = zone.gameObject.AddComponent<Image>(); zi.color = new Color(0, 0, 0, 0);
        steer = zone.gameObject.AddComponent<SteerZone>();
        arrowL = Img(controls, arrow, new Vector2(.12f, 0), new Vector2(0, 170), new Vector2(170, 170));
        arrowL.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
        arrowR = Img(controls, arrow, new Vector2(.36f, 0), new Vector2(0, 170), new Vector2(170, 170));

        // right side: nitro + brake
        var n = Rect("nitro", controls, new Vector2(1, 0), new Vector2(-190, 210), new Vector2(260, 260));
        var nImg = n.gameObject.AddComponent<Image>(); nImg.sprite = disc; nImg.color = new Color(0.1f, 0.3f, 0.6f, 0.55f);
        nitroBtn = n.gameObject.AddComponent<HoldButton>();
        nitroFill = Img(n, disc, new Vector2(.5f, .5f), Vector2.zero, new Vector2(240, 240));
        nitroFill.type = Image.Type.Filled; nitroFill.fillMethod = Image.FillMethod.Vertical; nitroFill.preserveAspect = false;
        nitroFill.color = Kit.A(Cyan, 0.75f);
        nitroRing = Img(n, ring, new Vector2(.5f, .5f), Vector2.zero, new Vector2(270, 270)); nitroRing.color = Cyan;
        var nt = Txt(n, "NITRO", 46, new Vector2(.5f, .5f), Vector2.zero, Color.white); nt.fontStyle = FontStyle.BoldAndItalic; Outline(nt, 3);

        var b = Rect("brake", controls, new Vector2(1, 0), new Vector2(-440, 130), new Vector2(190, 150));
        var bImg = b.gameObject.AddComponent<Image>(); bImg.sprite = Kit.RoundedSprite; bImg.type = Image.Type.Sliced; bImg.color = new Color(0.6f, 0.05f, 0.15f, 0.5f);
        brakeBtn = b.gameObject.AddComponent<HoldButton>();
        var bt = Txt(b, "BRAKE", 38, new Vector2(.5f, .5f), Vector2.zero, Color.white); bt.fontStyle = FontStyle.BoldAndItalic; Outline(bt, 2);
        keysHint = Txt(hud, "STEER  A/D or ARROWS     NITRO  SPACE     BRAKE  S", 26, new Vector2(.5f, 0), new Vector2(0, 40), new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter, 1200);

        // top-left: position
        posText = Txt(hud, "1", 150, new Vector2(0, 1), new Vector2(120, -130), Color.white, TextAnchor.MiddleCenter, 240);
        posText.fontStyle = FontStyle.BoldAndItalic; Outline(posText, 5);
        posOf = Txt(hud, "/6", 50, new Vector2(0, 1), new Vector2(255, -160), Soft, TextAnchor.MiddleLeft, 200);
        posOf.fontStyle = FontStyle.BoldAndItalic; Outline(posOf, 3);

        // top-center: lap + time
        lapText = Txt(hud, "LAP 1/3", 54, new Vector2(.5f, 1), new Vector2(0, -80), Gold, TextAnchor.MiddleCenter, 600);
        lapText.fontStyle = FontStyle.BoldAndItalic; Outline(lapText, 3);
        timeText = Txt(hud, "0:00.00", 46, new Vector2(.5f, 1), new Vector2(0, -145), Color.white, TextAnchor.MiddleCenter, 600);
        Outline(timeText, 3);
        bestText = Txt(hud, "", 30, new Vector2(.5f, 1), new Vector2(0, -195), Soft, TextAnchor.MiddleCenter, 600);
        Outline(bestText, 2);
        wrongText = Txt(hud, "WRONG WAY!", 90, new Vector2(.5f, .5f), new Vector2(0, 220), Hot, TextAnchor.MiddleCenter, 1200);
        wrongText.fontStyle = FontStyle.BoldAndItalic; Outline(wrongText, 5); wrongText.gameObject.SetActive(false);

        // top-right: minimap
        var mbg = Box(hud, new Vector2(1, 1), new Vector2(-170, -270), new Vector2(300, 300), new Color(0, 0, 0, 0.35f));
        miniRt = Rect("mini", mbg, new Vector2(.5f, .5f), Vector2.zero, new Vector2(280, 280));
        mini = miniRt.gameObject.AddComponent<RawImage>(); mini.raycastTarget = false;
        for (int i = 0; i < 8; i++) { var d = Img(miniRt, disc, Vector2.zero, Vector2.zero, new Vector2(22, 22)); d.gameObject.SetActive(false); dots.Add(d); }

        // speed under the minimap
        speedText = Txt(hud, "0", 80, new Vector2(1, 1), new Vector2(-270, -470), Color.white, TextAnchor.MiddleRight, 300);
        speedText.fontStyle = FontStyle.BoldAndItalic; Outline(speedText, 4);
        var kmh = Txt(hud, "KM/H", 30, new Vector2(1, 1), new Vector2(-70, -482), Soft, TextAnchor.MiddleCenter, 120);
        Outline(kmh, 2);

        Btn(hud, "II", new Vector2(0, 1), new Vector2(80, -290), new Vector2(100, 100), new Color(0, 0, 0, 0.45f), Color.white, () => Race.I.Pause(), 44);
        hud.gameObject.SetActive(false);
    }

    // right-pointing double chevron for the steering hints
    static Texture2D Chevron()
    {
        int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float u = (x + .5f) / n, v = Mathf.Abs((y + .5f) / n - 0.5f);
            float a = 0;
            foreach (var o in new[] { 0.12f, 0.42f })
            {
                float d = Mathf.Abs(u - o - (0.36f - v) * 0.9f);      // ">" stroke, tip on the right
                if (v < 0.36f) a = Mathf.Max(a, Mathf.Clamp01((0.09f - d) * 40f));
            }
            px[y * n + x] = new Color(1, 1, 1, a);
        }
        t.SetPixels(px); t.Apply(); return t;
    }

    public void ShowHud(bool on)
    {
        hud.gameObject.SetActive(on);
        if (!on) ClearTags();
        keysHint.gameObject.SetActive(!Application.isMobilePlatform);
    }

    public void SetMiniMap(Texture2D t) { if (mini) mini.texture = t; }

    public void UpdateHud(Car me, List<Car> order, float time, Track track)
    {
        int pos = order.IndexOf(me) + 1;
        posText.text = pos.ToString();
        posText.color = pos == 1 ? Gold : Color.white;
        posOf.text = "/" + order.Count;
        int lap = Mathf.Clamp(me.Lap, 1, Car.Laps);
        lapText.text = me.Finished ? "FINISHED" : lap == Car.Laps && me.Lap > 0 ? "FINAL LAP" : "LAP " + lap + "/" + Car.Laps;
        timeText.text = Time(me.Finished ? me.FinishTime : time);
        bestText.text = me.BestLap < 9999f ? "BEST LAP  " + Time(me.BestLap) : "";
        speedText.text = Mathf.RoundToInt(Mathf.Abs(me.Speed) * 3.6f).ToString();
        float nitro01 = me.NitroLeft > 0 ? 1f : me.Nitro / 100f;
        nitroFill.fillAmount = nitro01;
        bool ready = me.Nitro >= 25f || me.NitroLeft > 0;
        nitroRing.color = ready ? Color.Lerp(Cyan, Color.white, Mathf.PingPong(UnityEngine.Time.time * (me.Nitro >= 99f ? 4f : 1.5f), 1f) * 0.6f) : new Color(1, 1, 1, 0.25f);
        arrowL.color = new Color(1, 1, 1, SteerInput < -0.2f ? 0.85f : 0.3f);
        arrowR.color = new Color(1, 1, 1, SteerInput > 0.2f ? 0.85f : 0.3f);

        // minimap dots
        var size = miniRt.rect.size;
        for (int i = 0; i < dots.Count; i++)
        {
            bool on = i < order.Count;
            dots[i].gameObject.SetActive(on);
            if (!on) continue;
            var c = order[i];
            var mp = track.MiniPos(c.transform.position);
            dots[i].rectTransform.anchoredPosition = new Vector2(mp.x * size.x, mp.y * size.y);
            bool isMe = c == me;
            dots[i].color = isMe ? Gold : c.Driver == Driver.Remote ? Cyan : Hot;
            dots[i].rectTransform.sizeDelta = Vector2.one * (isMe ? 30 : 20);
            if (isMe) dots[i].transform.SetAsLastSibling();
        }

        // wrong way: facing against the track for a moment
        var fwd = new Vector3(Mathf.Sin(me.Heading), 0, Mathf.Cos(me.Heading));
        bool wrong = Vector3.Dot(fwd, track.T[me.Index]) < -0.3f && !me.Finished && Race.I.State == Race.St.Racing;
        wrongT = wrong ? wrongT + UnityEngine.Time.deltaTime : 0;
        wrongText.gameObject.SetActive(wrongT > 1f && Mathf.PingPong(UnityEngine.Time.time * 3f, 1f) > 0.3f);
    }

    public void CountdownNumber(string s, bool go)
    {
        countText.text = s; countText.color = go ? Lime : s == "1" ? Gold : Color.white;
        countT = go ? 0.9f : 0.85f;
        countText.gameObject.SetActive(true);
    }

    public void Banner(string title, string sub)
    {
        bannerText.text = title; bannerSub.text = sub; bannerT = 2.3f;
        bannerText.gameObject.SetActive(true); bannerSub.gameObject.SetActive(!string.IsNullOrEmpty(sub));
    }

    public void Toast(string s) { toastText.text = s; toastT = 2.6f; toastText.gameObject.SetActive(true); }

    public void NameTag(Transform t, string name)
    {
        var x = Txt(tags, name, 30, Vector2.zero, Vector2.zero, Cyan, TextAnchor.MiddleCenter, 400);
        Outline(x, 2);
        nameTags.Add((t, x));
    }
    void ClearTags() { foreach (var n in nameTags) if (n.x) Destroy(n.x.gameObject); nameTags.Clear(); }

    // ---------------------------------------------------------------- screens
    RectTransform Screen(bool dim = true)
    {
        foreach (Transform c in screens) Destroy(c.gameObject);
        var s = Fill("screen", screens);
        if (dim) { var img = s.gameObject.AddComponent<Image>(); img.color = new Color(0.03f, 0.02f, 0.08f, 0.8f); }
        return s;
    }

    public void CloseScreens() { foreach (Transform c in screens) Destroy(c.gameObject); }

    IEnumerator Pop(RectTransform r, float delay = 0)
    {
        r.localScale = Vector3.zero;
        float k = -delay / 0.28f;
        while (k < 1f) { k += UnityEngine.Time.unscaledDeltaTime / 0.28f; r.localScale = Vector3.one * Kit.EaseOutBack(Mathf.Clamp01(k)); yield return null; }
        r.localScale = Vector3.one;
    }
    IEnumerator Pulse(Transform t)
    {
        while (t) { t.localScale = Vector3.one * (1f + Mathf.Sin(UnityEngine.Time.unscaledTime * 4f) * 0.035f); yield return null; }
    }

    Text Title(Transform p, string s, float y, int size, Color c)
    {
        Txt(p, s, size, new Vector2(.5f, 1), new Vector2(8, y - 8), new Color(0, 0, 0, 0.55f), TextAnchor.MiddleCenter, 1400).fontStyle = FontStyle.BoldAndItalic;
        var t = Txt(p, s, size, new Vector2(.5f, 1), new Vector2(0, y), c, TextAnchor.MiddleCenter, 1400);
        t.fontStyle = FontStyle.BoldAndItalic;
        return t;
    }

    public void ShowMenu()
    {
        ShowHud(false);
        var s = Screen(false);
        var r = Race.I;
        // soft gradient so text reads over the city flyover
        var shade = Box(s, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0.02f, 0.01f, 0.06f, 0.45f));
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.sizeDelta = Vector2.zero;
        Title(s, "CITY", -210, 190, Color.white);
        Title(s, "RUSH", -390, 190, Hot);
        var tag = Txt(s, "STREET RACING  -  SOLO OR ONLINE", 34, new Vector2(.5f, 1), new Vector2(0, -510), Cyan, TextAnchor.MiddleCenter, 1000);
        Outline(tag, 2);

        // map cards
        for (int i = 0; i < Maps.All.Length; i++)
        {
            var m = Maps.All[i];
            bool sel = m.id == r.Map.id;
            var card = Box(s, new Vector2(.5f, 1), new Vector2(i == 0 ? -240 : 240, -700), new Vector2(450, 250), sel ? Kit.A(m.neon, 0.9f) : new Color(1, 1, 1, 0.12f), true);
            var nm = Txt(card, m.name, 54, new Vector2(.5f, .5f), new Vector2(0, 55), sel ? Ink : Color.white, TextAnchor.MiddleCenter, 440);
            nm.fontStyle = FontStyle.BoldAndItalic;
            var tl = Txt(card, m.tagline, 25, new Vector2(.5f, .5f), new Vector2(0, -8), sel ? Kit.A(Ink, 0.8f) : Soft, TextAnchor.MiddleCenter, 400);
            tl.horizontalOverflow = HorizontalWrapMode.Wrap; tl.rectTransform.sizeDelta = new Vector2(400, 70); tl.fontStyle = FontStyle.Normal;
            float best = r.Save.Best(m.id);
            Txt(card, best > 0 ? "BEST " + Time(best) : "NO TIME YET", 30, new Vector2(.5f, .5f), new Vector2(0, -70), sel ? Ink : Gold, TextAnchor.MiddleCenter, 430);
            var btn = card.gameObject.AddComponent<Button>(); btn.targetGraphic = card.GetComponent<Image>();
            var id = m.id;
            btn.onClick.AddListener(() => { if (id == Race.I.Map.id) return; Sfx.I.Click(); Race.I.SelectMap(id); });
        }

        // car carousel
        var def = Cars.Get(r.Save.car);
        var cbox = Box(s, new Vector2(.5f, 1), new Vector2(0, -1080), new Vector2(940, 400), new Color(0, 0, 0, 0.45f));
        Img(cbox, Icon(def.id), new Vector2(.5f, .5f), new Vector2(-215, 0), new Vector2(420, 420));
        var cn = Txt(cbox, def.name, 56, new Vector2(.5f, .5f), new Vector2(180, 120), Color.white, TextAnchor.MiddleCenter, 460);
        cn.fontStyle = FontStyle.BoldAndItalic;
        Stat(cbox, "SPEED", Mathf.InverseLerp(50, 67, def.top), 40);
        Stat(cbox, "ACCEL", Mathf.InverseLerp(17, 25, def.accel), -30);
        Stat(cbox, "GRIP", Mathf.InverseLerp(0.9f, 1.16f, def.handling), -100);
        Btn(cbox, "<", new Vector2(0, .5f), new Vector2(-10, 0), new Vector2(110, 150), new Color(1, 1, 1, 0.16f), Color.white, () => CycleCar(-1), 70);
        Btn(cbox, ">", new Vector2(1, .5f), new Vector2(10, 0), new Vector2(110, 150), new Color(1, 1, 1, 0.16f), Color.white, () => CycleCar(1), 70);

        var race = Btn(s, "RACE", new Vector2(.5f, 0), new Vector2(-235, 540), new Vector2(450, 170), Hot, Color.white, () => r.StartSolo(), 76);
        StartCoroutine(Pulse(race.transform));
        Btn(s, "ONLINE", new Vector2(.5f, 0), new Vector2(235, 540), new Vector2(450, 170), Cyan, Ink, () => r.OpenOnline(), 66);
        Btn(s, "LEADERBOARD", new Vector2(.5f, 0), new Vector2(0, 370), new Vector2(920, 120), new Color(1, 1, 1, 0.16f), Gold, () => WebBridge.ShowBoard(Race.I.Map.id), 46);
        Btn(s, "HOW TO PLAY", new Vector2(.5f, 0), new Vector2(-235, 225), new Vector2(450, 110), new Color(1, 1, 1, 0.12f), Color.white, ShowHowTo, 38);
        Btn(s, r.Save.muted ? "SOUND OFF" : "SOUND ON", new Vector2(.5f, 0), new Vector2(235, 225), new Vector2(450, 110), new Color(1, 1, 1, 0.12f), Color.white, () => { r.ToggleMute(); ShowMenu(); }, 38);
        if (r.Save.races > 0)
            Txt(s, r.Save.races + " RACES   -   " + r.Save.wins + " WINS", 30, new Vector2(.5f, 0), new Vector2(0, 120), Soft, TextAnchor.MiddleCenter, 900);
    }

    void Stat(Transform p, string label, float v, float y)
    {
        Txt(p, label, 28, new Vector2(.5f, .5f), new Vector2(95, y), Soft, TextAnchor.MiddleLeft, 130);
        var bg = Box(p, new Vector2(.5f, .5f), new Vector2(300, y), new Vector2(230, 22), new Color(1, 1, 1, 0.15f));
        var f = Box(bg, new Vector2(0, .5f), new Vector2(0, 0), new Vector2(230 * Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(v)), 22), Lime);
        f.pivot = new Vector2(0, .5f);
    }

    void CycleCar(int d)
    {
        var all = Cars.All; int i = 0;
        for (int k = 0; k < all.Length; k++) if (all[k].id == Race.I.Save.car) i = k;
        Race.I.Save.car = all[(i + d + all.Length) % all.Length].id;
        Race.I.Persist();
        ShowMenu();
    }

    public void ShowHowTo()
    {
        var s = Screen();
        Title(s, "HOW TO PLAY", -240, 100, Gold);
        string[] rows =
        {
            "GAS IS AUTOMATIC - you just steer",
            "STEER: slide your thumb on the LEFT side\n(keyboard: A / D or LEFT / RIGHT)",
            "DRIFT: hold a hard turn at speed\nto charge your NITRO bar",
            "NITRO: tap the blue button (SPACE)",
            "BLUE ARROW PADS = free speed boost",
            "BRAKE: hold BRAKE (S / DOWN) for tight turns",
            "3 LAPS. Win the race, set a record,\nclimb the leaderboard for each city",
        };
        for (int i = 0; i < rows.Length; i++)
        {
            var row = Box(s, new Vector2(.5f, 1), new Vector2(0, -420 - i * 175), new Vector2(940, 155), new Color(1, 1, 1, 0.08f));
            var t = Txt(row, rows[i], 36, new Vector2(.5f, .5f), Vector2.zero, Color.white, TextAnchor.MiddleCenter, 900);
            t.lineSpacing = 1.1f;
            StartCoroutine(Pop(row, 0.05f * i));
        }
        Btn(s, "LET'S GO", new Vector2(.5f, 0), new Vector2(0, 170), new Vector2(560, 150), Hot, Color.white, () =>
        {
            Race.I.Save.howto = true; Race.I.Persist();
            if (Race.I.State == Race.St.Menu) ShowMenu(); else CloseScreens();
        }, 60);
    }

    public void ShowPause()
    {
        var s = Screen();
        Title(s, Race.I.Online ? "MENU" : "PAUSED", -520, 120, Color.white);
        if (Race.I.Online) Txt(s, "Online races keep going!", 36, new Vector2(.5f, 1), new Vector2(0, -640), Soft);
        Btn(s, "RESUME", new Vector2(.5f, .5f), new Vector2(0, 140), new Vector2(600, 160), Lime, Ink, () => Race.I.Resume(), 64);
        if (!Race.I.Online)
            Btn(s, "RESTART", new Vector2(.5f, .5f), new Vector2(0, -60), new Vector2(600, 130), new Color(1, 1, 1, 0.15f), Color.white, () => { Race.I.Resume(); Race.I.StartSolo(); }, 48);
        Btn(s, "QUIT RACE", new Vector2(.5f, .5f), new Vector2(0, -230), new Vector2(600, 130), new Color(1, 1, 1, 0.15f), Hot, () => { Race.I.Resume(); Race.I.Quit(); }, 48);
    }

    public void ShowResults(List<Car> order, Car me, bool online)
    {
        hud.gameObject.SetActive(false);
        var s = Screen();
        var r = Race.I;
        int place = order.IndexOf(me) + 1;
        Title(s, place == 1 ? "VICTORY!" : "P" + place, -230, 150, place == 1 ? Gold : place <= 3 ? Cyan : Color.white);
        Txt(s, r.Map.name + "  -  " + Time(me.FinishTime) + (me.BestLap < 9999 ? "   BEST LAP " + Time(me.BestLap) : ""), 36, new Vector2(.5f, 1), new Vector2(0, -360), Color.white, TextAnchor.MiddleCenter, 1000);
        bool pb = Mathf.Abs(r.Save.Best(r.Map.id) - me.FinishTime) < 0.001f;
        rankText = Txt(s, pb ? "NEW PERSONAL BEST!" : "PERSONAL BEST  " + Time(r.Save.Best(r.Map.id)), 34, new Vector2(.5f, 1), new Vector2(0, -420), pb ? Lime : Soft, TextAnchor.MiddleCenter, 1000);
        if (lastRank != null) ApplyRank();

        for (int i = 0; i < order.Count; i++)
        {
            var c = order[i];
            bool isMe = c == me;
            var row = Box(s, new Vector2(.5f, 1), new Vector2(0, -540 - i * 105), new Vector2(920, 92), isMe ? Kit.A(Hot, 0.55f) : new Color(1, 1, 1, i % 2 == 0 ? 0.1f : 0.05f));
            var pt = Txt(row, (i + 1).ToString(), 48, new Vector2(0, .5f), new Vector2(60, 0), i == 0 ? Gold : Color.white, TextAnchor.MiddleCenter, 100);
            pt.fontStyle = FontStyle.BoldAndItalic;
            Img(row, Icon(c.Def.id), new Vector2(0, .5f), new Vector2(165, 0), new Vector2(110, 110));
            Txt(row, c.Name + (c.Driver == Driver.Remote ? "" : c == me ? "" : "  (AI)"), 38, new Vector2(0, .5f), new Vector2(430, 0), c.Driver == Driver.Remote ? Cyan : Color.white, TextAnchor.MiddleLeft, 420);
            string tm = c.Finished ? Time(c.FinishTime) : online ? "RACING..." : "DNF";
            Txt(row, tm, 38, new Vector2(1, .5f), new Vector2(-130, 0), Color.white, TextAnchor.MiddleRight, 260);
            StartCoroutine(Pop(row, 0.06f * i));
        }

        float y = 470;
        var again = Btn(s, online ? "RACE ONLINE" : "RACE AGAIN", new Vector2(.5f, 0), new Vector2(0, y), new Vector2(640, 160), Hot, Color.white, () => { if (online) r.OpenOnline(); else r.StartSolo(); }, 60);
        StartCoroutine(Pulse(again.transform));
        var share = Btn(s, "SHARE", new Vector2(.5f, 0), new Vector2(-320, y - 175), new Vector2(290, 120), new Color(1, 1, 1, 0.16f), Color.white, () => { }, 42);
        share.gameObject.AddComponent<ShareOnPress>().Text = () => r.ShareText();
        var other = Maps.Get(r.Map.id == "downtown" ? "suburbs" : "downtown");
        Btn(s, other.name, new Vector2(.5f, 0), new Vector2(0, y - 175), new Vector2(290, 120), new Color(1, 1, 1, 0.16f), other.neon, () => { r.SelectMap(other.id); }, 36);
        Btn(s, "MENU", new Vector2(.5f, 0), new Vector2(320, y - 175), new Vector2(290, 120), new Color(1, 1, 1, 0.16f), Color.white, () => r.Quit(), 42);
        Btn(s, "LEADERBOARD", new Vector2(.5f, 0), new Vector2(0, y - 320), new Vector2(920, 110), new Color(1, 1, 1, 0.1f), Gold, () => WebBridge.ShowBoard(r.Map.id), 40);
    }

    Race.RankMsg lastRank;
    public void SetRank(Race.RankMsg m) { lastRank = m; ApplyRank(); }
    public void ClearRank() => lastRank = null;
    void ApplyRank()
    {
        if (!rankText || lastRank == null) return;
        if (lastRank.rank > 0)
        {
            rankText.text = "WORLD RANK  #" + lastRank.rank + " OF " + lastRank.total + (lastRank.newBest ? "   NEW RECORD!" : "   BEST " + Time(lastRank.best / 1000f));
            rankText.color = lastRank.rank <= 10 ? Gold : Lime;
        }
    }

    public static string Time(float secs)
    {
        if (secs <= 0 || secs > 5999) return "-:--.--";
        int m = Mathf.FloorToInt(secs / 60f);
        float s = secs - m * 60;
        return m + ":" + s.ToString("00.00");
    }

    // ---------------------------------------------------------------- per frame
    void Update()
    {
        float aspect = (float)UnityEngine.Screen.width / Mathf.Max(1, UnityEngine.Screen.height);
        scaler.matchWidthOrHeight = aspect > 0.75f ? 1f : 0f;
        float udt = UnityEngine.Time.unscaledDeltaTime;

        if (countT > 0)
        {
            countT -= udt;
            float k = 1f - countT / 0.85f;
            countText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, Mathf.Clamp01(k * 4f));
            countText.color = Kit.A(countText.color, Mathf.Clamp01(countT * 3f));
            if (countT <= 0) countText.gameObject.SetActive(false);
        }
        if (bannerT > 0)
        {
            bannerT -= udt;
            float k = bannerT > 2.0f ? (2.3f - bannerT) / 0.3f : 1f;
            bannerText.rectTransform.localScale = Vector3.one * Kit.EaseOutBack(Mathf.Clamp01(k));
            float a = Mathf.Clamp01(bannerT * 2f);
            bannerText.color = Kit.A(bannerText.color, a); bannerSub.color = Kit.A(bannerSub.color, a);
            if (bannerT <= 0) { bannerText.gameObject.SetActive(false); bannerSub.gameObject.SetActive(false); }
        }
        if (toastT > 0)
        {
            toastT -= udt;
            toastText.color = Kit.A(toastText.color, Mathf.Clamp01(toastT * 2f));
            if (toastT <= 0) toastText.gameObject.SetActive(false);
        }
        var cam = Race.I ? Race.I.Cam : null;
        for (int i = nameTags.Count - 1; i >= 0; i--)
        {
            var (t, x) = nameTags[i];
            if (!t || !x) { if (x) Destroy(x.gameObject); nameTags.RemoveAt(i); continue; }
            var sp = cam.WorldToScreenPoint(t.position + Vector3.up * 3.2f);
            x.gameObject.SetActive(sp.z > 0 && sp.z < 120);
            x.rectTransform.position = sp;
        }
    }
}

// Left-half steering: thumb position relative to the zone's middle gives analog steer.
public class SteerZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    readonly Dictionary<int, float> fingers = new Dictionary<int, float>();
    public float Value
    {
        get
        {
            if (fingers.Count == 0) return 0;
            float v = 0; foreach (var f in fingers.Values) v = f; return v;
        }
    }
    float Calc(PointerEventData e)
    {
        var rt = (RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var lp);
        float x = lp.x / (rt.rect.width * 0.5f);     // -1 .. 1 across the zone
        return Mathf.Clamp(x / 0.35f, -1f, 1f);       // small center dead-band feel, full lock near the edges
    }
    public void OnPointerDown(PointerEventData e) => fingers[e.pointerId] = Calc(e);
    public void OnDrag(PointerEventData e) => fingers[e.pointerId] = Calc(e);
    public void OnPointerUp(PointerEventData e) => fingers.Remove(e.pointerId);
    void OnDisable() => fingers.Clear();
}

public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    readonly HashSet<int> ids = new HashSet<int>();
    public bool Held => ids.Count > 0;
    public void OnPointerDown(PointerEventData e) { ids.Add(e.pointerId); transform.localScale = Vector3.one * 0.93f; }
    public void OnPointerUp(PointerEventData e) { ids.Remove(e.pointerId); if (ids.Count == 0) transform.localScale = Vector3.one; }
    void OnDisable() { ids.Clear(); transform.localScale = Vector3.one; }
}

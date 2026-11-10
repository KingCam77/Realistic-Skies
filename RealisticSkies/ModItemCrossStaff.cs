using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The cross-staff (Jacob's staff, prefab 712): a long square rod with a cross-piece, the transom,
    /// that slides along it. Held up to the eye, the transom's two ends span an angle that is the
    /// larger the nearer it is to the eye; the scale on the rod reads that angle. It measures
    /// altitudes (lower end on the horizon, upper end on the body) and, as Werner proposed in 1514,
    /// lunar distances (one end on the Moon's near edge, the other on a star or the Sun).
    ///
    /// Use: scroll slides the transom (Shift: a minute at a time). Hold the right mouse button to bring it to
    /// the eye: the view widens so both ends show; lay them on the two marks and let go: the reading
    /// goes into the Navigator's Journal. In lunar mode (the quintant's key) first right click a star
    /// or the Sun to sight it; the staff then turns to the line from the Moon to it.
    /// The reading is the transom's place on its scale, as true as you lay its ends: the view at the eye is just wider than the span, so it is as fine as the screen allows.
    /// State: amount = the angle the transom is set to.
    /// </summary>
    public class ModItemCrossStaff : ShipItem
    {
        public const int PrefabIndex = 712;
        private const int SourceIndex = 91;
        private const string ItemName = "cross-staff";

        // the instrument, metres: the transom spans its angle from the eye at the rod's end
        private const float Rod = 1.8f, RodSide = 0.022f;
        private const float Transom = 0.30f, TransomSide = 0.016f;
        private const float MinAngle = 10f, MaxAngle = 90f;
        private const float SightMargin = 10f;          // degrees of view beyond the transom's span
        private const float Touch = 1.0f;               // degrees: an end that near a mark is on it
        // the reading is the transom's place on the scale: as good as you lay its ends, nothing added

        private const int NoLock = int.MinValue;

        private float angle = 40f;
        private int lunarLock = NoLock;
        private int sightFrame = -100;
        private bool sighting;
        private float savedFov = -1f, savedNear = -1f;
        private float roll;
        private GameObject rig;
        private Transform rigTransom, rigPivot;
        private Transform modelTransom;

        private static bool LunarMode;                  // shared with the quintant's key, per staff in hand

        // ---------------- life ----------------

        public override void OnLoad()
        {
            base.name = ItemName;
            modelTransom = base.transform.Find("transom");
            if (base.amount >= MinAngle && base.amount <= MaxAngle) angle = base.amount;
            ShowTransom();
        }

        private static Transform rigRod;
        private const float RigRodGrowth = 0.5f;     // at 10 degrees the rod's end takes this much more of the screen than at 90

        private static float SightFov(float a) => Mathf.Clamp(a + SightMargin, 20f, 100f);

        private static float Distance(float a) => Transom / 2f / Mathf.Tan(a * 0.5f * Mathf.Deg2Rad);

        private void ShowTransom()
        {
            if (modelTransom != null) modelTransom.localPosition = new Vector3(-Rod / 2f + Distance(angle), 0f, 0f);
            if (rigTransom != null) rigTransom.localPosition = new Vector3(0f, 0f, Distance(angle));
            if (rigRod != null)
            {
                float behind = TransomSide / 2f * 1.8f + 0.004f;            // the sleeve's half-length and a little more
                float from = Mathf.Max(0.01f, Distance(angle) - behind);
                float from90 = Distance(MaxAngle) - behind;
                float seen90 = 2f * Mathf.Atan(RodSide / 2f / from90) * Mathf.Rad2Deg;     // its true size at 90 degrees
                float share = seen90 / SightFov(MaxAngle) * (1f + RigRodGrowth * (MaxAngle - angle) / (MaxAngle - MinAngle));
                float seen = share * SightFov(angle);
                float k = from * Mathf.Tan(seen * 0.5f * Mathf.Deg2Rad) / (RodSide / 2f);
                rigRod.localPosition = new Vector3(0f, 0f, from);
                rigRod.localScale = new Vector3(k, k, (Rod - from) / Rod);
            }
        }

        public override void OnScroll(float input)
        {
            if (!base.sold || base.held == null || input == 0f) return;
            bool fine = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            angle = Mathf.Clamp(angle + Mathf.Sign(input) * (fine ? 1f / 60f : 0.25f), MinAngle, MaxAngle);
            base.amount = angle;
            ShowTransom();
            UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.08f, 2.8f);
        }

        /// <summary>Right click: in a shop, buy it; in lunar mode without a star, sight one.</summary>
        public override void OnAltActivate()
        {
            if (!base.sold)
            {
                base.OnAltActivate();
                return;
            }
            if (LunarMode && lunarLock == NoLock && !StudyMode.Shift) LockStar();
        }

        /// <summary>Held right click: up to the eye.</summary>
        public override void OnAltHeld()
        {
            if (!base.sold || base.held == null) return;
            // Shift + right click: brought close to read its scale, not raised to the eye
            if (!sighting && (StudyMode.Shift || StudyMode.Studying(this)))
            {
                StudyMode.Mark(this);
                return;
            }
            if (LunarMode && lunarLock == NoLock) return;
            sightFrame = Time.frameCount;
        }

        /// <summary>It can hang on a lamp hook, level, by a short lanyard round its middle.</summary>
        public override bool AllowOnItemClick(GoPointerButton lookedAtButton)
        {
            return lookedAtButton.GetComponent<ShipItemLampHook>() != null || base.AllowOnItemClick(lookedAtButton);
        }

        public override void OnPickup()
        {
            base.OnPickup();
            HangableItem h = GetComponent<HangableItem>();
            if (h != null) h.DisconnectJoint();
        }

        public override void OnEnterInventory()
        {
            base.OnEnterInventory();
            HangableItem h = GetComponent<HangableItem>();
            if (h != null) h.DisconnectJoint();
        }

        public override void OnDrop()
        {
            base.OnDrop();
            EndSight(false);
        }

        public override void ExtraLateUpdate()
        {
            base.ExtraLateUpdate();
            if (sighting) InstrumentAim.Mark();
            if (base.held != null && Plugin.configLunarKey.Value.IsDown())
            {
                if (!LunarMode && !Celestial.MoonReworkActive)
                {
                    Notify("Lunar distances need \"Moon Orbit Rework\" turned on");
                }
                else
                {
                    LunarMode = !LunarMode;
                    lunarLock = NoLock;
                    UISoundPlayer.instance.PlayOpenSound();
                    Notify(LunarMode ? "Cross-staff: lunar distance\nright click a star or the Sun, then hold\nthe right button and lay the ends on it and the Moon's near edge"
                                     : "Cross-staff: altitude");
                }
            }
            bool want = base.held != null && Time.frameCount - sightFrame <= 2 && !NavJournal.IsOpen;
            if (want && !sighting) BeginSight();
            else if (!want && sighting) EndSight(base.held != null);
            if (sighting) UpdateSight();
        }

        private void OnDestroy() => EndSight(false);

        // ---------------- at the eye ----------------

        private void BeginSight()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            sighting = true;
            savedFov = cam.fieldOfView;
            savedNear = cam.nearClipPlane;
            cam.nearClipPlane = Mathf.Min(savedNear, 0.05f);        // the transom comes close to the eye
            roll = 0f;
            if (rig == null)
            {
                rig = BuildRig(out rigTransom);
                rigPivot = rig.transform.Find("roll");
            }
            rig.transform.SetParent(cam.transform, false);
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;
            rig.SetActive(true);
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            ShowTransom();
        }

        private void EndSight(bool read)
        {
            if (!sighting) return;
            sighting = false;
            Camera cam = Camera.main;
            if (cam != null && savedFov > 0f) cam.fieldOfView = savedFov;
            if (cam != null && savedNear > 0f) cam.nearClipPlane = savedNear;
            if (rig != null) rig.SetActive(false);
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = true;
            if (read) Read();
        }

        private void UpdateSight()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            // the view just wider than the transom's span: its ends near the edges, as fine as the screen allows
            float fov = Mathf.Clamp(angle + SightMargin, 20f, 100f);
            cam.fieldOfView = Mathf.MoveTowards(cam.fieldOfView, fov, 200f * Time.unscaledDeltaTime);
            // the transom upright for an altitude; along the line from the Moon to the star for a lunar
            float target = 0f;
            if (LunarMode && lunarLock != NoLock && Celestial.HaveSkyAxes && Moon.instance != null && Moon.instance.transform.parent != null)
            {
                Vector3 a = cam.transform.InverseTransformDirection(MoonSeen());
                Vector3 b = cam.transform.InverseTransformDirection(BodyDir(lunarLock));
                Vector2 pa = new Vector2(a.x / Mathf.Max(0.05f, a.z), a.y / Mathf.Max(0.05f, a.z));
                Vector2 pb = new Vector2(b.x / Mathf.Max(0.05f, b.z), b.y / Mathf.Max(0.05f, b.z));
                Vector2 d = pb - pa;
                if (d.sqrMagnitude > 1e-6f) target = -Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            }
            roll = Mathf.MoveTowardsAngle(roll, target, 180f * Time.unscaledDeltaTime);
            // the whole staff turns about the line of sight: the rod stays under the transom's middle
            if (rigPivot != null) rigPivot.localRotation = Quaternion.Euler(0f, 0f, roll);
        }

        /// <summary>The world directions of the transom's two ends, as the eye sees them (upper/first, lower/second).</summary>
        private void Ends(out Vector3 upper, out Vector3 lower)
        {
            Transform cam = Camera.main.transform;
            float t = Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad);
            Quaternion r = Quaternion.Euler(0f, 0f, roll);
            upper = cam.TransformDirection((r * new Vector3(0f, t, 1f)).normalized);
            lower = cam.TransformDirection((r * new Vector3(0f, -t, 1f)).normalized);
        }

        // ---------------- the sky ----------------

        /// <summary>Where the Moon is by the Almanac: from the middle of the sky.</summary>
        internal static Vector3 MoonDir() => (Moon.instance.transform.position - Moon.instance.transform.parent.position).normalized;

        /// <summary>
        /// Where the Moon is drawn, from your eye. The game's Moon is a ball at a finite distance and the
        /// eye isn't at the middle of the sky, so it is drawn a little off where the Almanac has it
        /// (the stars and the Sun are at infinity: no such shift). What you lay the transom on is this.
        /// </summary>
        internal static Vector3 MoonSeen()
        {
            Camera cam = Camera.main;
            if (cam == null) return MoonDir();
            return (Moon.instance.transform.position - cam.transform.position).normalized;
        }

        internal static Vector3 BodyDir(int body)
        {
            if (body == Lunar.SunIndex) return -Sun.sun.transform.parent.forward;
            Lunar.BodyGHADec(body, GameState.day, Sun.sun.globalTime, out double gha, out double dec);
            return Lunar.WorldDirection(gha, dec, Sun.sun.globalTime);
        }

        internal static float Altitude(Vector3 dir) => Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;

        /// <summary>The Sun, the Moon or an Almanac star within a degree of a direction, nearest first (or NoLock; -2 = the Moon).</summary>
        internal static int BodyAt(Vector3 dir, float within, bool moon, out Vector3 bodyDir)
        {
            int best = NoLock;
            bodyDir = Vector3.zero;
            float bestAngle = within;
            if (Sun.sun != null)
            {
                Vector3 s = -Sun.sun.transform.parent.forward;
                float a = Vector3.Angle(dir, s);
                if (s.y > 0f && a < bestAngle) { bestAngle = a; best = Lunar.SunIndex; bodyDir = s; }
            }
            if (moon && Moon.instance != null && Moon.instance.transform.parent != null)
            {
                Vector3 m = MoonSeen();
                float a = Vector3.Angle(dir, m);
                if (m.y > 0f && a < bestAngle) { bestAngle = a; best = -2; bodyDir = m; }
            }
            if (Celestial.HaveSkyAxes)
            {
                for (int i = 0; i < Lunar.Stars.Count; i++)
                {
                    Vector3 d = BodyDir(i);
                    if (d.y <= 0f) continue;
                    float a = Vector3.Angle(dir, d);
                    if (a < bestAngle) { bestAngle = a; best = i; bodyDir = d; }
                }
            }
            return best;
        }

        private void LockStar()
        {
            if (!Celestial.HaveSkyAxes || Sun.sun == null) return;
            Vector3 aim = Camera.main.transform.forward;
            int b = BodyAt(aim, 3f, false, out _);
            if (b == NoLock)
            {
                Notify("No star near where you look: aim at one and right click");
                return;
            }
            lunarLock = b;
            UISoundPlayer.instance.PlayOpenSound();
            Notify((b == Lunar.SunIndex ? "Sighted the Sun" : "Sighted the star") + "\nhold the right button and lay the ends on it and the Moon's near edge");
        }

        private static float Gauss()
        {
            float u1 = Mathf.Max(1e-6f, UnityEngine.Random.value), u2 = UnityEngine.Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }

        /// <summary>Let go: what the ends lie on is read off the rod (to its precision) and written down.</summary>
        private void Read()
        {
            if (Camera.main == null || Sun.sun == null) return;
            Ends(out Vector3 upper, out Vector3 lower);
            if (!LunarMode)
            {
                if (Mathf.Abs(Altitude(lower)) > Touch)
                {
                    Notify("The lower end isn't on the horizon");
                    return;
                }
                int b = BodyAt(upper, Touch, true, out Vector3 dir);
                if (b == NoLock)
                {
                    Notify("Nothing at the upper end");
                    return;
                }
                double alt = angle;
                // the Moon laid on where it is drawn: taken to where the Almanac has it, as the
                // navigator's sums would expect (the game's own parallax, not the real one)
                if (b == -2) alt += Altitude(MoonDir()) - Altitude(dir);
                alt = Math.Round(alt * 60.0) / 60.0;            // read to the minute on the scale
                UISoundPlayer.instance.PlayOpenSound();
                Notify("Cross-staff: altitude" + Celestial.Format(alt) + "'");
                Debug.Log("RealisticSkies: cross-staff altitude" + Celestial.Format(alt) + "', true" + Celestial.Format(Altitude(dir)) + "'");
                NavJournal.NoteAltitude(alt);
                return;
            }

            if (lunarLock == NoLock || Moon.instance == null || Moon.instance.transform.parent == null) return;
            Vector3 moon = MoonSeen(), body = BodyDir(lunarLock);
            double sd = Lunar.MoonSemiDiameter;
            float edge = (float)(double.IsNaN(sd) ? 0.25 : sd);
            // one end on the body, the other on the Moon (anywhere on its face counts as its near edge)
            bool a = Vector3.Angle(upper, body) < Touch && Vector3.Angle(lower, moon) < Touch + edge;
            bool c = Vector3.Angle(lower, body) < Touch && Vector3.Angle(upper, moon) < Touch + edge;
            if (!a && !c)
            {
                Notify("The ends aren't on the Moon and the " + (lunarLock == Lunar.SunIndex ? "Sun" : "star"));
                return;
            }
            // what you laid is the distance to the Moon as drawn; the tables have it from the middle of
            // the sky: the difference (the game's parallax) is taken off for you
            Vector3 moonTrue = MoonDir();
            double shift = Vector3.Angle(moonTrue, body) - Vector3.Angle(moon, body);
            double near = Vector3.Angle(moonTrue, body) - edge;
            double dist = angle + shift;
            dist = Math.Round(dist * 60.0) / 60.0;
            UISoundPlayer.instance.PlayOpenSound();
            Notify("Cross-staff: Moon's near edge to the " + (lunarLock == Lunar.SunIndex ? "Sun" : "star") + Celestial.Format(dist) + "'");
            Debug.Log("RealisticSkies: cross-staff lunar" + Celestial.Format(dist) + "', true near edge" + Celestial.Format(near) + "', set" + Celestial.Format(angle) + "', drawn Moon off by" + Celestial.Format(shift) + "'");
            NavJournal.NoteLunar(dist, 1);
        }

        private static void Notify(string msg)
        {
            try { NotificationUi.instance.ShowNotification(msg); } catch (Exception) { }
        }

        // ---------------- model ----------------

        private static Material wood, brass;

        private static void Materials()
        {
            if (wood != null) return;
            Shader sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            wood = new Material(sh) { name = "cross-staff wood", color = new Color(0.26f, 0.15f, 0.085f) };     // dark pear wood, oiled
            wood.SetFloat("_Glossiness", 0.38f);
            brass = new Material(sh) { name = "cross-staff brass", color = new Color(0.78f, 0.62f, 0.30f) };
            brass.SetFloat("_Metallic", 0.8f);
            brass.SetFloat("_Glossiness", 0.6f);
        }

        internal static void Box(List<Vector3> v, List<Vector3> n, List<int> tri, Vector3 min, Vector3 max)
        {
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3 c = (min + max) / 2f, half = (max - min) / 2f;
            foreach (Vector3 nn in normals)
            {
                Vector3 a = nn.x != 0f ? Vector3.up : Vector3.right;
                Vector3 b = Vector3.Cross(nn, a);
                b = new Vector3(Mathf.Abs(b.x), Mathf.Abs(b.y), Mathf.Abs(b.z));
                Vector3 center = c + Vector3.Scale(nn, half);
                Vector3 ea = Vector3.Scale(a, half), eb = Vector3.Scale(b, half);
                int i0 = v.Count;
                Vector3[] corners = { center - ea - eb, center + ea - eb, center + ea + eb, center - ea + eb };
                foreach (Vector3 p in corners) { v.Add(p); n.Add(nn); }
                bool ok = Vector3.Dot(Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]), nn) > 0f;
                if (ok) tri.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                else tri.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
        }

        /// <summary>A capped cylinder from a to b, smooth-shaded round its side.</summary>
        internal static void Cyl(List<Vector3> v, List<Vector3> n, List<int> tri, Vector3 a, Vector3 b, float r, int seg = 24)
        {
            Vector3 ax = (b - a).normalized;
            Vector3 u = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 w = Vector3.Cross(ax, u);
            void Tri(int i, int j, int k, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(v[j] - v[i], v[k] - v[i]), outward) > 0f) tri.AddRange(new[] { i, j, k });
                else tri.AddRange(new[] { i, k, j });
            }
            int side = v.Count;
            for (int k = 0; k <= seg; k++)
            {
                float t = k * Mathf.PI * 2f / seg;
                Vector3 d = u * Mathf.Cos(t) + w * Mathf.Sin(t);
                v.Add(a + d * r); n.Add(d);
                v.Add(b + d * r); n.Add(d);
            }
            for (int k = 0; k < seg; k++)
            {
                int i = side + k * 2;
                Vector3 mid = ((v[i] + v[i + 3]) / 2f - (a + b) / 2f);
                Vector3 outward = mid - ax * Vector3.Dot(mid, ax);
                Tri(i, i + 2, i + 3, outward);
                Tri(i, i + 3, i + 1, outward);
            }
            foreach (bool end in new[] { false, true })
            {
                Vector3 c = end ? b : a, nn = end ? ax : -ax;
                int ci = v.Count;
                v.Add(c); n.Add(nn);
                for (int k = 0; k <= seg; k++)
                {
                    float t = k * Mathf.PI * 2f / seg;
                    v.Add(c + (u * Mathf.Cos(t) + w * Mathf.Sin(t)) * r); n.Add(nn);
                }
                for (int k = 0; k < seg; k++) Tri(ci, ci + 1 + k, ci + 2 + k, nn);
            }
        }

        internal static Mesh MakeMesh(string name, Action<List<Vector3>, List<Vector3>, List<int>, List<int>> build)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t0 = new List<int>(); var t1 = new List<int>();
            build(v, n, t0, t1);
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetNormals(n);
            m.subMeshCount = 2;
            m.SetTriangles(t0, 0);
            m.SetTriangles(t1, 1);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>The rod along z, centred; scale marks of brass along its top.</summary>
        private static Mesh RodMesh(float zFrom, float zTo, bool brass = true)
        {
            return MakeMesh("cross-staff rod", (v, n, w, b) =>
            {
                float h = RodSide / 2f;
                Box(v, n, w, new Vector3(-h, -h, zFrom), new Vector3(h, h, zTo));
                // a mark every five degrees of the transom's span, on the rod's top
                for (int deg = (int)MinAngle; brass && deg <= (int)MaxAngle; deg += 5)
                {
                    float z = zFrom + Distance(deg) * (zTo - zFrom) / Rod;
                    float wide = deg % 10 == 0 ? h : h * 0.5f;
                    Box(v, n, b, new Vector3(-wide, h, z - 0.0012f), new Vector3(wide, h + 0.0008f, z + 0.0012f));
                }
                if (brass) Box(v, n, b, new Vector3(-h - 0.002f, -h - 0.002f, zTo - 0.03f), new Vector3(h + 0.002f, h + 0.002f, zTo));      // the end ferrule
            });
        }

        /// <summary>
        /// The transom. For the eye ("sight"), its ends are thin brass blades standing in the transom's
        /// own plane: the eye reads the angle to their tips, and a thick end would show its near corner,
        /// which stands out wider than the tip by its depth (half a degree at arm's length).
        /// </summary>
        private static Mesh TransomMesh(bool sight = false)
        {
            return MakeMesh("cross-staff transom", (v, n, w, b) =>
            {
                float h = TransomSide / 2f, half = Transom / 2f;
                if (!sight)
                {
                    Box(v, n, w, new Vector3(-h, -half + 0.012f, -h), new Vector3(h, half - 0.012f, h));
                    Box(v, n, b, new Vector3(-h * 0.8f, half - 0.012f, -h * 0.8f), new Vector3(h * 0.8f, half, h * 0.8f));      // brass tips
                    Box(v, n, b, new Vector3(-h * 0.8f, -half, -h * 0.8f), new Vector3(h * 0.8f, -half + 0.012f, h * 0.8f));
                }
                else
                {
                    // the wood stops short enough that its near corners stay inside the tips' sight line
                    float wood = half - 0.012f;
                    Box(v, n, w, new Vector3(-h, -wood, -h), new Vector3(h, wood, h));
                    // blades narrowing to a fine point at the very tip, so the point is what touches
                    float bw = h * 0.8f, pw = 0.0008f, step = (half - wood) / 4f;
                    for (int k = 0; k < 4; k++)
                    {
                        float wk = Mathf.Lerp(bw, pw, k / 3f);
                        float y0 = wood + k * step, y1 = y0 + step;
                        Box(v, n, b, new Vector3(-wk, y0, -0.0002f), new Vector3(wk, y1, 0.0002f));
                        Box(v, n, b, new Vector3(-wk, -y1, -0.0002f), new Vector3(wk, -y0, 0.0002f));
                    }
                }
                // the turned brass sleeve that slides on the rod (round, enclosing the square rod's corners),
                // a little longer than the transom is thick, with a raised bead at either end
                float r = RodSide * 0.8f, cz = h * 1.8f;
                Cyl(v, n, b, new Vector3(0f, 0f, -cz), new Vector3(0f, 0f, cz), r, 32);
                Cyl(v, n, b, new Vector3(0f, 0f, -cz), new Vector3(0f, 0f, -cz + 0.0018f), r + 0.0009f, 32);
                Cyl(v, n, b, new Vector3(0f, 0f, cz - 0.0018f), new Vector3(0f, 0f, cz), r + 0.0009f, 32);
                Cyl(v, n, b, new Vector3(0f, 0f, -0.0007f), new Vector3(0f, 0f, 0.0007f), r + 0.0005f, 32);      // a fine middle line
                // the thumbscrew that clamps it: a boss on the sleeve, a shank, a round head and a wing
                Cyl(v, n, b, new Vector3(r - 0.001f, 0f, 0f), new Vector3(r + 0.0025f, 0f, 0f), 0.0032f, 16);
                Cyl(v, n, b, new Vector3(r + 0.0025f, 0f, 0f), new Vector3(r + 0.0065f, 0f, 0f), 0.0015f, 12);
                Cyl(v, n, b, new Vector3(r + 0.0065f, 0f, 0f), new Vector3(r + 0.008f, 0f, 0f), 0.0036f, 20);
                Box(v, n, b, new Vector3(r + 0.008f, -0.0009f, -0.0085f), new Vector3(r + 0.0115f, 0.0009f, 0.0085f));
            });
        }

        private static GameObject Part(string name, Mesh mesh, Transform parent, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { wood, brass };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>The staff at the eye: the rod runs out from just below the eye, the transom across the view.</summary>
        private static GameObject BuildRig(out Transform transom)
        {
            Materials();
            var root = new GameObject("cross-staff at the eye");
            UnityEngine.Object.DontDestroyOnLoad(root);
            Camera cam = Camera.main;
            int layer = cam != null ? cam.gameObject.layer : 0;
            var pivot = new GameObject("roll").transform;
            pivot.SetParent(root.transform, false);
            // the eye is at the rod's end: the rod runs straight out along the line of sight, under the
            // middle of the transom (seen end on it is a small square in the middle of the view)
            // Its near end rides just in front of the sleeve, so the sleeve is always on it; its thickness
            // is set in ShowTransom so that, seen end on, it is the model's at 90 degrees and grows only
            // gently on the screen as the transom goes out (the view narrows as it does).
            GameObject rod = Part("rod", RodMesh(0f, Rod, false), pivot, layer);      // plain wood: thickened, the marks and ferrule peeped out round its end
            rod.transform.localPosition = Vector3.zero;
            rigRod = rod.transform;
            transom = Part("transom", TransomMesh(true), pivot, layer).transform;
            return root;
        }

        // ---------------- the scale on its side ----------------

        /// <summary>A mesh turned about the vertical: the staff in hand lies across, left to right.</summary>
        private static Mesh Turned(Mesh m)
        {
            Quaternion q = Quaternion.Euler(0f, 90f, 0f);
            Vector3[] v = m.vertices, n = m.normals;
            for (int i = 0; i < v.Length; i++) { v[i] = q * v[i]; n[i] = q * n[i]; }
            m.vertices = v;
            m.normals = n;
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Ticks for every degree of the transom's span on the side that faces you (the item's -z),
        /// longer each five, with the tens in figures: read the angle off the transom's place.
        /// </summary>
        private static void Scale(Transform staff, int layer)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var w = new List<int>(); var b = new List<int>();
            float h = RodSide / 2f, face = -h - 0.0006f;
            for (int deg = (int)MinAngle; deg <= (int)MaxAngle; deg++)
            {
                float x = -Rod / 2f + Distance(deg);
                float len = deg % 10 == 0 ? h * 1.6f : deg % 5 == 0 ? h * 1.1f : h * 0.6f;
                Box(v, n, b, new Vector3(x - 0.0007f, h - len, face), new Vector3(x + 0.0007f, h, -h));
            }
            var mesh = new Mesh { name = "cross-staff scale" };
            mesh.SetVertices(v); mesh.SetNormals(n);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(w, 0);
            mesh.SetTriangles(b, 1);
            mesh.RecalculateBounds();
            Part("scale", mesh, staff, layer);

            // the Almanac's printing: its font and the material it is drawn with
            Font font = null;
            Material fontMat = null;
            if (Patches.itemPrefabs.TryGetValue(701, out GameObject almanac) && almanac != null)
            {
                TextMesh tm = almanac.GetComponentInChildren<TextMesh>(true);
                if (tm != null)
                {
                    font = tm.font;
                    MeshRenderer tr = tm.GetComponent<MeshRenderer>();
                    if (tr != null) fontMat = tr.sharedMaterial;
                }
            }
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (fontMat == null) fontMat = font.material;
            for (int deg = (int)MinAngle; deg <= (int)MaxAngle; deg += 10)
            {
                var go = new GameObject("scale " + deg);
                go.layer = layer;
                go.transform.SetParent(staff, false);
                go.transform.localPosition = new Vector3(-Rod / 2f + Distance(deg), -h * 0.55f, face - 0.0004f);
                var t = go.AddComponent<TextMesh>();
                t.font = font;
                t.text = deg.ToString();
                t.fontSize = 48;
                t.characterSize = 0.0022f;
                t.anchor = TextAnchor.MiddleCenter;
                t.alignment = TextAlignment.Center;
                t.color = new Color(0.86f, 0.68f, 0.32f);     // brass figures, inlaid in the dark wood
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = fontMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // ---------------- shops ----------------

        internal static void AddToShop(Transform shop, int island)
        {
            if (shop == null) return;
            foreach (ShopItemSpawner sp in shop.GetComponentsInChildren<ShopItemSpawner>(true))
            {
                SaveablePrefab sv = sp.itemPrefab != null ? sp.itemPrefab.GetComponent<SaveablePrefab>() : null;
                if (sv == null || sv.prefabIndex != 703) continue;
                GameObject clone = UnityEngine.Object.Instantiate(sp.gameObject, sp.transform.parent);
                clone.name = ItemName;
                if (ShopSpots.TryGet(ShopSpots.CrossStaff, island, out ShopSpots.Spot spot))
                {
                    ShopSpots.Apply(clone.transform, shop, spot);
                }
                else
                {
                    clone.transform.localPosition = sp.transform.localPosition + new Vector3(0f, 0.03f, -0.3f);
                    clone.transform.rotation = shop.rotation * Quaternion.Euler(0f, 90f, 0f);
                }
                ShopItemSpawner csp = clone.GetComponent<ShopItemSpawner>();
                AccessTools.Field(typeof(ShopItemSpawner), "item").SetValue(csp, null);
                csp.enabled = false;
                Renderer r = clone.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                var hook = clone.AddComponent<LoreSpawnerHook>();
                hook.spawner = csp;
                hook.prefabIndex = PrefabIndex;
                ShopSpots.RegisterSpawner(ShopSpots.CrossStaff, island, clone.transform);
                break;
            }
        }

        // ---------------- prefab ----------------

        private static GameObject holder;

        internal static void Register(PrefabsDirectory dir)
        {
            if (holder != null || dir == null || dir.directory == null) return;
            if (dir.directory.Length <= SourceIndex || dir.directory[SourceIndex] == null)
            {
                Debug.LogWarning("RealisticSkies: vanilla scroll (prefab 91) not found, no cross-staff");
                return;
            }
            if (dir.directory.Length > PrefabIndex && dir.directory[PrefabIndex] != null)
            {
                Debug.LogWarning("RealisticSkies: prefab " + PrefabIndex + " is already taken, no cross-staff");
                return;
            }
            try
            {
                Materials();
                holder = new GameObject("RealisticSkies cross-staff holder");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
                GameObject go = UnityEngine.Object.Instantiate(dir.directory[SourceIndex], holder.transform);
                go.name = PrefabIndex + " " + ItemName;

                ShipItemScroll old = go.GetComponent<ShipItemScroll>();
                ModItemCrossStaff item = go.AddComponent<ModItemCrossStaff>();
                ModItemLoreScroll.CopyShipItemFields(old, item);
                UnityEngine.Object.DestroyImmediate(old);
                for (int i = go.transform.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
                }
                go.transform.localScale = Vector3.one;
                BoxCollider box = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
                foreach (Collider c in go.GetComponents<Collider>())
                {
                    if (c != box) UnityEngine.Object.DestroyImmediate(c);
                }
                box.center = Vector3.zero;
                box.size = new Vector3(Rod, 0.06f, 0.06f);

                // the rod is the item itself, centred; the transom slides on it
                // held across, left to right, the scale on the side toward you (the eye end on the left)
                go.GetComponent<MeshFilter>().sharedMesh = Turned(RodMesh(-Rod / 2f, Rod / 2f));
                go.GetComponent<MeshRenderer>().sharedMaterials = new[] { wood, brass };
                GameObject t = Part("transom", Turned(TransomMesh(true)), go.transform, go.layer);   // the same pointed blades as at the eye
                t.transform.localPosition = new Vector3(-Rod / 2f + Distance(40f), 0f, 0f);
                Scale(go.transform, go.layer);

                item.name = ItemName;
                item.amount = 40f;
                item.health = 0f;
                item.holdDistance = 1.0f;
                item.inventoryRotationX = 0f;
                item.inventoryRotation = 0f;
                item.inventoryScale *= 0.35f;
                item.value = 70;
                if (Patches.itemPrefabs.TryGetValue(703, out GameObject chart) && chart != null
                    && chart.GetComponent<ShipItem>() is ShipItem chartItem && chartItem.value > 0)
                {
                    item.value = Mathf.RoundToInt(chartItem.value * 0.27f);     // about 70 crowns
                }
                item.mass = 1.2f;

                // hangs on a lamp hook as the lanterns do: level (no pitch or roll), turning freely
                HangableItem hang = go.AddComponent<HangableItem>();
                var ht = Traverse.Create(hang);
                ht.Field("lockX").SetValue(true);
                ht.Field("lockZ").SetValue(true);
                ht.Field("rotX").SetValue(0f);
                ht.Field("rotZ").SetValue(0f);

                go.GetComponent<SaveablePrefab>().prefabIndex = PrefabIndex;
                if (dir.directory.Length <= PrefabIndex)
                {
                    Array.Resize(ref dir.directory, PrefabIndex + 5);
                }
                dir.directory[PrefabIndex] = go;
                Debug.Log("RealisticSkies: added the cross-staff as prefab " + PrefabIndex);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not build the cross-staff: " + e);
            }
        }
    }

    /// <summary>
    /// How the mod's instruments hang on a lamp hook. The game hangs a thing by its origin at the hook
    /// and only keeps it level, so it faces whichever way you came at the hook and swings round. These
    /// hang square to the wall instead:
    /// the cross-staff upright, its middle through the hook's eye, the scale toward you;
    /// the quintant upside down by its handle, its frame flat to the wall.
    /// </summary>
    [HarmonyPatch(typeof(HangableItem), "LateUpdate")]
    internal static class HangPatch
    {
        // the end of the quintant's handle, in the quintant's own space (from its model)
        private static readonly Vector3 QuintantHandle = new Vector3(0.066f, -0.285f, -0.002f);

        [HarmonyPostfix]
        private static void Square(HangableItem __instance)
        {
            if (!__instance.IsHanging()) return;
            bool staff = __instance.GetComponent<ModItemCrossStaff>() != null;
            bool quintant = !staff && __instance.GetComponent<ModItemQuintant>() != null;
            bool astrolabe = !staff && !quintant && __instance.GetComponent<ModItemAstrolabe>() != null;
            bool nocturnal = !staff && !quintant && !astrolabe && __instance.GetComponent<ModItemNocturnal>() != null;
            ModItemRingDial dial = !staff && !quintant && !astrolabe && !nocturnal ? __instance.GetComponent<ModItemRingDial>() : null;
            ModItemTraverseBoard boardItem = !staff && !quintant && !astrolabe && !nocturnal && dial == null ? __instance.GetComponent<ModItemTraverseBoard>() : null;
            bool board = boardItem != null;
            if (!staff && !quintant && !astrolabe && !nocturnal && dial == null && !board) return;
            Collider hook = Traverse.Create(__instance).Field("currentHook").GetValue<Collider>();
            if (hook == null) return;
            Transform h = hook.transform;
            Vector3 at = h.position + h.forward * -0.128f;                    // where the game hangs things
            // a wall hook looks out along its -forward; a hook under a beam or the deck looks down, and
            // then its own up says which way it faces (not the world's, which wobbles with the roll)
            bool ceiling = Mathf.Abs(Vector3.Dot(h.forward, Vector3.up)) > 0.7f;
            Vector3 face = Vector3.ProjectOnPlane(ceiling ? h.up : -h.forward, Vector3.up);
            if (face.sqrMagnitude < 1e-4f) face = Vector3.ProjectOnPlane(h.right, Vector3.up);
            face.Normalize();
            Transform t = __instance.transform;
            if (astrolabe)
            {
                // plumb by its shackle ring, the scale (its -z) to the room
                t.rotation = Quaternion.LookRotation(-face, Vector3.up);
                t.position = at + Vector3.down * 0.01f - t.rotation * ModItemAstrolabe.HangPoint;
                return;
            }
            if (dial != null)
            {
                // plumb by its shackle's ring, the shackle up wherever it is set, the engraved face (its -z) to the room
                t.rotation = Quaternion.LookRotation(-face, Vector3.up) * Quaternion.AngleAxis(dial.Latitude, Vector3.forward);
                t.position = at + Vector3.down * 0.01f - t.rotation * RingDialMath.HangPoint(dial.Latitude);
                return;
            }
            if (board)
            {
                // by the hole in its heart, upright, the painted front (its -z) to the room, the back toward the hook's mount
                t.rotation = Quaternion.LookRotation(-face, Vector3.up);
                t.position = at - t.rotation * boardItem.Style.HangPoint;      // the heart, crest or finial: each style's own
                return;
            }
            if (nocturnal)
            {
                // by the hole in its handle, the handle up, the scales (its -z) to the room
                t.rotation = Quaternion.LookRotation(-face, Vector3.down);
                t.position = at + Vector3.down * 0.01f - t.rotation * ModItemNocturnal.HangPoint;
                return;
            }
            if (staff)
            {
                if (ceiling)
                {
                    // under a beam: level, along the way the hook faces, the scale (its -z) to the deck
                    t.rotation = Quaternion.LookRotation(Vector3.up, Vector3.Cross(Vector3.up, face));
                }
                else
                {
                    // on a wall: through the eye as the hook is turned (its up, in the wall's plane), the scale to the room
                    Vector3 rod = Vector3.ProjectOnPlane(h.up, -h.forward);
                    if (rod.sqrMagnitude < 1e-4f) rod = Vector3.up;
                    rod.Normalize();
                    Vector3 z = h.forward.normalized;
                    t.rotation = Quaternion.LookRotation(z, Vector3.Cross(z, rod));
                }
                t.position = at;
            }
            else
            {
                // upside down (its y down), plumb, the frame's plate (its +x) toward the wall; a hand's
                // breadth below the hook on a cord through its eye
                Vector3 y = Vector3.down, x = -face;
                Vector3 z = Vector3.Cross(x, y);
                t.rotation = Quaternion.LookRotation(z, y);
                Vector3 handle = at + Vector3.down * Drop;
                t.position = handle - t.rotation * QuintantHandle;
                Cord(__instance, at, handle, t.rotation * Vector3.forward);
            }
            return;
        }

        private const float Drop = 0.07f;
        private static Material cordMat;

        /// <summary>A cord from either side of the handle's knob up through the hook's eye.</summary>
        private static void Cord(HangableItem item, Vector3 eye, Vector3 knob, Vector3 along)
        {
            Transform c = item.transform.Find("hanging cord");
            if (c == null)
            {
                if (cordMat == null)
                {
                    Shader sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
                    cordMat = new Material(sh) { name = "hanging cord", color = new Color(0.30f, 0.24f, 0.16f) };
                    cordMat.SetFloat("_Glossiness", 0.05f);
                }
                c = new GameObject("hanging cord").transform;
                c.SetParent(item.transform, false);
                for (int i = 0; i < 2; i++)
                {
                    GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    UnityEngine.Object.Destroy(seg.GetComponent<Collider>());
                    seg.layer = item.gameObject.layer;
                    seg.transform.SetParent(c, false);
                    Renderer r = seg.GetComponent<Renderer>();
                    r.sharedMaterial = cordMat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                item.gameObject.AddComponent<HangingCordWatch>();
            }
            c.gameObject.SetActive(true);
            Vector3 side = along.normalized * 0.012f;
            for (int i = 0; i < 2; i++)
            {
                Transform seg = c.GetChild(i);
                Vector3 from = knob + (i == 0 ? side : -side), d = eye - from;
                seg.position = from + d * 0.5f;
                seg.rotation = Quaternion.LookRotation(d, Vector3.up);
                Vector3 ls = item.transform.lossyScale;
                seg.localScale = new Vector3(0.003f / ls.x, 0.003f / ls.y, d.magnitude / ls.z);
            }
        }
    }

    /// <summary>Takes the cord down when the quintant comes off the hook.</summary>
    internal class HangingCordWatch : MonoBehaviour
    {
        private HangableItem hang;

        private void LateUpdate()
        {
            if (hang == null) hang = GetComponent<HangableItem>();
            Transform c = transform.Find("hanging cord");
            if (c != null && hang != null && !hang.IsHanging() && c.gameObject.activeSelf) c.gameObject.SetActive(false);
        }
    }
}

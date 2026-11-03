using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The nocturnal (prefab 714): the time at night from the stars that turn about the pole. Three
    /// brass plates on one hollow rivet: the date plate with its handle, the toothed hour disc and the
    /// index. This one is made for the islands' sky: you look at the North Star through the rivet and
    /// lay the index on Corvin, the brightest star of Corvin's Sloop, which goes round the pole once a
    /// sidereal day.
    ///
    /// Use: scroll turns the hour disc a day at a time, to set its long midnight tooth to the night's
    /// date (the date of the evening). Hold the right mouse button to raise it, the handle down: bring
    /// the North Star into the rivet's hole and scroll the index onto Corvin (Shift: finely); let go and
    /// the index's edge reads the local time on the hour disc, to five minutes. With the journal carried
    /// the time is written in it, and against the glass's (or the watch's) time at the zero meridian
    /// it gives a rough longitude.
    ///
    /// The geometry (see docs/make_nocturnal.py, which draws the scales by the same numbers): angles
    /// counter-clockwise from up, seen from the front; k = 15 (1 + 1/len) degrees an hour; the date
    /// ring's mark for day d of the year at SHA(Corvin) + 360 (d + 1) / len; on the hour disc midnight
    /// at the long tooth, the morning hours counter-clockwise and the evening hours clockwise of it,
    /// the seam at noon. Set right, the index then reads the local mean time to within a few minutes
    /// (the North Star is not quite at the pole).
    /// State: amount = the day the disc is set to + the index's angle / 360.
    /// </summary>
    public class ModItemNocturnal : ShipItem
    {
        public const int PrefabIndex = 714;
        private const int SourceIndex = 91;
        private const string ItemName = "nocturnal";
        private const double ShaCorvin = 360.0 - 22.568;     // as the scales are drawn (the Almanac's Corvin)

        // the sight: the plate a hand's length from the eye
        private const float PlateDeg = 8f, HoleDeg = 0.3f, ArmDeg = 27f, ArmWidthDeg = 1.2f;
        private const float MinFov = 66f;

        private int day;                // the day of the year the midnight tooth is set to
        private float arm;              // the index, degrees counter-clockwise from up
        private Transform discT, armT;
        private int sightFrame = -100;
        private bool sighting;
        private float savedFov = -1f;
        private static Texture2D plateTex, discTex;

        internal static int YearLen => Plugin.configYearLen.Value == 365 ? 365 : 92;

        // ---------------- the calendar on its ring ----------------

        private static readonly string[] Seasons = { "Autumn", "Winter", "Spring", "Summer" };

        /// <summary>The day of the year as the date ring names it: "Autumn 14" or "5 October" (day 0: the autumn equinox).</summary>
        internal static string DateName(int d)
        {
            int len = YearLen;
            d = ((d % len) + len) % len;
            if (len == 92) return Seasons[d / 23] + " " + (d % 23 + 1);
            DateTime t = new DateTime(2001, 9, 22).AddDays(d);
            return t.Day + " " + t.ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture);
        }

        internal static int DayOfYear(int gameDay) => ((gameDay % YearLen) + YearLen) % YearLen;

        // ---------------- life ----------------

        public override void OnLoad()
        {
            base.name = ItemName;
            discT = base.transform.Find("disc");
            armT = base.transform.Find("arm");
            if (base.amount > 0f)
            {
                day = Mathf.Clamp(Mathf.FloorToInt(base.amount), 0, YearLen - 1);
                arm = (base.amount - Mathf.Floor(base.amount)) * 360f;
            }
            Show();
        }

        private void Store()
        {
            base.amount = day + Mathf.Repeat(arm, 360f) / 360f * 0.9999f + 1e-5f;
        }

        private void Show()
        {
            if (discT != null) discT.localRotation = Quaternion.Euler(0f, 0f, (float)MidnightAngle(day));
            if (armT != null) armT.localRotation = Quaternion.Euler(0f, 0f, arm);
        }

        /// <summary>Where the date ring marks a day: the midnight tooth set to it sits there.</summary>
        private static double MidnightAngle(int d) => Celestial.Repeat(ShaCorvin + 360.0 * (d + 1) / YearLen, 360.0);

        /// <summary>The hour the index's edge stands at on the hour disc.</summary>
        private static double Reading(int d, float index)
        {
            int len = YearLen;
            double k = 15.0 * (1.0 + 1.0 / len);
            double fromMidnight = NavMath.Wrap180(index - MidnightAngle(d)) / k;      // -12..12 hours
            return Celestial.Repeat(fromMidnight, 24.0);
        }

        public override void OnScroll(float input)
        {
            if (!base.sold || base.held == null || input == 0f) return;
            if (sighting)
            {
                bool fine = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                arm = Mathf.Repeat(arm + Mathf.Sign(input) * (fine ? 0.2f : 1f), 360f);
                UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.05f, 3.2f);
            }
            else
            {
                day = ((day + (input > 0f ? 1 : -1)) % YearLen + YearLen) % YearLen;
                UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.08f, 2.4f);
                Notify("Nocturnal set to the night of " + DateName(day));
            }
            Store();
            Show();
        }

        public override void OnAltActivate()
        {
            if (!base.sold) base.OnAltActivate();
        }

        public override void OnAltHeld()
        {
            if (base.sold && base.held != null) sightFrame = Time.frameCount;
        }

        public override bool AllowOnItemClick(GoPointerButton lookedAtButton)
        {
            return lookedAtButton.GetComponent<ShipItemLampHook>() != null || base.AllowOnItemClick(lookedAtButton);
        }

        public override void OnPickup()
        {
            base.OnPickup();
            HangableItem h = GetComponent<HangableItem>();
            if (h != null) h.DisconnectJoint();
            if (base.sold) Notify("Nocturnal: set to the night of " + DateName(day));
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

        private void OnDestroy() => EndSight(false);

        public override void ExtraLateUpdate()
        {
            base.ExtraLateUpdate();
            bool want = base.held != null && Time.frameCount - sightFrame <= 2 && !NavJournal.IsOpen;
            if (want && !sighting) BeginSight();
            if (sighting) InstrumentAim.Mark();
            else if (!want && sighting) EndSight(base.held != null);
        }

        // ---------------- at the eye ----------------

        private void BeginSight()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            sighting = true;
            savedFov = cam.fieldOfView;
            if (cam.fieldOfView < MinFov) cam.fieldOfView = MinFov;     // Corvin is 20 degrees off the pole
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        }

        private void EndSight(bool read)
        {
            if (!sighting) return;
            sighting = false;
            Camera cam = Camera.main;
            if (cam != null && savedFov > 0f) cam.fieldOfView = savedFov;
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = true;
            if (read) Read();
        }

        private void Read()
        {
            double h = Reading(day, arm);
            double shown = Math.Round(h * 12.0) / 12.0;                     // to five minutes
            shown = Celestial.Repeat(shown, 24.0);
            UISoundPlayer.instance.PlayOpenSound();
            Notify("Nocturnal: " + Hm(shown) + " by the stars, the night of " + DateName(day));
            if (Sun.sun != null)
            {
                Camera cam = Camera.main;
                string aim = "";
                if (cam != null && Celestial.HaveSkyAxes)
                {
                    double gmt = Sun.sun.globalTime;
                    Vector3 ns = Lunar.WorldDirection(Celestial.Repeat(Lunar.AriesGHA(GameState.day, gmt) + (360.0 - 287.0), 360.0), 89.6, gmt);
                    aim = ", North Star " + Vector3.Angle(cam.transform.forward, ns).ToString("0.00") + " deg off the middle";
                }
                double truth = Celestial.Repeat(Sun.sun.localTime, 24.0);
                double err = NavMath.Wrap180((h - truth) * 15.0) / 15.0 * 60.0;
                Debug.Log("RealisticSkies: nocturnal " + Hm(shown) + " (" + Hm(h) + " unrounded), true local " + Hm(truth) + ", error " +
                          err.ToString("0.0") + " min; set to " + DateName(day) + ", today " + DateName(DayOfYear(GameState.day)) + aim);
            }
            NavJournal.NoteNocturnal(shown, DateName(day));
        }

        internal static string Hm(double hours)
        {
            int m = (int)Math.Round(Celestial.Repeat(hours, 24.0) * 60.0) % (24 * 60);
            return (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
        }

        private static void Notify(string msg)
        {
            try { NotificationUi.instance.ShowNotification(msg); } catch (Exception) { }
        }

        /// <summary>
        /// The sky past the plate: its dark disc a hand's length off with the hole in the middle, the
        /// handle going down, and the index from the middle out past the plate, its left edge on the
        /// line through the hole.
        /// </summary>
        private void OnGUI()
        {
            if (!sighting || Event.current.type != EventType.Repaint) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            float px = Screen.height / 2f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            float rp = px * Mathf.Tan(PlateDeg * Mathf.Deg2Rad);
            Color dark = new Color(0.075f, 0.055f, 0.03f, 0.97f);
            Color old = GUI.color;
            Matrix4x4 m = GUI.matrix;
            GUI.color = dark;
            // the handle, then the plate with its hole
            float perM = rp / 0.070f;                                   // the plate is 70 mm across its radius
            GUI.DrawTexture(new Rect(cx + HandleX0 * perM, cy - HandleY1 * perM, (HandleX1 - HandleX0) * perM, (HandleY1 - HandleY0) * perM), HandleTexture());
            GUI.DrawTexture(new Rect(cx - rp, cy - rp, 2f * rp, 2f * rp), PlateTexture());
            // the hour disc's teeth just inside the edge
            float rd = rp * 0.81f;
            GUI.color = new Color(0.10f, 0.075f, 0.04f, 0.97f);
            GUIUtility.RotateAroundPivot(-(float)MidnightAngle(day), new Vector2(cx, cy));
            GUI.DrawTexture(new Rect(cx - rd, cy - rd, 2f * rd, 2f * rd), DiscTexture());
            GUI.matrix = m;
            // the index: its left edge (seen from here, pointing up) through the middle
            float len = px * Mathf.Tan(ArmDeg * Mathf.Deg2Rad);
            float aw = px * Mathf.Tan(ArmWidthDeg * Mathf.Deg2Rad);
            float hole = px * Mathf.Tan(HoleDeg * Mathf.Deg2Rad);
            GUI.color = new Color(0.13f, 0.095f, 0.05f, 0.98f);
            GUIUtility.RotateAroundPivot(-arm, new Vector2(cx, cy));
            GUI.DrawTexture(new Rect(cx, cy - len, aw, len - hole * 1.6f), Texture2D.whiteTexture);
            GUI.color = new Color(0.55f, 0.42f, 0.22f, 0.9f);                         // the edge, worn bright
            GUI.DrawTexture(new Rect(cx, cy - len, Mathf.Max(1f, aw * 0.06f), len - hole * 1.6f), Texture2D.whiteTexture);
            GUI.matrix = m;
            GUI.color = old;
        }

        // the handle as docs/make_nocturnal.py cuts it, metres, the plate's centre at 0
        private const float HandleX0 = -0.030f, HandleX1 = 0.030f, HandleY0 = -0.168f, HandleY1 = -0.050f;
        private static Texture2D handleTex;

        private static bool InHandle(float x, float y)
        {
            bool In(float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
            if (In(0f, -0.141f, 0.0042f)) return false;                         // the cord hole
            if (In(0f, -0.141f, 0.0155f) || In(-0.012f, -0.151f, 0.0065f) || In(0.012f, -0.151f, 0.0065f) || In(0f, -0.158f, 0.0065f)) return true;
            if (In(-0.0175f, -0.073f, 0.0045f) || In(0.0175f, -0.073f, 0.0045f)) return true;
            if (y > -0.058f || y < -0.128f) return false;
            // the neck narrows from 26 mm a side at the plate to 8.5 at the knob
            float half;
            if (y > -0.090f) half = Mathf.Lerp(0.026f, 0.012f, (-0.058f - y) / 0.032f);
            else if (y > -0.110f) half = Mathf.Lerp(0.012f, 0.009f, (-0.090f - y) / 0.020f);
            else half = Mathf.Lerp(0.009f, 0.0085f, (-0.110f - y) / 0.018f);
            return Mathf.Abs(x) <= half;
        }

        private static Texture2D HandleTexture()
        {
            if (handleTex != null) return handleTex;
            const int NX = 128, NY = 256, SUB = 3;
            handleTex = new Texture2D(NX, NY, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[NX * NY];
            for (int j = 0; j < NY; j++)
            {
                for (int i = 0; i < NX; i++)
                {
                    int hit = 0;
                    for (int sj = 0; sj < SUB; sj++)
                    {
                        for (int si = 0; si < SUB; si++)
                        {
                            float x = Mathf.Lerp(HandleX0, HandleX1, (i + (si + 0.5f) / SUB) / NX);
                            float y = Mathf.Lerp(HandleY0, HandleY1, (j + (sj + 0.5f) / SUB) / NY);
                            if (InHandle(x, y)) hit++;
                        }
                    }
                    px[j * NX + i] = new Color32(255, 255, 255, (byte)(255 * hit / (SUB * SUB)));
                }
            }
            handleTex.SetPixels32(px);
            handleTex.Apply();
            return handleTex;
        }

        /// <summary>A disc, opaque, with the rivet's hole in the middle.</summary>
        private static Texture2D PlateTexture()
        {
            if (plateTex != null) return plateTex;
            const int N = 512;
            float hole = Mathf.Tan(HoleDeg * Mathf.Deg2Rad) / Mathf.Tan(PlateDeg * Mathf.Deg2Rad);
            plateTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[N * N];
            float edge = 2f / N;
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - r) / edge) * Mathf.Clamp01((r - hole) / edge);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            plateTex.SetPixels32(px);
            plateTex.Apply();
            return plateTex;
        }

        /// <summary>The hour disc's teeth as a silhouette ring (the long one at midnight, up).</summary>
        private static Texture2D DiscTexture()
        {
            if (discTex != null) return discTex;
            const int N = 512;
            int len = YearLen;
            double k = 15.0 * (1.0 + 1.0 / len);
            var teeth = new List<double> { 0.0, 180.0 };
            for (int h = 1; h < 12; h++) { teeth.Add(h * k); teeth.Add(-h * k); }
            discTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[N * N];
            const float body = 0.885f, tip = 1.0f;
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(-dx, dy) * Mathf.Rad2Deg;        // counter-clockwise from up
                    float a = 0f;
                    if (r > body - 0.012f && r <= tip)
                    {
                        foreach (double t in teeth)
                        {
                            float off = Mathf.Abs(Mathf.DeltaAngle(ang, (float)t));
                            float reach = t == 0.0 ? tip : body + (tip - body) * 0.55f;
                            float half = (t == 0.0 ? 4.2f : 3.0f) * Mathf.Clamp01((reach - r) / (reach - body + 0.012f));
                            if (off < half) { a = 1f; break; }
                        }
                    }
                    if (r > body - 0.03f && r < body) a = Mathf.Max(a, 0.35f);       // the disc's edge against the plate
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            discTex.SetPixels32(px);
            discTex.Apply();
            return discTex;
        }

        // ---------------- model ----------------
        // Built by docs/make_nocturnal.py.

        private static Material brass;
        private static Dictionary<string, Mesh> parts;

        private static Texture2D Tex(string file, bool linear)
        {
            Texture2D t = ModResources.Texture("Nocturnal/" + file, linear, out bool shared);
            if (t == null) return null;
            // the scales are fine work: keep them sharp a little further off, and the big albedo small in memory
            t.anisoLevel = 9;
            t.mipMapBias = -0.7f;
            if (!shared)
            {
                t.name = "nocturnal " + file;
                if (file.StartsWith("albedo")) t.Compress(true);
                t.Apply(false, true);
            }
            return t;
        }

        private static bool LoadModel()
        {
            if (parts != null) return true;
            byte[] bin = ModResources.Read("Nocturnal/nocturnal.bin", out _);
            if (bin == null) return false;
            var found = new Dictionary<string, Mesh>();
            using (var r = new System.IO.BinaryReader(new System.IO.MemoryStream(bin)))
            {
                if (new string(r.ReadChars(4)) != "NOCT") throw new Exception("bad nocturnal.bin");
                int np = r.ReadInt32();
                for (int p = 0; p < np; p++)
                {
                    string name = new string(r.ReadChars(r.ReadInt32()));
                    int n = r.ReadInt32();
                    var v = new Vector3[n]; var nn = new Vector3[n]; var uv = new Vector2[n];
                    for (int i = 0; i < n; i++) v[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    for (int i = 0; i < n; i++) nn[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    for (int i = 0; i < n; i++) uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    var tris = new int[r.ReadInt32()];
                    for (int i = 0; i < tris.Length; i++) tris[i] = r.ReadInt32();
                    var mesh = new Mesh { name = "nocturnal " + name };
                    mesh.vertices = v; mesh.normals = nn; mesh.uv = uv;
                    mesh.triangles = tris;
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    found[name] = mesh;
                }
            }
            string y = YearLen.ToString();
            Shader sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            brass = new Material(sh) { name = "nocturnal brass" };
            Texture2D alb = Tex("albedo" + y + ".jpg", false), mg = Tex("metalgloss" + y + ".png", true), nm = Tex("normal" + y + ".png", true);
            if (alb != null) brass.SetTexture("_MainTex", alb);
            if (mg != null)
            {
                brass.SetTexture("_MetallicGlossMap", mg);
                brass.EnableKeyword("_METALLICGLOSSMAP");
                brass.SetFloat("_GlossMapScale", 1f);
            }
            if (nm != null)
            {
                brass.SetTexture("_BumpMap", nm);
                brass.EnableKeyword("_NORMALMAP");
                brass.SetFloat("_BumpScale", 1f);
            }
            parts = found;
            return true;
        }

        private static GameObject Part(string mesh, string name, Transform parent, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = parts[mesh];
            go.AddComponent<MeshRenderer>().sharedMaterial = brass;
            return go;
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
                if (ShopSpots.TryGet(ShopSpots.Nocturnal, island, out ShopSpots.Spot spot))
                {
                    ShopSpots.Apply(clone.transform, shop, spot);
                }
                else
                {
                    clone.transform.localPosition = sp.transform.localPosition + new Vector3(-0.25f, 0.02f, 0.15f);
                    clone.transform.rotation = shop.rotation * Quaternion.Euler(90f, 0f, 0f);        // lying face up
                }
                ShopItemSpawner csp = clone.GetComponent<ShopItemSpawner>();
                AccessTools.Field(typeof(ShopItemSpawner), "item").SetValue(csp, null);
                csp.enabled = false;
                Renderer r = clone.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                var hook = clone.AddComponent<LoreSpawnerHook>();
                hook.spawner = csp;
                hook.prefabIndex = PrefabIndex;
                ShopSpots.RegisterSpawner(ShopSpots.Nocturnal, island, clone.transform);
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
                Debug.LogWarning("RealisticSkies: vanilla scroll (prefab 91) not found, no nocturnal");
                return;
            }
            if (dir.directory.Length > PrefabIndex && dir.directory[PrefabIndex] != null)
            {
                Debug.LogWarning("RealisticSkies: prefab " + PrefabIndex + " is already taken, no nocturnal");
                return;
            }
            try
            {
                if (!LoadModel())
                {
                    Debug.LogWarning("RealisticSkies: nocturnal model missing, no nocturnal");
                    return;
                }
                holder = new GameObject("RealisticSkies nocturnal holder");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
                GameObject go = UnityEngine.Object.Instantiate(dir.directory[SourceIndex], holder.transform);
                go.name = PrefabIndex + " " + ItemName;

                ShipItemScroll old = go.GetComponent<ShipItemScroll>();
                ModItemNocturnal item = go.AddComponent<ModItemNocturnal>();
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
                box.center = new Vector3(0f, -0.045f, -0.002f);
                box.size = new Vector3(0.144f, 0.235f, 0.014f);

                go.GetComponent<MeshFilter>().sharedMesh = parts["plate"];
                go.GetComponent<MeshRenderer>().sharedMaterial = brass;
                Part("disc" + YearLen, "disc", go.transform, go.layer);
                Part("arm", "arm", go.transform, go.layer);
                Part("boss", "boss", go.transform, go.layer);

                item.name = ItemName;
                item.amount = 0f;
                item.health = 0f;
                item.holdDistance = 0.22f;
                item.inventoryRotationX = 0f;
                item.inventoryRotation = 0f;
                item.value = 50;
                if (Patches.itemPrefabs.TryGetValue(703, out GameObject chart) && chart != null
                    && chart.GetComponent<ShipItem>() is ShipItem chartItem && chartItem.value > 0)
                {
                    item.value = Mathf.RoundToInt(chartItem.value * 0.2f);      // about 50 crowns
                }
                item.mass = 0.6f;

                // hangs on a lamp hook by the hole in its handle, handle up
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
                Debug.Log("RealisticSkies: added the nocturnal as prefab " + PrefabIndex + " (" + YearLen + "-day year)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not build the nocturnal: " + e);
            }
        }

        /// <summary>Where the hook goes through the hole in the handle (the hole's far edge), in its own space.</summary>
        internal static readonly Vector3 HangPoint = new Vector3(0f, -0.141f - 0.0042f + 0.0008f, 0f);
    }
}

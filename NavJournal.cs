using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The navigator's journal: the sights, and the sums done from the Almanac.
    ///
    /// Pages:
    /// - Lunar distance: the measured distance is written in by the quintant, and the time by the
    ///   pocket watch if one is carried (both can be corrected). The navigator picks the body it was
    ///   measured from (the Sun or one of today's stars in the Almanac's lunar table); the journal
    ///   looks it up in the table and gives the time at the zero meridian and the watch's error.
    /// - Sights: every altitude read on the quintant is a new line, with the watch time if a watch is
    ///   carried. The navigator picks the body and says whether it was east or west; with the
    ///   latitude and the Almanac's GHA and Dec the journal works out the longitude.
    /// - Notes: free writing.
    /// What the navigator says is taken as said: the wrong star, side or time gives a wrong answer.
    ///
    /// Fields are typed on the keyboard. Kept per save slot in BepInEx/config/RealisticSkies/journal_slotN.txt.
    /// </summary>
    internal static class NavJournal
    {
        private const int NoBody = int.MinValue;

        private class SightLine
        {
            public string alt = "", time = "";
            public int side;                    // 0 not said, 1 east, 2 west
            public int body = NoBody;
        }

        // ---------------- what is written in it ----------------

        private static string errText = "", lunarDist = "", lunarWatch = "", notes = "", latText = "";
        private static bool latSouth, useErr = true;
        private static int lunarLimb;               // 0 not said, 1 near edge (to the star), 2 far edge
        private static bool lunarCorrect = true;    // add / take off the semi-diameter for the navigator
        private static int lunarBody = Lunar.SunIndex;
        private static double lunarAt = double.NaN;     // the true time the lunar was taken (to set the glass by)
        private static int tab;
        private static readonly List<SightLine> sights = new List<SightLine>();

        /// <summary>
        /// What the navigator knows of a chronometer: its error (hours fast) at two moments, and the
        /// rate on the watchmaker's certificate. The rate is the certificate's until two errors found
        /// half a day apart give one of their own.
        /// </summary>
        private class ChronoLine
        {
            public int serial;
            public double t1 = double.NaN, e1, t2 = double.NaN, e2;
            public double cert = double.NaN;        // minutes a day

            public void Fix(double t, double e)
            {
                t1 = t2; e1 = e2;
                t2 = t; e2 = e;
            }

            public double Rate
            {
                get
                {
                    if (!double.IsNaN(t1) && t2 - t1 >= 12.0) return (e2 - e1) / (t2 - t1) * 24.0 * 60.0;
                    return cert;
                }
            }

            /// <summary>The error expected now, hours fast (NaN: never found).</summary>
            public double ErrorAt(double now)
            {
                if (double.IsNaN(t2)) return double.NaN;
                double r = Rate;
                return double.IsNaN(r) ? e2 : e2 + r * (now - t2) / (24.0 * 60.0);
            }
        }

        private static readonly List<ChronoLine> chronos = new List<ChronoLine>();

        private static ChronoLine Chrono(int serial)
        {
            foreach (ChronoLine c in chronos) if (c.serial == serial) return c;
            var n = new ChronoLine { serial = serial };
            chronos.Add(n);
            return n;
        }
        private const int MaxSights = 40;
        private static int loadedSlot = -1;

        internal static bool IsOpen { get; private set; }

        private static int closedFrame = -100;
        private static bool waitRelease;            // closed with a mouse button down: wait for it to come up

        /// <summary>
        /// Open, or just closed with a mouse button still down: the click that closed the journal
        /// must not reach the world (a released button drops what you hold).
        /// </summary>
        internal static bool Busy
        {
            get
            {
                if (IsOpen) return true;
                if (waitRelease && !Input.GetMouseButton(0) && !Input.GetMouseButton(1)) waitRelease = false;
                return waitRelease || Time.frameCount - closedFrame < 3;
            }
        }

        // ---------------- open / close ----------------

        internal static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        internal static void Open()
        {
            if (IsOpen || GameState.inCursorMenu || GameState.sleeping || GameState.inBed || !GameState.playing) return;
            Load();
            IsOpen = true;
            Refs.SetPlayerControl(false);
            MouseLook.ToggleMouseLookAndCursor(false);
            UISoundPlayer.instance.PlayParchmentSound();
        }

        internal static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            closedFrame = Time.frameCount;
            waitRelease = Input.GetMouseButton(0) || Input.GetMouseButton(1);
            GUIUtility.keyboardControl = 0;
            picking = -1;
            Save();
            Refs.SetPlayerControl(true);
            MouseLook.ToggleMouseLookAndCursor(true);
            UISoundPlayer.instance.PlayParchmentSound();
        }

        /// <summary>Called every frame by the plugin: the journal key, and closing when it can't stay open.</summary>
        internal static void Tick()
        {
            if (Plugin.configJournalKey.Value.IsDown() && !(IsOpen && GUIUtility.keyboardControl != 0))
            {
                if (IsOpen) Close();
                else if (Carried() != null) Open();
            }
            if (IsOpen && (!GameState.playing || GameState.sleeping)) Close();
        }

        /// <summary>The journal in a pocket (an inventory slot) or in hand, or null.</summary>
        internal static ModItemJournal Carried()
        {
            GPButtonInventorySlot[] slots = GPButtonInventorySlot.inventorySlots;
            if (slots != null)
            {
                foreach (GPButtonInventorySlot slot in slots)
                {
                    if (slot != null && slot.currentItem is ModItemJournal pocket) return pocket;
                }
            }
            foreach (ModItemJournal j in UnityEngine.Object.FindObjectsOfType<ModItemJournal>())
            {
                if (j.sold && (j.held != null || j.GetCurrentInventorySlot() >= 0)) return j;
            }
            return null;
        }

        private const float AlmanacReach = 4f;
        private static bool almanacSeen;
        private static float almanacCheckedAt = -10f;

        /// <summary>The Almanac at hand, looked for once a second while the journal is open.</summary>
        private static bool HaveAlmanac
        {
            get
            {
                if (Time.unscaledTime - almanacCheckedAt > 1f)
                {
                    almanacCheckedAt = Time.unscaledTime;
                    almanacSeen = AlmanacAtHand();
                }
                return almanacSeen;
            }
        }

        /// <summary>An Almanac of yours in hand, in a pocket, or lying within reach.</summary>
        internal static bool AlmanacAtHand()
        {
            Camera cam = Camera.main;
            foreach (ModItemAlmanac a in UnityEngine.Object.FindObjectsOfType<ModItemAlmanac>())
            {
                if (!a.sold) continue;
                if (a.held != null || a.GetCurrentInventorySlot() >= 0) return true;
                if (cam != null && Vector3.Distance(a.transform.position, cam.transform.position) <= AlmanacReach) return true;
            }
            return false;
        }

        /// <summary>The pocket watch in hand or in a pocket (the one in hand first).</summary>
        internal static ModItemPocketWatch Watch()
        {
            ModItemPocketWatch pocket = null;
            foreach (ModItemPocketWatch w in UnityEngine.Object.FindObjectsOfType<ModItemPocketWatch>())
            {
                if (!w.sold || (w.held == null && w.GetCurrentInventorySlot() < 0)) continue;
                if (w.held != null) return w;
                pocket = w;
            }
            return pocket;
        }

        /// <summary>What the carried watch says, or nothing without one; the local time where the journal notes that.</summary>
        private static string WatchNow()
        {
            if (Mode.GlassTimeInJournal)
            {
                ModItemWatchGlass g = KeptGlass();
                return g != null ? NavMath.Clock(Celestial.Repeat(g.ReckonedTime, 24.0)) : "";
            }
            ModItemPocketWatch w = Watch();
            return w != null && Mode.PocketWatches != Keeping.Dead ? NavMath.Clock(w.ShownHours) : "";
        }

        private static bool GlassTime => Mode.GlassTimeInJournal;
        private const float GlassReach = 25f;

        /// <summary>A watch glass nearby that keeps a reckoned time (on your ship, by and large).</summary>
        private static ModItemWatchGlass KeptGlass()
        {
            Camera cam = Camera.main;
            if (cam == null) return null;
            foreach (ModItemWatchGlass g in ModItemWatchGlass.Near(cam.transform.position, GlassReach))
            {
                if (!double.IsNaN(g.ReckonedTime)) return g;
            }
            return null;
        }

        // ---------------- written in by the watchmaker ----------------

        /// <summary>A pocket watch regulated: it shows the true time, so its error is nothing.</summary>
        internal static void WatchRegulated()
        {
            Load();
            errText = "+00:00:00";
            AddNote("Day " + GameState.day + ": pocket watch regulated by the watchmaker, set to the time at the zero meridian");
            Save();
        }

        /// <summary>The watchmaker's certificate for a chronometer: right at this moment, and its rate.</summary>
        internal static void Certificate(int serial, double at, float rate)
        {
            Load();
            ChronoLine c = Chrono(serial);
            c.t1 = double.NaN;
            c.t2 = at;
            c.e2 = 0;
            c.cert = rate;
            AddNote("Day " + GameState.day + ": chronometer No. " + serial + " regulated by the watchmaker, rate " + RateText(rate));
            Save();
        }

        private static void AddNote(string line)
        {
            notes = string.IsNullOrEmpty(notes) ? line : line + "\n" + notes;
        }

        private static string RateText(double minPerDay)
        {
            double s = minPerDay * 60.0;
            return (s >= 0 ? "gains " : "loses ") + Math.Abs(s).ToString("0", CultureInfo.InvariantCulture) + " s a day";
        }

        // ---------------- written in by the quintant ----------------

        /// <summary>An altitude was read on the quintant: a new line on the Sights page.</summary>
        internal static void NoteAltitude(double altitude)
        {
            if (Carried() == null)
            {
                Debug.Log("RealisticSkies: quintant altitude " + NavMath.Angle(altitude) + ", no journal carried");
                return;
            }
            Load();
            string t = WatchNow();
            sights.Insert(0, new SightLine { alt = AngleField(altitude), time = t });
            if (sights.Count > MaxSights) sights.RemoveAt(sights.Count - 1);
            Notify("Journal: altitude " + NavMath.Angle(altitude) + (t.Length > 0 ? " at " + t : "") + " written down");
            Save();
        }

        /// <summary>A lunar distance was measured: it goes on the Lunar page, the rest is the navigator's.</summary>
        internal static void NoteLunar(double distance, int limb = 0)
        {
            if (Carried() == null)
            {
                Debug.Log("RealisticSkies: lunar distance " + NavMath.Angle(distance) + ", no journal carried");
                return;
            }
            Load();
            lunarDist = AngleField(distance);
            lunarWatch = WatchNow();
            lunarAt = NavMath.NowAbs;
            lunarLimb = limb;       // which edge it was: the navigator says (the cross-staff always takes the near one)
            tab = 0;
            Notify("Journal: lunar distance " + NavMath.Angle(distance) + (lunarWatch.Length > 0 ? " at " + lunarWatch : "") + " written down");
            Save();
        }

        /// <summary>
        /// The nocturnal gave the local time: a line in the notes, and against the time at the zero
        /// meridian (the glass's reckoning, or the watch less its error) a rough longitude.
        /// </summary>
        internal static void NoteNocturnal(double local, string night)
        {
            if (Carried() == null) return;
            Load();
            double gmt = double.NaN;
            string by = "";
            if (GlassTime)
            {
                ModItemWatchGlass g = KeptGlass();
                if (g != null) { gmt = Celestial.Repeat(g.ReckonedTime, 24.0); by = "the glass"; }
            }
            else
            {
                ModItemPocketWatch w = Watch();
                double err = useErr ? NavMath.ParseTime(errText) : 0.0;
                if (w != null && Mode.PocketWatches != Keeping.Dead && !double.IsNaN(err))
                {
                    gmt = Celestial.Repeat(w.ShownHours - err, 24.0);
                    by = useErr ? "the watch less its error" : "the watch";
                }
            }
            string line = "Day " + GameState.day + ", night of " + night + ": nocturnal " + ModItemNocturnal.Hm(local) + " local";
            if (!double.IsNaN(gmt))
            {
                double lon = NavMath.Wrap180((local - gmt) * 15.0);
                line += "; " + NavMath.Clock(gmt).Substring(0, 5) + " at the zero meridian by " + by + ": longitude about " +
                        Math.Abs(lon).ToString("0.0", CultureInfo.InvariantCulture) + "° " + (lon < 0 ? "W" : "E");
            }
            AddNote(line);
            Notify("Journal: nocturnal " + ModItemNocturnal.Hm(local) + " written in the notes");
            Save();
        }

        private static string AngleField(double deg)
        {
            double tm = Math.Round(Math.Abs(deg) * 60.0, 1);
            int d = (int)Math.Floor(tm / 60.0);
            return (deg < 0 ? "-" : "") + d + " " + (tm - d * 60.0).ToString("00.0", CultureInfo.InvariantCulture);
        }

        private static void Notify(string msg)
        {
            try
            {
                NotificationUi.instance.ShowNotification(msg);
            }
            catch (Exception)
            {
                Debug.Log("RealisticSkies: " + msg);
            }
        }

        /// <summary>The bodies of today's lunar table, as the Almanac prints them: the Sun, then its stars.</summary>
        private static List<int> TodaysBodies()
        {
            int day = GameState.day;
            int len = Mathf.Max(1, Patches.yearLen);
            int day1 = day - (day % len) % 2;          // the first day of the Almanac's two-day page
            var list = new List<int> { Lunar.SunIndex };
            list.AddRange(Lunar.TableStars(day1, 8, new List<int>()));
            return list;
        }

        // ---------------- storage ----------------

        private static string FilePath => Path.Combine(Path.Combine(Paths.ConfigPath, "RealisticSkies"), "journal_slot" + SaveSlots.currentSlot + ".txt");

        private static string Esc(string s) => (s ?? "").Replace("|", "/").Replace("\r", " ").Replace("\n", " ");

        private static void Load()
        {
            if (loadedSlot == SaveSlots.currentSlot) return;
            loadedSlot = SaveSlots.currentSlot;
            errText = lunarDist = lunarWatch = notes = latText = "";
            lunarAt = double.NaN;
            latSouth = false; useErr = true; lunarLimb = 0; lunarCorrect = true;
            lunarBody = Lunar.SunIndex;
            tab = 0;
            sights.Clear();
            chronos.Clear();
            var noteLines = new List<string>();
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (string line in File.ReadAllLines(FilePath))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                        switch (k)
                        {
                            case "watchError": errText = v; break;
                            case "lat": latText = v; break;
                            case "latSouth": latSouth = v == "1"; break;
                            case "useErr": useErr = v == "1"; break;
                            case "lunarLimb": int.TryParse(v, out lunarLimb); break;
                            case "lunarCorrect": lunarCorrect = v == "1"; break;
                            case "tab": int.TryParse(v, out tab); break;
                            case "lunarBody": int.TryParse(v, out lunarBody); break;
                            case "lunarDist": lunarDist = v; break;
                            case "lunarWatch": lunarWatch = v; break;
                            case "lunarAt": lunarAt = Dbl(v); break;
                            case "sight":
                                string[] p = v.Split('|');
                                var sl = new SightLine { alt = p.Length > 0 ? p[0] : "", time = p.Length > 1 ? p[1] : "" };
                                if (p.Length > 2) int.TryParse(p[2], out sl.side);
                                if (p.Length > 3 && !int.TryParse(p[3], out sl.body)) sl.body = NoBody;
                                if (sl.body != NoBody && (sl.body < Lunar.SunIndex || sl.body >= Lunar.Stars.Count)) sl.body = NoBody;
                                sights.Add(sl);
                                break;
                            case "note": noteLines.Add(v); break;
                            case "chrono":
                                string[] c = v.Split('|');
                                if (c.Length < 6 || !int.TryParse(c[0], out int serial)) break;
                                var cl = new ChronoLine { serial = serial, t1 = Dbl(c[1]), e1 = Dbl(c[2]), t2 = Dbl(c[3]), e2 = Dbl(c[4]), cert = Dbl(c[5]) };
                                chronos.Add(cl);
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not read the journal: " + e.Message);
            }
            notes = string.Join("\n", noteLines.ToArray());
            if (lunarBody >= Lunar.Stars.Count || lunarBody < Lunar.SunIndex) lunarBody = Lunar.SunIndex;
            tab = Mathf.Clamp(tab, 0, 3);
        }

        private static double Dbl(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : double.NaN;

        private static string Str(double d) => double.IsNaN(d) ? "-" : d.ToString("R", CultureInfo.InvariantCulture);

        private static void Save()
        {
            if (loadedSlot < 0) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var lines = new List<string>
                {
                    "watchError=" + errText, "lat=" + latText, "latSouth=" + (latSouth ? 1 : 0), "useErr=" + (useErr ? 1 : 0), "tab=" + tab, "lunarBody=" + lunarBody, "lunarDist=" + lunarDist, "lunarWatch=" + lunarWatch, "lunarAt=" + Str(lunarAt),
                    "lunarLimb=" + lunarLimb, "lunarCorrect=" + (lunarCorrect ? 1 : 0),
                };
                foreach (SightLine s in sights) lines.Add("sight=" + Esc(s.alt) + "|" + Esc(s.time) + "|" + s.side + "|" + s.body);
                foreach (ChronoLine c in chronos)
                {
                    lines.Add("chrono=" + c.serial + "|" + Str(c.t1) + "|" + Str(c.e1) + "|" + Str(c.t2) + "|" + Str(c.e2) + "|" + Str(c.cert));
                }
                foreach (string l in (notes ?? "").Replace("\r", "").Split('\n')) lines.Add("note=" + l);
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not save the journal: " + e.Message);
            }
        }

        // ---------------- the page ----------------

        private const float W = 1100f, H = 680f;
        private static Rect win;
        private static bool stylesReady;
        private static GUIStyle sPage, sTitle, sHead, sText, sSmall, sField, sButton, sTab, sTabOn, sResult, sWarn, sList, sArea;
        private static Vector2 sightScroll, noteScroll, pickScroll;
        private static int picking = -1;        // the sight line whose body is being picked

        private static Texture2D Solid(Color c, Color border, int size = 16, int bw = 0)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < bw || y < bw || x >= size - bw || y >= size - bw;
                    t.SetPixel(x, y, edge ? border : c);
                }
            }
            t.Apply();
            return t;
        }

        private static Texture2D Parchment()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var rnd = new System.Random(7);
            var baseC = new Color(0.92f, 0.86f, 0.72f);
            var edge = new Color(0.55f, 0.40f, 0.24f);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float e = Mathf.Min(Mathf.Min(x, n - 1 - x), Mathf.Min(y, n - 1 - y)) / 10f;
                    float noise = (float)rnd.NextDouble() * 0.04f - 0.02f;
                    Color c = Color.Lerp(edge, baseC, Mathf.Clamp01(e)) + new Color(noise, noise, noise * 0.8f);
                    c.a = 1f;
                    t.SetPixel(x, y, c);
                }
            }
            t.Apply();
            return t;
        }

        private static void MakeStyles()
        {
            // the textures are held only here, so the game's unloading of unused assets (on loading a
            // save, say) would take them; they are kept from it, and made again if lost all the same
            if (stylesReady && sPage != null && sPage.normal.background != null) return;
            stylesReady = true;
            Font font = null;
            if (Patches.itemPrefabs.TryGetValue(701, out GameObject almanac) && almanac != null)
            {
                TextMesh tm = almanac.GetComponentInChildren<TextMesh>(true);
                if (tm != null) font = tm.font;
            }
            var ink = new Color(0.20f, 0.13f, 0.07f);
            var red = new Color(0.50f, 0.10f, 0.06f);

            sPage = new GUIStyle { padding = new RectOffset(34, 34, 26, 26) };
            sPage.normal.background = Parchment();
            sPage.border = new RectOffset(12, 12, 12, 12);

            sText = new GUIStyle { font = font, fontSize = 22, wordWrap = true, richText = true };
            sText.normal.textColor = ink;
            sTitle = new GUIStyle(sText) { fontSize = 34, alignment = TextAnchor.MiddleCenter };
            sTitle.normal.textColor = red;
            sHead = new GUIStyle(sText) { fontSize = 25, fontStyle = FontStyle.Bold };
            sHead.normal.textColor = red;
            sSmall = new GUIStyle(sText) { fontSize = 18 };
            sSmall.normal.textColor = new Color(0.35f, 0.25f, 0.15f);
            sResult = new GUIStyle(sText) { fontSize = 23 };
            sWarn = new GUIStyle(sText) { fontSize = 20 };
            sWarn.normal.textColor = red;

            sField = new GUIStyle { font = font, fontSize = 22, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 4, 4) };
            sField.normal.background = Solid(new Color(0.97f, 0.94f, 0.85f), new Color(0.55f, 0.42f, 0.28f), 16, 1);
            sField.focused.background = Solid(new Color(1f, 0.98f, 0.90f), red, 16, 2);
            sField.normal.textColor = sField.focused.textColor = ink;
            sField.border = new RectOffset(3, 3, 3, 3);

            sButton = new GUIStyle { font = font, fontSize = 20, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(10, 10, 4, 4) };
            sButton.normal.background = Solid(new Color(0.82f, 0.72f, 0.54f), new Color(0.45f, 0.32f, 0.18f), 16, 1);
            sButton.hover.background = Solid(new Color(0.88f, 0.79f, 0.60f), new Color(0.45f, 0.32f, 0.18f), 16, 1);
            sButton.active.background = Solid(new Color(0.72f, 0.60f, 0.42f), new Color(0.45f, 0.32f, 0.18f), 16, 1);
            sButton.normal.textColor = sButton.hover.textColor = sButton.active.textColor = ink;
            sButton.border = new RectOffset(3, 3, 3, 3);

            sTab = new GUIStyle(sButton) { fontSize = 22 };
            sTabOn = new GUIStyle(sTab);
            sTabOn.normal.background = sTabOn.hover.background = Solid(new Color(0.96f, 0.91f, 0.78f), red, 16, 2);
            sTabOn.normal.textColor = sTabOn.hover.textColor = red;

            sList = new GUIStyle(sButton) { fontSize = 19 };
            sArea = new GUIStyle(sField) { wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
        }


        /// <summary>Drawn from the plugin's OnGUI.</summary>
        internal static void OnGUI()
        {
            if (!IsOpen) return;
            MakeStyles();
            // the text caret and selection, dark on the light page (the default caret is white)
            GUI.skin.settings.cursorColor = new Color(0.20f, 0.13f, 0.07f);
            GUI.skin.settings.cursorFlashSpeed = 1f;
            GUI.skin.settings.selectionColor = new Color(0.55f, 0.35f, 0.15f, 0.45f);
            float k = Screen.height / 1080f;
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
            float vw = Screen.width / k;
            if (win.width < 1f) win = new Rect((vw - W) / 2f, (1080f - H) / 2f, W, H);
            win = GUI.Window(0x52534A, win, DrawWindow, GUIContent.none, sPage);
            GUI.matrix = old;
        }

        private static void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Navigator's Journal", sTitle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Close", sButton, GUILayout.Width(110))) { Close(); return; }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Latitude", sText, GUILayout.Width(110));
            latText = GUILayout.TextField(latText, 12, sField, GUILayout.Width(130));
            if (GUILayout.Button(latSouth ? "S" : "N", sButton, GUILayout.Width(44))) latSouth = !latSouth;
            double latDec = Latitude();
            GUILayout.Label(double.IsNaN(latDec) ? "" : " (" + latDec.ToString("0.00", CultureInfo.InvariantCulture) + "°)", sSmall, GUILayout.Width(90));
            GUILayout.Label(GlassTime ? "Glass error" : "Watch error", sText, GUILayout.Width(170));
            errText = GUILayout.TextField(errText, 12, sField, GUILayout.Width(140));
            GUILayout.Label(" + fast, - slow", sSmall);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            bool chronoTab = ChronoTab;
            if (!chronoTab && tab == 3) tab = 0;
            string[] tabs = chronoTab ? new[] { "Lunar distance", "Sights", "Chronometers", "Notes" } : new[] { "Lunar distance", "Sights", "Notes" };
            int[] ids = chronoTab ? new[] { 0, 1, 3, 2 } : new[] { 0, 1, 2 };
            for (int i = 0; i < tabs.Length; i++)
            {
                if (GUILayout.Button(tabs[i], ids[i] == tab ? sTabOn : sTab, GUILayout.Height(40))) { tab = ids[i]; picking = -1; }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            if (picking >= 0 && tab == 1) DrawPicker();
            else if (tab == 0) DrawLunar();
            else if (tab == 1) DrawSights();
            else if (tab == 3) DrawChronometers();
            else DrawNotes();

            GUI.DragWindow(new Rect(0, 0, 10000, 60));
        }

        private static void Row(string label, ref string field, string hint)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, sText, GUILayout.Width(230));
            field = GUILayout.TextField(field, 14, sField, GUILayout.Width(170));
            GUILayout.Space(10);
            GUILayout.Label(hint, sSmall);
            GUILayout.EndHorizontal();
        }

        private static void DrawLunar()
        {
            GUILayout.Label("Lunar distance: the time at the zero meridian", sHead);

            // the body: one of today's table, chosen by the navigator
            List<int> bodies = TodaysBodies();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Moon to", sText, GUILayout.Width(230));
            GUILayout.BeginVertical();
            for (int r = 0; r * 5 < bodies.Count; r++)
            {
                GUILayout.BeginHorizontal();
                for (int c = r * 5; c < Math.Min(bodies.Count, r * 5 + 5); c++)
                {
                    int b = bodies[c];
                    if (GUILayout.Button(Lunar.Name(b), b == lunarBody ? sTabOn : sList, GUILayout.Width(138), GUILayout.Height(34))) lunarBody = b;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            bool known = bodies.Contains(lunarBody);
            Row("Distance", ref lunarDist, "to the Moon's edge, written in by the quintant");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Edge of the Moon", sText, GUILayout.Width(230));
            string limb = lunarLimb == 1 ? "near the star" : lunarLimb == 2 ? "far from the star" : "which edge?";
            if (GUILayout.Button(limb, sButton, GUILayout.Width(200))) lunarLimb = lunarLimb == 1 ? 2 : 1;
            GUILayout.Space(10);
            lunarCorrect = GUILayout.Toggle(lunarCorrect, lunarCorrect ? " [x] add the semi-diameter for me" : " [ ] I give the distance to the centre", sSmall);
            GUILayout.EndHorizontal();
            if (GlassTime) Row("Time by the glass", ref lunarWatch, "hh:mm:ss, by the ship's glass once it is set; empty for the first");
            else Row("Time by your watch", ref lunarWatch, "hh:mm:ss, from the pocket watch, or type it");
            GUILayout.Space(12);

            double measured = NavMath.ParseAngle(lunarDist);
            double watch = NavMath.ParseTime(lunarWatch);
            double sd = Lunar.MoonSemiDiameter;
            double dist = measured;
            if (lunarCorrect)
            {
                if (lunarLimb == 0)
                {
                    GUILayout.Label("Which edge of the Moon did you bring to the star?", sWarn);
                    return;
                }
                if (double.IsNaN(sd))
                {
                    GUILayout.Label("The Moon's semi-diameter is not known yet: look at the Moon once.", sWarn);
                    return;
                }
                // to the near edge the distance is short of the centre by the semi-diameter, to the far edge over it
                dist = measured + (lunarLimb == 1 ? sd : -sd);
                GUILayout.Label("To the centre:  " + NavMath.Angle(measured) + (lunarLimb == 1 ? " + " : " - ") +
                                NavMath.Angle(sd) + " = " + NavMath.Angle(dist), sResult);
            }
            if (!known)
            {
                GUILayout.Label("Pick the body you measured from.", sWarn);
                return;
            }
            if (double.IsNaN(dist) || (double.IsNaN(watch) && !GlassTime))
            {
                GUILayout.Label("Fill in the distance and the time of the sight.", sWarn);
                return;
            }
            if (!HaveAlmanac)
            {
                GUILayout.Label("Nothing to look the distance up in: the Almanac isn't at hand.", sWarn);
                return;
            }
            // where in the table to look: around the time of the sight (without a time yet, the
            // moment it was taken; only the search window comes from it, the answer from the table)
            double approx = double.IsNaN(watch)
                ? (double.IsNaN(lunarAt) ? NavMath.NowAbs : lunarAt)
                : NavMath.OnNearestDay(watch);
            if (!NavMath.LunarTime(lunarBody, dist, approx, out double gmt))
            {
                GUILayout.Label("No such distance from " + Lunar.Name(lunarBody) + " in the table within 12 hours of " +
                                NavMath.Clock(double.IsNaN(watch) ? approx : watch) + ". Is it the right body?", sWarn);
                return;
            }
            double h0 = Math.Floor(gmt);
            GUILayout.Label("From the Almanac:  " + NavMath.Clock(h0) + "  " + NavMath.Angle(NavMath.TableDistance(lunarBody, h0)) +
                            "     " + NavMath.Clock(h0 + 1) + "  " + NavMath.Angle(NavMath.TableDistance(lunarBody, h0 + 1)), sResult);
            GUILayout.Label("Time at the zero meridian:  <b>" + NavMath.Clock(gmt) + "</b>", sResult);
            double err = double.IsNaN(watch) ? double.NaN : Celestial.Repeat(watch - gmt + 12.0, 24.0) - 12.0;
            if (GlassTime)
            {
                if (!double.IsNaN(err))
                {
                    GUILayout.Label("The ship's glass is " + (err >= 0 ? "fast" : "slow") + " by <b>" + NavMath.Span(Math.Abs(err)).Substring(1) + "</b>", sResult);
                }
                GUILayout.Space(8);
                GUILayout.BeginHorizontal();
                GUI.enabled = !double.IsNaN(err);
                if (GUILayout.Button("Take this as the glass error", sButton, GUILayout.Width(360)))
                {
                    errText = (err < 0 ? "-" : "+") + NavMath.Clock(Math.Abs(err));
                    AddNote("Day " + (int)Math.Floor(gmt / 24.0) + ", " + NavMath.Clock(gmt) + " at the zero meridian: lunar, " + Lunar.Name(lunarBody) + " " +
                            NavMath.Angle(dist) + ", glass " + (err >= 0 ? "fast " : "slow ") + NavMath.Span(Math.Abs(err)).Substring(1));
                    Save();
                    UISoundPlayer.instance.PlayParchmentSound();
                }
                SetGlassButton(gmt, true);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                return;
            }
            GUILayout.Label("Your watch is " + (err >= 0 ? "fast" : "slow") + " by <b>" + NavMath.Span(Math.Abs(err)).Substring(1) + "</b>", sResult);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Take this as the watch error", sButton, GUILayout.Width(360)))
            {
                errText = (err < 0 ? "-" : "+") + NavMath.Clock(Math.Abs(err));
                int day = (int)Math.Floor(gmt / 24.0);
                AddNote("Day " + day + ", " + NavMath.Clock(gmt) + " at the zero meridian: lunar, " + Lunar.Name(lunarBody) + " " +
                        NavMath.Angle(dist) + ", watch " + (err >= 0 ? "fast " : "slow ") + NavMath.Span(Math.Abs(err)).Substring(1));
                Save();
                UISoundPlayer.instance.PlayParchmentSound();
            }
            ModItemPocketWatch w = Watch();
            GUI.enabled = w != null && Mode.PocketWatches == Keeping.Imperfect;
            if (GUILayout.Button("Set the watch right by it", sButton, GUILayout.Width(360)))
            {
                // the hands put back by the error found: from now on the watch tells the zero meridian's time
                w.Correct(err);
                errText = "+00:00:00";
                lunarWatch = NavMath.Clock(Celestial.Repeat(watch - err, 24.0));
                AddNote("Day " + (int)Math.Floor(gmt / 24.0) + ": watch set right by the lunar (it was " + (err >= 0 ? "fast " : "slow ") +
                        NavMath.Span(Math.Abs(err)).Substring(1) + ")");
                Save();
                UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.15f, 2.2f);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            SetGlassButton(gmt, false);
        }

        /// <summary>
        /// "Set the ship's glass by it": the zero meridian's time at the sight, against the true moment it
        /// was taken; from then on the watch glass keeps that reckoning, and its error with it. In the
        /// later eras too, for whoever wants it.
        /// </summary>
        private static void SetGlassButton(double gmt, bool clearsError)
        {
            Camera cam = Camera.main;
            var glasses = cam != null ? ModItemWatchGlass.Near(cam.transform.position, GlassReach) : new List<ModItemWatchGlass>();
            bool was = GUI.enabled;
            GUI.enabled = glasses.Count > 0;
            if (GUILayout.Button(glasses.Count > 0 ? "Set the ship's glass by it" : "No watch glass aboard", sButton, GUILayout.Width(360)))
            {
                double taken = double.IsNaN(lunarAt) ? NavMath.NowAbs : lunarAt;
                double offset = gmt - taken;
                foreach (ModItemWatchGlass g in glasses) g.SetBy(offset);
                if (clearsError) errText = "+00:00:00";
                double next = Math.Ceiling(NavMath.NowAbs + offset + 1e-6);
                AddNote("Day " + (int)Math.Floor(gmt / 24.0) + ", " + NavMath.Clock(gmt) + " at the zero meridian by a lunar (" + Lunar.Name(lunarBody) +
                        "): the ship's glass set, to start at " + NavMath.Clock(next).Substring(0, 5));
                Save();
                Notify("The ship's glass will be turned at " + NavMath.Clock(next).Substring(0, 5) + " by your reckoning");
            }
            GUI.enabled = was;
        }

        // ---------------- chronometers ----------------

        private static bool ChronoTab => Mode.Chronometers == Keeping.Imperfect || chronos.Count > 0;
        private static Vector2 chronoScroll;
        private const float ChronoReach = 3f;

        private static void DrawChronometers()
        {
            GUILayout.Label("Chronometers", sHead);
            GUILayout.Label("Stand by a chronometer with your pocket watch to compare them. The journal keeps each one's error and works out its rate.", sSmall);
            double now = NavMath.NowAbs;
            ModItemPocketWatch w = Mode.PocketWatches == Keeping.Dead ? null : Watch();
            double watchErr = NavMath.ParseTime(errText);
            Camera cam = Camera.main;
            List<ChronoState> near = cam != null ? ChronoState.Near(cam.transform.position, ChronoReach) : new List<ChronoState>();
            GUILayout.Space(6);
            if (near.Count == 0) GUILayout.Label("No chronometer within reach.", sSmall);
            foreach (ChronoState c in near)
            {
                ChronoLine line = Chrono(c.serial);
                double err = line.ErrorAt(now);
                GUILayout.BeginHorizontal();
                GUILayout.Label("No. " + c.serial + " shows <b>" + NavMath.Clock(c.ShownHours) + "</b>" + (c.Running ? "" : "  (stopped)"), sResult, GUILayout.Width(400));
                GUI.enabled = w != null && !double.IsNaN(err) && c.Running;
                if (GUILayout.Button("Watch error from it", sButton, GUILayout.Width(250)))
                {
                    // the zero meridian's time is what it shows less its error; the watch is off from that
                    double gmt = c.ShownHours - err;
                    double we = Celestial.Repeat(w.ShownHours - gmt + 12.0, 24.0) - 12.0;
                    errText = (we < 0 ? "-" : "+") + NavMath.Clock(Math.Abs(we));
                    AddNote("Day " + GameState.day + ": compared with chronometer No. " + c.serial + ", watch " + (we >= 0 ? "fast " : "slow ") + NavMath.Span(Math.Abs(we)).Substring(1));
                    Save();
                    UISoundPlayer.instance.PlayParchmentSound();
                }
                GUI.enabled = w != null && !double.IsNaN(watchErr) && c.Running;
                if (GUILayout.Button("Its error from the watch", sButton, GUILayout.Width(250)))
                {
                    double gmt = w.ShownHours - watchErr;
                    double ce = Celestial.Repeat(c.ShownHours - gmt + 12.0, 24.0) - 12.0;
                    line.Fix(now, ce);
                    AddNote("Day " + GameState.day + ": chronometer No. " + c.serial + " " + (ce >= 0 ? "fast " : "slow ") + NavMath.Span(Math.Abs(ce)).Substring(1) + " by the watch");
                    Save();
                    UISoundPlayer.instance.PlayParchmentSound();
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("No.", sText, GUILayout.Width(90));
            GUILayout.Label("Error found", sText, GUILayout.Width(330));
            GUILayout.Label("Rate", sText, GUILayout.Width(220));
            GUILayout.Label("Error now", sText);
            GUILayout.EndHorizontal();
            chronoScroll = GUILayout.BeginScrollView(chronoScroll, GUILayout.Height(220));
            int remove = -1;
            for (int i = 0; i < chronos.Count; i++)
            {
                ChronoLine c = chronos[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(c.serial.ToString(), sText, GUILayout.Width(90));
                string found = double.IsNaN(c.t2) ? "<i>not yet</i>"
                    : (c.e2 >= 0 ? "fast " : "slow ") + NavMath.Span(Math.Abs(c.e2)).Substring(1) + ", day " + (int)Math.Floor(c.t2 / 24.0) + " " + NavMath.Clock(c.t2).Substring(0, 5);
                GUILayout.Label(found, sResult, GUILayout.Width(330));
                double r = c.Rate;
                GUILayout.Label(double.IsNaN(r) ? "<i>not known</i>" : RateText(r) + (!double.IsNaN(c.t1) && c.t2 - c.t1 >= 12.0 ? "" : " (cert.)"), sResult, GUILayout.Width(220));
                double e = c.ErrorAt(now);
                GUILayout.Label(double.IsNaN(e) ? "" : "<b>" + (e >= 0 ? "fast " : "slow ") + NavMath.Span(Math.Abs(e)).Substring(1) + "</b>", sResult, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("x", sButton, GUILayout.Width(36))) remove = i;
                GUILayout.EndHorizontal();
            }
            if (chronos.Count == 0) GUILayout.Label("None yet: the watchmaker's certificate, or a comparison with your watch, starts a line.", sSmall);
            GUILayout.EndScrollView();
            if (remove >= 0) { chronos.RemoveAt(remove); Save(); }
        }

        private static double Latitude()
        {
            double v = NavMath.ParseAngle(latText);
            return latSouth ? -v : v;
        }

        private static void DrawSights()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sights", sHead, GUILayout.Width(120));
            useErr = GUILayout.Toggle(useErr, useErr ? (GlassTime ? "  [x] times corrected by the glass error" : "  [x] times corrected by the watch error") : "  [ ] times are already at the zero meridian", sText);
            GUILayout.EndHorizontal();
            GUILayout.Label("Each altitude read on the quintant, quadrant or cross-staff is a new line. Say which body and on which side it was.", sSmall);
            double err = useErr ? NavMath.ParseTime(errText) : 0.0;
            if (useErr && double.IsNaN(err))
            {
                GUILayout.Label("No watch error yet: find it from a lunar distance, or type it in.", sWarn);
                err = double.NaN;
            }
            double lat = Latitude();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Altitude", sText, GUILayout.Width(130));
            GUILayout.Label(GlassTime ? "Glass" : "Watch", sText, GUILayout.Width(130));
            GUILayout.Label("Side", sText, GUILayout.Width(66));
            GUILayout.Label("Body", sText, GUILayout.Width(180));
            GUILayout.Label("Longitude", sText);
            GUILayout.EndHorizontal();
            sightScroll = GUILayout.BeginScrollView(sightScroll, GUILayout.Height(320));
            int remove = -1;
            for (int i = 0; i < sights.Count; i++)
            {
                SightLine s = sights[i];
                GUILayout.BeginHorizontal();
                s.alt = GUILayout.TextField(s.alt, 12, sField, GUILayout.Width(122));
                GUILayout.Space(8);
                s.time = GUILayout.TextField(s.time, 12, sField, GUILayout.Width(122));
                GUILayout.Space(8);
                if (GUILayout.Button(s.side == 1 ? "E" : s.side == 2 ? "W" : "?", sButton, GUILayout.Width(58))) s.side = s.side == 1 ? 2 : 1;
                GUILayout.Space(8);
                if (GUILayout.Button(s.body == NoBody ? "pick..." : Lunar.Name(s.body), sButton, GUILayout.Width(172))) { picking = i; pickScroll = Vector2.zero; }
                GUILayout.Space(10);
                GUILayout.Label(Result(s, err, lat), sResult, GUILayout.ExpandWidth(true));
                GUI.enabled = Work(s, err, lat, out _, out _) == null;
                if (GUILayout.Button("to notes", sButton, GUILayout.Width(100))) ToNotes(s, err, lat);
                GUI.enabled = true;
                if (GUILayout.Button("x", sButton, GUILayout.Width(36))) remove = i;
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
            }
            if (sights.Count == 0) GUILayout.Label("Nothing yet: take a sight while you carry the journal.", sSmall);
            GUILayout.EndScrollView();
            if (remove >= 0) { sights.RemoveAt(remove); Save(); }
            if (GUILayout.Button("Add a line", sButton, GUILayout.Width(200)))
            {
                sights.Insert(0, new SightLine { time = WatchNow() });
                sightScroll = Vector2.zero;
            }
        }

        /// <summary>Works one line out; null if it could, else what is missing.</summary>
        private static string Work(SightLine s, double err, double lat, out NavMath.Sight r, out double gmtAbs)
        {
            r = default(NavMath.Sight);
            gmtAbs = double.NaN;
            if (!HaveAlmanac) return "the Almanac?";
            if (s.body == NoBody) return "which body?";
            if (s.side == 0) return "east or west?";
            if (double.IsNaN(lat)) return "latitude?";
            if (double.IsNaN(err)) return "watch error?";
            double watch = NavMath.ParseTime(s.time);
            if (double.IsNaN(watch)) return "time?";
            gmtAbs = NavMath.OnNearestDay(watch - err);
            r = NavMath.Reduce(s.body, gmtAbs, NavMath.ParseAngle(s.alt), lat, s.side == 1);
            return r.ok ? null : r.problem;
        }

        private static string Decimal(double deg) => deg.ToString("0.00", CultureInfo.InvariantCulture) + "°";

        /// <summary>What one line gives: the longitude (and for the sun the local time), or what is missing.</summary>
        private static string Result(SightLine s, double err, double lat)
        {
            string problem = Work(s, err, lat, out NavMath.Sight r, out _);
            if (problem != null) return "<i>" + problem + "</i>";
            // and in hundredths of a degree, as the interactive map reads (east positive)
            string res = "<b>" + NavMath.Lon(r.longitude) + "</b> (" + Decimal(r.longitude) + ")";
            if (s.body == Lunar.SunIndex) res += "   local " + NavMath.Clock(r.localTime).Substring(0, 5);
            return res;
        }

        /// <summary>The line's working and result, written into the notes.</summary>
        private static void ToNotes(SightLine s, double err, double lat)
        {
            if (Work(s, err, lat, out NavMath.Sight r, out double gmt) != null) return;
            int day = (int)Math.Floor(gmt / 24.0);
            string line = "Day " + day + ", " + NavMath.Clock(gmt) + " at the zero meridian: " + Lunar.Name(s.body) + " " +
                          NavMath.Angle(NavMath.ParseAngle(s.alt)) + (s.side == 1 ? " east" : " west") +
                          ", lat " + NavMath.Lat(lat) + " (" + Decimal(lat) + "), lon " + NavMath.Lon(r.longitude) + " (" + Decimal(r.longitude) + ")" +
                          (s.body == Lunar.SunIndex ? ", local time " + NavMath.Clock(r.localTime) : "");
            notes = string.IsNullOrEmpty(notes) ? line : line + "\n" + notes;
            Save();
            UISoundPlayer.instance.PlayParchmentSound();
            Notify("Journal: written in the notes");
        }

        /// <summary>The body for a sight line: the Sun or any star of the Almanac.</summary>
        private static void DrawPicker()
        {
            if (picking >= sights.Count) { picking = -1; return; }
            GUILayout.Label("Which body was it? (" + NavMath.Angle(NavMath.ParseAngle(sights[picking].alt)) + ")", sHead);
            pickScroll = GUILayout.BeginScrollView(pickScroll, GUILayout.Height(380));
            int i = Lunar.SunIndex;
            while (i < Lunar.Stars.Count)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < 4 && i < Lunar.Stars.Count; c++, i++)
                {
                    string n = i == Lunar.SunIndex ? "Sun"
                        : Lunar.Stars[i].name + (Lunar.Stars[i].name != Lunar.Stars[i].letter ? " (" + Lunar.Stars[i].letter + ")" : "");
                    if (GUILayout.Button(n, sList, GUILayout.Width(220), GUILayout.Height(34)))
                    {
                        sights[picking].body = i;
                        picking = -1;
                        Save();
                        GUIUtility.ExitGUI();
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Back", sButton, GUILayout.Width(120))) picking = -1;
        }

        private static void DrawNotes()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Notes", sHead, GUILayout.Width(120));
            GUILayout.Label("Day " + GameState.day + " of the voyage: " + ModItemNocturnal.DateName(ModItemNocturnal.DayOfYear(GameState.day)) +
                            ", day " + (ModItemNocturnal.DayOfYear(GameState.day) + 1) + " of the year", sSmall);
            GUILayout.EndHorizontal();
            noteScroll = GUILayout.BeginScrollView(noteScroll, GUILayout.Height(400));
            notes = GUILayout.TextArea(notes ?? "", sArea, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
        }
    }

    /// <summary>While the journal is open the game must not act on the keys and clicks meant for it.</summary>
    [HarmonyPatch]
    internal static class NavJournalInputPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(InventoryHotkeys), "Update")]
        private static bool Hotkeys() => !NavJournal.IsOpen;

        [HarmonyPrefix, HarmonyPatch(typeof(GoPointer), "LateUpdate")]
        private static bool Pointer() => !NavJournal.Busy;

        /// <summary>
        /// With the cursor on, the game's mouse pointer clicks whatever is under the cursor in the
        /// world, and a click on a page would drop the journal from your hand: not while it is open.
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(typeof(MouseButtonPointer), "LateUpdate")]
        private static bool MousePointer() => !NavJournal.Busy;

        /// <summary>Escape closes the journal instead of opening the game menu.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(StartMenu), "LateUpdate")]
        private static bool Menu()
        {
            if (!NavJournal.IsOpen) return true;
            if (Input.GetKeyDown(KeyCode.Escape)) NavJournal.Close();
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    internal enum TraverseMode { Manual, Automatic }

    /// <summary>
    /// The traverse board (prefab 718): a painted board the helmsman kept by the binnacle. A compass
    /// rose with six holes on each of its 32 points (one ring per two hours, the innermost first) and
    /// a speed grid of six rows (one per two hours) by twelve knots. Peg i is period i's: pegs 0-5 for
    /// the rose, 6-11 for the grid. Each peg hangs on a cord from the hub (rose) or the left of its row
    /// (grid) and swings with the ship.
    ///
    /// Hang it on a lamp hook by the hole in its heart, stick it on any surface like the vanilla wall
    /// pieces (wallAttachment), or lay it down. With empty hands, click a hole to put its period's peg
    /// in, click it again to pull it; Shift + click the hub clears the board. With the journal in hand,
    /// a click copies the board into the journal's Traverse page.
    ///
    /// Automatic mode (config): aboard a boat it measures the course steered and the speed over the
    /// first hour of every two, and sets both pegs when that hour is over; a new board at 00:00 and
    /// 12:00 local time.
    ///
    /// Model and textures: docs/make_traverse.py (see docs/TRAVERSE_SPEC.md). The board's own state is
    /// kept in BepInEx/config/RealisticSkies/traverse_slotN.txt (<see cref="TraverseStore"/>).
    /// </summary>
    public class ModItemTraverseBoard : ShipItem
    {
        private const int SourceIndex = 91;

        private const float T = TraverseBoardLayout.Thickness;      // the same on every style

        private TraverseStyle style;

        /// <summary>Which board this is (TraverseStyle): told by its prefab, 718 / 719 / 720.</summary>
        internal TraverseStyle Style
        {
            get
            {
                if (style == null)
                {
                    SaveablePrefab sp = GetComponent<SaveablePrefab>();
                    style = TraverseStyle.ByPrefab(sp != null ? sp.prefabIndex : TraverseStyle.Aestrin.Prefab);
                }
                return style;
            }
        }
        // the nearest hole within this of the view's middle (the nearest wins, so neighbours never overlap);
        // the hole already aimed at holds until another is clearly nearer
        private const float AimReach = 0.012f, AimHold = 0.003f, HubReach = 0.012f;

        internal TraverseState state = new TraverseState();
        private bool stateLoaded;

        // ---------------- life ----------------

        public override void OnLoad()
        {
            base.name = Style.ItemName;
            TryLoadState();
            EnsureRuntime();
            Relay();
        }

        private int InstanceId => base.saveable != null ? base.saveable.instanceId : 0;

        /// <summary>Its saved state, once the game has given it its id.</summary>
        private void TryLoadState()
        {
            if (stateLoaded) return;
            int id = InstanceId;
            if (id <= 0) return;
            stateLoaded = true;
            TraverseState s = TraverseStore.Get(id);
            if (s != null) { s.Fit(Style.Points); state = s; Relay(); }
        }

        private static bool ShiftKey => StudyMode.Shift;
        private static bool AutoMode => Plugin.configTraverseMode == null || Plugin.configTraverseMode.Value == TraverseMode.Automatic;
        private static bool CordsOn => Plugin.configPegCords == null || Plugin.configPegCords.Value;
        private bool InInventory => GetCurrentInventorySlot() != -1;

        public override bool AllowOnItemClick(GoPointerButton lookedAtButton)
        {
            return lookedAtButton.GetComponent<ShipItemLampHook>() != null || base.AllowOnItemClick(lookedAtButton);
        }

        public override void OnAltHeld()
        {
            if (base.sold && base.held != null && (ShiftKey || StudyMode.Studying(this)))
            {
                StudyMode.Mark(this);
                return;
            }
            base.OnAltHeld();
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
            ShowAim(false);
        }

        public override void OnLeaveInventory()
        {
            base.OnLeaveInventory();
            relayPending = true;
        }

        /// <summary>Put on a hook, it hangs there: not stuck to the wall behind the hook as well.</summary>
        public override void OnDrop()
        {
            HangableItem h = GetComponent<HangableItem>();
            if (base.sold && h != null && h.IsHanging()) return;
            base.OnDrop();
        }

        private new void OnDestroy()
        {
            base.OnDestroy();
            EndWork();
            if (cordMesh != null) Destroy(cordMesh);
        }

        public override void ExtraLateUpdate()
        {
            base.ExtraLateUpdate();
            TryLoadState();
            EnsureRuntime();
            if (base.sold && !InInventory) AutoTick();
            UpdateWork();
            UpdateAim();
            UpdatePegsAndCords();
        }

        // ---------------- the holes ----------------

        /// <summary>What a point on the board is: 0 nothing, 1 rose hole (a = ring, b = point), 2 grid hole (a = row, b = column), 3 the hub.</summary>
        internal struct Hole
        {
            public int kind, a, b;
            public Vector3 pos;
        }

        internal static Hole FindHole(TraverseStyle s, Vector3 local)
        {
            var none = new Hole();
            if (Mathf.Abs(local.z + T) > 0.004f) return none;           // the front face only
            var p = new Vector2(local.x, local.y);
            if (p.magnitude <= HubReach) return new Hole { kind = 3, pos = new Vector3(0f, 0f, -T) };
            Hole best = none;
            float bestD = AimReach;
            var rose = s.RoseHoles;
            for (int i = 0; i < rose.Length; i++)
            {
                float d = Vector2.Distance(p, new Vector2(rose[i].x, rose[i].y));
                if (d < bestD) { bestD = d; best = new Hole { kind = 1, a = i / s.Points, b = i % s.Points, pos = rose[i] }; }
            }
            var grid = s.GridHoles;
            for (int i = 0; i < grid.Length; i++)
            {
                float d = Vector2.Distance(p, new Vector2(grid[i].x, grid[i].y));
                if (d < bestD) { bestD = d; best = new Hole { kind = 2, a = i / TraverseStyle.Cols, b = i % TraverseStyle.Cols, pos = grid[i] }; }
            }
            return best;
        }

        private Hole aim;

        /// <summary>The hole in the middle of the board view, while working the board.</summary>
        private void UpdateAim()
        {
            Hole was = aim;
            aim = new Hole();
            if (working && workBlend >= 0.999f)
            {
                // straight along the view's middle, on the face's plane (no collider needed)
                ViewPose(out Vector3 eye, out Quaternion look);
                Vector3 e = base.transform.InverseTransformPoint(eye);
                Vector3 d = base.transform.InverseTransformDirection(look * Vector3.forward);
                if (d.z > 1e-4f)
                {
                    Vector3 at = e + d * ((-T - e.z) / d.z);
                    Hole h = FindHole(Style, at);
                    var p = new Vector2(at.x, at.y);
                    if (was.kind != 0)
                    {
                        float dw = Vector2.Distance(p, new Vector2(was.pos.x, was.pos.y));
                        float reach = was.kind == 3 ? HubReach : AimReach;
                        float dh = h.kind == 0 ? float.MaxValue : Vector2.Distance(p, new Vector2(h.pos.x, h.pos.y));
                        if (dw <= reach + AimHold && dw <= dh + AimHold) h = was;
                    }
                    aim = h;
                }
            }
            ShowAim(aim.kind != 0);
        }

        /// <summary>What the aimed hole is, for the line under the board view.</summary>
        private string AimText()
        {
            TraverseStyle s = Style;
            // the board does not know whether it is the morning's or the afternoon's; the junk's board
            // names its double hours, and those differ between its two boards: take the one it is now
            int half = s.PeriodNames != null ? HalfNow : 0;
            if (aim.kind == 1) return s.PointText(aim.b) + ", " + s.PeriodLook(aim.a, half);
            if (aim.kind == 2) return (aim.b + 1) + (aim.b == 0 ? " knot" : " knots") + ", " + s.PeriodLook(aim.a, half);
            if (aim.kind == 3) return "the hub (Shift + click: pull every peg)";
            return "";
        }

        /// <summary>The board it is now by local time (its first or second of the day).</summary>
        private int CycleNow => Style.CycleAt(Clock.LocalHours);
        private int HalfNow { get { int c = CycleNow; return c - 2 * (int)Math.Floor(c / 2.0); } }

        public override void UpdateLookText()
        {
            if (!base.sold)
            {
                base.UpdateLookText();
                return;
            }
            // nothing of its own: the game's controls line says it all (TraverseBoardPatches.LookControls)
            base.lookText = "";
        }

        // ---------------- working the board: a close view, the pegs by hand ----------------
        // Right click with empty hands brings the view up to the board's face; the mouse moves it over
        // the board (it rides with the board as the ship rolls), left click works the hole in the
        // middle of the view, right click (or Escape) leaves.
        // The game's own camera is carried there for the drawing of each frame (CameraCarry, from
        // Camera.onPreCull): a second camera would lose the game's colour grading, draw the scene
        // twice, and lag a frame behind the player's camera. Its near plane stops at 0.02 m and the far
        // plane comes in with it: a near plane much closer, against the far one at 21.5 km, costs the
        // depth buffer its precision and sets the shadows on the deck shaking.

        private const float WorkDistance = 0.17f, WorkFov = 50f, WorkBlendTime = 0.35f, WorkReach = 3f, WorkNear = 0.02f;
        private const float PanPerDegree = 0.0035f, PanMargin = 0.035f;

        private static ModItemTraverseBoard workingBoard;
        private static int workEndFrame = -100;
        private static Vector2 lookTurn;

        private bool working;
        private float workBlend;
        private int workStartFrame;
        private Vector2 pan, panSmooth;
        private static bool preCullHooked;

        /// <summary>A board is being worked: the pointer and the look are the board's.</summary>
        internal static bool Busy => (workingBoard != null && workingBoard.working) || Time.frameCount - workEndFrame < 3;
        internal static bool LookFrozen => workingBoard != null && workingBoard.working;
        /// <summary>What the mouse would have turned the look by, degrees (InstrumentAimLookPatch).</summary>
        internal static void LookTurn(float yaw, float nod) { lookTurn.x += yaw; lookTurn.y += nod; }
        /// <summary>Escape: leave the board (TraverseBoardPatches).</summary>
        internal static void EscapeWork() { if (workingBoard != null) workingBoard.EndWork(); }

        public override void OnAltActivate()
        {
            if (!base.sold) { base.OnAltActivate(); return; }
            if (base.held != null || InInventory || working || Time.frameCount - workEndFrame < 3) return;
            BeginWork();
        }

        private void BeginWork()
        {
            Camera main = Camera.main;
            if (main == null || NavJournal.IsOpen || !runtimeReady) return;
            if (workingBoard != null && workingBoard != this) workingBoard.EndWork();
            workingBoard = this;
            working = true;
            workStartFrame = Time.frameCount;
            workBlend = 0f;
            lookTurn = Vector2.zero;
            // start where the player looks at the board, kept on it
            Vector2 start = Vector2.zero;
            Collider col = GetComponent<Collider>();
            if (col != null && col.Raycast(new Ray(main.transform.position, main.transform.forward), out RaycastHit hit, 3f))
            {
                Vector3 l = base.transform.InverseTransformPoint(hit.point);
                start = new Vector2(l.x, l.y);
            }
            pan = panSmooth = ClampPan(start);
            if (!preCullHooked)
            {
                preCullHooked = true;
                Camera.onPreCull += PreCull;
            }
            Refs.SetPlayerControl(false);
            // the game's look text hangs in the world at the board; it would drift off as the ship
            // moves (the pointer that places it is off while working)
            LookUI ui = TraverseBoardPatches.Look;
            if (ui != null) ui.ClearText();
            UISoundPlayer.instance.PlayUISound(UISounds.itemPickup, 0.2f, 1.2f);
        }

        private void EndWork()
        {
            if (!working) return;
            working = false;
            workEndFrame = Time.frameCount;
            if (workingBoard == this) workingBoard = null;
            workBlend = 0f;
            aim = new Hole();
            ShowAim(false);
            Refs.SetPlayerControl(true);
        }

        private Vector2 ClampPan(Vector2 p)
        {
            Rect b = Style.Bounds;
            return new Vector2(Mathf.Clamp(p.x, b.xMin - PanMargin, b.xMax + PanMargin), Mathf.Clamp(p.y, b.yMin - PanMargin, b.yMax + PanMargin));
        }

        /// <summary>Every frame while working: leave if it can't go on, take the clicks, move the view.</summary>
        private void UpdateWork()
        {
            if (!working) return;
            Camera main = Camera.main;
            bool lost = main == null || !base.sold || base.held != null || InInventory || NavJournal.IsOpen || !GameState.playing ||
                        GameState.sleeping || Vector3.Distance(main.transform.position, base.transform.position) > WorkReach;
            if (lost) { EndWork(); return; }

            if (Time.frameCount != workStartFrame)
            {
                if (Input.GetMouseButtonDown(1)) { EndWork(); return; }
                if (Input.GetMouseButtonDown(0) && workBlend >= 0.999f) Click();
            }
            float sens = Plugin.configAimSensitivity != null ? Mathf.Clamp(Plugin.configAimSensitivity.Value * 2f, 0.2f, 2f) : 1f;
            pan = ClampPan(pan + new Vector2(lookTurn.x, lookTurn.y) * PanPerDegree * sens);
            lookTurn = Vector2.zero;
            float tau = Plugin.configAimSmoothing != null ? Mathf.Max(0.02f, Plugin.configAimSmoothing.Value * 1.5f) : 0.08f;
            panSmooth = Vector2.Lerp(panSmooth, pan, 1f - Mathf.Exp(-Time.deltaTime / tau));
            workBlend = Mathf.MoveTowards(workBlend, 1f, Time.deltaTime / WorkBlendTime);
        }

        /// <summary>The view: square to the board's face, a hand's breadth off it, riding with it.</summary>
        private void ViewPose(out Vector3 eye, out Quaternion look)
        {
            Transform b = base.transform;
            eye = b.TransformPoint(new Vector3(panSmooth.x, panSmooth.y, -T - WorkDistance));
            look = Quaternion.LookRotation(b.forward, b.up);
        }

        /// <summary>Just before the game's camera draws: carry it to the board for this frame.</summary>
        private static void PreCull(Camera c)
        {
            ModItemTraverseBoard b = workingBoard;
            if (b == null || !b.working || c != Camera.main || CameraCarry.Carried) return;
            b.ViewPose(out Vector3 eye, out Quaternion look);
            float e = Mathf.SmoothStep(0f, 1f, b.workBlend);
            // the camera's own pose, as the game left it this frame (CameraCarry puts it back after drawing)
            Vector3 p0 = c.transform.position;
            Quaternion r0 = c.transform.rotation;
            float near0 = c.nearClipPlane, far0 = c.farClipPlane;
            float near = Mathf.Lerp(near0, Mathf.Min(near0, WorkNear), e);
            float far = far0 * near / Mathf.Max(1e-4f, near0);           // the same ratio: no depth precision lost
            CameraCarry.Carry(c, Vector3.Lerp(p0, eye, e), Quaternion.Slerp(r0, look, e), Mathf.Lerp(c.fieldOfView, WorkFov, e), near, far);
        }

        private void Click()
        {
            if (aim.kind == 0) return;
            if (aim.kind == 3)
            {
                if (ShiftKey)
                {
                    PullAll();
                    Notify("You pull the pegs from the traverse board.");
                }
                return;
            }
            int period = aim.a;
            int peg = aim.kind == 1 ? period : 6 + period;
            int place = aim.b;
            state.place[peg] = state.place[peg] == place ? -1 : place;
            if (state.place[peg] < 0) Unset(peg);
            state.byHand[period] = true;
            state.copied[period] = false;
            state.cycleOf[period] = CycleNow;
            Tock();
        }

        private static GUIStyle workStyle;

        /// <summary>The aimed hole and the controls, under the board view.</summary>
        private void OnGUI()
        {
            if (!working || workBlend < 0.999f) return;
            if (workStyle == null || workStyle.font == null)
            {
                // the game's own UI font: the one its look text is set in
                workStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = Mathf.RoundToInt(Screen.height / 40f), richText = true };
                workStyle.font = TraverseBoardPatches.GameFont;
                workStyle.normal.textColor = new Color(1f, 0.96f, 0.86f);
            }
            string a = AimText();
            string text = (a.Length > 0 ? "<b>" + a + "</b>\n" : "\n") + "left click: peg in or out    right click: leave the board";
            var r = new Rect(0f, Screen.height * 0.80f, Screen.width, Screen.height * 0.15f);
            var shadow = new Rect(r.x + 2f, r.y + 2f, r.width, r.height);
            Color c = workStyle.normal.textColor;
            workStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(shadow, text, workStyle);
            workStyle.normal.textColor = c;
            GUI.Label(r, text, workStyle);
        }

        public override bool OnItemClick(PickupableItem heldItem)
        {
            if (base.sold && heldItem is ModItemJournal) CopyToJournal();
            return false;
        }

        private void PullAll()
        {
            for (int i = 0; i < 12; i++)
            {
                if (state.place[i] >= 0) { state.place[i] = -1; Unset(i); }
            }
            for (int j = 0; j < 6; j++) { state.byHand[j] = false; state.copied[j] = false; }
            Tock();
        }

        private static void Tock()
        {
            try { UISoundPlayer.instance.PlayUISound(UISounds.itemDrop, 0.12f, UnityEngine.Random.Range(1.9f, 2.3f)); } catch (Exception) { }
        }

        private static void Notify(string msg)
        {
            try { NotificationUi.instance.ShowNotification(msg); } catch (Exception) { Debug.Log("RealisticSkies: " + msg); }
        }

        // ---------------- into the journal ----------------

        private void CopyToJournal()
        {
            int n = CopyMarks();
            if (n == 0)
            {
                Notify("Nothing new on the traverse board.");
                return;
            }
            UISoundPlayer.instance.PlayWritingSound();
            Notify("You copy " + n + (n == 1 ? " line" : " lines") + " from the traverse board into the journal.");
        }

        /// <summary>Every period with a peg in and not yet copied goes into the journal; how many did.</summary>
        private int CopyMarks()
        {
            var marks = new List<NavJournal.TraverseMark>();
            for (int j = 0; j < 6; j++)
            {
                if (state.copied[j] || (state.place[j] < 0 && state.place[6 + j] < 0)) continue;
                marks.Add(new NavJournal.TraverseMark
                {
                    style = Style.Id,
                    cycle = state.cycleOf[j] >= 0 ? state.cycleOf[j] : CycleNow,
                    period = j,
                    point = state.place[j],
                    col = state.place[6 + j],
                });
                state.copied[j] = true;
            }
            if (marks.Count > 0) NavJournal.NoteTraverse(marks);
            return marks.Count;
        }

        // ---------------- the crew copies it (Virtual Crew) ----------------
        // At the end of every watch (04:00, 08:00, 12:00... local), if the Virtual Crew mod is there and a
        // navigator or first officer aboard is awake, they copy the board into the navigator's journal,
        // wherever the journal is aboard or on the captain. Otherwise nobody does: a watch not copied
        // before the board is cleared is lost.

        private int lastWatch = int.MinValue;

        /// <summary>Called every frame: copies at the turn of the watch, before a new board clears the old.</summary>
        private void CrewTick(double localHours)
        {
            // the watches turn with the board's own hours (23:00, 03:00... on the junk's board)
            int watch = (int)Math.Floor((localHours - Style.PeriodStart) / 4.0);
            if (lastWatch == int.MinValue) { lastWatch = watch; return; }
            if (watch == lastWatch) return;
            lastWatch = watch;
            if (!JournalAboard()) return;
            string who = VirtualCrew.AwakeOfficer();
            if (who == null) return;
            int n = CopyMarks();
            if (n > 0) Notify(who + " copies " + n + (n == 1 ? " line" : " lines") + " from the traverse board into the journal.");
        }

        /// <summary>The board is on the player's own boat (the one the crew sails) and a journal is aboard or carried.</summary>
        private bool JournalAboard()
        {
            Transform boat = GameState.currentBoat;
            if (boat == null || !OnBoat(this, boat)) return false;
            foreach (ModItemJournal j in UnityEngine.Object.FindObjectsOfType<ModItemJournal>())
            {
                if (j.sold && (j.GetCurrentInventorySlot() != -1 || j.held != null || OnBoat(j, boat))) return true;
            }
            return false;
        }

        private static bool OnBoat(ShipItem item, Transform boat)
        {
            if (item.currentActualBoat == boat || item.transform.IsChildOf(boat)) return true;
            PurchasableBoat pb = boat.GetComponentInParent<PurchasableBoat>();
            return pb != null && item.transform.IsChildOf(pb.transform);
        }

        // ---------------- automatic mode ----------------

        /// <summary>The ship's local time as one running count of hours, and the board it falls on.</summary>
        internal static class Clock
        {
            /// <summary>Hours since day 0 by the local (sun) time aboard.</summary>
            internal static double LocalHours
            {
                get
                {
                    if (Sun.sun == null) return GameState.day * 24.0;
                    double g = Sun.sun.globalTime, l = Sun.sun.localTime;
                    double off = l - g;
                    off -= 24.0 * Math.Round(off / 24.0);           // the longitude's offset, -12..12
                    return GameState.day * 24.0 + g + off;
                }
            }

            /// <summary>Hours since day 0 at the zero meridian (to time the measuring).</summary>
            internal static double GlobalHours => GameState.day * 24.0 + (Sun.sun != null ? Sun.sun.globalTime : 0f);
        }

        // ---------------- speeds: the chip log's knots ----------------
        // The board, the journal and the cartographer's ruler all count in the game's chip log knots and
        // the miles they make in a game hour. The vanilla chip log turns its pointer 28° per m/s of the
        // float, 15° to the knot: 1.867 knots per m/s (the speedometer's 1.944 is close). A minute of
        // latitude is 150 world units, so one log mile is a fraction of it that depends on how long a
        // game hour lasts: 0.45′ at the normal pace (125 s to the hour).

        internal const double LogKnotsPerMs = 28.0 / 15.0;

        /// <summary>Minutes of latitude in one log mile (a log knot kept for a game hour).</summary>
        internal static double ArcminPerLogMile
        {
            get
            {
                double secPerHour = Sun.sun != null && Sun.sun.initialTimescale > 0f ? 1.0 / Sun.sun.initialTimescale : 125.0;
                return secPerHour / (LogKnotsPerMs * 150.0);
            }
        }

        private Vector3 lastGlobe;
        private double lastHours;
        private bool haveLast;

        private Transform Boat()
        {
            if (base.currentActualBoat != null) return base.currentActualBoat;
            BoatRefs refs = GetComponentInParent<BoatRefs>();
            return refs != null ? refs.transform : null;
        }

        private void AutoTick()
        {
            if (Sun.sun == null || !GameState.playing || GameState.currentlyLoading || FloatingOriginManager.instance == null) return;
            double lh = Clock.LocalHours;
            // a style's boards may begin off the hour of 00/12 (the junk's at 23:00 and 11:00)
            double ps = Style.PeriodStart;
            int cycle = Style.CycleAt(lh);
            double inBoard = lh - ps - cycle * 12.0;
            int j = Mathf.Clamp((int)Math.Floor(inBoard / 2.0), 0, 5);
            double inPeriod = inBoard - j * 2.0;

            CrewTick(lh);           // before the new board clears the old one
            if (state.cycle != cycle)
            {
                if (state.cycle >= 0 && AutoMode) NewBoard();
                state.cycle = cycle;
            }
            int key = cycle * 6 + j;
            if (state.measKey != key)
            {
                state.measKey = key;
                state.evaluated = false;
                state.ResetSums();
                haveLast = false;
            }
            if (inPeriod < 1.0)
            {
                Transform boat = AutoMode ? Boat() : null;
                if (boat != null) Measure(boat);
                else haveLast = false;
            }
            else if (!state.evaluated)
            {
                state.evaluated = true;
                if (AutoMode) Evaluate(j, cycle);
                state.ResetSums();
                haveLast = false;
            }
        }

        private void Measure(Transform boat)
        {
            Vector3 g = FloatingOriginManager.instance.GetGlobeCoords(boat);
            double h = Clock.GlobalHours;
            if (!haveLast)
            {
                lastGlobe = g; lastHours = h; haveLast = true;
                return;
            }
            double dt = h - lastHours;
            Vector3 d = g - lastGlobe;
            lastGlobe = g; lastHours = h;
            if (dt <= 0.0 || Mathf.Abs(d.x) > 0.2f || Mathf.Abs(d.z) > 0.2f) return;
            double dN = d.z * 60.0;
            double dE = d.x * 60.0 * Math.Cos(g.z * Mathf.Deg2Rad);
            double dist = Math.Sqrt(dN * dN + dE * dE);
            Vector2 bow = BowDir(boat, dE, dN, dist);
            state.sumE += bow.x * dist;
            state.sumN += bow.y * dist;
            state.sumDist += dist;
            state.sumTime += dt;
        }

        private void Evaluate(int j, int cycle)
        {
            if (state.sumTime < 1.0 / 3.0 || state.byHand[j]) return;
            double kn = state.sumDist / state.sumTime / ArcminPerLogMile;      // what the chip log would show
            double brg = Math.Atan2(state.sumE, state.sumN) * Mathf.Rad2Deg;
            if (brg < 0) brg += 360.0;
            int np = Style.Points;
            int point = ((int)Math.Round(brg / Style.PointStep) % np + np) % np;
            if (kn < 0.5) return;                       // becalmed: no pegs
            int k = Mathf.Clamp((int)Math.Round(kn), 1, 12);
            state.place[j] = point;
            state.place[6 + j] = k - 1;
            state.copied[j] = false;
            state.cycleOf[j] = cycle;
        }

        /// <summary>00:00 and 12:00 (the junk's board: 23:00 and 11:00): a clean board.</summary>
        private void NewBoard()
        {
            int lost = 0;
            for (int j = 0; j < 6; j++)
            {
                if ((state.place[j] >= 0 || state.place[6 + j] >= 0) && !state.copied[j]) lost++;
            }
            for (int i = 0; i < 12; i++)
            {
                if (state.place[i] >= 0) { state.place[i] = -1; Unset(i); }
            }
            for (int j = 0; j < 6; j++) { state.byHand[j] = false; state.copied[j] = false; state.cycleOf[j] = -1; }
            Camera cam = Camera.main;
            bool near = cam != null && Vector3.Distance(cam.transform.position, base.transform.position) < 40f;
            if (lost > 0 && near)
            {
                Notify("The traverse board was cleared for the new watch. " + lost + (lost == 1 ? " mark was" : " marks were") + " never copied into the journal.");
            }
        }

        // which of a boat's own axes is its bow: the one it moves along most (it may not be +z)
        private static readonly Dictionary<int, float[]> bowVotes = new Dictionary<int, float[]>();
        private static readonly Vector3[] Axes = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

        /// <summary>The bow's horizontal direction as (east, north).</summary>
        private static Vector2 BowDir(Transform boat, double dE, double dN, double dist)
        {
            int id = boat.GetInstanceID();
            if (!bowVotes.TryGetValue(id, out float[] votes)) bowVotes[id] = votes = new float[4];
            if (dist > 1e-6)
            {
                Vector3 local = boat.InverseTransformDirection(new Vector3((float)dE, 0f, (float)dN).normalized);
                float[] c = { local.z, -local.z, local.x, -local.x };
                int bestC = 0;
                for (int i = 1; i < 4; i++) if (c[i] > c[bestC]) bestC = i;
                votes[bestC] += (float)dist;
            }
            int best = 0;
            for (int i = 1; i < 4; i++) if (votes[i] > votes[best]) best = i;
            Vector3 w = boat.TransformDirection(Axes[best]);
            var v = new Vector2(w.x, w.z);
            return v.sqrMagnitude > 1e-8f ? v.normalized : new Vector2((float)dE, (float)dN).normalized;
        }

        // ---------------- pegs and cords ----------------

        private const float SegLen = 0.0125f, CordR = 0.0008f, H = 1f / 120f, Damp = 0.997f, WPeg = 1f / 12f;
        private const int Iter = 16, MaxSub = 4, Sides = 6, PerSeg = 3;
        private const float SimReach = 12f;

        private class Cord
        {
            public Vector3 anchor, com, comQ;
            public Vector3[] p, q;
            public bool[] touch;
            public bool comTouch;
            public float seg;
            public int n;
        }

        private readonly Cord[] cords = new Cord[12];
        private Transform[] pegT;
        private GameObject anchorsGo;
        private MeshRenderer rootRenderer, cordRenderer, aimRenderer;
        private Transform aimT;
        private Mesh cordMesh;
        private bool runtimeReady, relayPending = true, lastCordsOn = true;
        private Vector3 lastPos, lastVel, accSm;
        private int motionFrames;
        private float simAcc;

        private Vector3 HolePos(int peg, int place)
        {
            return peg < 6 ? Style.RoseHole(peg, place) : Style.GridHole(peg - 6, place);
        }

        private static Vector3 AnchorHole(TraverseStyle s, int peg) => peg < 6 ? s.RoseAnchors[peg] : s.GridAnchors[peg - 6];
        private Vector3 AnchorHole(int peg) => AnchorHole(Style, peg);

        private void EnsureRuntime()
        {
            if (runtimeReady) return;
            Transform t = base.transform;
            pegT = new Transform[12];
            for (int i = 0; i < 12; i++) pegT[i] = t.Find("peg" + i);
            Transform an = t.Find("anchors");
            anchorsGo = an != null ? an.gameObject : null;
            rootRenderer = GetComponent<MeshRenderer>();
            TraverseStyle s = Style;
            StyleAssets assets = LoadModel(s);
            if (assets == null || pegT[0] == null) return;
            runtimeReady = true;

            for (int i = 0; i < 12; i++)
            {
                float len = i < 6 ? s.RoseCord : s.GridCord;
                int n = Mathf.Max(4, Mathf.RoundToInt(len / SegLen));
                cords[i] = new Cord
                {
                    n = n,
                    seg = len / n,
                    anchor = AnchorHole(i) + new Vector3(0f, 0f, s.AnchorTieZ),
                    p = new Vector3[n + 1], q = new Vector3[n + 1], touch = new bool[n + 1],
                };
            }

            var cg = new GameObject("cords");
            cg.layer = base.gameObject.layer;
            cg.transform.SetParent(t, false);
            cordMesh = new Mesh { name = "traverse board cords" };
            cordMesh.MarkDynamic();
            cg.AddComponent<MeshFilter>().sharedMesh = cordMesh;
            cordRenderer = cg.AddComponent<MeshRenderer>();
            cordRenderer.sharedMaterial = assets.cord;
            cordRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            BuildCordTopology();

            var ag = new GameObject("aim ring");
            ag.layer = base.gameObject.layer;
            ag.transform.SetParent(t, false);
            ag.AddComponent<MeshFilter>().sharedMesh = RingMesh();
            aimRenderer = ag.AddComponent<MeshRenderer>();
            aimRenderer.sharedMaterial = ringMat;
            aimRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aimRenderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor("_RendererColor", Color.white);
            block.SetVector("_Flip", new Vector4(1f, 1f, 1f, 1f));
            block.SetTexture("_MainTex", Texture2D.whiteTexture);
            aimRenderer.SetPropertyBlock(block);
            aimRenderer.enabled = false;
            aimT = ag.transform;
            relayPending = true;
        }

        private void ShowAim(bool on)
        {
            if (aimRenderer == null) return;
            aimRenderer.enabled = on;
            if (!on) return;
            aimT.localPosition = aim.pos + new Vector3(0f, 0f, -0.0003f);
            float s = aim.kind == 3 ? HubReach / 0.0034f : 1f;
            aimT.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>Unset peg: it hangs from the tie where it is, its centre of mass in front of it, as it came out.</summary>
        private void Unset(int peg)
        {
            Cord c = cords[peg];
            if (c == null) return;
            Vector3 tie = c.p[c.n];
            c.com = c.comQ = tie + new Vector3(0f, 0f, Style.PegCentreOfMassZ - Style.PegTie.z);
        }

        /// <summary>Every hanging cord laid straight down from its anchor, then let settle.</summary>
        private void Relay()
        {
            if (!runtimeReady) return;
            relayPending = false;
            float r = Style.PegCentreOfMassZ - Style.PegTie.z;
            for (int i = 0; i < 12; i++)
            {
                Cord c = cords[i];
                float len = c.seg * c.n;
                for (int k = 0; k <= c.n; k++)
                {
                    c.p[k] = c.q[k] = new Vector3(c.anchor.x, c.anchor.y - len * k / c.n, k == 0 ? c.anchor.z : -T - 0.002f);
                    c.touch[k] = false;
                }
                c.com = c.comQ = c.p[c.n] + new Vector3(0f, -r, 0f) + new Vector3(0f, 0f, -0.002f);
            }
            Vector3 g = Quaternion.Inverse(base.transform.rotation) * Physics.gravity;
            if (CordsOn) for (int s = 0; s < 120; s++) Step(H, g);
            motionFrames = 0;
            accSm = Vector3.zero;
            DrawPegs();
            if (CordsOn) DrawCords();
        }

        /// <summary>One Verlet step in the board's own space (docs/traverse_tools/viewer step()).</summary>
        private void Step(float dt, Vector3 g)
        {
            TraverseStyle s = Style;
            Vector3 gdt = g * (dt * dt);
            float stick = s.PegCentreOfMassZ - s.PegTie.z;
            float headR = s.PegHeadRadius;
            for (int i = 0; i < 12; i++)
            {
                Cord c = cords[i];
                int n = c.n;
                bool set = state.place[i] >= 0;
                Vector3 hole = set ? HolePos(i, state.place[i]) + s.PegTie : Vector3.zero;
                Vector3[] p = c.p, q = c.q;
                for (int k = 1; k <= n; k++)
                {
                    Vector3 v = (p[k] - q[k]) * Damp;
                    q[k] = p[k];
                    p[k] += v + gdt;
                }
                if (!set)
                {
                    Vector3 v = (c.com - c.comQ) * Damp;
                    c.comQ = c.com;
                    c.com += v + gdt;
                }
                for (int it = 0; it < Iter; it++)
                {
                    p[0] = c.anchor;
                    if (set) p[n] = hole;
                    for (int k = 0; k < n; k++)
                    {
                        Vector3 d = p[k + 1] - p[k];
                        float l = d.magnitude;
                        if (l < 1e-9f) continue;
                        float wa = k == 0 ? 0f : 1f, wb = (k + 1 == n && set) ? 0f : 1f;
                        float ws = wa + wb;
                        if (ws <= 0f) continue;
                        Vector3 corr = d * ((l - c.seg) / l);
                        p[k] += corr * (wa / ws);
                        p[k + 1] -= corr * (wb / ws);
                    }
                    if (!set)
                    {
                        Vector3 d = c.com - p[n];
                        float l = d.magnitude;
                        if (l > 1e-9f)
                        {
                            Vector3 corr = d * ((l - stick) / l);
                            float wsum = 1f + WPeg;
                            p[n] += corr * (1f / wsum);
                            c.com -= corr * (WPeg / wsum);
                        }
                    }
                    for (int k = 1; k <= n; k++)
                    {
                        float lim = s.OnFace(p[k].x, p[k].y) ? -T - CordR : -CordR;
                        if (p[k].z > lim) { p[k].z = lim; c.touch[k] = true; }
                    }
                    if (!set)
                    {
                        float lim = s.OnFace(c.com.x, c.com.y) ? -T - headR : -headR;
                        if (c.com.z > lim) { c.com.z = lim; c.comTouch = true; }
                    }
                }
                for (int k = 1; k <= n; k++)
                {
                    if (!c.touch[k]) continue;
                    q[k].x += (p[k].x - q[k].x) * 0.2f;
                    q[k].y += (p[k].y - q[k].y) * 0.2f;
                    c.touch[k] = false;
                }
                if (c.comTouch)
                {
                    c.comQ.x += (c.com.x - c.comQ.x) * 0.2f;
                    c.comQ.y += (c.com.y - c.comQ.y) * 0.2f;
                    c.comTouch = false;
                }
            }
        }

        private void UpdatePegsAndCords()
        {
            if (!runtimeReady) return;
            bool on = CordsOn;
            if (on != lastCordsOn)
            {
                lastCordsOn = on;
                relayPending = true;
            }
            if (anchorsGo != null && anchorsGo.activeSelf != on) anchorsGo.SetActive(on);
            if (cordRenderer != null) cordRenderer.enabled = on;
            if (!on || InInventory)
            {
                motionFrames = 0;
                DrawPegs();
                return;
            }
            if (relayPending) Relay();

            // the board's own acceleration, from its movement over the last frames
            Vector3 pos = base.transform.position;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (motionFrames > 0 && (pos - lastPos).sqrMagnitude > 1f)
            {
                // origin shift, teleport, load: drop this frame's acceleration and move nothing
                motionFrames = 0;
                lastPos = pos;
                DrawPegs();
                return;
            }
            if (motionFrames == 0)
            {
                lastPos = pos;
                lastVel = Vector3.zero;
                motionFrames = 1;
            }
            else
            {
                Vector3 vel = (pos - lastPos) / dt;
                if (motionFrames >= 2)
                {
                    // frames alternate ~11 / 13 ms (a physics step every other frame): smoothed over
                    // ~0.1 s of time, not per frame, so the spikes don't jerk the cords
                    Vector3 a = Vector3.ClampMagnitude((vel - lastVel) / dt, 40f);
                    accSm = Vector3.Lerp(accSm, a, 1f - Mathf.Exp(-dt / 0.1f));
                }
                lastVel = vel;
                lastPos = pos;
                motionFrames++;
            }

            // only near and in view; otherwise frozen as it is
            Camera cam = Camera.main;
            bool simulate = cam != null && Vector3.Distance(cam.transform.position, pos) < SimReach && (rootRenderer == null || rootRenderer.isVisible || base.held != null);
            if (!simulate) { simAcc = 0f; return; }
            Vector3 g = Quaternion.Inverse(base.transform.rotation) * (Physics.gravity - accSm);
            simAcc += Mathf.Min(dt, 0.1f);
            int steps = 0;
            while (simAcc >= H && steps < MaxSub) { Step(H, g); simAcc -= H; steps++; }
            if (simAcc >= H) simAcc = 0f;
            DrawPegs();
            DrawCords();
        }

        private void DrawPegs()
        {
            bool on = CordsOn;
            for (int i = 0; i < 12; i++)
            {
                Transform pt = pegT[i];
                if (pt == null) continue;
                int place = state.place[i];
                if (place >= 0 || !on || cords[i] == null)
                {
                    pt.localPosition = place >= 0 ? HolePos(i, place) : AnchorHole(i);
                    pt.localRotation = Quaternion.identity;
                    continue;
                }
                Cord c = cords[i];
                Vector3 tie = c.p[c.n];
                Vector3 axis = c.com - tie;
                axis = axis.sqrMagnitude > 1e-12f ? axis.normalized : Vector3.forward;
                pt.localRotation = Quaternion.FromToRotation(Vector3.forward, axis);
                pt.localPosition = tie - axis * Style.PegTie.z;
            }
        }

        // ---- the cords' mesh: one tube per cord along a Catmull-Rom curve, and a knot at each post ----

        private const int KnotLat = 4, KnotLon = 8;
        private Vector3[] cv, cn;
        private Vector4[] ct;
        private Vector2[] cuv;
        private Vector3[] samples;
        private static Vector3[] knotV, knotN;

        private void BuildCordTopology()
        {
            int verts = 0, maxS = 0;
            var tris = new List<int>();
            for (int i = 0; i < 12; i++)
            {
                int s = cords[i].n * PerSeg + 1;
                maxS = Mathf.Max(maxS, s);
                int b0 = verts;
                for (int r = 0; r < s - 1; r++)
                {
                    for (int k = 0; k < Sides; k++)
                    {
                        int a = b0 + r * (Sides + 1) + k, b = a + Sides + 1;
                        tris.Add(a); tris.Add(a + 1); tris.Add(b);
                        tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                    }
                }
                verts += s * (Sides + 1);
            }
            BuildKnot();
            for (int i = 0; i < 12; i++)
            {
                int b0 = verts;
                for (int la = 0; la < KnotLat; la++)
                {
                    for (int lo = 0; lo < KnotLon; lo++)
                    {
                        int a = b0 + la * (KnotLon + 1) + lo, b = a + KnotLon + 1;
                        AddKnotTri(tris, b0, a, b, a + 1);
                        AddKnotTri(tris, b0, a + 1, b, b + 1);
                    }
                }
                verts += knotV.Length;
            }
            cv = new Vector3[verts]; cn = new Vector3[verts]; ct = new Vector4[verts]; cuv = new Vector2[verts];
            samples = new Vector3[maxS];
            for (int i = 0; i < verts; i++) { cn[i] = Vector3.back; ct[i] = new Vector4(1f, 0f, 0f, 1f); }
            cordMesh.vertices = cv;
            cordMesh.normals = cn;
            cordMesh.tangents = ct;
            cordMesh.uv = cuv;
            cordMesh.SetTriangles(tris, 0);
            // fixed bounds round the board and the cords' full reach
            cordMesh.bounds = new Bounds(new Vector3(0f, -0.1f, -0.15f), new Vector3(1.2f, 1.4f, 0.8f));
        }

        /// <summary>Knot triangles wound so that they face out (Unity: Cross(v1 - v0, v2 - v0) along the normal).</summary>
        private static void AddKnotTri(List<int> tris, int b0, int i0, int i1, int i2)
        {
            Vector3 a = knotV[i0 - b0], b = knotV[i1 - b0], c = knotV[i2 - b0];
            Vector3 mid = (a + b + c) / 3f;
            Vector3 cr = Vector3.Cross(b - a, c - a);
            if (cr.sqrMagnitude < 1e-20f) return;                     // a pole's empty triangle
            if (Vector3.Dot(cr, mid) >= 0f) { tris.Add(i0); tris.Add(i1); tris.Add(i2); }
            else { tris.Add(i0); tris.Add(i2); tris.Add(i1); }
        }

        private static void BuildKnot()
        {
            if (knotV != null) return;
            int n = (KnotLat + 1) * (KnotLon + 1);
            knotV = new Vector3[n];
            knotN = new Vector3[n];
            const float R = 0.0019f;
            for (int la = 0; la <= KnotLat; la++)
            {
                float th = Mathf.PI * la / KnotLat;
                for (int lo = 0; lo <= KnotLon; lo++)
                {
                    float ph = 2f * Mathf.PI * lo / KnotLon;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th));
                    int i = la * (KnotLon + 1) + lo;
                    knotV[i] = new Vector3(d.x * R, d.y * R, d.z * R * 0.6f);
                    knotN[i] = new Vector3(d.x, d.y, d.z / 0.6f).normalized;
                }
            }
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        private void DrawCords()
        {
            if (cordMesh == null || cv == null) return;
            int vi = 0;
            for (int i = 0; i < 12; i++)
            {
                Cord c = cords[i];
                Vector3[] p = c.p;
                int n = c.n, s = 0;
                for (int k = 0; k < n; k++)
                {
                    Vector3 p0 = p[Mathf.Max(0, k - 1)], p1 = p[k], p2 = p[k + 1], p3 = p[Mathf.Min(n, k + 2)];
                    for (int j = 0; j < PerSeg; j++) samples[s++] = CatmullRom(p0, p1, p2, p3, (float)j / PerSeg);
                }
                samples[s++] = p[n];

                Vector3 tan = (samples[1] - samples[0]).normalized;
                Vector3 nrm = Vector3.Cross(tan, Vector3.forward);
                if (nrm.sqrMagnitude < 1e-6f) nrm = Vector3.Cross(tan, Vector3.right);
                nrm.Normalize();
                float along = 0f;
                for (int r = 0; r < s; r++)
                {
                    Vector3 tr = (samples[Mathf.Min(s - 1, r + 1)] - samples[Mathf.Max(0, r - 1)]);
                    if (tr.sqrMagnitude > 1e-14f) tan = tr.normalized;
                    if (r > 0) along += Vector3.Distance(samples[r], samples[r - 1]);
                    Vector3 m = nrm - tan * Vector3.Dot(nrm, tan);            // carried along the curve without twisting
                    if (m.sqrMagnitude > 1e-10f) nrm = m.normalized;
                    Vector3 bin = Vector3.Cross(tan, nrm);
                    float v = along / 0.012f;
                    for (int k = 0; k <= Sides; k++)
                    {
                        float a = 2f * Mathf.PI * k / Sides;
                        float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                        Vector3 dir = nrm * ca + bin * sa;
                        cv[vi] = samples[r] + dir * CordR;
                        cn[vi] = dir;
                        Vector3 tu = bin * ca - nrm * sa;
                        ct[vi] = new Vector4(tu.x, tu.y, tu.z, 1f);
                        cuv[vi] = new Vector2((float)k / Sides, v);
                        vi++;
                    }
                }
            }
            for (int i = 0; i < 12; i++)
            {
                Vector3 at = cords[i].p[0];
                for (int k = 0; k < knotV.Length; k++)
                {
                    cv[vi] = at + knotV[k];
                    cn[vi] = knotN[k];
                    ct[vi] = new Vector4(1f, 0f, 0f, 1f);
                    cuv[vi] = new Vector2((float)(k % (KnotLon + 1)) / KnotLon, (float)(k / (KnotLon + 1)) * 0.25f);
                    vi++;
                }
            }
            cordMesh.vertices = cv;
            cordMesh.normals = cn;
            cordMesh.tangents = ct;
            cordMesh.uv = cuv;
        }

        private static Mesh ringMesh;

        /// <summary>The aim highlight: a thin flat ring, 3.4 mm across the outside, facing -z.</summary>
        private static Mesh RingMesh()
        {
            if (ringMesh != null) return ringMesh;
            const int seg = 32;
            const float r0 = 0.0029f, r1 = 0.0034f;
            var v = new Vector3[seg * 2];
            var col = new Color[v.Length];
            var uv = new Vector2[v.Length];
            var tri = new int[seg * 6];
            var c = new Color(1f, 0.93f, 0.78f, 0.9f);
            for (int k = 0; k < seg; k++)
            {
                float a = 2f * Mathf.PI * k / seg;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v[2 * k] = d * r0; v[2 * k + 1] = d * r1;
                col[2 * k] = col[2 * k + 1] = c;
                uv[2 * k] = new Vector2(0.5f, 0.5f); uv[2 * k + 1] = new Vector2(0.5f, 0.5f);
                int a0 = 2 * k, a1 = 2 * k + 1, b0 = (2 * k + 2) % v.Length, b1 = (2 * k + 3) % v.Length;
                tri[6 * k] = a0; tri[6 * k + 1] = b0; tri[6 * k + 2] = a1;
                tri[6 * k + 3] = a1; tri[6 * k + 4] = b0; tri[6 * k + 5] = b1;
            }
            ringMesh = new Mesh { name = "traverse board aim ring" };
            ringMesh.vertices = v; ringMesh.colors = col; ringMesh.uv = uv; ringMesh.triangles = tri;
            ringMesh.RecalculateNormals();
            ringMesh.RecalculateBounds();
            return ringMesh;
        }

        // ---------------- model ----------------
        // Built by docs/make_traverse.py.

        /// <summary>A style's meshes and materials, loaded once.</summary>
        internal sealed class StyleAssets
        {
            public Dictionary<string, Mesh> parts;
            public Material face, back, hole, edge, peg, cord;
        }

        private static readonly Dictionary<string, StyleAssets> assetsById = new Dictionary<string, StyleAssets>();
        private static Material ringMat;

        private static Texture2D Tex(string folder, string file, bool linear)
        {
            Texture2D t = ModResources.Texture(folder + "/" + file, linear, out bool shared);
            if (t == null) return null;
            t.anisoLevel = 8;
            if (!shared)
            {
                t.name = folder + " " + file;
                t.wrapMode = TextureWrapMode.Repeat;
                if (file.Contains("albedo")) t.Compress(true);
                t.Apply(false, true);
            }
            return t;
        }

        /// <summary>A Standard material from a texture set (prefix): metal in the metal/gloss map's R (gold, brass), smoothness in its A.</summary>
        private static Material MakeMaterial(Shader sh, string folder, string name, string prefix, bool gloss = true, float plainGloss = 0.08f)
        {
            var m = new Material(sh) { name = name };
            Texture2D alb = Tex(folder, prefix + "albedo.jpg", false), nm = Tex(folder, prefix + "normal.png", true);
            Texture2D mg = gloss ? Tex(folder, prefix + "metalgloss.png", true) : null;
            if (alb != null) m.SetTexture("_MainTex", alb);
            m.SetFloat("_Metallic", 0f);
            if (mg != null)
            {
                m.SetTexture("_MetallicGlossMap", mg);
                m.EnableKeyword("_METALLICGLOSSMAP");
                m.SetFloat("_GlossMapScale", 1f);
            }
            else m.SetFloat("_Glossiness", plainGloss);
            if (nm != null)
            {
                m.SetTexture("_BumpMap", nm);
                m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_BumpScale", 1f);
            }
            return m;
        }

        /// <summary>A style's model and materials (null: its files are missing).</summary>
        private static StyleAssets LoadModel(TraverseStyle s)
        {
            if (assetsById.TryGetValue(s.Id, out StyleAssets have)) return have;
            byte[] bin = ModResources.Read(s.Folder + "/traverse.bin", out _);
            if (bin == null) return null;
            var found = new Dictionary<string, Mesh>();
            using (var r = new BinaryReader(new MemoryStream(bin)))
            {
                if (new string(r.ReadChars(4)) != "TRAV") throw new Exception("bad traverse.bin");
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
                    var mesh = new Mesh { name = s.Folder + " " + name };
                    if (n > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices = v; mesh.normals = nn; mesh.uv = uv;
                    mesh.triangles = tris;
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    found[name] = mesh;
                }
            }
            foreach (string need in new[] { "face", "back", "edge", "holes", "peg", "anchors" })
            {
                if (!found.ContainsKey(need)) throw new Exception(s.Folder + "/traverse.bin has no part " + need);
            }
            Shader sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            string f = s.Folder, id = s.Id;
            var a = new StyleAssets { parts = found };
            a.face = MakeMaterial(sh, f, "traverse face " + id, "face_");
            // the holes are lined with the bare wood (wood_), tinted darker; the back has its own set
            // (the European board's back is the same bare pine)
            Material wood = MakeMaterial(sh, f, "traverse wood " + id, "wood_");
            a.back = s.BackSet == "wood_" ? wood : MakeMaterial(sh, f, "traverse back " + id, s.BackSet);
            a.hole = new Material(wood) { name = "traverse holes " + id, color = s.HoleTint };
            a.edge = MakeMaterial(sh, f, "traverse edge " + id, "edge_");
            a.peg = MakeMaterial(sh, f, "traverse peg " + id, "peg_");
            a.cord = MakeMaterial(sh, f, "traverse cord " + id, "cord_", false, s.CordGloss);
            if (ringMat == null)
            {
                Shader ds = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
                ringMat = new Material(ds) { name = "traverse aim ring" };
            }
            assetsById[id] = a;
            return a;
        }

        private static GameObject Part(StyleAssets a, string mesh, Material mat, Transform parent, int layer, string name = null)
        {
            var go = new GameObject(name ?? mesh);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = a.parts[mesh];
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ---------------- shops ----------------

        /// <summary>
        /// Sold wherever the cross-staff is, in every Difficulty: each region's shops their own board
        /// (Aestrin the European, the Emerald Archipelago the junk's, Al'Ankh the pilot's star board).
        /// </summary>
        internal static void AddToShop(Transform shop, int island)
        {
            if (shop == null) return;
            PortRegion region = RegionOf(shop);
            TraverseStyle s = TraverseStyle.ForRegion(region);
            if (!registered.Contains(s.Id)) s = TraverseStyle.Aestrin;           // its files are missing
            Debug.Log("RealisticSkies: island " + island + " (" + region + ") sells the " + s.ItemName);
            foreach (ShopItemSpawner sp in shop.GetComponentsInChildren<ShopItemSpawner>(true))
            {
                SaveablePrefab sv = sp.itemPrefab != null ? sp.itemPrefab.GetComponent<SaveablePrefab>() : null;
                if (sv == null || sv.prefabIndex != 703) continue;
                GameObject clone = UnityEngine.Object.Instantiate(sp.gameObject, sp.transform.parent);
                clone.name = s.ItemName;
                if (ShopSpots.TryGet(ShopSpots.TraverseBoard, island, out ShopSpots.Spot spot))
                {
                    ShopSpots.Apply(clone.transform, shop, spot);
                }
                else
                {
                    // standing up against the stall's back, near the cross-staff, leaning a little
                    clone.transform.localPosition = sp.transform.localPosition + new Vector3(0.35f, 0.30f, -0.42f);
                    clone.transform.rotation = shop.rotation * Quaternion.Euler(-12f, 180f, 0f);
                }
                ShopItemSpawner csp = clone.GetComponent<ShopItemSpawner>();
                AccessTools.Field(typeof(ShopItemSpawner), "item").SetValue(csp, null);
                csp.enabled = false;
                Renderer r = clone.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                var hook = clone.AddComponent<LoreSpawnerHook>();
                hook.spawner = csp;
                hook.prefabIndex = s.Prefab;
                ShopSpots.RegisterSpawner(ShopSpots.TraverseBoard, island, clone.transform);
                break;
            }
        }

        /// <summary>The region of the port nearest a shop (the game's own PortRegion), none if no port is found.</summary>
        private static PortRegion RegionOf(Transform shop)
        {
            Port[] ports = Port.ports != null && Port.ports.Length > 0 ? Port.ports : UnityEngine.Object.FindObjectsOfType<Port>();
            Port best = null;
            float bestD = float.MaxValue;
            if (ports != null)
            {
                foreach (Port p in ports)
                {
                    if (p == null) continue;
                    float d = (p.transform.position - shop.position).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = p; }
                }
            }
            return best != null ? best.region : PortRegion.none;
        }

        // ---------------- prefab ----------------

        private static GameObject holder;
        private static readonly HashSet<string> registered = new HashSet<string>();

        /// <summary>The three boards, 718 / 719 / 720 (TraverseStyle).</summary>
        internal static void Register(PrefabsDirectory dir)
        {
            if (holder != null || dir == null || dir.directory == null) return;
            if (dir.directory.Length <= SourceIndex || dir.directory[SourceIndex] == null)
            {
                Debug.LogWarning("RealisticSkies: vanilla scroll (prefab 91) not found, no traverse boards");
                return;
            }
            holder = new GameObject("RealisticSkies traverse board holder");
            holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(holder);
            foreach (TraverseStyle s in TraverseStyle.All) Register(dir, s);
        }

        private static void Register(PrefabsDirectory dir, TraverseStyle s)
        {
            int index = s.Prefab;
            if (dir.directory.Length > index && dir.directory[index] != null)
            {
                Debug.LogWarning("RealisticSkies: prefab " + index + " is already taken, no " + s.ItemName);
                return;
            }
            try
            {
                StyleAssets a = LoadModel(s);
                if (a == null)
                {
                    Debug.LogWarning("RealisticSkies: " + s.Folder + " model missing, no " + s.ItemName);
                    return;
                }
                GameObject go = UnityEngine.Object.Instantiate(dir.directory[SourceIndex], holder.transform);
                go.name = index + " " + s.ItemName;

                ShipItemScroll old = go.GetComponent<ShipItemScroll>();
                ModItemTraverseBoard item = go.AddComponent<ModItemTraverseBoard>();
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
                // the board's bounds (heart, crest or finial included), the thickness of the board
                Rect bb = s.Bounds;
                box.center = new Vector3(bb.center.x, bb.center.y, -T / 2f);
                box.size = new Vector3(bb.width, bb.height, T);

                // the root shows the face: the wall-attach ghost draws the root's mesh
                go.GetComponent<MeshFilter>().sharedMesh = a.parts["face"];
                go.GetComponent<MeshRenderer>().sharedMaterial = a.face;
                int layer = go.layer;
                Part(a, "back", a.back, go.transform, layer);
                Part(a, "edge", a.edge, go.transform, layer);
                Part(a, "holes", a.hole, go.transform, layer);
                Part(a, "anchors", a.peg, go.transform, layer);
                for (int i = 0; i < 12; i++)
                {
                    GameObject peg = Part(a, "peg", a.peg, go.transform, layer, "peg" + i);
                    peg.transform.localPosition = AnchorHole(s, i);
                }
                foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }

                item.name = s.ItemName;
                item.amount = 0f;
                item.health = 0f;
                item.holdDistance = 0.55f;
                item.inventoryRotationX = 0f;
                item.inventoryRotation = 0f;
                item.wallAttachment = true;
                item.value = 25;
                if (Patches.itemPrefabs.TryGetValue(703, out GameObject chart) && chart != null
                    && chart.GetComponent<ShipItem>() is ShipItem chartItem && chartItem.value > 0)
                {
                    item.value = Mathf.Max(5, Mathf.RoundToInt(chartItem.value * 0.10f));     // about 25 crowns
                }
                item.mass = 1.5f;

                // hangs on a lamp hook by the hole in its heart (HangPatch)
                HangableItem hang = go.AddComponent<HangableItem>();
                var ht = Traverse.Create(hang);
                ht.Field("lockX").SetValue(true);
                ht.Field("lockZ").SetValue(true);
                ht.Field("rotX").SetValue(0f);
                ht.Field("rotZ").SetValue(0f);

                go.GetComponent<SaveablePrefab>().prefabIndex = index;
                if (dir.directory.Length <= index)
                {
                    Array.Resize(ref dir.directory, index + 5);
                }
                dir.directory[index] = go;
                registered.Add(s.Id);
                Debug.Log("RealisticSkies: added the " + s.ItemName + " as prefab " + index);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not build the " + s.ItemName + ": " + e);
            }
        }
    }

    /// <summary>One board's pegs and bookkeeping.</summary>
    internal class TraverseState
    {
        public readonly int[] place = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };    // -1 out; a point (rose: 0-31, or 0-23 on the junk's board) or a column 0-11 (grid)
        public readonly bool[] copied = new bool[6], byHand = new bool[6];
        public readonly int[] cycleOf = { -1, -1, -1, -1, -1, -1 };      // the board (day * 2 + half) each period's pegs went in on
        public int cycle = -1, measKey = -1;
        public bool evaluated;
        public double sumE, sumN, sumDist, sumTime;

        public void ResetSums() { sumE = sumN = sumDist = sumTime = 0.0; }

        /// <summary>Rose pegs beyond the board's own points (24 on the junk's board) come out.</summary>
        public void Fit(int points)
        {
            for (int i = 0; i < 6; i++) if (place[i] >= points) place[i] = -1;
        }

        private static string D(double d) => d.ToString("R", CultureInfo.InvariantCulture);

        private static string Bits(bool[] b)
        {
            var c = new char[b.Length];
            for (int i = 0; i < b.Length; i++) c[i] = b[i] ? '1' : '0';
            return new string(c);
        }

        public string Serialize()
        {
            return string.Join(",", Array.ConvertAll(place, x => x.ToString(CultureInfo.InvariantCulture))) + "|" + Bits(copied) + "|" + Bits(byHand) + "|" +
                   string.Join(",", Array.ConvertAll(cycleOf, x => x.ToString(CultureInfo.InvariantCulture))) + "|" + cycle + "|" + measKey + "|" + (evaluated ? 1 : 0) + "|" +
                   D(sumE) + "|" + D(sumN) + "|" + D(sumDist) + "|" + D(sumTime);
        }

        public static TraverseState Parse(string s)
        {
            try
            {
                string[] f = s.Split('|');
                var st = new TraverseState();
                string[] pl = f[0].Split(',');
                for (int i = 0; i < 12 && i < pl.Length; i++)
                {
                    int v = int.Parse(pl[i], CultureInfo.InvariantCulture);
                    st.place[i] = v < 0 || v >= (i < 6 ? 32 : 12) ? -1 : v;
                }
                for (int j = 0; j < 6; j++)
                {
                    st.copied[j] = f[1].Length > j && f[1][j] == '1';
                    st.byHand[j] = f[2].Length > j && f[2][j] == '1';
                }
                string[] cy = f[3].Split(',');
                for (int j = 0; j < 6 && j < cy.Length; j++) st.cycleOf[j] = int.Parse(cy[j], CultureInfo.InvariantCulture);
                st.cycle = int.Parse(f[4], CultureInfo.InvariantCulture);
                st.measKey = int.Parse(f[5], CultureInfo.InvariantCulture);
                st.evaluated = f[6] == "1";
                st.sumE = double.Parse(f[7], CultureInfo.InvariantCulture);
                st.sumN = double.Parse(f[8], CultureInfo.InvariantCulture);
                st.sumDist = double.Parse(f[9], CultureInfo.InvariantCulture);
                st.sumTime = double.Parse(f[10], CultureInfo.InvariantCulture);
                return st;
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: can't read a traverse board's state: " + e.Message);
                return null;
            }
        }
    }

    /// <summary>
    /// The boards' states, per save slot, in BepInEx/config/RealisticSkies/traverse_slotN.txt, keyed by
    /// the item's instance id (the game saves only an item's health and amount). Written on the game's save.
    /// </summary>
    internal static class TraverseStore
    {
        private static int loadedSlot = -1;
        private static readonly Dictionary<int, string> lines = new Dictionary<int, string>();

        private static string FilePath => Path.Combine(Path.Combine(Paths.ConfigPath, "RealisticSkies"), "traverse_slot" + SaveSlots.currentSlot + ".txt");

        private static void Ensure()
        {
            if (loadedSlot == SaveSlots.currentSlot) return;
            loadedSlot = SaveSlots.currentSlot;
            lines.Clear();
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string l in File.ReadAllLines(FilePath))
                {
                    int eq = l.IndexOf('=');
                    if (eq <= 0 || !l.StartsWith("board")) continue;
                    int bar = l.IndexOf('|', eq);
                    if (bar < 0 || !int.TryParse(l.Substring(eq + 1, bar - eq - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) continue;
                    lines[id] = l.Substring(bar + 1);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not read the traverse boards: " + e.Message);
            }
        }

        internal static TraverseState Get(int id)
        {
            Ensure();
            return lines.TryGetValue(id, out string s) ? TraverseState.Parse(s) : null;
        }

        internal static void Save()
        {
            try
            {
                Ensure();
                foreach (ModItemTraverseBoard b in UnityEngine.Object.FindObjectsOfType<ModItemTraverseBoard>())
                {
                    SaveablePrefab sp = b.GetComponent<SaveablePrefab>();
                    if (!b.sold || sp == null || sp.instanceId <= 0) continue;
                    lines[sp.instanceId] = b.state.Serialize();
                }
                // boards that are no more (sold, lost, destroyed) are dropped
                List<SaveablePrefab> live = SaveLoadManager.instance != null ? SaveLoadManager.instance.GetCurrentPrefabs() : null;
                if (live != null && live.Count > 0)
                {
                    var ids = new HashSet<int>();
                    foreach (SaveablePrefab p in live) if (p != null && TraverseStyle.IsBoardPrefab(p.prefabIndex)) ids.Add(p.instanceId);
                    foreach (int id in new List<int>(lines.Keys)) if (!ids.Contains(id)) lines.Remove(id);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var outL = new List<string>();
                foreach (KeyValuePair<int, string> kv in lines) outL.Add("board=" + kv.Key.ToString(CultureInfo.InvariantCulture) + "|" + kv.Value);
                File.WriteAllLines(FilePath, outL.ToArray());
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not save the traverse boards: " + e.Message);
            }
        }
    }

    [HarmonyPatch]
    internal static class TraverseBoardPatches
    {
        /// <summary>The game's save: the boards' pegs too.</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(SaveLoadManager), "SaveGame")]
        private static void Saved() => TraverseStore.Save();

        private static LookUI look;
        private static readonly AccessTools.FieldRef<LookUI, TextMesh> controlsRef = AccessTools.FieldRefAccess<LookUI, TextMesh>("controlsText");
        private static readonly AccessTools.FieldRef<LookUI, GoPointer> lookPointerRef = AccessTools.FieldRefAccess<LookUI, GoPointer>("pointer");
        private static readonly Action<LookUI> showL = AccessTools.MethodDelegate<Action<LookUI>>(AccessTools.Method(typeof(LookUI), "ShowLicon"));
        private static readonly Action<LookUI> showR = AccessTools.MethodDelegate<Action<LookUI>>(AccessTools.Method(typeof(LookUI), "ShowRicon"));
        private static readonly Action<LookUI> hideIcons = AccessTools.MethodDelegate<Action<LookUI>>(AccessTools.Method(typeof(LookUI), "HideIcons"));

        /// <summary>The game's look text (the name, the controls with their mouse icons).</summary>
        internal static LookUI Look
        {
            get
            {
                if (look == null) look = UnityEngine.Object.FindObjectOfType<LookUI>();
                return look;
            }
        }

        /// <summary>The game's UI font, as its look text uses it (null: Unity's own).</summary>
        internal static Font GameFont
        {
            get
            {
                LookUI ui = Look;
                TextMesh tm = ui != null ? controlsRef(ui) : null;
                return tm != null ? tm.font : null;
            }
        }

        /// <summary>
        /// The board's controls, the game's way: left mouse icon "pick up", right mouse icon "use" (as
        /// the bed or a lantern); with the journal in hand, left mouse "copy into journal".
        /// </summary>
        [HarmonyPostfix, HarmonyPatch(typeof(LookUI), "ShowLookText")]
        private static void LookControls(LookUI __instance, GoPointerButton button)
        {
            if (!(button is ModItemTraverseBoard b) || !b.sold || !Settings.controlsTextEnabled) return;
            TextMesh controls = controlsRef(__instance);
            if (controls == null) return;
            GoPointer p = lookPointerRef(__instance);
            PickupableItem inHand = p != null ? p.GetHeldItem() : null;
            if (inHand is ModItemJournal)
            {
                hideIcons(__instance);
                showL(__instance);
                controls.text = "copy into journal\n";
            }
            else if (inHand == null)
            {
                hideIcons(__instance);
                if (!b.nailed)
                {
                    showL(__instance);
                    showR(__instance);
                    controls.text = "pick up\nuse";
                }
                else
                {
                    showR(__instance);
                    controls.text = "\nuse";
                }
            }
        }

        /// <summary>While a board is being worked, its clicks are its own: the pointer does nothing.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(GoPointer), "LateUpdate")]
        private static bool Pointer() => !ModItemTraverseBoard.Busy;

        /// <summary>Escape leaves the board instead of opening the game menu.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(StartMenu), "LateUpdate")]
        private static bool Menu()
        {
            if (!ModItemTraverseBoard.LookFrozen) return true;
            if (Input.GetKeyDown(KeyCode.Escape)) ModItemTraverseBoard.EscapeWork();
            return false;
        }

        private static readonly AccessTools.FieldRef<GoPointer, GoPointerButton> pointedRef = AccessTools.FieldRefAccess<GoPointer, GoPointerButton>("pointedAtButton");
        private static readonly AccessTools.FieldRef<GoPointer, PickupableItem> heldRef = AccessTools.FieldRefAccess<GoPointer, PickupableItem>("heldItem");
        private static readonly AccessTools.FieldRef<GoPointer, Ray> rayRef = AccessTools.FieldRefAccess<GoPointer, Ray>("raycastRay");
        private static readonly AccessTools.FieldRef<GoPointer, float> distRef = AccessTools.FieldRefAccess<GoPointer, float>("currentLookDistance");

        /// <summary>
        /// The game won't point at a nailed item (but crates, bottles and beds) with empty hands: a
        /// nailed board must still take its pegs.
        /// </summary>
        [HarmonyPostfix, HarmonyPatch(typeof(GoPointer), "DoRaycast")]
        private static void NailedBoard(GoPointer __instance)
        {
            if (pointedRef(__instance) != null || heldRef(__instance) != null) return;
            if (GameState.sleeping || GameState.inBed || BoatCamera.on) return;
            if (!Physics.Raycast(rayRef(__instance), out RaycastHit hit, 1.8f, -604165)) return;
            ModItemTraverseBoard b = hit.collider.GetComponent<ModItemTraverseBoard>();
            if (b == null || !b.nailed || !b.sold) return;
            pointedRef(__instance) = b;
            b.Look(__instance);
            distRef(__instance) = hit.distance;
        }
    }

    /// <summary>
    /// The Virtual Crew mod (SailwindVirtualCrew), if it is installed, read by reflection: no hard
    /// dependency. Its crew is the crew of the boat the player is aboard.
    /// </summary>
    internal static class VirtualCrew
    {
        private static bool looked;
        private static PropertyInfo instanceP, crewP;
        private static Type crewmanT;
        private static PropertyInfo roleP, nameP, exhaustedP, taskP;

        private static bool Ready()
        {
            if (looked) return instanceP != null && crewP != null && roleP != null;
            looked = true;
            try
            {
                Type mgr = AccessTools.TypeByName("SailwindVirtualCrew.VirtualCrewManager");
                crewmanT = AccessTools.TypeByName("SailwindVirtualCrew.Crewman");
                if (mgr == null || crewmanT == null) return false;
                instanceP = AccessTools.Property(mgr, "Instance");
                crewP = AccessTools.Property(mgr, "Crew");
                roleP = AccessTools.Property(crewmanT, "Role");
                nameP = AccessTools.Property(crewmanT, "Name");
                exhaustedP = AccessTools.Property(crewmanT, "IsExhausted");
                taskP = AccessTools.Property(crewmanT, "CurrentTask");
                bool ok = instanceP != null && crewP != null && roleP != null;
                Debug.Log("RealisticSkies: Virtual Crew " + (ok ? "found: its navigator or first officer copies the traverse board" : "found, but not as expected: no crew copying"));
                return ok;
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: Virtual Crew: " + e.Message);
                instanceP = null;
                return false;
            }
        }

        /// <summary>"Edmund, the navigator," when a navigator or first officer is aboard and awake; else null.</summary>
        internal static string AwakeOfficer()
        {
            if (!Ready()) return null;
            try
            {
                object mgr = instanceP.GetValue(null, null);
                if (!(crewP.GetValue(mgr, null) is System.Collections.IEnumerable crew)) return null;
                string officer = null;
                foreach (object c in crew)
                {
                    if (c == null) continue;
                    string role = roleP.GetValue(c, null).ToString();
                    bool nav = role == "Navigator";
                    if (!nav && role != "ChiefOfficer") continue;
                    if (exhaustedP != null && (bool)exhaustedP.GetValue(c, null)) continue;
                    object task = taskP != null ? taskP.GetValue(c, null) : null;
                    if (task != null && task.GetType().Name == "SleepRequest") continue;
                    string name = nameP != null ? nameP.GetValue(c, null) as string : null;
                    string rank = nav ? "navigator" : "first officer";
                    string who = string.IsNullOrEmpty(name) ? "The " + rank : name + ", the " + rank + ",";
                    if (nav) return who;                // the navigator first
                    if (officer == null) officer = who;
                }
                return officer;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

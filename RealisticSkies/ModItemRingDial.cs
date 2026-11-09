using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The universal equinoctial ring dial (prefab 717): a brass sundial that works anywhere. The
    /// meridian ring hangs plumb from a shackle set to the latitude; inside it the equatorial ring
    /// carries the hours on its inner face; the bridge across the poles carries a cursor with a
    /// pinhole, set to the date (the Sun's declination). Hung up and turned until the Sun's spot sits
    /// on the inner face's middle line, the spot reads the local apparent (sun) time, and the meridian
    /// ring lies north and south.
    ///
    /// In the hand it is folded: scroll sets the latitude, Shift + scroll the date, Shift + right click
    /// studies it. A right click unfolds it before you (a sight rig hung ahead of the eye, fixed in the
    /// world's directions); scroll turns it. Hold the right button to go in close beside the bridge,
    /// turn it finely with the mouse, and let go to read. A right click folds it again.
    ///
    /// The model and textures come from docs/make_ringdial.py (see docs/RINGDIAL_SPEC.md).
    /// State: amount = day + round(latitude * 10) / 1000 + 1e-5.
    /// </summary>
    public class ModItemRingDial : ShipItem
    {
        public const int PrefabIndex = 717;
        private const int SourceIndex = 91;
        private const string ItemName = "ring dial";

        private const float ClickTime = 0.25f, FoldTime = 0.6f, SightTime = 0.35f;
        private const float RigAhead = 0.34f, RigBelow = 0.05f;
        private const float PendulumLength = 0.12f, Damping = 0.8f, Steadied = 0.12f;
        private const float MaxLatitude = 80f;
        // the mouse turns the dial finely while sighting: degrees of dial a degree the look would have turned, x Aim Sensitivity
        private const float MouseTurn = 0.25f;

        // the drawn shadow and spot
        private const float BR_W = 0.008f, Pen = 0.0003f, HoleR = 0.00055f;
        private const float ArcHalf = 12f;                   // degrees of ring either side of the spot the skin covers
        private const int DecalW = 320, DecalH = 48, DecalSeg = 48;
        private const int RingW = 1024, RingH = 12, RingSeg = 256;       // the rings' shadows all round, coarser

        private int day;
        private float latitude = 30f;

        // open: the sight rig is up and the held item hidden
        private bool open;
        private float foldLin = 1f, foldTarget = 1f;        // 1 folded, 0 open
        private float yaw;
        private GameObject rig;
        private Transform rigEq, rigBridge, rigCursor, rigShackle, decalT;
        private MeshRenderer decalR, ringR;
        private Texture2D decalTex, ringTex;
        private Color32[] decalPx, ringPx;
        private float inShade;                              // 0 in the sun, 1 something stands between the dial and the Sun
        private string shadeBy = "";
        private RingDialMath.Spot spot;
        private Vector3 sunLocal;
        private float sunAlt, daylight;

        // the right button: a click and a hold are different things
        private int altFrame = -100;
        private bool pressing;
        private float pressStart;

        // the close sight
        private bool sighting;
        private float sightBlend;
        private Quaternion sightLocalLook = Quaternion.identity;   // the close sight's look, in the camera's parent's frame
        private Vector3 sightTarget;                        // the place on the ring it looks at, dial space
        private Quaternion smoothLook = Quaternion.identity;
        private int lookFrame = -100;
        private float sightDist;                            // and how far the eye is from it

        // the swing
        private float swing, swingRate;
        private Vector3 lastPos, lastVel, accSmooth;

        private float yawTarget;                            // where the hand turns the dial to; yaw follows it smoothly
        private float sightSun = 1f;                        // the probe's sunlight, smoothed, for the spot shader in the close sight
        private Quaternion swingRot = Quaternion.identity;  // the ring's swing about the shackle this frame
        private Vector3 hangDir = Vector3.forward;          // where the dial hangs before you, level, smoothed
        private bool haveLast;

        internal float Latitude => latitude;
        internal bool IsOpen => open;

        // ---------------- the look, frozen while sighting ----------------

        private static int frozenFrame = -100;
        private static float lookTurn;
        internal static bool LookFrozen => Time.frameCount - frozenFrame <= 2;
        /// <summary>What the mouse would have turned the look by, degrees (InstrumentAimLookPatch).</summary>
        internal static void LookTurn(float yawDeg) => lookTurn += yawDeg;

        // ---------------- life ----------------

        public override void OnLoad()
        {
            base.name = ItemName;
            if (base.amount > 0f)
            {
                day = Mathf.Clamp(Mathf.FloorToInt(base.amount), 0, ModItemNocturnal.YearLen - 1);
                latitude = Mathf.Clamp(Mathf.Round((base.amount - Mathf.Floor(base.amount)) * 1000f) / 10f, 0f, 90f);
            }
            ShowFolded();
        }

        private void Store()
        {
            base.amount = day + Mathf.Round(latitude * 10f) / 1000f + 1e-5f;
        }

        /// <summary>The item itself: always folded, the shackle and cursor where they are set.</summary>
        private void ShowFolded()
        {
            Transform t = base.transform;
            Transform eq = t.Find("eqring"), br = t.Find("bridge"), sh = t.Find("shackle");
            if (eq != null) eq.localRotation = Quaternion.AngleAxis(90f, Vector3.up);
            if (br != null)
            {
                br.localRotation = Quaternion.identity;
                Transform c = br.Find("cursor");
                if (c != null) c.localPosition = new Vector3(RingDialMath.CursorX(day), 0f, 0f);
            }
            if (sh != null) sh.localRotation = Quaternion.AngleAxis(-latitude, Vector3.forward);
        }

        private static bool ShiftKey => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        public override void OnScroll(float input)
        {
            if (!base.sold || base.held == null || input == 0f) return;
            int dir = input > 0f ? 1 : -1;
            if (open)
            {
                float step = sighting ? 0.25f : ShiftKey ? 0.5f : 2f;
                yawTarget += dir * step;
                UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.05f, 3.2f);
                return;
            }
            if (ShiftKey)
            {
                int len = ModItemNocturnal.YearLen;
                day = ((day + dir) % len + len) % len;
                Notify("Ring dial set to " + ModItemNocturnal.DateName(day));
            }
            else
            {
                latitude = Mathf.Clamp(Mathf.Round(latitude) + dir, 0f, MaxLatitude);
                Notify("Ring dial set for latitude " + latitude.ToString("0") + "°");
            }
            UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.08f, 2.4f);
            Store();
            ShowFolded();
        }

        public override void OnAltActivate()
        {
            if (!base.sold) { base.OnAltActivate(); return; }
            // the press itself counts too, so a quick click is never missed
            if (base.held != null && (open || !ShiftKey)) altFrame = Time.frameCount;
        }

        public override void OnAltHeld()
        {
            if (!base.sold || base.held == null) return;
            // Shift + right click, folded: brought close to look it over
            if (!open && !pressing && (ShiftKey || StudyMode.Studying(this)))
            {
                StudyMode.Mark(this);
                return;
            }
            altFrame = Time.frameCount;
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
        }

        public override void OnEnterInventory()
        {
            base.OnEnterInventory();
            HangableItem h = GetComponent<HangableItem>();
            if (h != null) h.DisconnectJoint();
            CloseNow();
        }

        public override void OnDrop()
        {
            base.OnDrop();
            CloseNow();
        }

        private new void OnDestroy()
        {
            base.OnDestroy();
            CloseNow();
            if (rig != null) Destroy(rig);
            if (decalTex != null) Destroy(decalTex);
            if (ringTex != null) Destroy(ringTex);
            sunProbe.Dispose();
        }

        public override void ExtraLateUpdate()
        {
            base.ExtraLateUpdate();
            bool down = Time.frameCount - altFrame <= 2;
            if (!open)
            {
                // folded in the hand: a right click opens it
                if (!base.sold || base.held == null) { pressing = false; return; }
                if (down && !pressing) { pressing = true; pressStart = Time.time; }
                if (pressing && !down)
                {
                    pressing = false;
                    if (Time.time - pressStart < ClickTime && !NavJournal.IsOpen) Unfold();
                }
                return;
            }
            if (base.held == null || !base.sold || NavJournal.IsOpen) { CloseNow(); return; }
            if (down && !pressing) { pressing = true; pressStart = Time.time; }
            if (pressing && down && !sighting && foldTarget == 0f && foldLin <= 0.001f && Time.time - pressStart >= ClickTime) BeginSight();
            if (pressing && !down)
            {
                pressing = false;
                if (sighting) EndSight(true);
                else if (Time.time - pressStart < ClickTime) Fold();
            }
            UpdateRig();
        }

        // ---------------- folding ----------------

        private void Unfold()
        {
            Camera cam = Camera.main;
            if (cam == null || !LoadModel()) return;
            Vector3 f = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (f.sqrMagnitude < 1e-4f) f = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
            f.Normalize();
            yaw = yawTarget = Mathf.Atan2(-f.z, f.x) * Mathf.Rad2Deg;           // the bridge's N end (+x) the way you look
            swing = swingRate = 0f;
            haveLast = false;
            hangDir = f;
            if (rig == null) BuildRig(base.gameObject.layer);
            rig.SetActive(true);
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            open = true;
            HookPreCull(true);
            foldLin = 1f;
            foldTarget = 0f;
            pressing = false;
            UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.08f, 1.6f);
            UpdateRig();
        }

        private void Fold()
        {
            if (sighting) EndSight(false);
            foldTarget = 1f;
            UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.08f, 1.6f);
        }

        /// <summary>Back into the hand at once (dropped, pocketed, put away).</summary>
        private void CloseNow()
        {
            if (sighting) EndSight(false);
            sightBlend = 0f;
            HookPreCull(false);
            readPending = false;
            sunProbe.CancelBurst();
            if (!open) return;
            open = false;
            pressing = false;
            foldLin = foldTarget = 1f;
            if (rig != null) rig.SetActive(false);
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = true;
            ShowFolded();
        }

        // ---------------- the rig before the eye ----------------

        private void UpdateRig()
        {
            Camera cam = Camera.main;
            if (cam == null || rig == null) return;
            float dt = Time.deltaTime;

            foldLin = Mathf.MoveTowards(foldLin, foldTarget, dt / FoldTime);
            if (foldTarget == 1f && foldLin >= 1f)
            {
                CloseNow();
                return;
            }
            float fold = Mathf.SmoothStep(0f, 1f, foldLin);

            if (sighting)
            {
                // the mouse turns the dial, finely; the look holds still
                frozenFrame = Time.frameCount;
                InstrumentAim.Mark();
                float sens = Plugin.configAimSensitivity != null ? Plugin.configAimSensitivity.Value : 0.5f;
                yawTarget -= lookTurn * MouseTurn * sens;
            }
            lookTurn = 0f;
            // The hand's tremble on the mouse comes every frame, now one way, now the other; a fine
            // instrument seen from a few centimetres shows it as the ring, its shadow and its spot
            // shaking. The turn follows the hand smoothly instead (twice the Aim Smoothing, at least 0.1 s).
            float aimTau = Mathf.Max(0.1f, 2f * (Plugin.configAimSmoothing != null ? Plugin.configAimSmoothing.Value : 0.06f));
            yaw = Mathf.Lerp(yaw, yawTarget, 1f - Mathf.Exp(-dt / aimTau));

            // the way you face, level, smoothed: on a rolling deck the camera's own forward wobbles a little
            hangDir = Vector3.Slerp(hangDir, FlatForward(cam), 1f - Mathf.Exp(-dt / 0.12f));
            hangDir.y = 0f;
            hangDir = hangDir.sqrMagnitude > 1e-6f ? hangDir.normalized : FlatForward(cam);
            Swing(cam, dt);
            Vector3 f = hangDir;
            swingRot = Quaternion.AngleAxis(swing * Mathf.Rad2Deg, Vector3.Cross(Vector3.up, f));
            Quaternion rot = swingRot * Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(latitude, Vector3.forward);
            rigRot = rot;
            Vector3 ringPoint = RingPoint(cam);
            rig.transform.rotation = rigRot;
            rig.transform.position = ringPoint - rigRot * RingDialMath.HangPoint(latitude);
            rigShackle.localRotation = Quaternion.AngleAxis(-latitude, Vector3.forward);
            rigCursor.localPosition = new Vector3(RingDialMath.CursorX(day), 0f, 0f);

            // the bridge faces the Sun
            float bridgeAngle = 0f;
            bool haveSun = Sun.sun != null && Sun.sun.transform.parent != null;
            Vector3 sunW = haveSun ? -Sun.sun.transform.parent.forward : Vector3.down;
            sunLocal = Quaternion.Inverse(rot) * sunW;
            sunAlt = Mathf.Asin(Mathf.Clamp(sunW.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (haveSun) bridgeAngle = Mathf.Atan2(sunLocal.y, -sunLocal.z) * Mathf.Rad2Deg;
            rigEq.localRotation = Quaternion.AngleAxis(90f * fold, Vector3.up);
            rigBridge.localRotation = Quaternion.AngleAxis((1f - fold) * bridgeAngle, Vector3.right);

            spot = RingDialMath.Compute(sunLocal, RingDialMath.CursorX(day), sunAlt);
            if (!haveSun) spot.fault = RingDialMath.Fault.NoSun;
            if (Mode.AxialTilt <= 0f && spot.fault == RingDialMath.Fault.None || Mode.AxialTilt <= 0f && spot.fault == RingDialMath.Fault.Miss)
                spot.fault = RingDialMath.Fault.Equinox;                // the Sun on the equator all year: the ring always shades the hole
            veil = Veil();
            if (skinR != null)
            {
                // the spot shader: the game's own shadows are taken on the GPU, pixel by pixel
                inShade = 0f;
                daylight = Mathf.Clamp01((sunAlt - 1f) / 9f) * (1f - veil);
                bool show = fold <= 0.001f && daylight > 0.001f && spot.fault != RingDialMath.Fault.Low && spot.fault != RingDialMath.Fault.NoSun;
                skinR.enabled = show;
                if (!rig.activeSelf) rig.SetActive(true);
                if (show)
                {
                    float pen = Pen * (1f + 6f * veil);
                    skinMat.SetVector("_Sun", sunLocal);
                    skinMat.SetFloat("_CursorX", RingDialMath.CursorX(day));
                    skinMat.SetFloat("_Pen", pen);
                    skinMat.SetFloat("_RingPen", 1.6f * pen);
                    skinMat.SetFloat("_HaloR", 3.5f * HoleR + 2f * pen);
                    skinMat.SetFloat("_Shade", 0.62f * Mathf.Clamp01(1f - 1.6f * veil));
                    skinMat.SetFloat("_Light", daylight);
                    Light sl = SunLight();
                    skinMat.SetFloat("_ShadowStrength", sl != null && sl.shadows != LightShadows.None ? sl.shadowStrength : 1f);
                    // the close sight takes the probe's one smoothed value, not the game's shadow map
                    skinMat.SetFloat("_MapWeight", ProbeWorks ? 1f - Mathf.SmoothStep(0f, 1f, sightBlend) : 1f);
                    skinMat.SetFloat("_Sunlit", sightSun);
                }
                if (ProbeWorks && haveSun && sunAlt > 0f)
                {
                    // kept going while open, so that its answer is ready when the sight begins
                    UpdateProbe(RingPoint(cam) + Vector3.up * 0.03f, sunW);
                    if (!readPending) sightSun = Mathf.MoveTowards(sightSun, probeLit, dt / 0.3f);
                }
                UpdateSight(cam, ringPoint, rot, dt);
                return;
            }
            // something between the dial and the Sun (an arch, a sail, the deckhouse): it is in shadow
            float shadeTarget = 0f;
            if (haveSun && sunAlt > 0f)
            {
                if (ProbeWorks) { UpdateProbe(RingPoint(cam) + Vector3.up * 0.03f, sunW); shadeTarget = 1f - probeLit; }
                else shadeTarget = Blocked(cam, rig.transform.position, sunW) ? 1f : 0f;
            }
            inShade = Mathf.MoveTowards(inShade, shadeTarget, dt / 0.15f);
            daylight = Mathf.Clamp01((sunAlt - 1f) / 9f) * (1f - veil) * (1f - inShade);
            // the bridge's shadow (and the rings') stays when the rings shade the hole: only the spot goes
            bool draw = fold <= 0.001f && daylight > 0.001f && spot.fault != RingDialMath.Fault.Low && spot.fault != RingDialMath.Fault.NoSun;
            decalR.enabled = ringR.enabled = draw;
            if (draw) DrawDecal();

            UpdateSight(cam, ringPoint, rot, dt);
        }

        // ---------------- the sun probe: is the dial in the game's own shadow? (SunExposure.cs) ----------------

        private readonly SunProbe sunProbe = new SunProbe("ring dial");
        private bool readPending;
        private static bool ProbeWorks => SunProbe.Works;
        private float probeLit => sunProbe.Lit;
        private float probeRatio => sunProbe.Ratio;

        /// <summary>Looks above the shackle: five times a second in the close sight, where the answer is used, two otherwise.</summary>
        private void UpdateProbe(Vector3 at, Vector3 sunW)
        {
            sunProbe.Tick(at, sunW, rig.layer, sighting || skinR == null ? 0.2f : 0.5f);
        }

        /// <summary>With the spot shader the probe answers the reading itself: three quick looks, the most of them wins.</summary>
        private void ReadAfterProbe()
        {
            readPending = true;
            sunProbe.Burst(shaded =>
            {
                readPending = false;
                if (!open) return;
                inShade = shaded ? 1f : 0f;
                if (shaded) daylight = 0f;
                Read();
                inShade = 0f;
            });
        }

        private static readonly RaycastHit[] hits = new RaycastHit[32];

        /// <summary>
        /// Whether anything solid stands between the dial and the Sun. The player's own body (the
        /// collider round the camera) and the item in the hand don't count; triggers don't either.
        /// </summary>
        private bool Blocked(Camera cam, Vector3 from, Vector3 sunW)
        {
            int n = Physics.RaycastNonAlloc(from, sunW, hits, 3000f, ~0, QueryTriggerInteraction.Ignore);
            Vector3 eye = cam.transform.position;
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c is CharacterController) continue;
                if (c.transform.IsChildOf(base.transform)) continue;
                // the player's body: a small collider the camera hangs from
                if (cam.transform.IsChildOf(c.transform) && c.bounds.size.magnitude < 4f) continue;
                if (!(c is MeshCollider) && !(c is TerrainCollider) && c.bounds.size.magnitude < 4f && c.bounds.Contains(eye)) continue;
                // the game is full of invisible colliders (walkways, bounds, "COL" boxes): only something
                // that is drawn and casts a shadow, there where the ray meets it, shades the dial
                if (!CastsShadowAt(c, hits[i].point)) continue;
                shadeBy = Path(c.transform) + " (" + c.GetType().Name + ")";
                return true;
            }
            shadeBy = "";
            return false;
        }

        /// <summary>A shadow-casting renderer of this collider's object (or its parent's, or a sibling's with the same mesh) covers the point.</summary>
        private static bool CastsShadowAt(Collider c, Vector3 p)
        {
            Transform t = c.transform;
            if (Covers(t.GetComponent<Renderer>(), p)) return true;
            Transform parent = t.parent;
            if (parent == null) return false;
            if (Covers(parent.GetComponent<Renderer>(), p)) return true;
            Mesh mesh = c is MeshCollider mc ? mc.sharedMesh : null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform sib = parent.GetChild(i);
                if (sib == t) continue;
                MeshFilter mf = sib.GetComponent<MeshFilter>();
                bool same = mesh != null && mf != null && mf.sharedMesh == mesh;
                // a plain box or capsule standing in for a drawn sibling at the same place
                bool along = mesh == null && mf != null && (sib.position - t.position).sqrMagnitude < 0.01f;
                if ((same || along) && Covers(sib.GetComponent<Renderer>(), p)) return true;
            }
            return false;
        }

        private static bool Covers(Renderer r, Vector3 p)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) return false;
            if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off) return false;
            Bounds b = r.bounds;
            b.Expand(0.1f);
            return b.Contains(p);
        }

        private static string Path(Transform t)
        {
            string s = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        private Quaternion rigRot = Quaternion.identity;
        private bool preCullHooked;

        private static Vector3 FlatForward(Camera cam)
        {
            Vector3 f = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (f.sqrMagnitude < 1e-4f) f = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
            return f.normalized;
        }

        /// <summary>The shackle's ring, hung ahead of the eye and a little below it.</summary>
        private Vector3 RingPoint(Camera cam)
        {
            return cam.transform.position + hangDir * RigAhead - Vector3.up * RigBelow + Vector3.up * RingDialMath.HangY;
        }

        /// <summary>
        /// The player's camera is moved after the items' late update, so the rig placed there lags a
        /// frame behind and jerks as you walk. It is placed again from the camera as it is about to draw.
        /// </summary>
        private void HookPreCull(bool on)
        {
            if (on == preCullHooked) return;
            if (on) Camera.onPreCull += PreCull; else Camera.onPreCull -= PreCull;
            preCullHooked = on;
        }

        private void PreCull(Camera cam)
        {
            if (!open || rig == null || cam == null || cam != Camera.main) return;
            // hung on a hook, dropped or put away by some path that skipped our late update: fold it
            if (base.held == null || !base.sold) { CloseNow(); return; }
            Vector3 ringPoint = RingPoint(cam);
            rig.transform.position = ringPoint - rigRot * RingDialMath.HangPoint(latitude);
            if (sightBlend > 0f && !CameraCarry.Carried) PlaceSight(cam, ringPoint, rigRot);
        }

        /// <summary>The ring hangs from the hand like a pendulum; the ship's pushes along the look set it swinging.</summary>
        private void Swing(Camera cam, float dt)
        {
            float swayMult = Plugin.configInstrumentSway != null ? Plugin.configInstrumentSway.Value : 1f;
            if (dt <= 0f) return;
            if (swayMult <= 0.001f) { swing = swingRate = 0f; haveLast = false; return; }
            if (sightBlend > 0f)
            {
                // At the close sight the hand holds the dial still: no swing at all. (The ship's smoothed
                // motion is piecewise straight, its speed changing at each physics step; even damped, the
                // pendulum took those kicks, and with its pivot ten centimetres off the eye they showed as
                // the ring trembling by pixels.)
                swing *= Mathf.Exp(-dt / 0.15f);
                swingRate = 0f;
                haveLast = false;
                return;
            }
            Vector3 pos = cam.transform.position;
            if (!haveLast) { lastPos = pos; lastVel = Vector3.zero; accSmooth = Vector3.zero; haveLast = true; return; }
            Vector3 raw = (pos - lastPos) / dt;
            lastPos = pos;
            if (raw.magnitude > 50f) { lastVel = Vector3.zero; accSmooth = Vector3.zero; return; }      // floating origin reset
            // On a ship the camera moves in steps (the ship's physics runs at its own fixed rate, not
            // every frame), so frame-to-frame velocity jumps and its difference spikes. Both are
            // smoothed over a tenth of a second or so, and the push is capped: the ring answers the
            // ship's roll, not the stepping.
            Vector3 vel = Vector3.Lerp(lastVel, raw, 1f - Mathf.Exp(-dt / 0.10f));
            Vector3 acc = (vel - lastVel) / dt;
            lastVel = vel;
            accSmooth = Vector3.Lerp(accSmooth, Vector3.ClampMagnitude(acc, 6f), 1f - Mathf.Exp(-dt / 0.12f));
            Vector3 along = hangDir;
            float a = Vector3.Dot(accSmooth, along) * swayMult;
            // at the close sight the hand steadies the dial: it takes the ship's pushes far less and settles
            float steady = Mathf.Lerp(1f, 0.15f, sightBlend), damping = Mathf.Lerp(Damping, 2.5f, sightBlend);
            float w = Mathf.Sqrt(9.81f / PendulumLength);
            int n = Mathf.Clamp(Mathf.CeilToInt(dt / 0.005f), 1, 20);
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                float accel = -w * w * Mathf.Sin(swing) - 2f * damping * w * swingRate - Steadied * steady * a / PendulumLength;
                swingRate += accel * h;
                swing += swingRate * h;
            }
            swing = Mathf.Clamp(swing, -0.4f, 0.4f);
        }

        private static Light sunLight;
        private static bool sunLightLogged;

        /// <summary>The game's sun light (whose shadows the spot shader takes).</summary>
        private static Light SunLight()
        {
            if (sunLight != null) return sunLight;
            sunLight = RenderSettings.sun;
            if (sunLight == null && Sun.sun != null) sunLight = Sun.sun.GetComponentInChildren<Light>() ?? Sun.sun.GetComponentInParent<Light>();
            if (sunLight != null && !sunLightLogged)
            {
                sunLightLogged = true;
                Debug.Log("RealisticSkies: ring dial takes the shadows of " + sunLight.name + " (" + sunLight.shadows + ", strength " + sunLight.shadowStrength.ToString("0.00") + ")");
            }
            return sunLight;
        }

        // the sky's veil over the Sun (SunExposure.cs): 0 clear, 1 the disc hidden (no spot at all)
        private float veil;
        private static float Veil() => SunExposure.Veil();
        private static float lastCloud => SunExposure.LastCloud;
        private static float lastRain => SunExposure.LastRain;

        // ---------------- the drawn shadow and spot ----------------

        private static float SS(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// The bridge's shadow and the spot, drawn on a skin over the inner face round the spot: for each
        /// texel the ray toward the Sun is met with the bridge's plane, dark within its width, bright
        /// within the pinhole.
        /// </summary>
        private void DrawDecal()
        {
            Vector3 s = sunLocal;
            float r2 = Mathf.Sqrt(s.y * s.y + s.z * s.z);
            if (r2 < 1e-4f) { decalR.enabled = ringR.enabled = false; return; }
            Vector3 nb = new Vector3(0f, s.y / r2, s.z / r2);
            Vector3 w = Vector3.Cross(Vector3.right, nb);
            float sn = Vector3.Dot(s, nb);
            float xp = RingDialMath.CursorX(day);
            float R = RingDialMath.R_EI - 0.00006f;
            float light = daylight;
            // through cloud the Sun is a blurred disc: the edges soften, and the bar's shadow (lit round
            // by the bright sky) goes before the spot does
            float pen = Pen * (1f + 6f * veil);
            float shade = 0.62f * Mathf.Clamp01(1f - 1.6f * veil);
            float haloR = 3.5f * HoleR + 2f * pen;
            float ringPen = 1.6f * pen;
            decalT.localRotation = Quaternion.AngleAxis(spot.th, Vector3.right);
            for (int i = 0; i < DecalW; i++)
            {
                float a = (spot.th + (-ArcHalf + (i + 0.5f) / DecalW * 2f * ArcHalf)) * Mathf.Deg2Rad;
                float py = -R * Mathf.Cos(a), pz = -R * Mathf.Sin(a);
                for (int j = 0; j < DecalH; j++)
                {
                    float px = -RingDialMath.T_E / 2f + (j + 0.5f) / DecalH * RingDialMath.T_E;
                    float pd = py * nb.y + pz * nb.z;
                    float t = -pd / sn;
                    Color32 c = new Color32(0, 0, 0, 0);
                    if (t > 0f)
                    {
                        float qx = px + t * s.x, qy = py + t * s.y, qz = pz + t * s.z;
                        float across = qx * w.x + qy * w.y + qz * w.z;
                        float shadow = 1f - SS(BR_W - pen, BR_W + pen, Mathf.Abs(across));
                        float dx = qx - xp;
                        float d = Mathf.Sqrt(dx * dx + across * across);
                        // the rings' own shadows: the near side of the equatorial ring and the meridian
                        // ring stand further off than the bridge, so their edges are softer; whatever
                        // light comes through the hole is cut off by them from one edge
                        RingDialMath.RingShade(new Vector3(px, py, pz), s, ringPen, out float eqShade, out float merShade);
                        float ringOcc = 1f - (1f - eqShade) * (1f - merShade);
                        float sp = (1f - SS(HoleR - pen / 2f, HoleR + pen, d)) * (1f - ringOcc);
                        float halo = 0.35f * (1f - SS(HoleR, haloR, d)) * (1f - ringOcc);
                        float lit = sp + halo;
                        float dark = shade * Mathf.Max(shadow * (1f - sp), ringOcc);
                        float alpha = Mathf.Max(dark, lit) * light;
                        float bright = Mathf.Clamp01(1.7f * lit) * (lit > 0.001f ? Mathf.Clamp01(lit / Mathf.Max(0.001f, Mathf.Max(dark, lit))) : 0f);
                        c = new Color32((byte)(255f * bright), (byte)(240f * bright), (byte)(205f * bright), (byte)(255f * Mathf.Clamp01(alpha)));
                    }
                    decalPx[j * DecalW + i] = c;
                }
            }
            decalTex.SetPixels32(decalPx);
            decalTex.Apply(false);
            DrawRingShade(ringPen, shade, light);
        }

        /// <summary>
        /// The rings' shadows on the rest of the inner face, coarser, all the way round (the patch round
        /// the spot draws them finely there): the meridian ring shades the face near the hinges, and near
        /// the equinox the near side of the equatorial ring shades a strip along the far side. The half
        /// turned away from the Sun is in its own shadow and is left alone.
        /// </summary>
        private void DrawRingShade(float pen, float shade, float light)
        {
            Vector3 s = sunLocal;
            float R = RingDialMath.R_EI - 0.00006f;
            for (int i = 0; i < RingW; i++)
            {
                float deg = -180f + (i + 0.5f) / RingW * 360f;
                bool inPatch = Mathf.Abs(Mathf.DeltaAngle(deg, spot.th)) < ArcHalf;
                float a = deg * Mathf.Deg2Rad;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                bool facing = ca * s.y + sa * s.z > 0f;           // the face's normal (toward the axis) is (0, cos a, sin a)
                for (int j = 0; j < RingH; j++)
                {
                    Color32 c = new Color32(0, 0, 0, 0);
                    if (!inPatch && facing)
                    {
                        float px = -RingDialMath.T_E / 2f + (j + 0.5f) / RingH * RingDialMath.T_E;
                        RingDialMath.RingShade(new Vector3(px, -R * ca, -R * sa), s, pen, out float eq, out float mer);
                        float occ = 1f - (1f - eq) * (1f - mer);
                        c.a = (byte)(255f * Mathf.Clamp01(shade * occ * light));
                    }
                    ringPx[j * RingW + i] = c;
                }
            }
            ringTex.SetPixels32(ringPx);
            ringTex.Apply(false);
        }

        private static Mesh decalMesh, ringMesh;

        private static Mesh DecalMesh() => decalMesh ?? (decalMesh = StripMesh(ArcHalf, DecalSeg, "ring dial spot skin"));
        private static Mesh RingMesh() => ringMesh ?? (ringMesh = StripMesh(180f, RingSeg, "ring dial ring shade skin"));

        /// <summary>A strip of the inner face, the width of the face, arcHalf degrees either side of the bottom (noon).</summary>
        private static Mesh StripMesh(float arcHalf, int seg, string name)
        {
            float R = RingDialMath.R_EI - 0.00006f, hw = RingDialMath.T_E / 2f;
            var v = new Vector3[(seg + 1) * 2];
            var uv = new Vector2[v.Length];
            var col = new Color[v.Length];
            var tri = new int[seg * 6];
            for (int k = 0; k <= seg; k++)
            {
                float u = (float)k / seg;
                float a = (-arcHalf + u * 2f * arcHalf) * Mathf.Deg2Rad;
                float y = -R * Mathf.Cos(a), z = -R * Mathf.Sin(a);
                v[2 * k] = new Vector3(-hw, y, z); uv[2 * k] = new Vector2(u, 0f);
                v[2 * k + 1] = new Vector3(hw, y, z); uv[2 * k + 1] = new Vector2(u, 1f);
                col[2 * k] = col[2 * k + 1] = Color.white;
                if (k < seg)
                {
                    int b = 2 * k;
                    tri[6 * k] = b; tri[6 * k + 1] = b + 1; tri[6 * k + 2] = b + 2;
                    tri[6 * k + 3] = b + 1; tri[6 * k + 4] = b + 3; tri[6 * k + 5] = b + 2;
                }
            }
            var m = new Mesh { name = name };
            m.vertices = v; m.uv = uv; m.colors = col; m.triangles = tri;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ---------------- the close sight ----------------

        private void BeginSight()
        {
            Camera main = Camera.main;
            if (main == null || rig == null) return;
            sighting = true;
            // where to look: the spot, or where the Sun's hour angle puts it on the middle line
            Vector3 target;
            if (spot.fault != RingDialMath.Fault.Low && spot.fault != RingDialMath.Fault.NoSun) target = spot.hit;
            else
            {
                double apparent = Sun.sun != null ? Sun.sun.localTime + Celestial.EquationOfTime(Celestial.Now) / 15.0 : 12.0;
                float th = (float)NavMath.Wrap180((apparent - 12.0) * 15.0) * Mathf.Deg2Rad;
                target = new Vector3(0f, -RingDialMath.R_EI * Mathf.Cos(th), -RingDialMath.R_EI * Mathf.Sin(th));
            }
            // The spot falls on the bridge's shadow side, so the eye goes there too: inside the rings, a
            // little off the axis toward the spot and to one side, a little south of the equatorial ring
            // (the shackle is on the north side). The bridge then stands beside the view, not across it,
            // and the inner face is seen nearly square on.
            Vector3 toSpot = new Vector3(0f, target.y, target.z).normalized;
            Vector3 side = new Vector3(0f, -toSpot.z, toSpot.y);
            Vector3 goodEye = new Vector3(-0.025f, 0f, 0f) + toSpot * 0.012f + side * 0.012f;
            Vector3 goodDir = (target - goodEye).normalized;
            // The dial is brought up to the eye along the look, as one brings a thing up to look at it:
            // the game's camera stays where it is and rides the ship as ever. Of the ways in which the
            // inner face can be seen from the bridge's shadow side, the one nearest the player's own look
            // is taken, so the look turns as little as it can (looking at the dial with the Sun behind,
            // hardly at all).
            Quaternion rot = rig.transform.rotation;
            Vector3 mine = Quaternion.Inverse(rot) * main.transform.forward;
            // the eye as far back as it can be and still see the face: far from the origin the game's
            // positions are good to some 0.03 mm, a tremble that shows less the further off one looks
            Vector3 dir = goodDir;
            float dist = (target - goodEye).magnitude;
            bool found = false;
            for (int i = 0; i <= 20 && !found; i++)
            {
                Vector3 d = Vector3.Slerp(mine, goodDir, i / 20f).normalized;
                foreach (float s in SightDists)
                {
                    if (GoodEye(target - d * s, toSpot)) { dir = d; dist = s; found = true; break; }
                }
            }
            Vector3 look = rot * dir;
            Vector3 up = Mathf.Abs(Vector3.Dot(look, Vector3.up)) < 0.9f ? Vector3.up : rot * Vector3.right;
            Transform par = main.transform.parent;
            sightLocalLook = Quaternion.Inverse(par != null ? par.rotation : Quaternion.identity) * Quaternion.LookRotation(look, up);
            sightTarget = target;
            sightDist = dist;
        }

        private const float SightNear = 0.02f;              // the close sight's near plane (the far one comes in with it)
        private static readonly float[] SightDists = { 0.06f, 0.05f, 0.042f, 0.035f };     // the eye from the place on the ring, the furthest that works

        /// <summary>
        /// An eye there (dial space) sees the inner face at the spot: inside the equatorial ring, on the
        /// spot's side of the bridge and clear of it, and not out past the ring's edge so far that the
        /// face is seen edge on.
        /// </summary>
        private static bool GoodEye(Vector3 e, Vector3 toSpot)
        {
            float r = Mathf.Sqrt(e.y * e.y + e.z * e.z);
            if (r > RingDialMath.R_EI - 0.006f) return false;                  // inside the ring
            float towardSpot = e.y * toSpot.y + e.z * toSpot.z;
            if (towardSpot < 0.008f) return false;                             // on the spot's side, clear of the bridge
            if (Mathf.Abs(e.x) > 0.045f) return false;                         // not past the poles
            return true;
        }

        private void EndSight(bool read)
        {
            if (!sighting) return;
            sighting = false;
            if (!read) return;
            if (skinR == null) { Read(); return; }
            // the spot shader knows the game's shadow, the CPU doesn't: ask the probe now (a few frames)
            if (spot.fault == RingDialMath.Fault.Low || spot.fault == RingDialMath.Fault.NoSun) { Read(); return; }   // nothing to ask the probe
            if (ProbeWorks) { ReadAfterProbe(); return; }
            Camera cam = Camera.main;
            bool haveSun = Sun.sun != null && Sun.sun.transform.parent != null;
            if (cam != null && haveSun && rig != null && Blocked(cam, rig.transform.position, -Sun.sun.transform.parent.forward)) { inShade = 1f; daylight = 0f; }
            Read();
            inShade = 0f;
        }

        private void UpdateSight(Camera main, Vector3 ringPoint, Quaternion rot, float dt)
        {
            sightBlend = Mathf.MoveTowards(sightBlend, sighting ? 1f : 0f, dt / SightTime);
        }

        /// <summary>
        /// The close view. The game's camera keeps its place on the ship; its look is turned (in the ship's
        /// own frame, so it rides the ship like any look) and narrowed for the drawing of this frame, and
        /// put back at the end of it. The dial, still fixed in the world's directions, is brought up so
        /// that the place on the ring the eye was aimed at sits in the middle of the view.
        /// </summary>
        private void PlaceSight(Camera main, Vector3 ringPoint, Quaternion rot)
        {
            float e = Mathf.SmoothStep(0f, 1f, sightBlend);
            Transform t = main.transform;
            Quaternion par = t.parent != null ? t.parent.rotation : Quaternion.identity;
            // The game keeps the camera level with the world (the deck rolls in the view), but its look
            // flickers by a hundredth of a degree each frame, one way then the other: nothing at the
            // usual field of view, a shimmer on the ring four centimetres off. The look is smoothed here.
            Quaternion want = par * sightLocalLook;
            if (Time.frameCount != lookFrame)
            {
                smoothLook = Time.frameCount - lookFrame > 2 ? want : Quaternion.Slerp(smoothLook, want, 1f - Mathf.Exp(-Time.deltaTime / 0.1f));
                lookFrame = Time.frameCount;
            }
            Quaternion camRot = Quaternion.Slerp(t.rotation, smoothLook, e);
            // first the dial comes onto the line of the look, a little way out, then along it to the eye:
            // the place looked at stays in the middle and nothing sweeps across the view
            Vector3 fwd = camRot * Vector3.forward;
            Vector3 onLine = t.position + fwd * (sightDist + (1f - e) * 0.25f) - rot * sightTarget;
            rig.transform.position = Vector3.Lerp(rig.transform.position, onLine, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sightBlend * 2.5f)));
            // no zoom: the game's own field of view (a narrower one magnified the far-from-origin tremble)
            // The near plane comes in so the ring a few centimetres off isn't cut, and the far plane comes
            // in by the same factor: the game draws its shadows from the depth buffer, and a wider range
            // between the two (near 5 cm, far 21.5 km in the game) made the deck's shadows tremble.
            float near = Mathf.Lerp(main.nearClipPlane, Mathf.Min(main.nearClipPlane, SightNear), e);
            float far = main.farClipPlane * near / Mathf.Max(1e-4f, main.nearClipPlane);
            CameraCarry.Carry(main, t.position, camRot, main.fieldOfView, near, far);
        }

        // ---------------- reading ----------------

        private void Read()
        {
            // No spot, no reading, and no message: the player sees there is nothing on the ring, and the
            // manual tells why (low Sun, noon, the equinox, shade, cloud, the dial turned too far off).
            if (spot.fault != RingDialMath.Fault.None) return;
            if (daylight <= 0.001f) return;
            double h = Celestial.Repeat(spot.hours, 24.0);
            double shown = Celestial.Repeat(Math.Round(h * 12.0) / 12.0, 24.0);        // to five minutes
            UISoundPlayer.instance.PlayOpenSound();
            Notify("Ring dial: " + ModItemNocturnal.Hm(shown) + " by the Sun, " + ModItemNocturnal.DateName(day));      // the player judges the spot by eye
            NavJournal.NoteRingDial(shown, ModItemNocturnal.DateName(day));
        }

        private static void Notify(string msg)
        {
            try { NotificationUi.instance.ShowNotification(msg); } catch (Exception) { }
        }

        // ---------------- model ----------------
        // Built by docs/make_ringdial.py.

        private static Material brass, fittings, edge, bridge, decalMat;
        private static Shader spotShader;
        private MeshRenderer skinR;
        private Material skinMat;
        private static Dictionary<string, Mesh> parts;

        private static Texture2D Tex(string file, bool linear)
        {
            Texture2D t = ModResources.Texture("RingDial/" + file, linear, out bool shared);
            if (t == null) return null;
            t.anisoLevel = 9;
            // only the engraving is kept sharp further off; sharpened normal and gloss maps make the
            // thin turned rings sparkle
            t.mipMapBias = file.Contains("albedo") ? -0.4f : 0f;
            if (!shared)
            {
                t.name = "ring dial " + file;
                if (file.Contains("albedo")) t.Compress(true);
                t.Apply(false, true);
            }
            return t;
        }

        private static Material MakeMaterial(Shader sh, string name, string prefix, string suffix, float gloss = 0.85f)
        {
            var m = new Material(sh) { name = name };
            Texture2D alb = Tex(prefix + "albedo" + suffix + ".jpg", false), mg = Tex(prefix + "metalgloss" + suffix + ".png", true), nm = Tex(prefix + "normal" + suffix + ".png", true);
            if (alb != null) m.SetTexture("_MainTex", alb);
            if (mg != null)
            {
                m.SetTexture("_MetallicGlossMap", mg);
                m.EnableKeyword("_METALLICGLOSSMAP");
                m.SetFloat("_GlossMapScale", gloss);
            }
            if (nm != null)
            {
                m.SetTexture("_BumpMap", nm);
                m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_BumpScale", 1f);
            }
            return m;
        }

        private static bool LoadModel()
        {
            if (parts != null) return true;
            byte[] bin = ModResources.Read("RingDial/ringdial.bin", out _);
            if (bin == null) return false;
            var found = new Dictionary<string, Mesh>();
            using (var r = new System.IO.BinaryReader(new System.IO.MemoryStream(bin)))
            {
                if (new string(r.ReadChars(4)) != "RDIA") throw new Exception("bad ringdial.bin");
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
                    var mesh = new Mesh { name = "ring dial " + name };
                    if (n > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices = v; mesh.normals = nn; mesh.uv = uv;
                    mesh.triangles = tris;
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    found[name] = mesh;
                }
            }
            foreach (string need in new[] { "meridian", "meridian_edge", "fittings", "eqring", "eqring_edge", "bridge", "cursor", "shackle" })
            {
                if (!found.ContainsKey(need)) throw new Exception("ringdial.bin has no part " + need);
            }
            Shader sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            brass = MakeMaterial(sh, "ringdial brass", "", "");
            fittings = MakeMaterial(sh, "ringdial fittings", "fittings_", "");
            edge = MakeMaterial(sh, "ringdial edge", "edge_", "", 0.7f);         // the lathe lines catch the light at a slant
            bridge = MakeMaterial(sh, "ringdial bridge", "bridge_", ModItemNocturnal.YearLen.ToString());
            Shader ds = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            decalMat = new Material(ds) { name = "ringdial spot" };
            // the spot shader from the bundle: everything drawn on the GPU, in the game's own shadows
            spotShader = ModResources.Shader("RingDial/RingDialSpot.shader");
            if (spotShader != null && !spotShader.isSupported) spotShader = null;
            Debug.Log("RealisticSkies: ring dial spot " + (spotShader != null ? "drawn by the bundle's shader" : "drawn on the CPU (no RingDialSpot shader in the bundle)"));
            parts = found;
            return true;
        }

        private static GameObject Part(string mesh, Material mat, Transform parent, int layer)
        {
            var go = new GameObject(mesh);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = parts[mesh];
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>The parts under a root that already shows the meridian ring.</summary>
        private static void AddParts(Transform root, int layer)
        {
            Part("meridian_edge", edge, root, layer);
            Part("fittings", fittings, root, layer);
            GameObject eq = Part("eqring", brass, root, layer);
            Part("eqring_edge", edge, eq.transform, layer);
            GameObject br = Part("bridge", bridge, root, layer);
            Part("cursor", fittings, br.transform, layer);
            Part("shackle", fittings, root, layer);
        }

        private void BuildRig(int layer)
        {
            rig = new GameObject("RealisticSkies ring dial (open)");
            rig.layer = layer;
            rig.AddComponent<MeshFilter>().sharedMesh = parts["meridian"];
            rig.AddComponent<MeshRenderer>().sharedMaterial = brass;
            AddParts(rig.transform, layer);
            rigEq = rig.transform.Find("eqring");
            rigBridge = rig.transform.Find("bridge");
            rigCursor = rigBridge.Find("cursor");
            rigShackle = rig.transform.Find("shackle");
            foreach (MeshRenderer r in rig.GetComponentsInChildren<MeshRenderer>())
            {
                // They don't take the game's shadows: its shadow map is centimetres a texel, and on rings
                // 4.5 mm thick its acne shimmers as the dial sways (the drawn shadow is the true one).
                // With the spot shader they don't cast either: the skin takes the game's shadows, and
                // the rings' own would come back on it as that same acne.
                r.shadowCastingMode = spotShader != null ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = false;
            }
            if (spotShader != null)
            {
                var k = new GameObject("spot skin (shader)");
                k.layer = layer;
                k.transform.SetParent(rigEq, false);
                k.AddComponent<MeshFilter>().sharedMesh = RingMesh();
                skinR = k.AddComponent<MeshRenderer>();
                skinR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                skinR.receiveShadows = true;
                skinR.material = skinMat = new Material(spotShader) { name = "ringdial spot (shader)" };
                skinR.enabled = false;
                rig.SetActive(false);
                return;
            }
            var d = new GameObject("spot skin");
            d.layer = layer;
            d.transform.SetParent(rigEq, false);
            d.AddComponent<MeshFilter>().sharedMesh = DecalMesh();
            decalR = d.AddComponent<MeshRenderer>();
            decalR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            decalR.receiveShadows = false;
            decalTex = new Texture2D(DecalW, DecalH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "ring dial spot" };
            decalPx = new Color32[DecalW * DecalH];
            decalR.material = new Material(decalMat) { mainTexture = decalTex };
            // Sprites/Default takes its tint and flip per renderer, which only a SpriteRenderer sets;
            // left at zero, the skin is invisible (and squashed flat)
            var block = new MaterialPropertyBlock();
            block.SetColor("_RendererColor", Color.white);
            block.SetVector("_Flip", new Vector4(1f, 1f, 1f, 1f));
            block.SetTexture("_MainTex", decalTex);             // [PerRendererData] in Sprites/Default
            decalR.SetPropertyBlock(block);
            decalR.enabled = false;
            Debug.Log("RealisticSkies: ring dial spot drawn with " + decalMat.shader.name);
            decalT = d.transform;

            var g = new GameObject("ring shade skin");
            g.layer = layer;
            g.transform.SetParent(rigEq, false);
            g.AddComponent<MeshFilter>().sharedMesh = RingMesh();
            ringR = g.AddComponent<MeshRenderer>();
            ringR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringR.receiveShadows = false;
            ringTex = new Texture2D(RingW, RingH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "ring dial ring shade" };
            ringPx = new Color32[RingW * RingH];
            ringR.material = new Material(decalMat) { mainTexture = ringTex };
            var block2 = new MaterialPropertyBlock();
            block2.SetColor("_RendererColor", Color.white);
            block2.SetVector("_Flip", new Vector4(1f, 1f, 1f, 1f));
            block2.SetTexture("_MainTex", ringTex);
            ringR.SetPropertyBlock(block2);
            ringR.enabled = false;
            rig.SetActive(false);
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
                if (ShopSpots.TryGet(ShopSpots.RingDial, island, out ShopSpots.Spot spot))
                {
                    ShopSpots.Apply(clone.transform, shop, spot);
                }
                else
                {
                    clone.transform.localPosition = sp.transform.localPosition + new Vector3(-0.25f, 0.02f, 0.38f);   // beside the nocturnal
                    clone.transform.rotation = shop.rotation * Quaternion.Euler(90f, 0f, 0f);          // lying face up
                }
                ShopItemSpawner csp = clone.GetComponent<ShopItemSpawner>();
                AccessTools.Field(typeof(ShopItemSpawner), "item").SetValue(csp, null);
                csp.enabled = false;
                Renderer r = clone.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                var hook = clone.AddComponent<LoreSpawnerHook>();
                hook.spawner = csp;
                hook.prefabIndex = PrefabIndex;
                ShopSpots.RegisterSpawner(ShopSpots.RingDial, island, clone.transform);
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
                Debug.LogWarning("RealisticSkies: vanilla scroll (prefab 91) not found, no ring dial");
                return;
            }
            if (dir.directory.Length > PrefabIndex && dir.directory[PrefabIndex] != null)
            {
                Debug.LogWarning("RealisticSkies: prefab " + PrefabIndex + " is already taken, no ring dial");
                return;
            }
            try
            {
                if (!LoadModel())
                {
                    Debug.LogWarning("RealisticSkies: ring dial model missing, no ring dial");
                    return;
                }
                holder = new GameObject("RealisticSkies ring dial holder");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
                GameObject go = UnityEngine.Object.Instantiate(dir.directory[SourceIndex], holder.transform);
                go.name = PrefabIndex + " " + ItemName;

                ShipItemScroll old = go.GetComponent<ShipItemScroll>();
                ModItemRingDial item = go.AddComponent<ModItemRingDial>();
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
                box.center = new Vector3(0f, 0.015f, 0f);
                // as thick as the meridian ring (4.5 mm), so that lying flat it rests on the ring and doesn't
                // float; the brackets and stops (5.5 mm proud) and the shackle's eye then sink a little
                box.size = new Vector3(0.16f, 0.23f, 0.0045f);

                go.GetComponent<MeshFilter>().sharedMesh = parts["meridian"];
                go.GetComponent<MeshRenderer>().sharedMaterial = brass;
                AddParts(go.transform, go.layer);
                item.transform.Find("eqring").localRotation = Quaternion.AngleAxis(90f, Vector3.up);
                item.transform.Find("shackle").localRotation = Quaternion.AngleAxis(-30f, Vector3.forward);
                item.transform.Find("bridge/cursor").localPosition = new Vector3(RingDialMath.CursorX(0), 0f, 0f);

                item.name = ItemName;
                item.amount = 0f;
                item.health = 0f;
                item.holdDistance = 0.25f;
                item.inventoryRotationX = 0f;
                item.inventoryRotation = 0f;
                item.value = 155;
                if (Patches.itemPrefabs.TryGetValue(703, out GameObject chart) && chart != null
                    && chart.GetComponent<ShipItem>() is ShipItem chartItem && chartItem.value > 0)
                {
                    // about 155 crowns, 70% of the pocket watch (0.85): fine work and never wrong, only fussy about the weather
                    item.value = Mathf.RoundToInt(chartItem.value * 0.6f);
                }
                item.mass = 0.5f;

                // hangs on a lamp hook by its shackle's ring, the shackle up (HangPatch)
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
                Debug.Log("RealisticSkies: added the ring dial as prefab " + PrefabIndex + " (" + ModItemNocturnal.YearLen + "-day year)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not build the ring dial: " + e);
            }
        }
    }
}

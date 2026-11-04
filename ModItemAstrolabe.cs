using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The mariner's astrolabe (prefab 713): a heavy brass ring hung from the thumb by its shackle, so
    /// that it hangs plumb under its own weight, with an alidade turning on its centre and two sight
    /// vanes with pinholes. No horizon is needed: the hanging ring gives the vertical, so stars can be
    /// taken at night. The scale reads the zenith distance, 0 at the top as on the old instruments;
    /// the journal takes the altitude as 90 degrees less the reading.
    ///
    /// Use: hold the right mouse button to raise it to the eye and look through the pinholes; bring the
    /// body into the hole and let go: the reading goes into the Navigator's Journal. At sea the ring
    /// swings with the ship, and the hole with it: catch the body as the swing passes the middle.
    /// State: amount = the zenith distance last read (the alidade is left there).
    /// </summary>
    public class ModItemAstrolabe : ShipItem
    {
        public const int PrefabIndex = 713;
        private const int SourceIndex = 91;
        private const string ItemName = "astrolabe";

        // the instrument, metres: in its own xy plane, the front (scale) toward -z, the shackle up
        private const float R = 0.10f, RimW = 0.016f, Thick = 0.012f;

        // the pinhole sight
        private const float SightFov = 30f;
        private const float HoleRadius = 1.2f;              // degrees
        // the hanging ring as a pendulum: about a 0.7 s swing, lightly damped
        private const float PendulumLength = 0.12f, Damping = 0.30f;
        private const float Steadied = 0.6f;                // the heavy ring, steadied by the hand: it takes the pushes less

        private float zenith = 45f;                         // the reading the alidade is left at
        private Transform alidade;
        private int sightFrame = -100;
        private bool sighting;
        private bool sunSight;          // the Sun behind you, its spot on the vane before you: the alidade is the reverse of your look
        private bool sunHinted;         // told, this sight, not to look into the Sun
        private float savedFov = -1f;

        // the swing in the plane of sight (radians) and its rate
        private float swing, swingRate;
        private Vector3 lastPos, lastVel;
        private bool haveLast;

        private static Texture2D holeTex, discTex, glowTex, vaneTex;

        // The Sun is not looked at through the holes: its light through the upper vane's hole is let
        // fall on the lower vane, and the spot of light is brought onto the lower hole.
        private const float VaneWithin = 2.6f;              // degrees off the line: the spot is near the vane
        private const float SunSemi = 0.267f;               // the Sun's true half-width, degrees
        // the alidade as the model has it, millimetres: the vanes 104 apart, 17 wide and 19 high,
        // pierced 2.4 across at 12 up
        private const float VaneGap = 104f, VaneW = 17f, VaneH = 19f, VaneHoleY = 12f, PinR = 1.2f, AlidadeW = 6.8f;
        private static Texture2D spotTex;

        // ---------------- life ----------------

        public override void OnLoad()
        {
            base.name = ItemName;
            alidade = base.transform.Find("alidade");
            if (base.amount > 0f && base.amount <= 90f) zenith = base.amount;
            ShowAlidade();
        }

        private void ShowAlidade()
        {
            // the alidade's line at the zenith distance from the top, as read on the scale
            if (alidade != null) alidade.localRotation = Quaternion.Euler(0f, 0f, 90f - zenith);
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
            else if (!want && sighting) EndSight(base.held != null);
            if (base.held != null) Swing();
            UpdateVaneView();
            if (sighting) InstrumentAim.Mark();
        }

        // ---------------- the swing ----------------

        /// <summary>
        /// The ring hangs from the hand like a pendulum; the hand moves with the ship, and its sideways
        /// pushes (along the line of sight, level) set the ring swinging in its own plane.
        /// </summary>
        private void Swing()
        {
            Camera cam = Camera.main;
            float dt = Time.deltaTime;
            if (cam == null || dt <= 0f) return;
            Vector3 pos = cam.transform.position;
            if (!haveLast) { lastPos = pos; lastVel = Vector3.zero; haveLast = true; return; }
            Vector3 vel = (pos - lastPos) / dt;
            Vector3 acc = (vel - lastVel) / dt;
            lastPos = pos;
            lastVel = vel;
            if (acc.magnitude > 30f || vel.magnitude > 50f) acc = Vector3.zero;     // the world was moved (floating origin)
            Vector3 along = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            float a = along.sqrMagnitude > 1e-4f ? Vector3.Dot(acc, along.normalized) : 0f;
            float w = Mathf.Sqrt(9.81f / PendulumLength);
            // a few small steps: the swing is quick against the frame
            int n = Mathf.Clamp(Mathf.CeilToInt(dt / 0.005f), 1, 20);
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                float accel = -w * w * Mathf.Sin(swing) - 2f * Damping * w * swingRate - Steadied * a / PendulumLength;
                swingRate += accel * h;
                swing += swingRate * h;
            }
            swing = Mathf.Clamp(swing, -0.6f, 0.6f);
        }

        // ---------------- at the eye ----------------

        private void BeginSight()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            sighting = true;
            savedFov = cam.fieldOfView;
            cam.fieldOfView = SightFov;
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        }

        private void EndSight(bool read)
        {
            if (!sighting) return;
            sighting = false;
            if (vaneRig != null) vaneRig.SetActive(false);
            sunHinted = false;
            Camera cam = Camera.main;
            if (cam != null && savedFov > 0f) cam.fieldOfView = savedFov;
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = true;
            if (read) Read();
        }

        /// <summary>The line through the pinholes: where you look, tipped by the ring's swing.</summary>
        private Vector3 SightLine(Camera cam)
        {
            return Quaternion.AngleAxis(-swing * Mathf.Rad2Deg, cam.transform.right) * cam.transform.forward;
        }

        private void Read()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            // the alidade was turned to where you looked; the ring's vertical was off by its swing
            bool sun = sunSight;
            float looked = ModItemCrossStaff.Altitude(sun ? -cam.transform.forward : cam.transform.forward);
            double z = 90.0 - looked;
            if (z < 0.0 || z > 90.0)
            {
                Notify("Below the horizon: nothing to read");
                return;
            }
            z = Math.Round(z * 6.0) / 6.0;                  // read to ten minutes on the scale
            zenith = (float)z;
            base.amount = zenith;
            ShowAlidade();
            double alt = 90.0 - z;
            UISoundPlayer.instance.PlayOpenSound();
            Notify("Astrolabe:" + Celestial.Format(z) + "' from the zenith");
            // for checking afterwards: what the line through the holes really pointed at
            Vector3 line = sun ? -SightLine(cam) : SightLine(cam);
            Vector3 dir = Vector3.zero;
            int b = Celestial.HaveSkyAxes ? ModItemCrossStaff.BodyAt(line, HoleRadius * 2f, true, out dir) : int.MinValue;
            string truth = b != int.MinValue ? ", true" + Celestial.Format(ModItemCrossStaff.Altitude(dir)) + "'" : ", nothing in the hole";
            Debug.Log("RealisticSkies: astrolabe altitude" + Celestial.Format(alt) + "' (zenith" + Celestial.Format(z) + "')" + truth +
                      ", swing " + (swing * Mathf.Rad2Deg).ToString("0.00") + " deg");
            NavJournal.NoteAltitude(alt);
        }

        private static void Notify(string msg)
        {
            try { NotificationUi.instance.ShowNotification(msg); } catch (Exception) { }
        }

        private static Texture2D HoleTexture()
        {
            if (holeTex != null) return holeTex;
            const int N = 256;
            holeTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // a round hole with a soft brass-dark rim
                    float a = Mathf.Clamp01((r - 0.9f) / 0.1f);
                    px[y * N + x] = new Color32(12, 9, 5, (byte)(a * 255f));
                }
            }
            holeTex.SetPixels32(px);
            holeTex.Apply();
            return holeTex;
        }

        /// <summary>The far vane's pinhole, swinging with the ring; the rest of the view is the vane's brass.</summary>
        private void OnGUI()
        {
            if (!sighting || Event.current.type != EventType.Repaint) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 line = SightLine(cam);
            if (vaneRig != null && vaneRig.activeSelf) return;            // the vane itself is up before the eye
            // the flat vane only if the model is missing; with it, looking into the Sun is just the hole
            if (vaneParts == null && vaneModelTried && SunOnVane(cam, -line)) return;
            if (!sunHinted && Sun.sun != null && Vector3.Angle(line, -Sun.sun.transform.parent.forward) < 6f)
            {
                sunHinted = true;
                Notify("Don't look into the Sun: turn your back to it and look down at the vane");
            }
            Vector3 sp = cam.WorldToScreenPoint(cam.transform.position + line);
            if (sp.z <= 0f) return;
            float px = Screen.height / 2f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float r = px * Mathf.Tan(HoleRadius * Mathf.Deg2Rad);
            float cx = sp.x, cy = Screen.height - sp.y;
            var hole = new Rect(cx - r, cy - r, 2f * r, 2f * r);
            Color dark = new Color(0.047f, 0.035f, 0.02f, 1f);
            Color old = GUI.color;
            GUI.color = dark;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Mathf.Max(0f, hole.yMin)), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, hole.yMax, Screen.width, Mathf.Max(0f, Screen.height - hole.yMax)), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, hole.yMin, Mathf.Max(0f, hole.xMin), hole.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(hole.xMax, hole.yMin, Mathf.Max(0f, Screen.width - hole.xMax), hole.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(hole, HoleTexture());
            GUI.color = old;
        }

        /// <summary>
        /// Near the Sun: the lower vane, seen close, with its little hole in the middle and the spot of
        /// sunlight through the upper hole on it. The spot lies opposite to where the Sun is off the
        /// line (Sun too high: the spot falls low), at the Sun's true size. Spot on the hole: the
        /// alidade is on the Sun.
        /// </summary>
        private bool SunOnVane(Camera cam, Vector3 line)
        {
            if (Sun.sun == null || Sun.sun.transform.parent == null) return false;
            Vector3 sun = -Sun.sun.transform.parent.forward;
            if (sun.y <= -0.005f || Vector3.Angle(line, sun) > VaneWithin) return false;
            Vector3 right = cam.transform.right;
            Vector3 up = Vector3.Cross(line, right).normalized;
            float along = Vector3.Dot(sun, line);
            float ox = Mathf.Atan2(Vector3.Dot(sun, right), along);
            float oy = Mathf.Atan2(Vector3.Dot(sun, up), along);

            float w = Screen.width, h = Screen.height;
            float mm = Mathf.Min(0.30f * w / VaneW, 0.62f * h / VaneH);     // pixels a millimetre on the vane
            float cx = w / 2f, cy = h / 2f;
            Color old = GUI.color;
            // the world behind, dimmed: the eye is on the vane
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            // the alidade's arm running down from the vane, then the vane itself (its corners rounded
            // as cast, the edges bevelled and catching the light), its hole on the line; both throw a
            // soft shadow on the dimmed world behind
            float edge = VaneRound * mm;
            var vane = new Rect(cx - (VaneW / 2f + VaneRound) * mm, cy - (VaneH - VaneHoleY + VaneRound) * mm, (VaneW + 2f * VaneRound) * mm, (VaneH + 2f * VaneRound) * mm);
            var armR = new Rect(cx - AlidadeW / 2f * mm, vane.yMax - edge * 2f, AlidadeW * mm, h - vane.yMax + edge * 2f);
            GUI.color = Color.white;
            // the arm's grain at its true length: the painted strip is ArmLengthMm long, only its top shows
            float armMm = armR.height / mm;
            GUI.DrawTextureWithTexCoords(armR, ArmTexture(), new Rect(0f, 1f - Mathf.Min(1f, armMm / ArmLengthMm), 1f, Mathf.Min(1f, armMm / ArmLengthMm)));
            GUI.DrawTexture(vane, VanePlateTexture());

            // the light through the upper hole: the hole's own size, blurred at its edge by the Sun's
            // width over the length of the alidade, and moved the opposite way to the Sun off the line
            float blur = VaneGap * Mathf.Tan(SunSemi * Mathf.Deg2Rad);
            float rOut = (PinR + blur) * mm;
            float sx = cx - VaneGap * Mathf.Tan(ox) * mm, sy = cy + VaneGap * Mathf.Tan(oy) * mm;
            GUI.BeginGroup(vane);
            Vector2 o = vane.position;
            float hr = PinR * mm;
            bool painted = VanePlateTexture() != null && VanePlateTexture().name.EndsWith("vane.png");
            GUI.color = Color.white;                                       // the hole's countersink, part of the brass (painted in)
            if (!painted) GUI.DrawTexture(new Rect(cx - hr * HoleChamfer - o.x, cy - hr * HoleChamfer - o.y, 2f * hr * HoleChamfer, 2f * hr * HoleChamfer), HoleRimTexture());
            GUI.color = new Color(1f, 0.85f, 0.5f, 0.25f);
            float gr = rOut * 2.2f;
            GUI.DrawTexture(new Rect(sx - gr - o.x, sy - gr - o.y, 2f * gr, 2f * gr), GlowTexture());
            GUI.color = new Color(1f, 0.96f, 0.82f, 1f);
            GUI.DrawTexture(new Rect(sx - rOut - o.x, sy - rOut - o.y, 2f * rOut, 2f * rOut), SpotTexture((PinR - blur) / (PinR + blur)));
            // the lower vane's hole: what falls in it goes on to the eye, not onto the brass
            Texture2D holeTexP = painted ? HolePainted() : null;
            if (holeTexP != null)
            {
                GUI.color = Color.white;
                float hc = hr * HoleChamfer;
                GUI.DrawTexture(new Rect(cx - hc - o.x, cy - hc - o.y, 2f * hc, 2f * hc), holeTexP);
            }
            else
            {
                GUI.color = new Color(0.015f, 0.01f, 0.006f, 1f);
                GUI.DrawTexture(new Rect(cx - hr - o.x, cy - hr - o.y, 2f * hr, 2f * hr), DiscTexture());
            }
            GUI.EndGroup();
            GUI.color = old;
            return true;
        }

        private const float VaneRound = 1.5f;          // mm: the vane's cast edge rounded off
        private const float HoleChamfer = 1.35f;       // the hole's countersink, as a part of its radius
        private static Texture2D vanePlateTex, armTex, holeRimTex;
        private static readonly Vector2 Light = new Vector2(-0.55f, 0.83f);     // from the upper left (texture y is up)

        private const float ArmLengthMm = 40f;          // the painted arm strip (docs/make_vane.py)
        private static Texture2D holePaintedTex;
        private static Texture2D HolePainted() => holePaintedTex != null ? holePaintedTex : (holePaintedTex = Painted("vane_hole.png"));

        private static Texture2D Painted(string file)
        {
            Texture2D t = ModResources.Texture("Astrolabe/" + file, false, out bool shared);
            if (t == null) return null;
            if (!shared) { t.name = "astrolabe " + file; t.hideFlags = HideFlags.HideAndDontSave; }
            t.wrapMode = TextureWrapMode.Clamp;
            t.anisoLevel = 4;
            return t;
        }

        // ---- brass, made procedurally: value noise for the grain, lit as polished metal ----

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967295f;
            }
        }

        private static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx);
            float b = Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }

        /// <summary>Brushed up and down: fine streaks that run long, a few broad ones, a faint mottle. 0..1 around 0.5.</summary>
        private static float Grain(float xmm, float ymm, int seed)
        {
            float o = seed * 97.3f;
            return 0.60f * Noise(xmm * 70f + o, ymm * 0.35f) + 0.25f * Noise(xmm * 18f + o, ymm * 0.2f + o) + 0.15f * Noise(xmm * 0.6f + o, ymm * 0.6f);
        }

        /// <summary>
        /// Polished brass under an open sky: brighter where the face looks up at the sky, a soft
        /// highlight toward the upper left, the grain catching the light in streaks.
        /// </summary>
        private static Color32 LitBrass(float grain, float up, float highlight, float edgeShade, float alpha)
        {
            Color baseCol = new Color(0.78f, 0.60f, 0.31f);
            Color sky = new Color(1.0f, 0.95f, 0.82f);
            float k = 0.70f + 0.30f * up + 0.06f * (grain - 0.5f) * 2f;
            Color c = baseCol * k;
            c = Color.Lerp(c, sky, Mathf.Clamp01(highlight) * 0.6f);
            c *= edgeShade;
            c.a = Mathf.Clamp01(alpha);
            return c;
        }

        /// <summary>The vane's face: rounded cast corners, the brushed face, a rounded-over edge.</summary>
        private static Texture2D VanePlateTexture()
        {
            if (vanePlateTex != null) return vanePlateTex;
            vanePlateTex = Painted("vane.png");                 // old brass, from docs/make_vane.py
            if (vanePlateTex != null) return vanePlateTex;
            const float PxMm = 30f;
            float wmm = VaneW + 2f * VaneRound, hmm = VaneH + 2f * VaneRound;
            int nx = Mathf.RoundToInt(wmm * PxMm), ny = Mathf.RoundToInt(hmm * PxMm);
            vanePlateTex = new Texture2D(nx, ny, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave, anisoLevel = 4 };
            var px = new Color32[nx * ny];
            float hx = wmm / 2f - VaneRound, hy = hmm / 2f - VaneRound, bevel = 0.9f;
            for (int y = 0; y < ny; y++)
            {
                for (int x = 0; x < nx; x++)
                {
                    float qx = (x + 0.5f) / PxMm - wmm / 2f, qy = (y + 0.5f) / PxMm - hmm / 2f;
                    float dx = Mathf.Abs(qx) - hx, dy = Mathf.Abs(qy) - hy;
                    float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
                    float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f) - VaneRound;
                    float a = Mathf.Clamp01(0.5f - d * PxMm);
                    if (a <= 0f) { px[y * nx + x] = new Color32(0, 0, 0, 0); continue; }
                    Vector2 n = ox > 0f || oy > 0f ? new Vector2(Mathf.Sign(qx) * ox, Mathf.Sign(qy) * oy).normalized
                              : (dx > dy ? new Vector2(Mathf.Sign(qx), 0f) : new Vector2(0f, Mathf.Sign(qy)));
                    float into = Mathf.Clamp01(-d / bevel);                     // 0 at the very edge, 1 on the flat
                    float tilt = 1f - into;                                     // how far the rounded edge turns away
                    float up = (qy / hmm + 0.5f) * into + Mathf.Max(0f, n.y) * tilt;
                    float hl = Mathf.Exp(-(Mathf.Pow((qx + wmm * 0.15f) / (wmm * 0.22f), 2f) + Mathf.Pow((qy - hmm * 0.10f) / (hmm * 0.55f), 2f))) * (0.85f + 0.3f * Grain(qx, qy, 5));   // drawn out along the brushing
                    hl += tilt * Mathf.Max(0f, Vector2.Dot(n, Light)) * 1.2f;   // the rounded edge facing the light glints
                    float shade = 1f - tilt * Mathf.Max(0f, -Vector2.Dot(n, Light)) * 0.55f - Mathf.Pow(tilt, 3f) * 0.25f;
                    px[y * nx + x] = LitBrass(Grain(qx, qy, 1), up, hl, shade, a);
                }
            }
            vanePlateTex.SetPixels32(px);
            vanePlateTex.Apply(true);
            return vanePlateTex;
        }

        /// <summary>The alidade's arm below the vane: the same brass, rounded over along both sides.</summary>
        private static Texture2D ArmTexture()
        {
            if (armTex != null) return armTex;
            armTex = Painted("vane_arm.png");
            if (armTex != null) return armTex;
            const float PxMm = 30f;
            int nx = Mathf.RoundToInt(AlidadeW * PxMm), ny = 512;
            armTex = new Texture2D(nx, ny, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave, anisoLevel = 4 };
            var px = new Color32[nx * ny];
            for (int y = 0; y < ny; y++)
            {
                for (int x = 0; x < nx; x++)
                {
                    float q = (x + 0.5f) / PxMm - AlidadeW / 2f;
                    float ymm = y / (float)ny * 40f;
                    float into = Mathf.Clamp01((AlidadeW / 2f - Mathf.Abs(q)) / 0.8f);
                    float tilt = 1f - into;
                    float side = Mathf.Sign(q);
                    float hl = tilt * Mathf.Max(0f, side * Light.x) * 1.1f + 0.15f * (y / (float)ny);
                    float shade = 0.86f - tilt * Mathf.Max(0f, -side * Light.x) * 0.5f;       // it lies a little in the vane's shade
                    float a = Mathf.Clamp01((AlidadeW / 2f - Mathf.Abs(q)) * PxMm + 0.5f);
                    px[y * nx + x] = LitBrass(Grain(q, ymm, 2), 0.4f + 0.3f * (y / (float)ny), hl, shade, a);
                }
            }
            armTex.SetPixels32(px);
            armTex.Apply(true);
            return armTex;
        }

        /// <summary>The hole: black through the middle, its countersunk rim lit on the side away from the light.</summary>
        private static Texture2D HoleRimTexture()
        {
            if (holeRimTex != null) return holeRimTex;
            const int N = 256;
            holeRimTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[N * N];
            float inner = 1f / HoleChamfer;
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float aa = 2f / N;
                    if (r > 1f + aa) { px[y * N + x] = new Color32(0, 0, 0, 0); continue; }
                    if (r < inner)
                    {
                        float edgeA = Mathf.Clamp01((inner - r) / aa);
                        px[y * N + x] = Color32.Lerp(LitBrass(0.5f, 0f, 0f, 0.35f, 1f), new Color32(4, 3, 2, 255), edgeA);
                        continue;
                    }
                    // the countersink slopes down toward the hole: its face looks inward, so it is lit
                    // on the far side from the light
                    Vector2 inward = r > 1e-4f ? new Vector2(-dx / r, -dy / r) : Vector2.zero;
                    float lit = Vector2.Dot(inward, Light);
                    px[y * N + x] = LitBrass(0.5f, 0.3f, Mathf.Max(0f, lit) * 0.9f, 0.62f + 0.3f * lit, Mathf.Clamp01((1f - r) / aa + 0.5f));
                }
            }
            holeRimTex.SetPixels32(px);
            holeRimTex.Apply();
            return holeRimTex;
        }

        // ---------------- the vane at the eye, as a model (Sun sights) ----------------
        // The lower vane and a piece of the arm, built by docs/make_vane_model.py, hung before the camera
        // and lit by the game (and a little light of their own, on a layer nothing else uses, so the
        // face toward the eye isn't black with the Sun behind it). The spot of sunlight is a quad on
        // its face whose texture is redrawn as the Sun moves; what falls in the hole is left out.
        // Only a picture: the reading is the camera's line, as before.

        private const float VaneViewDist = 0.30f, VaneViewScale = 4f;       // as the real vane 7.5 cm from the eye
        private static readonly Vector3 VaneViewTurn = new Vector3(-7f, 12f, 0f);
        private const float VaneShowWithin = 6f;              // the vane comes up before the spot reaches it
        // the spot's quad covers the whole face of the vane (mm from the hole), so the light runs over all of it
        private const float SpotX0 = -10f, SpotX1 = 10f, SpotY0 = -13.5f, SpotY1 = 8.5f, VaneFrontMm = 1.0f;
        private const int SpotNX = 120, SpotNY = 132;
        private static GameObject vaneRig;
        private static Light vaneLight;
        private static Texture2D spotLive;
        private static Color32[] spotPx;
        private static Dictionary<string, Mesh> vaneParts;
        private static bool vaneModelTried;

        private void UpdateVaneView()
        {
            Camera cam = Camera.main;
            float ox = 0f, oy = 0f;
            // with the Sun at your back you look down at the vane, as the pilots did; the alidade then
            // points the other way from your look, up at the Sun over your shoulder
            bool show = sighting && cam != null && SunOffset(cam, -SightLine(cam), out ox, out oy);
            sunSight = show;
            if (!show)
            {
                if (vaneRig != null && vaneRig.activeSelf) vaneRig.SetActive(false);
                return;
            }
            if (!EnsureVaneRig(cam)) return;
            if (!vaneRig.activeSelf) vaneRig.SetActive(true);
            if (vaneLight != null) vaneLight.transform.rotation = cam.transform.rotation * Quaternion.LookRotation(new Vector3(0.45f, -0.6f, 0.65f));
            DrawSpot(-VaneGap * Mathf.Tan(ox), -VaneGap * Mathf.Tan(oy));
        }

        /// <summary>The Sun's offset from the line through the holes, radians right and up; false when it isn't near.</summary>
        private static bool SunOffset(Camera cam, Vector3 line, out float ox, out float oy)
        {
            ox = oy = 0f;
            if (Sun.sun == null || Sun.sun.transform.parent == null) return false;
            Vector3 sun = -Sun.sun.transform.parent.forward;
            if (sun.y <= -0.005f || Vector3.Angle(line, sun) > VaneShowWithin) return false;
            // the vane faces you: its right is your right, its up your up (seen as in a mirror)
            Vector3 right = cam.transform.right;
            Vector3 up = Vector3.ProjectOnPlane(cam.transform.up, line).normalized;
            float along = Vector3.Dot(sun, line);
            ox = Mathf.Atan2(Vector3.Dot(sun, right), along);
            oy = Mathf.Atan2(Vector3.Dot(sun, up), along);
            return true;
        }

        private static Texture2D VaneTex(string file, bool linear)
        {
            Texture2D t = ModResources.Texture("Astrolabe/" + file, linear, out bool shared);
            if (t == null) return null;
            if (!shared) { t.name = "astrolabe " + file; t.hideFlags = HideFlags.HideAndDontSave; }
            t.anisoLevel = 4;
            return t;
        }

        private static Material VaneBrass(string albedo, string normal)
        {
            Shader sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            var m = new Material(sh) { name = "astrolabe vane brass", hideFlags = HideFlags.HideAndDontSave };
            Texture2D a = VaneTex(albedo, false), n = VaneTex(normal, true);
            if (a != null) m.SetTexture("_MainTex", a);
            if (n != null)
            {
                m.SetTexture("_BumpMap", n);
                m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_BumpScale", 1f);
            }
            m.SetFloat("_Metallic", 0.55f);
            m.SetFloat("_Glossiness", 0.45f);
            return m;
        }

        private static bool EnsureVaneRig(Camera cam)
        {
            if (vaneRig != null && vaneRig.transform.parent == cam.transform) return true;
            if (vaneRig != null) UnityEngine.Object.Destroy(vaneRig);
            vaneRig = null;
            if (vaneParts == null)
            {
                if (vaneModelTried) return false;
                vaneModelTried = true;
                byte[] bin = ModResources.Read("Astrolabe/vane_model.bin", out _);
                if (bin == null) return false;
                vaneParts = ReadParts(bin, "astrolabe vane ");
            }
            int layer = 0;
            for (int l = 31; l >= 8; l--)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(l)) && (cam.cullingMask & (1 << l)) != 0) { layer = l; break; }
            }
            vaneRig = new GameObject("astrolabe vane view");
            vaneRig.layer = layer;
            vaneRig.transform.SetParent(cam.transform, false);
            vaneRig.transform.localPosition = new Vector3(0f, 0f, VaneViewDist);
            vaneRig.transform.localRotation = Quaternion.Euler(VaneViewTurn);
            vaneRig.transform.localScale = Vector3.one * VaneViewScale;
            void Piece(string part, Material mat)
            {
                if (!vaneParts.TryGetValue(part, out Mesh mesh)) return;
                var go = new GameObject(part) { layer = layer };
                go.transform.SetParent(vaneRig.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            Piece("vane", VaneBrass("vane_albedo.png", "vane_normal.png"));
            Piece("arm", VaneBrass("vane_arm_albedo.png", "vane_arm_normal.png"));
            Shader std = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            var dark = new Material(std) { name = "astrolabe vane cap", color = new Color(0.03f, 0.025f, 0.02f), hideFlags = HideFlags.HideAndDontSave };
            dark.SetFloat("_Glossiness", 0f);
            Piece("cap", dark);

            // the spot of sunlight, a quad on the face round the hole
            spotLive = new Texture2D(SpotNX, SpotNY, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            spotPx = new Color32[SpotNX * SpotNY];
            float x0 = SpotX0 / 1000f, x1 = SpotX1 / 1000f, y0 = SpotY0 / 1000f, y1 = SpotY1 / 1000f, z = -(VaneFrontMm + 0.05f) / 1000f;
            var quad = new Mesh { name = "astrolabe sun spot", hideFlags = HideFlags.HideAndDontSave };
            quad.vertices = new[] { new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y1, z), new Vector3(x0, y1, z) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            var spotGo = new GameObject("sun spot") { layer = layer };
            spotGo.transform.SetParent(vaneRig.transform, false);
            spotGo.AddComponent<MeshFilter>().sharedMesh = quad;
            var smr = spotGo.AddComponent<MeshRenderer>();
            Shader spr = Shader.Find("Sprites/Default");
            smr.sharedMaterial = new Material(spr) { name = "astrolabe sun spot", mainTexture = spotLive, hideFlags = HideFlags.HideAndDontSave };
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.receiveShadows = false;

            // what falls in the hole goes on to the eye: a dark disc over the hole's mouth, drawn after
            // the spot, its edge a true circle (cutting the hole out of the coarse spot texture left steps)
            const int seg = 96;
            var dv = new Vector3[seg + 1]; var dt = new int[seg * 3];
            float pr = PinR / 1000f, pz = z - 0.00002f;
            dv[0] = new Vector3(0f, 0f, pz);
            for (int i = 0; i < seg; i++)
            {
                float ang = 2f * Mathf.PI * i / seg;
                dv[i + 1] = new Vector3(pr * Mathf.Cos(ang), pr * Mathf.Sin(ang), pz);
                dt[i * 3] = 0; dt[i * 3 + 1] = 1 + (i + 1) % seg; dt[i * 3 + 2] = 1 + i;
            }
            var disc = new Mesh { name = "astrolabe hole mouth", vertices = dv, triangles = dt, hideFlags = HideFlags.HideAndDontSave };
            disc.RecalculateNormals(); disc.RecalculateBounds();
            var mouth = new GameObject("hole mouth") { layer = layer };
            mouth.transform.SetParent(vaneRig.transform, false);
            mouth.AddComponent<MeshFilter>().sharedMesh = disc;
            var mmr = mouth.AddComponent<MeshRenderer>();
            mmr.sharedMaterial = new Material(spr) { name = "astrolabe hole mouth", color = new Color(0.025f, 0.018f, 0.012f, 1f), renderQueue = 3010, hideFlags = HideFlags.HideAndDontSave };
            mmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mmr.receiveShadows = false;

            // a little light of its own on its layer, from the upper left, as daylight off the sky
            if (layer != 0)
            {
                var lg = new GameObject("vane light");
                lg.transform.SetParent(vaneRig.transform, false);
                vaneLight = lg.AddComponent<Light>();
                vaneLight.type = LightType.Directional;
                vaneLight.color = new Color(1f, 0.95f, 0.86f);
                vaneLight.intensity = 0.45f;                   // the Sun at your back does most of it now
                vaneLight.shadows = LightShadows.None;
                vaneLight.cullingMask = 1 << layer;
            }
            Debug.Log("RealisticSkies: astrolabe vane view on layer " + layer);
            return true;
        }

        /// <summary>The spot (centre in mm on the vane, from the hole) redrawn: bright core, soft edge, a faint glow; none in the hole.</summary>
        private static void DrawSpot(float sx, float sy)
        {
            if (spotLive == null) return;
            float blur = VaneGap * Mathf.Tan(SunSemi * Mathf.Deg2Rad);
            float core = PinR - blur, outer = PinR + blur;
            for (int j = 0; j < SpotNY; j++)
            {
                float y = Mathf.Lerp(SpotY0, SpotY1, (j + 0.5f) / SpotNY);
                for (int i = 0; i < SpotNX; i++)
                {
                    float x = Mathf.Lerp(SpotX0, SpotX1, (i + 0.5f) / SpotNX);
                    float dx = x - sx, dy = y - sy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float I = d <= core ? 1f : Mathf.SmoothStep(1f, 0f, (d - core) / (outer - core));
                    float glow = Mathf.Exp(-(d * d) / (outer * outer * 2.2f)) * 0.18f;
                    float a = Mathf.Max(I, glow);
                    Color c = Color.Lerp(new Color(1f, 0.82f, 0.48f), new Color(1f, 0.98f, 0.88f), I);
                    spotPx[j * SpotNX + i] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(Mathf.Clamp01(a) * 255f));
                }
            }
            spotLive.SetPixels32(spotPx);
            spotLive.Apply(false);
        }

        private static Dictionary<string, Mesh> ReadParts(byte[] bin, string prefix)
        {
            var found = new Dictionary<string, Mesh>();
            using (var r = new System.IO.BinaryReader(new System.IO.MemoryStream(bin)))
            {
                if (new string(r.ReadChars(4)) != "ASTR") throw new Exception("bad model file");
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
                    var mesh = new Mesh { name = prefix + name, hideFlags = HideFlags.HideAndDontSave };
                    mesh.vertices = v; mesh.normals = nn; mesh.uv = uv;
                    mesh.triangles = tris;
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    found[name] = mesh;
                }
            }
            return found;
        }

        /// <summary>The spot of sunlight: full inside the core, falling off to nothing at its edge.</summary>
        private static Texture2D SpotTexture(float core)
        {
            if (spotTex != null) return spotTex;
            return Radial(ref spotTex, r => r <= core ? 1f : Mathf.SmoothStep(1f, 0f, (r - core) / (1f - core)));
        }

        private static Texture2D Radial(ref Texture2D tex, Func<float, float> alpha)
        {
            if (tex != null) return tex;
            const int N = 128;
            tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(Mathf.Sqrt(dx * dx + dy * dy))) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // a round disc with a soft edge, and a soft glow falling off from the middle
        private static Texture2D DiscTexture() => Radial(ref discTex, r => (1f - r) / 0.06f);
        private static Texture2D GlowTexture() => Radial(ref glowTex, r => r >= 1f ? 0f : Mathf.Pow(1f - r, 2f));

        /// <summary>The vane's face: brass, worn and faintly scratched, darker toward its edges.</summary>
        private static Texture2D VaneTexture()
        {
            if (vaneTex != null) return vaneTex;
            const int N = 256;
            vaneTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[N * N];
            var rnd = new System.Random(713);
            float[] streak = new float[N];
            for (int i = 0; i < N; i++) streak[i] = (float)rnd.NextDouble();
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float lit = Mathf.Lerp(1.1f, 0.8f, Mathf.SmoothStep(0.3f, 1.4f, r));
                    float s = 0.94f + 0.06f * streak[(x + y / 7) % N] + 0.04f * (float)(rnd.NextDouble() - 0.5);
                    float k = lit * s;
                    px[y * N + x] = new Color32((byte)Mathf.Clamp(150f * k, 0f, 255f), (byte)Mathf.Clamp(112f * k, 0f, 255f),
                                                (byte)Mathf.Clamp(58f * k, 0f, 255f), 255);
                }
            }
            vaneTex.SetPixels32(px);
            vaneTex.Apply();
            return vaneTex;
        }

        // ---------------- model ----------------
        // Built by docs/make_astrolabe.py: the plate cast flat and pierced, its edges and hollows
        // worn, the scale, the journal's star and little stars engraved; the alidade with pointed
        // ends and pierced vanes, a domed pin head, a round suspension ring.

        private static Material brass;
        private static Dictionary<string, Mesh> parts;

        private static Texture2D Tex(string file, bool linear)
        {
            Texture2D t = ModResources.Texture("Astrolabe/" + file, linear, out bool shared);
            if (t == null) return null;
            if (!shared) t.name = "astrolabe " + file;
            t.anisoLevel = 4;
            return t;
        }

        private static bool LoadModel()
        {
            if (parts != null) return true;
            byte[] bin = ModResources.Read("Astrolabe/astrolabe.bin", out _);
            if (bin == null) return false;
            var found = new Dictionary<string, Mesh>();
            using (var r = new System.IO.BinaryReader(new System.IO.MemoryStream(bin)))
            {
                if (new string(r.ReadChars(4)) != "ASTR") throw new Exception("bad astrolabe.bin");
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
                    var mesh = new Mesh { name = "astrolabe " + name };
                    if (n > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices = v; mesh.normals = nn; mesh.uv = uv;
                    mesh.triangles = tris;
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    found[name] = mesh;
                }
            }
            Shader sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            brass = new Material(sh) { name = "astrolabe brass" };
            Texture2D alb = Tex("albedo.jpg", false), mg = Tex("metalgloss.png", true), nm = Tex("normal.png", true);
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

        private static GameObject Part(string name, Transform parent, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = parts[name];
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
                if (ShopSpots.TryGet(ShopSpots.Astrolabe, island, out ShopSpots.Spot spot))
                {
                    ShopSpots.Apply(clone.transform, shop, spot);
                }
                else
                {
                    clone.transform.localPosition = sp.transform.localPosition + new Vector3(0.25f, 0.02f, 0.15f);
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
                ShopSpots.RegisterSpawner(ShopSpots.Astrolabe, island, clone.transform);
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
                Debug.LogWarning("RealisticSkies: vanilla scroll (prefab 91) not found, no astrolabe");
                return;
            }
            if (dir.directory.Length > PrefabIndex && dir.directory[PrefabIndex] != null)
            {
                Debug.LogWarning("RealisticSkies: prefab " + PrefabIndex + " is already taken, no astrolabe");
                return;
            }
            try
            {
                if (!LoadModel())
                {
                    Debug.LogWarning("RealisticSkies: astrolabe model missing, no astrolabe");
                    return;
                }
                holder = new GameObject("RealisticSkies astrolabe holder");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
                GameObject go = UnityEngine.Object.Instantiate(dir.directory[SourceIndex], holder.transform);
                go.name = PrefabIndex + " " + ItemName;

                ShipItemScroll old = go.GetComponent<ShipItemScroll>();
                ModItemAstrolabe item = go.AddComponent<ModItemAstrolabe>();
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
                box.center = new Vector3(0f, 0.035f, -0.0125f);
                box.size = new Vector3(2f * R + 0.004f, 0.27f, 0.035f);

                go.GetComponent<MeshFilter>().sharedMesh = parts["plate"];
                go.GetComponent<MeshRenderer>().sharedMaterial = brass;
                GameObject al = Part("alidade", go.transform, go.layer);
                Part("cap", al.transform, go.layer);
                Part("ring", go.transform, go.layer);
                al.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

                item.name = ItemName;
                item.amount = 45f;
                item.health = 0f;
                item.holdDistance = 0.45f;
                item.inventoryRotationX = 0f;
                item.inventoryRotation = 0f;
                item.value = 90;
                if (Patches.itemPrefabs.TryGetValue(703, out GameObject chart) && chart != null
                    && chart.GetComponent<ShipItem>() is ShipItem chartItem && chartItem.value > 0)
                {
                    item.value = Mathf.RoundToInt(chartItem.value * 0.35f);     // about 90 crowns
                }
                item.mass = 2.5f;

                // hangs on a lamp hook by its shackle ring, plumb
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
                Debug.Log("RealisticSkies: added the astrolabe as prefab " + PrefabIndex);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not build the astrolabe: " + e);
            }
        }

        /// <summary>Where the shackle's ring meets the hook, in the astrolabe's own space.</summary>
        internal static readonly Vector3 HangPoint = new Vector3(0f, R + 0.051f + 0.0155f - 0.0032f, 0f);
    }
}

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Is the Sun shining on a sun instrument? Shared by the ring dial and the astrolabe's Sun sight.
    /// Two parts: the sky's veil (cloud and rain, from the weather), and the game's own shadow at the
    /// instrument (a roof, an arch, a sail, your own head), from a <see cref="SunProbe"/>.
    /// </summary>
    internal static class SunExposure
    {
        private static readonly FieldInfo finalParticlesInfo = AccessTools.Field(typeof(Weather), "finalParticles");

        // the sky's veil over the Sun: 0 clear, 1 the disc hidden
        private const float VeilCloudFrom = 0.5f, VeilCloudTo = 0.95f;   // the game's own shadows still fall below 0.5
        private const float VeilRainFrom = 0.05f, VeilRainTo = 0.5f;

        internal static float LastCloud { get; private set; } = -1f;
        internal static float LastRain { get; private set; } = -1f;
        private static float veilNext, veilLast;

        /// <summary>
        /// How much cloud and rain hide the Sun: 0 clear, 1 overcast. Through thin cloud the Sun is a
        /// blurred disc, so a spot of its light grows faint and soft; behind thick cloud or in rain it is gone.
        /// </summary>
        internal static float Veil()
        {
            // the weather changes slowly: read it (by reflection) four times a second, not every frame
            if (Time.unscaledTime < veilNext) return veilLast;
            veilNext = Time.unscaledTime + 0.25f;
            try
            {
                if (finalParticlesInfo == null || Weather.instance == null) return veilLast = 0f;
                var p = (WeatherParticlesSettings)finalParticlesInfo.GetValue(Weather.instance);
                LastCloud = p.cloudDensity;
                LastRain = p.rainDensity;
                float c = Mathf.Clamp01((p.cloudDensity - VeilCloudFrom) / (VeilCloudTo - VeilCloudFrom));
                float r = Mathf.Clamp01((p.rainDensity - VeilRainFrom) / (VeilRainTo - VeilRainFrom));
                float v = Mathf.Max(c, r);
                return veilLast = v * v * (3f - 2f * v);
            }
            catch (Exception) { return veilLast = 0f; }
        }

        /// <summary>The Sun's direction in the world, or false if there is no Sun yet.</summary>
        internal static bool SunDir(out Vector3 sun)
        {
            sun = Vector3.down;
            if (Sun.sun == null || Sun.sun.transform.parent == null) return false;
            sun = -Sun.sun.transform.parent.forward;
            return true;
        }
    }

    /// <summary>
    /// Is a place in the game's own shadow? Two specks of white turned to the Sun: one takes the
    /// game's shadows, the other doesn't. Now and then a tiny camera draws them (they are shown only
    /// for that moment, never to the player) and the picture is read back without stalling the frame.
    /// The first darker than the second: something casts its shadow there, the same shadow the game
    /// draws on the deck.
    ///
    /// The game's sky is bright: in shade the first still keeps about 0.6 of the second. Two thresholds
    /// with a gap between them, and a few readings that must agree before the answer turns, so a stray
    /// one (the shadow map isn't drawn the same every time for so small a view) changes nothing.
    /// </summary>
    internal class SunProbe
    {
        private const float ShadeIn = 0.75f, ShadeOut = 0.88f;

        private readonly string name;
        private GameObject probe;
        private Camera cam;
        private RenderTexture rt;
        private Renderer quadA, quadB;
        private float next;
        private bool pending, alive = true;
        private bool shaded;
        private int votes;
        private static Material mat;

        // a burst: a few quick looks for one answer (the reading of an instrument)
        private readonly float[] burst = new float[3];
        private int burstCount = -1;
        private Action<bool> burstDone;

        /// <summary>1 in the sun, 0 in the shade (steady: see the class notes).</summary>
        internal float Lit { get; private set; } = 1f;
        /// <summary>The last reading: the shadowed speck's light over the unshadowed one's; -1 none yet.</summary>
        internal float Ratio { get; private set; } = -1f;
        internal bool Bursting => burstCount >= 0;

        internal static bool Works => SystemInfo.supportsAsyncGPUReadback;

        internal SunProbe(string name) { this.name = name; }

        private Renderer Quad(string n, float x, bool receive)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = n;
            q.layer = probe.layer;
            q.transform.SetParent(probe.transform, false);
            q.transform.localPosition = new Vector3(x, 0f, 0f);
            q.transform.localScale = Vector3.one * 0.012f;
            Renderer r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = receive;
            r.enabled = false;
            return r;
        }

        private void Build(int layer)
        {
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse")) { name = "RealisticSkies sun probe", color = Color.white };
                mat.SetFloat("_Glossiness", 0f);
                mat.SetFloat("_Metallic", 0f);
            }
            probe = new GameObject("RealisticSkies " + name + " sun probe") { layer = layer };
            quadA = Quad("shadowed", -0.008f, true);
            quadB = Quad("unshadowed", 0.008f, false);
            rt = new RenderTexture(8, 4, 16, RenderTextureFormat.ARGB32) { name = name + " sun probe" };
            var cgo = new GameObject("probe camera");
            cgo.transform.SetParent(probe.transform, false);
            cgo.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            cam = cgo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = 0.006f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 0.08f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = ~0;                  // the shadow casters come in by the mask too
            cam.renderingPath = RenderingPath.Forward;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.targetTexture = rt;
        }

        /// <summary>
        /// Call every frame while the answer is wanted: looks at most every interval seconds (at once
        /// while a burst is on). at: the place, a few centimetres clear of the instrument itself.
        /// </summary>
        internal void Tick(Vector3 at, Vector3 sunW, int layer, float interval)
        {
            if (!Works || !alive) return;
            if (probe == null) Build(layer);
            if (pending || (!Bursting && Time.unscaledTime < next)) return;
            next = Time.unscaledTime + interval;
            // the quads' faces (their -z) and the camera turned to the Sun
            probe.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-sunW, Mathf.Abs(sunW.y) < 0.99f ? Vector3.up : Vector3.forward));
            quadA.enabled = quadB.enabled = true;
            try { cam.Render(); }
            finally { quadA.enabled = quadB.enabled = false; }
            pending = true;
            UnityEngine.Rendering.AsyncGPUReadback.Request(rt, 0, OnRead);
        }

        /// <summary>Takes the next three looks (one a frame, as Tick is called) and tells whether most were in shade.</summary>
        internal void Burst(Action<bool> done)
        {
            burstCount = 0;
            burstDone = done;
        }

        internal void CancelBurst() { burstCount = -1; burstDone = null; }

        private void OnRead(UnityEngine.Rendering.AsyncGPUReadbackRequest req)
        {
            pending = false;
            if (!alive || req.hasError) return;
            var data = req.GetData<Color32>();
            float a = 0f, b = 0f;
            for (int y = 0; y < 4; y++)
            {
                // the middle columns of each half, clear of the edges
                for (int x = 1; x < 3; x++) { Color32 c = data[y * 8 + x]; a += c.r + c.g + c.b; }
                for (int x = 5; x < 7; x++) { Color32 c = data[y * 8 + x]; b += c.r + c.g + c.b; }
            }
            if (b < 8f) return;                         // nothing drawn this time: keep what we had
            Ratio = a / b;
            if (Bursting)
            {
                burst[burstCount++] = Ratio;
                if (burstCount < burst.Length) return;
                int dark = 0;
                foreach (float r in burst) if (r < (ShadeIn + ShadeOut) / 2f) dark++;
                Action<bool> done = burstDone;
                CancelBurst();
                done?.Invoke(dark * 2 > burst.Length);
                return;
            }
            bool want = shaded ? Ratio <= ShadeOut : Ratio < ShadeIn;
            votes = want == shaded ? 0 : votes + 1;
            if (votes >= (shaded ? 3 : 2))
            {
                shaded = want;
                votes = 0;
            }
            Lit = shaded ? 0f : 1f;
        }

        internal void Dispose()
        {
            alive = false;
            CancelBurst();
            if (probe != null) UnityEngine.Object.Destroy(probe);
            if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
            probe = null; rt = null;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Training aid ("Star Labels" in the config): look at a bright star or a planet at night and its
    /// name fades in next to it, with SHA / Dec as listed in the Almanac. No instrument needed.
    /// </summary>
    public class StarLabels : MonoBehaviour
    {
        private const float PickAngle = 2.5f;     // degrees from the centre of view
        private const float Distance = 100f;      // how far in front of the eye the label is drawn
        private const float OffsetAngle = 1.2f;   // label sits this far to the right of the body

        private const float LineGap = 0.6f;       // degrees left open around each star
        private const float LineWidth = 0.2f;     // at Distance, about 0.1 degree

        private TextMesh label;
        private Camera cam;
        private float alpha;
        private string current = "";

        // constellation figure of the star being looked at
        private string figure;
        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private Material lineMat;

        private struct Named
        {
            public int number;
            public Func<Planet> planet;
        }

        // almanac numbering: the home planet is skipped, so planet4 is "Planet 3" and so on
        private static readonly Named[] planets =
        {
            new Named { number = 1, planet = () => PlanetPatch.planet1 },
            new Named { number = 2, planet = () => PlanetPatch.planet2 },
            new Named { number = 3, planet = () => PlanetPatch.planet4 },
            new Named { number = 4, planet = () => PlanetPatch.planet5 },
            new Named { number = 5, planet = () => PlanetPatch.planet6 },
            new Named { number = 6, planet = () => PlanetPatch.planet7 },
            new Named { number = 7, planet = () => PlanetPatch.planet8 },
        };

        private void Start()
        {
            var go = new GameObject("RealisticSkies star label");
            go.transform.SetParent(base.transform, false);
            this.label = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            this.label.font = font;
            this.label.fontSize = 40;
            this.label.characterSize = 0.4f * Distance / 100f;
            this.label.anchor = TextAnchor.MiddleLeft;
            this.label.alignment = TextAlignment.Left;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.material = font.material;
            renderer.material.renderQueue = 3002;
            go.SetActive(false);

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                this.lineMat = new Material(shader) { renderQueue = 3001 };
            }
        }

        private void LateUpdate()
        {
            if (this.label == null)
            {
                return;
            }

            string target = "";
            string targetFigure = null;
            Vector3 targetDir = Vector3.zero;

            if (Plugin.configStarLabels.Value && !Plugin.configDisable.Value && GameState.playing &&
                Sun.sun != null && Celestial.HaveSkyAxes && this.FindCamera())
            {
                // only once stars are actually showing
                if (Sun.sun.GetNightLerp() > 0.3f)
                {
                    this.Pick(out target, out targetDir, out targetFigure);
                }
            }

            // fade in / out
            if (target.Length > 0)
            {
                if (target != this.current)
                {
                    this.current = target;
                    this.label.text = target;
                    this.alpha = 0f;
                }
                this.figure = Plugin.configConstellationLines.Value ? targetFigure : null;
                this.alpha = Mathf.MoveTowards(this.alpha, 0.65f, Time.unscaledDeltaTime * 2f);
                Transform c = this.cam.transform;
                Vector3 side = Quaternion.AngleAxis(OffsetAngle, c.up) * targetDir;
                this.label.transform.position = c.position + side * Distance;
                this.label.transform.rotation = Quaternion.LookRotation(side, c.up);
                this.label.transform.localScale = Vector3.one * this.FovScale();
            }
            else
            {
                this.alpha = Mathf.MoveTowards(this.alpha, 0f, Time.unscaledDeltaTime * 3f);
                if (this.alpha <= 0f)
                {
                    this.current = "";
                }
            }

            this.label.gameObject.SetActive(this.alpha > 0f);
            this.label.color = new Color(1f, 1f, 1f, this.alpha);
            this.DrawFigure();
        }

        /// <summary>Draw the stick figure of the current constellation on the sky, fading with the label.</summary>
        private void DrawFigure()
        {
            int used = 0;
            if (this.figure != null && this.alpha > 0f && this.lineMat != null && this.cam != null &&
                ConstellationLines.Edges.TryGetValue(this.figure, out string[] edges))
            {
                int day = GameState.day;
                double hour = Sun.sun.globalTime;
                double aries = Lunar.AriesGHA(day, hour);
                Vector3 eye = this.cam.transform.position;
                Color color = new Color(0.68f, 0.87f, 0.54f, this.alpha * 0.9f);
                for (int i = 0; i + 1 < edges.Length; i += 2)
                {
                    if (!ConstellationLines.TryGetStar(edges[i], out AtlasStars.AtlasStar a) ||
                        !ConstellationLines.TryGetStar(edges[i + 1], out AtlasStars.AtlasStar b))
                    {
                        continue;
                    }
                    Vector3 da = Lunar.WorldDirection(Celestial.Repeat(aries + a.SHA, 360.0), a.dec, hour);
                    Vector3 db = Lunar.WorldDirection(Celestial.Repeat(aries + b.SHA, 360.0), b.dec, hour);
                    if (da.y < 0f && db.y < 0f) continue;   // whole segment below the horizon
                    float angle = Vector3.Angle(da, db);
                    if (angle <= 2f * LineGap) continue;
                    // leave a small gap at each star, as on the charts
                    Vector3 pa = Vector3.Slerp(da, db, LineGap / angle);
                    Vector3 pb = Vector3.Slerp(da, db, 1f - LineGap / angle);
                    LineRenderer lr = this.Line(used++);
                    lr.SetPosition(0, eye + pa * Distance);
                    lr.SetPosition(1, eye + pb * Distance);
                    lr.startColor = lr.endColor = color;
                    lr.startWidth = lr.endWidth = LineWidth * this.FovScale();
                }
            }
            for (int i = 0; i < this.lines.Count; i++)
            {
                this.lines[i].gameObject.SetActive(i < used);
            }
        }

        private LineRenderer Line(int index)
        {
            while (this.lines.Count <= index)
            {
                var go = new GameObject("RealisticSkies constellation line");
                go.transform.SetParent(base.transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = this.lineMat;
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.startWidth = lr.endWidth = LineWidth;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                this.lines.Add(lr);
            }
            return this.lines[index];
        }

        /// <summary>
        /// The names and lines are things in the world at a fixed distance, sized for the game's own
        /// field of view (76 degrees). With a wider view everything looks smaller, so they are made
        /// larger by as much, to stay the same size on the screen.
        /// </summary>
        private float FovScale()
        {
            Camera main = Camera.main != null ? Camera.main : this.cam;
            if (main == null) return 1f;
            float k = Mathf.Tan(main.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(38f * Mathf.Deg2Rad);
            return Mathf.Clamp(k, 0.5f, 3f);
        }

        private bool FindCamera()
        {
            if (this.cam == null)
            {
                GameObject outline = GameObject.Find("Outline Camera");
                this.cam = outline != null ? outline.GetComponent<Camera>() : Camera.main;
            }
            return this.cam != null;
        }

        private void Pick(out string text, out Vector3 dir, out string figure)
        {
            text = "";
            dir = Vector3.zero;
            figure = null;
            Vector3 view = this.cam.transform.forward;
            // score = angle / how far off a star of that brightness may be and still be "looked at":
            // bright stars are easy to point at, faint ones need a closer look
            float best = 1f;
            int day = GameState.day;
            double hour = Sun.sun.globalTime;
            double aries = Lunar.AriesGHA(day, hour);

            foreach (AtlasStars.AtlasStar s in AtlasStars.Stars)
            {
                float allowed = s.mag <= 2.5f ? PickAngle : s.mag <= 4f ? 1.5f : 0.8f;
                double gha = Celestial.Repeat(aries + s.SHA, 360.0);
                Vector3 d = Lunar.WorldDirection(gha, s.dec, hour);
                if (d.y <= 0f) continue;
                float score = Vector3.Angle(view, d) / allowed;
                if (score < best)
                {
                    best = score;
                    dir = d;
                    text = Describe(s);
                    AtlasStars.Constellation.TryGetValue(s.code, out figure);
                }
            }
            if (Plugin.configPlanetsToggle.Value && SkyboxManager.BaseSky != null)
            {
                Vector3 centre = SkyboxManager.BaseSky.transform.position;
                foreach (Named p in planets)
                {
                    GameObject sprite = p.planet().sprite;
                    if (sprite == null || !sprite.activeInHierarchy) continue;   // too faint to see
                    Vector3 d = (sprite.transform.position - centre).normalized;
                    if (d.y <= 0f) continue;
                    float score = Vector3.Angle(view, d) / PickAngle;
                    if (score < best)
                    {
                        best = score;
                        dir = d;
                        text = PlanetNames.Get(p.number);
                        figure = null;
                    }
                }
            }
        }

        private static string Describe(AtlasStars.AtlasStar s)
        {
            string title;
            if (s.letter != null)
            {
                string shown = Lunar.DisplayName(s.letter);
                title = shown == s.letter ? $"{s.letter}  {s.code}" : $"{shown} ({s.letter})  {s.code}";
            }
            else if (s.name != null)
            {
                title = $"{s.name}  {s.code}";
            }
            else
            {
                title = s.code;
            }

            // Almanac stars carry the Almanac's exact figures; the rest are read off the chart, so
            // whole arc minutes is as precise as it is honest to show
            bool exact = s.letter != null || (s.name != null && Lunar.Stars.Exists(st => st.letter == s.name));
            string text = exact
                ? $"{title}\nSHA{Celestial.Format(s.SHA)}  Dec{Celestial.Format(s.dec)}"
                : $"{title}\nSHA {Whole(s.SHA)}  Dec {Whole(s.dec)}";

            if (AtlasStars.Constellation.TryGetValue(s.code, out string constellation))
            {
                text += "\n" + constellation;
            }
            return text;
        }

        private static string Whole(double deg)
        {
            int total = (int)Math.Round(Math.Abs(deg) * 60.0);
            return (deg < 0 ? "-" : "") + (total / 60) + "°" + (total % 60).ToString("00") + "'";
        }
    }

}

using System;
using System.Globalization;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>How the look text and the journal name the directions on the native boards (config Traverse Board / Point Names).</summary>
    internal enum TraverseNaming { Native, NativeAndEuropean, European }

    /// <summary>
    /// One style of traverse board: its layout (from the generated TraverseBoardLayout* class), its
    /// resource folder, materials, item and region. The same mechanics run on every style; only what
    /// is here differs.
    /// - aestrin (prefab 718): the European board, 32 points, periods from 00:00, sold in Aestrin.
    /// - emerald (prefab 719): the junk's board, 24 directions 15° apart (south drawn at the top, the
    ///   holes already placed so), double hours from 23:00, sold in the Emerald Archipelago.
    /// - alankh (prefab 720): the pilot's star board, 32 points named by the stars' risings and
    ///   settings, periods from 00:00, sold in Al'Ankh.
    /// The boards draw their own script; the look text and the journal show only Latin letters (the
    /// game's font has neither Chinese nor Arabic).
    /// </summary>
    internal sealed class TraverseStyle
    {
        public string Id, Folder, ItemName;
        public int Prefab;
        public PortRegion Region;

        public int Points;
        public float PointStep, PeriodStart;
        public string[] PointNames;                 // the native names, Latin letters (Aestrin: the compass points)
        public string[,] PeriodNames;               // [period, half] (Emerald only), else null
        public bool Native;                         // its names are not the European compass points

        public Vector3[] RoseHoles, GridHoles, RoseAnchors, GridAnchors;
        public const int Cols = 12;
        public float RoseCord, GridCord, AnchorTieZ, PegCentreOfMassZ, PegHeadRadius;
        public Vector3 PegTie, HangPoint;
        public Rect Bounds;
        public Vector2[] Outline;                   // the face for the cords' collision; null: the round arch
        public float ArchRadius, BottomY;

        public string BackSet;                      // the texture set on the back
        public Color HoleTint;
        public float CordGloss;

        public Vector3 RoseHole(int ring, int point) => RoseHoles[ring * Points + point];
        public Vector3 GridHole(int row, int col) => GridHoles[row * Cols + col];

        // ---------------- the three styles ----------------

        internal static readonly TraverseStyle Aestrin = new TraverseStyle
        {
            Id = "aestrin", Folder = "TraverseBoard", ItemName = "traverse board", Prefab = 718, Region = PortRegion.medi,
            Points = TraverseBoardLayout.Points, PointStep = 11.25f, PeriodStart = 0f,
            PointNames = TraverseBoardLayout.PointNames, PeriodNames = null, Native = false,
            RoseHoles = TraverseBoardLayout.RoseHoles, GridHoles = TraverseBoardLayout.GridHoles,
            RoseAnchors = TraverseBoardLayout.RoseAnchors, GridAnchors = TraverseBoardLayout.GridAnchors,
            RoseCord = TraverseBoardLayout.RoseCord, GridCord = TraverseBoardLayout.GridCord, AnchorTieZ = TraverseBoardLayout.AnchorTieZ,
            PegTie = TraverseBoardLayout.PegTie, PegCentreOfMassZ = TraverseBoardLayout.PegCentreOfMassZ, PegHeadRadius = TraverseBoardLayout.PegHeadRadius,
            HangPoint = TraverseBoardLayout.HangPoint, Bounds = TraverseBoardLayout.Bounds,
            Outline = null, ArchRadius = TraverseBoardLayout.ArchRadius, BottomY = TraverseBoardLayout.BottomY,
            BackSet = "wood_", HoleTint = new Color(0.36f, 0.29f, 0.23f), CordGloss = 0.08f,
        };

        internal static readonly TraverseStyle Emerald = new TraverseStyle
        {
            Id = "emerald", Folder = "TraverseBoardEmerald", ItemName = "junk's traverse board", Prefab = 719, Region = PortRegion.emerald,
            Points = TraverseBoardLayoutEmerald.Points, PointStep = TraverseBoardLayoutEmerald.PointStep, PeriodStart = TraverseBoardLayoutEmerald.PeriodStart,
            PointNames = TraverseBoardLayoutEmerald.PointNames, PeriodNames = TraverseBoardLayoutEmerald.PeriodNames, Native = true,
            RoseHoles = TraverseBoardLayoutEmerald.RoseHoles, GridHoles = TraverseBoardLayoutEmerald.GridHoles,
            RoseAnchors = TraverseBoardLayoutEmerald.RoseAnchors, GridAnchors = TraverseBoardLayoutEmerald.GridAnchors,
            RoseCord = TraverseBoardLayoutEmerald.RoseCord, GridCord = TraverseBoardLayoutEmerald.GridCord, AnchorTieZ = TraverseBoardLayoutEmerald.AnchorTieZ,
            PegTie = TraverseBoardLayoutEmerald.PegTie, PegCentreOfMassZ = TraverseBoardLayoutEmerald.PegCentreOfMassZ, PegHeadRadius = TraverseBoardLayoutEmerald.PegHeadRadius,
            HangPoint = TraverseBoardLayoutEmerald.HangPoint, Bounds = TraverseBoardLayoutEmerald.Bounds,
            Outline = TraverseBoardLayoutEmerald.Outline, ArchRadius = 0f, BottomY = TraverseBoardLayoutEmerald.BottomY,
            BackSet = "back_", HoleTint = new Color(0.54f, 0.47f, 0.40f), CordGloss = 0.35f,
        };

        internal static readonly TraverseStyle AlAnkh = new TraverseStyle
        {
            Id = "alankh", Folder = "TraverseBoardAlAnkh", ItemName = "pilot's star board", Prefab = 720, Region = PortRegion.alankh,
            Points = TraverseBoardLayoutAlAnkh.Points, PointStep = TraverseBoardLayoutAlAnkh.PointStep, PeriodStart = TraverseBoardLayoutAlAnkh.PeriodStart,
            PointNames = TraverseBoardLayoutAlAnkh.PointNames, PeriodNames = null, Native = true,
            RoseHoles = TraverseBoardLayoutAlAnkh.RoseHoles, GridHoles = TraverseBoardLayoutAlAnkh.GridHoles,
            RoseAnchors = TraverseBoardLayoutAlAnkh.RoseAnchors, GridAnchors = TraverseBoardLayoutAlAnkh.GridAnchors,
            RoseCord = TraverseBoardLayoutAlAnkh.RoseCord, GridCord = TraverseBoardLayoutAlAnkh.GridCord, AnchorTieZ = TraverseBoardLayoutAlAnkh.AnchorTieZ,
            PegTie = TraverseBoardLayoutAlAnkh.PegTie, PegCentreOfMassZ = TraverseBoardLayoutAlAnkh.PegCentreOfMassZ, PegHeadRadius = TraverseBoardLayoutAlAnkh.PegHeadRadius,
            HangPoint = TraverseBoardLayoutAlAnkh.HangPoint, Bounds = TraverseBoardLayoutAlAnkh.Bounds,
            Outline = TraverseBoardLayoutAlAnkh.Outline, ArchRadius = 0f, BottomY = TraverseBoardLayoutAlAnkh.BottomY,
            BackSet = "back_", HoleTint = new Color(0.60f, 0.50f, 0.44f), CordGloss = 0.35f,
        };

        internal static readonly TraverseStyle[] All = { Aestrin, Emerald, AlAnkh };

        internal static TraverseStyle ByPrefab(int prefab)
        {
            foreach (TraverseStyle s in All) if (s.Prefab == prefab) return s;
            return Aestrin;
        }

        internal static TraverseStyle ById(string id)
        {
            foreach (TraverseStyle s in All) if (s.Id == id) return s;
            return Aestrin;
        }

        internal static bool IsBoardPrefab(int prefab)
        {
            foreach (TraverseStyle s in All) if (s.Prefab == prefab) return true;
            return false;
        }

        /// <summary>The board a region's shops sell.</summary>
        internal static TraverseStyle ForRegion(PortRegion r)
        {
            foreach (TraverseStyle s in All) if (s.Region == r) return s;
            return Aestrin;
        }

        // ---------------- the face, for the cords ----------------

        /// <summary>Is (x, y) on the board's face (else the cords fall to the wall behind)?</summary>
        public bool OnFace(float x, float y)
        {
            if (Outline == null)
            {
                float R = ArchRadius;
                return y <= 0f ? (Mathf.Abs(x) <= R && y >= BottomY) : (x * x + y * y <= R * R);
            }
            bool inside = false;
            Vector2[] o = Outline;
            for (int i = 0, j = o.Length - 1; i < o.Length; j = i++)
            {
                if ((o[i].y > y) != (o[j].y > y) && x < (o[j].x - o[i].x) * (y - o[i].y) / (o[j].y - o[i].y) + o[i].x) inside = !inside;
            }
            return inside;
        }

        // ---------------- time ----------------

        /// <summary>The board (cycle) a moment of local time falls on: its two boards a day run from PeriodStart and PeriodStart + 12.</summary>
        public int CycleAt(double localHours) => (int)Math.Floor((localHours - PeriodStart) / 12.0);

        /// <summary>The hour period j of a board's half (0 the first, 1 the second) begins at, 0..23.</summary>
        public int StartHour(int period, int half)
        {
            int h = (int)Math.Round(PeriodStart) + 2 * period + 12 * half;
            return ((h % 24) + 24) % 24;
        }

        /// <summary>"07–09"</summary>
        public string HoursText(int period, int half)
        {
            int h = StartHour(period, half);
            return h.ToString("00") + "–" + ((h + 2) % 24).ToString("00");
        }

        private static TraverseNaming Naming => Plugin.configPointNames != null ? Plugin.configPointNames.Value : TraverseNaming.NativeAndEuropean;

        /// <summary>The period in the look text: "hours 06–08", or on the junk's board "double hour Chen (07–09)".</summary>
        public string PeriodLook(int period, int half)
        {
            if (PeriodNames != null && Naming != TraverseNaming.European)
                return "double hour " + PeriodNames[period, half] + " (" + HoursText(period, half) + ")";
            return "hours " + HoursText(period, half);
        }

        /// <summary>The period in the journal's Time column: "06–08", or "Chen 07–09".</summary>
        public string PeriodJournal(int period, int half)
        {
            if (PeriodNames != null && Naming != TraverseNaming.European)
                return PeriodNames[period, half] + " " + HoursText(period, half);
            return HoursText(period, half);
        }

        /// <summary>"board of 23–11 (Zi to Si)" for the junk's board; else the morning or afternoon board.</summary>
        public string BoardName(int half)
        {
            if (PeriodNames == null) return half == 0 ? "morning board" : "afternoon board";
            string hours = "board of " + StartHour(0, half).ToString("00") + "–" + StartHour(0, 1 - half).ToString("00");
            return Naming == TraverseNaming.European ? hours : hours + " (" + PeriodNames[0, half] + " to " + PeriodNames[5, half] + ")";
        }

        // ---------------- directions ----------------

        /// <summary>The nearest of the 32 European points to a bearing, and whether it is off it.</summary>
        private static string European(double bearing, out bool approx)
        {
            int p = ((int)Math.Round(bearing / 11.25) % 32 + 32) % 32;
            double off = Math.Abs(bearing - Math.Round(bearing / 11.25) * 11.25);
            approx = off > 0.01;
            return TraverseBoardLayout.PointNames[p];
        }

        /// <summary>A hole's direction, as the Point Names option says: "Chou 30° (≈ NNE)", "rising of Naghma 56¼°", "NNE 22½°".</summary>
        public string PointText(int point)
        {
            double b = point * (double)PointStep;
            string deg = Degrees(b) + "°";
            if (!Native) return PointNames[point] + " " + deg;
            string eur = European(b, out bool approx);
            string eu = (approx ? "≈ " : "") + eur;
            switch (Naming)
            {
                case TraverseNaming.Native: return PointNames[point] + " " + deg;
                case TraverseNaming.European: return eu + " " + deg;
                default: return PointNames[point] + " " + deg + " (" + eu + ")";
            }
        }

        /// <summary>A course made good (any bearing): the nearest direction of this board, in whole degrees.</summary>
        public string CourseText(double bearing)
        {
            string deg = Math.Round(bearing).ToString("0", CultureInfo.InvariantCulture) + "°";
            string eur = European(bearing, out _);
            if (!Native) return eur + " " + deg;
            int p = ((int)Math.Round(bearing / PointStep) % Points + Points) % Points;
            switch (Naming)
            {
                case TraverseNaming.Native: return PointNames[p] + " " + deg;
                case TraverseNaming.European: return eur + " " + deg;
                default: return PointNames[p] + " " + deg + " (" + eur + ")";
            }
        }

        /// <summary>Degrees with quarters as the old tables wrote them: 11¼, 22½, 33¾.</summary>
        internal static string Degrees(double deg)
        {
            double q = Math.Round(deg * 4.0) / 4.0;
            int whole = (int)Math.Floor(q);
            int frac = (int)Math.Round((q - whole) * 4.0);
            return whole.ToString(CultureInfo.InvariantCulture) + (frac == 1 ? "¼" : frac == 2 ? "½" : frac == 3 ? "¾" : "");
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using PsychoticLab;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Dresses one of the mod's townsfolk (the watchmaker) from a look written the way the Sailwind
    /// Player Model mod writes the player's: "gender=1;head=3;hair=2;eyebrow=4;facialhair=18;headgear=2;
    /// torso=8;hips=4;legs=20;c_skin=5;c_hair=4;c_primary=4;c_secondary=3;c_leather=2;c_metal=4".
    /// Make a figure on that mod's Character screen and copy its "Character" line from its config.
    /// The same rules as there: gender 2 is female, anything else male; a variant counts only the parts
    /// that have a mesh and wraps past the end; 0 is none on hair and hat and the last variant elsewhere;
    /// the colors pick from the same lists, and 0 keeps the figure's own color. Unknown names are left out.
    /// Nothing of that mod is needed: this is its scheme, done here.
    /// </summary>
    internal class TownsfolkLook : MonoBehaviour
    {
        internal string look;

        private sealed class Slot
        {
            public readonly string Key;
            public readonly bool AllowsNone;
            public readonly Func<CharacterCustomizer, List<GameObject>> List;
            public readonly Action<CharacterCustomizer, int> Set;

            public Slot(string key, bool none, Func<CharacterCustomizer, List<GameObject>> list, Action<CharacterCustomizer, int> set)
            {
                Key = key;
                AllowsNone = none;
                List = list;
                Set = set;
            }
        }

        private static CharacterObjectGroups G(CharacterCustomizer c) => c.isFemale ? c.female : c.male;

        // the slots after gender, in the Player Model mod's order
        private static readonly Slot[] Slots =
        {
            new Slot("head", false, c => G(c)?.headAllElements, (c, v) => { if (c.isFemale) c.femaleHeadAllElements = v; else c.maleHeadAllElements = v; }),
            new Slot("hair", true, c => c.allGender?.all_Hair, (c, v) => c.allGenderAll_Hair = v),
            new Slot("eyebrow", false, c => G(c)?.eyebrow, (c, v) => { if (c.isFemale) c.femaleEyebrow = v; else c.maleEyebrow = v; }),
            new Slot("facialhair", false, c => c.male?.facialHair, (c, v) => c.maleFacialHair = v),
            new Slot("headgear", true, c => c.allGender?.headCoverings_Base_Hair, (c, v) => c.allGenderHeadCoverings_Base_Hair = v),
            new Slot("torso", false, c => G(c)?.torso, (c, v) => { if (c.isFemale) c.femaleTorso = v; else c.maleTorso = v; }),
            new Slot("hips", false, c => G(c)?.hips, (c, v) => { if (c.isFemale) c.femaleHips = v; else c.maleHips = v; }),
            new Slot("legs", false, c => G(c)?.leg_Right, (c, v) =>
            {
                if (c.isFemale) { c.femaleLeg_Right = v; c.femaleLeg_Left = v; }
                else { c.maleLeg_Right = v; c.maleLeg_Left = v; }
            }),
        };

        private struct ColorSlot
        {
            public string Key;
            public Color[] Palette;
            public Action<Material, Color> Write;
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        private static readonly Color[] Cloth =
        {
            C(0.92f, 0.9f, 0.85f), C(0.9f, 0.84f, 0.68f), C(0.8f, 0.7f, 0.52f), C(0.66f, 0.52f, 0.36f), C(0.45f, 0.32f, 0.22f),
            C(0.28f, 0.2f, 0.14f), C(0.7f, 0.18f, 0.16f), C(0.45f, 0.12f, 0.14f), C(0.85f, 0.45f, 0.15f), C(0.8f, 0.65f, 0.2f),
            C(0.25f, 0.5f, 0.25f), C(0.4f, 0.42f, 0.22f), C(0.18f, 0.48f, 0.48f), C(0.49f, 0.7f, 0.83f), C(0.25f, 0.42f, 0.7f),
            C(0.14f, 0.2f, 0.36f), C(0.42f, 0.25f, 0.5f), C(0.55f, 0.55f, 0.55f), C(0.25f, 0.25f, 0.27f), C(0.1f, 0.1f, 0.11f),
        };
        private static readonly Color[] Leather =
        {
            C(0.62f, 0.45f, 0.3f), C(0.42f, 0.29f, 0.2f), C(0.28f, 0.19f, 0.13f), C(0.48f, 0.22f, 0.16f), C(0.12f, 0.11f, 0.1f),
            C(0.4f, 0.38f, 0.36f), C(0.36f, 0.36f, 0.24f),
        };
        private static readonly Color[] Metal =
        {
            C(0.63f, 0.62f, 0.56f), C(0.45f, 0.46f, 0.48f), C(0.6f, 0.42f, 0.25f), C(0.72f, 0.58f, 0.28f), C(0.85f, 0.68f, 0.25f),
            C(0.78f, 0.8f, 0.82f), C(0.2f, 0.2f, 0.22f),
        };
        private static readonly Color[] Hair =
        {
            C(0.1f, 0.09f, 0.08f), C(0.22f, 0.15f, 0.1f), C(0.36f, 0.24f, 0.15f), C(0.45f, 0.28f, 0.16f), C(0.52f, 0.24f, 0.14f),
            C(0.62f, 0.22f, 0.12f), C(0.75f, 0.4f, 0.18f), C(0.8f, 0.65f, 0.4f), C(0.89f, 0.78f, 0.55f), C(0.9f, 0.86f, 0.75f),
            C(0.58f, 0.58f, 0.58f), C(0.88f, 0.88f, 0.86f),
        };
        private static readonly Color[] Skin =
        {
            C(0.96f, 0.84f, 0.74f), C(1f, 0.8f, 0.68f), C(0.9f, 0.72f, 0.58f), C(0.8f, 0.62f, 0.46f), C(0.72f, 0.55f, 0.4f),
            C(0.58f, 0.4f, 0.28f), C(0.42f, 0.28f, 0.18f), C(0.28f, 0.18f, 0.12f),
        };

        private static Color Scale(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

        private static void Set(Material m, string prop, Color c)
        {
            if (!m.HasProperty(prop)) return;
            Color old = m.GetColor(prop);
            m.SetColor(prop, new Color(c.r, c.g, c.b, old.a));
        }

        private static readonly ColorSlot[] Colors =
        {
            new ColorSlot { Key = "c_skin", Palette = Skin, Write = (m, c) => { Set(m, "_Color_Skin", c); Set(m, "_Color_Scar", new Color(c.r * 0.95f, c.g * 0.72f, c.b * 0.66f)); } },
            new ColorSlot { Key = "c_hair", Palette = Hair, Write = (m, c) =>
            {
                Set(m, "_Color_Hair", c);
                Color skin = m.HasProperty("_Color_Skin") ? m.GetColor("_Color_Skin") : c;
                Set(m, "_Color_Stubble", Color.Lerp(c, skin, 0.5f));
            } },
            new ColorSlot { Key = "c_primary", Palette = Cloth, Write = (m, c) => Set(m, "_Color_Primary", c) },
            new ColorSlot { Key = "c_secondary", Palette = Cloth, Write = (m, c) => Set(m, "_Color_Secondary", c) },
            new ColorSlot { Key = "c_leather", Palette = Leather, Write = (m, c) => { Set(m, "_Color_Leather_Primary", c); Set(m, "_Color_Leather_Secondary", Scale(c, 1.2f)); } },
            new ColorSlot { Key = "c_metal", Palette = Metal, Write = (m, c) =>
            {
                Set(m, "_Color_Metal_Primary", c);
                Set(m, "_Color_Metal_Secondary", Scale(c, 0.8f));
                Set(m, "_Color_Metal_Dark", Scale(c, 0.45f));
            } },
        };

        private IEnumerator Start()
        {
            // after the figure has dressed itself once (its Start builds its lists of parts)
            yield return null;
            CharacterCustomizer c = GetComponentInChildren<CharacterCustomizer>(true);
            if (c == null || string.IsNullOrEmpty(look)) yield break;
            try
            {
                Dress(c, Parse(look));
                Traverse t = Traverse.Create(c);
                t.Method("ClearItems").GetValue();
                t.Method("UpdateModel").GetValue();
            }
            catch (Exception e)
            {
                Exception inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                Debug.LogWarning("RealisticSkies: can't dress the watchmaker: " + inner.Message);
            }
        }

        private static Dictionary<string, int> Parse(string s)
        {
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in s.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                if (int.TryParse(part.Substring(eq + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                {
                    d[part.Substring(0, eq).Trim()] = v;
                }
            }
            return d;
        }

        private static bool Wearable(GameObject go)
        {
            if (go == null) return false;
            SkinnedMeshRenderer r = go.GetComponent<SkinnedMeshRenderer>();
            return r != null && r.sharedMesh != null && r.sharedMesh.vertexCount > 0;
        }

        private static int Count(List<GameObject> list)
        {
            if (list == null) return 0;
            int n = 0;
            foreach (GameObject g in list) if (Wearable(g)) n++;
            return n;
        }

        private static int Normalize(bool none, int count, int value)
        {
            int lo = none ? 0 : 1;
            int span = count + 1 - lo;
            if (span <= 0) return lo;
            int r = (value - lo) % span;
            if (r < 0) r += span;
            return r + lo;
        }

        private static int ToIndex(List<GameObject> list, int value)
        {
            if (value <= 0 || list == null) return -1;
            int n = value;
            for (int i = 0; i < list.Count; i++)
            {
                if (Wearable(list[i]) && --n == 0) return i;
            }
            return -1;
        }

        private static void Dress(CharacterCustomizer c, Dictionary<string, int> v)
        {
            v.TryGetValue("gender", out int gender);
            bool female = gender == 2 && c.female != null && c.female.torso != null && c.female.torso.Count > 0;
            c.isFemale = female;
            foreach (Slot s in Slots)
            {
                List<GameObject> list = s.List(c);
                int count = Count(list);
                if (count <= 0)
                {
                    if (s.AllowsNone) s.Set(c, -1);
                    continue;
                }
                v.TryGetValue(s.Key, out int value);
                s.Set(c, ToIndex(list, Normalize(s.AllowsNone, count, value)));
            }
            PinTheRest(c);
            Sanitize(c);
            Colour(c, v);
        }

        /// <summary>The parts the look doesn't name: no attachments, arms and hands to go with the torso.</summary>
        private static void PinTheRest(CharacterCustomizer c)
        {
            c.allGenderAll_Head_Attachment = -1;
            c.allGenderHeadCoverings_No_FacialHair = -1;
            c.allGenderHeadCoverings_No_Hair = -1;
            c.allGenderChest_Attachment = -1;
            c.allGenderBack_Attachment = -1;
            c.allGenderShoulder_Attachment_Right = -1;
            c.allGenderShoulder_Attachment_Left = -1;
            c.allGenderElbow_Attachment_Right = -1;
            c.allGenderElbow_Attachment_Left = -1;
            c.allGenderHips_Attachment = -1;
            c.allGenderKnee_Attachement_Right = -1;
            c.allGenderKnee_Attachement_Left = -1;
            c.allGenderElf_Ear = -1;
            if (c.isFemale) c.femaleHeadNoElements = -1;
            else c.maleHeadNoElements = -1;
            CharacterObjectGroups g = G(c);
            if (g == null) return;
            int torso = Math.Max(0, c.isFemale ? c.femaleTorso : c.maleTorso);
            int L(List<GameObject> l) => l == null || l.Count == 0 ? -1 : Math.Abs(torso) % l.Count;
            if (c.isFemale)
            {
                c.femaleArm_Upper_Right = L(g.arm_Upper_Right); c.femaleArm_Upper_Left = L(g.arm_Upper_Left);
                c.femaleArm_Lower_Right = L(g.arm_Lower_Right); c.femaleArm_Lower_Left = L(g.arm_Lower_Left);
                c.femaleHand_Right = L(g.hand_Right); c.femaleHand_Left = L(g.hand_Left);
            }
            else
            {
                c.maleArm_Upper_Right = L(g.arm_Upper_Right); c.maleArm_Upper_Left = L(g.arm_Upper_Left);
                c.maleArm_Lower_Right = L(g.arm_Lower_Right); c.maleArm_Lower_Left = L(g.arm_Lower_Left);
                c.maleHand_Right = L(g.hand_Right); c.maleHand_Left = L(g.hand_Left);
            }
        }

        /// <summary>Every index within its list, so the figure can't come out broken.</summary>
        private static void Sanitize(CharacterCustomizer c)
        {
            SanitizeGroup(c, "male", c.male, false);
            SanitizeGroup(c, "female", c.female, false);
            SanitizeGroup(c, "allGender", c.allGender, true);
        }

        private static void SanitizeGroup(CharacterCustomizer c, string prefix, object group, bool none)
        {
            if (group == null) return;
            Type type = group.GetType();
            foreach (FieldInfo f in typeof(CharacterCustomizer).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (f.FieldType != typeof(int) || !f.Name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string rest = f.Name.Substring(prefix.Length);
                if (rest.Length == 0) continue;
                FieldInfo lf = type.GetField(char.ToLowerInvariant(rest[0]) + rest.Substring(1), BindingFlags.Instance | BindingFlags.Public);
                if (lf == null) continue;
                int n = lf.GetValue(group) is List<GameObject> l ? l.Count : 0;
                int was = (int)f.GetValue(c), now = was;
                if (n == 0) { if (none) now = -1; }
                else if (was >= n) now = n - 1;
                else if (was < 0 && !none) now = 0;
                if (now != was) f.SetValue(c, now);
            }
        }

        private static void Colour(CharacterCustomizer c, Dictionary<string, int> v)
        {
            if (c.mat == null) return;
            bool any = false;
            foreach (ColorSlot s in Colors) if (v.TryGetValue(s.Key, out int k) && k > 0 && k <= s.Palette.Length) any = true;
            if (!any) return;
            // his own material: the townsfolk share theirs
            Material m = new Material(c.mat) { name = c.mat.name + " (watchmaker)" };
            foreach (ColorSlot s in Colors)
            {
                if (v.TryGetValue(s.Key, out int k) && k > 0 && k <= s.Palette.Length) s.Write(m, s.Palette[k - 1]);
            }
            c.mat = m;
        }
    }
}

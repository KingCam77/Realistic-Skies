using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// "Lost at Sea" (docs/LOSTATSEA_SPEC.md), step 2: the scenario in the new-game menu.
    ///
    /// The game's choice of a starting region is a carousel: two arrows and one button that carries
    /// the region's name and confirms it. The scenario is a fourth stop of it. On that stop the
    /// confirm button gives way to two buttons side by side, "Magellan" and "Chronometers": pressing
    /// one sets the Difficulty and starts the game. The game itself starts as an Aestrin game
    /// (Happy Bay's region: its currency and start money); <see cref="Armed"/> tells the rest of the
    /// scenario that this new game is ours.
    /// </summary>
    internal static class LostAtSeaMenu
    {
        /// <summary>The carousel stands on the scenario.</summary>
        internal static bool Selected;

        /// <summary>The new game now starting is the scenario (set when an era button starts it; never by a load).</summary>
        internal static bool Armed;

        internal static Difficulty Era = Difficulty.Chronometers;

        private const int AestrinStop = 2, ScenarioStop = 3;

        // drafts: to be approved by the user
        private const string TitleScenario = "choose your era:";
        private const string InfoText =
            "A storm has driven your ship off her\ncourse, somewhere south-west of\nHappy Bay. Find your latitude by the\nSun and the stars, and bring her in.";
        private const string DifficultyText = "Lost at Sea: a navigation lesson";
        private const string ErasText = "Magellan: glasses and the astrolabe, no clocks\nChronometers: a quintant and a pocket watch";

        private static GameObject confirm, deco, magellan, chronometers;
        private static TextMesh title;
        private static string titleVanilla;

        private static void Build(StartMenu menu)
        {
            if (deco != null) return;
            Traverse t = Traverse.Create(menu);
            Transform ui = t.Field("chooseIslandUI").GetValue<GameObject>().transform;
            GameObject decoMedi = t.Field("decoMedi").GetValue<GameObject>();
            confirm = ui.Find("button confirm").gameObject;
            Transform tt = ui.Find("text title");
            if (tt != null) title = tt.GetComponent<TextMesh>();
            if (title != null) titleVanilla = title.text;

            // the side panel: Aestrin's, with our words
            deco = UnityEngine.Object.Instantiate(decoMedi, decoMedi.transform.parent);
            deco.name = "start deco Lost at Sea";
            deco.SetActive(false);
            // no Aestrin trees and well round the buttons: only the panel and its texts
            for (int i = deco.transform.childCount - 1; i >= 0; i--)
            {
                GameObject c = deco.transform.GetChild(i).gameObject;
                if (c.GetComponent<TextMesh>() == null && !c.name.StartsWith("bg")) UnityEngine.Object.Destroy(c);
            }
            foreach (TextMesh tm in deco.GetComponentsInChildren<TextMesh>(true))
            {
                string n = tm.gameObject.name;
                if (n.Contains("ship")) tm.gameObject.SetActive(false);
                else if (n.Contains("(difficulty)")) tm.text = DifficultyText;
                else if (n.Contains("difficulty")) tm.text = ErasText;
                else tm.text = InfoText;
            }

            // the mod's own things at the ends of the scroll, where the regions have their trees: the
            // astrolabe on the left, the hour glass on the right
            try
            {
                Props();
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: Lost at Sea menu props: " + e);
            }

            // two halves of the confirm button
            float half = 0.257f;
            magellan = EraButton("button era Magellan", "Magellan", Difficulty.Magellan, -half);
            chronometers = EraButton("button era Chronometers", "Chronometers", Difficulty.Chronometers, half);
        }

        private const float PropScale = 2.3f;       // the menu's things are drawn larger than life, as its trees are smaller

        private static void Props()
        {
            // the astrolabe, upright, a little turned, its foot on the scroll's left roll
            GameObject a = Prop(ModItemAstrolabe.PrefabIndex, "prop astrolabe");
            if (a != null)
            {
                a.transform.localPosition = new Vector3(-1.06f, 0.24f, 0.16f);
                a.transform.localRotation = Quaternion.Euler(0f, 5f, 0f);       // its face (scale and alidade) to the viewer; a smaller angle turns it toward the middle of the screen
                a.transform.localScale = Vector3.one * PropScale;
            }
            // the hour glass, standing on the right roll, its sand half run
            GameObject g = Prop(708, "prop hour glass");
            if (g != null)
            {
                const float glassScale = 1.3f;      // the hour glass's own size (ModItemHourglass)
                const float size = 1.85f;           // a little smaller than the astrolabe's scale: it is a tall thing
                g.transform.localPosition = new Vector3(1.04f, -0.01f + HourglassMesh.Centre * glassScale * size, 0.22f);
                g.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
                g.transform.localScale = Vector3.one * size;
                Transform sand = g.transform.Find("sand");
                MeshFilter mf = sand != null ? sand.GetComponent<MeshFilter>() : null;
                if (mf != null)
                {
                    var mesh = new Mesh { name = "menu hour glass sand" };
                    HourglassMesh.BuildSand(mesh, 0.55f, false, false, glassScale);
                    mf.sharedMesh = mesh;
                }
            }
        }

        /// <summary>
        /// A thing of the mod to look at, not to use: a copy of its prefab with everything but its
        /// shapes taken off (no item, no physics, no pointer), under the scenario's panel.
        /// </summary>
        private static GameObject Prop(int prefabIndex, string name)
        {
            PrefabsDirectory dir = PrefabsDirectory.instance;
            if (dir == null || dir.directory == null || prefabIndex >= dir.directory.Length || dir.directory[prefabIndex] == null) return null;
            // the panel is switched off while this is built, so nothing of the copy wakes up before it is stripped
            GameObject go = UnityEngine.Object.Instantiate(dir.directory[prefabIndex], deco.transform);
            go.name = name;
            for (int pass = 0; pass < 4; pass++)        // some parts need others: what won't go now goes on a later pass
            {
                foreach (Component c in go.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer) continue;
                    if (pass < 3 && c is Rigidbody) continue;       // joints and colliders first
                    UnityEngine.Object.DestroyImmediate(c);
                }
            }
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = deco.layer;
            foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = true;
            go.SetActive(true);
            return go;
        }

        private static GameObject EraButton(string name, string label, Difficulty era, float dx)
        {
            GameObject b = UnityEngine.Object.Instantiate(confirm, confirm.transform.parent);
            b.name = name;
            b.transform.localPosition = confirm.transform.localPosition + new Vector3(dx, 0f, 0f);
            // the copy's own Start adds a fresh outline
            foreach (cakeslice.Outline o in b.GetComponentsInChildren<cakeslice.Outline>(true)) UnityEngine.Object.DestroyImmediate(o);
            foreach (Transform c in b.transform)
            {
                if (c.GetComponent<StartMenuButton>() != null)
                {
                    Vector3 s = c.localScale;
                    c.localScale = new Vector3(s.x * 0.49f, s.y, s.z);
                    c.gameObject.AddComponent<LostAtSeaEraButton>().era = era;
                }
                TextMesh tm = c.GetComponent<TextMesh>();
                if (tm != null)
                {
                    tm.text = label;
                    c.localScale *= 0.62f;
                }
            }
            b.SetActive(false);
            return b;
        }

        private static void Show(bool scenario, StartMenu menu)
        {
            if (deco == null) return;
            deco.SetActive(scenario);
            if (scenario) Traverse.Create(menu).Field("decoMedi").GetValue<GameObject>().SetActive(false);
            confirm.SetActive(!scenario);
            magellan.SetActive(scenario);
            chronometers.SetActive(scenario);
            if (title != null) title.text = scenario ? TitleScenario : titleVanilla;
        }

        private static IEnumerator ShowAfterFade(StartMenu menu)
        {
            // the game's own fade swaps the region's things 0.3 s in, behind the blackout
            yield return new WaitForSeconds(0.3f);
            yield return null;
            Show(Selected, menu);
            try
            {
                if (Selected) LostAtSeaStart.EnterScene(menu);
                else LostAtSeaStart.LeaveScene();
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: Lost at Sea scene: " + e);
            }
        }

        [HarmonyPatch(typeof(StartMenu), "ChangeRegion")]
        private static class ChangeRegionPatch
        {
            private static bool Prefix(StartMenu __instance, int change, ref int ___currentRegion)
            {
                try
                {
                    Build(__instance);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("RealisticSkies: Lost at Sea menu could not be built: " + e);
                    return true;   // the game's own three regions
                }
                int stop = (Selected ? ScenarioStop : ___currentRegion) + change;
                if (stop > ScenarioStop) stop = 0;
                else if (stop < 0) stop = ScenarioStop;
                Selected = stop == ScenarioStop;
                ___currentRegion = Selected ? AestrinStop : stop;
                __instance.StartCoroutine((IEnumerator)AccessTools.Method(typeof(StartMenu), "FadeToCurrentRegion").Invoke(__instance, null));
                __instance.StartCoroutine(ShowAfterFade(__instance));
                return false;
            }
        }

        [HarmonyPatch(typeof(StartMenuButton), "OnActivate", new Type[0])]
        private static class EraButtonPatch
        {
            private static void Prefix(StartMenuButton __instance)
            {
                LostAtSeaEraButton b = __instance.GetComponent<LostAtSeaEraButton>();
                if (b != null) Era = b.era;
            }
        }

        [HarmonyPatch(typeof(StartMenu), "StartNewGame")]
        private static class StartNewGamePatch
        {
            private static void Prefix(StartMenu __instance)
            {
                Armed = Selected;
                if (!Armed) return;
                // the era goes into this game's save (ModeInSave), not into the mod's settings
                Debug.Log("RealisticSkies: Lost at Sea chosen, era " + Era);
                try
                {
                    LostAtSeaStart.Begin(__instance);
                }
                catch (Exception e)
                {
                    Armed = false;
                    Debug.LogError("RealisticSkies: Lost at Sea could not start, a plain Aestrin game instead: " + e);
                }
            }

            // no region's starter set (its boat and items) for the scenario; and its own date, after
            // whatever else sets the day of a new game (the Climate mod's start season)
            [HarmonyPriority(Priority.Last)]
            private static void Postfix()
            {
                if (!Armed) return;
                GameState.newGameRegion = PortRegion.none;
                LostAtSeaStart.SetDate();
            }
        }

        // a load is never the scenario's start
        [HarmonyPatch(typeof(StartMenu), "LoadGame")]
        private static class LoadGamePatch
        {
            private static void Prefix()
            {
                Armed = false;
                // the ship shown behind the scenario's menu goes home before the save is read
                try { LostAtSeaStart.LeaveScene(); }
                catch (Exception e) { Debug.LogWarning("RealisticSkies: Lost at Sea scene: " + e); }
            }
        }
    }

    /// <summary>Marks one of the two era buttons of the scenario's stop in the new-game menu.</summary>
    internal class LostAtSeaEraButton : MonoBehaviour
    {
        public Difficulty era;
    }
}

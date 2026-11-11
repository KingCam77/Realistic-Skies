using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// "Lost at Sea" (docs/LOSTATSEA_SPEC.md), step 5: the intro and the hints, as parchment cards in
    /// Unity's immediate GUI, in the Navigator's Journal's hand. A card takes the cursor while it is
    /// up and is closed with its button or Escape; cards wait in a queue. The intro is a card on a
    /// dark screen that clears when it is closed.
    ///
    /// The scenario's state lives in the save itself (GameState.modData): that the game is a Lost at
    /// Sea game, its start, and which hints have been shown.
    /// </summary>
    internal static class LostAtSeaNotes
    {
        private const string Key = "RealisticSkies.LostAtSea";

        // ---------------- the texts (drafts for the user) ----------------

        private const string IntroTitle = "Lost at Sea";
        private const string IntroText =
            "The storm is over. It drove your ship before it, and now the coast is gone and you do not know where you are.\n\n" +
            "You sailed from Oasis with a cargo for Happy Bay. Happy Bay lies at <b>35°20' North, 3°35' East</b>. " +
            "The storm has carried you south and west of it.\n\n" +
            "There are two ways to find the port:\n" +
            "   1. Find your latitude, sail north to 35°20', then turn east and keep to that latitude until land rises. This is the old, sure way.\n" +
            "   2. Find both your latitude and your longitude, and steer straight for Happy Bay. The instruments for it are aboard.\n\n" +
            "You have food and water for a week, and a fishing rod.";
        private const string IntroButton = "Wake up";

        /// <summary>The voyage's opening words, for the journal's Handbook.</summary>
        internal static readonly InstrumentHints.Entry Voyage = new InstrumentHints.Entry { title = IntroTitle, text = IntroText };

        private const string FirstTitle = "Your instruments";
        private const string FirstText =
            "Everything you need is aboard. Some instruments hang on hooks below deck, the rest lie on the table on the stern deck.\n\n" +
            "Look at an instrument, a chart or a book and press {0}: a note like this one tells what it is for and how to use it. The key works with the thing in your hand too.\n\n" +
            "The Navigator's Journal is in your pocket: press J to open it. Your instruments write their readings into it.";

        // added in the Chronometers era, where a pocket watch is aboard
        private const string FirstWatch =
            "\n\nYour pocket watch is wound and going, but do not trust it yet: after the storm it may be half an hour out. " +
            "Set it by a lunar distance, and wind it every day.";

        // {0}: the port the game's Recovery brought the player back to (Oasis, unless he has called elsewhere since)
        private const string RescuedText =
            "Fishermen found your ship adrift and brought her back to {0}, and you with her. What the sea left of your cargo is still aboard.\n\n" +
            "Happy Bay lies at 35°20' North, 3°35' East. When you are rested and provisioned, try again.";

        private const string ArrivedTitle = "Happy Bay";
        private const string ArrivedText =
            "You have brought her in. Sell your cargo at the market: it was bought in Oasis and should fetch a good price here.\n\n" +
            "The hints are now switched off. You can turn them on again in the mod's settings (Training: Instrument Hints).";

        // ---------------- state kept in the save ----------------

        private const string ShownKey = "RealisticSkies.HintsShown";
        private static readonly HashSet<string> shown = new HashSet<string>();
        private static string loadedFrom;

        /// <summary>This game is a Lost at Sea game.</summary>
        internal static bool Active => GameState.modData != null && GameState.modData.ContainsKey(Key);

        /// <summary>The scenario is over: the player has landed at Happy Bay.</summary>
        private static bool Done => Active && GameState.modData[Key].Contains("done=1");

        private static void Read()
        {
            string text = null;
            if (GameState.modData != null) GameState.modData.TryGetValue(ShownKey, out text);
            if (text == loadedFrom) return;
            loadedFrom = text;
            shown.Clear();
            if (text == null) return;
            foreach (string k in text.Split(','))
            {
                if (k.Length > 0) shown.Add(k);
            }
        }

        private static void Write()
        {
            if (GameState.modData == null) return;
            loadedFrom = string.Join(",", new List<string>(shown).ToArray());
            GameState.modData[ShownKey] = loadedFrom;
        }

        /// <summary>Marks the new game as the scenario's, turns the hints on and shows the intro.</summary>
        internal static void Begin(Difficulty era, float lat, float lon)
        {
            if (GameState.modData == null) GameState.modData = new Dictionary<string, string>();
            float now = GameState.day + (Sun.sun != null ? Sun.sun.globalTime / 24f : 0f);
            GameState.modData[Key] = string.Format(CultureInfo.InvariantCulture, "era={0};lat={1:F3};lon={2:F3};day={3};calm={4:F2}",
                era, lat, lon, GameState.day, now + LostAtSeaStart.CalmDays);
            calmFrom = null;
            Plugin.configHints.Value = true;
            Plugin.instance.Config.Save();
            cards.Clear();
            cards.Add(new Card { title = IntroTitle, text = IntroText, button = IntroButton, dark = true, width = 900f });
            Hint("first", FirstTitle, string.Format(FirstText, HintKeyName) + (era == Difficulty.Magellan ? "" : FirstWatch));
        }

        private static string calmFrom;
        private static float calmUntil;

        /// <summary>The game day (with its fraction) until which a Lost at Sea game keeps the weather fair; 0 otherwise.</summary>
        internal static float CalmUntil
        {
            get
            {
                string text = Active ? GameState.modData[Key] : "";
                if (text == calmFrom) return calmUntil;
                calmFrom = text;
                calmUntil = 0f;
                if (text.Contains("done=1")) return 0f;
                foreach (string part in text.Split(';'))
                {
                    if (part.StartsWith("calm=")) float.TryParse(part.Substring(5), NumberStyles.Float, CultureInfo.InvariantCulture, out calmUntil);
                }
                return calmUntil;
            }
        }

        private static string HintKeyName => Plugin.configHintKey.Value.MainKey.ToString();

        /// <summary>A hint, shown once per saved game while Training / Instrument Hints is on.</summary>
        internal static void Hint(string key, string title, string text, string actionLabel = null, Action action = null)
        {
            if (!Plugin.configHints.Value || GameState.modData == null) return;
            Read();
            if (shown.Contains(key)) return;
            shown.Add(key);
            Write();
            cards.Add(new Card { title = title, text = text, button = "Close", width = 640f, actionLabel = actionLabel, action = action });
        }

        // what the player holds, and where he stands: looked at a few times a second
        private static GoPointer[] pointers;
        private static InstrumentHints.Entry target;
        private static bool targetHeld;
        private static bool wasRecovering;
        private static float nextLook;
        private static Port happyBay;
        private const float AshoreDistance = 600f;

        private static void Watch()
        {
            if (Time.unscaledTime < nextLook || !GameState.playing) return;
            nextLook = Time.unscaledTime + 0.15f;

            // the thing the Hint Key would tell about: what is looked at, else what is held
            target = null;
            if (Plugin.configHints.Value && cards.Count == 0)
            {
                if (pointers == null || pointers.Length == 0 || pointers[0] == null) pointers = UnityEngine.Object.FindObjectsOfType<GoPointer>();
                foreach (GoPointer p in pointers)
                {
                    if (p == null) continue;
                    InstrumentHints.Entry e = InstrumentHints.Find(p.GetPointedAtItem());
                    targetHeld = false;
                    if (e == null)
                    {
                        // in hand the key still works, but nothing is written on the screen: a line
                        // across the view is in the way of taking a sight
                        e = InstrumentHints.Find(p.GetHeldItem() as ShipItem);
                        targetHeld = e != null;
                    }
                    if (e != null) { target = e; break; }
                }
            }

            // the voyage failed (hunger, thirst, or Recovery asked for in the menu): the game takes the
            // player and the ship to the last port, which the start set to Oasis. A word about it there.
            if (Active && !Done)
            {
                if (GameState.recovering) wasRecovering = true;
                else if (wasRecovering)
                {
                    wasRecovering = false;
                    string port = GameState.lastVisitedPort != null ? GameState.lastVisitedPort.GetPortName() : null;
                    if (string.IsNullOrEmpty(port)) port = "port";
                    cards.Add(new Card { title = port, text = string.Format(RescuedText, port), button = "Close", width = 640f });
                    Debug.Log("RealisticSkies: Lost at Sea: recovered to " + port);
                }
            }
            else wasRecovering = false;

            // the end of the scenario: ashore at Happy Bay
            if (Active && !Done && GameState.currentBoat == null && !PlayerSwimming.swimming && Refs.observerMirror != null)
            {
                if (happyBay == null && Port.ports != null)
                {
                    foreach (Port p in Port.ports)
                    {
                        if (p != null && p.GetPortName() != null && p.GetPortName().IndexOf("Happy Bay", StringComparison.OrdinalIgnoreCase) >= 0) { happyBay = p; break; }
                    }
                }
                if (happyBay != null && Vector3.Distance(happyBay.transform.position, Refs.observerMirror.transform.position) < AshoreDistance)
                {
                    GameState.modData[Key] = GameState.modData[Key] + ";done=1";
                    cards.Add(new Card { title = ArrivedTitle, text = ArrivedText, button = "Close", width = 640f });
                    Plugin.configHints.Value = false;
                    Plugin.instance.Config.Save();
                    Debug.Log("RealisticSkies: Lost at Sea: landed at Happy Bay, the scenario is over");
                }
            }
        }

        // ---------------- the cards ----------------

        private class Card
        {
            public string title, text, button;
            public bool dark;
            public float width;
            public string actionLabel;      // a second button that does something and closes the card
            public Action action;
        }

        private static readonly List<Card> cards = new List<Card>();
        private static bool up;                     // a card has the cursor
        private static float darkness;              // the intro's dark screen, fading out once it is closed
        private static int closedFrame = -100;
        private static bool waitRelease;

        /// <summary>A card is up, or was just closed with a mouse button still down: the game must not take the click.</summary>
        internal static bool Busy
        {
            get
            {
                if (up) return true;
                if (waitRelease && !Input.GetMouseButton(0) && !Input.GetMouseButton(1)) waitRelease = false;
                return waitRelease || Time.frameCount - closedFrame < 3;
            }
        }

        private static bool CanShow =>
            GameState.playing && !GameState.currentlyLoading && !GameState.sleeping && !GameState.recovering && !NavJournal.IsOpen
            && (!GameState.inCursorMenu || up);

        private static void Raise()
        {
            up = true;
            if (cards[0].dark) darkness = 1f;
            Refs.SetPlayerControl(false);
            MouseLook.ToggleMouseLookAndCursor(false);
            // the game's look text ("pick up", the mouse icons) hangs in the world at the thing looked
            // at; the pointer that places it is off while a card is up, so it would drift as the ship moves
            try
            {
                if (pointers == null || pointers.Length == 0 || pointers[0] == null) pointers = UnityEngine.Object.FindObjectsOfType<GoPointer>();
                foreach (GoPointer p in pointers)
                {
                    if (p != null && p.lookUI != null) p.lookUI.ClearText();
                }
            }
            catch (Exception e) { Debug.LogWarning("RealisticSkies: hints: " + e.Message); }
            try { UISoundPlayer.instance.PlayParchmentSound(); } catch (Exception) { }
        }

        private static void Lower()
        {
            up = false;
            closedFrame = Time.frameCount;
            waitRelease = Input.GetMouseButton(0) || Input.GetMouseButton(1);
            Refs.SetPlayerControl(true);
            MouseLook.ToggleMouseLookAndCursor(true);
        }

        private static void CloseCard()
        {
            if (cards.Count > 0) cards.RemoveAt(0);
            if (up) Lower();
        }

        /// <summary>Called every frame by the plugin.</summary>
        internal static void Tick()
        {
            try { Watch(); }
            catch (Exception e) { Debug.LogWarning("RealisticSkies: hints: " + e); nextLook = Time.unscaledTime + 10f; }

            // the Hint Key: the card of the thing looked at or held (as often as asked)
            if (target != null && cards.Count == 0 && CanShow && Plugin.configHints.Value && Plugin.configHintKey.Value.IsDown())
            {
                cards.Add(new Card { title = target.title, text = target.text, button = "Close", width = 700f, actionLabel = target.actionLabel, action = target.action });
                closedFrame = -100;
                target = null;
            }
            if (cards.Count == 0)
            {
                if (up) Lower();
                if (darkness > 0f) darkness = Mathf.Max(0f, darkness - Time.unscaledDeltaTime / 1.5f);
                return;
            }
            if (!up)
            {
                if (darkness > 0f && !cards[0].dark) darkness = Mathf.Max(0f, darkness - Time.unscaledDeltaTime / 1.5f);
                // one card at a time, a breath after the last
                if (CanShow && Time.frameCount - closedFrame > 20 && (darkness <= 0f || cards[0].dark)) Raise();
            }
            else if (!GameState.playing)
            {
                cards.Clear();
                Lower();
            }
        }

        private static GUIStyle sPage, sTitle, sText, sButton, sPrompt, sPromptShade;
        private static Texture2D black;

        private static Texture2D Solid(Color c, Color border, int bw)
        {
            const int size = 16;
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

        // the journal's look (NavJournal.MakeStyles): the Almanac's font, ink on parchment
        private static void MakeStyles()
        {
            if (sPage != null && sPage.normal.background != null && black != null) return;
            Font font = null;
            if (Patches.itemPrefabs.TryGetValue(701, out GameObject almanac) && almanac != null)
            {
                TextMesh tm = almanac.GetComponentInChildren<TextMesh>(true);
                if (tm != null) font = tm.font;
            }
            var ink = new Color(0.20f, 0.13f, 0.07f);
            var red = new Color(0.50f, 0.10f, 0.06f);
            sPage = new GUIStyle { padding = new RectOffset(40, 40, 30, 30) };
            sPage.normal.background = Parchment();
            sPage.border = new RectOffset(12, 12, 12, 12);
            sText = new GUIStyle { font = font, fontSize = 23, wordWrap = true, richText = true };
            sText.normal.textColor = ink;
            sTitle = new GUIStyle(sText) { fontSize = 36, alignment = TextAnchor.MiddleCenter };
            sTitle.normal.textColor = red;
            sButton = new GUIStyle { font = font, fontSize = 22, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(10, 10, 4, 4) };
            sButton.normal.background = Solid(new Color(0.82f, 0.72f, 0.54f), new Color(0.45f, 0.32f, 0.18f), 1);
            sButton.hover.background = Solid(new Color(0.88f, 0.79f, 0.60f), new Color(0.45f, 0.32f, 0.18f), 1);
            sButton.active.background = Solid(new Color(0.72f, 0.60f, 0.42f), new Color(0.45f, 0.32f, 0.18f), 1);
            sButton.normal.textColor = sButton.hover.textColor = sButton.active.textColor = ink;
            sButton.border = new RectOffset(3, 3, 3, 3);
            black = Solid(Color.black, Color.black, 0);
            sPrompt = new GUIStyle { font = font, fontSize = 24, alignment = TextAnchor.MiddleCenter };
            sPrompt.normal.textColor = new Color(0.96f, 0.92f, 0.80f);
            sPromptShade = new GUIStyle(sPrompt);
            sPromptShade.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        }

        /// <summary>Drawn from the plugin's OnGUI.</summary>
        internal static void OnGUI()
        {
            bool prompt = target != null && !targetHeld && !up && cards.Count == 0 && Plugin.configHints.Value && CanShow;
            if (darkness <= 0f && !up && !prompt) return;
            MakeStyles();
            if (prompt)
            {
                // a line under the crosshair: which key tells about the thing looked at
                float s = Screen.height / 1080f;
                Matrix4x4 pm = GUI.matrix;
                GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
                var r = new Rect(0f, 1080f * 0.62f, Screen.width / s, 40f);
                string line = HintKeyName + ": about " + target.title.Substring(0, 1).ToLowerInvariant() + target.title.Substring(1);
                GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), line, sPromptShade);
                GUI.Label(r, line, sPrompt);
                GUI.matrix = pm;
            }
            if (darkness > 0f)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, darkness);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);
                GUI.color = old;
            }
            if (!up || cards.Count == 0) return;
            Card card = cards[0];
            float k = Screen.height / 1080f;
            Matrix4x4 m = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
            float vw = Screen.width / k, w = card.width;
            float inner = w - sPage.padding.horizontal;
            float h = sPage.padding.vertical + sTitle.CalcHeight(new GUIContent(card.title), inner) + 14f
                      + sText.CalcHeight(new GUIContent(card.text), inner) + 22f + 44f;
            GUILayout.BeginArea(new Rect((vw - w) / 2f, (1080f - h) / 2f, w, h), sPage);
            GUILayout.Label(card.title, sTitle);
            GUILayout.Space(14);
            GUILayout.Label(card.text, sText);
            GUILayout.Space(22);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool act = false;
            if (card.actionLabel != null)
            {
                act = GUILayout.Button(card.actionLabel, sButton, GUILayout.Width(280), GUILayout.Height(44));
                GUILayout.Space(16);
            }
            bool close = GUILayout.Button(card.button, sButton, GUILayout.Width(190), GUILayout.Height(44));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.matrix = m;
            if (act)
            {
                try { card.action?.Invoke(); }
                catch (Exception e) { Debug.LogWarning("RealisticSkies: hint button: " + e); }
            }
            if (act || close) CloseCard();
        }

        /// <summary>Escape closes the card instead of opening the game menu. Called from the menu patch below.</summary>
        internal static void Escape()
        {
            if (up) CloseCard();
        }
    }

    /// <summary>While a card is up the game must not act on the keys and clicks meant for it (as with the journal).</summary>
    [HarmonyPatch]
    internal static class LostAtSeaNotesInputPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(InventoryHotkeys), "Update")]
        private static bool Hotkeys() => !LostAtSeaNotes.Busy;

        [HarmonyPrefix, HarmonyPatch(typeof(GoPointer), "LateUpdate")]
        private static bool Pointer() => !LostAtSeaNotes.Busy;

        [HarmonyPrefix, HarmonyPatch(typeof(MouseButtonPointer), "LateUpdate")]
        private static bool MousePointer() => !LostAtSeaNotes.Busy;

        [HarmonyPrefix, HarmonyPatch(typeof(StartMenu), "LateUpdate")]
        private static bool Menu()
        {
            if (!LostAtSeaNotes.Busy) return true;
            if (Input.GetKeyDown(KeyCode.Escape)) LostAtSeaNotes.Escape();
            return false;
        }
    }
}

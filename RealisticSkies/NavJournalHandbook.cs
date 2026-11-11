using System.Collections.Generic;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// The journal's Handbook page: bookmarks down the left, the chosen note on the right. It holds
    /// the lessons (prime meridian time, latitude, longitude, dead reckoning), the card of every
    /// chart and instrument (the same texts the Hint Key shows, <see cref="InstrumentHints"/>), and,
    /// in a Lost at Sea game, the voyage's opening words.
    /// </summary>
    internal static partial class NavJournal
    {
        private const int HandbookTab = 5;
        private const float BookHeight = 430f;

        private static int bookPage;
        private static Vector2 bookListScroll, bookTextScroll;

        private static void DrawHandbook()
        {
            var pages = new List<InstrumentHints.Entry>();
            int lessons = 0;
            if (LostAtSeaNotes.Active)
            {
                pages.Add(LostAtSeaNotes.Voyage);
                lessons++;
            }
            pages.AddRange(InstrumentHints.Topics);
            lessons += InstrumentHints.Topics.Length;
            pages.AddRange(InstrumentHints.Cards());
            bookPage = Mathf.Clamp(bookPage, 0, pages.Count - 1);

            GUILayout.BeginHorizontal();

            bookListScroll = GUILayout.BeginScrollView(bookListScroll, GUILayout.Width(310), GUILayout.Height(BookHeight));
            GUILayout.Label("Lessons", sSmall);
            for (int i = 0; i < pages.Count; i++)
            {
                if (i == lessons)
                {
                    GUILayout.Space(8);
                    GUILayout.Label("Charts and instruments", sSmall);
                }
                if (GUILayout.Button(pages[i].title, i == bookPage ? sTabOn : sList, GUILayout.Height(32)))
                {
                    bookPage = i;
                    bookTextScroll = Vector2.zero;
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(16);

            InstrumentHints.Entry page = pages[bookPage];
            bookTextScroll = GUILayout.BeginScrollView(bookTextScroll, GUILayout.Height(BookHeight));
            GUILayout.Label(page.title, sHead);
            GUILayout.Space(4);
            GUILayout.Label(page.text, sText);
            if (page.actionLabel != null)
            {
                GUILayout.Space(8);
                if (GUILayout.Button(page.actionLabel, sButton, GUILayout.Width(280), GUILayout.Height(40)) && page.action != null) page.action();
            }
            GUILayout.EndScrollView();

            GUILayout.EndHorizontal();
        }
    }
}

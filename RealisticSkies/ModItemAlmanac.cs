using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{
    public class ModItemAlmanac : ShipItem
    {
        private GameObject yearClosed;

        private GameObject yearOpen;

        private GameObject closedEmpty;

        private GameObject openEmpty;

        private Mesh closedMesh;

        private Mesh openMesh;

        private Vector3 closedColSize = new Vector3(0.55f, 0.76f, 0.1f);

        private Vector3 openColSize = new Vector3(1.18f, 0.76f, 0.08f);

        private int page;

        private int pagecount;

        static bool constructed;

        private GameObject dayEntriesLeft;
        private GameObject dayEntriesRight;

        private GameObject hourEntriesLeft;
        private GameObject hourEntriesRight;


        private GameObject dayLeft;
        private GameObject dayRight;

        private GameObject aries1;
        private GameObject aries2;

        private GameObject p1long1;
        private GameObject p1dec1;
        private GameObject p1long2;
        private GameObject p1dec2;
        private GameObject p1mag;

        private GameObject p2long1;
        private GameObject p2dec1;
        private GameObject p2long2;
        private GameObject p2dec2;
        private GameObject p2mag;

        private GameObject p3long1;
        private GameObject p3dec1;
        private GameObject p3long2;
        private GameObject p3dec2;
        private GameObject p3mag;

        private GameObject p4long1;
        private GameObject p4dec1;
        private GameObject p4long2;
        private GameObject p4dec2;
        private GameObject p4mag;

        private GameObject HrTC1;
        private GameObject HrTC2;
        private GameObject HrTC3;
        private GameObject HrTC4;
        private GameObject HrTC5;
        private GameObject HrTC6;
        private GameObject HrTC7;
        private GameObject HrTC8;
        private GameObject HrTC9;
        private GameObject HrTC10;
        private GameObject HrTC11;
        private GameObject HrTC12;
        private GameObject HrTC13;
        private GameObject HrTC14;
        private GameObject HrTC15;

        private Camera playerCam;

        // Lunar distance page: runtime copy of "Entries Left" with every column used for a distance
        private GameObject lunarLeft;
        private TextMesh lunarTitle;
        private TextMesh lunarSun1, lunarSun2, lunarSunHeader, lunarSD;
        private TextMesh[] lunarCol1 = new TextMesh[8];
        private TextMesh[] lunarCol2 = new TextMesh[8];
        private TextMesh[] lunarHeader = new TextMesh[8];
        private string lunarSunHeaderTemplate;
        private string[] lunarHeaderTemplate = new string[8];

        // Sun & Moon tables on the empty part of the right page (copies of the planet columns)
        private TextMesh smSunAHA1, smSunDEC1, smSunAHA2, smSunDEC2, smSunNote;
        private TextMesh smMoonAHA1, smMoonDEC1, smMoonAHA2, smMoonDEC2, smMoonNote;

        private ModItemAlmanac()
        {
            if (!constructed)
            {
                base.value = 4000;
                base.name = "almanac";
                base.category = TransactionCategory.toolsAndSupplies;
                base.inventoryScale = 0.7f;
                base.inventoryRotation = -180f;
                base.furniturePlaceHeight = 0.5f;
                constructed = true;
            }

        }

        public override void OnLoad()
        {
            this.yearClosed = base.transform.GetChild(0).GetChild(1).gameObject;
            this.yearOpen = base.transform.GetChild(1).GetChild(1).gameObject;
            this.closedEmpty = base.transform.GetChild(0).gameObject;
            this.openEmpty = base.transform.GetChild(1).gameObject;

            this.playerCam = GameObject.Find("Outline Camera").GetComponent<Camera>();

            this.closedMesh = base.transform.GetChild(0).GetChild(0).gameObject.GetComponent<MeshFilter>().sharedMesh;
            this.openMesh = base.transform.GetChild(1).GetChild(0).gameObject.GetComponent<MeshFilter>().sharedMesh;
            base.holdDistance = 0.75f;

            this.dayEntriesLeft = base.transform.GetChild(1).GetChild(4).gameObject;
            this.dayEntriesRight = base.transform.GetChild(1).GetChild(5).gameObject;
            this.hourEntriesLeft = base.transform.GetChild(1).GetChild(6).gameObject;
            this.hourEntriesRight = base.transform.GetChild(1).GetChild(7).gameObject;

            this.dayLeft = this.dayEntriesLeft.transform.GetChild(0).gameObject;
            this.dayRight = this.dayEntriesRight.transform.GetChild(0).gameObject;

            this.aries1 = this.dayEntriesLeft.transform.GetChild(3).gameObject;
            this.aries2 = this.dayEntriesLeft.transform.GetChild(4).gameObject;

            this.p1long1 = this.dayEntriesLeft.transform.GetChild(5).GetChild(0).gameObject;
            this.p1dec1 = this.dayEntriesLeft.transform.GetChild(5).GetChild(1).gameObject;
            this.p1long2 = this.dayEntriesLeft.transform.GetChild(5).GetChild(2).gameObject;
            this.p1dec2 = this.dayEntriesLeft.transform.GetChild(5).GetChild(3).gameObject;
            this.p1mag = this.dayEntriesLeft.transform.GetChild(5).GetChild(4).gameObject;

            this.p2long1 = this.dayEntriesLeft.transform.GetChild(6).GetChild(0).gameObject;
            this.p2dec1 = this.dayEntriesLeft.transform.GetChild(6).GetChild(1).gameObject;
            this.p2long2 = this.dayEntriesLeft.transform.GetChild(6).GetChild(2).gameObject;
            this.p2dec2 = this.dayEntriesLeft.transform.GetChild(6).GetChild(3).gameObject;
            this.p2mag = this.dayEntriesLeft.transform.GetChild(6).GetChild(4).gameObject;

            this.p3long1 = this.dayEntriesLeft.transform.GetChild(7).GetChild(0).gameObject;
            this.p3dec1 = this.dayEntriesLeft.transform.GetChild(7).GetChild(1).gameObject;
            this.p3long2 = this.dayEntriesLeft.transform.GetChild(7).GetChild(2).gameObject;
            this.p3dec2 = this.dayEntriesLeft.transform.GetChild(7).GetChild(3).gameObject;
            this.p3mag = this.dayEntriesLeft.transform.GetChild(7).GetChild(4).gameObject;

            this.p4long1 = this.dayEntriesLeft.transform.GetChild(8).GetChild(0).gameObject;
            this.p4dec1 = this.dayEntriesLeft.transform.GetChild(8).GetChild(1).gameObject;
            this.p4long2 = this.dayEntriesLeft.transform.GetChild(8).GetChild(2).gameObject;
            this.p4dec2 = this.dayEntriesLeft.transform.GetChild(8).GetChild(3).gameObject;
            this.p4mag = this.dayEntriesLeft.transform.GetChild(8).GetChild(4).gameObject;

            this.HrTC1  = this.hourEntriesLeft.transform.GetChild(1).GetChild(0).gameObject;
            this.HrTC2  = this.hourEntriesLeft.transform.GetChild(1).GetChild(1).gameObject;
            this.HrTC3  = this.hourEntriesLeft.transform.GetChild(1).GetChild(2).gameObject;
            this.HrTC4  = this.hourEntriesLeft.transform.GetChild(1).GetChild(3).gameObject;
            this.HrTC5  = this.hourEntriesLeft.transform.GetChild(1).GetChild(4).gameObject;
            this.HrTC6  = this.hourEntriesLeft.transform.GetChild(1).GetChild(5).gameObject;
            this.HrTC7  = this.hourEntriesLeft.transform.GetChild(1).GetChild(6).gameObject;
            this.HrTC8  = this.hourEntriesLeft.transform.GetChild(1).GetChild(7).gameObject;
            this.HrTC9  = this.hourEntriesLeft.transform.GetChild(1).GetChild(8).gameObject;
            this.HrTC10 = this.hourEntriesLeft.transform.GetChild(1).GetChild(9).gameObject;

            this.HrTC11 = this.hourEntriesRight.transform.GetChild(1).GetChild(0).gameObject;
            this.HrTC12 = this.hourEntriesRight.transform.GetChild(1).GetChild(1).gameObject;
            this.HrTC13 = this.hourEntriesRight.transform.GetChild(1).GetChild(2).gameObject;
            this.HrTC14 = this.hourEntriesRight.transform.GetChild(1).GetChild(3).gameObject;
            this.HrTC15 = this.hourEntriesRight.transform.GetChild(1).GetChild(4).gameObject;

            this.NamePlanets();
            this.NameStars();
            this.BuildSunMoonPage();
            this.BuildLunarPage();

            base.holdDistance = 0.75f;

            //year is stored in the health value + 1

            if (!base.sold)
            {
                base.health = Mathf.FloorToInt(Patches.year);
            }

            // with the lunar page every pair of days has two pages: planets, then lunar distances
            this.pagecount = this.PagesPerPair * Mathf.CeilToInt((float)Patches.yearLen / 2.0f) + 1;

            //page is stored in the amount value
            // (page, year and page count are set before Open() so a saved open almanac shows its page)
            if (this.amount == 0f)
            {
                this.amount = -1.0f;
            }
            this.page = Mathf.Clamp((int)Mathf.Abs(base.amount), 1, this.pagecount);

            if (this.amount > 0f)
            {
                this.Open();
            }
            else
            {
                this.Close();
            }

            this.yearClosed.GetComponent<TextMesh>().text = $"{base.health}";
            this.yearOpen.GetComponent<TextMesh>().text = $"{base.health}";
        }

        public override void OnScroll(float input)
        {
            if (this.amount > 0f)
            {
                bool flag3 = input < 0f;
                if (flag3)
                {
                    this.turnPage(true);
                }
                else
                {
                    bool flag4 = input > 0f;
                    if (flag4)
                    {
                        this.turnPage(false);
                    }
                }
            }
            else
            {
                base.OnScroll(input);
            }
        }



        public override void OnBuy()
        {
            base.health = Mathf.FloorToInt(Patches.year);
            this.yearClosed.GetComponent<TextMesh>().text = $"{base.health}";
            this.yearOpen.GetComponent<TextMesh>().text = $"{base.health}";
        }

        private void turnPage(bool input)
        {
            if (input)
            {
                if (this.page < this.pagecount)
                {
                    this.page++;
                    UISoundPlayer.instance.PlayParchmentSound();
                    loadPage(this.page);
                }
            }
            else
            {
                if (this.page > 1)
                {
                    this.page--;
                    UISoundPlayer.instance.PlayParchmentSound();
                    loadPage(this.page);
                }
            }
            base.amount = this.page;
        }

        public override void OnPickup()
        {
            base.OnPickup();
            if (base.amount > 0f)
            {
                base.holdDistance = 181.6f * Mathf.Pow(this.playerCam.fieldOfView, -1.38f);
            }
        }

        private void Open()
        {
            base.amount = Mathf.Abs(base.amount);
            this.closedEmpty.SetActive(false);
            this.openEmpty.SetActive(true);

            loadPage(this.page);



            base.holdDistance = 181.6f * Mathf.Pow(this.playerCam.fieldOfView,-1.38f);

            base.GetComponent<MeshFilter>().sharedMesh = this.openMesh;
            base.GetComponent<BoxCollider>().size = this.openColSize;
        }

        private void Close()
        {
            base.amount = -1.0f * Mathf.Abs(base.amount);
            this.closedEmpty.SetActive(true);
            this.openEmpty.SetActive(false);

            base.holdDistance = 0.75f;

            base.GetComponent<MeshFilter>().sharedMesh = this.closedMesh;
            base.GetComponent<BoxCollider>().size = this.closedColSize;
        }

        public override void OnAltActivate()
        {
            if (!this.sold)
            {
                base.OnAltActivate();
                return;
            }
            if (this.amount < 0f)
            {
                this.Open();
                this.heldRotationOffset = 0f;
            }
            else
            {
                this.Close();
            }
            UISoundPlayer.instance.PlayParchmentSound();
        }

        private int PagesPerPair => (this.lunarLeft != null && Celestial.MoonReworkActive) ? 2 : 1;

        private void loadPage(int page)
        {
            if (this.lunarLeft != null)
            {
                this.lunarLeft.SetActive(false);
            }

            if (page == this.pagecount)
            {
                this.dayEntriesLeft.SetActive(false);
                this.dayEntriesRight.SetActive(false);

                this.hourEntriesLeft.SetActive(true);
                this.hourEntriesRight.SetActive(true);

                this.HrTC1.GetComponent<TextMesh>().text = calcAriesHr(0);
                this.HrTC2.GetComponent<TextMesh>().text = calcAriesHr(4);
                this.HrTC3.GetComponent<TextMesh>().text = calcAriesHr(8);
                this.HrTC4.GetComponent<TextMesh>().text = calcAriesHr(12);
                this.HrTC5.GetComponent<TextMesh>().text = calcAriesHr(16);
                this.HrTC6.GetComponent<TextMesh>().text = calcAriesHr(20);
                this.HrTC7.GetComponent<TextMesh>().text = calcAriesHr(24);
                this.HrTC8.GetComponent<TextMesh>().text = calcAriesHr(28);
                this.HrTC9.GetComponent<TextMesh>().text = calcAriesHr(32);
                this.HrTC10.GetComponent<TextMesh>().text = calcAriesHr(36);

                this.HrTC11.GetComponent<TextMesh>().text = calcAriesHr(40);
                this.HrTC12.GetComponent<TextMesh>().text = calcAriesHr(44);
                this.HrTC13.GetComponent<TextMesh>().text = calcAriesHr(48);
                this.HrTC14.GetComponent<TextMesh>().text = calcAriesHr(52);
                this.HrTC15.GetComponent<TextMesh>().text = calcAriesHr(56);
            }
            else
            {
                int perPair = this.PagesPerPair;
                int pair = (page + perPair - 1) / perPair;
                bool lunarPage = perPair == 2 && page % 2 == 0;

                this.dayEntriesLeft.SetActive(!lunarPage);
                this.dayEntriesRight.SetActive(true);

                this.hourEntriesLeft.SetActive(false);
                this.hourEntriesRight.SetActive(false);


                int day1 = 2 * (pair - 1) + (int)this.health * Patches.yearLen;
                int day2 = 2 * (pair - 1) + 1 + (int)this.health * Patches.yearLen;

                this.dayLeft.GetComponent<TextMesh>().text = PairName(day1, day2);
                this.dayRight.GetComponent<TextMesh>().text = PairName(day1, day2);

                this.FillSunMoon(day1, day2);

                if (lunarPage)
                {
                    this.lunarLeft.SetActive(true);
                    this.FillLunarPage(day1, day2);
                    return;
                }

                this.aries1.GetComponent<TextMesh>().text = calcAries(day1);
                this.aries2.GetComponent<TextMesh>().text = calcAries(day2);

                string AHApass = "";
                string DECpass = "";
                string MAGpass = "";

                calcPlanet(day1, PlanetPatch.planet2, out AHApass, out DECpass, out MAGpass);
                this.p1long1.GetComponent<TextMesh>().text = AHApass;
                this.p1dec1.GetComponent<TextMesh>().text = DECpass;
                this.p1mag.GetComponent<TextMesh>().text = MAGpass;
                calcPlanet(day2, PlanetPatch.planet2, out AHApass, out DECpass, out MAGpass);
                this.p1long2.GetComponent<TextMesh>().text = AHApass;
                this.p1dec2.GetComponent<TextMesh>().text = DECpass;

                calcPlanet(day1, PlanetPatch.planet4, out AHApass, out DECpass, out MAGpass);
                this.p2long1.GetComponent<TextMesh>().text = AHApass;
                this.p2dec1.GetComponent<TextMesh>().text = DECpass;
                this.p2mag.GetComponent<TextMesh>().text = MAGpass;
                calcPlanet(day2, PlanetPatch.planet4, out AHApass, out DECpass, out MAGpass);
                this.p2long2.GetComponent<TextMesh>().text = AHApass;
                this.p2dec2.GetComponent<TextMesh>().text = DECpass;

                calcPlanet(day1, PlanetPatch.planet6, out AHApass, out DECpass, out MAGpass);
                this.p3long1.GetComponent<TextMesh>().text = AHApass;
                this.p3dec1.GetComponent<TextMesh>().text = DECpass;
                this.p3mag.GetComponent<TextMesh>().text = MAGpass;
                calcPlanet(day2, PlanetPatch.planet6, out AHApass, out DECpass, out MAGpass);
                this.p3long2.GetComponent<TextMesh>().text = AHApass;
                this.p3dec2.GetComponent<TextMesh>().text = DECpass;

                calcPlanet(day1, PlanetPatch.planet7, out AHApass, out DECpass, out MAGpass);
                this.p4long1.GetComponent<TextMesh>().text = AHApass;
                this.p4dec1.GetComponent<TextMesh>().text = DECpass;
                this.p4mag.GetComponent<TextMesh>().text = MAGpass;
                calcPlanet(day2, PlanetPatch.planet7, out AHApass, out DECpass, out MAGpass);
                this.p4long2.GetComponent<TextMesh>().text = AHApass;
                this.p4dec2.GetComponent<TextMesh>().text = DECpass;
            }
        }

        private string calcAries(int day)
        {
            string val = "\n";

            for (int hr = 0; hr < 24; hr++)
            {
                float siderialTime = 0.0f;
                if (Plugin.configSiderialToggle.Value)
                {
                    siderialTime = (float)hr + ((float)day * 24 + (float)hr) / (float)Patches.yearLen;
                }
                else
                {
                    siderialTime = (float)hr;
                }
                val += degToDisp(siderialTime * 15.0f, false) + "\n";
            }

            return val;
        }

        private string calcAriesHr(int sec)
        {
            string val = "\n";

            for (int min = 0; min < 60; min++)
            {
                float siderialTime = 0.0f;
                if (Plugin.configSiderialToggle.Value)
                {
                    siderialTime = (float)min / 60 + (float)sec / 3600  + ((float)min / 60 + (float)sec / 3600) / (float)Patches.yearLen;
                }
                else
                {
                    siderialTime = (float)min / 60 + (float)sec / 3600;
                }
                val += degToDisp2(siderialTime * 15.0f, false) + "\n";
            }

            return val;
        }


        private void calcPlanet(int day, Planet planetstruct, out string AHA, out string DEC, out string MAG)
        {
            AHA = "\n";
            DEC = "\n";
            MAG = "MAG ";

            for (int hr = 0; hr < 24; hr++)
            {
                float yearTime = ((float)hr/24 + (float)day) / (float)Patches.yearLen;

                Vector3 home = PlanetPatch.calcPlanetPos(PlanetPatch.planetHome, yearTime);
                Vector3 target = PlanetPatch.calcPlanetPos(planetstruct, yearTime);

                //Debug.Log($"Home   Pos X: {PlanetPatch.planetHome.pos.x}, Y: {PlanetPatch.planetHome.pos.y}, Z: {PlanetPatch.planetHome.pos.z}");
                //Debug.Log($"Planet Pos X: {planetstruct.pos.x}, Y: {planetstruct.pos.y}, Z: {planetstruct.pos.z}");

                Vector3 D = target - home;

                float siderialTime = 0.0f;
                if (Plugin.configSiderialToggle.Value)
                {
                    siderialTime = (float)hr + ((float)day * 24 + (float)hr) / (float)Patches.yearLen;
                }

                // planets are computed against the ecliptic; the table is against the equator
                Vector3 equ = Celestial.EclipticToEquatorial(D);
                float longitude = (360 - Mathf.Atan2(equ.y, equ.x) * Mathf.Rad2Deg) + (siderialTime * 15.0f);
                float Dec = Mathf.Asin(equ.z / equ.magnitude) * Mathf.Rad2Deg;

                AHA += degToDisp(longitude, false) + "\n";
                DEC += degToDisp(Dec, false) + "\n";

                // planetstruct.pos is never updated on the static Planet structs (they are copied by
                // value), so use the position computed for this hour
                float P = Vector3.Angle(-target, -D);


                if (hr == 0)
                {
                    float Q = 0;
                    if (planetstruct.type == 1)
                    {
                        Q = -2.5f * Mathf.Log(0.72f * Mathf.Exp(-3.332f * Mathf.Pow(Mathf.Tan(P / 2.0f * Mathf.Deg2Rad), 0.631f)) + 0.28f * Mathf.Exp(-1.862f * Mathf.Pow(Mathf.Tan(P / 2.0f * Mathf.Deg2Rad), 1.218f)), 10.0f);
                    }
                    else if (planetstruct.type == 2)
                    {
                        Q = -0.00106f * P + 0.0002054f * P * P;
                    }
                    else if (planetstruct.type == 3)
                    {
                        Q = -0.00037f * P + 0.000615f * P * P;
                    }

                    float RD = D.magnitude * target.magnitude * 0.68f * 0.68f;
                    float apparent = planetstruct.absBright + 5 * Mathf.Log(RD, 10.0f) + Q;
                    if (apparent > 0)
                    {
                        MAG += " ";
                    }
                    MAG += apparent.ToString("F2");
                }
            }


        }







        private void BuildSunMoonPage()
        {
            Transform left = this.dayEntriesLeft.transform;
            Transform right = this.dayEntriesRight.transform;

            // right page is laid out like the left one, mirrored: Hr, then Sun and Moon columns,
            // leaving the star list on the far right untouched
            Transform sun = CloneColumns(left, right, "Planet2", "Sun Table", 0.15f);
            Transform moon = CloneColumns(left, right, "Planet3", "Moon Table", 0.235f);
            if (sun == null || moon == null)
            {
                Debug.LogWarning("RealisticSkies: almanac layout not as expected, Sun & Moon tables disabled");
                return;
            }

            Transform hr = right.Find("Hr");
            if (hr != null)
            {
                hr.gameObject.SetActive(true);
            }

            SetText(FindText(sun, "Name Text"), "Sun");
            this.smSunAHA1 = FindText(sun, "AHA1");
            this.smSunDEC1 = FindText(sun, "DEC1");
            this.smSunAHA2 = FindText(sun, "AHA2");
            this.smSunDEC2 = FindText(sun, "DEC2");
            this.smSunNote = FindText(sun, "Mag Text");

            SetText(FindText(moon, "Name Text"), "Moon");
            this.smMoonAHA1 = FindText(moon, "AHA1");
            this.smMoonDEC1 = FindText(moon, "DEC1");
            this.smMoonAHA2 = FindText(moon, "AHA2");
            this.smMoonDEC2 = FindText(moon, "DEC2");
            this.smMoonNote = FindText(moon, "Mag Text");
        }

        private void NamePlanets()
        {
            // the four planet columns are Almanac planets 2, 3, 5 and 6
            Transform left = this.dayEntriesLeft.transform;
            SetText(FindText(left.Find("Planet2"), "Name Text"), PlanetNames.Get(2));
            SetText(FindText(left.Find("Planet3"), "Name Text"), PlanetNames.Get(3));
            SetText(FindText(left.Find("Planet5"), "Name Text"), PlanetNames.Get(5));
            SetText(FindText(left.Find("Planet6"), "Name Text"), PlanetNames.Get(6));
        }

        private void NameStars()
        {
            // lore names in the Bright Stars list on the right page
            TextMesh list = FindText(this.dayEntriesRight.transform, "Star Names");
            if (list == null)
            {
                return;
            }
            string[] lines = list.text.Split('\n');
            int longest = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string letter = lines[i].TrimEnd('\r');
                if (letter.Length == 0) continue;
                string shown = Lunar.DisplayNameWithLetter(letter);
                if (shown != letter)
                {
                    lines[i] = shown + (lines[i].EndsWith("\r") ? "\r" : "");
                }
                longest = Math.Max(longest, shown.Length);
            }
            list.text = string.Join("\n", lines);
            if (!Plugin.configStarNames.Value)
            {
                return;     // letters only: leave the vanilla layout alone
            }
            // pin the column by its right edge, so long names grow to the left (into the empty
            // margin) instead of spilling over the SHA column; the alignment was right already
            list.anchor = (TextAnchor)((int)list.anchor / 3 * 3 + 2);   // UpperX -> UpperRight, etc.
            list.alignment = TextAlignment.Right;
            // a touch smaller than the numbers, still well above the old shrunk size; very long names
            // shrink further. The line spacing is scaled back by the same factor so rows stay level.
            const float fitChars = 14f;
            float k = 0.9f;
            if (longest > fitChars)
            {
                k *= fitChars / longest;
            }
            list.characterSize *= k;
            list.lineSpacing /= k;
            PullNamesToSha(list);
        }

        /// <summary>
        /// Pinned by the right edge, the names end where the old letter column was centred, a good
        /// way left of the SHA column. Slide them right until they end just short of the widest SHA
        /// value. SHA and DEC are children of the names text, so they are moved back by the same amount.
        /// </summary>
        private static void PullNamesToSha(TextMesh names)
        {
            try
            {
                Transform shaT = names.transform.Find("Star SHA");
                TextMesh sha = shaT != null ? shaT.GetComponent<TextMesh>() : null;
                if (sha == null) return;
                float shaWidth = 0f;
                foreach (string line in sha.text.Split('\n'))
                {
                    shaWidth = Math.Max(shaWidth, TextWidth(sha, line.TrimEnd('\r')));
                }
                float digit = TextWidth(sha, "0");
                if (shaWidth <= 0f || digit <= 0f) return;
                // the SHA block's left edge in the names' own space, from its anchor (left / centre / right)
                float anchorFrac = ((int)sha.anchor % 3) * 0.5f;
                float shaLeft = shaT.localPosition.x - anchorFrac * shaWidth * shaT.localScale.x;
                float dx = shaLeft - NameGap * digit * shaT.localScale.x;
                // only ever move right, and never onto the SHA column itself
                if (dx <= 0f || dx >= shaLeft) return;
                Transform t = names.transform;
                t.localPosition += t.localRotation * new Vector3(dx * t.localScale.x, 0f, 0f);
                foreach (Transform child in t)
                {
                    child.localPosition -= new Vector3(dx, 0f, 0f);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("RealisticSkies: could not line up the star names: " + e.Message);
            }
        }

        /// <summary>Gap between a star's name and its SHA, in digit widths.</summary>
        private const float NameGap = 1.6f;

        /// <summary>Width of one line of a TextMesh in its own local units.</summary>
        private static float TextWidth(TextMesh tm, string s)
        {
            Font f = tm.font;
            if (f == null || string.IsNullOrEmpty(s)) return 0f;
            int size = tm.fontSize > 0 ? tm.fontSize : f.fontSize;
            f.RequestCharactersInTexture(s, size, tm.fontStyle);
            float px = 0f;
            foreach (char ch in s)
            {
                if (f.GetCharacterInfo(ch, out CharacterInfo ci, size, tm.fontStyle) || f.GetCharacterInfo(ch, out ci))
                {
                    px += ci.advance;
                }
            }
            // a TextMesh draws 10 font pixels per local unit at Character Size 1
            return px * 0.1f * tm.characterSize;
        }

        private void BuildLunarPage()
        {
            Transform parent = this.dayEntriesLeft.transform.parent;
            Transform existing = parent.Find("Entries Lunar Left");
            if (existing != null)
            {
                this.lunarLeft = existing.gameObject;
            }
            else
            {
                this.lunarLeft = UnityEngine.Object.Instantiate<GameObject>(this.dayEntriesLeft, parent);
                this.lunarLeft.name = "Entries Lunar Left";
            }
            this.lunarLeft.SetActive(false);

            Transform root = this.lunarLeft.transform;
            this.lunarTitle = FindText(root, "Title");
            this.lunarSD = MakeSubtitle(this.lunarTitle, "Moon SD");

            // the Aries column becomes the Sun column
            this.lunarSun1 = FindText(root, "Aries AHA1");
            this.lunarSun2 = FindText(root, "Aries AHA2");
            this.lunarSunHeader = FindText(root, "AHA Text");
            this.lunarSunHeaderTemplate = this.lunarSunHeader != null ? this.lunarSunHeader.text : "";

            // each planet group holds two stars: AHA columns for the first, DEC columns for the second
            string[] groups = { "Planet2", "Planet3", "Planet5", "Planet6" };
            for (int g = 0; g < groups.Length; g++)
            {
                Transform group = root.Find(groups[g]);
                if (group == null)
                {
                    Debug.LogWarning("RealisticSkies: almanac layout not as expected, lunar distance page disabled");
                    UnityEngine.Object.Destroy(this.lunarLeft);
                    this.lunarLeft = null;
                    return;
                }
                SetText(FindText(group, "Name Text"), "");
                SetText(FindText(group, "Mag Text"), "");

                this.lunarCol1[2 * g] = FindText(group, "AHA1");
                this.lunarCol2[2 * g] = FindText(group, "AHA2");
                this.lunarHeader[2 * g] = FindText(group, "AHA Text");

                this.lunarCol1[2 * g + 1] = FindText(group, "DEC1");
                this.lunarCol2[2 * g + 1] = FindText(group, "DEC2");
                this.lunarHeader[2 * g + 1] = FindText(group, "DEC Text");
            }
            for (int i = 0; i < 8; i++)
            {
                this.lunarHeaderTemplate[i] = this.lunarHeader[i] != null ? this.lunarHeader[i].text : "";
            }
        }

        /// <summary>
        /// The two days of a page by the names the journal, the nocturnal and the ring dial call them:
        /// "Autumn 3-4 (days 3-4)", "Autumn 23 - Winter 1 (days 23-24)", "5-6 October (days 14-15)": the date, and
        /// the days of the year counted from 1. (The pages used to be headed
        /// "Days 2 and 3": game days counted from nought, one less than the day of the year everything
        /// else names, so a navigator opened the next day's page.)
        /// </summary>
        private static string PairName(int day1, int day2)
        {
            string a = ModItemNocturnal.DateName(day1), b = ModItemNocturnal.DateName(day2);
            int len = ModItemNocturnal.YearLen;                     // days of the year from 1, as the journal and the log book's date line count them
            string days = " (days " + (ModItemNocturnal.DayOfYear(day1) % len + 1) + "-" + (ModItemNocturnal.DayOfYear(day2) % len + 1) + ")";
            int sa = a.LastIndexOf(' '), sb = b.LastIndexOf(' ');
            if (sa < 0 || sb < 0) return a + " - " + b + days;
            if (ModItemNocturnal.YearLen == 92)
            {
                // "Autumn 3", "Autumn 4"
                return (a.Substring(0, sa) == b.Substring(0, sb) ? a + "-" + b.Substring(sb + 1) : a + " - " + b) + days;
            }
            // "5 October", "6 October"
            int fa = a.IndexOf(' '), fb = b.IndexOf(' ');
            return (a.Substring(fa + 1) == b.Substring(fb + 1) ? a.Substring(0, fa) + "-" + b : a + " - " + b) + days;
        }

        private void FillLunarPage(int day1, int day2)
        {
            // distances are to the moon's centre; a sight to its edge needs the semi-diameter, in a
            // smaller line under the title
            SetText(this.lunarTitle, PairName(day1, day2) + "  Lunar Distances (centre)");
            double sd = Lunar.MoonSemiDiameter;
            SetText(this.lunarSD, double.IsNaN(sd) ? "" : $"Moon semi-diameter{Celestial.Format(sd)}'");

            // the Sun's column named on the same line as the stars'
            SetText(this.lunarSunHeader, this.lunarSunHeaderTemplate.Replace("Aries", "").Replace("AHA", "Sun"));
            SetText(this.lunarSun1, DistanceColumn(Lunar.SunIndex, day1));
            SetText(this.lunarSun2, DistanceColumn(Lunar.SunIndex, day2));

            // the brightest stars the moon is well placed for on these two days
            List<int> stars = Lunar.TableStars(day1, 8, new List<int>());

            for (int i = 0; i < 8; i++)
            {
                if (i < stars.Count)
                {
                    string name = Lunar.Name(stars[i]);
                    SetText(this.lunarHeader[i], this.lunarHeaderTemplate[i].Replace("AHA", name).Replace("DEC", name));
                    SetText(this.lunarCol1[i], DistanceColumn(stars[i], day1));
                    SetText(this.lunarCol2[i], DistanceColumn(stars[i], day2));
                }
                else
                {
                    SetText(this.lunarHeader[i], "");
                    SetText(this.lunarCol1[i], "");
                    SetText(this.lunarCol2[i], "");
                }
            }
        }

        private static string DistanceColumn(int body, int day)
        {
            string val = "\n";
            for (int hr = 0; hr < 24; hr++)
            {
                val += Celestial.Format(Lunar.Distance(body, day, hr)) + "\n";
            }
            return val;
        }

        private static Transform CloneColumns(Transform fromPage, Transform toPage, string source, string name, float x)
        {
            Transform existing = toPage.Find(name);
            if (existing != null)
            {
                return existing;
            }
            Transform src = fromPage.Find(source);
            if (src == null)
            {
                return null;
            }
            GameObject copy = UnityEngine.Object.Instantiate<GameObject>(src.gameObject, toPage);
            copy.name = name;
            copy.transform.localPosition = new Vector3(x, src.localPosition.y, src.localPosition.z);
            copy.transform.localRotation = src.localRotation;
            copy.transform.localScale = src.localScale;
            copy.SetActive(true);
            return copy.transform;
        }

        private void FillSunMoon(int day1, int day2)
        {
            SetText(this.smSunAHA1, SunColumn(day1, true));
            SetText(this.smSunDEC1, SunColumn(day1, false));
            SetText(this.smSunAHA2, SunColumn(day2, true));
            SetText(this.smSunDEC2, SunColumn(day2, false));
            SetText(this.smSunNote, "");

            if (Celestial.MoonReworkActive)
            {
                SetText(this.smMoonAHA1, MoonColumn(day1, true));
                SetText(this.smMoonDEC1, MoonColumn(day1, false));
                SetText(this.smMoonAHA2, MoonColumn(day2, true));
                SetText(this.smMoonDEC2, MoonColumn(day2, false));

                // illuminated fraction at noon of each day
                double ill1 = Celestial.MoonIllumination(day1 + 0.5) * 100.0;
                double ill2 = Celestial.MoonIllumination(day2 + 0.5) * 100.0;
                SetText(this.smMoonNote, $"Lit {ill1:F0}% {ill2:F0}%");
            }
            else
            {
                string empty = "\n";
                for (int hr = 0; hr < 24; hr++)
                {
                    empty += "  --\n";
                }
                SetText(this.smMoonAHA1, empty);
                SetText(this.smMoonDEC1, empty);
                SetText(this.smMoonAHA2, empty);
                SetText(this.smMoonDEC2, empty);
                SetText(this.smMoonNote, "");
            }
        }

        private static string SunColumn(int day, bool aha)
        {
            string val = "\n";
            for (int hr = 0; hr < 24; hr++)
            {
                double t = day + hr / 24.0;
                val += Celestial.Format(aha ? Celestial.SunGHA(t, hr) : Celestial.SunDec(t)) + "\n";
            }
            return val;
        }

        private static string MoonColumn(int day, bool aha)
        {
            string val = "\n";
            for (int hr = 0; hr < 24; hr++)
            {
                double t = day + hr / 24.0;
                val += Celestial.Format(aha ? Celestial.MoonGHA(t, hr) : Celestial.MoonDec(t)) + "\n";
            }
            return val;
        }

        /// <summary>A smaller copy of a title, one line below it.</summary>
        private static TextMesh MakeSubtitle(TextMesh title, string name)
        {
            if (title == null) return null;
            Transform existing = title.transform.parent.Find(name);
            if (existing != null) return existing.GetComponent<TextMesh>();
            GameObject go = UnityEngine.Object.Instantiate(title.gameObject, title.transform.parent);
            go.name = name;
            TextMesh tm = go.GetComponent<TextMesh>();
            int px = tm.fontSize > 0 ? tm.fontSize : (tm.font != null ? tm.font.fontSize : 14);
            float line = px * 0.1f * title.characterSize * title.lineSpacing * 1.1f;     // one line of the title, local units
            tm.characterSize = title.characterSize * 0.7f;
            Transform t = go.transform;
            t.localPosition = title.transform.localPosition - title.transform.localRotation * new Vector3(0f, line * title.transform.localScale.y, 0f);
            tm.text = "";
            return tm;
        }

        private static TextMesh FindText(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }
            Transform child = parent.Find(name);
            return child != null ? child.GetComponent<TextMesh>() : null;
        }

        private static void SetText(TextMesh mesh, string text)
        {
            if (mesh != null)
            {
                mesh.text = text;
            }
        }

        private static void Hide(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        //Awful display formatting code
        private string degToDisp(float val, bool flag)
        {
            float deg = 0.0f;
            float arcMin = 0.0f;
            bool neg = false;

            if (val < 0)
            {
                neg = true;
            }

            val = Mathf.Abs(val);

            val = val % 360;

            deg = Mathf.Floor(val);
            arcMin = (val - Mathf.Floor(val)) * 60;

            string arc = arcMin.ToString("0.0");

            if (arc.Length < 4)
            {
                arc = "0" + arc;
            }

            string degDisp = deg.ToString("F0");

            string disp = degDisp + "°" + arc;

            if(flag)
            {
                if (neg)
                {
                    disp = "S" + disp;
                }
                else
                {
                    disp = "N" + disp;
                }
            }
            else
            {
                if (neg)
                {
                    disp = "-" + disp;
                }
                else
                {
                    disp = " " + disp;
                }
            }


            return disp;
        }


        private string degToDisp2(float val, bool flag)
        {
            float deg = 0.0f;
            float arcMin = 0.0f;
            bool neg = false;

            if (val < 0)
            {
                neg = true;
            }

            val = Mathf.Abs(val);

            val = val % 360;

            deg = Mathf.Floor(val);
            arcMin = (val - Mathf.Floor(val)) * 60;

            string arc = arcMin.ToString("F0");

            if (arc.Length < 2)
            {
                arc = "0" + arc;
            }

            string degDisp = deg.ToString("F0");

            string disp = degDisp + "°" + arc;

            if (flag)
            {
                if (neg)
                {
                    disp = "S" + disp;
                }
                else
                {
                    disp = "N" + disp;
                }
            }
            else
            {
                if (neg)
                {
                    disp = "-" + disp;
                }
                else
                {
                    disp = " " + disp;
                }
            }


            return disp;
        }
    }
}
using System;
using System.Collections;
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

        private GameObject p2long1;
        private GameObject p2dec1;
        private GameObject p2long2;
        private GameObject p2dec2;

        private GameObject p3long1;
        private GameObject p3dec1;
        private GameObject p3long2;
        private GameObject p3dec2;

        private GameObject p4long1;
        private GameObject p4dec1;
        private GameObject p4long2;
        private GameObject p4dec2;

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

        private ModItemAlmanac()
        {
            if (!constructed)
            {
                base.value = 4000;
                base.name = "almanac";
                base.category = TransactionCategory.toolsAndSupplies;
                base.inventoryScale = 1f;
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

            this.p2long1 = this.dayEntriesLeft.transform.GetChild(6).GetChild(0).gameObject;
            this.p2dec1 = this.dayEntriesLeft.transform.GetChild(6).GetChild(1).gameObject;
            this.p2long2 = this.dayEntriesLeft.transform.GetChild(6).GetChild(2).gameObject;
            this.p2dec2 = this.dayEntriesLeft.transform.GetChild(6).GetChild(3).gameObject;

            this.p3long1 = this.dayEntriesLeft.transform.GetChild(7).GetChild(0).gameObject;
            this.p3dec1 = this.dayEntriesLeft.transform.GetChild(7).GetChild(1).gameObject;
            this.p3long2 = this.dayEntriesLeft.transform.GetChild(7).GetChild(2).gameObject;
            this.p3dec2 = this.dayEntriesLeft.transform.GetChild(7).GetChild(3).gameObject;

            this.p4long1 = this.dayEntriesLeft.transform.GetChild(8).GetChild(0).gameObject;
            this.p4dec1 = this.dayEntriesLeft.transform.GetChild(8).GetChild(1).gameObject;
            this.p4long2 = this.dayEntriesLeft.transform.GetChild(8).GetChild(2).gameObject;
            this.p4dec2 = this.dayEntriesLeft.transform.GetChild(8).GetChild(3).gameObject;

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

            //page is stored in the amount value

            if (this.amount == 0f)
            {
                this.amount = -1.0f;
                this.Close();
            } 
            else if (this.amount > 0f)
            {
                this.Open();
            }
            else
            {
                this.Close();
            }
            this.page = (int)Mathf.Abs(base.amount);

            //year is stored in the health value + 1

            if (!base.sold)
            {
                base.health = Mathf.FloorToInt(Patches.year);
            }

            this.pagecount = Mathf.CeilToInt((float)Patches.yearLen / 2.0f) + 1;

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

        private void Open()
        {
            base.amount = Mathf.Abs(base.amount);
            this.closedEmpty.SetActive(false);
            this.openEmpty.SetActive(true);

            loadPage(this.page);

            base.holdDistance = 0.36f;

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

        private void loadPage(int page)
        {
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
                this.dayEntriesLeft.SetActive(true);
                this.dayEntriesRight.SetActive(true);

                this.hourEntriesLeft.SetActive(false);
                this.hourEntriesRight.SetActive(false);


                int day1 = 2 * (page - 1) + (int)this.health * Patches.yearLen;
                int day2 = 2 * (page - 1) + 1 + (int)this.health * Patches.yearLen;

                this.dayLeft.GetComponent<TextMesh>().text = $"Days {day1} and {day2} AT";
                this.dayRight.GetComponent<TextMesh>().text = $"Days {day1} and {day2} AT";

                this.aries1.GetComponent<TextMesh>().text = calcAries(day1);
                this.aries2.GetComponent<TextMesh>().text = calcAries(day2);

                string AHApass = "";
                string DECpass = "";
                calcPlanet(day1, PlanetPatch.planet2, out AHApass, out DECpass);
                this.p1long1.GetComponent<TextMesh>().text = AHApass;
                this.p1dec1.GetComponent<TextMesh>().text = DECpass;
                calcPlanet(day2, PlanetPatch.planet2, out AHApass, out DECpass);
                this.p1long2.GetComponent<TextMesh>().text = AHApass;
                this.p1dec2.GetComponent<TextMesh>().text = DECpass;

                calcPlanet(day1, PlanetPatch.planet4, out AHApass, out DECpass);
                this.p2long1.GetComponent<TextMesh>().text = AHApass;
                this.p2dec1.GetComponent<TextMesh>().text = DECpass;
                calcPlanet(day2, PlanetPatch.planet4, out AHApass, out DECpass);
                this.p2long2.GetComponent<TextMesh>().text = AHApass;
                this.p2dec2.GetComponent<TextMesh>().text = DECpass;

                calcPlanet(day1, PlanetPatch.planet6, out AHApass, out DECpass);
                this.p3long1.GetComponent<TextMesh>().text = AHApass;
                this.p3dec1.GetComponent<TextMesh>().text = DECpass;
                calcPlanet(day2, PlanetPatch.planet6, out AHApass, out DECpass);
                this.p3long2.GetComponent<TextMesh>().text = AHApass;
                this.p3dec2.GetComponent<TextMesh>().text = DECpass;

                calcPlanet(day1, PlanetPatch.planet7, out AHApass, out DECpass);
                this.p4long1.GetComponent<TextMesh>().text = AHApass;
                this.p4dec1.GetComponent<TextMesh>().text = DECpass;
                calcPlanet(day2, PlanetPatch.planet7, out AHApass, out DECpass);
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


        private void calcPlanet(int day, Planet planetstruct, out string AHA, out string DEC)
        {
            AHA = "\n";
            DEC = "\n";

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

                float longitude = (360 - Mathf.Atan2(D.y, D.x) * Mathf.Rad2Deg) + (siderialTime * 15.0f);
                float Dec = Mathf.Asin(target.z / D.magnitude) * Mathf.Rad2Deg;

                AHA += degToDisp(longitude, false) + "\n";
                DEC += degToDisp(Dec, false) + "\n";

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
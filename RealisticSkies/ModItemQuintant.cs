using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;
using static System.Net.Mime.MediaTypeNames;

namespace RealisticSkies
{
    public class ModItemQuintant : ShipItemHangable
    {
        private Transform dial;

        private Transform rotatingParent;

        private Transform mirror;

        private Transform scope;

        private Transform indexShade1;

        private Transform indexShade2;

        private Transform indexShade3;

        private Transform horizonShade1;

        private Transform horizonShade2;

        private Transform horizonShade3;

        private Transform hud;

        private Transform declutter;

        private Transform minute;

        private Transform second;

        private GameObject clock;

        private GameObject horizonCam;

        private GameObject indexCam;

        private GameObject ocularCam;

        private Material glassMaterial;

        private Material horizonMaterial;

        private Material indexMaterial;

        private Material ocularMaterial;

        private Renderer horizonRenderer;

        private Renderer indexRenderer;

        private Renderer ocularRenderer;

        public bool lockX;

        public bool lockY;

        public bool lockZ;

        private Quaternion initialRot;

        private Quaternion inspectRot;

        private Quaternion dropRot;

        private Vector3 initialPos;

        private Vector3 inspectPos;

        private Vector3 scopeInitialPos;

        private Vector3 scopeDeployPos;

        private bool inspecting;

        private bool rotating;

        private Quaternion indexShade1Closed;

        private Quaternion indexShade2Closed;

        private Quaternion indexShade3Closed;

        private Quaternion indexShade1Open;

        private Quaternion indexShade2Open;

        private Quaternion indexShade3Open;

        private Quaternion horizonShade1Closed;

        private Quaternion horizonShade2Closed;

        private Quaternion horizonShade3Closed;

        private Quaternion horizonShade1Open;

        private Quaternion horizonShade2Open;

        private Quaternion horizonShade3Open;

        private Quaternion hudOpen;

        private Quaternion declutterOpen;

        private Quaternion hudClosed;

        private Quaternion declutterClosed;

        private Vector3 minuteRot = Vector3.zero;

        private Vector3 secondRot = Vector3.zero;

        private int shadeLevel = 3;

        private int hudLevel = 2;

        private bool moving;

        private GameObject textQuint;

        private GameObject textClock;

        private float gameTime;

        private PostProcessingProfile postProcessing;



        static bool constructed;

        // lunar distance mode: click a star / the sun, then click the moon
        private bool lunarMode;

        private const int NoLock = int.MinValue;

        private int lunarLock = NoLock;

        private const float LunarSnap = 3f; // degrees: how close the crosshair has to be to a body

        private ModItemQuintant()
        {
            if (!constructed)
            {
                base.value = 16000;
                base.name = "quintant";
                base.category = TransactionCategory.toolsAndSupplies;
                base.inventoryScale = 1f;
                base.inventoryRotation = 90f;
                base.holdDistance = 0.4f;
                base.furniturePlaceHeight = 0.5f;
                constructed = true;
            }

        }

        public override void OnLoad()
        {
            rotatingParent = base.transform.GetChild(0);
            dial  = base.transform.GetChild(0).GetChild(2);
            mirror = base.transform.GetChild(0).GetChild(0);
            scope = base.transform.GetChild(0).GetChild(1);

            this.declutter = base.transform.GetChild(0).GetChild(6);
            this.hud = base.transform.GetChild(0).GetChild(7);
            this.indexShade1 = base.transform.GetChild(0).GetChild(8);
            this.indexShade2 = base.transform.GetChild(0).GetChild(9);
            this.indexShade3 = base.transform.GetChild(0).GetChild(10);
            this.horizonShade1 = base.transform.GetChild(0).GetChild(11);
            this.horizonShade2 = base.transform.GetChild(0).GetChild(12);
            this.horizonShade3 = base.transform.GetChild(0).GetChild(13);

            this.hudClosed = this.hud.localRotation;
            this.declutterClosed = this.declutter.localRotation;

            this.hudOpen = this.hudClosed * Quaternion.Euler(Vector3.forward * -110f);
            this.declutterOpen = this.declutterClosed * Quaternion.Euler(Vector3.forward * -110f);

            this.indexShade1Closed = this.indexShade1.localRotation;
            this.indexShade2Closed = this.indexShade2.localRotation;
            this.indexShade3Closed = this.indexShade3.localRotation;
            this.horizonShade1Closed = this.horizonShade1.localRotation;
            this.horizonShade2Closed = this.horizonShade2.localRotation;
            this.horizonShade3Closed = this.horizonShade3.localRotation;

            this.indexShade1Open = this.indexShade1Closed * Quaternion.Euler(Vector3.forward * -100f);
            this.indexShade2Open = this.indexShade2Closed * Quaternion.Euler(Vector3.forward * -100f);
            this.indexShade3Open = this.indexShade3Closed * Quaternion.Euler(Vector3.forward * -100f);
            this.horizonShade1Open = this.horizonShade1Closed * Quaternion.Euler(Vector3.forward * -110f);
            this.horizonShade2Open = this.horizonShade2Closed * Quaternion.Euler(Vector3.forward * -110f);
            this.horizonShade3Open = this.horizonShade3Closed * Quaternion.Euler(Vector3.forward * -110f);

            this.horizonRenderer = base.transform.GetChild(0).GetChild(0).GetChild(1).gameObject.GetComponent<MeshRenderer>();
            this.indexRenderer = base.transform.GetChild(0).GetChild(3).gameObject.GetComponent<MeshRenderer>();
            this.ocularRenderer = base.transform.GetChild(0).GetChild(1).GetChild(1).gameObject.GetComponent<MeshRenderer>();

            this.horizonMaterial = this.horizonRenderer.material;
            this.indexMaterial = this.indexRenderer.material;
            this.ocularMaterial = this.ocularRenderer.material;

            this.glassMaterial = base.transform.GetChild(0).GetChild(5).gameObject.GetComponent<MeshRenderer>().material;

            this.horizonCam = base.transform.GetChild(0).GetChild(0).GetChild(0).gameObject;
            this.indexCam = base.transform.GetChild(0).GetChild(4).gameObject;
            this.ocularCam = base.transform.GetChild(0).GetChild(1).GetChild(0).gameObject;

            this.textQuint = base.transform.GetChild(0).GetChild(14).gameObject;

            this.clock = base.transform.GetChild(0).GetChild(15).gameObject;
            this.second = base.transform.GetChild(0).GetChild(15).GetChild(2);
            this.minute = base.transform.GetChild(0).GetChild(15).GetChild(1);

            this.textClock = base.transform.GetChild(0).GetChild(15).GetChild(3).gameObject;

            scopeInitialPos = scope.localPosition;
            scopeDeployPos = scope.localPosition + new Vector3(0.0f, 0.0f, -0.07421875f);

            initialPos = rotatingParent.localPosition;
            inspectPos = rotatingParent.localPosition + new Vector3(-0.19f, 0.13f, -0.15f);

            initialRot = rotatingParent.localRotation;
            inspectRot = initialRot * Quaternion.Euler(Vector3.up * -90f);
            dropRot = initialRot * Quaternion.Euler(Vector3.left * -90f);

            this.postProcessing = FindObjectOfType<PlayerAlcohol>().postProcessing;

            this.toggleMirrors(false);
            this.takeReading(false);
        }

        public override bool AllowOnItemClick(GoPointerButton lookedAtButton)
        {
            // can be hung on lamp hooks
            return lookedAtButton.GetComponent<ShipItemLampHook>() != null || (!base.big && lookedAtButton.allowPlacingItems);
        }

        public override void OnAltActivate()
        {
            if (sold && lunarMode && !inspecting && !rotating)
            {
                this.LunarClick();
                return;
            }
            if (!sold)
            {
                base.OnAltActivate();
            }
            else
            {
                if (rotating)
                {
                    return;
                }
                inspecting = !inspecting;
                if (!rotating)
                {
                    if (inspecting)
                    {
                        this.toggleMirrors(false);
                        StartCoroutine(SmoothlyRotate(inspectRot));
                        StartCoroutine(SmoothlyTranslate(rotatingParent, inspectPos));

                        takeReading(true);
                    }
                    else
                    {
                        this.toggleMirrors(true);
                        takeReading(false);

                        StartCoroutine(SmoothlyRotate(initialRot));
                        StartCoroutine(SmoothlyTranslate(rotatingParent, initialPos));
                    }
                }
            }
        }

        public override void OnDrop()
        {
            base.OnDrop();
            if (inspecting)
            {
                this.textQuint.gameObject.SetActive(false);
                this.clock.gameObject.SetActive(false);

                //inspecting = false;

                //takeReading(false);
                StartCoroutine(SmoothlyTranslate(rotatingParent, initialPos));
                StartCoroutine(SmoothlyRotate(initialRot));
            }

            StartCoroutine(SmoothlyTranslate(scope, scopeInitialPos));
            this.toggleMirrors(false);
        }

        public override void OnPickup()
        {
            base.OnPickup();
            if (inspecting)
            {
                this.textQuint.gameObject.SetActive(true);
                this.clock.gameObject.SetActive(true);

                StartCoroutine(SmoothlyRotate(inspectRot));
                StartCoroutine(SmoothlyTranslate(rotatingParent, inspectPos));

            }


                StartCoroutine(SmoothlyTranslate(scope, scopeDeployPos));

            this.toggleMirrors(true);
        }

        private void toggleMirrors(bool active)
        {
            if (active)
            {
                this.indexRenderer.material = this.indexMaterial;
                this.horizonRenderer.material = this.horizonMaterial;
                this.ocularRenderer.material = this.ocularMaterial;
                this.indexCam.SetActive(true);
                this.horizonCam.SetActive(true);
                this.ocularCam.SetActive(true);
            }
            else
            {
                this.indexRenderer.material = this.glassMaterial;
                this.horizonRenderer.material = this.glassMaterial;
                this.ocularRenderer.material = this.glassMaterial;
                this.indexCam.SetActive(false);
                this.horizonCam.SetActive(false);
                this.ocularCam.SetActive(false);
            }
        }

        private void takeReading(bool active)
        {
            if (active)
            {
                this.gameTime = Sun.sun.globalTime;

                float hour = Mathf.Floor(this.gameTime);
                float min = Mathf.Floor((this.gameTime - hour) * 60);
                float sec = Mathf.Floor((this.gameTime - hour - min / 60) * 60 * 60);

                // round the whole angle to 0.1' first so 12°59.97' shows as 13°00.0', not 12°60.0'
                double totalMin = Math.Round(this.dial.localEulerAngles.x * 60.0, 1);
                var deg = Math.Floor(totalMin / 60.0);
                var arcMin = (totalMin - deg * 60.0).ToString("0.0");

                // into the navigator's journal, if one is carried
                NavJournal.NoteAltitude(totalMin / 60.0);

                if (Plugin.configQuintantHelp.Value == "Full")
                {
                    this.textQuint.gameObject.SetActive(true);
                    this.textClock.gameObject.SetActive(true);

                    this.textQuint.GetComponent<TextMesh>().text = $"{deg}°{arcMin}'";

                    if (sec < 10)
                    {
                        this.textClock.GetComponent<TextMesh>().text = $"{min}:0{sec}";
                    }
                    else
                    {
                        this.textClock.GetComponent<TextMesh>().text = $"{min}:{sec}";
                    }

                }
                else if (Plugin.configQuintantHelp.Value == "Sec")
                {
                    this.textQuint.gameObject.SetActive(true);
                    this.textClock.gameObject.SetActive(true);

                    this.textQuint.GetComponent<TextMesh>().text = $"{arcMin}'";
                    this.textClock.GetComponent<TextMesh>().text = $"{sec}";
                } else if (Plugin.configQuintantHelp.Value == "Arc")
                {
                    this.textQuint.gameObject.SetActive(true);
                    this.textClock.gameObject.SetActive(false);

                    this.textQuint.GetComponent<TextMesh>().text = $"{arcMin}'";

                } else
                {
                    this.textQuint.gameObject.SetActive(false);
                    this.textClock.gameObject.SetActive(false);
                }


                    this.minuteRot.x = -360 * min / 60;
                this.secondRot.x = -360 * sec / 60;

                this.minute.localEulerAngles = this.minuteRot;
                this.second.localEulerAngles = this.secondRot;


                this.clock.gameObject.SetActive(true);
            }
            else
            {
                this.textQuint.gameObject.SetActive(false);
                this.clock.gameObject.SetActive(false);
            }

        }


        public override void OnScroll(float input)
        {
            bool flag1 = this.moving;
            if (!flag1)
            {
                bool flag2 = this.inspecting;
                if (flag2)
                {
                    bool flag3 = input < 0f;
                    if (flag3)
                    {
                        base.StartCoroutine(this.MoveShade(false));
                    }
                    else
                    {
                        bool flag4 = input > 0f;
                        if (flag4)
                        {
                            base.StartCoroutine(this.MoveShade(true));
                        }
                    }
                }
                else
                {
                    // sights: none <-> fine crosshair <-> the quintant's crosshair <-> crosshair and frame
                    // (the fine one is ours, on a third plate further from the eyepiece: it stays in when the
                    // quintant's plates swing in over it, and only swings away to leave the clear view)
                    if (input < 0f)
                    {
                        if (hudLevel == 0 && !fineCrossOn)
                        {
                            fineCrossOn = true;
                            base.StartCoroutine(this.SwingFineCross(true, true));
                        }
                        else
                        {
                            base.StartCoroutine(this.MoveHud(false));
                        }
                    }
                    else if (input > 0f)
                    {
                        if (hudLevel == 0 && fineCrossOn)
                        {
                            fineCrossOn = false;
                            base.StartCoroutine(this.SwingFineCross(false, true));
                        }
                        else if (hudLevel > 0)
                        {
                            base.StartCoroutine(this.MoveHud(true));
                        }
                    }
                }
            }
        }


        private IEnumerator MoveShade(bool open)
        {   //opens and closes the shades. Pass true to use opening logic, pass false to use closing logic
            moving = true;
            for (float t = 0f; t <= 1f; t += Time.deltaTime * 5f)
            {
                if (open)
                {
                    if (shadeLevel == 1)
                    {   //shade 1 closed, open shade 1
                        indexShade1.localRotation = Quaternion.Slerp(indexShade1Closed, indexShade1Open, t);
                        horizonShade1.localRotation = Quaternion.Slerp(horizonShade1Closed, horizonShade1Open, t);
                    }
                    else if (shadeLevel == 2)
                    {   //shade 1 and 2 closed, open shade 2
                        indexShade2.localRotation = Quaternion.Slerp(indexShade2Closed, indexShade2Open, t);
                        horizonShade2.localRotation = Quaternion.Slerp(horizonShade2Closed, horizonShade2Open, t);
                    }
                    else if (shadeLevel == 3)
                    {   //shade 1, 2, and 3 closed, open shade 3
                        indexShade3.localRotation = Quaternion.Slerp(indexShade3Closed, indexShade3Open, t);
                        horizonShade3.localRotation = Quaternion.Slerp(horizonShade3Closed, horizonShade3Open, t);
                    }
                }
                else
                {
                    if (shadeLevel == 0)
                    {   //no shades closed, close shade 1
                        indexShade1.localRotation = Quaternion.Slerp(indexShade1Open, indexShade1Closed, t);
                        horizonShade1.localRotation = Quaternion.Slerp(horizonShade1Open, horizonShade1Closed, t);
                    }
                    else if (shadeLevel == 1)
                    {   //shade 1 closed, close shade 2
                        indexShade2.localRotation = Quaternion.Slerp(indexShade2Open, indexShade2Closed, t);
                        horizonShade2.localRotation = Quaternion.Slerp(horizonShade2Open, horizonShade2Closed, t);
                    }
                    else if (shadeLevel == 2)
                    {   //shade 1 and 2 closed, close shade 3
                        indexShade3.localRotation = Quaternion.Slerp(indexShade3Open, indexShade3Closed, t);
                        horizonShade3.localRotation = Quaternion.Slerp(horizonShade3Open, horizonShade3Closed, t);
                    }
                }
                yield return new WaitForEndOfFrame();
            }
            if (open)
            {
                if (shadeLevel == 1)
                {
                    indexShade1.localRotation = indexShade1Open;
                    horizonShade1.localRotation = horizonShade1Open;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                else if (shadeLevel == 2)
                {
                    indexShade2.localRotation = indexShade2Open;
                    horizonShade2.localRotation = horizonShade2Open;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                else if (shadeLevel == 3)
                {
                    indexShade3.localRotation = indexShade3Open;
                    horizonShade3.localRotation = horizonShade3Open;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                shadeLevel--;
                if (shadeLevel < 0)
                {
                    shadeLevel = 0;
                }
            }
            else
            {
                if (shadeLevel == 0)
                {
                    indexShade1.localRotation = indexShade1Closed;
                    horizonShade1.localRotation = horizonShade1Closed;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                else if (shadeLevel == 1)
                {
                    indexShade2.localRotation = indexShade2Closed;
                    horizonShade2.localRotation = horizonShade2Closed;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                else if (shadeLevel == 2)
                {
                    indexShade3.localRotation = indexShade3Closed;
                    horizonShade3.localRotation = horizonShade3Closed;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                shadeLevel++;
                if (shadeLevel > 3)
                {
                    shadeLevel = 3;
                }
            }
            //Debug.LogWarning("Sextant: shadeLevel: " + shadeLevel);
            moving = false;
        }
        

        private IEnumerator MoveHud(bool open)
        {   //opens and closes the shades. Pass true to use opening logic, pass false to use closing logic
            moving = true;
            for (float t = 0f; t <= 1f; t += Time.deltaTime * 5f)
            {
                if (open)
                {
                    if (hudLevel == 1)
                    {   //hud closed, open hud
                        hud.localRotation = Quaternion.Slerp(hudClosed, hudOpen, t);
                    }
                    else if (hudLevel == 2)
                    {   //hud and declutter closed, open declutter
                        declutter.localRotation = Quaternion.Slerp(declutterClosed, declutterOpen, t);
                    }
                }
                else
                {
                    if (hudLevel == 0)
                    {   //no hud closed, close hud
                        hud.localRotation = Quaternion.Slerp(hudOpen, hudClosed, t);
                    }
                    else if (hudLevel == 1)
                    {   //hud closed, close declutter
                        declutter.localRotation = Quaternion.Slerp(declutterOpen, declutterClosed, t);
                    }
                }
                yield return new WaitForEndOfFrame();
            }
            if (open)
            {
                if (hudLevel == 1)
                {
                    hud.localRotation = hudOpen;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                else if (hudLevel == 2)
                {
                    declutter.localRotation = declutterOpen;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                hudLevel--;
                if (hudLevel < 0)
                {
                    hudLevel = 0;
                }
            }
            else
            {
                if (hudLevel == 0)
                {
                    hud.localRotation = hudClosed;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                else if (hudLevel == 1)
                {
                    declutter.localRotation = declutterClosed;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                hudLevel++;
                if (hudLevel > 2)
                {
                    hudLevel = 2;
                }
            }
            //Debug.LogWarning("Sextant: shadeLevel: " + shadeLevel);
            moving = false;
        }


        private IEnumerator SmoothlyRotate(Quaternion targetRot)
        {
            rotating = true;
            Quaternion startRot = rotatingParent.localRotation;
            for (float t = 0f; t <= 1f; t += Time.deltaTime * 5f)
            {
                rotatingParent.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return new WaitForEndOfFrame();
            }
            rotatingParent.localRotation = targetRot;
            rotating = false;
        }

        private IEnumerator SmoothlyTranslate(Transform obj, Vector3 targetPos)
        {
            Vector3 startPos = obj.localPosition;
            for (float t = 0f; t <= 1f; t += Time.deltaTime * 5f)
            {
                obj.localPosition = Vector3.Lerp(startPos, targetPos, t);
                yield return new WaitForEndOfFrame();
            }
            obj.localPosition = targetPos;
        }

        private static void Notify(string text)
        {
            if (NotificationUi.instance != null)
            {
                NotificationUi.instance.ShowNotification(text, 5f);
            }
        }

        private void ToggleLunarMode()
        {
            if (!lunarMode && !Celestial.MoonReworkActive)
            {
                Notify("Lunar distances need \"Moon Orbit Rework\" turned on");
                return;
            }
            lunarMode = !lunarMode;
            lunarLock = NoLock;
            UISoundPlayer.instance.PlayOpenSound();
            Notify(lunarMode
                ? "Quintant: lunar distance\nclick a star or the Sun, then bring\nthe Moon's edge to it and click"
                : "Quintant: altitude");
        }

        private void LunarClick()
        {
            if (!Celestial.HaveSkyAxes || Sun.sun == null || Moon.instance == null || Moon.instance.transform.parent == null)
            {
                return;
            }

            Vector3 aim = this.ocularCam != null ? this.ocularCam.transform.forward : Camera.main.transform.forward;
            // with a star locked, the touch is where its reflection stands: on the vertical line, at
            // the star's height (the Moon's edge, in the clear half, meets the star across the line)
            Camera ocam = this.ocularCam != null ? this.ocularCam.GetComponent<Camera>() : null;
            if (lunarLock != NoLock && ocam != null)
            {
                aim = ocam.ViewportPointToRay(new Vector3(0.5f, 0.5f + StarAboveLine, 0f)).direction;
            }
            int day = GameState.day;
            double hour = Sun.sun.globalTime;

            Vector3 moonDir = (Moon.instance.transform.position - Moon.instance.transform.parent.position).normalized;
            float moonAngle = Vector3.Angle(aim, moonDir);

            // the sight: the Moon's edge brought to the reflected star in the crosshair; what is read
            // is the angle from the line of sight to the star, so it is only as good as the aim
            double sd = Lunar.MoonSemiDiameter;
            float onMoon = (float)(double.IsNaN(sd) ? 0.5 : sd) + 0.5f;
            if (lunarLock != NoLock && moonAngle < onMoon)
            {
                // As if the quintant were rocked to find the touch: a navigator swings the instrument
                // so the star brushes the Moon's edge at its nearest point. So what counts is how far
                // from the Moon's centre the crosshair is, not in which direction: the near edge is
                // short of the centre by that much, the far edge beyond it.
                Vector3 starDir = LockedDirection(day, hour);
                double centreAngle = Vector3.Angle(starDir, moonDir);
                bool nearSide = Vector3.Dot(aim - moonDir, starDir - moonDir) >= 0f;
                double dist = nearSide ? centreAngle - moonAngle : centreAngle + moonAngle;
                // for checking sights afterwards: what was read, and the truth at that moment
                double centre = Lunar.Distance(lunarLock, day, hour);
                Debug.Log($"RealisticSkies: lunar sight day {day} {hour:0.0000}h, {Lunar.Name(lunarLock)}: read{Celestial.Format(dist)}', " +
                          $"centre{Celestial.Format(centre)}', aim{Celestial.Format(moonAngle)}' off the Moon's centre, SD{Celestial.Format(sd)}'");
                UISoundPlayer.instance.PlayOpenSound();
                Notify($"{Lunar.Name(lunarLock)} to the Moon's edge:{Celestial.Format(dist)}'");
                NavJournal.NoteLunar(Math.Round(dist * 600.0) / 600.0);     // which body and which edge: the navigator says
                return;
            }
            if (lunarLock != NoLock && moonAngle < LunarSnap)
            {
                Notify("Bring the Moon's edge right into the crosshair");
                return;
            }

            // nearest star from the Almanac catalogue, or the sun
            int best = NoLock;
            float bestAngle = LunarSnap;

            Vector3 sunDir = -Sun.sun.transform.parent.forward;
            if (sunDir.y > 0f && Vector3.Angle(aim, sunDir) < bestAngle)
            {
                best = Lunar.SunIndex;
                bestAngle = Vector3.Angle(aim, sunDir);
            }
            for (int i = 0; i < Lunar.Stars.Count; i++)
            {
                Lunar.BodyGHADec(i, day, hour, out double gha, out double dec);
                Vector3 dir = Lunar.WorldDirection(gha, dec, hour);
                if (dir.y <= 0f)
                {
                    continue;
                }
                float a = Vector3.Angle(aim, dir);
                if (a < bestAngle)
                {
                    best = i;
                    bestAngle = a;
                }
            }

            if (best != NoLock)
            {
                lunarLock = best;
                UISoundPlayer.instance.PlayOpenSound();
                Notify((best == Lunar.SunIndex ? "Locked on the Sun" : "Locked on the star") + "\nbring the Moon's edge to it and click");
            }
            else if (moonAngle < LunarSnap)
            {
                Notify("Click a star or the Sun first");
            }
            else
            {
                Notify("No star near the crosshair");
            }
        }

        /// <summary>Where the locked star (or the sun) stands in the sky now: it keeps moving while you aim.</summary>
        private Vector3 LockedDirection(int day, double hour)
        {
            if (lunarLock == Lunar.SunIndex) return -Sun.sun.transform.parent.forward;
            Lunar.BodyGHADec(lunarLock, day, hour, out double gha, out double dec);
            return Lunar.WorldDirection(gha, dec, hour);
        }

        private float horizonNear = -1f, horizonFov = -1f;     // the horizon camera's own settings, put back after lunar sights

        // The light path of the quintant (read off the bundle's "702 quintant" prefab):
        //   eyepiece camera (scope) -> "index mirror", a split quad covering the right of the tube,
        //   showing the index camera's picture -> index camera, looking back and down -> "horizon
        //   mirror", a quad on the index arm, showing the horizon camera's picture.
        // Corners of the two quads in their own space (x as the game has it) with the texture
        // coordinates on them; only their face toward the camera that looks at them is used.
        private static readonly Vector3[] IndexQuad = { new Vector3(0.0390f, -0.030070f, 0.070945f), new Vector3(0f, -0.030070f, 0.070945f), new Vector3(0f, 0.030070f, 0.049055f) };
        private static readonly Vector2[] IndexUV = { new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f) };
        private static readonly Vector3[] HorizonQuad = { new Vector3(-0.0690f, -0.048864f, 0.017785f), new Vector3(-0.0270f, -0.048864f, 0.017785f), new Vector3(-0.0270f, 0.048864f, -0.017785f) };
        private static readonly Vector2[] HorizonUV = { new Vector2(0.423077f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f) };
        // where the reflected star sits in the eyepiece picture: at the crosshair, just inside the
        // mirror half so the mirror's edge doesn't cut it (viewport fractions)
        private const float StarRightOfLine = 0.006f, StarAboveLine = 0f;

        // A fine crosshair with a gap at the middle, one of the sights chosen with the scroll wheel: the
        // quintant's own (part of its "hud" plate, lines some 20' thick at that distance) hides a
        // star at the crosshair. It is carried by a third plate like the quintant's own (the same ring
        // and hinge, without the bars), which swings in and out of the view as they do. The lines are
        // soft-edged (a texture with an alpha falloff, mipmapped) so they don't crawl as the picture moves.
        private GameObject fineCross;
        private bool fineCrossOn;
        private float fineSwing = 1f;                        // 0 = in the view, 1 = swung away
        private bool fineSwinging;
        private float fineSwingStart;
        private const float FinePlateZ = -0.032f;            // the quintant's plates are at -0.042 and -0.052
        private const float CrossWidth = 4f / 60f;           // degrees, the quad; the dark core is about half of it
        private const float CrossGap = 9f / 60f;             // degrees, from the centre to where the lines start
        private const float RingInner = 0.0305f;             // metres, the lines run in under the ring
        private const float CrossSwingAngle = 110f;          // as the quintant's plates
        private static Texture2D crossTex;
        private static Mesh ringMesh;

        private static Texture2D CrossTexture()
        {
            if (crossTex != null) return crossTex;
            const int W = 32, H = 32;
            crossTex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "fine crosshair", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                // across the line: a flat core with smooth shoulders
                float v = Mathf.Abs((y + 0.5f) / H - 0.5f) * 2f;               // 0 at the middle, 1 at the edge
                float across = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.95f, v));
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W;
                    float along = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.04f));
                    px[y * W + x] = new Color32(0, 0, 0, (byte)(255f * across * along * 0.92f + 0.5f));
                }
            }
            crossTex.SetPixels32(px);
            crossTex.Apply(true, true);
            return crossTex;
        }

        private static Mesh RingMesh()
        {
            if (ringMesh != null) return ringMesh;
            int n = HudRingMesh.Vertices.Length / 3;
            var v = new Vector3[n]; var nm = new Vector3[n]; var uv = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector3(HudRingMesh.Vertices[3 * i], HudRingMesh.Vertices[3 * i + 1], HudRingMesh.Vertices[3 * i + 2]);
                nm[i] = new Vector3(HudRingMesh.Normals[3 * i], HudRingMesh.Normals[3 * i + 1], HudRingMesh.Normals[3 * i + 2]);
                uv[i] = new Vector2(HudRingMesh.UV[2 * i], HudRingMesh.UV[2 * i + 1]);
            }
            ringMesh = new Mesh { name = "fine crosshair plate", vertices = v, normals = nm, uv = uv, triangles = HudRingMesh.Triangles };
            ringMesh.RecalculateBounds();
            return ringMesh;
        }

        private void ShowFineCross(bool held)
        {
            if (fineCross == null && hud != null && ocularCam != null)
            {
                Camera oc = ocularCam.GetComponent<Camera>();
                fineCross = new GameObject("fine crosshair plate");
                fineCross.layer = hud.gameObject.layer;
                fineCross.transform.SetParent(hud.parent, false);
                fineCross.transform.localPosition = new Vector3(hud.localPosition.x, hud.localPosition.y, FinePlateZ);
                fineCross.transform.localRotation = hudClosed;
                fineCross.transform.localScale = hud.localScale;
                fineCross.AddComponent<MeshFilter>().sharedMesh = RingMesh();
                var pr = fineCross.AddComponent<MeshRenderer>();
                Renderer hr = hud.GetComponent<Renderer>();
                if (hr != null) pr.sharedMaterials = hr.sharedMaterials;
                if (hr != null) { pr.shadowCastingMode = hr.shadowCastingMode; pr.receiveShadows = hr.receiveShadows; }

                // the lines, on the plate in the middle of the ring, sized for their distance from the eyepiece
                Vector3 centreLocal = new Vector3(-0.029f, -0.029f, 0f);
                Vector3 centre = fineCross.transform.TransformPoint(centreLocal);
                float dist = Mathf.Max(0.05f, Vector3.Dot(centre - ocularCam.transform.position, ocularCam.transform.forward));
                float scale = Mathf.Max(1e-4f, fineCross.transform.lossyScale.x);
                float w = dist * Mathf.Tan(CrossWidth * Mathf.Deg2Rad) / scale;
                float gap = dist * Mathf.Tan(CrossGap * Mathf.Deg2Rad) / scale;
                float len = RingInner - gap;
                Shader sh = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
                var mat = new Material(sh) { mainTexture = CrossTexture(), color = Color.white, name = "fine crosshair" };
                mat.renderQueue = 3000;
                var lines = new GameObject("lines").transform;
                lines.SetParent(fineCross.transform, false);
                lines.localPosition = centreLocal;
                lines.localRotation = Quaternion.Inverse(fineCross.transform.rotation) * ocularCam.transform.rotation;   // square to the eyepiece
                Vector3[] centres = { new Vector3(gap + len / 2f, 0, 0), new Vector3(-gap - len / 2f, 0, 0), new Vector3(0, gap + len / 2f, 0), new Vector3(0, -gap - len / 2f, 0) };
                for (int i = 0; i < 4; i++)
                {
                    GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    UnityEngine.Object.Destroy(bar.GetComponent<Collider>());
                    bar.transform.SetParent(lines, false);
                    bar.transform.localPosition = centres[i];
                    bar.transform.localRotation = i < 2 ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
                    bar.transform.localScale = new Vector3(len, w, 1f);
                    bar.layer = ocularCam.gameObject.layer;
                    Renderer br = bar.GetComponent<Renderer>();
                    br.sharedMaterial = mat;
                    br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    br.receiveShadows = false;
                }
                if (!chainLogged) Debug.Log($"RealisticSkies: fine crosshair plate {dist:F3} m from the eyepiece, lines {w * scale * 1000f:F2} mm");
            }
            if (fineSwinging && Time.time - fineSwingStart > 1f) fineSwinging = false;   // a swing cut short (the quintant put away)
            if (fineCross == null) return;
            if (hudLevel > 0 && !fineCrossOn && !fineSwinging) fineCrossOn = true;   // under the quintant's plates it is always in
            if (!fineSwinging) fineSwing = fineCrossOn ? 0f : 1f;
            fineCross.transform.localRotation = hudClosed * Quaternion.Euler(0f, 0f, -CrossSwingAngle * fineSwing);
        }

        /// <summary>Swings the fine crosshair in (true) or away, over 0.2 s as the plates go; "owns" when it is the only thing moving.</summary>
        private IEnumerator SwingFineCross(bool swingIn, bool owns)
        {
            if (owns) moving = true;
            fineSwinging = true;
            fineSwingStart = Time.time;
            float from = fineSwing, to = swingIn ? 0f : 1f;
            for (float t = 0f; t <= 1f; t += Time.deltaTime * 5f)
            {
                fineSwing = Mathf.Lerp(from, to, t);
                yield return new WaitForEndOfFrame();
            }
            fineSwing = to;
            fineSwinging = false;
            if (owns)
            {
                UISoundPlayer.instance.PlayOpenSound();
                moving = false;
            }
        }
        private static bool chainLogged;

        /// <summary>Where a ray meets a quad's plane, in the quad's texture coordinates.</summary>
        private static bool QuadUV(Transform t, Vector3[] q, Vector2[] uv, Ray ray, out Vector2 at)
        {
            at = Vector2.zero;
            Vector3 a = t.TransformPoint(q[0]), b = t.TransformPoint(q[1]), c = t.TransformPoint(q[2]);
            Vector3 e1 = b - a, e2 = c - a, n = Vector3.Cross(e1, e2);
            float den = Vector3.Dot(n, ray.direction);
            if (Mathf.Abs(den) < 1e-12f) return false;
            float dist = Vector3.Dot(n, a - ray.origin) / den;
            if (dist <= 0f) return false;
            Vector3 d = ray.origin + ray.direction * dist - a;
            float d11 = Vector3.Dot(e1, e1), d12 = Vector3.Dot(e1, e2), d22 = Vector3.Dot(e2, e2);
            float b1 = Vector3.Dot(d, e1), b2 = Vector3.Dot(d, e2), det = d11 * d22 - d12 * d12;
            if (Mathf.Abs(det) < 1e-18f) return false;
            float al = (b1 * d22 - b2 * d12) / det, be = (b2 * d11 - b1 * d12) / det;
            at = uv[0] + al * (uv[1] - uv[0]) + be * (uv[2] - uv[0]);
            return true;
        }

        /// <summary>
        /// Lunar mode with a star locked: the horizon glass shows the locked star at the eyepiece's
        /// crosshair (a fine one with a gap in the middle, so the star shows), kept there as the sky
        /// turns while you swing the quintant to the Moon, as the index mirror of a sextant keeps the
        /// star. Bring the Moon's edge, in the clear half, to the star at the crosshair: the sight is
        /// read there.
        /// The spot is followed back along the quintant's own light path every frame (the arm tilts):
        /// eyepiece -> index mirror -> index camera -> horizon mirror -> horizon camera's picture.
        /// The camera's near plane is pushed out so it doesn't see the quintant's own frame.
        /// </summary>
        private void ReflectLockedStar()
        {
            Camera hc = horizonCam != null ? horizonCam.GetComponent<Camera>() : null;
            Camera oc = ocularCam != null ? ocularCam.GetComponent<Camera>() : null;
            Camera ic = indexCam != null ? indexCam.GetComponent<Camera>() : null;
            if (hc != null && horizonNear < 0f) { horizonNear = hc.nearClipPlane; horizonFov = hc.fieldOfView; }
            bool active = lunarMode && lunarLock != NoLock && !inspecting && !rotating && held && hc != null && oc != null && ic != null
                          && indexRenderer != null && horizonRenderer != null && Celestial.HaveSkyAxes;
            this.ShowFineCross(held && !inspecting);
            if (!active)
            {
                if (hc != null && hc.nearClipPlane != horizonNear) hc.nearClipPlane = horizonNear;
                return;
            }
            hc.nearClipPlane = Mathf.Max(horizonNear, 3f);
            horizonMaterial.mainTextureScale = Vector2.one;
            horizonMaterial.mainTextureOffset = Vector2.zero;

            // follow the crosshair's light back to the horizon camera's picture
            Ray eye = oc.ViewportPointToRay(new Vector3(0.5f + StarRightOfLine, 0.5f + StarAboveLine, 0f));
            if (!QuadUV(indexRenderer.transform, IndexQuad, IndexUV, eye, out Vector2 iuv)) return;
            Vector2 ivp = indexMaterial.mainTextureOffset + Vector2.Scale(indexMaterial.mainTextureScale, iuv);
            Ray back = ic.ViewportPointToRay(new Vector3(ivp.x, ivp.y, 0f));
            if (!QuadUV(horizonRenderer.transform, HorizonQuad, HorizonUV, back, out Vector2 hvp)) return;

            // turn the camera so the star falls on that spot of its picture
            float vfov = hc.fieldOfView;
            float hfov = Camera.VerticalToHorizontalFieldOfView(vfov, hc.aspect);
            float yaw = Mathf.Atan((2f * hvp.x - 1f) * Mathf.Tan(hfov * 0.5f * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan((2f * hvp.y - 1f) * Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
            Vector3 star = LockedDirection(GameState.day, Sun.sun.globalTime);
            horizonCam.transform.rotation = Quaternion.LookRotation(star, horizonCam.transform.up) * Quaternion.Euler(pitch, -yaw, 0f);

            if (!chainLogged)
            {
                chainLogged = true;
                Debug.Log($"RealisticSkies: quintant light path: index mirror uv {iuv}, index picture {ivp} (material {indexMaterial.mainTextureScale} {indexMaterial.mainTextureOffset}), " +
                          $"horizon picture {hvp}, index cam fov {ic.fieldOfView:0.#} aspect {ic.aspect:0.###}");
            }
        }

        public override void ExtraLateUpdate()
        {
            if (held && sold && !inspecting) InstrumentAim.Mark();
            if (held && Plugin.configLunarKey.Value.IsDown())
            {
                this.ToggleLunarMode();
            }

            // without the built-in clock the time has to come from a chronometer or lunar distances
            if (!Mode.QuintantClock && this.clock != null && this.clock.activeSelf)
            {
                this.clock.SetActive(false);
            }

            if (!inspecting && held)
            {
                if (Sun.sun.localTime > 18f || Sun.sun.localTime < 6f)
                {   
                    // if it's night, we don't need to blind the player in any case
                }
                else
                {
                    Vector3 sunDirection = -Sun.sun.transform.forward;
                    Vector3 lookDirDirection = ocularCam.transform.forward;

                    float angle = Vector3.Angle(sunDirection, lookDirDirection);

                    if (angle < 35f)
                    {
                        BlindPlayer(angle);
                    }
                }
            }


            if (!inspecting && !rotating && held)
            {
                dial.localEulerAngles = Vector3.zero;
                Vector3 eulerAngles = dial.eulerAngles;
                if (true)
                {
                    eulerAngles.x = 0f;
                }
                if (lockY)
                {
                    eulerAngles.y = 0f;
                }
                if (lockZ)
                {
                    eulerAngles.z = 0f;
                }
                dial.eulerAngles = eulerAngles;
                eulerAngles = dial.localEulerAngles;
                if (eulerAngles.x < 0f || eulerAngles.x > 180f)
                {
                    eulerAngles.x = 0f;
                }
                else if (eulerAngles.x > 72f)
                {
                    eulerAngles.x = 72f;
                }
                Vector3 eulerAngles2 = eulerAngles;
                Vector3 eulerAngles3 = eulerAngles;
                eulerAngles2.x = 0.5f * eulerAngles.x;

                dial.localEulerAngles = eulerAngles;
                mirror.localEulerAngles = eulerAngles2;
                horizonCam.transform.localEulerAngles = eulerAngles2;

                float Scale = Mathf.Cos(Mathf.Deg2Rad * (eulerAngles2.x + 20.0f));
                float Offset = 0.5f - 0.5f * Scale - 0.0301536896f;
                horizonMaterial.mainTextureScale = new Vector2(1.0f, Scale);
                horizonMaterial.mainTextureOffset = new Vector2(0.0f, Offset);
            }

            this.ReflectLockedStar();
            this.ScheduleLunarView();
        }

        // ---------------- the quintant turned into the plane of the Moon and the star ----------------

        private static ModItemQuintant lunarHeld;   // the quintant being turned, if any
        private static bool beforeRenderHooked;
        private float lunarRoll;                    // degrees about the line of sight, now
        private bool rolled;                        // turned for this frame's picture: put back after it
        private Vector3 unrolledPos;
        private Quaternion unrolledRot;
        private Coroutine unroller;

        /// <summary>
        /// For a lunar distance a sextant is held on its side, its frame in the plane of the Moon and
        /// the star: the Moon seen straight through, the star brought down in the mirror. With a
        /// star locked the quintant turns itself about the line of sight into that plane (smoothly,
        /// and back upright when the lock goes). The turn has to come after the game has put the held
        /// item in place for the frame, so it is done just before rendering, and the star in the glass
        /// is placed again after it.
        /// </summary>
        private void ScheduleLunarView()
        {
            if (!beforeRenderHooked)
            {
                beforeRenderHooked = true;
                UnityEngine.Application.onBeforeRender += BeforeRender;
            }
            if (held && (lunarMode || Mathf.Abs(lunarRoll) > 0.01f)) lunarHeld = this;
        }

        private static void BeforeRender()
        {
            ModItemQuintant q = lunarHeld;
            if (q == null) return;
            if (!q.held)
            {
                q.lunarRoll = 0f;
                lunarHeld = null;
                return;
            }
            q.ApplyLunarRoll();
            q.ReflectLockedStar();
            if (!q.lunarMode && Mathf.Abs(q.lunarRoll) <= 0.01f) lunarHeld = null;
        }

        private void ApplyLunarRoll()
        {
            Transform pt = held.transform;
            float target = 0f;
            if (lunarMode && lunarLock != NoLock && !inspecting && !rotating && Celestial.HaveSkyAxes && Sun.sun != null)
            {
                // the star, seen from the line of sight: the quintant's foot (where the mirror looks) toward it
                Vector3 star = Vector3.ProjectOnPlane(LockedDirection(GameState.day, Sun.sun.globalTime), pt.forward);
                if (star.sqrMagnitude > 1e-6f) target = Vector3.SignedAngle(-pt.up, star, pt.forward);
            }
            lunarRoll = Mathf.MoveTowardsAngle(lunarRoll, target, 200f * Time.unscaledDeltaTime);
            if (Mathf.Abs(lunarRoll) < 0.01f) return;
            // The turn is only for the picture: once the frame is drawn the quintant is put back as the
            // game had it, so its own update (the arm and mirror, set from how it is held) never sees
            // a turned quintant (that sent the arm spinning).
            unrolledPos = base.transform.position;
            unrolledRot = base.transform.rotation;
            rolled = true;
            if (unroller == null) unroller = StartCoroutine(PutBackAfterFrame());
            Quaternion turn = Quaternion.AngleAxis(lunarRoll, pt.forward);
            base.transform.position = pt.position + turn * (base.transform.position - pt.position);
            base.transform.rotation = turn * base.transform.rotation;
        }

        private IEnumerator PutBackAfterFrame()
        {
            var end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;
                if (rolled)
                {
                    rolled = false;
                    base.transform.position = unrolledPos;
                    base.transform.rotation = unrolledRot;
                }
            }
        }


        private static readonly FieldInfo finalParticlesInfo = AccessTools.Field(typeof(Weather), "finalParticles");

        private int DetectWeather()
        {   //returns the shade level needed for the current cloudiness (0: all shades closed, 3: no shades closed)

            //use reflection to get the cloud density, no other way to get that value!
            WeatherParticlesSettings finalParticles = (WeatherParticlesSettings)finalParticlesInfo.GetValue(Weather.instance);
            float cloudsDensity = finalParticles.cloudDensity;
            //Debug.LogWarning("Debug: cloudDensity: " + cloudsDensity);  //DEBUG: this is going to be needed to find the values to tweak this stuff

            if (cloudsDensity < 0.1f)
            {   //clear weather
                return 3;
            }
            if (cloudsDensity >= 0.1f && cloudsDensity < 0.5f)
            {   //cloudy weather
                return 2;
            }
            if (cloudsDensity >= 0.5f && cloudsDensity < 1f)
            {   //cloudier weather
                return 1;
            }
            if (cloudsDensity >= 1f)
            {   //stormy weather
                return 0;
            }
            return 0;
        }

        private void BlindPlayer(float angle)
        {
            if (shadeLevel >= DetectWeather())
            {   //(0: no shades closed, 3: all shades closed)
                return;
            }
            float t;
            if (angle >= 35f)
            {
                t = 0;
            }
            else
            {
                t = 1 - angle * angle / 1225f;
            }

            //Bloom
            BloomModel.Settings bloomSettings = postProcessing.bloom.settings;
            bloomSettings.bloom.threshold = Mathf.Lerp(1.05f, 0f, t);
            bloomSettings.bloom.radius = Mathf.Lerp(2.5f, 5f, t);
            postProcessing.bloom.settings = bloomSettings;

            //Color Grading
            ColorGradingModel.Settings colorSettings = postProcessing.colorGrading.settings;
            colorSettings.basic.postExposure = Mathf.Lerp(0.66f, -1f, t);
            postProcessing.colorGrading.settings = colorSettings;
        }


    }
}
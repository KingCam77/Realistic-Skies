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
    public class ModItemQuintant : ShipItem
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

        public override void OnAltActivate()
        {
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

                var deg = Mathf.Floor(this.dial.localEulerAngles.x);
                var arcMin = Math.Round((this.dial.localEulerAngles.x - Mathf.Floor(this.dial.localEulerAngles.x)) * 60, 1);

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
                    bool flag3 = input < 0f;
                    if (flag3)
                    {
                        base.StartCoroutine(this.MoveHud(false));
                    }
                    else
                    {
                        bool flag4 = input > 0f;
                        if (flag4)
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

        public override void ExtraLateUpdate()
        {
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
        }


        private int DetectWeather()
        {   //returns the shade level needed for the current cloudiness (0: all shades closed, 3: no shades closed)

            //use reflection to get the cloud density, no other way to get that value!
            //DEBUG: The FieldInfo should be cached in OnLoad(), no need to do it everytime here...
            FieldInfo finalParticlesInfo = AccessTools.Field(typeof(Weather), "finalParticles");
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
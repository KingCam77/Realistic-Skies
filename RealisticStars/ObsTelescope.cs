using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.PostProcessing;
using static System.Net.Mime.MediaTypeNames;

namespace RealisticSkies
{
    public class ObsTelescope : GoPointerButton
    {
        public static float adjustDec;
        public static float adjustSha;

        public static Transform scope;
        public static Transform turret;
        public static Transform dome;

        public static Transform target;

        public static Quaternion initialRotTurret;

        public static float DEC;
        public static float SHA;

        private static GameObject textDEC;
        private static GameObject textSHA;


        // I wish i could find a functionto do this but this might be the best implimentation
        private static float[] minPoints = [-70.4f,-70.7f,-70.8f,-70.8f,-70.7f,
                                            -70.4f,-70.0f,-69.3f,-68.5f,-67.3f,
                                            -65.9f,-64.0f,-61.7f,-58.6f,-54.7f,
                                            -49.8f,-43.5f,-35.8f,-27.1f,-17.3f,
                                            -07.5f, 02.0f, 10.6f, 18.1f, 24.5f,
                                             30.0f, 34.6f, 38.5f, 41.7f, 44.4f,
                                             46.7f, 48.6f, 50.1f, 51.3f, 52.3f,
                                             52.9f, 53.4f, 53.6f, 53.6f, 53.3f,
                                             52.8f, 52.1f, 51.0f, 49.6f, 47.8f,
                                             45.5f, 42.7f, 39.2f, 35.0f, 30.0f,
                                             23.8f, 16.6f, 08.3f,-00.7f,-10.0f,
                                            -19.2f,-27.6f,-35.1f,-41.4f,-46.8f,
                                            -51.2f,-54.8f,-57.9f,-60.4f,-62.5f,
                                            -64.2f,-65.7f,-67.0f,-67.9f,-68.8f,
                                            -69.4f,-70.0f,-70.4f];

        private void Awake()
        {
            ObsTelescope.turret = base.transform.GetChild(0);
            ObsTelescope.scope = ObsTelescope.turret.GetChild(0);
            ObsTelescope.target = ObsTelescope.scope.GetChild(0);
            ObsTelescope.dome = base.transform.parent.GetChild(4);

            ObsTelescope.textDEC = base.transform.GetChild(1).gameObject;
            ObsTelescope.textSHA = base.transform.GetChild(2).gameObject;

            ObsTelescope.initialRotTurret = ObsTelescope.turret.rotation;
            DEC = -4.9f;
            SHA = 272.5f;
        }

        public override void OnActivate(GoPointer activatingPointer)
        {
            Debug.Log("telescope onactivate");
            if (activatingPointer.type == GoPointer.PointerType.crosshairMouse)
            {
                StickyClick(activatingPointer);
            }
            UISoundPlayer.instance.PlayUISound(UISounds.winchClick, 0.33f, 0.85f);
        }

        public override void OnUnactivate(GoPointer activatingPointer)
        {
            Debug.Log(" telescope onUNactivate");
        }

        private void Update()
        {
            if ((bool)stickyClickedBy || isClicked)
            {
                if ((bool)stickyClickedBy && Time.deltaTime != 0.0f)
                {
                    adjustDec = stickyClickedBy.movement.GetKeyboardDelta().y;
                    adjustDec /= Time.deltaTime;
                    adjustDec *= -0.00004f;

                    adjustSha = stickyClickedBy.movement.GetKeyboardDelta().x;
                    adjustSha /= Time.deltaTime;
                    adjustSha *= -0.00004f;

                }
            }
            else
            {
                adjustSha = 0f;
                adjustDec = 0f;
            }

            DEC += adjustDec;

            SHA += adjustSha;

            if (SHA < 0)
            {
                SHA += 360;
            }

            SHA = SHA % 360.0f;

            if (DEC > 80.0f)
            {
                DEC = 80.0f;
            }
            if (DEC < -70.0f)
            {
                DEC = -70.0f;
            }


            float rotateVal = Patches.time * 15.0f + 2.91713f;

            if (Plugin.configSiderialToggle.Value && !Plugin.configDisable.Value)
            {
                rotateVal += 360.0f * (Patches.year % 1);
            }


            ObsTelescope.turret.rotation = ObsTelescope.initialRotTurret;
            ObsTelescope.turret.Rotate(Vector3.up, SHA + rotateVal, Space.Self);

            float x = 186.0f - Mathf.Abs((SHA + rotateVal) % 360.0f - 186.0f);

            float DECuse = DEC;

            if (DEC < 0.8f * x - 68.0f)
            {
                int index = Mathf.FloorToInt(((SHA + rotateVal) % 360.0f) / 5.0f);
                float inter = ((SHA + rotateVal) % 5.0f) / 5.0f;

                float decMin = Mathf.Lerp(minPoints[index], minPoints[index+1], inter);

                if (DEC < decMin)
                {
                    DECuse = decMin;
                }
            }



            ObsTelescope.scope.localRotation = Quaternion.Euler(0, 0, DECuse);

            Vector3 targetPostition = new Vector3(target.position.x, ObsTelescope.dome.transform.position.y, target.position.z);
            ObsTelescope.dome.LookAt(targetPostition, Vector3.up);

            ObsTelescope.textDEC.GetComponent<TextMesh>().text = degToDisp(DEC, false);
            ObsTelescope.textSHA.GetComponent<TextMesh>().text = degToDisp(SHA, false);


        }

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


        private IEnumerator SmoothlyRotate(Quaternion targetRot, Transform obj)
        {
            Quaternion startRot = obj.localRotation;
            for (float t = 0f; t <= 1f; t += Time.deltaTime * 5f)
            {
                obj.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return new WaitForEndOfFrame();
            }
            obj.localRotation = targetRot;
        }
    }
}
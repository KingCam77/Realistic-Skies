using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace RealisticSkies
{

    public class ModItemPlanisphere : ShipItem
    {
        // Scroll: change angle
        // Inspect: flip over

        static bool constructed;

        private float rotateVal;

        private bool inspecting;

        private bool rotating;

        private bool rotatingDisk;

        private Transform frame;

        private Transform disk;

        private Quaternion initialRot;

        private Quaternion inspectRot;

        private Camera playerCam;

        private ModItemPlanisphere()
        {
            if (!constructed)
            {
                base.value = 1000;
                base.name = "planisphere";
                base.category = TransactionCategory.toolsAndSupplies;
                base.inventoryScale = 1f;
                base.inventoryRotation = -180f;
                base.furniturePlaceHeight = 0.5f;
                constructed = true;
            }

        }

        public override void OnLoad()
        {
            this.frame = base.transform.GetChild(0);
            this.disk = this.frame.GetChild(0);
            this.rotateVal = 0;
            

            this.initialRot = this.frame.localRotation;
            this.inspectRot = this.initialRot * Quaternion.Euler(Vector3.up * -180f);
            this.playerCam = GameObject.Find("Outline Camera").GetComponent<Camera>();

            base.holdDistance = 143.9f * Mathf.Pow(this.playerCam.fieldOfView, -1.38f);
        }


        public override void OnAltActivate()
        {
            if (!sold)
            {
                base.OnAltActivate();
            }
            else
            {
                inspecting = !inspecting;
                if (inspecting)
                {
                    StartCoroutine(SmoothlyRotate(inspectRot));
                }
                else
                {
                    StartCoroutine(SmoothlyRotate(initialRot));
                }
            }
        }

        public override void OnScroll(float input)
        {
            if (!rotatingDisk)
            {
                bool flag3 = input < 0f;
                if (flag3)
                {
                    if (inspecting)
                    {
                        this.rotateVal -= 0.4891304348f;
                    }
                    else
                    {
                        this.rotateVal -= 15.16304348f;
                    }
                }
                else
                {
                    bool flag4 = input > 0f;
                    if (flag4)
                    {
                        if (inspecting)
                        {
                            this.rotateVal += 0.4891304348f;
                        }
                        else
                        {
                            this.rotateVal += 15.16304348f;
                        }
                    }
                }

                if (!inspecting)
                {
                    StartCoroutine(SmoothlyRotateDisk(Quaternion.Euler(0.0f, 0.0f, this.rotateVal), 20f));
                }
                else
                {
                    this.disk.localRotation = Quaternion.Euler(0.0f, 0.0f, this.rotateVal);
                }
            }
        }

        private IEnumerator SmoothlyRotate(Quaternion targetRot)
        {
            rotating = true;
            Quaternion startRot = this.frame.localRotation;
            for (float t = 0f; t <= 1f; t += Time.deltaTime * 5f)
            {
                this.frame.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return new WaitForEndOfFrame();
            }
            this.frame.localRotation = targetRot;
            rotating = false;
        }

        private IEnumerator SmoothlyRotateDisk(Quaternion targetRot, float speed)
        {
            rotatingDisk = true;
            Quaternion startRot = this.disk.localRotation;
            for (float t = 0f; t <= 1f; t += Time.deltaTime * speed)
            {
                this.disk.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return new WaitForEndOfFrame();
            }
            this.disk.localRotation = targetRot;
            rotatingDisk = false;
        }

    }
}
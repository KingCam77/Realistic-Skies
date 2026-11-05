using System;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Celestial Atlas (prefab 706): a book whose pages are the textures listed in its
    /// ScrollDirectory. Restored from the released 2.3.0 build, which was never pushed to GitHub.
    /// </summary>
    public class ModItemCharts : ShipItem
    {
        private GameObject closedEmpty;

        private GameObject openEmpty;

        private Texture[] pages;

        private ScrollDirectory directory;

        private Renderer pageRenderer;

        private Mesh closedMesh;

        private Mesh openMesh;

        private Vector3 closedColSize = new Vector3(0.55f, 0.76f, 0.1f);

        private Vector3 openColSize = new Vector3(1.18f, 0.76f, 0.08f);

        private int page;

        private int pagecount;

        static bool constructed;

        private Camera playerCam;

        private ModItemCharts()
        {
            if (!constructed)
            {
                base.value = 4000;
                base.name = "Celestial Atlas";
                base.category = TransactionCategory.toolsAndSupplies;
                base.inventoryScale = 0.7f;
                base.inventoryRotation = -180f;
                base.furniturePlaceHeight = 0.5f;
                constructed = true;
            }
        }

        public override void OnLoad()
        {
            this.directory = base.GetComponent<ScrollDirectory>();
            this.pages = this.directory.scrolls[0].pages;
            this.pagecount = this.pages.Length;

            this.pageRenderer = base.transform.GetChild(1).GetChild(3).GetComponent<MeshRenderer>();
            this.pageRenderer.material.mainTexture = this.pages[0];
            this.pageRenderer.enabled = true;

            this.closedEmpty = base.transform.GetChild(0).gameObject;
            this.openEmpty = base.transform.GetChild(1).gameObject;

            this.playerCam = GameObject.Find("Outline Camera").GetComponent<Camera>();

            this.closedMesh = base.transform.GetChild(0).GetChild(0).gameObject.GetComponent<MeshFilter>().sharedMesh;
            this.openMesh = base.transform.GetChild(1).GetChild(0).gameObject.GetComponent<MeshFilter>().sharedMesh;

            base.holdDistance = 0.75f;

            //page is stored in the amount value
            // (the released build set the page after Open(), which made a saved open atlas load page 0)
            if (base.amount == 0f)
            {
                base.amount = -1.0f;
            }
            this.page = Mathf.Clamp((int)Mathf.Abs(base.amount), 1, Mathf.Max(1, this.pagecount));

            if (base.amount > 0f)
            {
                this.Open();
            }
            else
            {
                this.Close();
            }
        }

        public override void OnScroll(float input)
        {
            if (base.amount > 0f)
            {
                if (input < 0f)
                {
                    this.turnPage(true);
                }
                else if (input > 0f)
                {
                    this.turnPage(false);
                }
            }
            else
            {
                base.OnScroll(input);
            }
        }

        private void turnPage(bool forward)
        {
            if (forward)
            {
                if (this.page < this.pagecount)
                {
                    this.page++;
                    UISoundPlayer.instance.PlayParchmentSound();
                    this.loadPage(this.page);
                }
            }
            else
            {
                if (this.page > 1)
                {
                    this.page--;
                    UISoundPlayer.instance.PlayParchmentSound();
                    this.loadPage(this.page);
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

            this.loadPage(this.page);

            base.holdDistance = 181.6f * Mathf.Pow(this.playerCam.fieldOfView, -1.38f);

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
            if (!base.sold)
            {
                base.OnAltActivate();
                return;
            }
            if (base.amount < 0f)
            {
                this.Open();
                base.heldRotationOffset = 0f;
            }
            else
            {
                this.Close();
            }
            UISoundPlayer.instance.PlayParchmentSound();
        }

        private void loadPage(int page)
        {
            if (page < 1 || page > this.pages.Length)
            {
                return;
            }
            this.pageRenderer.material.mainTexture = this.pages[page - 1];
        }
    }
}

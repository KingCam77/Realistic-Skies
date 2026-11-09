using System.Collections;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Turns the game's own camera (and brings its clip planes in) for the drawing of one frame and puts
    /// it back at the end of that frame. A second camera would lose the game's colour grading and
    /// effects; this way the view is the game's, and the game's code never finds its camera changed.
    /// Called from Camera.onPreCull, before the frame is drawn; again every frame for as long as it is
    /// wanted.
    /// </summary>
    internal class CameraCarry : MonoBehaviour
    {
        private static CameraCarry inst;
        private Camera cam;
        private Vector3 pos;
        private Quaternion rot;
        private float fov, near, far;
        private bool carried;

        /// <summary>The camera is changed this frame.</summary>
        internal static bool Carried => inst != null && inst.carried;

        internal static void Carry(Camera c, Vector3 p, Quaternion r, float fieldOfView, float nearClip, float farClip)
        {
            if (c == null) return;
            if (inst == null)
            {
                var go = new GameObject("RealisticSkies camera carry");
                DontDestroyOnLoad(go);
                inst = go.AddComponent<CameraCarry>();
            }
            if (!inst.carried)
            {
                inst.cam = c;
                inst.pos = c.transform.position;
                inst.rot = c.transform.rotation;
                inst.fov = c.fieldOfView;
                inst.near = c.nearClipPlane;
                inst.far = c.farClipPlane;
                inst.carried = true;
                inst.StartCoroutine(inst.PutBack());
            }
            c.transform.SetPositionAndRotation(p, r);
            c.fieldOfView = fieldOfView;
            c.nearClipPlane = nearClip;
            c.farClipPlane = farClip;
        }

        private IEnumerator PutBack()
        {
            yield return new WaitForEndOfFrame();
            if (cam != null)
            {
                cam.transform.SetPositionAndRotation(pos, rot);
                cam.fieldOfView = fov;
                cam.nearClipPlane = near;
                cam.farClipPlane = far;
            }
            carried = false;
        }
    }
}

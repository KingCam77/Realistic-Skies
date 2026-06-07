using System;
using UnityEngine;

namespace RealisticSkies
{
    public class Ladder : GoPointerButton
    {

        public override void OnActivate()
        {
            Transform transform = Refs.charController.transform;
            Debug.LogWarning("HarbourLadder: player transform parent is " + transform.parent.gameObject.name);
            bool flag = !(transform.parent.gameObject.name != "_shifting world");
            if (flag)
            {
                transform.position = base.transform.position + Vector3.up * this.upDistance + transform.forward * 0.1f;
            }
        }
        public float upDistance = 4.5f;
    }
}
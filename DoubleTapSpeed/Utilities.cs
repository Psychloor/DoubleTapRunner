namespace DoubleTapSpeed
{

    using UnityEngine;

    public static class Utilities
    {

        public static bool AxisClicked(string axis, ref float previous, float threshold)
        {
            float current = Mathf.Abs(Input.GetAxis(axis));
            bool clicked = current >= threshold && previous < threshold;

            previous = current;
            return clicked;
        }

        public static VRCPlayer GetLocalVRCPlayer()
        {
            return VRCPlayer.field_Internal_Static_VRCPlayer_0;
        }

        public static bool HasDoubleClicked(KeyCode keyCode, ref float lastTimeClicked, float threshold)
        {
            if (!Input.GetKeyDown(keyCode)) return false;
            if (Time.time - lastTimeClicked <= threshold)
            {
                lastTimeClicked = threshold * 2;
                return true;
            }

            lastTimeClicked = Time.time;
            return false;
        }

    }

}
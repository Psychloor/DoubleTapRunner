namespace DoubleTapRunner
{

    using System;
    using System.Linq;
    using System.Reflection;

    using UnhollowerRuntimeLib.XrefScans;

    using UnityEngine;

    using VRC.SDKBase;

    public static class Utilities
    {
        public static bool GetStreamerMode =>
            VRCInputManager.Method_Public_Static_Boolean_EnumNPublicSealedvaUnCoHeToTaThShPeVoUnique_0(
                VRCInputManager.EnumNPublicSealedvaUnCoHeToTaThShPeVoUnique.StreamerModeEnabled);

        public static bool AxisClicked(string axis, ref float previous, float threshold)
        {
            float current = Mathf.Abs(Input.GetAxis(axis));
            bool clicked = current >= threshold && previous < threshold;

            previous = current;
            return clicked;
        }

        public static VRCInputMethod GetLastUsedInputMethod()
        {
            return VRCInputManager.Method_Public_Static_VRCInputMethod_0();
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
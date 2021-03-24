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
        public delegate bool StreamerModeDelegate();

        private static StreamerModeDelegate ourStreamerModeDelegate;

        public static StreamerModeDelegate GetStreamerMode
        {
            get
            {
                if (ourStreamerModeDelegate != null) return ourStreamerModeDelegate;

                foreach (PropertyInfo property in typeof(VRCInputManager).GetProperties(BindingFlags.Public | BindingFlags.Static))
                {
                    if (property.PropertyType != typeof(bool)) continue;
                    if (XrefScanner.XrefScan(property.GetSetMethod()).Any(
                        xref => xref.Type == XrefType.Global && xref.ReadAsObject()?.ToString().Equals("VRC_STREAMER_MODE_ENABLED") == true))
                    {
                        ourStreamerModeDelegate = (StreamerModeDelegate)Delegate.CreateDelegate(typeof(StreamerModeDelegate), property.GetGetMethod());
                        return ourStreamerModeDelegate;
                    }
                }

                return null;
            }
        }

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
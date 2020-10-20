using DoubleTapRunner;

using MelonLoader;

using BuildInfo = DoubleTapRunner.BuildInfo;

[assembly: MelonInfo(typeof(DoubleTapper), BuildInfo.Name, BuildInfo.Version, BuildInfo.Author, BuildInfo.DownloadLink)]
[assembly: MelonGame("VRChat", "VRChat")]

namespace DoubleTapRunner
{

    using System;
    using System.Collections;
    using System.Reflection;

    using Harmony;

    using MelonLoader;

    using UnityEngine;

    using VRC.Core;
    using VRC.SDKBase;

    public class DoubleTapper : MelonMod
    {

        private const string SettingsCategory = "DoubleTapRunner";

        private static DoubleTapper instance;

        // Original Settings
        private static float walkSpeed, runSpeed, strafeSpeed;

        private static bool worldAllowed;

        private Settings activeSettings;

        private static bool currentlyRunning;

        private float lastTimeClicked = 25f;

        private float previousAxis;

        private bool useAxisValues;

        private static bool modLoadedCorrectly = true;

        public override void OnApplicationStart()
        {
            if (instance != null)
            {
                MelonLogger.LogError("There's already an instance of Double-Tap Runner. Remove the duplicate dll files");
                MelonLogger.LogError("Not Guaranteed to work with multiple instances at all");
                return;
            }

            instance = this;

            // Default settings
            activeSettings = new Settings
                                 {
                                     Enabled = true,
                                     SpeedMultiplier = 2f,
                                     DoubleClickTime = .5f,
                                     Forward = KeyCode.W,
                                     Backward = KeyCode.S,
                                     Left = KeyCode.A,
                                     Right = KeyCode.D,
                                     AxisDeadZone = .1f,
                                     AxisClickThreshold = .6f
                                 };

            MelonPrefs.RegisterCategory(SettingsCategory, "Double-Tap Runner");
            MelonPrefs.RegisterBool(SettingsCategory, nameof(Settings.Enabled), activeSettings.Enabled, "Enabled");
            MelonPrefs.RegisterFloat(SettingsCategory, nameof(Settings.SpeedMultiplier), activeSettings.SpeedMultiplier, "Speed Multiplier");
            MelonPrefs.RegisterFloat(SettingsCategory, nameof(Settings.DoubleClickTime), activeSettings.DoubleClickTime, "Double Click Time");

            MelonPrefs.RegisterString(SettingsCategory, nameof(Settings.Forward), Enum.GetName(typeof(KeyCode), activeSettings.Forward), "Desktop Forward");
            MelonPrefs.RegisterString(SettingsCategory, nameof(Settings.Backward), Enum.GetName(typeof(KeyCode), activeSettings.Backward), "Desktop Backward");
            MelonPrefs.RegisterString(SettingsCategory, nameof(Settings.Left), Enum.GetName(typeof(KeyCode), activeSettings.Left), "Desktop Left");
            MelonPrefs.RegisterString(SettingsCategory, nameof(Settings.Right), Enum.GetName(typeof(KeyCode), activeSettings.Right), "Desktop Right");

            MelonPrefs.RegisterFloat(SettingsCategory, nameof(Settings.AxisDeadZone), activeSettings.AxisDeadZone, "Axis Dead Zone");
            MelonPrefs.RegisterFloat(SettingsCategory, nameof(Settings.AxisClickThreshold), activeSettings.AxisClickThreshold, "Axis Click Threshold");
            ApplySettings();

            try
            {
                // way more mod friendly but might mean more updates... oh well, not really hard to find. "Fade to" and check params
                MethodInfo fadeToMethod = typeof(VRCUiManager).GetMethod(
                    nameof(VRCUiManager.Method_Public_Void_String_Single_Action_0),
                    BindingFlags.Public | BindingFlags.Instance);
                harmonyInstance.Patch(
                    fadeToMethod,
                    null,
                    new HarmonyMethod(typeof(DoubleTapper).GetMethod(nameof(JoinedRoomPatch), BindingFlags.NonPublic | BindingFlags.Static)));
            }
            catch (Exception e)
            {
                modLoadedCorrectly = false;
                MelonLogger.LogError("Failed to patch into FadeTo: " + e);
            }

            try
            {
                MethodInfo leaveRoomMethod = typeof(NetworkManager).GetMethod(nameof(NetworkManager.OnLeftRoom), BindingFlags.Public | BindingFlags.Instance);
                harmonyInstance.Patch(
                    leaveRoomMethod,
                    new HarmonyMethod(typeof(DoubleTapper).GetMethod(nameof(LeftRoomPrefix), BindingFlags.NonPublic | BindingFlags.Static)));
            }
            catch (Exception e)
            {
                modLoadedCorrectly = false;
                MelonLogger.LogError("Failed to patch into OnLeftRoom: " + e);
            }

            if (!modLoadedCorrectly)
                MelonLogger.LogError("Didn't load in correctly, not guaranteed to fully work so i'll shutdown this mod");
        }

        private static bool LeftRoomPrefix()
        {
            worldAllowed = false;
            currentlyRunning = false;
            return true;
        }

        public override void OnModSettingsApplied()
        {
            ApplySettings();
        }

        public override void OnUpdate()
        {
            if (!activeSettings.Enabled
                || !worldAllowed) return;

            // Grab last used input method
            useAxisValues = Utilities.GetLastUsedInputMethod() switch
                {
                    VRCInputMethod.Keyboard => false,
                    VRCInputMethod.Mouse    => false,
                    _                       => true
                };

            // Axis
            if (useAxisValues)
            {
                // Do we want to maybe run? (●'◡'●)
                if (!currentlyRunning)
                {
                    // Clicked
                    if (!Utilities.AxisClicked("Vertical", ref previousAxis, activeSettings.AxisClickThreshold)) return;

                    // Woow, someone double clicked with a (VR)CONTROLLER!!! ╰(*°▽°*)╯
                    if (Time.time - lastTimeClicked <= activeSettings.DoubleClickTime)
                    {
                        currentlyRunning = true;
                        SetLocomotion();
                        lastTimeClicked = activeSettings.DoubleClickTime * 4f;
                    }
                    else
                        lastTimeClicked = Time.time;
                }

                // maybe we should stop?
                else
                {
                    if (Mathf.Abs(Input.GetAxis("Vertical") + Input.GetAxis("Horizontal")) <= activeSettings.AxisDeadZone) return;
                    currentlyRunning = false;
                    SetLocomotion();
                }
            }

            // Keyboard
            else
            {
                // Do we want to maybe run? (●'◡'●)
                if (!currentlyRunning
                    && Utilities.HasDoubleClicked(activeSettings.Forward, ref lastTimeClicked, activeSettings.DoubleClickTime))
                {
                    currentlyRunning = true;
                    SetLocomotion();
                }

                // maybe we should stop?
                else if (currentlyRunning
                         && !Input.GetKey(activeSettings.Forward)
                         && !Input.GetKey(activeSettings.Backward)
                         && !Input.GetKey(activeSettings.Left)
                         && !Input.GetKey(activeSettings.Right))
                {
                    currentlyRunning = false;
                    SetLocomotion();
                }
            }
        }

        private static IEnumerator CheckIfWorldAllowed()
        {
            // Disallow until proven otherwise
            worldAllowed = false;
            if(!modLoadedCorrectly) yield break;

            string worldId = RoomManagerBase.field_Internal_Static_ApiWorld_0.id;

            // Check if black/whitelisted from EmmVRC - thanks Emilia and the rest of EmmVRC Staff
            WWW www = new WWW($"https://thetrueyoshifan.com/RiskyFuncsCheck.php?worldid={worldId}");
            while (!www.isDone)
                yield return new WaitForEndOfFrame();
            string result = www.text?.Trim().ToLower();
            www.Dispose();
            if (!string.IsNullOrWhiteSpace(result))
                switch (result)
                {
                    case "allowed":
                        worldAllowed = true;
                        yield break;

                    case "denied":
                        worldAllowed = false;
                        yield break;
                }

            // no result from server or they're currently dead
            // Check tags then
            API.Fetch<ApiWorld>(
                worldId,
                new Action<ApiContainer>(
                    container =>
                        {
                            ApiWorld apiWorld = container.Model.Cast<ApiWorld>();
                            worldAllowed = true;
                            foreach (string worldTag in apiWorld.tags)
                            {
                                if (worldTag.IndexOf("game", StringComparison.OrdinalIgnoreCase) == -1) continue;
                                worldAllowed = false;
                                break;
                            }
                        }),
                disableCache: false);
        }

        private static void JoinedRoomPatch(string __0, float __1)
        {
            if (__0.Equals("BlackFade")
                && __1.Equals(0f)
                && RoomManagerBase.field_Internal_Static_ApiWorldInstance_0 != null) MelonCoroutines.Start(CheckIfWorldAllowed());
        }

        private void ApplySettings()
        {
            activeSettings.Enabled = MelonPrefs.GetBool(SettingsCategory, nameof(Settings.Enabled));
            activeSettings.SpeedMultiplier = MelonPrefs.GetFloat(SettingsCategory, nameof(Settings.SpeedMultiplier));
            activeSettings.DoubleClickTime = MelonPrefs.GetFloat(SettingsCategory, nameof(Settings.DoubleClickTime));

            if (Enum.TryParse(MelonPrefs.GetString(SettingsCategory, nameof(Settings.Forward)), out KeyCode forward))
            {
                activeSettings.Forward = forward;
            }
            else
            {
                MelonLogger.LogError("Failed to parse KeyCode Forward");
            }
            
            if (Enum.TryParse(MelonPrefs.GetString(SettingsCategory, nameof(Settings.Backward)), out KeyCode backward))
            {
                activeSettings.Backward = backward;
            }
            else
            {
                MelonLogger.LogError("Failed to parse KeyCode Backward");
            }
            
            if (Enum.TryParse(MelonPrefs.GetString(SettingsCategory, nameof(Settings.Left)), out KeyCode left))
            {
                activeSettings.Left = left;
            }
            else
            {
                MelonLogger.LogError("Failed to parse KeyCode Left");
            }
            
            if (Enum.TryParse(MelonPrefs.GetString(SettingsCategory, nameof(Settings.Right)), out KeyCode right))
            {
                activeSettings.Right = right;
            }
            else
            {
                MelonLogger.LogError("Failed to parse KeyCode Right");
            }

            activeSettings.AxisDeadZone = MelonPrefs.GetFloat(SettingsCategory, nameof(Settings.AxisDeadZone));
            activeSettings.AxisClickThreshold = MelonPrefs.GetFloat(SettingsCategory, nameof(Settings.AxisClickThreshold));

            SetLocomotion();
        }

        private void SetLocomotion()
        {
            if (RoomManagerBase.field_Internal_Static_ApiWorld_0 == null
                || RoomManagerBase.field_Internal_Static_ApiWorldInstance_0 == null) return;

            if (!worldAllowed) currentlyRunning = false;
            if (walkSpeed == 0
                || runSpeed == 0
                || strafeSpeed == 0) return;

            LocomotionInputController locomotion = Utilities.GetLocalVRCPlayer()?.GetComponent<LocomotionInputController>();
            if (locomotion == null) return;

            // Grab current settings as some worlds change locomotion with triggers in different parts of the world
            // also means we just went from not running to running
            if (currentlyRunning)
            {
                walkSpeed = locomotion.walkSpeed;
                runSpeed = locomotion.runSpeed;
                strafeSpeed = locomotion.strafeSpeed;
            }

            float multiplier = activeSettings.Enabled && currentlyRunning ? activeSettings.SpeedMultiplier : 1f;
            locomotion.walkSpeed = walkSpeed * multiplier;
            locomotion.runSpeed = runSpeed * multiplier;
            locomotion.strafeSpeed = strafeSpeed * multiplier;
        }

        private struct Settings
        {

            public float DoubleClickTime;

            public bool Enabled;

            public float SpeedMultiplier;

            public KeyCode Forward, Backward, Left, Right;

            public float AxisDeadZone, AxisClickThreshold;

        }

    }

}
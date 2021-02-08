using DoubleTapRunner;

using MelonLoader;

using BuildInfo = DoubleTapRunner.BuildInfo;

[assembly: MelonInfo(typeof(DoubleTapper), BuildInfo.Name, BuildInfo.Version, BuildInfo.Author, BuildInfo.DownloadLink)]
[assembly: MelonGame("VRChat", "VRChat")]

namespace DoubleTapRunner
{

    using System;
    using System.Collections;
    using System.Linq;
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

        private static bool currentlyRunning, grabbedWorldSettings;

        private float lastTimeClicked = 25f;

        private float previousAxis;

        private bool useAxisValues;

        private static bool modLoadedCorrectly = true;

        public override void OnApplicationStart()
        {
            if (instance != null)
            {
                MelonLogger.Error("There's already an instance of Double-Tap Runner. Remove the duplicate dll files");
                MelonLogger.Error("Not Guaranteed to work with multiple instances at all");
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

            MelonPreferences.CreateCategory(SettingsCategory, "Double-Tap Runner");
            MelonPreferences.CreateEntry(SettingsCategory, nameof(Settings.Enabled), activeSettings.Enabled, "Enabled");
            MelonPreferences.CreateEntry(SettingsCategory, nameof(Settings.SpeedMultiplier), activeSettings.SpeedMultiplier, "Speed Multiplier");
            MelonPreferences.CreateEntry(SettingsCategory, nameof(Settings.DoubleClickTime), activeSettings.DoubleClickTime, "Double Click Time");

            MelonPreferences.CreateEntry(SettingsCategory, nameof(Settings.Forward), Enum.GetName(typeof(KeyCode), activeSettings.Forward), "Desktop Forward");
            MelonPreferences.CreateEntry(SettingsCategory, nameof(Settings.Backward), Enum.GetName(typeof(KeyCode), activeSettings.Backward), "Desktop Backward");
            MelonPreferences.CreateEntry(SettingsCategory, nameof(Settings.Left), Enum.GetName(typeof(KeyCode), activeSettings.Left), "Desktop Left");
            MelonPreferences.CreateEntry(SettingsCategory, nameof(Settings.Right), Enum.GetName(typeof(KeyCode), activeSettings.Right), "Desktop Right");

            MelonPreferences.CreateEntry(SettingsCategory, nameof(Settings.AxisDeadZone), activeSettings.AxisDeadZone, "Axis Dead Zone");
            MelonPreferences.CreateEntry(SettingsCategory, nameof(Settings.AxisClickThreshold), activeSettings.AxisClickThreshold, "Axis Click Threshold");
            ApplySettings();

            try
            {
                // way more mod friendly but might mean more updates... oh well, not really hard to find. "Fade to" and check params
                var fadeMethods = typeof(VRCUiManager).GetMethods()
                                                      .Where(
                                                          m => m.Name.StartsWith("Method_Public_Void_String_Single_Action_") && m.GetParameters().Length == 3);
                foreach (MethodInfo fadeMethod in fadeMethods)
                {
                    Harmony.Patch(
                        fadeMethod,
                        null,
                        new HarmonyMethod(typeof(DoubleTapper).GetMethod(nameof(JoinedRoomPatch), BindingFlags.NonPublic | BindingFlags.Static)));
                }
            }
            catch (Exception e)
            {
                modLoadedCorrectly = false;
                MelonLogger.Error("Failed to patch into FadeTo: " + e);
            }

            try
            {
                MethodInfo leaveRoomMethod = typeof(NetworkManager).GetMethod(nameof(NetworkManager.OnLeftRoom), BindingFlags.Public | BindingFlags.Instance);
                Harmony.Patch(
                    leaveRoomMethod,
                    new HarmonyMethod(typeof(DoubleTapper).GetMethod(nameof(LeftRoomPrefix), BindingFlags.NonPublic | BindingFlags.Static)));
            }
            catch (Exception e)
            {
                modLoadedCorrectly = false;
                MelonLogger.Error("Failed to patch into OnLeftRoom: " + e);
            }

            if (!modLoadedCorrectly)
                MelonLogger.Error("Didn't load in correctly, not guaranteed to fully work so i'll shutdown this mod");
        }

        private static bool LeftRoomPrefix()
        {
            grabbedWorldSettings = false;
            worldAllowed = false;
            currentlyRunning = false;
            return true;
        }
        
        public override void OnPreferencesSaved()
        {
            ApplySettings();
        }

        public override void OnPreferencesLoaded()
        {
            ApplySettings();
        }

        public override void OnUpdate()
        {
            #if DEBUG
                if (Input.GetKeyDown(KeyCode.O))
                {
                    LocomotionInputController locomotion = Utilities.GetLocalVRCPlayer()?.GetComponent<LocomotionInputController>();
                    if (locomotion == null) return;
                    MelonLogger.Msg(
                        $"Motionstate Floats: {locomotion.field_Public_Single_0} {locomotion.field_Public_Single_1} {locomotion.field_Public_Single_2} {locomotion.field_Public_Single_3} {locomotion.field_Public_Single_4} {locomotion.field_Public_Single_5} {locomotion.field_Public_Single_6} ");
                }
            #endif
            
            
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
            if (!modLoadedCorrectly) yield break;

            string worldId = RoomManager.field_Internal_Static_ApiWorld_0.id;

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
                    #if DEBUG
                        MelonLogger.Msg("World Allowed - Emm");
                    #endif
                        yield break;

                    case "denied":
                        worldAllowed = false;
                    #if DEBUG
                        MelonLogger.Msg("World Denied - Emm");
                    #endif
                        yield break;
                }

        #if DEBUG
            MelonLogger.Msg("No Result From Emm");
        #endif

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
                            #if DEBUG
                                MelonLogger.Msg("World Denied");
                            #endif
                                break;
                            }
                        }),
                disableCache: false);

        #if DEBUG
            MelonLogger.Msg("World Allowed");
        #endif
        }

        private static void JoinedRoomPatch(string __0, float __1)
        {
            if (__0.Equals("BlackFade")
                && __1.Equals(0f)
                && RoomManager.field_Internal_Static_ApiWorldInstance_0 != null) MelonCoroutines.Start(CheckIfWorldAllowed());
        }

        private void ApplySettings()
        {
            activeSettings.Enabled = MelonPreferences.GetEntryValue<bool>(SettingsCategory, nameof(Settings.Enabled));
            activeSettings.SpeedMultiplier = MelonPreferences.GetEntryValue<float>(SettingsCategory, nameof(Settings.SpeedMultiplier));
            activeSettings.DoubleClickTime = MelonPreferences.GetEntryValue<float>(SettingsCategory, nameof(Settings.DoubleClickTime));

            if (Enum.TryParse(MelonPreferences.GetEntryValue<string>(SettingsCategory, nameof(Settings.Forward)), out KeyCode forward))
            {
                activeSettings.Forward = forward;
            }
            else
            {
                MelonLogger.Error("Failed to parse KeyCode Forward");
            }
            
            if (Enum.TryParse(MelonPreferences.GetEntryValue<string>(SettingsCategory, nameof(Settings.Backward)), out KeyCode backward))
            {
                activeSettings.Backward = backward;
            }
            else
            {
                MelonLogger.Error("Failed to parse KeyCode Backward");
            }
            
            if (Enum.TryParse(MelonPreferences.GetEntryValue<string>(SettingsCategory, nameof(Settings.Left)), out KeyCode left))
            {
                activeSettings.Left = left;
            }
            else
            {
                MelonLogger.Error("Failed to parse KeyCode Left");
            }
            
            if (Enum.TryParse(MelonPreferences.GetEntryValue<string>(SettingsCategory, nameof(Settings.Right)), out KeyCode right))
            {
                activeSettings.Right = right;
            }
            else
            {
                MelonLogger.Error("Failed to parse KeyCode Right");
            }

            activeSettings.AxisDeadZone = MelonPreferences.GetEntryValue<float>(SettingsCategory, nameof(Settings.AxisDeadZone));
            activeSettings.AxisClickThreshold = MelonPreferences.GetEntryValue<float>(SettingsCategory, nameof(Settings.AxisClickThreshold));

            SetLocomotion();
        }

        private void SetLocomotion()
        {
            if (RoomManager.field_Internal_Static_ApiWorld_0 == null
                || RoomManager.field_Internal_Static_ApiWorldInstance_0 == null) return;

            if (!worldAllowed) currentlyRunning = false;

            LocomotionInputController locomotion = Utilities.GetLocalVRCPlayer()?.GetComponent<LocomotionInputController>();
            if (locomotion == null) return;

            // Grab current settings as some worlds change locomotion with triggers in different parts of the world
            // also means we just went from not running to running
            if (currentlyRunning)
            {
                grabbedWorldSettings = true;
                walkSpeed = locomotion.field_Public_Single_1;
                runSpeed = locomotion.field_Public_Single_0;
                strafeSpeed = locomotion.field_Public_Single_2;
            }

            // to stop being unable to move randomly...
            if (!grabbedWorldSettings) return;
            
            float multiplier = activeSettings.Enabled && currentlyRunning ? activeSettings.SpeedMultiplier : 1f;
            locomotion.field_Public_Single_1 = walkSpeed * multiplier;
            locomotion.field_Public_Single_0 = runSpeed * multiplier;
            locomotion.field_Public_Single_2 = strafeSpeed * multiplier;
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
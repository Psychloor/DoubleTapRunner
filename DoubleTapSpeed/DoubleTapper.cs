using DoubleTapRunner;

using MelonLoader;

using BuildInfo = DoubleTapRunner.BuildInfo;

[assembly: MelonInfo(typeof(DoubleTapper), BuildInfo.Name, BuildInfo.Version, BuildInfo.Author, BuildInfo.DownloadLink)]
[assembly: MelonGame("VRChat", "VRChat")]

namespace DoubleTapRunner
{

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using HarmonyLib;

    using MelonLoader;

    using UnityEngine;

    using VRC.Core;
    using VRC.SDK.Internal;
    using VRC.SDKBase;

    public class DoubleTapper : MelonMod
    {

        private MelonPreferences_Category settingsCategory;

        private static DoubleTapper instance;

        // Original Settings
        private static float walkSpeed, runSpeed, strafeSpeed;

        private static bool worldAllowed;

        private static bool currentlyRunning, grabbedWorldSettings;

        private static bool modLoadedCorrectly = true;

        private Settings activeSettings;

        private float lastTimeClicked = 25f;

        private float previousAxis;

        private bool useAxisValues;

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

            settingsCategory = MelonPreferences.CreateCategory("DoubleTapRunner", "Double-Tap Runner");
            settingsCategory.CreateEntry( nameof(Settings.Enabled), activeSettings.Enabled, "Enabled");
            settingsCategory.CreateEntry(nameof(Settings.SpeedMultiplier), activeSettings.SpeedMultiplier, "Speed Multiplier");
            settingsCategory.CreateEntry( nameof(Settings.DoubleClickTime), activeSettings.DoubleClickTime, "Double Click Time");

            settingsCategory.CreateEntry( nameof(Settings.Forward), Enum.GetName(typeof(KeyCode), activeSettings.Forward), "Desktop Forward");
            settingsCategory.CreateEntry(
                
                nameof(Settings.Backward),
                Enum.GetName(typeof(KeyCode), activeSettings.Backward),
                "Desktop Backward");
            settingsCategory.CreateEntry( nameof(Settings.Left), Enum.GetName(typeof(KeyCode), activeSettings.Left), "Desktop Left");
            settingsCategory.CreateEntry( nameof(Settings.Right), Enum.GetName(typeof(KeyCode), activeSettings.Right), "Desktop Right");

            settingsCategory.CreateEntry( nameof(Settings.AxisDeadZone), activeSettings.AxisDeadZone, "Axis Dead Zone");
            settingsCategory.CreateEntry( nameof(Settings.AxisClickThreshold), activeSettings.AxisClickThreshold, "Axis Click Threshold");
            ApplySettings();

            try
            {
                // way more mod friendly but might mean more updates... oh well, not really hard to find. "Fade to" and check params
                IEnumerable<MethodInfo> fadeMethods = typeof(VRCUiManager).GetMethods()
                                                                          .Where(
                                                                              m => m.Name.StartsWith("Method_Public_Void_String_Single_Action_")
                                                                                   && m.GetParameters().Length == 3);
                foreach (MethodInfo fadeMethod in fadeMethods)
                    HarmonyInstance.Patch(
                        fadeMethod,
                        null,
                        new HarmonyMethod(typeof(DoubleTapper).GetMethod(nameof(JoinedRoomPatch), BindingFlags.NonPublic | BindingFlags.Static)));
            }
            catch (Exception e)
            {
                modLoadedCorrectly = false;
                MelonLogger.Error("Failed to patch into FadeTo: " + e);
            }

            try
            {
                MethodInfo leaveRoomMethod = typeof(NetworkManager).GetMethod(nameof(NetworkManager.OnLeftRoom), BindingFlags.Public | BindingFlags.Instance);
                HarmonyInstance.Patch(
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
                    
                    MelonLogger.Msg("Locomotion Floats:");
                    var floatFields = typeof(LocomotionInputController).GetFields()
                                                                  .Where(f => f.FieldType == typeof(float)).OrderBy(f => f.Name);
                    foreach (FieldInfo field in floatFields)
                    {
                        MelonLogger.Msg($"\t{field.Name}: {field.GetValue(locomotion)}");
                    }
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
                    {
                        lastTimeClicked = Time.time;
                    }
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
            WWW www = new WWW($"https://dl.emmvrc.com/riskyfuncs.php?worldid={worldId}", null, new Il2CppSystem.Collections.Generic.Dictionary<string, string>());
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
            activeSettings.Enabled = settingsCategory.GetEntry<bool>(nameof(Settings.Enabled)).Value;
            activeSettings.SpeedMultiplier = settingsCategory.GetEntry<float>(nameof(Settings.SpeedMultiplier)).Value;
            activeSettings.DoubleClickTime = settingsCategory.GetEntry<float>(nameof(Settings.DoubleClickTime)).Value;

            if (Enum.TryParse(settingsCategory.GetEntry<string>(nameof(Settings.Forward)).Value, out KeyCode forward))
                activeSettings.Forward = forward;
            else MelonLogger.Error("Failed to parse KeyCode Forward");

            if (Enum.TryParse(settingsCategory.GetEntry<string>(nameof(Settings.Backward)).Value, out KeyCode backward))
                activeSettings.Backward = backward;
            else MelonLogger.Error("Failed to parse KeyCode Backward");

            if (Enum.TryParse(settingsCategory.GetEntry<string>(nameof(Settings.Left)).Value, out KeyCode left)) activeSettings.Left = left;
            else MelonLogger.Error("Failed to parse KeyCode Left");

            if (Enum.TryParse(settingsCategory.GetEntry<string>(nameof(Settings.Right)).Value, out KeyCode right))
                activeSettings.Right = right;
            else MelonLogger.Error("Failed to parse KeyCode Right");

            activeSettings.AxisDeadZone = settingsCategory.GetEntry<float>(nameof(Settings.AxisDeadZone)).Value;
            activeSettings.AxisClickThreshold = settingsCategory.GetEntry<float>(nameof(Settings.AxisClickThreshold)).Value;

            SetLocomotion();
        }

        private void SetLocomotion()
        {
            if (RoomManager.field_Internal_Static_ApiWorld_0 == null
                || RoomManager.field_Internal_Static_ApiWorldInstance_0 == null) return;

            // ReSharper disable once Unity.NoNullPropagation
            var localPlayerApi = VRCPlayer.field_Internal_Static_VRCPlayer_0?.field_Private_VRCPlayerApi_0;
            if (localPlayerApi == null) return;
            
            if (!worldAllowed || Utilities.GetStreamerMode) currentlyRunning = false;

            //LocomotionInputController locomotion = Utilities.GetLocalVRCPlayer()?.GetComponent<LocomotionInputController>();
            //if (locomotion == null) return;

            // Grab current settings as some worlds change locomotion with triggers in different parts of the world
            // also means we just went from not running to running
            if (currentlyRunning)
            {
                grabbedWorldSettings = true;
                walkSpeed = localPlayerApi.GetWalkSpeed();
                runSpeed = localPlayerApi.GetRunSpeed();
                strafeSpeed = localPlayerApi.GetStrafeSpeed();
            }

            // to stop being unable to move randomly...
            if (!grabbedWorldSettings) return;

            float multiplier = activeSettings.Enabled && currentlyRunning ? activeSettings.SpeedMultiplier : 1f;
            localPlayerApi.SetWalkSpeed(walkSpeed * multiplier);
            localPlayerApi.SetRunSpeed(runSpeed * multiplier);
            localPlayerApi.SetStrafeSpeed(strafeSpeed * multiplier);
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
using System;
using System.Collections.Generic;
using HarmonyLib;
using UBOAT.Game;
using UBOAT.Game.Core.Serialization;
using UnityEngine;

namespace UInterface
{
    [NonSerializedInGameState]
    public sealed class SubmarineFramework : IUserMod
    {
        private static readonly HashSet<string> Installed = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> Attempted = new HashSet<string>(StringComparer.Ordinal);
        private static bool initialized;

        public void OnLoaded()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            VanillaStations.Register();
            if (TryInstall("uinterface.lifecycle", typeof(PlayerSubmarineLifecycle), typeof(SaveRestoreLifecycle)))
            {
                SubmarineApi.TrackSceneUnloads();
                TryInstall(DeviceStateApi.FeatureId, typeof(DeviceStateWrite), typeof(DeviceStateRead));
            }

            TryInstall("uinterface.dependency-scenes", typeof(PlayerSubmarineDependencyLifecycle));
            TryInstall("uinterface.campaign", typeof(CampaignSubmarineLoader));
            TryInstall("uinterface.port", typeof(PortSubmarineLoader), typeof(PortSubmarineOptions));
            TryInstall("uinterface.sensor-contact-map", typeof(NativeSensorContactMap));
            Installed.Add("uinterface.subsystem-operations");
            Installed.Add("uinterface.submarine-actions");
            Installed.Add("uinterface.mounted-subsystems");
            Installed.Add("uinterface.first-person-buttons");
            Installed.Add("uinterface.native-sensor-observations");
            Installed.Add("uinterface.first-person-axes");
            Installed.Add("uinterface.first-person-hold");
            Installed.Add("uinterface.sensor-contacts");
            Installed.Add("uinterface.tube-requests");
        }

        internal static int GetFeatureVersion(string featureId)
        {
            return featureId != null && Installed.Contains(featureId) ? 1 : 0;
        }

        internal static bool TryInstallPropulsionBinding()
        {
            return TryInstall("uinterface.propulsion-binding", typeof(NativePropulsionBinding));
        }

        internal static bool TryInstallPropulsionOutput()
        {
            return TryInstall(PropulsionApi.FeatureId, typeof(NativePropellerPower), typeof(NativeTargetSpeed));
        }

        internal static bool TryInstallPowerSupply()
        {
            return TryInstall(PowerApi.FeatureId, typeof(NativeBatteryUpdate));
        }

        internal static bool TryInstallPowerInterlocks()
        {
            return TryInstall("uinterface.power-interlocks", typeof(NativeCompartmentPower));
        }

        internal static bool TryInstallStationTemplates()
        {
            return TryInstall("uinterface.station-templates", typeof(StationTemplateQuery), typeof(StationTemplateListQuery));
        }

        internal static bool TryInstallAmmoCompatibility()
        {
            return TryInstall("uinterface.ammo-compatibility", typeof(NativeAmmoSlotRule),
                typeof(NativeAutomaticLoadTube), typeof(NativeAutomaticLoadAmmo));
        }

        internal static bool TryInstallLaunchSpread()
        {
            return TryInstall("uinterface.launch-spread", typeof(NativeLaunchRequest), typeof(NativeLaunchSpreadRead));
        }

        private static bool TryInstall(string featureId, params Type[] patchClasses)
        {
            if (!Attempted.Add(featureId))
            {
                return Installed.Contains(featureId);
            }

            var harmony = new Harmony(featureId);
            try
            {
                foreach (var patchClass in patchClasses)
                {
                    harmony.CreateClassProcessor(patchClass).Patch();
                }

                Installed.Add(featureId);
                Debug.Log("[UInterface] Registered " + featureId + ".");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[UInterface] Failed to register " + featureId + ".");
                Debug.LogException(exception);
                try
                {
                    harmony.UnpatchAll(featureId);
                }
                catch (Exception cleanupException)
                {
                    Debug.LogException(cleanupException);
                }

                return false;
            }
        }
    }
}

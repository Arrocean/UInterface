using System;
using UBOAT.Game.Scene;
using UBOAT.Game.Scene.Camera;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using ShipActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace UInterface
{
    internal static class VanillaActionRules
    {
        internal static bool OpensStorageWithoutActor(CharacterAction template)
        {
            return template is OpenStorageAction || template is OpenCompartmentStorageAction ||
                template is OpenTorpedoStorageAction || template is OpenTorpedoStorageOnTubeAction;
        }

        internal static bool CompletesBeforeQueue(ShipActivator station, Type actionType)
        {
            return actionType == typeof(SwitchCompartmentPowerAction) ||
                actionType == typeof(SwitchSteeringModeAction) ||
                actionType == typeof(OpenRadioViewAction) ||
                (actionType == typeof(TurnLightOnOffAction) && station.GetComponentInChildren<Light>()) ||
                (actionType == typeof(ToggleDisallowRepairStatusAction) && station.GetComponent<Equipment>()) ||
                ((actionType == typeof(OpenCloseTorpedoLauncherBreechDoor) ||
                    actionType == typeof(OpenTorpedoStorageOnTubeAction)) &&
                    MainCamera.Instance?.CurrentMode == CameraMode.Generic);
        }
    }
}

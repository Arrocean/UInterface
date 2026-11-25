using HarmonyLib;
using UBOAT.Game;
using UBOAT.Game.Core.Serialization;
using UnityEngine;

namespace UInterface
{
    [NonSerializedInGameState]
    public sealed class SubmarineFramework : IUserMod
    {
        public void OnLoaded()
        {
            VanillaStations.Register();
            new Harmony("uinterface.submarines").PatchAll(typeof(SubmarineFramework).Assembly);
            SubmarineApi.TrackSceneUnloads();
            Debug.Log("[UInterface] Campaign, port, equipment and station integration registered.");
        }
    }
}

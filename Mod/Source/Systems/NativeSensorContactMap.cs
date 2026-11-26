using HarmonyLib;
using UBOAT.Game.UI;
using UnityEngine;

namespace UInterface
{
    [HarmonyPatch(typeof(MapUI), "OnEnable")]
    internal static class NativeSensorContactMap
    {
        private static void Postfix(MapUI __instance)
        {
            if (__instance.GetComponentInChildren<SensorContactMapLayer>(includeInactive: true))
            {
                return;
            }

            var layer = new GameObject("UInterface Unknown Contacts", typeof(RectTransform), typeof(CanvasRenderer));
            layer.layer = __instance.gameObject.layer;
            var rect = (RectTransform)layer.transform;
            rect.SetParent(__instance.transform, worldPositionStays: false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var graphic = layer.AddComponent<SensorContactMapLayer>();
            graphic.raycastTarget = false;
            graphic.maskable = false;
        }
    }
}

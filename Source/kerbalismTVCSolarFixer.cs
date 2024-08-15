using UnityEngine;
using HarmonyLib;
using KERBALISM;

namespace WeatherDrivenSolarPanel
{
    [HarmonyPatch(typeof(SolarPanelFixer))]
    [HarmonyPatch("FixedUpdate")]
    public static class kerbalismTVCSolarFixer
    {
        public static void Prefix(SolarPanelFixer __instance)
        {
            __instance.currentOutput++;
            Debug.Log("Prefix: customVariable incremented to " + __instance.currentOutput);
        }

        public static void Postfix(SolarPanelFixer __instance)
        {
            if (__instance.currentOutput > 10)
            {
                Debug.Log("Postfix: customVariable is greater than 10");
            }
        }
    }
}
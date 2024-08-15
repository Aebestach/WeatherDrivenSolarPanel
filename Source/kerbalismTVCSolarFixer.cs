using UnityEngine;
using HarmonyLib;
using KERBALISM;
using static KERBALISM.SolarPanelFixer;
using KSP.Localization;
using System.Collections.Generic;
using WDSP_GenericFunctionModule;

namespace WeatherDrivenSolarPanel
{
    [KSPAddon(KSPAddon.Startup.Flight, true)]
    public class Injector : MonoBehaviour
    {
        public static double WeatherImpactFactor;
        public static string layerName = null;

        //Change the value of status of the solar panel.
        public static double statusChangeValue = 1.0;
        public const string prefix2 = "#WDSP_TVC_";
        public static string GetLocWDSP(string template) => Localizer.Format(prefix2 + template);
        public static string WDSP_TVC_cloudyAffect = GetLocWDSP("cloudyAffect");                                       // "Cloudy Weather"
        public static string WDSP_TVC_dustStormAffect = GetLocWDSP("dustStormAffect");                                 // "Sandstorm/Dust Storm"
        public static string WDSP_TVC_rainAffect = GetLocWDSP("rainAffect");                                           // "Precipitation/Thunderstorm Weather"
        public static string WDSP_TVC_snowAffect = GetLocWDSP("snowAffect");                                           // "Snow Weather"
        public static string WDSP_TVC_volcanoesAffect = GetLocWDSP("volcanoesAffect");                                 // "Affected by volcanoes"
        public static string WDSP_TVC_sunDirect = GetLocWDSP("sunDirect");                                             // "Sunny"
        // Define and initialize the cloud dictionary
        public static Dictionary<string, HashSet<string>> categoryDictionary = new Dictionary<string, HashSet<string>>
    {
        { "cloudyAffect", new HashSet<string> { "Kerbin-clouds1", "Kerbin-clouds2", "Eve-clouds1", "Eve-clouds2",
                "Jool-clouds-underworld", "Jool-clouds0", "Jool-clouds1", "Jool-clouds2",
                "Laythe-clouds1", "Duna-rare-cirrus", "TemperateCumulus",
                "TemperateAltoStratus", "Cirrus", "Rouqea-clouds1", "Rouqea-clouds2",
                "Suluco-MainClouds", "Suluco-HighClouds", "Noyreg-clouds1",
                "Noyreg-clouds2", "Anehta-clouds-underworld", "Anehta-clouds1",
                "Anehta-clouds2", "Efil-clouds1", "Efil-clouds2", "Earth-clouds1", "Earth-clouds2", "Venus-clouds1",
            "Titan-clouds1","Mars-rare-cirrus","Eve-Clouds-Low","Eve-Clouds-High","Huygen-Clouds-Low","Kerbin-Clouds-Low","Kerbin-Clouds-High",
        "Laythe-Clouds-Low","Lindor-Clouds-Underworld","Lindor-Clouds1","Lindor-Clouds2","Lindor-Clouds3"} },

        { "rainAffect", new HashSet<string> { "Kerbin-Weather1", "Kerbin-Weather2", "TemperateWeather",
                "Rouqea-Weather", "Suluco-Weather1", "Efil-Weather","Earth-Weather1","Earth-Weather2","Titan-Weather1","Eve-Weather-Heavy",
        "Huygen-Weather","Kerbin-Weather-Heavy","Laythe-Weather"} },

        { "dustStormAffect", new HashSet<string> {  "Duna-duststorm-big", "Storms-Dust","Stable-Dust",
            "Mars-duststorm-big","Dust-Small","Dust-Large","Dust-Global","Tylo-Dust"} },

        { "snowAffect", new HashSet<string> { "Laythe-Weather1", "Suluco-Snow", "Kerbin-Snow-1", "Kerbin-Snow-2"} },

        { "volcanoesAffect",new HashSet<string> { "Laythe-HighAlt-Volcanoes"} }
    };
        /// <summary>Main PAW info label</summary>
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "#WDSP_TVC_weatherStatus")]//Weather Status
        public static string weatherPanelStatus = string.Empty;

        public void Awake()
        {
            Harmony harmony = new Harmony("WeatherDrivenSolarPanel");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(SolarPanelFixer), "FixedUpdate")]
    public static class kerbalismTVCSolarFixer
    {
        public static bool Prefix(SolarPanelFixer __instance)
        {
            // sanity check
            if (__instance.SolarPanel == null)
            {
                return true;
            }

            // Keep resetting launchUT in prelaunch state. It is possible for that value to come from craft file which could result in panels being degraded from the start.
            if (Lib.IsFlight() && __instance.vessel != null && __instance.vessel.situation == Vessel.Situations.PRELAUNCH)
                __instance.launchUT = Planetarium.GetUniversalTime();

            // can't produce anything if not deployed, broken, etc
            PanelState newState = __instance.SolarPanel.GetState();
            if (__instance.state != newState)
            {
                __instance.state = newState;
                if (Lib.IsEditor() && (newState == PanelState.Extended || newState == PanelState.ExtendedFixed || newState == PanelState.Retracted))
                    Lib.RefreshPlanner();
            }

            if (!(__instance.state == PanelState.Extended || __instance.state == PanelState.ExtendedFixed || __instance.state == PanelState.Static))
            {
                __instance.exposureState = ExposureState.Disabled;
                __instance.currentOutput = 0.0;
                //return;
            }

            // do nothing else in editor
            if (Lib.IsEditor())
            {
                //return;
            }

            // get vessel data from cache
            VesselData vd = __instance.vessel.KerbalismData();

            // do nothing if vessel is invalid
            if (!vd.IsSimulated)
            {
                //return;
            }

            // Update tracked sun in auto mode
            if (!__instance.manualTracking && __instance.trackedSunIndex != vd.EnvMainSun.SunData.bodyIndex)
            {
                __instance.trackedSunIndex = vd.EnvMainSun.SunData.bodyIndex;
                __instance.SolarPanel.SetTrackedBody(vd.EnvMainSun.SunData.body);
            }

            VesselData.SunInfo trackedSunInfo = vd.EnvSunsInfo.Find(p => p.SunData.bodyIndex == __instance.trackedSunIndex);

            if (trackedSunInfo.SunlightFactor == 0.0)
                __instance.exposureState = ExposureState.InShadow;
            else
                __instance.exposureState = ExposureState.Exposed;


            if (vd.EnvIsAnalytic)
            {
                // if we are switching to analytic mode and the vessel is landed, get an average exposure over a day
                // TODO : maybe check the rotation speed of the body, this might be inaccurate for tidally-locked bodies (test on the mun ?)
                if (!__instance.analyticSunlight && Lib.Landed(__instance.vessel)) __instance.persistentFactor = __instance.GetAnalyticalCosineFactorLanded(vd);
                __instance.analyticSunlight = true;
            }
            else
            {
                __instance.analyticSunlight = false;
            }

            // cosine / occlusion factor isn't updated when in analyticalSunlight / unloaded states :
            // - evaluting sun_dir / vessel orientation gives random results resulting in inaccurate behavior / random EC rates
            // - using the last calculated factor is a satisfactory simulation of a sun relative vessel attitude keeping behavior
            //   without all the complexity of actually doing it
            if (__instance.analyticSunlight)
            {
                __instance.exposureFactor = __instance.persistentFactor;
            }
            else
            {
                // reset factors
                __instance.persistentFactor = 0.0;
                __instance.exposureFactor = 0.0;

                // iterate over all stars, compute the exposure factor
                foreach (VesselData.SunInfo sunInfo in vd.EnvSunsInfo)
                {
                    // ignore insignifiant flux from distant stars
                    if (sunInfo != trackedSunInfo && sunInfo.SolarFlux < 1e-6)
                        continue;

                    double sunCosineFactor = 0.0;
                    double sunOccludedFactor = 0.0;
                    string occludingPart = null;

                    // Get the cosine factor (alignement between the sun and the panel surface)
                    sunCosineFactor = __instance.SolarPanel.GetCosineFactor(sunInfo.Direction);

                    if (sunCosineFactor == 0.0)
                    {
                        // If this is the tracked sun and the panel is not oriented toward the sun, update the gui info string.
                        if (sunInfo == trackedSunInfo)
                            __instance.exposureState = ExposureState.BadOrientation;
                    }
                    else
                    {
                        // The panel is oriented toward the sun, do a physic raycast to check occlusion from parts, terrain, buildings...
                        sunOccludedFactor = __instance.SolarPanel.GetOccludedFactor(sunInfo.Direction, out occludingPart, sunInfo != trackedSunInfo);

                        // If this is the tracked sun and the panel is occluded, update the gui info string. 
                        if (sunInfo == trackedSunInfo && sunOccludedFactor == 0.0)
                        {
                            if (occludingPart != null)
                            {
                                __instance.exposureState = ExposureState.OccludedPart;
                                __instance.mainOccludingPart = Lib.EllipsisMiddle(occludingPart, 15);
                            }
                            else
                            {
                                __instance.exposureState = ExposureState.OccludedTerrain;
                            }
                        }
                    }

                    // Compute final aggregate exposure factor
                    double sunExposureFactor = sunCosineFactor * sunOccludedFactor * sunInfo.FluxProportion;

                    // Add the final factor to the saved exposure factor to be used in analytical / unloaded states.
                    // If occlusion is from the scene, not a part (terrain, building...) don't save the occlusion factor,
                    // as occlusion from the terrain and static objects is too variable over time.
                    if (occludingPart != null)
                        __instance.persistentFactor += sunExposureFactor;
                    else
                        __instance.persistentFactor += sunCosineFactor * sunInfo.FluxProportion;

                    // Only apply the exposure factor if not in shadow (body occlusion check)
                    if (sunInfo.SunlightFactor == 1.0) __instance.exposureFactor += sunExposureFactor;
                    else if (sunInfo == trackedSunInfo) __instance.exposureState = ExposureState.InShadow;
                }
                vd.SaveSolarPanelExposure(__instance.persistentFactor);
            }

            // get solar flux and deduce a scalar based on nominal flux at 1AU
            // - this include atmospheric absorption if inside an atmosphere
            // - at high timewarps speeds, atmospheric absorption is analytical (integrated over a full revolution)
            double distanceFactor = vd.EnvSolarFluxTotal / Sim.SolarFluxAtHome;

            // get wear factor (time based output degradation)
            __instance.wearFactor = 1.0;
            if (__instance.timeEfficCurve?.Curve.keys.Length > 1)
                __instance.wearFactor = Lib.Clamp(__instance.timeEfficCurve.Evaluate((float)((Planetarium.GetUniversalTime() - __instance.launchUT) / 3600.0)), 0.0, 1.0);


            //Handles logic related to volumetric clouds
            if (__instance.vessel.atmDensity > 0)
            {
                Injector.WeatherImpactFactor = GenericFunctionModule.VolumetricCloudTransmittance(trackedSunInfo.SunData.body, out string NlayerName);
                Injector.layerName = NlayerName;
                calculateStatus();
            }

            // get final output rate in EC/s
            __instance.currentOutput = __instance.nominalRate * __instance.wearFactor * distanceFactor * __instance.exposureFactor * Injector.WeatherImpactFactor; ;

            // ignore very small outputs
            if (__instance.currentOutput < 1e-10)
            {
                __instance.currentOutput = 0.0;
                //return;
            }

            // get resource handler
            ResourceInfo ec = KERBALISM.ResourceCache.GetResource(__instance.vessel, "ElectricCharge");

            // produce EC
            ec.Produce(__instance.currentOutput * Kerbalism.elapsed_s, KERBALISM.ResourceBroker.SolarPanel);

            return false;
        }

        /*public static void Postfix(SolarPanelFixer __instance)
        {
            //__instance.currentOutput = 10;
        }*/

        static string GetCategoryByValue(string value)
        {
            foreach (var kvp in Injector.categoryDictionary)
            {
                if (kvp.Value.Contains(value))
                {
                    return kvp.Key;
                }
            }
            return "Not Found!";
        }

        static public void calculateStatus()
        {
            switch (GetCategoryByValue(Injector.layerName))
            {
                case "cloudyAffect":
                    if (Injector.statusChangeValue < 0.95f)
                    {
                        Injector.weatherPanelStatus = "<color=#5F9F9F>" + Injector.WDSP_TVC_cloudyAffect + "</color>";
                    }
                    break;
                case "rainAffect":
                    if (Injector.statusChangeValue < 0.85f)
                    {
                        Injector.weatherPanelStatus = "<color=#5F9F9F>" + Injector.WDSP_TVC_rainAffect + "</color>";
                    }
                    break;
                case "dustStormAffect":
                    if (Injector.statusChangeValue < 0.9f)
                    {
                        Injector.weatherPanelStatus = "<color=#5F9F9F>" + Injector.WDSP_TVC_dustStormAffect + "</color>";
                    }
                    break;
                case "snowAffect":
                    if (Injector.statusChangeValue < 0.9f)
                    {
                        Injector.weatherPanelStatus = "<color=#5F9F9F>" + Injector.WDSP_TVC_snowAffect + "</color>";
                    }
                    break;
                case "volcanoesAffect":
                    if (Injector.statusChangeValue < 0.95f)
                    {
                        Injector.weatherPanelStatus = "<color=#5F9F9F>" + Injector.WDSP_TVC_volcanoesAffect + "</color>";
                    }
                    break;
                default:
                    // Default case if none of the above conditions are met
                    Injector.weatherPanelStatus = "<color=#FF7F00>" + Injector.WDSP_TVC_sunDirect + "</color>";
                    break;
            }
        }
    }
}
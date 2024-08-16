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
    public class WDSPInjector : MonoBehaviour
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
        public static string WDSP_TVC_weatherStatus = GetLocWDSP("weatherStatus");                                    // "Sunny"
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
        public void Awake()
        {
            Harmony harmony = new Harmony("WeatherDrivenSolarPanel");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(SolarPanelFixer), "FixedUpdate")]
    public static class kerbalismTVCFixedUpdateSolarFixer
    {
        static double _currentOutput;
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
                return true;
            }

            // do nothing else in editor
            if (Lib.IsEditor())
            {
                return true;
            }

            // get vessel data from cache
            VesselData vd = __instance.vessel.KerbalismData();

            // do nothing if vessel is invalid
            if (!vd.IsSimulated)
            {
                return true;
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
                WDSPInjector.WeatherImpactFactor = GenericFunctionModule.VolumetricCloudTransmittance(trackedSunInfo.SunData.body, out string NlayerName);
                WDSPInjector.layerName = NlayerName;
                WDSPInjector.statusChangeValue = WDSPInjector.WeatherImpactFactor;
            }
            // get final output rate in EC/s
            __instance.currentOutput = __instance.nominalRate * __instance.wearFactor * distanceFactor * __instance.exposureFactor;

            // ignore very small outputs
            if (__instance.currentOutput < 1e-10)
            {
                __instance.currentOutput = 0.0;
                return true;
            }
            // get resource handler
            ResourceInfo ec = KERBALISM.ResourceCache.GetResource(__instance.vessel, "ElectricCharge");
            __instance.currentOutput = __instance.currentOutput * WDSPInjector.WeatherImpactFactor;
            // produce EC
            ec.Produce(__instance.currentOutput * Kerbalism.elapsed_s, KERBALISM.ResourceBroker.SolarPanel);

            return false;
        }
    }

    [HarmonyPatch(typeof(SolarPanelFixer), "Update")]
    public static class kerbalismTVCUpdateSolarFixer
    {
        public static bool Prefix(SolarPanelFixer __instance)
        {
            // sanity check
            if (__instance.SolarPanel == null) return true;

            // call Update specfic handling, if any
            __instance.SolarPanel.OnUpdate();

            // Do nothing else in the editor
            if (Lib.IsEditor()) return true;

            // Don't update PAW if not needed
            if (!__instance.part.IsPAWVisible()) return true;

            // Update tracked body selection button (Kopernicus multi-star support)
            if (__instance.Events["ManualTracking"].active && (__instance.state == PanelState.Extended || __instance.state == PanelState.ExtendedFixed || __instance.state == PanelState.Static))
            {
                __instance.Events["ManualTracking"].guiActive = true;
                __instance.Events["ManualTracking"].guiName = Lib.BuildString(Local.SolarPanelFixer_Trackedstar + " ", __instance.manualTracking ? ": " : Local.SolarPanelFixer_AutoTrack, FlightGlobals.Bodies[__instance.trackedSunIndex].bodyDisplayName.Replace("^N", ""));//"Tracked star"[Auto] : "
            }
            else
            {
                __instance.Events["ManualTracking"].guiActive = false;
            }

            // Update main status field visibility
            if (__instance.state == PanelState.Failure || __instance.state == PanelState.Unknown)
                __instance.Fields["panelStatus"].guiActive = false;
            else
                __instance.Fields["panelStatus"].guiActive = true;

            // Update main status field text
            bool addRate = false;
            switch (__instance.exposureState)
            {
                case ExposureState.InShadow:
                    __instance.panelStatus = "<color=#ff2222>" + Local.SolarPanelFixer_inshadow + "</color>";//in shadow
                    addRate = true;
                    break;
                case ExposureState.OccludedTerrain:
                    __instance.panelStatus = "<color=#ff2222>" + Local.SolarPanelFixer_occludedbyterrain + "</color>";//occluded by terrain
                    addRate = true;
                    break;
                case ExposureState.OccludedPart:
                    __instance.panelStatus = Lib.BuildString("<color=#ff2222>", Local.SolarPanelFixer_occludedby.Format(__instance.mainOccludingPart), "</color>");//occluded by 
                    addRate = true;
                    break;
                case ExposureState.BadOrientation:
                    __instance.panelStatus = "<color=#ff2222>" + Local.SolarPanelFixer_badorientation + "</color>";//bad orientation
                    addRate = true;
                    break;
                case ExposureState.Disabled:
                    switch (__instance.state)
                    {
                        case PanelState.Retracted: __instance.panelStatus = Local.SolarPanelFixer_retracted; break;//"retracted"
                        case PanelState.Extending: __instance.panelStatus = Local.SolarPanelFixer_extending; break;//"extending"
                        case PanelState.Retracting: __instance.panelStatus = Local.SolarPanelFixer_retracting; break;//"retracting"
                        case PanelState.Broken: __instance.panelStatus = Local.SolarPanelFixer_broken; break;//"broken"
                        case PanelState.Failure: __instance.panelStatus = Local.SolarPanelFixer_failure; break;//"failure"
                        case PanelState.Unknown: __instance.panelStatus = Local.SolarPanelFixer_invalidstate; break;//"invalid state"
                    }
                    break;
                case ExposureState.Exposed:

                    __instance.sb.Length = 0;
                    if (Settings.UseSIUnits)
                    {
                        if (__instance.hasRUI)
                            __instance.sb.Append(Lib.SIRate(__instance.currentOutput, Lib.ECResID));
                        else
                            __instance.sb.Append(Lib.SIRate(__instance.currentOutput, __instance.EcUIUnit));
                    }
                    else
                    {
                        __instance.sb.Append(__instance.currentOutput.ToString(__instance.rateFormat));
                        __instance.sb.Append(" ");
                        __instance.sb.Append(__instance.EcUIUnit);
                    }
                    if (__instance.analyticSunlight)
                    {
                        __instance.sb.Append(", ");
                        __instance.sb.Append(Local.SolarPanelFixer_analytic);//analytic
                        __instance.sb.Append(" ");
                        __instance.sb.Append(__instance.persistentFactor.ToString("P0"));
                    }
                    else
                    {
                        __instance.sb.Append(", ");
                        __instance.sb.Append(Local.SolarPanelFixer_exposure);//exposure
                        __instance.sb.Append(" ");
                        __instance.sb.Append(__instance.exposureFactor.ToString("P0"));
                        if (__instance.vessel.atmDensity > 0)
                        {
                            __instance.sb.Append("\n");
                            __instance.sb.Append(WDSPInjector.WDSP_TVC_weatherStatus);
                            __instance.sb.Append(": ");
                            __instance.sb.Append(CalculateStatus());
                        }
                    }
                    if (__instance.wearFactor < 1.0)
                    {
                        __instance.sb.Append("\n");
                        __instance.sb.Append(Local.SolarPanelFixer_wear);//wear
                        __instance.sb.Append(" : ");
                        __instance.sb.Append((1.0 - __instance.wearFactor).ToString("P0"));
                    }
                    __instance.panelStatus = __instance.sb.ToString();
                    break;
            }
            if (addRate && __instance.currentOutput > 0.001)
            {
                if (Settings.UseSIUnits)
                {
                    if (__instance.hasRUI)
                        Lib.BuildString(Lib.SIRate(__instance.currentOutput, Lib.ECResID), ", ", __instance.panelStatus);
                    else
                        Lib.BuildString(Lib.SIRate(__instance.currentOutput, __instance.EcUIUnit), ", ", __instance.panelStatus);
                }
                else
                {
                    Lib.BuildString(__instance.currentOutput.ToString(__instance.rateFormat), " ", __instance.EcUIUnit, ", ", __instance.panelStatus);
                }
            }
            return false;
        }
        static string GetCategoryByValue(string value)
        {
            foreach (var kvp in WDSPInjector.categoryDictionary)
            {
                if (kvp.Value.Contains(value))
                {
                    return kvp.Key;
                }
            }
            return "Not Found!";
        }

        public static string CalculateStatus()
        {
            Debug.Log($"WDSPInjector.statusChangeValue is {WDSPInjector.statusChangeValue}");
            switch (GetCategoryByValue(WDSPInjector.layerName))
            {
                case "cloudyAffect":
                    if (WDSPInjector.statusChangeValue < 0.8f)
                    {
                        return "<color=#5F9F9F>" + WDSPInjector.WDSP_TVC_cloudyAffect + "</color>";
                    }
                    break;
                case "rainAffect":
                    if (WDSPInjector.statusChangeValue < 0.8f)
                    {
                        return "<color=#5F9F9F>" + WDSPInjector.WDSP_TVC_rainAffect + "</color>";
                    }
                    break;
                case "dustStormAffect":
                    if (WDSPInjector.statusChangeValue < 0.8f)
                    {
                        return "<color=#5F9F9F>" + WDSPInjector.WDSP_TVC_dustStormAffect + "</color>";
                    }
                    break;
                case "snowAffect":
                    if (WDSPInjector.statusChangeValue < 0.8f)
                    {
                        return "<color=#5F9F9F>" + WDSPInjector.WDSP_TVC_snowAffect + "</color>";
                    }
                    break;
                case "volcanoesAffect":
                    if (WDSPInjector.statusChangeValue < 0.8f)
                    {
                        return "<color=#5F9F9F>" + WDSPInjector.WDSP_TVC_volcanoesAffect + "</color>";
                    }
                    break;
                default:
                    // Default case if none of the above conditions are met
                    return "<color=#FF7F00>" + WDSPInjector.WDSP_TVC_sunDirect + "</color>";
            }
            return "<color=#FF7F00>" + WDSPInjector.WDSP_TVC_sunDirect + "</color>";
        }
    }
}
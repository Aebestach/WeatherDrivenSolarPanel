#if !NoKerbalism
using HarmonyLib;
using KERBALISM;
using KSP.Localization;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using WDSP_GenericFunctionModule;
using static KERBALISM.SolarPanelFixer;

namespace WeatherDrivenSolarPanel
{
    public class WDSPWeatherStatusDisplay : PartModule
    {
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "#WDSP_TVC_weatherStatus")]                    //Weather Status
        public string weatherPanelStatus = string.Empty;

        [KSPField(isPersistant = true)]
        public double totalWeatherTime = 0.0;
        [KSPField(isPersistant = true)]
        public double wearFactorTVC = 1.0;
        [KSPField(isPersistant = true)]
        public double timeTimer = 0.0;
        [KSPField(isPersistant = true)]
        public double timeWeather = -1.0;
        [KSPField(isPersistant = true)]
        public double startTime = -1.0;

        private SolarPanelFixer solarFixer;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            solarFixer = part.FindModuleImplementing<SolarPanelFixer>();
            // Reset timers to prevent massive jump in accumulated time due to time elapsed while in background/unloaded.
            // Background processing should handle its own time accumulation if applicable.
            timeWeather = -1.0; 
            if (state != StartState.Editor)
            {
                if (startTime < 0) startTime = Planetarium.GetUniversalTime();
            }
        }

        public override void OnUpdate()
        {
            if (solarFixer == null || (solarFixer.state != PanelState.Extended && solarFixer.state != PanelState.Static && solarFixer.state != PanelState.ExtendedFixed)
                || solarFixer.vessel == null || solarFixer.vessel.atmDensity <= 0 || solarFixer.wearFactor == 0.0)
            {
                Fields["weatherPanelStatus"].guiActive = false;
                return;
            }
            Fields["weatherPanelStatus"].guiActive = true;
        }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class WDSPInjector : MonoBehaviour
    {
        public void Awake()
        {
            Harmony harmony = new Harmony("WeatherDrivenSolarPanel");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(SolarPanelFixer), "FixedUpdate")]
    public static class FixedUpdate_SolarFixer
    {
        public static string layerName = null;

        //Change the value of status of the solar panel.
        public static double statusChangeValue = 1.0;
        public const string prefix2 = "#WDSP_TVC_";
        public static string GetLocWDSP(string template) => Localizer.Format(prefix2 + template);
        public static string WDSP_TVC_cloudyAffect = GetLocWDSP("cloudyAffect");                                       // "Cloudy Weather"
        public static string WDSP_TVC_dustStormAffect = GetLocWDSP("dustStormAffect");                                 // "Sandstorm/Dust Storm"
        public static string WDSP_TVC_precipitationAffect = GetLocWDSP("precipitationAffect");                         // "Precipitation/Thunderstorm Weather"
        public static string WDSP_TVC_volcanoesAffect = GetLocWDSP("volcanoesAffect");                                 // "Affected by volcanoes"
        public static string WDSP_TVC_sunDirect = GetLocWDSP("sunDirect");                                             // "Sunny"
        public static string WDSP_TVC_weatherStatus = GetLocWDSP("weatherStatus");                                     // "Weather Status"
        // Define and initialize the cloud dictionary
        static Dictionary<string, HashSet<string>> categoryDictionary = new Dictionary<string, HashSet<string>>();
        private static bool configLoaded = false;

        private static void LoadConfig()
        {
            if (configLoaded) return;
            ConfigNode node = GameDatabase.Instance.GetConfigNodes("WDSP_CONFIG").FirstOrDefault();
            if (node != null)
            {
                ConfigNode categories = node.GetNode("WEATHER_CATEGORIES");
                if (categories != null)
                {
                    foreach (ConfigNode.Value value in categories.values)
                    {
                        if (!categoryDictionary.ContainsKey(value.name))
                        {
                            categoryDictionary[value.name] = new HashSet<string>();
                        }

                        string[] layers = value.value.Split(',');
                        foreach (string layer in layers)
                        {
                            string trimmed = layer.Trim();
                            if (!string.IsNullOrEmpty(trimmed))
                            {
                                categoryDictionary[value.name].Add(trimmed);
                            }
                        }
                    }
                }
            }
            configLoaded = true;
        }
        public static readonly FloatCurve weatherTimeEfficCurve = new FloatCurve();
        public static readonly FloatCurve timeEfficCurveNonRO = new FloatCurve();

        public static bool switchWeatherAffectWear;
        public static bool switchTimeDecayWear;

        private static readonly ConditionalWeakTable<SolarPanelFixer, WDSPWeatherStatusDisplay> _moduleCache = new ConditionalWeakTable<SolarPanelFixer, WDSPWeatherStatusDisplay>();
        private static Dictionary<string, string> reverseCategoryDictionary;

        public static bool Prefix(SolarPanelFixer __instance)
        {
            LoadConfig();
            // sanity check
            if (__instance.SolarPanel == null || Lib.IsEditor())
            {
                return true;
            }

            if (!_moduleCache.TryGetValue(__instance, out WDSPWeatherStatusDisplay wdsp))
            {
                wdsp = __instance.part.FindModuleImplementing<WDSPWeatherStatusDisplay>();
                if (wdsp != null)
                {
                    _moduleCache.Add(__instance, wdsp);
                }
            }
            
            if (wdsp == null) return true;

            // Keep resetting launchUT in prelaunch state.
            // It is possible for that value to come from craft file which could result in panels being degraded from the start.
            if (Lib.IsFlight() && __instance.vessel != null && __instance.vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                wdsp.startTime = Planetarium.GetUniversalTime();
                if (__instance.vessel != null && __instance.vessel.atmDensity > 0 && switchWeatherAffectWear)
                {
                    wdsp.timeWeather = Planetarium.GetUniversalTime();
                }
                __instance.wearFactor = 1.0;
                wdsp.totalWeatherTime = 0.0;
                wdsp.timeTimer = 0.0;
                wdsp.wearFactorTVC = 1.0;

                __instance.panelStatusWear = "0 %".ToString();
                __instance.Fields["panelStatusWear"].guiActive = false;
            }

            // can't produce anything if not deployed, broken, etc
            PanelState newState = __instance.SolarPanel.GetState();
            if (__instance.state != newState)
            {
                // If repaired, the partial state will be restored.
                if (__instance.state == PanelState.Broken && newState == PanelState.Retracted)
                {
                    wdsp.startTime = Planetarium.GetUniversalTime();
                    if (__instance.vessel != null && __instance.vessel.atmDensity > 0 && switchWeatherAffectWear)
                    {
                        wdsp.timeWeather = Planetarium.GetUniversalTime();
                    }
                    __instance.wearFactor = 1.0;
                    wdsp.totalWeatherTime = 0.0;
                    wdsp.timeTimer = 0.0;
                    wdsp.wearFactorTVC = 1.0;

                    __instance.panelStatusWear = "0 %".ToString();
                    __instance.Fields["panelStatusWear"].guiActive = false;
                }
                __instance.state = newState;
                if (Lib.IsEditor() && (newState == PanelState.Extended || newState == PanelState.ExtendedFixed || newState == PanelState.Retracted))
                    Lib.RefreshPlanner();

                if (newState == PanelState.Extended || newState == PanelState.ExtendedFixed || newState == PanelState.Static)
                {
                    wdsp.startTime = Planetarium.GetUniversalTime();
                    wdsp.timeWeather = Planetarium.GetUniversalTime();
                }
                else
                {
                    wdsp.startTime = -1.0;
                    wdsp.timeWeather = -1.0;
                }
            }            

            // Get the current time
            if (__instance.state == PanelState.Extended || __instance.state == PanelState.ExtendedFixed || __instance.state == PanelState.Static)
            {
                if (wdsp.startTime < 0) 
                    wdsp.startTime = Planetarium.GetUniversalTime();
                if (__instance.vessel != null && __instance.vessel.atmDensity > 0 && switchWeatherAffectWear)
                {
                    if (wdsp.timeWeather < 0) 
                        wdsp.timeWeather = Planetarium.GetUniversalTime();
                }
            }

            if (!(__instance.state == PanelState.Extended || __instance.state == PanelState.ExtendedFixed || __instance.state == PanelState.Static))
            {
                // Fix: Update wearFactor even when retracted to ensure correct display on load
                double _wearFactorTime = 1.0;
                if ((__instance.vessel.situation != Vessel.Situations.PRELAUNCH) && switchTimeDecayWear)
                {
                    if (__instance.timeEfficCurve?.Curve.keys.Length > 1 && __instance.hasRUI)
                    {
                        _wearFactorTime = __instance.timeEfficCurve.Evaluate((float)(wdsp.timeTimer / 3600.0));
                    }
                    else if (!__instance.hasRUI)
                    {
                        _wearFactorTime = timeEfficCurveNonRO.Evaluate((float)(wdsp.timeTimer / 21600.0));
                    }
                }
                __instance.wearFactor = _wearFactorTime * wdsp.wearFactorTVC;

                __instance.exposureState = ExposureState.Disabled;
                __instance.currentOutput = 0.0;
                return true;
            }

            // get vessel data from cache
            VesselData vd = __instance.vessel.KerbalismData();

            // do nothing if vessel is invalid
            if (!vd.IsSimulated)
            {
                return true;
            }

            if (!__instance.manualTracking && (__instance.state == PanelState.Extended || __instance.state == PanelState.ExtendedFixed || __instance.state == PanelState.Static))
            {
                VesselData.SunInfo bestSun = null;

                if (vd.EnvIsAnalytic)
                {
                    // Mode A: Analytic Mode - Find the star providing the maximum solar flux
                    double maxFlux = -1.0;
                    foreach (var sunInfo in vd.EnvSunsInfo)
                    {
                        if (sunInfo.SolarFlux > maxFlux)
                        {
                            maxFlux = sunInfo.SolarFlux;
                            bestSun = sunInfo;
                        }
                    }
                }
                else
                {
                    // Mode B: Standard Mode - Find the brightest star that is not currently occluded
                    double maxActiveFlux = -1.0;
                    foreach (var sunInfo in vd.EnvSunsInfo)
                    {
                        if (sunInfo.SunlightFactor > 0.05)
                        {
                            if (sunInfo.SolarFlux > maxActiveFlux)
                            {
                                maxActiveFlux = sunInfo.SolarFlux;
                                bestSun = sunInfo;
                            }
                        }
                    }

                    // Fallback: If all stars are blocked, target the nearest star by distance
                    if (bestSun == null)
                    {
                        double minDistance = double.MaxValue;
                        foreach (var sunInfo in vd.EnvSunsInfo)
                        {
                            double dist = Vector3d.Distance(__instance.vessel.GetWorldPos3D(), sunInfo.SunData.body.position);
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                bestSun = sunInfo;
                            }
                        }
                    }
                }

                // Update tracked sun in auto mode
                if (bestSun != null && __instance.trackedSunIndex != bestSun.SunData.bodyIndex)
                {
                    __instance.trackedSunIndex = bestSun.SunData.bodyIndex;
                    __instance.SolarPanel.SetTrackedBody(bestSun.SunData.body);
                }
            }

            __instance.trackedSunInfo = vd.EnvSunsInfo.Find(p => p.SunData.bodyIndex == __instance.trackedSunIndex);

            if (__instance.trackedSunInfo.SunlightFactor == 0.0)
                __instance.exposureState = ExposureState.InShadow;
            else
                __instance.exposureState = ExposureState.Exposed;


            // --- A factor specifically designed to calculate actual electricity generation ---
            double powerFactor = 0.0;
            if (vd.EnvIsAnalytic)
            {
                __instance.analyticSunlight = true;
                powerFactor = CalculateMultiStarPowerAnalytic(__instance.vessel, vd.EnvSunsInfo, __instance.trackedSunInfo, __instance.SolarPanel.Type, __instance.SolarPanel.IsTracking);
            }
            else
            {
                __instance.analyticSunlight = false;
                // reset factors
                __instance.exposureFactor = 0.0;
                powerFactor = 0.0;

                // iterate over all stars, compute the exposure factor
                foreach (VesselData.SunInfo sunInfo in vd.EnvSunsInfo)
                {
                    // ignore insignifiant flux from distant stars
                    if (sunInfo != __instance.trackedSunInfo && sunInfo.SolarFlux < 1e-6)
                        continue;

                    double sunCosineFactor = 0.0;
                    double sunOccludedFactor = 0.0;
                    string occludingPart = null;

                    // Get the cosine factor (alignement between the sun and the panel surface)
                    sunCosineFactor = __instance.SolarPanel.GetCosineFactor(sunInfo.Direction);

                    if (sunCosineFactor == 0.0)
                    {
                        if (sunInfo == __instance.trackedSunInfo)
                            __instance.exposureState = ExposureState.BadOrientation;
                        sunCosineFactor = 0.0;
                    }
                    else
                    {
                        // The panel is oriented toward the sun, do a physic raycast to check occlusion
                        sunOccludedFactor = __instance.SolarPanel.GetOccludedFactor(sunInfo.Direction, out occludingPart, sunInfo != __instance.trackedSunInfo);

                        if (sunInfo == __instance.trackedSunInfo && sunOccludedFactor == 0.0)
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

                    if (sunInfo.SunlightFactor == 1.0)
                    {
                        // Core: Angle of the star * Occlusion of the star * (Actual flux of the star / Reference flux)
                        double starDistanceFactor = sunInfo.SolarFlux / Sim.SolarFluxAtHome;
                        powerFactor += sunCosineFactor * sunOccludedFactor * starDistanceFactor;

                    }
                    else if (sunInfo == __instance.trackedSunInfo)
                    {
                        __instance.exposureState = ExposureState.InShadow;
                    }

                    if (sunInfo == __instance.trackedSunInfo)
                    {
                        __instance.exposureFactor = sunCosineFactor * sunOccludedFactor;
                    }
                }
            }

            // get Weather Impact factor (Weather Impact are not calculated in analysis mode)
            double WeatherImpactFactor = 1.0;
            if (__instance.vessel.atmDensity > 0 && switchWeatherAffectWear)
            {
                WeatherImpactFactor = GenericFunctionModule.VolumetricCloudTransmittance(__instance.trackedSunInfo.sunData.body, out string NlayerName);
                layerName = NlayerName;
                statusChangeValue = WeatherImpactFactor;
                Debug.Log($"layerName的值是{layerName}");
                Debug.Log($"GetCategoryByValue的值是{GetCategoryByValue(layerName)}");
                if (__instance.vessel != null
                    && (GetCategoryByValue(layerName) != "cloudyAffect")
                    && WeatherImpactFactor < 0.9f
                    && (__instance.vessel.situation != Vessel.Situations.PRELAUNCH))
                {

                    if (__instance.state == PanelState.Extended || __instance.state == PanelState.ExtendedFixed || __instance.state == PanelState.Static)
                    {
                        wdsp.totalWeatherTime += Planetarium.GetUniversalTime() - wdsp.timeWeather;
                        wdsp.timeWeather = Planetarium.GetUniversalTime();
                    }
                }
                wdsp.weatherPanelStatus = CalculateStatus(wdsp, wdsp.totalWeatherTime);
            }
            else if (switchWeatherAffectWear == false)
            {
                WeatherImpactFactor = GenericFunctionModule.VolumetricCloudTransmittance(__instance.trackedSunInfo.sunData.body, out string NlayerName);
                layerName = NlayerName;
                statusChangeValue = WeatherImpactFactor;
                wdsp.weatherPanelStatus = CalculateStatus(wdsp);
            }

            // get wear factor (time based output degradation)
            double wearFactorTime = 1.0;
            if ((__instance.vessel.situation != Vessel.Situations.PRELAUNCH) && switchTimeDecayWear)
            {
                if (__instance.state == PanelState.Extended || __instance.state == PanelState.ExtendedFixed || __instance.state == PanelState.Static)
                {
                    wdsp.timeTimer += Planetarium.GetUniversalTime() - wdsp.startTime;
                    wdsp.startTime = Planetarium.GetUniversalTime();
                }

                if (__instance.timeEfficCurve?.Curve.keys.Length > 1 && __instance.hasRUI)
                {
                    wearFactorTime = __instance.timeEfficCurve.Evaluate((float)(wdsp.timeTimer / 3600.0));
                }
                else if (!__instance.hasRUI)
                {
                    wearFactorTime = timeEfficCurveNonRO.Evaluate((float)(wdsp.timeTimer / 21600.0));
                }
            }

            // get final output rate in EC/s
            __instance.wearFactor = wearFactorTime * wdsp.wearFactorTVC;


            if (__instance.wearFactor == 0.0)
            {
                __instance.Fields["panelMode"].guiActive = false;
                __instance.Fields["panelStatusEnergy"].guiActive = false;
                __instance.Fields["panelStatusExposure"].guiActive = false;
                __instance.state = PanelState.Failure;
                __instance.exposureState = ExposureState.Disabled;
            }
            else 
            {
                __instance.currentOutput = __instance.nominalRate * powerFactor;
                // ignore very small outputs
                if (__instance.currentOutput < 1e-10)
                {
                    __instance.currentOutput = 0.0;
                    return false;
                }
                __instance.currentOutput = __instance.currentOutput * __instance.wearFactor * WeatherImpactFactor;
                // get resource handler
                ResourceInfo res = KERBALISM.ResourceCache.GetResource(__instance.vessel, __instance.resourceName);
                // produce resource
                res.Produce(__instance.currentOutput * Kerbalism.elapsed_s, KERBALISM.ResourceBroker.SolarPanel);
            }
            return false;
        }

        public static string GetCategoryByValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Not Found!";

            if (reverseCategoryDictionary == null) InitReverseDictionary();
            
            if (reverseCategoryDictionary.TryGetValue(value, out string category))
            {
                return category;
            }
            return "Not Found!";
        }

        private static void InitReverseDictionary()
        {
            LoadConfig();
            if (reverseCategoryDictionary != null) return;
            reverseCategoryDictionary = new Dictionary<string, string>();
            foreach (var kvp in categoryDictionary)
            {
                foreach (var val in kvp.Value)
                {
                    if (!reverseCategoryDictionary.ContainsKey(val))
                    {
                        reverseCategoryDictionary.Add(val, kvp.Key);
                    }
                }
            }
        }

        public static string CalculateStatus(WDSPWeatherStatusDisplay wdsp, double weatherTime = -1.0)
        {
            string category = GetCategoryByValue(layerName);
            string statusText = WDSP_TVC_sunDirect;
            string color = "FF7F00";
            bool updateWear = weatherTime >= 0;

            switch (category)
            {
                case "cloudyAffect":
                    if (statusChangeValue < 0.8f)
                    {
                        statusText = WDSP_TVC_cloudyAffect;
                        color = "5F9F9F";
                    }
                    break;
                case "precipitationAffect":
                    if (statusChangeValue < 0.8f)
                    {
                        if (updateWear && statusChangeValue < 0.45f)
                            wdsp.wearFactorTVC = Lib.Clamp(weatherTimeEfficCurve.Evaluate((float)(weatherTime / 21600.0)), 0.0, 1.0);
                        statusText = WDSP_TVC_precipitationAffect;
                        color = "5F9F9F";
                    }
                    break;
                case "dustStormAffect":
                    if (statusChangeValue < 0.8f)
                    {
                        if (updateWear && statusChangeValue < 0.75f)
                            wdsp.wearFactorTVC = Lib.Clamp(weatherTimeEfficCurve.Evaluate((float)(weatherTime / 21600.0)), 0.0, 1.0);
                        statusText = WDSP_TVC_dustStormAffect;
                        color = "5F9F9F";
                    }
                    break;
                case "volcanoesAffect":
                    if (statusChangeValue < 0.8f)
                    {
                        if (updateWear && statusChangeValue < 0.65f)
                            wdsp.wearFactorTVC = Lib.Clamp(weatherTimeEfficCurve.Evaluate((float)(weatherTime / 21600.0)), 0.0, 1.0);
                        statusText = WDSP_TVC_volcanoesAffect;
                        color = "5F9F9F";
                    }
                    break;
            }
            return $"<color=#{color}>{statusText}</color>";
        }

        public static void InitCurves()
        {
            InitReverseDictionary();
            if (timeEfficCurveNonRO.Curve.keys.Length > 0) return;

            timeEfficCurveNonRO.Add(0f, 1.0f, -3.521126E-05f, -3.521126E-05f);
            timeEfficCurveNonRO.Add(4260f, 0.85f, -3.638498E-05f, -3.638498E-05f);
            timeEfficCurveNonRO.Add(6390f, 0.77f, -3.521128E-05f, -3.521128E-05f);
            timeEfficCurveNonRO.Add(8520f, 0.7f, -2.582158E-05f, -2.582158E-05f);
            timeEfficCurveNonRO.Add(10650f, 0.66f, -4.694836E-05f, -4.694836E-05f);
            timeEfficCurveNonRO.Add(12780f, 0.5f, -8.450705E-05f, -8.450705E-05f);
            timeEfficCurveNonRO.Add(14910f, 0.3f, -6.455398E-05f, -6.455398E-05f);
            timeEfficCurveNonRO.Add(19170f, 0.15f, -5.28169E-05f, -5.28169E-05f);
            timeEfficCurveNonRO.Add(21300f, 0f, -7.042254E-05f, -7.042254E-05f);

            weatherTimeEfficCurve.Add(0f, 1f, -0.0004694836f, -0.0004694836f);
            weatherTimeEfficCurve.Add(426f, 0.8f, -0.0005868545f, -0.0005868545f);
            weatherTimeEfficCurve.Add(852f, 0.5f, -0.000528169f, -0.000528169f);
            weatherTimeEfficCurve.Add(1278f, 0.35f, -0.0003521127f, -0.0003521127f);
            weatherTimeEfficCurve.Add(1704f, 0.2f, -0.0004107981f, -0.0004107981f);
            weatherTimeEfficCurve.Add(2130f, 0f, -0.0004694836f, -0.0004694836f);
        }
    }

    [HarmonyPatch(typeof(SolarPanelFixer), "BackgroundUpdate")]
    public static class BackgroundUpdate_Patch
    {
        private static ProtoPartModuleSnapshot GetWDSPModule(Vessel v, ProtoPartModuleSnapshot m)
        {
            if (v.protoVessel != null)
            {
                foreach (var p in v.protoVessel.protoPartSnapshots)
                {
                    if (p.modules.Contains(m))
                    {
                        return p.modules.Find(mod => mod.moduleName == "WDSPWeatherStatusDisplay");
                    }
                }
            }
            return null;
        }

        public static bool Prefix(Vessel v, ProtoPartSnapshot p, ProtoPartModuleSnapshot m, SolarPanelFixer prefab, VesselData vd, ResourceInfo ec, double elapsed_s)
        {
            for (int i = 0; i < p.modules.Count; i++)
            {
                if (p.modules[i].moduleName == "Reliability" && Lib.Proto.GetBool(p.modules[i], "broken"))
                {
                    string type = Lib.Proto.GetString(p.modules[i], "type");
                    if (type == "SolarPanelFixer" || (prefab.SolarPanel != null && prefab.SolarPanel.TargetModule != null && type == prefab.SolarPanel.TargetModule.moduleName))
                    {
                        UnityEngine.Profiling.Profiler.EndSample();
                        return true;
                    }
                }
            }

            FixedUpdate_SolarFixer.InitCurves();
            string state = Lib.Proto.GetString(m, "state");

            ProtoPartModuleSnapshot wdsp = GetWDSPModule(v, m);
            if (wdsp == null) return true;

            if (!(state == "Static" || state == "Extended" || state == "ExtendedFixed"))
            {
                Lib.Proto.Set(wdsp, "startTime", -1.0);
                Lib.Proto.Set(wdsp, "timeWeather", -1.0);
                
                return true;
            }

            // this is ugly spaghetti code but initializing the prefab at loading time is messy because the targeted solar panel module may not be loaded yet
            if (!prefab.isInitialized) prefab.OnStart(PartModule.StartState.None);
            if (prefab.resourceName != "ElectricCharge")
            {
                ec = KERBALISM.ResourceCache.GetResource(v, prefab.resourceName);
            }

            double efficiencyFactor = 0.0;
            
            // Retrieve tracking info
            int trackedSunIndex = Lib.Proto.GetInt(m, "trackedSunIndex");
            bool manualTracking = Lib.Proto.GetBool(m, "manualTracking");
            bool isTracking = prefab.SolarPanel.IsTracking;

            VesselData.SunInfo trackedSunInfo = vd.EnvSunsInfo.Find(s => s.SunData.bodyIndex == trackedSunIndex);
            // Auto-tracking logic for background/analytic mode
            if (!manualTracking && isTracking && vd.EnvSunsInfo.Count > 0)
            {
                VesselData.SunInfo bestSun = null;

                if (vd.EnvIsAnalytic)
                {
                    // Mode A: Analytic Mode - Find the star providing the maximum solar flux
                    double maxFlux = -1.0;
                    foreach (var sunInfo in vd.EnvSunsInfo)
                    {
                        if (sunInfo.SolarFlux > maxFlux)
                        {
                            maxFlux = sunInfo.SolarFlux;
                            bestSun = sunInfo;
                        }
                    }
                }
                else
                {
                    // Mode B: Standard Mode - Find the brightest star that is not currently occluded
                    double maxActiveFlux = -1.0;
                    foreach (var sunInfo in vd.EnvSunsInfo)
                    {
                        if (sunInfo.SunlightFactor > 0.05)
                        {
                            if (sunInfo.SolarFlux > maxActiveFlux)
                            {
                                maxActiveFlux = sunInfo.SolarFlux;
                                bestSun = sunInfo;
                            }
                        }
                    }

                    // Fallback: If all stars are blocked, target the nearest star by distance
                    if (bestSun == null)
                    {
                        double minDistance = double.MaxValue;
                        Vector3d vesselPos = Lib.VesselPosition(v);
                        foreach (var sunInfo in vd.EnvSunsInfo)
                        {
                            double dist = Vector3d.Distance(vesselPos, sunInfo.SunData.body.position);
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                bestSun = sunInfo;
                            }
                        }
                    }
                }
                if (bestSun != null)
                {
                    trackedSunInfo = bestSun;
                    // Update the proto if the tracked sun has changed, so it persists
                    if (trackedSunIndex != bestSun.SunData.bodyIndex)
                    {
                        Lib.Proto.Set(m, "trackedSunIndex", bestSun.SunData.bodyIndex);
                    }
                }
            }
            if (trackedSunInfo == null && vd.EnvSunsInfo.Count > 0) trackedSunInfo = vd.EnvSunsInfo[0];
            double powerFactor = CalculateMultiStarPowerAnalytic(v, vd.EnvSunsInfo, trackedSunInfo, prefab.SolarPanel.Type, isTracking);
            efficiencyFactor = powerFactor;
            
            // Load persistent variables
            double wearFactorTVC = Lib.Proto.GetDouble(wdsp, "wearFactorTVC");
            double timeTimer = Lib.Proto.GetDouble(wdsp, "timeTimer");
            double startTime = Lib.Proto.GetDouble(wdsp, "startTime");
            
            // get wear factor (time based output degradation)
            if ((v.situation != Vessel.Situations.PRELAUNCH) && FixedUpdate_SolarFixer.switchTimeDecayWear)
            {
                double now = Planetarium.GetUniversalTime();
                if (startTime > 0)
                {
                    timeTimer += now - startTime;
                    Lib.Proto.Set(wdsp, "timeTimer", timeTimer);
                }
                startTime = now;
                Lib.Proto.Set(wdsp, "startTime", startTime);

                if (prefab.timeEfficCurve?.Curve.keys.Length > 1 && prefab.hasRUI)
                {
                    efficiencyFactor *= prefab.timeEfficCurve.Evaluate((float)(timeTimer / 3600.0));
                }
                else if (!prefab.hasRUI)
                {
                    efficiencyFactor *= FixedUpdate_SolarFixer.timeEfficCurveNonRO.Evaluate((float)(timeTimer / 21600.0));
                }
            }

            // get nominal panel charge rate at 1 AU
            // don't use the prefab value as some modules that does dynamic switching (SSTU) may have changed it
            double nominalRate = Lib.Proto.GetDouble(m, "nominalRate");
            // calculate output
            double output = nominalRate * efficiencyFactor * wearFactorTVC;
            // produce EC
            ec.Produce(output * elapsed_s, KERBALISM.ResourceBroker.SolarPanel);
            return false;
        }
    }


    [HarmonyPatch(typeof(SolarPanelFixer), "Update")]
    public static class Update_SolarFixer
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

            // Update main status field text
            __instance.Fields["panelMode"].guiActive = true;
            if (__instance.analyticSunlight)
            {
                __instance.panelMode = Local.SolarPanelFixer_analytic;
            }
            else
            {
                __instance.panelMode = Local.SolarPanelFixer_realtime;
            }
            __instance.Fields["panelStatus"].guiActive = true;
            __instance.Fields["panelStatusEnergy"].guiActive = false;
            __instance.Fields["panelStatusExposure"].guiActive = false;
            if (__instance.wearFactor == 1.0)
            {
                __instance.Fields["panelStatusWear"].guiActive = false;
            }
            bool addRate = false;
          
            if (__instance.state == PanelState.Failure || __instance.state == PanelState.Unknown)
            {
                __instance.panelStatusWear = "100 %".ToString();
                __instance.wearFactor = 0.0;
                __instance.Fields["panelStatusWear"].guiActive = true;
            }
            switch (__instance.exposureState)
            {
                case ExposureState.InShadow:
                    // In a multi-star environment, smooth transitions are possible when switching celestial bodies.
                    if (__instance.currentOutput >= 1e-3)
                    {
                        goto case ExposureState.Exposed;
                    }
                    __instance.panelStatus = "<color=#ff2222>" + Local.SolarPanelFixer_inshadow + "</color>";//In Shadow
                    addRate = true;
                    break;
                case ExposureState.OccludedTerrain:
                    __instance.panelStatus = "<color=#ff2222>" + Local.SolarPanelFixer_occludedbyterrain + "</color>";//Occluded By Terrain
                    addRate = true;
                    break;
                case ExposureState.OccludedPart:
                    __instance.panelStatus = Lib.BuildString("<color=#ff2222>", Local.SolarPanelFixer_occludedby.Format(__instance.mainOccludingPart), "</color>");//Occluded By 
                    addRate = true;
                    break;
                case ExposureState.BadOrientation:
                    // In a multi-star environment, smooth transitions are possible when switching celestial bodies.
                    if (__instance.currentOutput > 1e-10)
                    {
                        goto case ExposureState.Exposed;
                    }
                    __instance.panelStatus = "<color=#ff2222>" + Local.SolarPanelFixer_badorientation + "</color>";//Bad Orientation
                    addRate = true;
                    break;
                case ExposureState.Disabled:
                    __instance.Fields["panelMode"].guiActive = false;
                    switch (__instance.state)
                    {
                        case PanelState.Retracted: __instance.panelStatus = Local.SolarPanelFixer_retracted; break;//"Retracted"
                        case PanelState.Extending: __instance.panelStatus = Local.SolarPanelFixer_extending; break;//"Extending"
                        case PanelState.Retracting: __instance.panelStatus = Local.SolarPanelFixer_retracting; break;//"Retracting"
                        case PanelState.Broken: __instance.panelStatus = Local.SolarPanelFixer_broken; break;//"Broken"
                        case PanelState.Failure: __instance.panelStatus = Local.SolarPanelFixer_failure; break;//"Failure"
                        case PanelState.Unknown: __instance.panelStatus = Local.SolarPanelFixer_invalidstate; break;//"Invalid State"
                    }
                    break;
                case ExposureState.Exposed:
                    // The value has been adjusted to three decimal places for more accurate display in PAW.
                    if (__instance.currentOutput >= 1e-10 && __instance.currentOutput < 1e-3)
                    {
                        __instance.rateFormat = "F5";
                    }
                    else if (__instance.currentOutput >= 1e-3)
                    {
                        __instance.rateFormat = "F3";
                    }
                    
                    __instance.Fields["panelStatusExposure"].guiActive = true;
                    __instance.Fields["panelStatusEnergy"].guiActive = true;
                    __instance.panelStatus = "<color=#eaff56>" + Local.SolarPanelFixer_sunDirect + "</color>"; //"Sun Direct"
                    __instance.sb.Length = 0;
                    if (Settings.UseSIUnits)
                    {
                        if (__instance.hasRUI)
                            __instance.sb.Append(Lib.SIRate(__instance.currentOutput, __instance.resourceName.GetHashCode()));
                        else
                            __instance.sb.Append(Lib.SIRate(__instance.currentOutput, __instance.EcUIUnit));
                        __instance.panelStatusEnergy = __instance.sb.ToString();
                    }
                    else
                    {
                        __instance.sb.Append(__instance.currentOutput.ToString(__instance.rateFormat));
                        __instance.sb.Append(" ");
                        __instance.sb.Append(__instance.EcUIUnit);
                        __instance.panelStatusEnergy = __instance.sb.ToString();
                    }
                    __instance.sb.Length = 0;
                    if (__instance.analyticSunlight)
                    {
                        __instance.Fields["panelStatus"].guiActive = false;
                        __instance.Fields["panelStatusExposure"].guiActive = false;
                    }
                    else
                    {
                        __instance.Fields["panelStatus"].guiActive = true;
                        __instance.Fields["panelStatusExposure"].guiActive = true;
                        __instance.sb.Append(" ");
                        __instance.sb.Append(__instance.exposureFactor.ToString("P0"));
                        __instance.panelStatusExposure = __instance.sb.ToString();
                    }
                    break;
            }
            if (__instance.wearFactor < 1.0 && (FixedUpdate_SolarFixer.switchTimeDecayWear || FixedUpdate_SolarFixer.switchWeatherAffectWear))
            {
                __instance.Fields["panelStatusWear"].guiActive = true;
                __instance.sb.Length = 0;
                __instance.sb.Append((1.0 - __instance.wearFactor).ToString("P2"));
                __instance.panelStatusWear = __instance.sb.ToString();
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
    }

    [HarmonyPatch(typeof(SolarPanelFixer), "OnStart")]
    public static class OnStart_SolarFixer
    {
        public static void Prefix(SolarPanelFixer __instance)
        {
            LoadConfig();
            FixedUpdate_SolarFixer.InitCurves();

            if (FixedUpdate_SolarFixer.switchTimeDecayWear && FixedUpdate_SolarFixer.switchWeatherAffectWear)
            {
                return;
            }

            WDSPWeatherStatusDisplay wdsp = __instance.part.FindModuleImplementing<WDSPWeatherStatusDisplay>();
            if (wdsp != null)
            {
                if (FixedUpdate_SolarFixer.switchTimeDecayWear)
                {
                    wdsp.totalWeatherTime = 0;
                }
                else if (FixedUpdate_SolarFixer.switchWeatherAffectWear)
                {
                    wdsp.timeTimer = 0;
                }
            }
        }

        private static bool configLoaded = false;
        public static void LoadConfig()
        {
            if (configLoaded) return;
            string configFilePath = KSPUtil.ApplicationRootPath + "GameData/WeatherDrivenSolarPanel/Config/globalConfig.cfg";

            ConfigNode configNode = ConfigNode.Load(configFilePath);
            if (configNode != null)
            {
                ConfigNode myPluginNode = configNode.GetNode("WDSP");
                if (myPluginNode != null)
                {
                    if (myPluginNode.HasValue("switchTimeDecayWear"))
                    {
                        FixedUpdate_SolarFixer.switchTimeDecayWear = bool.Parse(myPluginNode.GetValue("switchTimeDecayWear"));
                    }
                    if (myPluginNode.HasValue("switchWeatherAffectWear"))
                    {
                        FixedUpdate_SolarFixer.switchWeatherAffectWear = bool.Parse(myPluginNode.GetValue("switchWeatherAffectWear"));
                    }
                }
                configLoaded = true;
            }
            else
            {
                Debug.LogError("Failed to load config file: " + configFilePath);
            }
        }
    }
}
#endif
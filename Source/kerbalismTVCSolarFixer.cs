using HarmonyLib;
using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using WDSP_GenericFunctionModule;

namespace WeatherDrivenSolarPanel
{
    public class WDSPWeatherStatusDisplay : PartModule
    {
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "#WDSP_TVC_weatherStatus")]
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

        private PartModule solarFixer;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            solarFixer = FindSolarPanelFixer();
            timeWeather = -1.0;
            if (state != StartState.Editor && startTime < 0)
            {
                startTime = Planetarium.GetUniversalTime();
            }
        }

        public override void OnUpdate()
        {
            if (solarFixer == null)
            {
                solarFixer = FindSolarPanelFixer();
            }

            if (solarFixer == null
                || !KerbalismSolarPanelFixerRuntimePatch.IsPanelDeployed(KerbalismSolarPanelFixerRuntimePatch.GetValue(solarFixer, "state"))
                || solarFixer.vessel == null
                || solarFixer.vessel.atmDensity <= 0
                || KerbalismSolarPanelFixerRuntimePatch.GetDouble(solarFixer, "wearFactor", 1.0) == 0.0)
            {
                Fields["weatherPanelStatus"].guiActive = false;
                return;
            }

            Fields["weatherPanelStatus"].guiActive = true;
        }

        private PartModule FindSolarPanelFixer()
        {
            if (part == null) return null;
            foreach (PartModule module in part.Modules)
            {
                if (module != null && module.moduleName == "SolarPanelFixer")
                {
                    return module;
                }
            }
            return null;
        }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class WDSPInjector : MonoBehaviour
    {
        public void Awake()
        {
            KerbalismSolarPanelFixerRuntimePatch.PatchIfKerbalismPresent();
        }
    }

    public static class KerbalismSolarPanelFixerRuntimePatch
    {
        private const string HarmonyId = "WeatherDrivenSolarPanel";
        private const string Prefix = "#WDSP_TVC_";

        private static readonly FloatCurve weatherTimeEfficCurve = new FloatCurve();
        private static readonly FloatCurve timeEfficCurveNonRO = new FloatCurve();
        private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> memberCache = new Dictionary<Type, Dictionary<string, MemberInfo>>();

        private static bool patched;
        private static bool configLoaded;
        private static bool curvesLoaded;
        private static bool switchWeatherAffectWear;
        private static bool switchTimeDecayWear;

        private static Type solarPanelFixerType;
        private static Type resourceCacheType;
        private static Type resourceBrokerType;
        private static Type kerbalismType;
        private static object solarPanelBroker;

        private static readonly string cloudyAffect = Localizer.Format(Prefix + "cloudyAffect");
        private static readonly string dustStormAffect = Localizer.Format(Prefix + "dustStormAffect");
        private static readonly string precipitationAffect = Localizer.Format(Prefix + "precipitationAffect");
        private static readonly string volcanoesAffect = Localizer.Format(Prefix + "volcanoesAffect");
        private static readonly string sunDirect = Localizer.Format(Prefix + "sunDirect");

        public static void PatchIfKerbalismPresent()
        {
            if (patched) return;

            solarPanelFixerType = AccessTools.TypeByName("KERBALISM.SolarPanelFixer");
            if (solarPanelFixerType == null)
            {
                return;
            }

            resourceCacheType = AccessTools.TypeByName("KERBALISM.ResourceCache");
            resourceBrokerType = AccessTools.TypeByName("KERBALISM.ResourceBroker");
            kerbalismType = AccessTools.TypeByName("KERBALISM.Kerbalism");
            solarPanelBroker = GetStaticValue(resourceBrokerType, "SolarPanel");

            Harmony harmony = new Harmony(HarmonyId);
            MethodInfo fixedUpdate = AccessTools.Method(solarPanelFixerType, "FixedUpdate");
            MethodInfo onStart = AccessTools.Method(solarPanelFixerType, "OnStart");

            if (fixedUpdate != null)
            {
                harmony.Patch(
                    fixedUpdate,
                    postfix: new HarmonyMethod(typeof(KerbalismSolarPanelFixerRuntimePatch), nameof(FixedUpdatePostfix)));
            }

            if (onStart != null)
            {
                harmony.Patch(
                    onStart,
                    postfix: new HarmonyMethod(typeof(KerbalismSolarPanelFixerRuntimePatch), nameof(OnStartPostfix)));
            }

            patched = true;
            Debug.Log("[WDSP] Kerbalism detected; runtime solar panel weather patch enabled.");
        }

        public static void FixedUpdatePostfix(object __instance)
        {
            try
            {
                PartModule fixer = __instance as PartModule;
                if (fixer == null || fixer.part == null || fixer.vessel == null || HighLogic.LoadedSceneIsEditor)
                {
                    return;
                }

                WDSPWeatherStatusDisplay wdsp = fixer.part.FindModuleImplementing<WDSPWeatherStatusDisplay>();
                if (wdsp == null)
                {
                    return;
                }

                InitCurves();

                object solarPanel = GetValue(__instance, "SolarPanel");
                if (solarPanel == null || !IsPanelDeployed(GetValue(__instance, "state")))
                {
                    SetValue(__instance, "wearFactor", CalculateCombinedWear(__instance, wdsp, false));
                    return;
                }

                double originalOutput = GetDouble(__instance, "currentOutput", 0.0);
                if (originalOutput <= 1e-10)
                {
                    return;
                }

                CelestialBody trackedSun = GetTrackedSun(__instance);
                GenericFunctionModule.WeatherSample weatherSample = fixer.vessel.atmDensity > 0
                    ? GenericFunctionModule.SampleWeather(trackedSun)
                    : new GenericFunctionModule.WeatherSample();

                double weatherPowerFactor = fixer.vessel.atmDensity > 0 ? weatherSample.PowerFactor : 1.0;
                double combinedWearFactor = CalculateCombinedWear(__instance, wdsp, true, weatherSample);
                double kerbalismWearFactor = GetDouble(__instance, "wearFactor", 1.0);
                double wdspWearFactor = kerbalismWearFactor > 0.0
                    ? Mathf.Clamp01((float)(combinedWearFactor / kerbalismWearFactor))
                    : 0.0;

                double adjustedOutput = originalOutput * weatherPowerFactor * wdspWearFactor;
                double delta = adjustedOutput - originalOutput;

                SetValue(__instance, "currentOutput", adjustedOutput);
                SetValue(__instance, "wearFactor", combinedWearFactor);

                if (fixer.vessel.atmDensity > 0)
                {
                    wdsp.weatherPanelStatus = CalculateStatus(wdsp, weatherSample, switchWeatherAffectWear ? wdsp.totalWeatherTime : -1.0);
                }

                ApplyResourceDelta(fixer.vessel, delta);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WDSP] Kerbalism runtime patch failed: " + ex.Message);
            }
        }

        public static void OnStartPostfix(object __instance)
        {
            try
            {
                LoadConfig();
                InitCurves();

                PartModule fixer = __instance as PartModule;
                WDSPWeatherStatusDisplay wdsp = fixer != null && fixer.part != null
                    ? fixer.part.FindModuleImplementing<WDSPWeatherStatusDisplay>()
                    : null;

                if (wdsp == null || (switchTimeDecayWear && switchWeatherAffectWear))
                {
                    return;
                }

                if (switchTimeDecayWear)
                {
                    wdsp.totalWeatherTime = 0;
                }
                else if (switchWeatherAffectWear)
                {
                    wdsp.timeTimer = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WDSP] Kerbalism OnStart runtime patch failed: " + ex.Message);
            }
        }

        public static object GetValue(object instance, string name)
        {
            if (instance == null) return null;
            MemberInfo member = GetMember(instance.GetType(), name);
            if (member is FieldInfo field) return field.GetValue(instance);
            if (member is PropertyInfo property) return property.GetValue(instance, null);
            return null;
        }

        public static double GetDouble(object instance, string name, double fallback)
        {
            object value = GetValue(instance, name);
            if (value == null) return fallback;
            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
                return fallback;
            }
        }

        public static bool IsPanelDeployed(object state)
        {
            string value = state != null ? state.ToString() : string.Empty;
            return value == "Extended" || value == "ExtendedFixed" || value == "Static";
        }

        private static void SetValue(object instance, string name, object value)
        {
            if (instance == null) return;
            MemberInfo member = GetMember(instance.GetType(), name);
            if (member is FieldInfo field)
            {
                field.SetValue(instance, ConvertValue(value, field.FieldType));
            }
            else if (member is PropertyInfo property && property.CanWrite)
            {
                property.SetValue(instance, ConvertValue(value, property.PropertyType), null);
            }
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null || targetType.IsInstanceOfType(value))
            {
                return value;
            }
            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, value.ToString());
            }
            return Convert.ChangeType(value, targetType);
        }

        private static MemberInfo GetMember(Type type, string name)
        {
            if (type == null) return null;

            if (!memberCache.TryGetValue(type, out Dictionary<string, MemberInfo> members))
            {
                members = new Dictionary<string, MemberInfo>();
                memberCache[type] = members;
            }

            if (members.TryGetValue(name, out MemberInfo cached))
            {
                return cached;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MemberInfo member = (MemberInfo)type.GetField(name, flags) ?? type.GetProperty(name, flags);
            members[name] = member;
            return member;
        }

        private static object GetStaticValue(Type type, string name)
        {
            if (type == null) return null;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = type.GetField(name, flags);
            if (field != null) return field.GetValue(null);
            PropertyInfo property = type.GetProperty(name, flags);
            return property != null ? property.GetValue(null, null) : null;
        }

        private static CelestialBody GetTrackedSun(object fixer)
        {
            int trackedSunIndex = (int)GetDouble(fixer, "trackedSunIndex", 0);
            if (trackedSunIndex >= 0 && trackedSunIndex < FlightGlobals.Bodies.Count)
            {
                return FlightGlobals.Bodies[trackedSunIndex];
            }
            return global::Sun.Instance != null ? global::Sun.Instance.sun : null;
        }

        private static double CalculateCombinedWear(object fixer, WDSPWeatherStatusDisplay wdsp, bool updateTime, GenericFunctionModule.WeatherSample sample = null)
        {
            LoadConfig();
            double kerbalismWearFactor = GetDouble(fixer, "wearFactor", 1.0);
            double timeWeatherWear = wdsp.wearFactorTVC;

            if (updateTime && switchWeatherAffectWear && sample != null && sample.WearSeverity > 0.05f)
            {
                Vessel vessel = (fixer as PartModule)?.vessel;
                double currentTime = Planetarium.GetUniversalTime();
                if (vessel != null && vessel.situation != Vessel.Situations.PRELAUNCH)
                {
                    if (wdsp.timeWeather > 0)
                    {
                        wdsp.totalWeatherTime += (currentTime - wdsp.timeWeather) * sample.WearSeverity;
                    }
                }
                wdsp.timeWeather = currentTime;
                timeWeatherWear = Mathf.Clamp01(weatherTimeEfficCurve.Evaluate((float)(wdsp.totalWeatherTime / 21600.0)));
                wdsp.wearFactorTVC = timeWeatherWear;
            }

            if (updateTime && switchTimeDecayWear && IsPanelDeployed(GetValue(fixer, "state")))
            {
                double currentTime = Planetarium.GetUniversalTime();
                if (wdsp.startTime > 0)
                {
                    wdsp.timeTimer += currentTime - wdsp.startTime;
                }
                wdsp.startTime = currentTime;
            }

            return Mathf.Clamp01((float)(kerbalismWearFactor * timeWeatherWear));
        }

        private static void ApplyResourceDelta(Vessel vessel, double deltaPerSecond)
        {
            if (vessel == null || Math.Abs(deltaPerSecond) < 1e-10 || resourceCacheType == null)
            {
                return;
            }

            MethodInfo getResource = AccessTools.Method(resourceCacheType, "GetResource", new[] { typeof(Vessel), typeof(string) });
            object resourceInfo = getResource?.Invoke(null, new object[] { vessel, "ElectricCharge" });
            if (resourceInfo == null)
            {
                return;
            }

            double elapsed = GetKerbalismElapsedSeconds();
            string methodName = deltaPerSecond >= 0.0 ? "Produce" : "Consume";
            MethodInfo method = AccessTools.Method(resourceInfo.GetType(), methodName);
            method?.Invoke(resourceInfo, new[] { Math.Abs(deltaPerSecond) * elapsed, solarPanelBroker });
        }

        private static double GetKerbalismElapsedSeconds()
        {
            object elapsed = GetStaticValue(kerbalismType, "elapsed_s");
            if (elapsed == null)
            {
                return TimeWarp.fixedDeltaTime;
            }
            try
            {
                return Convert.ToDouble(elapsed);
            }
            catch
            {
                return TimeWarp.fixedDeltaTime;
            }
        }

        private static string CalculateStatus(WDSPWeatherStatusDisplay wdsp, GenericFunctionModule.WeatherSample sample, double weatherTime = -1.0)
        {
            string statusText = sunDirect;
            string color = "FF7F00";
            bool updateWear = weatherTime >= 0;

            if (sample != null && sample.HasWeather)
            {
                switch (sample.Category)
                {
                    case GenericFunctionModule.CategoryCloudy:
                        if (sample.Severity > 0.08f)
                        {
                            statusText = cloudyAffect;
                            color = "5F9F9F";
                        }
                        break;
                    case GenericFunctionModule.CategoryPrecipitation:
                        if (sample.Severity > 0.05f)
                        {
                            if (updateWear && sample.WearSeverity > 0.05f)
                                wdsp.wearFactorTVC = Mathf.Clamp01(weatherTimeEfficCurve.Evaluate((float)(weatherTime / 21600.0)));
                            statusText = precipitationAffect;
                            color = "5F9F9F";
                        }
                        break;
                    case GenericFunctionModule.CategoryDustStorm:
                        if (sample.Severity > 0.05f)
                        {
                            if (updateWear && sample.WearSeverity > 0.05f)
                                wdsp.wearFactorTVC = Mathf.Clamp01(weatherTimeEfficCurve.Evaluate((float)(weatherTime / 21600.0)));
                            statusText = dustStormAffect;
                            color = "5F9F9F";
                        }
                        break;
                    case GenericFunctionModule.CategoryVolcanoes:
                        if (sample.Severity > 0.05f)
                        {
                            if (updateWear && sample.WearSeverity > 0.05f)
                                wdsp.wearFactorTVC = Mathf.Clamp01(weatherTimeEfficCurve.Evaluate((float)(weatherTime / 21600.0)));
                            statusText = volcanoesAffect;
                            color = "5F9F9F";
                        }
                        break;
                }
            }

            return $"<color=#{color}>{statusText}</color>";
        }

        private static void InitCurves()
        {
            if (curvesLoaded) return;

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

            curvesLoaded = true;
        }

        private static void LoadConfig()
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
                        switchTimeDecayWear = bool.Parse(myPluginNode.GetValue("switchTimeDecayWear"));
                    }
                    if (myPluginNode.HasValue("switchWeatherAffectWear"))
                    {
                        switchWeatherAffectWear = bool.Parse(myPluginNode.GetValue("switchWeatherAffectWear"));
                    }
                }
            }
            else
            {
                Debug.LogError("[WDSP] Failed to load config file: " + configFilePath);
            }

            configLoaded = true;
        }
    }
}

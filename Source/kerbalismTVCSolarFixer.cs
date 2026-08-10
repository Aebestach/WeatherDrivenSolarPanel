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

        [KSPField(guiActive = false, guiActiveUnfocused = false, guiActiveEditor = false, guiName = "#WDSP_PAW_dust")]
        public string panelStatusDust = "0 %";

        [KSPField(isPersistant = true)]
        public double totalWeatherTime = 0.0;
        // Visual dust exposure (dust-storm / volcano layers only).
        [KSPField(isPersistant = true)]
        public double totalDustTime = 0.0;
        // Dust-storm / volcano contribution to weather wear (cleared by EVA cleaning).
        [KSPField(isPersistant = true)]
        public double totalDustWearTime = 0.0;
        [KSPField(isPersistant = true)]
        public double wearFactorTVC = 1.0;
        [KSPField(isPersistant = true)]
        public double timeTimer = 0.0;
        [KSPField(isPersistant = true)]
        public double timeWeather = -1.0;
        [KSPField(isPersistant = true)]
        public double startTime = -1.0;

        [UI_FloatRange(minValue = 0f, maxValue = 21300f, stepIncrement = 5f)]
        [KSPField(guiActive = false, guiActiveEditor = false, guiName = "#WDSP_Debug_dustExposureDays", guiFormat = "F0", guiUnits = " d")]
        public float debugDustExposureDays = 0f;

        private PartModule solarFixer;
        private SolarPanelDustOverlay dustOverlay;
        private float nextDustOverlayRetryTime;
        private bool dustDebugUiBound;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            solarFixer = FindSolarPanelFixer();
            timeWeather = -1.0;
            if (state != StartState.Editor && startTime < 0)
            {
                startTime = Planetarium.GetUniversalTime();
            }

            if (state != StartState.Editor)
            {
                InitializeDustOverlay();
                SetupDustDebugUI();
            }
        }

        public override void OnUpdate()
        {
            if (solarFixer == null)
            {
                solarFixer = FindSolarPanelFixer();
            }

            object panelState = solarFixer != null
                ? KerbalismSolarPanelFixerRuntimePatch.GetValue(solarFixer, "state")
                : null;
            string panelStateName = panelState != null ? panelState.ToString() : string.Empty;
            bool deployed = solarFixer != null
                && (KerbalismSolarPanelFixerRuntimePatch.IsPanelDeployed(panelState)
                    || panelStateName == "Broken"
                    || panelStateName == "Failure");
            // Dust sticks to the mesh for the whole deploy/retract motion; only weather PAW uses deployed.
            UpdateDustOverlay(true);
            SyncDustDebugUI();
            SyncCleanDustEvent();

            if (!deployed
                || solarFixer.vessel == null
                || solarFixer.vessel.atmDensity <= 0
                || KerbalismSolarPanelFixerRuntimePatch.GetDouble(solarFixer, "wearFactor", 1.0) == 0.0)
            {
                Fields["weatherPanelStatus"].guiActive = false;
                return;
            }

            Fields["weatherPanelStatus"].guiActive = true;
        }

        private double GetDisplayedWearFactor()
        {
            if (solarFixer != null)
            {
                return KerbalismSolarPanelFixerRuntimePatch.GetDouble(solarFixer, "wearFactor", wearFactorTVC);
            }
            return wearFactorTVC;
        }

        private void SyncCleanDustEvent()
        {
            BaseEvent cleanEvent = Events["CleanDust"];
            if (cleanEvent == null)
            {
                return;
            }

            bool offer = HighLogic.LoadedSceneIsFlight
                && WDSPDustCleaning.CanOfferCleanButton(
                    GetDisplayedWearFactor(),
                    totalDustTime,
                    totalDustWearTime);
            cleanEvent.guiActive = false;
            cleanEvent.guiActiveUnfocused = offer;
            cleanEvent.externalToEVAOnly = true;
            cleanEvent.unfocusedRange = WDSPDustCleaning.CleanUnfocusedRange;
            cleanEvent.active = true;
        }

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "#WDSP_Debug_addDust")]
        public void DebugAddDustExposure()
        {
            debugDustExposureDays = Mathf.Min(
                WDSPDustVisualMath.MaxDebugExposureDays,
                debugDustExposureDays + WDSPDustVisualMath.DebugExposureStepDays);
            ApplyDebugDustExposureDays(true);
        }

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "#WDSP_Debug_clearDust")]
        public void DebugClearDustExposure()
        {
            debugDustExposureDays = 0f;
            ApplyDebugDustExposureDays(true);
        }

        [KSPEvent(
            guiActive = false,
            guiActiveEditor = false,
            guiActiveUnfocused = true,
            externalToEVAOnly = true,
            unfocusedRange = WDSPDustCleaning.CleanUnfocusedRange,
            guiName = "#WDSP_CleanDust")]
        public void CleanDust()
        {
            double combinedWear = GetDisplayedWearFactor();
            if (!WDSPDustCleaning.CanOfferCleanButton(combinedWear, totalDustTime, totalDustWearTime)
                || !WDSPDustCleaning.HasDustToClean(totalDustTime, totalDustWearTime))
            {
                WDSPDustCleaning.PostMessage(
                    WDSPDustCleaning.FailReason(combinedWear, totalDustTime, totalDustWearTime),
                    false);
                return;
            }

            double previousTvc = Math.Max(1e-6, wearFactorTVC);
            totalDustTime = 0.0;
            totalDustWearTime = 0.0;
            wearFactorTVC = WDSPDustCleaning.EvaluateWeatherWearFactor(totalWeatherTime, totalDustWearTime);

            if (solarFixer != null)
            {
                double currentCombined = KerbalismSolarPanelFixerRuntimePatch.GetDouble(solarFixer, "wearFactor", 1.0);
                double kerbalismOnly = currentCombined / previousTvc;
                KerbalismSolarPanelFixerRuntimePatch.SetValue(
                    solarFixer,
                    "wearFactor",
                    (double)Mathf.Clamp01((float)(kerbalismOnly * wearFactorTVC)));
            }

            UpdateDustStatusPAW();
            UpdateDustOverlay(true);
            SyncCleanDustEvent();
            WDSPDustCleaning.PostMessage(Localizer.Format("#WDSP_CleanDust_success"), true);
        }

        public void OnDestroy()
        {
            if (dustOverlay != null)
            {
                dustOverlay.Dispose();
                dustOverlay = null;
            }
        }

        internal void InitializeDustOverlay(PartModule targetModule = null)
        {
            if (!WDSPGlobalConfig.SwitchDustVisuals || part == null)
            {
                return;
            }

            if (dustOverlay != null)
            {
                dustOverlay.Dispose();
            }

            if (targetModule == null && solarFixer != null)
            {
                targetModule = KerbalismSolarPanelFixerRuntimePatch.GetTargetPanelModule(solarFixer);
            }

            dustOverlay = SolarPanelDustOverlay.Create(
                part,
                SolarPanelDustOverlay.FindPanelAnchors(part, targetModule));
            if (dustOverlay == null)
            {
                nextDustOverlayRetryTime = Time.unscaledTime + 5f;
            }
        }

        private void UpdateDustOverlay(bool deployed)
        {
            if (!WDSPGlobalConfig.SwitchDustVisuals)
            {
                // Difficulty toggle off: remove overlays immediately; exposure data is kept.
                if (dustOverlay != null)
                {
                    dustOverlay.Dispose();
                    dustOverlay = null;
                }
                return;
            }

            if (dustOverlay == null)
            {
                if (Time.unscaledTime < nextDustOverlayRetryTime)
                {
                    return;
                }
                InitializeDustOverlay();
            }

            if (dustOverlay != null)
            {
                dustOverlay.TryRebuildIfIncomplete(part);
                dustOverlay.Update(
                    WDSPDustVisualMath.EvaluateDustAmountFromExposure(totalDustTime),
                    deployed);
            }
        }

        private void UpdateDustStatusPAW()
        {
            bool showDust = WDSPGlobalConfig.SwitchDustVisuals;
            // guiActiveUnfocused is required for EVA PAWs (active vessel is the kerbal, not the craft).
            Fields["panelStatusDust"].guiActive = showDust;
            Fields["panelStatusDust"].guiActiveUnfocused = showDust;
            if (!showDust)
            {
                return;
            }

            float dustAmount = WDSPDustVisualMath.EvaluateDustAmountFromExposure(totalDustTime);
            panelStatusDust = dustAmount.ToString("P0");
        }

        private void SetupDustDebugUI()
        {
            UpdateDustStatusPAW();

            bool showDebug = WDSPGlobalConfig.SwitchDustDebug && WDSPGlobalConfig.SwitchDustVisuals;
            Fields["debugDustExposureDays"].guiActive = showDebug;
            Events["DebugAddDustExposure"].guiActive = showDebug;
            Events["DebugClearDustExposure"].guiActive = showDebug;
            if (!showDebug)
            {
                return;
            }

            debugDustExposureDays = WDSPDustVisualMath.ExposureSecondsToDays(totalDustTime);
            if (Fields["debugDustExposureDays"].uiControlFlight is UI_FloatRange range)
            {
                range.maxValue = WDSPDustVisualMath.MaxDebugExposureDays;
            }
            if (!dustDebugUiBound)
            {
                dustDebugUiBound = true;
                Fields["debugDustExposureDays"].uiControlFlight.onFieldChanged += OnDebugDustExposureChanged;
            }
        }

        private void SyncDustDebugUI()
        {
            UpdateDustStatusPAW();

            bool showDebug = WDSPGlobalConfig.SwitchDustDebug && WDSPGlobalConfig.SwitchDustVisuals;
            if (!showDebug)
            {
                Fields["debugDustExposureDays"].guiActive = false;
                Events["DebugAddDustExposure"].guiActive = false;
                Events["DebugClearDustExposure"].guiActive = false;
                return;
            }

            if (!Fields["debugDustExposureDays"].guiActive)
            {
                SetupDustDebugUI();
            }

            float dustDays = WDSPDustVisualMath.ExposureSecondsToDays(totalDustTime);
            if (Mathf.Abs(dustDays - debugDustExposureDays) > 0.25f)
            {
                debugDustExposureDays = dustDays;
            }
        }

        private void OnDebugDustExposureChanged(BaseField field, object oldValue)
        {
            ApplyDebugDustExposureDays(true);
        }

        private void ApplyDebugDustExposureDays(bool deployed)
        {
            // Debug slider only adjusts dust/volcano exposure — not precipitation wear.
            totalDustTime = WDSPDustVisualMath.ExposureDaysToSeconds(debugDustExposureDays);
            UpdateDustStatusPAW();
            UpdateDustOverlay(deployed);
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

        private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> memberCache = new Dictionary<Type, Dictionary<string, MemberInfo>>();

        private static bool patched;
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
                VesselSolarContext solarContext = VesselSolarContext.GetOrCompute(fixer.vessel);
                GenericFunctionModule.WeatherSample weatherSample = fixer.vessel.atmDensity > 0 && solarContext != null
                    ? solarContext.GetWeatherSample(trackedSun)
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
                    wdsp.weatherPanelStatus = CalculateStatus(
                        wdsp,
                        weatherSample,
                        switchWeatherAffectWear ? wdsp.totalWeatherTime + wdsp.totalDustWearTime : -1.0);
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

                PartModule fixer = __instance as PartModule;
                WDSPWeatherStatusDisplay wdsp = fixer != null && fixer.part != null
                    ? fixer.part.FindModuleImplementing<WDSPWeatherStatusDisplay>()
                    : null;

                if (wdsp != null)
                {
                    wdsp.InitializeDustOverlay(GetTargetPanelModule(fixer));
                }

                if (wdsp == null || (switchTimeDecayWear && switchWeatherAffectWear))
                {
                    return;
                }

                if (switchTimeDecayWear)
                {
                    wdsp.totalWeatherTime = 0;
                    wdsp.totalDustWearTime = 0;
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

        internal static PartModule GetTargetPanelModule(PartModule fixer)
        {
            if (fixer == null)
            {
                return null;
            }

            object solarPanel = GetValue(fixer, "SolarPanel");
            return GetValue(solarPanel, "TargetModule") as PartModule;
        }

        public static bool IsPanelDeployed(object state)
        {
            string value = state != null ? state.ToString() : string.Empty;
            return value == "Extended" || value == "ExtendedFixed" || value == "Static";
        }

        public static void SetValue(object instance, string name, object value)
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

            if (updateTime && sample != null && (switchWeatherAffectWear || WDSPGlobalConfig.SwitchDustVisuals))
            {
                Vessel vessel = (fixer as PartModule)?.vessel;
                double currentTime = Planetarium.GetUniversalTime();
                bool canAccumulate = vessel != null
                    && vessel.situation != Vessel.Situations.PRELAUNCH
                    && wdsp.timeWeather > 0;
                double deltaTime = canAccumulate ? (currentTime - wdsp.timeWeather) : 0.0;

                float dustSeverity = GenericFunctionModule.GetDustAccumulationSeverity(sample);
                if (canAccumulate && dustSeverity > 0.05f)
                {
                    if (switchWeatherAffectWear)
                    {
                        wdsp.totalDustWearTime += deltaTime * dustSeverity;
                    }
                    if (WDSPGlobalConfig.SwitchDustVisuals)
                    {
                        wdsp.totalDustTime += deltaTime * dustSeverity;
                    }
                }
                else if (switchWeatherAffectWear && canAccumulate && sample.WearSeverity > 0.05f)
                {
                    // Precipitation (and other non-dust wear) — not removed by EVA cleaning.
                    wdsp.totalWeatherTime += deltaTime * sample.WearSeverity;
                }

                wdsp.timeWeather = currentTime;
                if (switchWeatherAffectWear)
                {
                    timeWeatherWear = WDSPDustCleaning.EvaluateWeatherWearFactor(
                        wdsp.totalWeatherTime,
                        wdsp.totalDustWearTime);
                    wdsp.wearFactorTVC = timeWeatherWear;
                }
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
                                wdsp.wearFactorTVC = WDSPDustCleaning.EvaluateWeatherWearFactor(
                                    wdsp.totalWeatherTime,
                                    wdsp.totalDustWearTime);
                            statusText = precipitationAffect;
                            color = "5F9F9F";
                        }
                        break;
                    case GenericFunctionModule.CategoryDustStorm:
                        if (sample.Severity > 0.05f)
                        {
                            if (updateWear && sample.WearSeverity > 0.05f)
                                wdsp.wearFactorTVC = WDSPDustCleaning.EvaluateWeatherWearFactor(
                                    wdsp.totalWeatherTime,
                                    wdsp.totalDustWearTime);
                            statusText = dustStormAffect;
                            color = "5F9F9F";
                        }
                        break;
                    case GenericFunctionModule.CategoryVolcanoes:
                        if (sample.Severity > 0.05f)
                        {
                            if (updateWear && sample.WearSeverity > 0.05f)
                                wdsp.wearFactorTVC = WDSPDustCleaning.EvaluateWeatherWearFactor(
                                    wdsp.totalWeatherTime,
                                    wdsp.totalDustWearTime);
                            statusText = volcanoesAffect;
                            color = "5F9F9F";
                        }
                        break;
                }
            }

            return $"<color=#{color}>{statusText}</color>";
        }

        private static void LoadConfig()
        {
            // Always refresh so Difficulty Settings changes apply without restart.
            switchTimeDecayWear = WDSPGlobalConfig.SwitchTimeDecayWear;
            switchWeatherAffectWear = WDSPGlobalConfig.SwitchWeatherAffectWear;
        }
    }
}

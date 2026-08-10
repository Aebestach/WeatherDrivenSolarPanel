using System.Reflection;
using KSP.Localization;

namespace WeatherDrivenSolarPanel
{
    /// <summary>Difficulty column 1 — gameplay toggles (wear / dust visuals).</summary>
    public class WDSPGameplayParameters : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#WDSP_ParamTitleGameplay");
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => "WeatherDrivenSolarPanel";
        public override string DisplaySection => Localizer.Format("#WDSP_ParamSection");
        public override int SectionOrder => 0;
        public override bool HasPresets => true;

        [GameParameters.CustomParameterUI("#WDSP_ParamTimeDecayWear", toolTip = "#WDSP_ParamTimeDecayWear_tip")]
        public bool switchTimeDecayWear = true;

        [GameParameters.CustomParameterUI("#WDSP_ParamWeatherAffectWear", toolTip = "#WDSP_ParamWeatherAffectWear_tip")]
        public bool switchWeatherAffectWear = true;

        [GameParameters.CustomParameterUI("#WDSP_ParamDustVisuals", toolTip = "#WDSP_ParamDustVisuals_tip")]
        public bool switchDustVisuals = true;

        public static WDSPGameplayParameters Instance =>
            HighLogic.CurrentGame?.Parameters.CustomParams<WDSPGameplayParameters>();

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    switchTimeDecayWear = false;
                    switchWeatherAffectWear = false;
                    switchDustVisuals = false;
                    break;
                case GameParameters.Preset.Normal:
                case GameParameters.Preset.Moderate:
                case GameParameters.Preset.Hard:
                default:
                    switchTimeDecayWear = true;
                    switchWeatherAffectWear = true;
                    switchDustVisuals = true;
                    break;
            }
        }
    }

    /// <summary>Difficulty column 2 — mid/end Kerbin-year anchors for wear & dust curves.</summary>
    public class WDSPWearCurveParameters : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#WDSP_ParamTitleWearCurves");
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => "WeatherDrivenSolarPanel";
        public override string DisplaySection => Localizer.Format("#WDSP_ParamSection");
        public override int SectionOrder => 1;
        public override bool HasPresets => true;

        [GameParameters.CustomFloatParameterUI(
            "#WDSP_ParamTimeMidYears",
            toolTip = "#WDSP_ParamTimeMidYears_tip",
            minValue = 1f,
            maxValue = 100f,
            displayFormat = "F1",
            stepCount = 199)]
        public float timeMidYears = WDSPWearCurves.DefaultTimeMidYears;

        [GameParameters.CustomFloatParameterUI(
            "#WDSP_ParamTimeEndYears",
            toolTip = "#WDSP_ParamTimeEndYears_tip",
            minValue = 2f,
            maxValue = 150f,
            displayFormat = "F1",
            stepCount = 297)]
        public float timeEndYears = WDSPWearCurves.DefaultTimeEndYears;

        [GameParameters.CustomFloatParameterUI(
            "#WDSP_ParamWeatherMidYears",
            toolTip = "#WDSP_ParamWeatherMidYears_tip",
            minValue = 0.5f,
            maxValue = 30f,
            displayFormat = "F1",
            stepCount = 295)]
        public float weatherMidYears = WDSPWearCurves.DefaultWeatherMidYears;

        [GameParameters.CustomFloatParameterUI(
            "#WDSP_ParamWeatherEndYears",
            toolTip = "#WDSP_ParamWeatherEndYears_tip",
            minValue = 1f,
            maxValue = 50f,
            displayFormat = "F1",
            stepCount = 245)]
        public float weatherEndYears = WDSPWearCurves.DefaultWeatherEndYears;

        [GameParameters.CustomFloatParameterUI(
            "#WDSP_ParamDustMidYears",
            toolTip = "#WDSP_ParamDustMidYears_tip",
            minValue = 0.5f,
            maxValue = 30f,
            displayFormat = "F1",
            stepCount = 295)]
        public float dustMidYears = WDSPWearCurves.DefaultDustMidYears;

        [GameParameters.CustomFloatParameterUI(
            "#WDSP_ParamDustEndYears",
            toolTip = "#WDSP_ParamDustEndYears_tip",
            minValue = 1f,
            maxValue = 50f,
            displayFormat = "F1",
            stepCount = 245)]
        public float dustEndYears = WDSPWearCurves.DefaultDustEndYears;

        private float lastTimeMidYears = WDSPWearCurves.DefaultTimeMidYears;
        private float lastTimeEndYears = WDSPWearCurves.DefaultTimeEndYears;
        private float lastWeatherMidYears = WDSPWearCurves.DefaultWeatherMidYears;
        private float lastWeatherEndYears = WDSPWearCurves.DefaultWeatherEndYears;
        private float lastDustMidYears = WDSPWearCurves.DefaultDustMidYears;
        private float lastDustEndYears = WDSPWearCurves.DefaultDustEndYears;

        public static WDSPWearCurveParameters Instance =>
            HighLogic.CurrentGame?.Parameters.CustomParams<WDSPWearCurveParameters>();

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                case GameParameters.Preset.Normal:
                    timeMidYears = WDSPWearCurves.DefaultTimeMidYears;
                    timeEndYears = WDSPWearCurves.DefaultTimeEndYears;
                    weatherMidYears = WDSPWearCurves.DefaultWeatherMidYears;
                    weatherEndYears = WDSPWearCurves.DefaultWeatherEndYears;
                    dustMidYears = WDSPWearCurves.DefaultDustMidYears;
                    dustEndYears = WDSPWearCurves.DefaultDustEndYears;
                    break;
                case GameParameters.Preset.Moderate:
                    timeMidYears = 25f;
                    timeEndYears = 40f;
                    weatherMidYears = 1.5f;
                    weatherEndYears = 4f;
                    dustMidYears = 0.8f;
                    dustEndYears = 2f;
                    break;
                case GameParameters.Preset.Hard:
                    timeMidYears = 20f;
                    timeEndYears = 35f;
                    weatherMidYears = 1f;
                    weatherEndYears = 3f;
                    dustMidYears = 0.5f;
                    dustEndYears = 1.5f;
                    break;
            }

            ClampYearPairs();
        }

        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            ClampYearPairs();
            return true;
        }

        private void ClampYearPairs()
        {
            WDSPWearCurves.EnforceYearPair(
                ref timeMidYears, ref timeEndYears, ref lastTimeMidYears, ref lastTimeEndYears,
                1f, 100f, 2f, 150f);
            WDSPWearCurves.EnforceYearPair(
                ref weatherMidYears, ref weatherEndYears, ref lastWeatherMidYears, ref lastWeatherEndYears,
                0.5f, 30f, 1f, 50f);
            WDSPWearCurves.EnforceYearPair(
                ref dustMidYears, ref dustEndYears, ref lastDustMidYears, ref lastDustEndYears,
                0.5f, 30f, 1f, 50f);
        }

    }

    /// <summary>Difficulty column 3 — performance tuning and dust debug.</summary>
    public class WDSPPerformanceParameters : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#WDSP_ParamTitlePerformance");
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => "WeatherDrivenSolarPanel";
        public override string DisplaySection => Localizer.Format("#WDSP_ParamSection");
        public override int SectionOrder => 2;
        public override bool HasPresets => false;

        [GameParameters.CustomIntParameterUI(
            "#WDSP_ParamRayMarchSteps",
            toolTip = "#WDSP_ParamRayMarchSteps_tip",
            minValue = WDSPGlobalConfig.MinWeatherRayMarchSteps,
            maxValue = WDSPGlobalConfig.MaxWeatherRayMarchSteps,
            stepSize = 1)]
        public int weatherRayMarchSteps = WDSPGlobalConfig.DefaultWeatherRayMarchSteps;

        [GameParameters.CustomIntParameterUI(
            "#WDSP_ParamSampleInterval",
            toolTip = "#WDSP_ParamSampleInterval_tip",
            minValue = 1,
            maxValue = 20,
            stepSize = 1)]
        public int weatherSampleInterval = WDSPGlobalConfig.DefaultWeatherSampleInterval;

        [GameParameters.CustomParameterUI("#WDSP_ParamDustDebug", toolTip = "#WDSP_ParamDustDebug_tip")]
        public bool switchDustDebug = false;

        public static WDSPPerformanceParameters Instance =>
            HighLogic.CurrentGame?.Parameters.CustomParams<WDSPPerformanceParameters>();
    }
}

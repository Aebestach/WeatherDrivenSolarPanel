using System;
using UnityEngine;

namespace WeatherDrivenSolarPanel
{
    /// <summary>
    /// Rebuilds time / weather / dust wear curves from difficulty mid/end years
    /// while preserving the shape of the original chart templates.
    /// </summary>
    internal static class WDSPWearCurves
    {
        internal const double KerbinDaySeconds = 21600.0;
        internal const float KerbinYearDays = 426f;
        internal const float DebugExposureStepDays = 100f;

        internal const float DefaultTimeMidYears = 30f;
        internal const float DefaultTimeEndYears = 50f;
        internal const float DefaultWeatherMidYears = 2f;
        internal const float DefaultWeatherEndYears = 5f;
        // Dust coverage ramps faster than weather wear so boards look dirty before they fail.
        internal const float DefaultDustMidYears = 1f;
        internal const float DefaultDustEndYears = 2.5f;

        private struct CurveKey
        {
            public float Time;
            public float Value;
            public float InTangent;
            public float OutTangent;

            public CurveKey(float time, float value, float inTangent, float outTangent)
            {
                Time = time;
                Value = value;
                InTangent = inTangent;
                OutTangent = outTangent;
            }
        }

        // Template: Kerbin days -> efficiency. Mid @ 30y (12780d), end @ 50y (21300d).
        private static readonly CurveKey[] TimeTemplate =
        {
            new CurveKey(0f, 1.0f, -3.521126E-05f, -3.521126E-05f),
            new CurveKey(4260f, 0.85f, -3.638498E-05f, -3.638498E-05f),
            new CurveKey(6390f, 0.77f, -3.521128E-05f, -3.521128E-05f),
            new CurveKey(8520f, 0.7f, -2.582158E-05f, -2.582158E-05f),
            new CurveKey(10650f, 0.66f, -4.694836E-05f, -4.694836E-05f),
            new CurveKey(12780f, 0.5f, -8.450705E-05f, -8.450705E-05f),
            new CurveKey(14910f, 0.3f, -6.455398E-05f, -6.455398E-05f),
            new CurveKey(19170f, 0.15f, -5.28169E-05f, -5.28169E-05f),
            new CurveKey(21300f, 0f, -7.042254E-05f, -7.042254E-05f)
        };

        // Template: Kerbin days -> efficiency. Mid @ 2y (852d), end @ 5y (2130d).
        private static readonly CurveKey[] WeatherTemplate =
        {
            new CurveKey(0f, 1f, -0.0004694836f, -0.0004694836f),
            new CurveKey(426f, 0.8f, -0.0005868545f, -0.0005868545f),
            new CurveKey(852f, 0.5f, -0.000528169f, -0.000528169f),
            new CurveKey(1278f, 0.35f, -0.0003521127f, -0.0003521127f),
            new CurveKey(1704f, 0.2f, -0.0004107981f, -0.0004107981f),
            new CurveKey(2130f, 0f, -0.0004694836f, -0.0004694836f)
        };

        private static FloatCurve timeCurve;
        private static FloatCurve weatherCurve;
        private static FloatCurve dustCurve;
        private static float cachedTimeMid = float.NaN;
        private static float cachedTimeEnd = float.NaN;
        private static float cachedWeatherMid = float.NaN;
        private static float cachedWeatherEnd = float.NaN;
        private static float cachedDustMid = float.NaN;
        private static float cachedDustEnd = float.NaN;

        internal static float MaxDebugExposureDays => YearsToDays(GetDustEndYears());

        internal static float EvaluateTimeEfficiency(double ageSeconds)
        {
            EnsureCurves();
            float days = (float)(Math.Max(0.0, ageSeconds) / KerbinDaySeconds);
            return Mathf.Clamp01(timeCurve.Evaluate(days));
        }

        internal static float EvaluateWeatherEfficiency(double weatherExposureSeconds)
        {
            EnsureCurves();
            float days = (float)(Math.Max(0.0, weatherExposureSeconds) / KerbinDaySeconds);
            return Mathf.Clamp01(weatherCurve.Evaluate(days));
        }

        internal static float EvaluateDustAmountFromExposure(double dustExposureSeconds)
        {
            EnsureCurves();
            float days = (float)(Math.Max(0.0, dustExposureSeconds) / KerbinDaySeconds);
            float remaining = Mathf.Clamp01(dustCurve.Evaluate(days));
            return 1f - remaining;
        }

        internal static float EvaluateDustAmount(double efficiencyFactor)
        {
            return 1f - Mathf.Clamp01((float)efficiencyFactor);
        }

        internal static float ExposureSecondsToDays(double exposureSeconds)
        {
            return (float)(Math.Max(0.0, exposureSeconds) / KerbinDaySeconds);
        }

        internal static double ExposureDaysToSeconds(float exposureDays)
        {
            return Math.Max(0f, exposureDays) * KerbinDaySeconds;
        }

        internal static float YearsToDays(float years)
        {
            return Math.Max(0f, years) * KerbinYearDays;
        }

        internal const float MinYearGap = 0.1f;

        /// <summary>
        /// Ensures mid (50%) is strictly before end (0%/100%).
        /// When invalid, prefer keeping the end anchor and pull mid down.
        /// </summary>
        internal static void SanitizeYears(ref float midYears, ref float endYears, float defaultMid, float defaultEnd)
        {
            if (float.IsNaN(midYears) || float.IsInfinity(midYears) || midYears <= 0f)
            {
                midYears = defaultMid;
            }
            if (float.IsNaN(endYears) || float.IsInfinity(endYears) || endYears <= 0f)
            {
                endYears = defaultEnd;
            }

            midYears = Mathf.Clamp(midYears, 0.1f, 200f);
            endYears = Mathf.Clamp(endYears, 0.2f, 250f);
            if (endYears < midYears + MinYearGap)
            {
                midYears = Mathf.Max(0.1f, endYears - MinYearGap);
                if (endYears < midYears + MinYearGap)
                {
                    endYears = Mathf.Min(250f, midYears + MinYearGap);
                }
            }
        }

        /// <summary>
        /// Difficulty-UI clamp: if the player drags mid past end, bump end up when possible;
        /// if they drag end below mid, pull mid down.
        /// </summary>
        internal static void EnforceYearPair(
            ref float midYears,
            ref float endYears,
            ref float lastMidYears,
            ref float lastEndYears,
            float minMid,
            float maxMid,
            float minEnd,
            float maxEnd)
        {
            midYears = Mathf.Clamp(midYears, minMid, maxMid);
            endYears = Mathf.Clamp(endYears, minEnd, maxEnd);

            if (endYears >= midYears + MinYearGap)
            {
                lastMidYears = midYears;
                lastEndYears = endYears;
                return;
            }

            bool midMovedUp = midYears > lastMidYears + 0.001f;
            bool endMovedDown = endYears < lastEndYears - 0.001f;

            if (midMovedUp && !endMovedDown)
            {
                endYears = Mathf.Min(maxEnd, midYears + MinYearGap);
                if (endYears < midYears + MinYearGap)
                {
                    midYears = Mathf.Max(minMid, endYears - MinYearGap);
                }
            }
            else
            {
                midYears = Mathf.Max(minMid, endYears - MinYearGap);
                if (endYears < midYears + MinYearGap)
                {
                    endYears = Mathf.Min(maxEnd, midYears + MinYearGap);
                }
            }

            lastMidYears = midYears;
            lastEndYears = endYears;
        }

        private static void EnsureCurves()
        {
            float timeMid = GetTimeMidYears();
            float timeEnd = GetTimeEndYears();
            float weatherMid = GetWeatherMidYears();
            float weatherEnd = GetWeatherEndYears();
            float dustMid = GetDustMidYears();
            float dustEnd = GetDustEndYears();

            if (timeCurve == null
                || !Mathf.Approximately(cachedTimeMid, timeMid)
                || !Mathf.Approximately(cachedTimeEnd, timeEnd))
            {
                timeCurve = BuildRemappedCurve(
                    TimeTemplate,
                    DefaultTimeMidYears * KerbinYearDays,
                    DefaultTimeEndYears * KerbinYearDays,
                    YearsToDays(timeMid),
                    YearsToDays(timeEnd));
                cachedTimeMid = timeMid;
                cachedTimeEnd = timeEnd;
            }

            if (weatherCurve == null
                || !Mathf.Approximately(cachedWeatherMid, weatherMid)
                || !Mathf.Approximately(cachedWeatherEnd, weatherEnd))
            {
                weatherCurve = BuildRemappedCurve(
                    WeatherTemplate,
                    DefaultWeatherMidYears * KerbinYearDays,
                    DefaultWeatherEndYears * KerbinYearDays,
                    YearsToDays(weatherMid),
                    YearsToDays(weatherEnd));
                cachedWeatherMid = weatherMid;
                cachedWeatherEnd = weatherEnd;
            }

            if (dustCurve == null
                || !Mathf.Approximately(cachedDustMid, dustMid)
                || !Mathf.Approximately(cachedDustEnd, dustEnd))
            {
                // Dust uses the weather-chart shape: coverage = 1 - efficiency(exposure).
                // Template mid/end stay on the WeatherTemplate anchors (2y / 5y), not the shorter dust defaults.
                dustCurve = BuildRemappedCurve(
                    WeatherTemplate,
                    DefaultWeatherMidYears * KerbinYearDays,
                    DefaultWeatherEndYears * KerbinYearDays,
                    YearsToDays(dustMid),
                    YearsToDays(dustEnd));
                cachedDustMid = dustMid;
                cachedDustEnd = dustEnd;
            }
        }

        private static FloatCurve BuildRemappedCurve(
            CurveKey[] template,
            float templateMidDays,
            float templateEndDays,
            float userMidDays,
            float userEndDays)
        {
            FloatCurve curve = new FloatCurve();
            float scaleBefore = templateMidDays > 0f ? userMidDays / templateMidDays : 1f;
            float scaleAfter = (templateEndDays > templateMidDays)
                ? (userEndDays - userMidDays) / (templateEndDays - templateMidDays)
                : 1f;
            scaleBefore = Mathf.Max(scaleBefore, 1e-4f);
            scaleAfter = Mathf.Max(scaleAfter, 1e-4f);

            for (int i = 0; i < template.Length; i++)
            {
                CurveKey key = template[i];
                float tUser = RemapTime(key.Time, templateMidDays, templateEndDays, userMidDays, userEndDays);
                bool inFirstHalf = key.Time <= templateMidDays + 0.001f;
                bool outFirstHalf = key.Time < templateMidDays - 0.001f;
                float inTangent = key.InTangent / (inFirstHalf ? scaleBefore : scaleAfter);
                float outTangent = key.OutTangent / (outFirstHalf ? scaleBefore : scaleAfter);
                curve.Add(tUser, key.Value, inTangent, outTangent);
            }

            return curve;
        }

        private static float RemapTime(
            float templateTime,
            float templateMid,
            float templateEnd,
            float userMid,
            float userEnd)
        {
            if (templateTime <= templateMid)
            {
                float u = templateMid > 0f ? templateTime / templateMid : 0f;
                return u * userMid;
            }

            float spanTemplate = templateEnd - templateMid;
            float uAfter = spanTemplate > 0f ? (templateTime - templateMid) / spanTemplate : 1f;
            return userMid + uAfter * (userEnd - userMid);
        }

        private static float GetTimeMidYears()
        {
            WDSPWearCurveParameters parameters = WDSPWearCurveParameters.Instance;
            float mid = parameters != null ? parameters.timeMidYears : DefaultTimeMidYears;
            float end = parameters != null ? parameters.timeEndYears : DefaultTimeEndYears;
            SanitizeYears(ref mid, ref end, DefaultTimeMidYears, DefaultTimeEndYears);
            return mid;
        }

        private static float GetTimeEndYears()
        {
            WDSPWearCurveParameters parameters = WDSPWearCurveParameters.Instance;
            float mid = parameters != null ? parameters.timeMidYears : DefaultTimeMidYears;
            float end = parameters != null ? parameters.timeEndYears : DefaultTimeEndYears;
            SanitizeYears(ref mid, ref end, DefaultTimeMidYears, DefaultTimeEndYears);
            return end;
        }

        private static float GetWeatherMidYears()
        {
            WDSPWearCurveParameters parameters = WDSPWearCurveParameters.Instance;
            float mid = parameters != null ? parameters.weatherMidYears : DefaultWeatherMidYears;
            float end = parameters != null ? parameters.weatherEndYears : DefaultWeatherEndYears;
            SanitizeYears(ref mid, ref end, DefaultWeatherMidYears, DefaultWeatherEndYears);
            return mid;
        }

        private static float GetWeatherEndYears()
        {
            WDSPWearCurveParameters parameters = WDSPWearCurveParameters.Instance;
            float mid = parameters != null ? parameters.weatherMidYears : DefaultWeatherMidYears;
            float end = parameters != null ? parameters.weatherEndYears : DefaultWeatherEndYears;
            SanitizeYears(ref mid, ref end, DefaultWeatherMidYears, DefaultWeatherEndYears);
            return end;
        }

        private static float GetDustMidYears()
        {
            WDSPWearCurveParameters parameters = WDSPWearCurveParameters.Instance;
            float mid = parameters != null ? parameters.dustMidYears : DefaultDustMidYears;
            float end = parameters != null ? parameters.dustEndYears : DefaultDustEndYears;
            SanitizeYears(ref mid, ref end, DefaultDustMidYears, DefaultDustEndYears);
            return mid;
        }

        private static float GetDustEndYears()
        {
            WDSPWearCurveParameters parameters = WDSPWearCurveParameters.Instance;
            float mid = parameters != null ? parameters.dustMidYears : DefaultDustMidYears;
            float end = parameters != null ? parameters.dustEndYears : DefaultDustEndYears;
            SanitizeYears(ref mid, ref end, DefaultDustMidYears, DefaultDustEndYears);
            return end;
        }
    }

    /// <summary>Backward-compatible alias used by existing call sites. </summary>
    internal static class WDSPDustVisualMath
    {
        internal const double KerbinDaySeconds = WDSPWearCurves.KerbinDaySeconds;
        internal const float DebugExposureStepDays = WDSPWearCurves.DebugExposureStepDays;
        internal static float MaxDebugExposureDays => WDSPWearCurves.MaxDebugExposureDays;

        internal static float EvaluateWeatherEfficiency(double weatherExposureSeconds) =>
            WDSPWearCurves.EvaluateWeatherEfficiency(weatherExposureSeconds);

        internal static float EvaluateDustAmountFromExposure(double dustExposureSeconds) =>
            WDSPWearCurves.EvaluateDustAmountFromExposure(dustExposureSeconds);

        internal static float EvaluateDustAmount(double efficiencyFactor) =>
            WDSPWearCurves.EvaluateDustAmount(efficiencyFactor);

        internal static float ExposureSecondsToDays(double exposureSeconds) =>
            WDSPWearCurves.ExposureSecondsToDays(exposureSeconds);

        internal static double ExposureDaysToSeconds(float exposureDays) =>
            WDSPWearCurves.ExposureDaysToSeconds(exposureDays);
    }
}

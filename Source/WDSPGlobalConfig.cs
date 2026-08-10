using System;

namespace WeatherDrivenSolarPanel
{
    /// <summary>
    /// Runtime settings. Values come from Difficulty Settings when a game is loaded;
    /// otherwise the compile-time defaults below are used.
    /// </summary>
    public static class WDSPGlobalConfig
    {
        public const int DefaultWeatherRayMarchSteps = 50;
        public const int DefaultWeatherSampleInterval = 1;
        public const int MinWeatherRayMarchSteps = 10;
        public const int MaxWeatherRayMarchSteps = 50;

        public static bool SwitchTimeDecayWear
        {
            get
            {
                WDSPGameplayParameters gameplay = WDSPGameplayParameters.Instance;
                return gameplay != null ? gameplay.switchTimeDecayWear : true;
            }
        }

        public static bool SwitchWeatherAffectWear
        {
            get
            {
                WDSPGameplayParameters gameplay = WDSPGameplayParameters.Instance;
                return gameplay != null ? gameplay.switchWeatherAffectWear : true;
            }
        }

        public static bool SwitchDustVisuals
        {
            get
            {
                WDSPGameplayParameters gameplay = WDSPGameplayParameters.Instance;
                return gameplay != null ? gameplay.switchDustVisuals : true;
            }
        }

        public static bool SwitchDustDebug
        {
            get
            {
                WDSPPerformanceParameters performance = WDSPPerformanceParameters.Instance;
                return performance != null && performance.switchDustDebug;
            }
        }

        public static int WeatherRayMarchSteps
        {
            get
            {
                WDSPPerformanceParameters performance = WDSPPerformanceParameters.Instance;
                int steps = performance != null
                    ? performance.weatherRayMarchSteps
                    : DefaultWeatherRayMarchSteps;
                return ClampInt(steps, MinWeatherRayMarchSteps, MaxWeatherRayMarchSteps);
            }
        }

        public static int WeatherSampleInterval
        {
            get
            {
                WDSPPerformanceParameters performance = WDSPPerformanceParameters.Instance;
                int interval = performance != null
                    ? performance.weatherSampleInterval
                    : DefaultWeatherSampleInterval;
                return Math.Max(1, interval);
            }
        }

        public static void EnsureLoaded()
        {
            // Kept for call-site compatibility; settings are difficulty-driven.
        }

        private static int ClampInt(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(value, max));
        }
    }
}

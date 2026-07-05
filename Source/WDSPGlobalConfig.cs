using System;
using UnityEngine;

namespace WeatherDrivenSolarPanel
{
    /// <summary>Global WDSP settings loaded from GlobalConfig.cfg.</summary>
    public static class WDSPGlobalConfig
    {
        public const int DefaultWeatherRayMarchSteps = 50;
        public const int DefaultWeatherSampleInterval = 1;
        public const int MinWeatherRayMarchSteps = 10;
        public const int MaxWeatherRayMarchSteps = 50;

        private static bool _loaded;
        private static bool _switchTimeDecayWear = true;
        private static bool _switchWeatherAffectWear = true;
        private static int _weatherRayMarchSteps = DefaultWeatherRayMarchSteps;
        private static int _weatherSampleInterval = DefaultWeatherSampleInterval;

        public static bool SwitchTimeDecayWear
        {
            get { EnsureLoaded(); return _switchTimeDecayWear; }
        }

        public static bool SwitchWeatherAffectWear
        {
            get { EnsureLoaded(); return _switchWeatherAffectWear; }
        }

        public static int WeatherRayMarchSteps
        {
            get { EnsureLoaded(); return _weatherRayMarchSteps; }
        }

        /// <summary>Minimum physics steps between full TVC weather resamples (1 = every step).</summary>
        public static int WeatherSampleInterval
        {
            get { EnsureLoaded(); return _weatherSampleInterval; }
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;

            string configFilePath = KSPUtil.ApplicationRootPath + "GameData/WeatherDrivenSolarPanel/Config/globalConfig.cfg";
            ConfigNode configNode = ConfigNode.Load(configFilePath);
            if (configNode != null)
            {
                ConfigNode pluginNode = configNode.GetNode("WDSP");
                if (pluginNode != null)
                {
                    if (pluginNode.HasValue("switchTimeDecayWear"))
                        _switchTimeDecayWear = bool.Parse(pluginNode.GetValue("switchTimeDecayWear"));

                    if (pluginNode.HasValue("switchWeatherAffectWear"))
                        _switchWeatherAffectWear = bool.Parse(pluginNode.GetValue("switchWeatherAffectWear"));

                    if (pluginNode.HasValue("weatherRayMarchSteps"))
                        _weatherRayMarchSteps = ClampInt(int.Parse(pluginNode.GetValue("weatherRayMarchSteps")), MinWeatherRayMarchSteps, MaxWeatherRayMarchSteps);

                    if (pluginNode.HasValue("weatherSampleInterval"))
                        _weatherSampleInterval = Math.Max(1, int.Parse(pluginNode.GetValue("weatherSampleInterval")));
                }
            }
            else
            {
                Debug.LogError("[WDSP] Failed to load config file: " + configFilePath);
            }

            _loaded = true;
        }

        private static int ClampInt(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(value, max));
        }
    }
}

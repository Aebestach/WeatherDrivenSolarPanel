using Atmosphere;
using System;
using UnityEngine;
using Utils;
using System.Collections.Generic;
using System.Linq;

namespace WDSP_GenericFunctionModule
{
    public class GenericFunctionModule
    {
        public const string CategorySunny = "sunDirect";
        public const string CategoryCloudy = "cloudyAffect";
        public const string CategoryPrecipitation = "precipitationAffect";
        public const string CategoryDustStorm = "dustStormAffect";
        public const string CategoryVolcanoes = "volcanoesAffect";

        public class WeatherSample
        {
            public string DominantLayerName = null;
            public string Category = CategorySunny;
            public double PowerFactor = 1.0;
            public float SunTransmittance = 1.0f;
            public float LocalCoverage = 0.0f;
            public float Severity = 0.0f;
            public float WearSeverity = 0.0f;
            public float PrecipitationSeverity = 0.0f;
            public float DustSeverity = 0.0f;
            public float LightningSeverity = 0.0f;
            public bool HasWeather = false;
        }

        private class SmoothedWeatherState
        {
            public double LastUT = -1.0;
            public float Severity = 0.0f;
            public float WearSeverity = 0.0f;
            public double PowerFactor = 1.0;
            public string Category = CategorySunny;
            public string LayerName = null;
        }

        private class CachedBodyLayers
        {
            public int SourceCount = -1;
            public int BuiltFrame = -1;
            public GameScenes Scene = GameScenes.LOADING;
            public readonly List<CloudsObject> Layers = new List<CloudsObject>();
        }

        private class WeatherCacheEntry
        {
            public int LastPhysicsStepId = -1;
            public WeatherSample Sample;
        }

        private const int BodyLayerCacheFrameLifetime = 120;
        private const float EarlyExitDensityThreshold = 12.0f;
        private static HashSet<string> excludedLayers = new HashSet<string>();
        private static Dictionary<string, string> layerToCategoryMap = new Dictionary<string, string>();
        private static bool configLoaded = false;
        private static readonly Dictionary<string, CachedBodyLayers> bodyLayerCache = new Dictionary<string, CachedBodyLayers>();
        private static readonly List<CloudsObject> emptyCloudLayerList = new List<CloudsObject>();
        private static readonly Dictionary<string, SmoothedWeatherState> smoothedStates = new Dictionary<string, SmoothedWeatherState>();
        private static readonly Dictionary<string, WeatherCacheEntry> weatherSampleCache = new Dictionary<string, WeatherCacheEntry>();

        private static double _lastPhysicsUniversalTime = -1.0;
        private static int _physicsStepId = 0;

        public static int PhysicsStepId
        {
            get { return _physicsStepId; }
        }

        public static void NotifyPhysicsStep(double universalTime)
        {
            if (_lastPhysicsUniversalTime != universalTime)
            {
                _lastPhysicsUniversalTime = universalTime;
                _physicsStepId++;
            }
        }

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
                        string[] layers = value.value.Split(',');
                        foreach (string layer in layers)
                        {
                            string trimmed = layer.Trim();
                            if (!string.IsNullOrEmpty(trimmed) && !layerToCategoryMap.ContainsKey(trimmed))
                            {
                                layerToCategoryMap.Add(trimmed, value.name);
                            }
                        }
                    }
                }

                ConfigNode excluded = node.GetNode("EXCLUDED_LAYERS");
                if (excluded != null)
                {
                    foreach (string val in excluded.GetValues("layer"))
                    {
                        excludedLayers.Add(val);
                    }
                }
            }
            configLoaded = true;
        }

        public static string GetCategoryByValue(string layerName)
        {
            LoadConfig();
            if (!string.IsNullOrEmpty(layerName) && layerToCategoryMap.TryGetValue(layerName, out string category))
            {
                return category;
            }
            return "Not Found!";
        }

        private static List<CloudsObject> GetCloudLayersForBody(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return emptyCloudLayerList;
            }

            List<CloudsObject> allLayers = CloudsManager.GetObjectList();
            if (allLayers == null)
            {
                return emptyCloudLayerList;
            }

            CachedBodyLayers cachedLayers;
            int currentFrame = Time.frameCount;
            GameScenes currentScene = HighLogic.LoadedScene;
            bool needsRebuild = !bodyLayerCache.TryGetValue(body, out cachedLayers)
                || cachedLayers.SourceCount != allLayers.Count
                || cachedLayers.Scene != currentScene
                || currentFrame - cachedLayers.BuiltFrame > BodyLayerCacheFrameLifetime;

            if (!needsRebuild)
            {
                return cachedLayers.Layers;
            }

            if (cachedLayers == null)
            {
                cachedLayers = new CachedBodyLayers();
                bodyLayerCache[body] = cachedLayers;
            }

            cachedLayers.Layers.Clear();
            foreach (CloudsObject layer in allLayers)
            {
                if (layer.Body == body && layer.LayerRaymarchedVolume != null && !excludedLayers.Contains(layer.Name))
                {
                    cachedLayers.Layers.Add(layer);
                }
            }

            cachedLayers.SourceCount = allLayers.Count;
            cachedLayers.BuiltFrame = currentFrame;
            cachedLayers.Scene = currentScene;
            return cachedLayers.Layers;
        }

        public static double VolumetricCloudTransmittance(CelestialBody sun, out string layerName)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            WeatherSample sample = SampleWeather(vessel, sun);
            layerName = sample.DominantLayerName;
            return sample.PowerFactor;
        }

        /// <summary>Legacy entry point; uses the active vessel.</summary>
        public static WeatherSample SampleWeather(CelestialBody sun)
        {
            return SampleWeather(FlightGlobals.ActiveVessel, sun);
        }

        public static WeatherSample SampleWeather(Vessel vessel, CelestialBody sun)
        {
            LoadConfig();
            if (sun == null || vessel == null)
            {
                return new WeatherSample();
            }

            string cacheKey = BuildWeatherCacheKey(vessel.id, sun.flightGlobalsIndex);
            int sampleInterval = GetWeatherSampleInterval();
            int rayMarchSteps = GetWeatherRayMarchSteps();

            WeatherCacheEntry entry;
            if (weatherSampleCache.TryGetValue(cacheKey, out entry) && entry.Sample != null)
            {
                if (sampleInterval > 1 && (_physicsStepId - entry.LastPhysicsStepId) < sampleInterval)
                {
                    return entry.Sample;
                }
            }

            WeatherSample sample = CalculateWeatherSample(vessel, sun, rayMarchSteps, entry != null ? entry.Sample : null);
            if (entry == null)
            {
                entry = new WeatherCacheEntry();
                weatherSampleCache[cacheKey] = entry;
            }

            entry.LastPhysicsStepId = _physicsStepId;
            entry.Sample = sample;
            return sample;
        }

        private static string BuildWeatherCacheKey(Guid vesselId, int sunIndex)
        {
            return vesselId.ToString("N") + ":" + sunIndex.ToString();
        }

        private static int GetWeatherSampleInterval()
        {
            return WeatherDrivenSolarPanel.WDSPGlobalConfig.WeatherSampleInterval;
        }

        private static int GetWeatherRayMarchSteps()
        {
            return WeatherDrivenSolarPanel.WDSPGlobalConfig.WeatherRayMarchSteps;
        }

        private static WeatherSample CalculateWeatherSample(Vessel vessel, CelestialBody sun, int maxStepCount, WeatherSample previousSample)
        {
            WeatherSample sample = new WeatherSample();

            if (vessel == null || sun == null || vessel.atmDensity <= 0)
            {
                return sample;
            }

            int stepCount = ResolveRayMarchSteps(maxStepCount, previousSample);
            float totalDensity = 0f;
            Vector3d toSun = sun.position - vessel.GetWorldPos3D();
            Vector3d lightDirection = toSun.normalized;

            string body = vessel.mainBody != null ? vessel.mainBody.bodyName : FlightGlobals.currentMainBody.bodyName;
            List<CloudsObject> layers = GetCloudLayersForBody(body);

            float dominantOpticalWeight = 0.0f;
            float dominantWeatherWeight = 0.0f;
            foreach (var layer in layers)
            {
#if DEBUG
				Debug.Log($"Layer Name is {layer.name}");
#endif
                var volume = layer.LayerRaymarchedVolume;
                if (volume == null)
                {
                    continue;
                }

                string category = GetCategoryByValue(layer.Name);
                Vector3 currentPosition = vessel.transform.position;
                Vector3d sphereCenter = volume.ParentTransform != null ? volume.ParentTransform.position : vessel.mainBody.transform.position;
                float innerSphereRadius = Mathf.Max(volume.PlanetRadius, volume.InnerSphereRadius);
                float outerSphereRadius = Mathf.Max(volume.PlanetRadius, volume.OuterSphereRadius);

                float innerIntersect = (float)IntersectSphere(currentPosition, lightDirection, sphereCenter, innerSphereRadius);
                float outerIntersect = (float)IntersectSphere(currentPosition, lightDirection, sphereCenter, outerSphereRadius);
                float startDistance = Mathf.Max(0, Mathf.Min(innerIntersect, outerIntersect));
                float endDistance = Mathf.Max(0, Mathf.Max(innerIntersect, outerIntersect));
                Vector3 startPos = currentPosition + (Vector3)lightDirection * startDistance;
                Vector3 endPos = currentPosition + (Vector3)lightDirection * endDistance;

                float stepSize = (endPos - startPos).magnitude / stepCount;
                if (float.IsNaN(stepSize) || float.IsInfinity(stepSize) || stepSize <= 0f)
                {
                    SampleLocalWeather(vessel, layer.Name, category, volume, ref sample, ref dominantWeatherWeight);
                    continue;
                }

                currentPosition = startPos;
                for (int x = 0; x < stepCount; x++)
                {
                    float coverageAtPosition = volume.SampleCoverage(currentPosition, out float cloudType, false);

                    if (coverageAtPosition > 0f)
                    {
                        float interpolatedDensity = GetInterpolatedDensity(volume, cloudType);
                        float opticalWeight = interpolatedDensity * coverageAtPosition * stepSize;
                        totalDensity += opticalWeight;

                        if (opticalWeight > dominantOpticalWeight)
                        {
                            dominantOpticalWeight = opticalWeight;
                            sample.DominantLayerName = layer.Name;
                            if (category != "Not Found!")
                            {
                                sample.Category = category;
                            }
                        }

                        if (totalDensity >= EarlyExitDensityThreshold)
                        {
                            break;
                        }
                    }

                    currentPosition += stepSize * (Vector3)lightDirection;
                }

                SampleLocalWeather(vessel, layer.Name, category, volume, ref sample, ref dominantWeatherWeight);
            }

            sample.SunTransmittance = Mathf.Clamp01((float)Math.Exp(-totalDensity));
            sample.PowerFactor = CalculatePowerFactor(sample.SunTransmittance, sample.Category);

            float opticalSeverity = Mathf.Clamp01(1.0f - (float)sample.PowerFactor);
            sample.Severity = Mathf.Max(sample.Severity, opticalSeverity);
            sample.HasWeather = sample.Severity > 0.05f || sample.PowerFactor < 0.98;

            if (sample.Category == CategoryCloudy)
            {
                sample.WearSeverity = 0.0f;
            }
            else if (sample.WearSeverity <= 0.0f)
            {
                sample.WearSeverity = sample.Severity;
            }

            return SmoothSample(vessel, sun, sample);
        }

        private static int ResolveRayMarchSteps(int maxStepCount, WeatherSample previousSample)
        {
            int steps = Math.Max(10, maxStepCount);
            if (previousSample != null && previousSample.PowerFactor >= 0.98 && !previousSample.HasWeather)
            {
                steps = Math.Max(15, steps / 2);
            }

            return steps;
        }

        private static void SampleLocalWeather(Vessel vessel, string layerName, string category, CloudsRaymarchedVolume volume, ref WeatherSample sample, ref float dominantWeatherWeight)
        {
            Vector3 center = volume.ParentTransform != null ? volume.ParentTransform.position : vessel.mainBody.transform.position;
            Vector3 radialDirection = (vessel.transform.position - center).normalized;
            float vesselRadius = (vessel.transform.position - center).magnitude;
            float startRadius = Mathf.Max(vesselRadius, volume.InnerSphereRadius);
            float endRadius = volume.OuterSphereRadius;

            if (startRadius > endRadius || radialDirection == Vector3.zero)
            {
                return;
            }

            const int localStepCount = 8;
            for (int i = 0; i < localStepCount; i++)
            {
                float radius = Mathf.Lerp(startRadius, endRadius, localStepCount == 1 ? 0.0f : (float)i / (localStepCount - 1));
                Vector3 samplePosition = center + radialDirection * radius;
                float coverage = volume.SampleCoverage(samplePosition, out float cloudType, false);
                if (coverage <= 0f)
                {
                    continue;
                }

                float density = GetInterpolatedDensity(volume, cloudType);
                float precipitation = Mathf.Max(volume.GetInterpolatedCloudTypeDropletsDensity(cloudType), volume.GetInterpolatedCloudTypeWetSurfacesDensity(cloudType));
                float lightning = volume.GetInterpolatedCloudTypeLightningFrequency(cloudType);
                float particle = volume.GetInterpolatedCloudTypeParticleFieldDensity(cloudType);

                string inferredCategory = InferCategory(category, precipitation, lightning);
                float weatherWeight = CalculateWeatherWeight(inferredCategory, coverage, density, precipitation, lightning, particle);
                if (weatherWeight <= 0f)
                {
                    continue;
                }

                sample.LocalCoverage = Mathf.Max(sample.LocalCoverage, coverage);
                sample.PrecipitationSeverity = Mathf.Max(sample.PrecipitationSeverity, coverage * Mathf.Clamp01(Mathf.Max(precipitation, lightning)));
                sample.DustSeverity = Mathf.Max(sample.DustSeverity, inferredCategory == CategoryDustStorm ? coverage * Mathf.Clamp01(Mathf.Max(particle, density)) : 0.0f);
                sample.LightningSeverity = Mathf.Max(sample.LightningSeverity, coverage * Mathf.Clamp01(lightning));

                if (weatherWeight > dominantWeatherWeight)
                {
                    dominantWeatherWeight = weatherWeight;
                    sample.DominantLayerName = layerName;
                    sample.Category = inferredCategory;
                    sample.Severity = Mathf.Clamp01(weatherWeight);
                    sample.WearSeverity = CalculateWearSeverity(inferredCategory, weatherWeight);
                }
            }
        }

        private static string InferCategory(string configuredCategory, float precipitation, float lightning)
        {
            if (configuredCategory == CategoryPrecipitation || configuredCategory == CategoryDustStorm || configuredCategory == CategoryVolcanoes || configuredCategory == CategoryCloudy)
            {
                return configuredCategory;
            }

            if (Mathf.Max(precipitation, lightning) > 0.05f)
            {
                return CategoryPrecipitation;
            }

            return CategoryCloudy;
        }

        private static float CalculateWeatherWeight(string category, float coverage, float density, float precipitation, float lightning, float particle)
        {
            float baseIntensity = Mathf.Clamp01(coverage * Mathf.Max(0.35f, density));

            switch (category)
            {
                case CategoryPrecipitation:
                    return Mathf.Clamp01(baseIntensity * Mathf.Max(1.0f, precipitation + lightning));
                case CategoryDustStorm:
                    return Mathf.Clamp01(baseIntensity * Mathf.Max(1.0f, particle));
                case CategoryVolcanoes:
                    return Mathf.Clamp01(baseIntensity * 1.25f);
                case CategoryCloudy:
                    return Mathf.Clamp01(baseIntensity * 0.75f);
                default:
                    return baseIntensity;
            }
        }

        private static float CalculateWearSeverity(string category, float weatherWeight)
        {
            switch (category)
            {
                case CategoryPrecipitation:
                    return Mathf.Clamp01(weatherWeight * 0.85f);
                case CategoryDustStorm:
                    return Mathf.Clamp01(weatherWeight);
                case CategoryVolcanoes:
                    return Mathf.Clamp01(weatherWeight * 1.15f);
                default:
                    return 0.0f;
            }
        }

        private static double CalculatePowerFactor(float sunTransmittance, string category)
        {
            if (sunTransmittance >= 0.999f)
            {
                return 1.0;
            }

            double factor = Math.Sqrt(Math.Max(0.0, sunTransmittance));
            switch (category)
            {
                case CategoryDustStorm:
                case CategoryVolcanoes:
                    factor = Math.Min(factor, 0.9);
                    break;
                case CategoryPrecipitation:
                    factor = Math.Min(factor, 0.95);
                    break;
            }

            return Mathf.Clamp01((float)factor);
        }

        private static WeatherSample SmoothSample(Vessel vessel, CelestialBody sun, WeatherSample sample)
        {
            string key = vessel.id.ToString() + ":" + sun.flightGlobalsIndex.ToString();
            double ut = Planetarium.GetUniversalTime();

            if (!smoothedStates.TryGetValue(key, out SmoothedWeatherState state))
            {
                state = new SmoothedWeatherState();
                smoothedStates.Add(key, state);
            }

            float alpha = 1.0f;
            if (state.LastUT >= 0.0)
            {
                float deltaTime = Mathf.Clamp((float)(ut - state.LastUT), 0.0f, 60.0f);
                alpha = 1.0f - Mathf.Exp(-deltaTime / 8.0f);
            }

            state.Severity = Mathf.Lerp(state.Severity, sample.Severity, alpha);
            state.WearSeverity = Mathf.Lerp(state.WearSeverity, sample.WearSeverity, alpha);
            state.PowerFactor = state.PowerFactor + (sample.PowerFactor - state.PowerFactor) * alpha;
            state.LastUT = ut;

            if (sample.Severity >= state.Severity * 0.85f || state.Category == CategorySunny)
            {
                state.Category = sample.Category;
                state.LayerName = sample.DominantLayerName;
            }

            sample.Severity = state.Severity;
            sample.WearSeverity = state.WearSeverity;
            sample.PowerFactor = Mathf.Clamp01((float)state.PowerFactor);
            sample.Category = state.Category;
            sample.DominantLayerName = state.LayerName ?? sample.DominantLayerName;
            sample.HasWeather = sample.Severity > 0.05f || sample.PowerFactor < 0.98;

            return sample;
        }

        private static float GetInterpolatedDensity(CloudsRaymarchedVolume volume, float cloudType)
        {
            if (volume.CloudTypes == null || volume.CloudTypes.Count == 0)
            {
                return 0.0f;
            }

            cloudType = Mathf.Clamp01(cloudType);
            cloudType *= volume.CloudTypes.Count - 1;
            int currentCloudType = Mathf.Clamp((int)cloudType, 0, volume.CloudTypes.Count - 1);
            int nextCloudType = Mathf.Min(currentCloudType + 1, volume.CloudTypes.Count - 1);
            float cloudFrac = cloudType - currentCloudType;

            return Mathf.Lerp(volume.CloudTypes[currentCloudType].Density, volume.CloudTypes[nextCloudType].Density, cloudFrac);
        }

        private static double IntersectSphere(Vector3d origin, Vector3d d, Vector3d sphereCenter, double r)
        {
            double a = Vector3d.Dot(d, d);
            double b = 2.0 * Vector3d.Dot(d, origin - sphereCenter);
            double c = Vector3d.Dot(sphereCenter, sphereCenter) + Vector3d.Dot(origin, origin) - 2.0 * Vector3d.Dot(sphereCenter, origin) - r * r;

            double test = b * b - 4.0 * a * c;

            if (test < 0)
            {
                return Mathf.Infinity;
            }

            double u = (-b - Math.Sqrt(test)) / (2.0 * a);

            u = (u < 0) ? (-b + Math.Sqrt(test)) / (2.0 * a) : u;

            return u;
        }
    }
}

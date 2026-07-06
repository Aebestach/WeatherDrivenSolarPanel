using System;
using System.Collections.Generic;
using UnityEngine;
using WDSP_GenericFunctionModule;

namespace WeatherDrivenSolarPanel
{
    /// <summary>Per-star vessel-level solar data independent of panel orientation.</summary>
    public struct StarSolarData
    {
        public weatherDrivenSolarPanel.WDSPStarInfo Star;
        public Vector3d SunDirection;
        public double StarFlux;
        public float AtmoAngleMult;
        public double WeatherPowerFactor;
        public GenericFunctionModule.WeatherSample WeatherSample;
        /// <summary>Flux × atmosphere × weather / normalization, before nominalRate and panel exposure.</summary>
        public double SharedFlowScale;

        public bool CanProduce;
    }

    /// <summary>Shared solar environment computed once per vessel per physics step.</summary>
    public sealed class VesselSolarContext
    {
        internal const double PanelFlowConstant1 = 24.3999996185303;
        internal const double PanelFlowConstant2 = 56.37091313591871;

        private static double _cacheUniversalTime = -1.0;
        private static readonly Dictionary<Guid, VesselSolarContext> _contexts = new Dictionary<Guid, VesselSolarContext>();

        public Vessel Vessel { get; private set; }
        public Vector3d Position { get; private set; }
        public double UniverseTime { get; private set; }
        public double TotalFlux { get; private set; }
        public bool InAtmosphere { get; private set; }
        public float TempMult { get; private set; }
        public float AtmoDensityMult { get; private set; }
        public List<CelestialBody> OccludingBodies { get; private set; }
        public List<weatherDrivenSolarPanel.WDSPStarInfo> VisibleStars { get; private set; }
        public StarSolarData[] StarData { get; private set; }
        public int StarCount { get; private set; }

        private readonly Dictionary<int, GenericFunctionModule.WeatherSample> _weatherBySunIndex =
            new Dictionary<int, GenericFunctionModule.WeatherSample>();

        public static VesselSolarContext GetOrCompute(Vessel vessel)
        {
            if (vessel == null)
            {
                return null;
            }

            weatherDrivenSolarPanel.InitCurves();
            WDSPGlobalConfig.EnsureLoaded();

            double universeTime = Planetarium.GetUniversalTime();
            GenericFunctionModule.NotifyPhysicsStep(universeTime);

            if (_cacheUniversalTime != universeTime)
            {
                _contexts.Clear();
                _cacheUniversalTime = universeTime;
            }

            Guid vesselId = vessel.id;
            if (!_contexts.TryGetValue(vesselId, out VesselSolarContext context))
            {
                context = new VesselSolarContext();
                context.Build(vessel, universeTime);
                _contexts[vesselId] = context;
            }

            return context;
        }

        private void Build(Vessel vessel, double universeTime)
        {
            Vessel = vessel;
            UniverseTime = universeTime;
            Position = weatherDrivenSolarPanel.VesselPosition(vessel);
            InAtmosphere = vessel.atmDensity > 0;

            OccludingBodies = new List<CelestialBody>();
            weatherDrivenSolarPanel.GetLargeBodiesNonAlloc(Position, OccludingBodies);

            VisibleStars = new List<weatherDrivenSolarPanel.WDSPStarInfo>();
            List<weatherDrivenSolarPanel.WDSPStarInfo> stars = weatherDrivenSolarPanel.WDSPStarInfo.GetStars();
            StarCount = stars.Count;
            StarData = new StarSolarData[StarCount];

            TempMult = 1f;
            AtmoDensityMult = 1f;
            if (InAtmosphere)
            {
                double gravAccelParameter = vessel.mainBody.gravParameter /
                    Math.Pow(vessel.mainBody.Radius + FlightGlobals.ship_altitude, 2);
                float massOfAirColumn = (float)(FlightGlobals.getStaticPressure() / gravAccelParameter);
                TempMult = weatherDrivenSolarPanel.EvaluateTemperatureEfficMult((float)vessel.atmosphericTemperature);
                AtmoDensityMult = weatherDrivenSolarPanel.EvaluateAtmoDensityMult(massOfAirColumn);
            }

            double starFluxAtHomeNorm = 0.0;
            if (PhysicsGlobals.SolarLuminosityAtHome != 0)
            {
                starFluxAtHomeNorm = 1360.0 / PhysicsGlobals.SolarLuminosityAtHome;
            }

            TotalFlux = 0.0;
            HashSet<int> appliedPhysicsStars = new HashSet<int>();

            for (int s = 0; s < StarCount; s++)
            {
                weatherDrivenSolarPanel.WDSPStarInfo star = stars[s];
                Vector3d direction;
                double distance;

                if (weatherDrivenSolarPanel.IsBodyVisible(vessel, Position, star.Sun, OccludingBodies, out direction, out distance))
                {
                    VisibleStars.Add(star);
                }

                if (!appliedPhysicsStars.Contains(star.Sun.flightGlobalsIndex))
                {
                    star.ApplyPhysics();
                    appliedPhysicsStars.Add(star.Sun.flightGlobalsIndex);
                }

                Vector3d sunDirection = (star.Sun.position - Position).normalized;
                float atmoAngleMult = 1f;
                if (InAtmosphere)
                {
                    float sunZenithAngleDeg = Vector3.Angle(FlightGlobals.upAxis, star.Sun.position);
                    atmoAngleMult = weatherDrivenSolarPanel.EvaluateAtmoAngleMult(sunZenithAngleDeg);
                }

                double starFlux = star.CalculateFluxAt(vessel) * starFluxAtHomeNorm;
                TotalFlux += starFlux;

                double weatherPowerFactor = 1.0;
                GenericFunctionModule.WeatherSample weatherSample = null;
                if (InAtmosphere)
                {
                    weatherSample = GenericFunctionModule.SampleWeather(vessel, star.Sun);
                    weatherPowerFactor = weatherSample.PowerFactor;
                    _weatherBySunIndex[star.Sun.flightGlobalsIndex] = weatherSample;
                }

                bool canProduce = starFluxAtHomeNorm > 0
                    && TempMult != 0f
                    && AtmoDensityMult != 0f
                    && atmoAngleMult != 0f;

                double sharedFlowScale = 0.0;
                if (canProduce)
                {
                    sharedFlowScale = starFlux * TempMult * AtmoDensityMult * atmoAngleMult * weatherPowerFactor
                        / starFluxAtHomeNorm / PanelFlowConstant1 / PanelFlowConstant2;
                }

                StarData[s] = new StarSolarData
                {
                    Star = star,
                    SunDirection = sunDirection,
                    StarFlux = starFlux,
                    AtmoAngleMult = atmoAngleMult,
                    WeatherPowerFactor = weatherPowerFactor,
                    WeatherSample = weatherSample,
                    SharedFlowScale = sharedFlowScale,
                    CanProduce = canProduce
                };
            }
        }

        public GenericFunctionModule.WeatherSample GetWeatherSample(CelestialBody sun)
        {
            if (sun == null || !InAtmosphere)
            {
                return new GenericFunctionModule.WeatherSample();
            }

            GenericFunctionModule.WeatherSample sample;
            if (_weatherBySunIndex.TryGetValue(sun.flightGlobalsIndex, out sample))
            {
                return sample;
            }

            sample = GenericFunctionModule.SampleWeather(Vessel, sun);
            _weatherBySunIndex[sun.flightGlobalsIndex] = sample;
            return sample;
        }

        public weatherDrivenSolarPanel.WDSPStarInfo GetBrightestVisibleStar()
        {
            return weatherDrivenSolarPanel.WDSPStarInfo.GetBrightest(Position, VisibleStars);
        }

        public weatherDrivenSolarPanel.WDSPStarInfo GetClosestStar(List<weatherDrivenSolarPanel.WDSPStarInfo> stars)
        {
            weatherDrivenSolarPanel.WDSPStarInfo closestStar = null;
            double closestDistSqr = double.MaxValue;

            for (int i = 0; i < stars.Count; i++)
            {
                double d2 = Vector3d.SqrMagnitude(Vessel.transform.position - stars[i].Sun.position);
                if (d2 < closestDistSqr)
                {
                    closestDistSqr = d2;
                    closestStar = stars[i];
                }
            }

            return closestStar;
        }
    }
}

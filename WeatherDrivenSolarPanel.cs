using UnityEngine;
using Atmosphere;
using System;
using Kopernicus.Components;
using KSP.Localization;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace weatherDrivenSolarPanel
{
    public class weatherDrivenSolarPanel : ModuleDeployableSolarPanel
    {
        //Strings for Localization
        private static readonly string SP_status_DirectSunlight = Localizer.Format("#Kopernicus_UI_DirectSunlight");  // "Direct Sunlight"
        private static readonly string button_Auto = Localizer.Format("#Kopernicus_UI_AutoTracking");                 // "Auto"
        private static readonly string SelectBody = Localizer.Format("#Kopernicus_UI_SelectBody");                    // "Select Tracking Body"
        private static readonly string SelectBody_Msg = Localizer.Format("#Kopernicus_UI_SelectBody_Msg");            // "Please select the Body you want to track with this Solar Panel."
        private static readonly string WDSP_TVC_dustAffect = Localizer.Format("#WDSP_TVC_dustAffect");                // "Affected by sandstorm"
        private static readonly string WDSP_TVC_rainAffect = Localizer.Format("#WDSP_TVC_rainAffect");                // "Affected by rain"
        private static readonly string WDSP_TVC_snowAffect = Localizer.Format("#WDSP_TVC_snowAffect");                // "Affected by snow"
        private static readonly string WDSP_TVC_volcanoesAffect = Localizer.Format("#WDSP_TVC_volcanoesAffect");      // "Affected by volcanoes"


        //panel power cached value
        private double _cachedFlowRate = 0;
        private float cachedFlowRate = 0;

        //timer value
        private int frameTimer = 0;

        //Change the value of status of the solar panel.
        private float statusChangeValue = 1;
        string layerName;

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = "#Kopernicus_UI_TrackingBody", isPersistant = true)]
        [SuppressMessage("ReSharper", "NotAccessedField.Global")]
        public String trackingBodyName;

        [KSPField(isPersistant = true)]
        private Boolean _manualTracking;

        //declare internal float curves
        private static readonly FloatCurve AtmosphericAttenutationAirMassMultiplier = new FloatCurve();
        private static readonly FloatCurve AtmosphericAttenutationSolarAngleMultiplier = new FloatCurve();

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            //The single star model is updated more frequently.
            //single-star mode=10;
            //multi-star mode=50;
            int flagFactor = 10;
            if (KopernicusStar.UseMultiStarLogic)
            {
                flagFactor = 50;
            }

            //Calculations copied from Kopernicus solving for the energy output of solar panels for single, or multiple stars,
            //while including impact factors for True volumetric clouds.
            frameTimer++;
            if (HighLogic.LoadedSceneIsFlight)
            {
                if ((deployState == ModuleDeployablePart.DeployState.EXTENDED))
                {
                    if (frameTimer >
                        (flagFactor * Kopernicus.RuntimeUtility.RuntimeUtility.KopernicusConfig.SolarRefreshRate))
                    {
                        statusChangeValue = 1;
                        CelestialBody trackingStar = trackingBody;
                        frameTimer = 0;
                        KopernicusStar bestStar = KopernicusStar.CelestialBodies[trackingStar];
                        Double totalFlux = 0;
                        Double totalFlow = 0;
                        flowRate = 0;
                        _flowRate = 0;
                        Double bestFlux = 0;
                        for (Int32 s = 0; s < KopernicusStar.Stars.Count; s++)
                        {
                            KopernicusStar star = KopernicusStar.Stars[s];
                            // Use this star
                            star.shifter.ApplyPhysics();

                            // Set Tracking Speed to zero
                            Single oldTrackingSpeed = trackingSpeed;
                            trackingSpeed = 0;

                            // Change the tracking body
                            trackingBody = star.sun;
                            GetTrackingBodyTransforms();
                            CalculateTracking();

                            //Calculate flux
                            double starFluxAtHome = 0;
                            if (PhysicsGlobals.SolarLuminosityAtHome != 0)
                            {
                                starFluxAtHome = 1360 / PhysicsGlobals.SolarLuminosityAtHome;
                            }

                            double starFlux;
                            starFlux = star.CalculateFluxAt(vessel) * starFluxAtHome;

                            //Check if star has better flux
                            if (bestFlux < starFlux)
                            {
                                bestFlux = starFlux;
                                bestStar = star;
                            }

                            // Add to TotalFlux and EC tally
                            totalFlux += starFlux;
                            float panelEffectivness = 0;
                            //Now for some fancy atmospheric math
                            float atmoDensityMult = 1;
                            float atmoAngleMult = 1;
                            float tempMult = 1;
                            float CheckWeatherValue = 1f;
                            if (this.vessel.atmDensity > 0)
                            {
                                float sunZenithAngleDeg = Vector3.Angle(FlightGlobals.upAxis, star.sun.position);
                                Double gravAccelParameter = (vessel.mainBody.gravParameter / Math.Pow(vessel.mainBody.Radius + FlightGlobals.ship_altitude, 2));
                                float massOfAirColumn = (float)(FlightGlobals.getStaticPressure() / gravAccelParameter);
                                tempMult = this.temperatureEfficCurve.Evaluate((float)this.vessel.atmosphericTemperature);
                                atmoDensityMult = AtmosphericAttenutationAirMassMultiplier.Evaluate(massOfAirColumn);
                                atmoAngleMult = AtmosphericAttenutationSolarAngleMultiplier.Evaluate(sunZenithAngleDeg);

                                //Return value a scale factor
                                CheckWeatherValue = CheckWeather();
                            }

                            if ((sunAOA != 0) && (tempMult != 0) && (atmoAngleMult != 0) && (atmoDensityMult != 0))
                            {
                                panelEffectivness = (chargeRate / 24.4f) / 56.37091313591871f * sunAOA * tempMult *
                                                    atmoAngleMult *
                                                    atmoDensityMult; //56.blabla is a weird constant we use to turn flux into EC
                            }

                            if (starFluxAtHome > 0)
                            {
                                totalFlow += (starFlux * panelEffectivness) /
                                             (1360 / PhysicsGlobals.SolarLuminosityAtHome);
                            }

                            totalFlow *= CheckWeatherValue;
                            statusChangeValue = CheckWeatherValue;

                            // Restore Tracking Speed
                            trackingSpeed = oldTrackingSpeed;
                        }
                        // Restore the starting star
                        trackingBody = trackingStar;
                        KopernicusStar.CelestialBodies[trackingStar].shifter.ApplyPhysics();
                        GetTrackingBodyTransforms();
                        CalculateTracking();
                        vessel.solarFlux = totalFlux;
                        //Add to new output
                        flowRate = (float)totalFlow;
                        _flowRate = totalFlow / chargeRate;
                        resHandler.UpdateModuleResourceOutputs(_flowRate);
                        //caching logic
                        cachedFlowRate = flowRate;
                        _cachedFlowRate = _flowRate;
                        // Setup next tracking body
                        if ((bestStar != null && bestStar != trackingBody) && (!_manualTracking))
                        {
                            trackingBody = bestStar.sun;
                            GetTrackingBodyTransforms();
                            CalculateTracking();
                        }
                    }
                    else
                    {
                        //inbetween timings logic
                        flowRate = cachedFlowRate;
                        _flowRate = _cachedFlowRate;
                        resHandler.UpdateModuleResourceOutputs(_flowRate);
                    }

                    //see if tracked star is blocked or not
                    if (sunAOA > 0)
                    {
                        //this ensures the "blocked" GUI option is set right, if we're exposed to you we're not blocked
                        vessel.directSunlight = true;
                    }

                    // Restore The Current Star
                    KopernicusStar.Current.shifter.ApplyPhysics();
                }
            }
            else
            {
                //Packed logic
                flowRate = cachedFlowRate;
                _flowRate = _cachedFlowRate;
                resHandler.UpdateModuleResourceOutputs(_flowRate);
            }
        }
        public override void PostCalculateTracking(bool trackingLOS, Vector3 trackingDirection)
        {
            sunAOA = 0f;
            Vector3 trackDir = (trackingBody.transform.position - panelRotationTransform.position).normalized;
            if (!trackingLOS)
            {
                sunAOA = 0f;
                status = Localizer.Format("#Kopernicus_UI_PanelBlocked", blockingObject);
                return;
            }

            status = SP_status_DirectSunlight;
            if (panelType == PanelType.FLAT)
            {
                sunAOA = Mathf.Clamp(Vector3.Dot(trackingDotTransform.forward, trackDir), 0f, 1f);
            }
            else if (panelType != PanelType.CYLINDRICAL)
            {
                sunAOA = 0.25f;
            }
            else
            {
                Vector3 direction;
                if (alignType == PanelAlignType.PIVOT)
                {
                    direction = trackingDotTransform.forward;
                }
                else if (alignType != PanelAlignType.X)
                {
                    direction = alignType != PanelAlignType.Y ? part.partTransform.forward : part.partTransform.up;
                }
                else
                {
                    direction = part.partTransform.right;
                }

                sunAOA = (1f - Mathf.Abs(Vector3.Dot(direction, trackDir))) * 0.318309873f;
            }
        }

        void EarlyLateUpdate()
        {
            if (KopernicusStar.UseMultiStarLogic)
            {
                if (deployState == ModuleDeployablePart.DeployState.EXTENDED)
                {
                    // Update the name
                    trackingBodyName = trackingBody.bodyDisplayName.Replace("^N", "");

                    if (!_manualTracking)
                    {
                        trackingBodyName = Localizer.Format("#Kopernicus_UI_AutoTrackingBodyName", trackingBodyName);
                    }
                }
            }
        }

        [KSPEvent(active = true, guiActive = false, guiName = "#Kopernicus_UI_SelectBody")]
        public void ManualTracking()
        {
            if (KopernicusStar.UseMultiStarLogic)
            {
                KopernicusStar[] orderedStars = KopernicusStar.Stars
                    .OrderBy(s => Vector3.Distance(vessel.transform.position, s.sun.position)).ToArray();
                Int32 stars = orderedStars.Count();
                DialogGUIBase[] options = new DialogGUIBase[stars + 1];
                // Assemble the buttons
                options[0] = new DialogGUIButton(button_Auto, () => { _manualTracking = false; }, true); //Auto
                for (Int32 i = 0; i < stars; i++)
                {
                    CelestialBody body = orderedStars[i].sun;
                    options[i + 1] = new DialogGUIButton
                    (
                        body.bodyDisplayName.Replace("^N", ""),
                        () => SetTrackingBody(body),
                        true
                    );
                }

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new MultiOptionDialog(
                    "SelectTrackingBody",
                    SelectBody_Msg, //Please select the Body you want to track with this Solar Panel.
                    SelectBody, //Select Tracking Body
                    UISkinManager.GetSkin("MainMenuSkin"),
                    options), false, UISkinManager.GetSkin("MainMenuSkin"));
            }
        }

        public void SetTrackingBody(CelestialBody sun)
        {
            _manualTracking = true;
            trackingBody = sun;
            GetTrackingBodyTransforms();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            //Setup Floatcurves
            AtmosphericAttenutationAirMassMultiplier.Add(0f, 1f, 0f, 0f);
            AtmosphericAttenutationAirMassMultiplier.Add(5f, 0.982f, -0.010f, -0.010f);
            AtmosphericAttenutationAirMassMultiplier.Add(10f, 0.891f, -0.032f, -0.032f);
            AtmosphericAttenutationAirMassMultiplier.Add(15f, 0.746f, -0.025f, -0.025f);
            AtmosphericAttenutationAirMassMultiplier.Add(20f, 0.657f, -0.014f, -0.014f);
            AtmosphericAttenutationAirMassMultiplier.Add(30f, 0.550f, -0.0081f, -0.0081f);
            AtmosphericAttenutationAirMassMultiplier.Add(40f, 0.484f, -0.0053f, -0.0053f);
            AtmosphericAttenutationAirMassMultiplier.Add(50f, 0.439f, -0.0039f, -0.0039f);
            AtmosphericAttenutationAirMassMultiplier.Add(60f, 0.405f, -0.0030f, -0.0030f);
            AtmosphericAttenutationAirMassMultiplier.Add(80f, 0.357f, -0.0020f, -0.0020f);
            AtmosphericAttenutationAirMassMultiplier.Add(100f, 0.324f, -0.0014f, -0.0014f);
            AtmosphericAttenutationAirMassMultiplier.Add(150f, 0.271f, -0.00079f, -0.00079f);
            AtmosphericAttenutationAirMassMultiplier.Add(200f, 0.239f, -0.00052f, -0.00052f);
            AtmosphericAttenutationAirMassMultiplier.Add(300f, 0.200f, -0.00029f, -0.00029f);
            AtmosphericAttenutationAirMassMultiplier.Add(500f, 0.159f, -0.00014f, -0.00014f);
            AtmosphericAttenutationAirMassMultiplier.Add(800f, 0.130f, -0.00007f, -0.00007f);
            AtmosphericAttenutationAirMassMultiplier.Add(1200f, 0.108f, -0.00004f, 0f);
            AtmosphericAttenutationSolarAngleMultiplier.Add(0f, 1f, 0f, 0f);
            AtmosphericAttenutationSolarAngleMultiplier.Add(15f, 0.985f, -0.0020f, -0.0020f);
            AtmosphericAttenutationSolarAngleMultiplier.Add(30f, 0.940f, -0.0041f, -0.0041f);
            AtmosphericAttenutationSolarAngleMultiplier.Add(45f, 0.862f, -0.0064f, -0.0064f);
            AtmosphericAttenutationSolarAngleMultiplier.Add(60f, 0.746f, -0.0092f, -0.0092f);
            AtmosphericAttenutationSolarAngleMultiplier.Add(75f, 0.579f, -0.0134f, -0.0134f);
            AtmosphericAttenutationSolarAngleMultiplier.Add(90f, 0.336f, -0.0185f, -0.0185f);
            AtmosphericAttenutationSolarAngleMultiplier.Add(105f, 0.100f, -0.008f, -0.008f);
            AtmosphericAttenutationSolarAngleMultiplier.Add(120f, 0.050f, 0f, 0f);
            if (KopernicusStar.UseMultiStarLogic)
            {
                if (HighLogic.LoadedSceneIsFlight)
                {
                    TimingManager.LateUpdateAdd(TimingManager.TimingStage.Early, EarlyLateUpdate);

                    Fields["trackingBodyName"].guiActive = true;
                    Events["ManualTracking"].guiActive = true;

                    if (_manualTracking)
                    {
                        CelestialBody trackingBody = GetTrackingBodyFromName(trackingBodyName);

                        if (trackingBody != null)
                        {
                            SetTrackingBody(trackingBody);
                        }
                        else
                        {
                            _manualTracking = false;
                        }
                    }
                }
            }
        }

        public new void OnDestroy()
        {
            if (KopernicusStar.UseMultiStarLogic)
            {
                TimingManager.LateUpdateRemove(TimingManager.TimingStage.Early, EarlyLateUpdate);
            }
        }

        private CelestialBody GetTrackingBodyFromName(string name)
        {
            for (Int32 s = 0; s < FlightGlobals.Bodies.Count; s++)
            {
                CelestialBody body = FlightGlobals.Bodies[s];
                if (body.bodyDisplayName.Replace("^N", "") == name)
                {
                    return body;
                }
            }
            return null;
        }

        public override void CalculateTracking()
        {
            base.CalculateTracking();
            if ((layerName == "Duna-duststorm-big") && (statusChangeValue >= 0 && statusChangeValue <= 0.44))
            {
                this.status = WDSP_TVC_dustAffect;
            }

            if ((layerName == "Kerbin-Weather1") && (statusChangeValue >= 0 && statusChangeValue <= 0.52))
            {
                this.status = WDSP_TVC_rainAffect;
            }

            if ((layerName == "Laythe-Weather1") && (statusChangeValue >= 0.1))
            {
                this.status = WDSP_TVC_snowAffect;
            }

            if (layerName == "Laythe-HighAlt-Volcanoes")
            {
                this.status = WDSP_TVC_volcanoesAffect;
            }
        }

        public float CheckWeather()
        {
            float densitie;
            float reFactor;
            var layers = CloudsManager.GetObjectList();
            foreach (var layer in layers)
            {
                reFactor = 1f;
                //Duna storm
                if (layer.Name == "Duna-duststorm-big")
                {
                    layerName = layer.Name;
                    densitie = layer.LayerRaymarchedVolume.SampleCoverage(FlightGlobals.ActiveVessel.transform.position, out float CloudType, false);
                    if (densitie > 0.35f)
                    {
                        //Scope limited to (-0.5,0.475)
                        reFactor = 1f - densitie * 1.5f;
                        if (reFactor < 0f)
                        {
                            return 0f;
                        }
                    }
                    else
                    {
                        float den = densitie / 0.35f;
                        //Scope limited to (0.5,1)
                        reFactor = 1f - (0.5f * den);
                    }
                    return reFactor;
                }

                //kerbin rain and snow
                if (layer.Name == "Kerbin-Weather1")
                {
                    layerName = layer.Name;
                    densitie = layer.LayerRaymarchedVolume.SampleCoverage(FlightGlobals.ActiveVessel.transform.position, out float CloudType, false);
                    if (densitie > 0.2f)
                    {
                        //Scope limited to (-1.6,0.52)
                        reFactor = 1f - densitie * 2.6f;
                        if (reFactor < 0f)
                        {
                            return 0f;
                        }
                    }
                    else
                    {
                        float den = densitie / 0.2f;
                        //Scope limited to (0.6,1)
                        reFactor = 1f - (0.4f * den);
                    }
                    return reFactor;
                }

                //Laythe snow
                if (layer.Name == "Laythe-Weather1")
                {
                    layerName = layer.Name;
                    densitie = layer.LayerRaymarchedVolume.SampleCoverage(FlightGlobals.ActiveVessel.transform.position, out float CloudType, false);
                    if (densitie > 0.1f)
                    {
                        //Scope limited to (0.1,1)
                        reFactor = 1f - densitie * 0.9f;
                    }
                    return reFactor;
                }

                if (layer.Name == "Laythe-HighAlt-Volcanoes")
                {
                    layerName = layer.Name;
                    densitie = layer.LayerRaymarchedVolume.SampleCoverage(FlightGlobals.ActiveVessel.transform.position, out float CloudType, false);
                    if (densitie >= 0.1f)
                    {
                        reFactor = 0;
                    }
                    return reFactor;
                }
            }
            return 1f;
        }
    }
}





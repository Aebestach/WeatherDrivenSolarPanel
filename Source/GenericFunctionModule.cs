using Atmosphere;
using System;
using UnityEngine;
using Utils;

namespace WDSP_GenericFunctionModule
{
    public class GenericFunctionModule
    {
        /*public static float CheckWeather(out string NlayerName)
        {
            float densitie;
            float reFactor;
            NlayerName = null;
            var layers = CloudsManager.GetObjectList();
            foreach (var layer in layers)
            {
                reFactor = 1f;
                //Duna storm
                if (layer.Name == "Duna-duststorm-big")
                {
                    NlayerName = layer.Name;
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
                if (layer.Name == "Kerbin-Weather2" || layer.Name == "Kerbin-Weather1")
                {
                    NlayerName = layer.Name;
                    densitie = layer.LayerRaymarchedVolume.SampleCoverage(FlightGlobals.ActiveVessel.transform.position, out float CloudType, false);
                    if (densitie > 0.3f)
                    {
                        //Scope limited to (-1.6,0.22)
                        reFactor = 1f - densitie * 2.6f;
                        if (reFactor < 0f)
                        {
                            return 0f;
                        }
                    }
                    else
                    {
                        float den = densitie / 0.3f;
                        //Scope limited to (0.6,1)
                        reFactor = 1f - (0.4f * den);
                    }
                    return reFactor;
                }

                //Laythe snow
                if (layer.Name == "Laythe-Weather1")
                {
                    NlayerName = layer.Name;
                    densitie = layer.LayerRaymarchedVolume.SampleCoverage(FlightGlobals.ActiveVessel.transform.position, out float CloudType, false);
                    if (densitie > 0.1f)
                    {
                        //Scope limited to (0.1,1)
                        reFactor = 1f - densitie * 0.9f;
                    }
                    return reFactor;
                }

                ////Laythe volcanoes
                if (layer.Name == "Laythe-HighAlt-Volcanoes")
                {
                    NlayerName = layer.Name;
                    densitie = layer.LayerRaymarchedVolume.SampleCoverage(FlightGlobals.ActiveVessel.transform.position, out float CloudType, false);
                    if (densitie >= 0.1f)
                    {
                        reFactor = 0;
                    }
                    return reFactor;
                }

                //Mars storm
                if (layer.Name == "Storms-Dust" || layer.Name == "Stable-Dust")
                {
                    NlayerName = layer.Name;
                    densitie = layer.LayerRaymarchedVolume.SampleCoverage(FlightGlobals.ActiveVessel.transform.position, out float CloudType, false);
                    if (densitie > 0.3f)
                    {
                        //Scope limited to (-0.2,0.64)
                        reFactor = 1f - densitie * 1.2f;
                        if (reFactor < 0f)
                        {
                            return 0f;
                        }
                    }
                    return reFactor;
                }

            }
            return 1f;
        }*/

        public static float VolumetricCloudTransmittance(CelestialBody sun, out string layerName)
        {
            layerName = null;
            //int stepCount = 500;
            int stepCount = 100;
            float totalDensity = 0f;
            bool RSSflag;

            Transform scaledTransform;
            Material cloudMaterial;


            Vector3d lightDirection;
            Vessel vessel = FlightGlobals.ActiveVessel;
            Vector3d toSun = sun.position - vessel.GetWorldPos3D();
            lightDirection = toSun.normalized;

            var layers = CloudsManager.GetObjectList();

            string body = FlightGlobals.currentMainBody.bodyName;
            scaledTransform = Tools.GetScaledTransform(body);

            foreach (var layer in layers)
            {
                if (layer.Name != "Kerbin-Snow-Particles-1" && layer.Name != "Kerbin-Snow-Particles-2" && layer.Name != "CloudTops"
                    && layer.Name != "EarthAurora1" && layer.Name != "EarthAurora2" && layer.Name != "EarthAurora3"
                    && layer.Name != "PolarHood" && layer.Name != "TropicalCumulus" && layer.Name != "TholinHaze" && layer.Name != "MethaneDrizzle")
                {
                    cloudMaterial = layer.LayerRaymarchedVolume.RaymarchedCloudMaterial;

                    Vector3 currentPosition = FlightGlobals.ActiveVessel.transform.position;
                    Vector3d sphereCenter = ScaledSpace.ScaledToLocalSpace(scaledTransform.position);
                    var planetRadius = cloudMaterial.GetFloat("planetRadius");
                    float innerSphereRadius = Mathf.Max(planetRadius, cloudMaterial.GetFloat("innerSphereRadius"));
                    float outerSphereRadius = Mathf.Max(planetRadius, cloudMaterial.GetFloat("outerSphereRadius"));

                    var innerIntersect = (float)IntersectSphere(currentPosition, lightDirection, sphereCenter, innerSphereRadius);
                    var outerIntersect = (float)IntersectSphere(currentPosition, lightDirection, sphereCenter, outerSphereRadius);
                    var startDistance = Mathf.Min(innerIntersect, outerIntersect);
                    var endDistance = Mathf.Max(innerIntersect, outerIntersect);
                    Vector3 startPos = currentPosition + lightDirection * startDistance;
                    Vector3 endPos = currentPosition + lightDirection * endDistance;

                    float stepSize = (endPos - startPos).magnitude / stepCount;

                    for (int x = 0; x < stepCount; x++)
                    {
                        float coverageAtposition = layer.LayerRaymarchedVolume.SampleCoverage(currentPosition, out float CloudType, false);

                        if (coverageAtposition > 0f)
                        {
                            layerName = layer.Name;
                            CloudType *= layer.LayerRaymarchedVolume.CloudTypes.Count - 1;
                            int currentCloudType = (int)CloudType;
                            int nextCloudType = Math.Min(currentCloudType + 1, layer.LayerRaymarchedVolume.CloudTypes.Count - 1);

                            var cloudTypes = layer.LayerRaymarchedVolume.CloudTypes;
                            float interpolatedDensity = Mathf.Lerp(cloudTypes[currentCloudType].Density, cloudTypes[nextCloudType].Density, CloudType);

                            totalDensity += interpolatedDensity * coverageAtposition * stepSize;
                        }

                        currentPosition += stepSize * lightDirection;
                    }
                }
            }

            float lightTransmittance = (float)Math.Exp(-totalDensity);

            float middleValue = 1f;
            switch (layerName)
            {
                case "TemperateCumulus":
                case "TemperateAltoStratus":
                case "Cirrus":
                case "TemperateWeather":
                case "Storms-Dust":
                case "Stable-Dust":
                    RSSflag = true;
                    break;
                default:
                    RSSflag = false;
                    break;
            }

            if (RSSflag == true)
            {
                //Use of RSS versions
                if (lightTransmittance == 1f)
                {
                    middleValue = 1f;
                }
                else if (layerName == "Storms-Dust" || layerName == "Stable-Dust")
                {
                    middleValue = Mathf.Sqrt(Mathf.Sqrt(lightTransmittance));
                    middleValue = Mathf.Clamp(middleValue, 0.4f, 0.75f);
                }
                else
                {
                    middleValue = middleValue * 0.8f;
                }
            }
            else
            {
                //Use of stock versions
                if (lightTransmittance == 1f)
                {
                    middleValue = 1f;
                }
                else if (lightTransmittance <= 0.0001f)
                {
                    middleValue = 0f;
                }
                else if (lightTransmittance > 0.001f && lightTransmittance <= 0.3f)
                {
                    //Scope limited to (0.1,0.53)
                    middleValue = 0.55f * lightTransmittance / 0.31f;
                    middleValue = Mathf.Clamp(middleValue, 0.1f, 0.53f);
                }
                else if (lightTransmittance > 0.3f && lightTransmittance < 1f)
                {
                    middleValue = Mathf.Max(0.55f, Mathf.Sqrt(lightTransmittance));
                }
            }

            return middleValue;
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

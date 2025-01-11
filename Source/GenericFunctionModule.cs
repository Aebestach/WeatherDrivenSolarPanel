using Atmosphere;
using System;
using System.Linq;
using UnityEngine;
using Utils;

namespace WDSP_GenericFunctionModule
{
    public class GenericFunctionModule
    {
        public static double VolumetricCloudTransmittance(CelestialBody sun, out string layerName)
        {
            layerName = null;
            //int stepCount = 500;
            int stepCount = 50;
            float totalDensity = 0f;
            bool RSSflag;

            Transform scaledTransform;
            Material cloudMaterial;


            Vector3d lightDirection;
            Vessel vessel = FlightGlobals.ActiveVessel;
            Vector3d toSun = sun.position - vessel.GetWorldPos3D();
            lightDirection = toSun.normalized;

            string body = FlightGlobals.currentMainBody.bodyName;
            var layers = CloudsManager.GetObjectList().Where(x => x.Body == body && x.LayerRaymarchedVolume != null);
            scaledTransform = Tools.GetScaledTransform(body);

            foreach (var layer in layers)
            {
                if (layer.Name != "Kerbin-Snow-Particles-1" && layer.Name != "Kerbin-Snow-Particles-2" && layer.Name != "CloudTops"
                    && layer.Name != "EarthAurora1" && layer.Name != "EarthAurora2" && layer.Name != "EarthAurora3"
                    && layer.Name != "PolarHood" && layer.Name != "TropicalCumulus" && layer.Name != "TholinHaze"
                    && layer.Name != "MethaneDrizzle" && layer.Name != "Duna-dust-scattered" && layer.Name != "Earth-Snow-Particles-1"
                    && layer.Name != "Earth-Snow-Particles-2" && layer.Name != "Venus-fog-scattered" && layer.Name != "Venus-highfog-scattered"
                    && layer.Name != "Titan-fog-scattered" && layer.Name != "Eve-Fog" && layer.Name != "Eve-Weather-Light"
                    && layer.Name != "Titan-fog-scattered" && layer.Name != "PolarHoodNorth" && layer.Name != "PolarHoodSouth"
                    && layer.Name != "Huygen-Fog" && layer.Name != "Laythe-Fog")
                {
                    cloudMaterial = layer.LayerRaymarchedVolume.RaymarchedCloudMaterial;

                    Vector3 currentPosition = FlightGlobals.ActiveVessel.transform.position;
                    Vector3d sphereCenter = ScaledSpace.ScaledToLocalSpace(scaledTransform.position);
                    var planetRadius = cloudMaterial.GetFloat("planetRadius");
                    float innerSphereRadius = Mathf.Max(planetRadius, cloudMaterial.GetFloat("innerSphereRadius"));
                    float outerSphereRadius = Mathf.Max(planetRadius, cloudMaterial.GetFloat("outerSphereRadius"));

                    var innerIntersect = (float)IntersectSphere(currentPosition, lightDirection, sphereCenter, innerSphereRadius);
                    var outerIntersect = (float)IntersectSphere(currentPosition, lightDirection, sphereCenter, outerSphereRadius);
                    var startDistance = Mathf.Max(0, Mathf.Min(innerIntersect, outerIntersect));
                    var endDistance = Mathf.Max(0, Mathf.Max(innerIntersect, outerIntersect));
                    Vector3 startPos = currentPosition + lightDirection * startDistance;
                    Vector3 endPos = currentPosition + lightDirection * endDistance;

                    float stepSize = (endPos - startPos).magnitude / stepCount;
                    currentPosition = startPos;
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

            double middleValue = 1.0;
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
                    middleValue = Mathf.Clamp((float)middleValue, 0.4f, 0.75f);
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
                else if (lightTransmittance > 0.001f && lightTransmittance <= 0.4f)
                {
                    //Scope limited to (0.1,0.53)
                    middleValue = 0.55f * lightTransmittance / 0.41f;
                    middleValue = Mathf.Clamp((float)middleValue, 0.1f, 0.53f);
                }
                else if (lightTransmittance > 0.4f && lightTransmittance < 1f)
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
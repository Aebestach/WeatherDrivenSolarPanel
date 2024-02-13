using Atmosphere;
using UnityEngine;
using System;

namespace EVETestExample
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class EVETestExample : MonoBehaviour
    {
        public void Update()
        {
            int stepCount = 25;
            float totalDensity = 0f;


            Transform scaledTransform;
            Material cloudMaterial;


            Vector3d lightDirection;
            Vessel vessel = FlightGlobals.ActiveVessel;
            CelestialBody sun = FlightGlobals.Bodies.Find(b => b.name == "Sun");
            Vector3d toSun = sun.position - vessel.GetWorldPos3D();
            Vector3d localToSun = vessel.transform.InverseTransformDirection(toSun);
            lightDirection = localToSun.normalized;


            var layers = CloudsManager.GetObjectList();

            string body = FlightGlobals.currentMainBody.bodyName;
            scaledTransform = Utils.Tools.GetScaledTransform(body);

            foreach (var layer in layers)
            {
                if (layer.Name == "Kerbin-Weather1" || layer.Name == "Kerbin-Weather2")
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
            print("lightTransmittance" + lightTransmittance);
        }

        private double IntersectSphere(Vector3d origin, Vector3d d, Vector3d sphereCenter, double r)
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
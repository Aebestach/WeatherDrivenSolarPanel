using System;
using KSP.Localization;
using UnityEngine;

namespace WeatherDrivenSolarPanel
{
    /// <summary>
    /// EVA engineer (level 3+) dust cleaning rules shared by stock and Kerbalism modules.
    /// </summary>
    internal static class WDSPDustCleaning
    {
        internal const int MinEngineerLevel = 3;
        /// <summary>Wear fraction (0 = new, 1 = failed). Cleaning allowed only below this.</summary>
        internal const float MaxWearFraction = 0.8f;
        internal const float CleanUnfocusedRange = 10f;

        internal static float EvaluateWeatherWearFactor(double precipitationWearSeconds, double dustWearSeconds)
        {
            return WDSPWearCurves.EvaluateWeatherEfficiency(
                Math.Max(0.0, precipitationWearSeconds) + Math.Max(0.0, dustWearSeconds));
        }

        internal static bool IsEngineerAtLeast(ProtoCrewMember crew, int level)
        {
            if (crew == null || crew.experienceLevel < level || crew.experienceTrait == null)
            {
                return false;
            }

            if (string.Equals(crew.experienceTrait.TypeName, "Engineer", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Some installs expose the trait via Config.Name instead of TypeName.
            return crew.experienceTrait.Config != null
                && string.Equals(crew.experienceTrait.Config.Name, "Engineer", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryGetActiveEvaEngineer(out ProtoCrewMember engineer)
        {
            engineer = null;
            Vessel active = FlightGlobals.ActiveVessel;
            if (active == null || !active.isEVA)
            {
                return false;
            }

            System.Collections.Generic.List<ProtoCrewMember> crew = active.GetVesselCrew();
            if (crew == null || crew.Count == 0)
            {
                return false;
            }

            ProtoCrewMember candidate = crew[0];
            if (!IsEngineerAtLeast(candidate, MinEngineerLevel))
            {
                return false;
            }

            engineer = candidate;
            return true;
        }

        internal static bool HasDustToClean(double dustVisualSeconds, double dustWearSeconds)
        {
            return dustVisualSeconds > 1e-3 || dustWearSeconds > 1e-3;
        }

        internal static bool IsWearLowEnoughToClean(double combinedWearFactor)
        {
            double wearAmount = 1.0 - Math.Max(0.0, Math.Min(1.0, combinedWearFactor));
            return wearAmount < MaxWearFraction;
        }

        /// <summary>Show the EVA PAW button when a qualified engineer is active and wear allows cleaning.</summary>
        internal static bool CanOfferCleanButton(double combinedWearFactor, double dustVisualSeconds, double dustWearSeconds)
        {
            // Dust args kept for call-site consistency; presence is checked on click.
            _ = dustVisualSeconds;
            _ = dustWearSeconds;
            return TryGetActiveEvaEngineer(out _)
                && IsWearLowEnoughToClean(combinedWearFactor);
        }

        internal static string FailReason(
            double combinedWearFactor,
            double dustVisualSeconds,
            double dustWearSeconds)
        {
            if (!HasDustToClean(dustVisualSeconds, dustWearSeconds))
            {
                return Localizer.Format("#WDSP_CleanDust_noDust");
            }

            if (!IsWearLowEnoughToClean(combinedWearFactor))
            {
                return Localizer.Format("#WDSP_CleanDust_tooWorn");
            }

            if (!TryGetActiveEvaEngineer(out _))
            {
                return Localizer.Format("#WDSP_CleanDust_needEngineer");
            }

            return Localizer.Format("#WDSP_CleanDust_failed");
        }

        internal static void PostMessage(string message, bool success)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            ScreenMessages.PostScreenMessage(
                message,
                4f,
                success ? ScreenMessageStyle.UPPER_CENTER : ScreenMessageStyle.UPPER_CENTER);
        }
    }
}

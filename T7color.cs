using EFT.InventoryLogic;
using UnityEngine;

namespace BetterVision
{
    internal static class T7Color
    {
        internal static Texture2D ExternalRampTexture;

        private class ThermalParams
        {
            public ThermalVisionComponent.SelectablePalette RampPalette;
            public float MainTexColorCoef;
            public float MinimumTemperatureValue;
            public float RampShift;
        }

        private static readonly ThermalParams OriginalBlackHot = new ThermalParams
        {
            RampPalette = ThermalVisionComponent.SelectablePalette.BlackHot,
            MainTexColorCoef = 0.2f,
            MinimumTemperatureValue = 0.25f,
            RampShift = -0.059f
        };

        private static ThermalParams FlirWhiteHot => new ThermalParams
        {
            RampPalette = ThermalVisionComponent.SelectablePalette.WhiteHot,
            MainTexColorCoef = BetterVision.Adv_MainTexColorCoef.Value,
            MinimumTemperatureValue = BetterVision.Adv_MinimumTemperatureValue.Value,
            RampShift = BetterVision.Adv_RampShift.Value
        };

        internal static void Apply(ThermalVision tv)
        {
            if (tv == null || tv.ThermalVisionUtilities == null)
                return;

            var util = tv.ThermalVisionUtilities;
            var vc = util.ValuesCoefs;

            ThermalParams preset = BetterVision.T7RedHot.Value
                ? FlirWhiteHot
                : OriginalBlackHot;

            util.CurrentRampPalette = preset.RampPalette;

            if (BetterVision.T7RedHot.Value && ExternalRampTexture != null)
            {
                foreach (var connector in util.RampTexPalletteConnectors)
                {
                    if (connector.SelectablePalette == ThermalVisionComponent.SelectablePalette.WhiteHot)
                    {
                        connector.Texture = ExternalRampTexture;
                    }
                }
            }

            if (vc != null)
            {
                vc.MainTexColorCoef = preset.MainTexColorCoef;
                vc.MinimumTemperatureValue = preset.MinimumTemperatureValue;
                vc.RampShift = preset.RampShift;
            }
        }
    }
}

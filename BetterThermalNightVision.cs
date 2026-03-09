using BepInEx;
using BepInEx.Configuration;
using BSG.CameraEffects;
using EFT.CameraControl;
using HarmonyLib;
using System.IO;
using UnityEngine;

namespace BetterVision
{
    [BepInPlugin("ciallo.BetterThermalNightVision", "Better Thermal & Night Vision", "1.3.2")]
    public class BetterVision : BaseUnityPlugin
    {
        internal const int ConfigVersion = 132;
        internal static ConfigEntry<int> ConfigFileVersion;
        public static BetterVision Instance;

        internal static ConfigEntry<bool> ScopeFps;
        internal static ConfigEntry<bool> ScopeGlitch;
        internal static ConfigEntry<bool> ScopeMotion;
        internal static ConfigEntry<bool> ScopeNoise;
        internal static ConfigEntry<bool> ScopePixel;
        internal static ConfigEntry<bool> ScopeChromatic;
        internal static ConfigEntry<bool> ScopeBlur;
        internal static ConfigEntry<float> ScopeMaxDistance;
        internal static ConfigEntry<float> ScopeDepthFade;

        internal static ConfigEntry<bool> ScopeUseCustomColor;
        internal static ConfigEntry<float> ScopeMainTexColorCoef;
        internal static ConfigEntry<float> ScopeMinTempValue;
        internal static ConfigEntry<float> ScopeRampShift;
        internal static bool ScopeColorInitialized = false;
        internal static float ScopeRead_MainTexColorCoef;
        internal static float ScopeRead_MinTempValue;
        internal static float ScopeRead_RampShift;
        internal static ConfigEntry<bool> ApplyReadColorOnce;

        internal static ConfigEntry<bool> T7Fps;
        internal static ConfigEntry<bool> T7Glitch;
        internal static ConfigEntry<bool> T7Motion;
        internal static ConfigEntry<bool> T7Noise;
        internal static ConfigEntry<bool> T7Pixel;
        internal static ConfigEntry<bool> T7Blur;
        internal static ConfigEntry<bool> T7BlockScope;
        internal static ConfigEntry<float> T7DepthFade;
        internal static ConfigEntry<bool> T7RedHot;
        internal static ConfigEntry<float> Adv_MainTexColorCoef;
        internal static ConfigEntry<float> Adv_MinimumTemperatureValue;
        internal static ConfigEntry<float> Adv_RampShift;

        internal static ConfigEntry<bool> NVNoise;
        internal static ConfigEntry<bool> NVScopeDefault;
        internal static ConfigEntry<float> NVScopeBrightness;
        internal static ConfigEntry<bool> NVT7Mask;

        public void Log(string msg)
        {
            Logger.LogInfo(msg);
        }

        private void Awake()
        {
            Instance = this;
            string configPath = Config.ConfigFilePath;
            int oldVersion = 0;
            if (File.Exists(configPath))
            {
                var temp = new ConfigFile(configPath, true);
                oldVersion = temp.Bind("Internal", "ConfigVersion", 0).Value;
            }
            if (oldVersion != ConfigVersion)
                File.Delete(configPath);
            ConfigFileVersion = Config.Bind("Internal", "ConfigVersion", ConfigVersion,
                new ConfigDescription("", null, new ConfigurationManagerAttributes { Browsable = false }));

            string pluginDir = Path.GetDirectoryName(Info.Location);
            string pngPath = Path.Combine(pluginDir, "ColorRamp.png");

            if (File.Exists(pngPath))
            {
                byte[] data = File.ReadAllBytes(pngPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(data);
                T7Color.ExternalRampTexture = tex;
            }

            ScopeFps = Config.Bind("Thermal Optic", "FPS Limit", false);
            ScopeGlitch = Config.Bind("Thermal Optic", "Scanline", false);
            ScopeMotion = Config.Bind("Thermal Optic", "Motion Blur", false);
            ScopeNoise = Config.Bind("Thermal Optic", "Noise", false);
            ScopePixel = Config.Bind("Thermal Optic", "Pixelation", false);
            ScopeChromatic = Config.Bind("Thermal Optic", "Edge Aberration", false);
            ScopeBlur = Config.Bind("Thermal Optic", "Blur", false);

            ScopeMaxDistance = Config.Bind("Thermal Optic", "Distance", 500f,
                new ConfigDescription("", new AcceptableValueRange<float>(100f, 2000f)));
            ScopeDepthFade = Config.Bind("Thermal Optic", "Depth Fade (?)", 0.01f,
                new ConfigDescription("Lower is clearer on far distance",
                    new AcceptableValueRange<float>(0.001f, 0.050f)));

            ScopeUseCustomColor = Config.Bind("Thermal Optic", "Use Custom Color (?)", false,
                new ConfigDescription("Warning:\n These values will cover each thermal scopes modes in raid.\n" +
                                      "One set of values won't fit different scopes or color modes.\n" +
                                      "To recover, refer to Use Recover Default",
                    null, new ConfigurationManagerAttributes { IsAdvanced = true }));
            ScopeMainTexColorCoef = Config.Bind("Thermal Optic", "Use MainTexColorCoef - Brightness", 0.5f,
                new ConfigDescription("", new AcceptableValueRange<float>(0.01f, 1f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            ScopeMinTempValue = Config.Bind("Thermal Optic", "Use MinTempValue - ColorDiff", 0.2f,
                new ConfigDescription("", new AcceptableValueRange<float>(-1f, 1f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            ScopeRampShift = Config.Bind("Thermal Optic", "Use RampShift - ColorShift", -0.5f,
                new ConfigDescription("", new AcceptableValueRange<float>(-1f, 1f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            ApplyReadColorOnce = Config.Bind("Thermal Optic", "Use Recover Default (?)", false,
                new ConfigDescription("Enable to reset the scope's default values.\n Close menu and use current thermal scope to apply.\n" +
                                      "Only cache values of first thermal scope mode used in raid.\n To recover >1 scopes (modes) you need to go next raid and disable Use Custom Color.",
                    null, new ConfigurationManagerAttributes { IsAdvanced = true }));

            T7Fps = Config.Bind("T7 Thermal", "FPS Limit", false);
            T7Glitch = Config.Bind("T7 Thermal", "Glitch effect", false);
            T7Motion = Config.Bind("T7 Thermal", "Motion Blur", false);
            T7Noise = Config.Bind("T7 Thermal", "Noise", false);
            T7Pixel = Config.Bind("T7 Thermal", "Pixelation", false);
            T7Blur = Config.Bind("T7 Thermal", "Blur", false);
            T7BlockScope = Config.Bind("T7 Thermal", "Block Optic Scope (?)", false,
                new ConfigDescription("If disable, can only revert block after raid. Zoomable scopes have no thermograph due to rendering of EFT"));
            T7DepthFade = Config.Bind("T7 Thermal", "Depth Fade (?)", 0.01f,
                new ConfigDescription("Lower is more visible on far distance. T7 Default is 0.03",
                    new AcceptableValueRange<float>(0.001f, 0.050f)));

            T7RedHot = Config.Bind("T7 Thermal", "White-Red Mode", false);
            Adv_MainTexColorCoef = Config.Bind("T7 Thermal", "WR MainTexColorCoef - Brightness", 0.7f,
                new ConfigDescription("", new AcceptableValueRange<float>(0.01f, 1f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            Adv_MinimumTemperatureValue = Config.Bind("T7 Thermal", "WR MinTempValue - ColorDiff", 0.1f,
                new ConfigDescription("", new AcceptableValueRange<float>(-1f, 1f), new ConfigurationManagerAttributes { IsAdvanced = true }));
            Adv_RampShift = Config.Bind("T7 Thermal", "WR RampShift - ColorShift", -0.45f,
                new ConfigDescription("", new AcceptableValueRange<float>(-1f, 1f), new ConfigurationManagerAttributes { IsAdvanced = true }));

            NVNoise = Config.Bind("Night Vision", "Noise", false);
            NVScopeDefault = Config.Bind("Night Vision", "Optic Bright Default (?)", false,
                new ConfigDescription("Whether enable vanilla scope vision brightness when using helmet NV, which is darker in zoomable scopes"));
            NVScopeBrightness = Config.Bind("Night Vision", "Optic Brightness (?)", 1.0f,
                new ConfigDescription("Need disable Optic Bright Default",
                    new AcceptableValueRange<float>(0.5f, 3f)));
            NVT7Mask = Config.Bind("Black Screen Mask", "Helmet NV & T7", false);

            new Harmony("ciallo.BetterThermalNightVision").PatchAll();
        }
    }

    [HarmonyPatch(typeof(OpticComponentUpdater), "CopyComponentFromOptic")]
    public class Patch_OpticThermal
    {
        static void Postfix(OpticComponentUpdater __instance, OpticSight opticSight)
        {
            var thermalData = opticSight.ScopeData.ThermalVisionData;
            if (thermalData == null || !thermalData.ThermalVision)
                return;

            ThermalVision tv = __instance.GetComponent<ThermalVision>();
            if (tv == null)
                return;

            tv.IsFpsStuck = BetterVision.ScopeFps.Value;
            tv.IsGlitch = BetterVision.ScopeGlitch.Value;
            tv.IsMotionBlurred = BetterVision.ScopeMotion.Value;
            tv.IsNoisy = BetterVision.ScopeNoise.Value;
            tv.IsPixelated = BetterVision.ScopePixel.Value;
            tv.ThermalVisionUtilities.DepthFade = BetterVision.ScopeDepthFade.Value;

            if (!BetterVision.ScopeBlur.Value)
            {
                tv.UnsharpRadiusBlur = 0f;
                tv.UnsharpBias = 0f;
            }

            ChromaticAberration ca = tv.GetComponent<ChromaticAberration>();
            if (ca != null)
            {
                ca.Shift = BetterVision.ScopeChromatic.Value
                    ? tv.ChromaticAberrationThermalShift
                    : 0f;
            }

            var util = tv.ThermalVisionUtilities;
            var vc = util?.ValuesCoefs;

            if (vc != null)
            {
                if (!BetterVision.ScopeColorInitialized)
                {
                    BetterVision.ScopeRead_MainTexColorCoef = vc.MainTexColorCoef;
                    BetterVision.ScopeRead_MinTempValue = vc.MinimumTemperatureValue;
                    BetterVision.ScopeRead_RampShift = vc.RampShift;
                    BetterVision.ScopeColorInitialized = true;
                }
                if (BetterVision.ScopeUseCustomColor.Value)
                {
                    if (BetterVision.ApplyReadColorOnce.Value)
                    {
                        BetterVision.ScopeMainTexColorCoef.Value = BetterVision.ScopeRead_MainTexColorCoef;
                        BetterVision.ScopeMinTempValue.Value = BetterVision.ScopeRead_MinTempValue;
                        BetterVision.ScopeRampShift.Value = BetterVision.ScopeRead_RampShift;
                        BetterVision.ApplyReadColorOnce.Value = false;
                    }
                    vc.MainTexColorCoef = BetterVision.ScopeMainTexColorCoef.Value;
                    vc.MinimumTemperatureValue = BetterVision.ScopeMinTempValue.Value;
                    vc.RampShift = BetterVision.ScopeRampShift.Value;
                }
            }

            var cam = __instance.GetComponent<UnityEngine.Camera>();
            if (cam != null)
                cam.farClipPlane = BetterVision.ScopeMaxDistance.Value;

            tv.SetMaterialProperties();
        }
    }

    [HarmonyPatch(typeof(PlayerCameraController), "method_5")]
    public class Patch_T7Thermal
    {
        static void Postfix()
        {
            ThermalVision tv = CameraClass.Instance.ThermalVision;
            if (tv == null)
                return;

            T7Color.Apply(tv);

            tv.IsFpsStuck = BetterVision.T7Fps.Value;
            tv.IsGlitch = BetterVision.T7Glitch.Value;
            tv.IsMotionBlurred = BetterVision.T7Motion.Value;
            tv.IsNoisy = BetterVision.T7Noise.Value;
            tv.IsPixelated = BetterVision.T7Pixel.Value;
            tv.ThermalVisionUtilities.DepthFade = BetterVision.T7DepthFade.Value;

            if (!BetterVision.T7Blur.Value) { tv.UnsharpRadiusBlur = 0f; tv.UnsharpBias = 0f; }

            tv.SetMaterialProperties();
        }
    }

    [HarmonyPatch(typeof(PlayerCameraController), "method_4")]
    public class Patch_NV_Noise
    {
        static void Postfix()
        {
            if (BetterVision.NVNoise.Value)
                return;

            NightVision nv = CameraClass.Instance.NightVision;
            if (nv == null)
                return;

            nv.NoiseIntensity = 0f;
            nv.NoiseScale = 0f;
            nv.ApplySettings();
        }
    }

    [HarmonyPatch(typeof(ThermalVision), "OnPreCull")]
    public class Patch_Mask
    {
        static void Prefix(ThermalVision __instance)
        {
            if (BetterVision.NVT7Mask.Value)
                return;

            if (__instance.TextureMask != null)
                __instance.TextureMask.enabled = false;
        }
    }
}

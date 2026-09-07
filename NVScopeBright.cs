using System.Reflection;
using BSG.CameraEffects;
using EFT.CameraControl;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace BetterVision
{
    public static class NVScopeBright
    {
        private static CommandBuffer _cb;
        private static int _rtId = Shader.PropertyToID("_NVScopeBrightRT");

        private static Material _nvMat;
        private static Material _nvSource;
        private static Camera _opticCam;

        public static void Init(OpticCameraManager manager)
        {
            if (manager == null || manager.Camera == null)
                return;
            _opticCam = manager.Camera;
            _cb = new CommandBuffer { name = "NVScopeBright_CommandBuffer" };
            _opticCam.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, _cb);
            _opticCam.AddCommandBuffer(CameraEvent.BeforeImageEffects, _cb);

            // This used to be a Harmony postfix on OpticCameraManager.method_2, the method that
            // runs when a scope becomes the active optic. That name is gone on a deobfuscated
            // 4.1 client - but the last thing the method does is raise OnOpticEnabled, so
            // subscribing to the event runs at exactly the same point and needs no patch at all.
            // Init can run more than once per session (it rebuilds the optic camera), so drop
            // any earlier subscription first rather than stacking duplicates.
            manager.OnOpticEnabled -= Rebuild;
            manager.OnOpticEnabled += Rebuild;
        }

        public static void Rebuild()
        {
            if (_opticCam == null || _cb == null)
                return;

            _cb.Clear();

            if (BetterVision.NVScopeDefault.Value)
                return;

            var mainNV = CameraManager.Instance.NightVision;
            if (mainNV == null || !mainNV.On)
                return;

            Material source = ResolveMaterial(mainNV);
            if (source == null)
                return;

            if (_nvMat == null || _nvSource != source)
            {
                _nvSource = source;
                _nvMat = new Material(source);
            }

            float boost = BetterVision.NVScopeBrightness.Value;
            float baseIntensity = mainNV.Intensity;

            _nvMat.SetFloat("_Intensity", baseIntensity * boost);

            _cb.GetTemporaryRT(_rtId, -1, -1, 0, FilterMode.Bilinear);

            _cb.Blit(BuiltinRenderTextureType.CameraTarget, _rtId, _nvMat);
            _cb.Blit(_rtId, BuiltinRenderTextureType.CameraTarget);

            _cb.ReleaseTemporaryRT(_rtId);
        }

        private static PropertyInfo _materialProperty;
        private static bool _materialPropertyResolved;

        /// <summary>
        /// NightVision's material used to be reachable as <c>Material_0</c>. That name is
        /// derived from the property's own type, which is exactly the shape SPT's assembly tool
        /// treats as leftover obfuscation, so 4.1 may well have renamed it. Find it by shape
        /// instead - it is the only Material-typed property NightVision declares - and keep the
        /// old name as the first guess so the common case costs one lookup.
        ///
        /// The private backing field is deliberately not used: the getter creates the material
        /// lazily, so the field reads null until something has asked for it at least once.
        /// </summary>
        private static Material ResolveMaterial(NightVision nv)
        {
            if (!_materialPropertyResolved)
            {
                _materialPropertyResolved = true;

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                _materialProperty = typeof(NightVision).GetProperty("Material_0", flags);

                if (_materialProperty == null || _materialProperty.PropertyType != typeof(Material))
                {
                    _materialProperty = null;
                    foreach (PropertyInfo p in typeof(NightVision).GetProperties(flags))
                    {
                        if (p.PropertyType != typeof(Material) || p.GetGetMethod(true) == null)
                            continue;
                        if (_materialProperty != null)
                        {
                            _materialProperty = null;
                            break;
                        }
                        _materialProperty = p;
                    }
                }

                if (_materialProperty == null)
                    BetterVision.PluginLog.LogError(
                        "Could not find the night-vision material on BSG.CameraEffects.NightVision; " +
                        "the optic brightness option will do nothing.");
                else
                    BetterVision.PluginLog.LogInfo($"Night-vision material property: {_materialProperty.Name}");
            }

            return _materialProperty?.GetValue(nv, null) as Material;
        }
    }

    // Patch：倍镜相机初始化
    [HarmonyPatch(typeof(OpticCameraManager), "Init")]
    public class Patch_NVScopeBright_Init
    {
        static void Postfix(OpticCameraManager __instance)
        {
            NVScopeBright.Init(__instance);
        }
    }
}

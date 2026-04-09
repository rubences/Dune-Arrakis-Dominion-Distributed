// ====================================================================
// DuneArrakis Dominion - HolographicScanlineEffect (PostProcessing)
// Shader-side helper: aplica un efecto de HUD holográfico de scanlines
// al render texture de la cámara principal.
//
// Requiere: Universal Render Pipeline (URP) con Custom Post Processing
// o insertarse como componente en una Camera en Overlay.
// ====================================================================

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DuneArrakis.Unity.VFX
{
    [System.Serializable, VolumeComponentMenuForRenderPipeline("DuneArrakis/Holographic Scanline", typeof(UniversalRenderPipeline))]
    public class HolographicScanlineEffect : VolumeComponent, IPostProcessComponent
    {
        [Header("Scanlines")]
        public ClampedFloatParameter scanlineIntensity = new ClampedFloatParameter(0.08f, 0f, 1f);
        public ClampedFloatParameter scanlineFrequency = new ClampedFloatParameter(800f, 100f, 2000f);
        public ClampedFloatParameter scanlineSpeed     = new ClampedFloatParameter(1.5f, 0f, 10f);

        [Header("Vignette & Chromatic")]
        public ClampedFloatParameter vignetteIntensity    = new ClampedFloatParameter(0.25f, 0f, 1f);
        public ClampedFloatParameter chromaticAberration  = new ClampedFloatParameter(0.015f, 0f, 0.1f);

        [Header("Color Grading")]
        public ColorParameter        tintColor = new ColorParameter(new Color(0.6f, 0.4f, 0.0f, 0.1f));
        public ClampedFloatParameter contrast  = new ClampedFloatParameter(1.1f, 0.5f, 2f);

        public bool IsActive() => scanlineIntensity.value > 0.001f;
        public bool IsTileCompatible() => false;
    }
}

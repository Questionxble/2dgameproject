// Alternative Bloom Post-Processing Setup Guide for Lightning Glow
// Place this script on your Main Camera or a Post-Process Volume

/*
BLOOM POST-PROCESSING SETUP FOR LIGHTNING GLOW:

1. Install Post Processing Stack:
   - Window → Package Manager → Search "Post Processing" → Install

2. Setup Post Process Volume:
   - Create Empty GameObject → Add Component → Post-process Volume
   - Set "Is Global" to true
   - Create new Post Process Profile asset

3. Add Bloom Effect:
   - In Profile → Add Effect → Unity → Bloom
   - Configure Bloom settings:
     * Intensity: 0.3-0.8 (how bright the glow)
     * Threshold: 0.9-1.2 (minimum brightness to glow)
     * Soft Knee: 0.5-1.0 (smooth falloff)
     * Note: Glow spread is controlled internally by Unity's Bloom

4. Setup Camera:
   - Main Camera → Add Component → Post-process Layer
   - Set Layer to "Everything" or create dedicated layer

5. Lightning Material Adjustments:
   - Increase emission values in lightning materials
   - Use HDR colors (values > 1.0) for emission
   - Ensure materials use values above bloom threshold

PROS:
✅ Automatic glow on all bright objects
✅ True HDR lighting effects  
✅ Looks incredibly realistic
✅ Affects surrounding environment

CONS:
❌ Performance cost on lower-end devices
❌ Affects ALL bright objects in scene
❌ Requires Post-Processing package
❌ Less precise control per object

RECOMMENDED MATERIAL VALUES FOR BLOOM:
- Main Lightning Emission: (1.5, 2.5, 4.0) - Bright blue HDR
- Chain Lightning Emission: (1.0, 1.5, 3.0) - Medium blue HDR
- Sky Bolt Emission: (2.0, 3.0, 5.0) - Very bright HDR

The dual LineRenderer approach implemented above gives you the same visual effect
without requiring post-processing setup and gives you precise control over each
lightning arc's glow appearance.
*/

using UnityEngine;

// Conditional compilation for post-processing support
#if UNITY_POST_PROCESSING_STACK_V2
using UnityEngine.Rendering.PostProcessing;
#endif

namespace LightningEffects
{
    // Optional component to automatically setup bloom for lightning
    // Only works if Post Processing Stack V2 is installed
#if UNITY_POST_PROCESSING_STACK_V2
    [RequireComponent(typeof(PostProcessVolume))]
#endif
    public class LightningBloomSetup : MonoBehaviour
    {
        [Header("Bloom Settings for Lightning")]
        [Range(0f, 2f)] public float bloomIntensity = 0.5f;
        [Range(0.5f, 2f)] public float bloomThreshold = 1.0f;
        [Range(0f, 1f)] public float bloomSoftKnee = 0.7f;
        // Note: Unity's Bloom effect doesn't have a radius property
        // The glow spread is controlled internally by the post-processing system
        
        
#if UNITY_POST_PROCESSING_STACK_V2
        private PostProcessVolume volume;
        private Bloom bloom;
#endif
        
        void Start()
        {
#if UNITY_POST_PROCESSING_STACK_V2
            SetupBloomForLightning();
#else
            Debug.LogWarning("Post Processing Stack V2 not found. Lightning Bloom setup skipped. Use dual LineRenderer glow instead.");
#endif
        }
        
        
#if UNITY_POST_PROCESSING_STACK_V2
        void SetupBloomForLightning()
        {
            volume = GetComponent<PostProcessVolume>();
            
            if (volume.profile == null)
            {
                volume.profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            }
            
            if (!volume.profile.TryGetSettings(out bloom))
            {
                bloom = volume.profile.AddSettings<Bloom>();
            }
            
            // Configure bloom for lightning effects
            bloom.intensity.value = bloomIntensity;
            bloom.threshold.value = bloomThreshold;
            bloom.softKnee.value = bloomSoftKnee;
            bloom.enabled.value = true;
            
            Debug.Log("Lightning Bloom post-processing configured");
        }
        
        // Call this to adjust bloom settings at runtime
        public void UpdateBloomSettings()
        {
            if (bloom != null)
            {
                bloom.intensity.value = bloomIntensity;
                bloom.threshold.value = bloomThreshold;
                bloom.softKnee.value = bloomSoftKnee;
            }
        }
        
        void OnValidate()
        {
            if (Application.isPlaying && bloom != null)
            {
                UpdateBloomSettings();
            }
        }
#endif
    }
}
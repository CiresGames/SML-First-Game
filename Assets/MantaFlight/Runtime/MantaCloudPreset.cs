using System;
using UnityEngine;

namespace MantaFlight
{
    [Serializable]
    public struct CloudAppearance
    {
        [Range(0, 1)] public float coverage;
        [Range(.2f, 1)] public float footprint;
        [Range(.06f, 1)] public float thickness;
        [Range(.3f, 3)] public float noiseScale;
        [Range(0, 1.5f)] public float erosion;
        [Range(0, 1)] public float lenticular;
        [Range(0, 1)] public float layers;
        [Range(.05f, 2)] public float density;
        public Color sunlight, shadow;
        public Vector3 wind;
        [Range(0, 2)] public float evolution;
        [Range(0,1)] public float rain;
        [Tooltip("Grey cloud ceiling and suppression of direct sunlight.")][Range(0,1)] public float overcast;
        [Tooltip("Electrical storm activity, independent of rain intensity.")][Range(0,1)] public float thunder;

        public static CloudAppearance Lerp(CloudAppearance a, CloudAppearance b, float t) => new CloudAppearance
        {
            coverage = Mathf.Lerp(a.coverage,b.coverage,t), footprint = Mathf.Lerp(a.footprint,b.footprint,t),
            thickness = Mathf.Lerp(a.thickness,b.thickness,t), noiseScale = Mathf.Lerp(a.noiseScale,b.noiseScale,t),
            erosion = Mathf.Lerp(a.erosion,b.erosion,t), lenticular = Mathf.Lerp(a.lenticular,b.lenticular,t),
            layers = Mathf.Lerp(a.layers,b.layers,t), density = Mathf.Lerp(a.density,b.density,t),
            sunlight = Color.Lerp(a.sunlight,b.sunlight,t), shadow = Color.Lerp(a.shadow,b.shadow,t),
            wind = Vector3.Lerp(a.wind,b.wind,t), evolution = Mathf.Lerp(a.evolution,b.evolution,t), rain=Mathf.Lerp(a.rain,b.rain,t),
            overcast=Mathf.Lerp(a.overcast,b.overcast,t),thunder=Mathf.Lerp(a.thunder,b.thunder,t)
        };
    }

    [CreateAssetMenu(menuName = "Manta/Cloud weather preset")]
    public sealed class MantaCloudPreset : ScriptableObject
    {
        [TextArea] public string description;
        public CloudAppearance appearance = new CloudAppearance
        {
            coverage=.55f, footprint=1, thickness=1, noiseScale=1, erosion=.65f,
            density=.8f, sunlight=new Color(1,.88f,.69f), shadow=new Color(.36f,.48f,.61f),
            wind=new Vector3(2.8f,.12f,.8f), evolution=.35f
        };
    }
}

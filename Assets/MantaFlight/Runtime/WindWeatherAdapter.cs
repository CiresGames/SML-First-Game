using UnityEngine;

namespace MantaFlight
{
    // Le noyau vent ne référence jamais la météo ; cet adaptateur porte la dépendance.
    [DefaultExecutionOrder(-106)]
    public sealed class WindWeatherAdapter : MonoBehaviour
    {
        [Tooltip("Gestionnaire à piloter.")] public WindManager manager;
        [Tooltip("Source du vent global et de l'intensité de pluie interpolée.")] public MantaCloudscape clouds;
        [Tooltip("Force des zones par pluie maximale (Storm).")][Min(0)] public float stormMultiplier = 1.8f;
        [Tooltip("Amplitude de dérive lente des directions, en degrés.")][Range(0, 90)] public float driftAngle = 15;
        [Tooltip("Période de dérive en secondes.")][Min(1)] public float driftPeriod = 180;
        Vector3 savedBackground;
        float savedMultiplier, savedYaw;
        void OnEnable()
        {
            if (!manager) manager = GetComponent<WindManager>();
            if (!manager) return;
            savedBackground = manager.backgroundWind; savedMultiplier = manager.weatherMultiplier; savedYaw = manager.weatherYaw;
        }
        void Update()
        {
            if (!manager || !clouds) return;
            manager.backgroundWind = clouds.CurrentWind;
            manager.weatherMultiplier = Mathf.Lerp(1, stormMultiplier, Mathf.Clamp01(clouds.rain));
            manager.weatherYaw = (Mathf.PerlinNoise(37, Time.time / Mathf.Max(1, driftPeriod)) * 2 - 1) * driftAngle;
        }
        void OnDisable()
        {
            if (!manager) return;
            manager.backgroundWind = savedBackground; manager.weatherMultiplier = savedMultiplier; manager.weatherYaw = savedYaw;
        }
    }
}

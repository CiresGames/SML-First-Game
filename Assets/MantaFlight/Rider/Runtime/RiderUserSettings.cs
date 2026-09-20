using UnityEngine;
namespace MantaFlight.Rider
{
    public sealed class RiderUserSettings : ScriptableObject
    {
        const string PitchKey = "MantaFlight.Rider.InvertPitch.v1";
        [SerializeField] bool invertPitch;
        public bool InvertPitch => invertPitch;
        public static RiderUserSettings Load()
        {
            var value = CreateInstance<RiderUserSettings>();
            value.invertPitch = PlayerPrefs.GetInt(PitchKey, 0) != 0;
            return value;
        }
        public void SetInvertPitch(bool value)
        { invertPitch = value; PlayerPrefs.SetInt(PitchKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}

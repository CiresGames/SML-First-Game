using UnityEngine;
using UnityEngine.Events;
using MantaFlight.Rider;

namespace MantaFlight.Sharks
{
    public sealed class SharkPlayerHealth : MonoBehaviour
    {
        [Min(1)] public float maximumHealth = 100;
        [Min(0)] public float hitInvulnerability = .8f;
        public UnityEvent onBitten = new UnityEvent();
        public UnityEvent onDefeated = new UnityEvent();
        public float Health { get; private set; }
        public int HitsTaken { get; private set; }
        public bool CanBeAttacked => isActiveAndEnabled && Health > 0 && Time.time >= protectedUntil;
        float protectedUntil;
        RiderController rider;
        MantaController manta;
        void Awake()
        {
            Health = maximumHealth;
            rider = GetComponent<RiderController>();
            manta = rider && rider.mount ? rider.mount.manta : GetComponent<MantaController>();
        }
        public bool TakeDamage(float amount)
        {
            if (!CanBeAttacked || amount <= 0) return false;
            Health = Mathf.Max(0, Health - amount); HitsTaken++;
            protectedUntil = Time.time + hitInvulnerability;
            onBitten.Invoke();
            if (Health <= 0)
            {
                onDefeated.Invoke();
                if (manta) { manta.ResetFlight(); Health = maximumHealth; protectedUntil = Time.time + 5; }
            }
            return true;
        }
    }
}

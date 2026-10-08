using System;
using System.Collections.Generic;

namespace MantaFlight
{
    public enum MantaStat { Speed, Manoeuvrability, Endurance, Force, Obedience }

    /// <summary>Save data and deterministic rules. No scene or Unity dependencies.</summary>
    [Serializable]
    public sealed class MantaProgressionData
    {
        public const int MaxLevel = 20, MaxFruits = 2;
        public int version = 2, level = 1;
        public string mantaName = "Manta";
        public float xp, bond, endurance = 100;
        public bool exhausted;
        public int[] fruits = new int[5], inventory = new int[5];
        public List<string> discoveries = new List<string>(), collectedFruits = new List<string>();
        public event Action<int> BondLevelChanged;
        public int BondLevel => Math.Min(10, 1 + (int)Math.Sqrt(Math.Max(0, bond) / 100));
        public float NextBond => BondLevel >= 10 ? 0 : BondLevel * BondLevel * 100;
        public float NextXP => 100 + (level - 1) * 45;
        public static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        public static bool Valid(MantaStat stat) => (int)stat >= 0 && (int)stat < 5;
        public void AddXP(float amount)
        {
            if (!Valid(amount) || level >= MaxLevel) return;
            xp += Math.Min(amount, 100000);
            while (level < MaxLevel && xp >= NextXP) { xp -= NextXP; level++; }
            if (level == MaxLevel) xp = 0;
        }
        public void AddBond(float amount)
        {
            if (!Valid(amount)) return;
            int before = BondLevel; bond = Math.Min(8100, bond + amount);
            for (int next = before + 1; next <= BondLevel; next++) BondLevelChanged?.Invoke(next);
        }
        public bool Discover(string id, float reward = 75, float bondReward = 25)
        {
            if (string.IsNullOrWhiteSpace(id) || discoveries.Contains(id)) return false;
            discoveries.Add(id); AddXP(reward); AddBond(bondReward); return true;
        }
        public bool CollectFruit(string id, MantaStat stat)
        {
            if (!Valid(stat) || string.IsNullOrWhiteSpace(id) || collectedFruits.Contains(id)) return false;
            int i = (int)stat;
            if (fruits[i] + inventory[i] >= MaxFruits) return false;
            collectedFruits.Add(id); inventory[i]++; AddBond(15); return true;
        }
        public bool Feed(MantaStat stat)
        {
            if (!Valid(stat)) return false;
            int i = (int)stat;
            if (inventory[i] <= 0 || fruits[i] >= MaxFruits) return false;
            inventory[i]--; fruits[i]++; AddBond(35); return true;
        }
        public float Strength(MantaStat stat, int extraFruit = 0) => Valid(stat)
            ? Math.Min(MaxFruits, Math.Max(0, fruits[(int)stat] + extraFruit)) * .12f : 0;
        public bool IsValid()
        {
            if (version != 2 || string.IsNullOrWhiteSpace(mantaName) || level < 1 || level > MaxLevel
                || !Valid(xp) || xp >= NextXP || !Valid(bond) || bond > 8100 || !Valid(endurance)
                || fruits == null || inventory == null || fruits.Length != 5 || inventory.Length != 5
                || discoveries == null || collectedFruits == null) return false;
            for (int i = 0; i < 5; i++)
            {
                if (fruits[i] < 0 || inventory[i] < 0 || fruits[i] > MaxFruits || inventory[i] > MaxFruits
                    || fruits[i] + inventory[i] > MaxFruits) return false;
            }
            return true;
        }
    }
}

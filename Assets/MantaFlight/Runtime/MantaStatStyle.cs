using System;
using UnityEngine;

namespace MantaFlight
{
    /// <summary>Shared by UI, world fruit and the Blender authoring script.</summary>
    public static class MantaStatStyle
    {
        [Serializable] public sealed class Entry { public string stat, hex, shape, resource; }
        [Serializable] sealed class Palette { public Entry[] entries; }
        static Entry[] entries;
        public static Entry Get(MantaStat stat)
        {
            if (entries == null)
            {
                var source = Resources.Load<TextAsset>("MantaFruitPalette");
                if (source == null) throw new InvalidOperationException("Missing MantaFruitPalette resource.");
                entries = JsonUtility.FromJson<Palette>(source.text).entries;
            }
            foreach (var entry in entries) if (entry.stat == stat.ToString()) return entry;
            throw new ArgumentOutOfRangeException(nameof(stat));
        }
        public static Color Color(MantaStat stat)
        {
            if (!ColorUtility.TryParseHtmlString("#" + Get(stat).hex, out var color))
                throw new InvalidOperationException("Invalid manta stat color: " + stat);
            return color;
        }
    }
}

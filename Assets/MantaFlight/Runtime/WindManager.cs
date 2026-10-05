using System.Collections.Generic;
using UnityEngine;

namespace MantaFlight
{
    [DefaultExecutionOrder(-105), DisallowMultipleComponent]
    public sealed class WindManager : MonoBehaviour
    {
        [Tooltip("Taille des cellules de la grille en mètres.")][Min(10)] public float cellSize = 400;
        [Tooltip("Délai maximal de réindexation des volumes déplacés/redimensionnés.")][Min(.02f)] public float rebuildInterval = .25f;
        [Tooltip("Au-delà, une très grande zone passe dans la liste de débordement.")][Min(1)] public int maxCellsPerZone = 4096;
        [Tooltip("Vent hors zones, en mètres par seconde ; peut être piloté par la météo.")] public Vector3 backgroundWind;
        [Tooltip("Multiplicateur météo des zones.")][Min(0)] public float weatherMultiplier = 1;
        [Tooltip("Dérive météo de la direction des zones, en degrés.")] public float weatherYaw;
        public static WindManager Instance { get; private set; }
        public int LastCandidateCount { get; private set; }
        static readonly HashSet<WindZone3D> zones = new HashSet<WindZone3D>();
        readonly Dictionary<Vector3Int, List<WindZone3D>> grid = new Dictionary<Vector3Int, List<WindZone3D>>();
        readonly List<WindZone3D> oversized = new List<WindZone3D>();
        readonly Stack<List<WindZone3D>> pool = new Stack<List<WindZone3D>>();
        bool dirty = true;
        float nextRebuild, indexedCellSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; zones.Clear(); }
        void OnEnable()
        {
            if (Instance && Instance != this) { Debug.LogError("Only one WindManager may be active.", this); enabled = false; return; }
            Instance = this; dirty = true;
        }
        void OnDisable() { if (Instance == this) Instance = null; }
        void OnValidate() => dirty = true;
        void Update() { if (dirty || Time.time >= nextRebuild) RebuildIndex(); }
        public static void Register(WindZone3D zone) { zones.Add(zone); Invalidate(); }
        public static void Unregister(WindZone3D zone) { zones.Remove(zone); Invalidate(); }
        public static void Invalidate() { if (Instance) Instance.dirty = true; }
        Vector3Int Cell(Vector3 p) => Vector3Int.FloorToInt(p / indexedCellSize);

        public void RebuildIndex()
        {
            foreach (var list in grid.Values) { list.Clear(); pool.Push(list); }
            grid.Clear(); oversized.Clear();
            indexedCellSize = Mathf.Max(10, cellSize);
            foreach (var zone in zones)
            {
                if (!zone || !zone.Usable) continue;
                var bounds = zone.WorldBounds;
                Vector3Int min = Cell(bounds.min), max = Cell(bounds.max);
                double count = ((double)max.x - min.x + 1) * ((double)max.y - min.y + 1) * ((double)max.z - min.z + 1);
                if (count > Mathf.Max(1, maxCellsPerZone)) { oversized.Add(zone); continue; }
                for (int x = min.x; x <= max.x; x++)
                    for (int y = min.y; y <= max.y; y++)
                        for (int z = min.z; z <= max.z; z++)
                        {
                            var key = new Vector3Int(x, y, z);
                            if (!grid.TryGetValue(key, out var list)) grid.Add(key, list = pool.Count > 0 ? pool.Pop() : new List<WindZone3D>());
                            list.Add(zone);
                        }
            }
            dirty = false; nextRebuild = Time.time + Mathf.Max(.02f, rebuildInterval);
        }

        /// <summary>Unique entrée gameplay : vitesse de l'air en coordonnées monde (m/s).</summary>
        public static Vector3 GetWindAt(Vector3 position) => Instance ? Instance.Sample(position) : Vector3.zero;
        public Vector3 Sample(Vector3 position)
        {
            if (dirty) RebuildIndex();
            Vector3 sum = Vector3.zero; float total = 0, coverage = 0;
            LastCandidateCount = 0;
            if (grid.TryGetValue(Cell(position), out var local)) Accumulate(local, position, ref sum, ref total, ref coverage);
            Accumulate(oversized, position, ref sum, ref total, ref coverage);
            // Conserve le falloff même pour une zone seule : normaliser seulement les poids
            // annulerait la transition aux bords. Le fond s'efface progressivement.
            return total > 0 ? Vector3.Lerp(backgroundWind, sum / total, coverage) : backgroundWind;
        }
        void Accumulate(List<WindZone3D> candidates, Vector3 p, ref Vector3 sum, ref float total, ref float coverage)
        {
            foreach (var zone in candidates)
            {
                LastCandidateCount++;
                if (!zone || !zone.Usable || !zone.WorldBounds.Contains(p)) continue;
                float influence = zone.InfluenceAt(p);
                float w = influence * Mathf.Max(.01f, zone.weight);
                sum += zone.Sample(Time.time, Mathf.Max(0, weatherMultiplier), weatherYaw) * w;
                total += w; coverage = Mathf.Max(coverage, influence);
            }
        }
    }
}

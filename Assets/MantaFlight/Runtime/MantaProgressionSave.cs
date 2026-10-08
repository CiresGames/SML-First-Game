using System;
using UnityEngine;

namespace MantaFlight
{
    public static class MantaProgressionSave
    {
        [Serializable] sealed class VersionHeader { public int version; }
        // Read retired fields only to validate v1 snapshots before migration. New saves omit them.
        [Serializable] sealed class LegacyTraining
        {
            public int points;
            public int[] ranks, practice;
            public float[] training;
            public bool IsValid(int level)
            {
                if (points < 0 || points > 38 || ranks == null || practice == null || training == null
                    || ranks.Length != 5 || practice.Length != 5 || training.Length != 5) return false;
                int spent = 0;
                for (int i = 0; i < 5; i++)
                {
                    if (ranks[i] < 1 || ranks[i] > 10 || practice[i] < 0 || practice[i] > 3
                        || !MantaProgressionData.Valid(training[i]) || training[i] >= 180 * (practice[i] + 1)) return false;
                    for (int rank = 1; rank < ranks[i]; rank++) spent += 1 + (rank - 1) / 3;
                }
                return spent + points == (level - 1) * 2 && practice[4] == 0 && training[4] == 0;
            }
        }
        public static MantaProgressionData Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var header = JsonUtility.FromJson<VersionHeader>(json);
            if (header == null || (header.version != 1 && header.version != 2)) return null;
            var data = JsonUtility.FromJson<MantaProgressionData>(json);
            if (data == null) return null;
            if (header.version == 1)
            {
                var legacy = JsonUtility.FromJson<LegacyTraining>(json);
                if (legacy == null || !legacy.IsValid(data.level)) return null;
                data.version = 2;
            }
            return data.IsValid() ? data : null;
        }
    }
}

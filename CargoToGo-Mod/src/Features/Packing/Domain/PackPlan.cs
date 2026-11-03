using System;

namespace CargoToGo.Features.Packing.Domain
{
    /// <summary>The rules of a cargo that need no game: how a requirement is split over the sources in order,
    /// and how cargo prefab names relate to piece prefab names. Kept free of Unity / Valheim types so unit tests cover it.</summary>
    internal static class PackPlan
    {
        /// <summary>Splits <paramref name="wanted"/> over sources in order. available[i] is what source i can hand over.
        /// Result[i] is what to take from source i: never more than it has, earlier sources first, sum = min(wanted, total available).
        /// Negative counts count as 0; wanted &lt;= 0 takes nothing.</summary>
        public static int[] Allocate(int[] available, int wanted)
        {
            if (available == null) return new int[0];
            var take = new int[available.Length];
            var left = wanted;
            // Stryker disable once Equality : with left == 0 the loop body takes 0 from every source, same result (equivalent mutant)
            for (var i = 0; i < available.Length && left > 0; i++)
            {
                var n = Math.Min(left, Math.Max(0, available[i]));
                take[i] = n;
                left -= n;
            }
            return take;
        }

        /// <summary>True when the sources together hold at least <paramref name="wanted"/>. wanted &lt;= 0 is always covered.</summary>
        public static bool Covers(int[] available, int wanted)
        {
            if (wanted <= 0) return true;
            if (available == null) return false;
            long sum = 0;
            foreach (var a in available)
            {
                // Stryker disable once Equality : adding a == 0 changes nothing (equivalent mutant)
                if (a > 0) sum += a;
                if (sum >= wanted) return true;
            }
            return false;
        }

        /// <summary>How much of a requirement could not be taken: wanted minus what was really removed (never negative).</summary>
        public static int Shortfall(int wanted, int[] took)
        {
            long sum = 0;
            // Stryker disable once Equality : adding t == 0 changes nothing (equivalent mutant)
            if (took != null) foreach (var t in took) if (t > 0) sum += t;
            return (int)Math.Max(0, wanted - sum);
        }
    }

    /// <summary>Cargo item prefabs are named CC_Cargo_[piece prefab]; the item's display name is "Cargo: [piece name]".</summary>
    internal static class CargoNames
    {
        public const string PrefabPrefix = "CC_Cargo_";
        public const string NamePrefix = "Cargo: ";

        /// <summary>The cargo item prefab name for a piece prefab name.</summary>
        public static string CargoFor(string piecePrefab)
        {
            return PrefabPrefix + piecePrefab;
        }

        /// <summary>True for a name that is a cargo prefab of some piece (the prefix alone, with no piece, is not).</summary>
        public static bool IsCargo(string prefabName)
        {
            return prefabName != null && prefabName.Length > PrefabPrefix.Length && prefabName.StartsWith(PrefabPrefix, StringComparison.Ordinal);
        }

        /// <summary>The piece prefab a cargo prefab name stands for, or null when the name is not a cargo.</summary>
        public static string PieceOf(string cargoPrefab)
        {
            return IsCargo(cargoPrefab) ? cargoPrefab.Substring(PrefabPrefix.Length) : null;
        }
    }
}

using System;
using System.Collections.Generic;
using AzuCraftyBoxes.IContainers;
using AzuCraftyBoxes.Util;
using BepInEx.Bootstrap;
using CargoToGo.Configuration;
using HarmonyLib;
using KydlCore.Common;
using UnityEngine;

namespace CargoToGo.Features.Packing.Infrastructure
{
    /// <summary>Safe to touch whether or not AzuCraftyBoxes is installed: nothing in this class references Azu types.</summary>
    internal static class AzuCompat
    {
        public const string Guid = "Azumatt.AzuCraftyBoxes";
        private static bool? _installed;

        /// <summary>AzuCraftyBoxes is loaded by BepInEx.</summary>
        public static bool Installed
        {
            get
            {
                if (!_installed.HasValue) _installed = Chainloader.PluginInfos.ContainsKey(Guid);
                return _installed.Value;
            }
        }

        /// <summary>Installed AND enabled in CargoToGo's config.</summary>
        public static bool Active { get { return Installed && ModSettings.UseAzuCraftyBoxes.Value; } }
    }

    /// <summary>Everything that touches AzuCraftyBoxes types lives here. Only call into this class when
    /// <see cref="AzuCompat.Active"/> is true, and keep Azu types out of every signature that other
    /// classes call, so the JIT never needs the Azu assembly when it is not installed.</summary>
    internal static class AzuBridge
    {
        private static bool _resolved;
        private static bool _lookupFailureLogged;
        private static Func<bool> _shouldPrevent;
        private static Func<int, int> _leaveOne;

        /// <summary>The containers AzuCraftyBoxes would pull from right now, using Azu's own range
        /// (its "Container Range" setting), chest toggles and pull mode. Chests whose inventory is in
        /// <paramref name="alreadyCovered"/> are skipped because CargoToGo found them itself.</summary>
        public static List<Source> Collect(Player p, HashSet<Inventory> alreadyCovered)
        {
            var result = new List<Source>();
            try
            {
                if (p == null || Prevented(p)) return result;

                // Azu returns a shared list that it clears and refills later, so wrap the entries immediately.
                var containers = AzuCraftyBoxes.API.GetNearbyContainers(p, AzuCraftyBoxes.AzuCraftyBoxesPlugin.mRange.Value);
                if (containers == null) return result;

                foreach (var c in containers)
                {
                    if (c == null) continue;
                    var inv = c.GetInventory();
                    if (inv != null && alreadyCovered.Contains(inv)) continue;
                    result.Add(new AzuSource(c));
                }
            }
            catch (Exception e)
            {
                if (!_lookupFailureLogged)
                {
                    _lookupFailureLogged = true;
                    ModLog.Error("AzuCraftyBoxes lookup failed, continuing without it (logged once)" + " | " + e);
                }
            }
            return result;
        }

        /// <summary>Resolves the two Azu internals we mirror (pull on/off gate and "leave one item"). Both are optional.</summary>
        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            try
            {
                var prevent = AccessTools.Method(typeof(AzuCraftyBoxes.Util.Functions.MiscFunctions), "ShouldPrevent");
                if (prevent != null) _shouldPrevent = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), prevent);

                var leave = AccessTools.Method(typeof(AzuCraftyBoxes.Util.Functions.Boxes), "CheckAndDecrement", new[] { typeof(int) });
                if (leave != null) _leaveOne = (Func<int, int>)Delegate.CreateDelegate(typeof(Func<int, int>), leave);
            }
            catch (Exception e)
            {
                ModLog.Warn("Could not hook AzuCraftyBoxes internals, using fallbacks: " + e.GetType().Name + ": " + e.Message);
            }
            ModLog.Info("AzuCraftyBoxes integration ready (pull gate: " + (_shouldPrevent != null ? "Azu ShouldPrevent" : "IsPullingAllowed fallback") +
                     ", leave-one rule: " + (_leaveOne != null ? "mirrored" : "not available") + ").");
        }

        /// <summary>True when Azu itself would not pull right now (its mod toggle is off, or the player switched pulling off).</summary>
        private static bool Prevented(Player p)
        {
            Resolve();
            try
            {
                if (_shouldPrevent != null) return _shouldPrevent();
                return !p.IsPullingAllowed();
            }
            catch (Exception e)
            {
                ModLog.Verbose("Azu pull gate check failed, treating as 'prevented': " + e.GetType().Name + ": " + e.Message);
                return true;
            }
        }

        /// <summary>Azu's "leave one" option keeps one of each item in every container.</summary>
        private static int LeaveOne(int count)
        {
            Resolve();
            return _leaveOne != null ? _leaveOne(count) : count;
        }

        /// <summary>Chests need their ZDO ownership claimed before items are removed (same as CargoToGo does for its own
        /// chests). Azu's wrapper does not expose the Container, so it is found again through its inventory.</summary>
        private static void ClaimOwnership(IContainer c)
        {
            if (!(c is VanillaContainer)) return; // drawers claim ownership themselves, bags live in the player's inventory
            try
            {
                var inv = c.GetInventory();
                if (inv == null) return;
                foreach (var col in Physics.OverlapSphere(c.GetPosition(), 3f))
                {
                    var cont = col.GetComponentInParent<Container>();
                    if (cont == null || cont.GetInventory() != inv) continue;
                    var nv = cont.GetComponent<ZNetView>();
                    if (nv != null && nv.IsValid()) nv.ClaimOwnership();
                    return;
                }
                ModLog.Verbose("Could not locate the chest at " + c.GetPosition() + " to claim ownership.");
            }
            catch (Exception e)
            {
                ModLog.Verbose("Claiming ownership of an AzuCraftyBoxes chest failed: " + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>Holds the IContainer as <c>object</c> on purpose: no Azu type in a field or constructor, so merely loading
        /// this class (Harmony's PatchAll scans the whole assembly) can never need the Azu assembly.</summary>
        private sealed class AzuSource : Source
        {
            private readonly object _box;

            public AzuSource(object container) { _box = container; }

            public override SourceKind Kind { get { return SourceKind.Azu; } }

            public override int Count(string sharedName, string itemPrefab)
            {
                try
                {
                    var c = (IContainer)_box;
                    var containerPrefab = c.GetPrefabName();
                    if (string.IsNullOrWhiteSpace(containerPrefab)) return 0;
                    // Azu's per-container include/exclude rules (Azumatt.AzuCraftyBoxes.yml).
                    if (!AzuCraftyBoxes.API.CanItemBePulled(containerPrefab, itemPrefab)) return 0;
                    return Mathf.Max(0, LeaveOne(c.ItemCount(sharedName)));
                }
                catch (Exception e)
                {
                    ModLog.Verbose("Counting in an AzuCraftyBoxes container failed: " + e.GetType().Name + ": " + e.Message);
                    return 0;
                }
            }

            public override int Take(string sharedName, string itemPrefab, int amount)
            {
                try
                {
                    var c = (IContainer)_box;
                    var take = Mathf.Min(amount, Count(sharedName, itemPrefab));
                    if (take <= 0) return 0;
                    ClaimOwnership(c);
                    // Same call Azu uses when crafting: (item, already removed, wanted) -> total removed so far.
                    var removed = c.ProcessContainerInventory(sharedName, 0, take);
                    return Mathf.Clamp(removed, 0, take);
                }
                catch (Exception e)
                {
                    ModLog.Error("Taking items from an AzuCraftyBoxes container failed" + " | " + e);
                    return 0;
                }
            }
        }
    }
}

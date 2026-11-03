using Kydl.Common;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace CargoToGo
{
    /// <summary>Creates one cargo item prefab per buildable piece (CC_Cargo_[piece prefab]).
    /// The contents of a cargo are implicit: exactly the piece's m_resources.</summary>
    internal static class CargoFactory
    {
        public const string PrefabPrefix = CargoNames.PrefabPrefix;
        public const string NamePrefix = CargoNames.NamePrefix;
        private static GameObject _holder;
        private static int RestrictedCount;   // cargos created in the current Ensure() pass that can't go through portals

        private static GameObject Holder()
        {
            if (_holder == null)
            {
                _holder = new GameObject("CargoToGoPrefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            return _holder;
        }

        public static void Ensure(ObjectDB db)
        {
            if (db == null || db.m_items == null || db.m_items.Count == 0) return;

            var existing = new HashSet<string>();
            var existingCargo = new Dictionary<string, GameObject>();
            GameObject basePrefab = null, fallback = null;
            foreach (var go in db.m_items)
            {
                if (go == null) continue;
                existing.Add(go.name);
                if (go.name == "CargoCrate") basePrefab = go;
                else if (go.name == "Stone") fallback = go;
                else if (CargoNames.IsCargo(go.name)) existingCargo[go.name] = go;
            }
            if (basePrefab == null) basePrefab = fallback;
            if (basePrefab == null) { ModLog.Error("Neither 'CargoCrate' nor 'Stone' found in ObjectDB; no cargo items were registered."); return; }
            if (basePrefab.name != "CargoCrate") ModLog.Warn("Prefab 'CargoCrate' not found, using '" + basePrefab.name + "' as the cargo base.");

            var added = 0;
            RestrictedCount = 0;
            foreach (var go in db.m_items.ToArray())
            {
                var drop = go != null ? go.GetComponent<ItemDrop>() : null;
                var table = drop != null ? drop.m_itemData.m_shared.m_buildPieces : null;
                if (table == null || table.m_pieces == null) continue;
                foreach (var pg in table.m_pieces)
                {
                    var piece = pg != null ? pg.GetComponent<Piece>() : null;
                    if (piece == null || piece.m_icon == null || piece.m_resources == null || piece.m_resources.Length == 0) continue;
                    var pname = Utils.GetPrefabName(pg);
                    var cname = PrefabPrefix + pname;
                    if (!existing.Add(cname))
                    {
                        // Already registered by an earlier pass: just make sure its portal flag matches the materials as they are now.
                        GameObject known;
                        if (existingCargo.TryGetValue(cname, out known))
                        {
                            var kd = known.GetComponent<ItemDrop>();
                            string kb;
                            if (kd != null) kd.m_itemData.m_shared.m_teleportable = ContentsTeleportable(piece, out kb);
                        }
                        continue;
                    }
                    try { if (Create(db, basePrefab, cname, piece)) added++; }
                    catch (System.Exception e) { ModLog.Error("Failed to create cargo '" + cname + "'" + " | " + e); }
                }
            }
            if (added > 0)
            {
                var upd = AccessTools.Method(typeof(ObjectDB), "UpdateRegisters");
                if (upd == null) ModLog.Error("ObjectDB.UpdateRegisters not found; new cargo items may not be resolvable.");
                else upd.Invoke(db, null);
                ModLog.Info("Registered " + added + " new cargo items in ObjectDB (cloned from '" + basePrefab.name + "'); " + RestrictedCount + " of them contain materials that can't pass through portals.");
            }
        }

        private static bool Create(ObjectDB db, GameObject basePrefab, string cname, Piece piece)
        {
            var clone = Object.Instantiate(basePrefab, Holder().transform);
            clone.name = cname;
            var drop = clone.GetComponent<ItemDrop>();
            if (drop == null) { ModLog.Warn("Base prefab has no ItemDrop; skipped '" + cname + "'"); Object.Destroy(clone); return false; }

            var sd = drop.m_itemData.m_shared;
            var desc = new StringBuilder("Sealed materials for building this piece. They can only be used for that.\nCtrl+Alt+Right-click destroys the cargo and returns the materials.\n");
            float weight = 0f;
            foreach (var r in piece.m_resources)
            {
                if (r.m_resItem == null || r.m_amount <= 0) continue;
                weight += r.m_amount * r.m_resItem.m_itemData.m_shared.m_weight;
                desc.Append("\n").Append(r.m_amount).Append("x ").Append(r.m_resItem.m_itemData.m_shared.m_name);
            }

            // Portal rules: a cargo is exactly as teleportable as the materials inside it (ores, ingots, ... are not).
            // The world modifiers (TeleportAll / NoPortals) are applied by vanilla Inventory.IsTeleportable on top of this flag.
            string blocker;
            var teleportable = ContentsTeleportable(piece, out blocker);
            if (!teleportable)
            {
                desc.Append("\n\nContains materials that can't pass through portals (unless the world's portal rules allow everything).");
                RestrictedCount++;
                ModLog.Verbose("Cargo '" + cname + "' is portal-restricted: contains '" + blocker + "'.");
            }

            sd.m_name = NamePrefix + piece.m_name;
            sd.m_description = desc.ToString();
            sd.m_itemType = ItemDrop.ItemData.ItemType.Material;
            sd.m_icons = new[] { piece.m_icon };
            sd.m_variants = 0;
            sd.m_maxStackSize = 1;
            sd.m_autoStack = false;
            sd.m_maxQuality = 1;
            sd.m_weight = weight * ModSettings.WeightMultiplier.Value;
            sd.m_teleportable = teleportable;
            sd.m_questItem = false;
            sd.m_dlc = "";
            sd.m_value = 0;
            sd.m_equipStatusEffect = null;
            sd.m_movementModifier = 0f;
            sd.m_useDurability = false;
            sd.m_canBeReparied = false;
            drop.m_itemData.m_dropPrefab = clone;
            db.m_items.Add(clone);
            return true;
        }

        // ---------- portal rules ----------
        /// <summary>True only if every material of the piece is itself teleportable (vanilla m_teleportable flag).
        /// Metal ores / ingots / scrap etc. are not, so a cargo holding them isn't either. Follows modded items too,
        /// because it reads the live flag instead of a hard-coded list. Unknown piece -> false (fail closed).</summary>
        public static bool ContentsTeleportable(Piece piece, out string blocker)
        {
            blocker = null;
            if (piece == null || piece.m_resources == null) { blocker = "(unknown contents)"; return false; }
            foreach (var r in piece.m_resources)
            {
                if (r.m_resItem == null || r.m_amount <= 0) continue;
                var sd = r.m_resItem.m_itemData.m_shared;
                if (!sd.m_teleportable) { blocker = sd.m_name; return false; }
            }
            return true;
        }

        /// <summary>Re-evaluates one cargo's flag from what is inside it right now. Called right before vanilla checks
        /// Inventory.IsTeleportable, so changes made to material flags by other mods after our registration still count.
        /// The ItemData.m_shared object is shared with the cargo prefab, so this also fixes the tooltip.</summary>
        public static void RefreshTeleportable(ItemDrop.ItemData cargo)
        {
            string blocker;
            var piece = PrefabPieceOf(cargo);
            var ok = ContentsTeleportable(piece, out blocker);
            if (cargo.m_shared.m_teleportable != ok)
                ModLog.Verbose("Cargo '" + cargo.m_dropPrefab.name + "' teleportable " + cargo.m_shared.m_teleportable + " -> " + ok + (ok ? "" : " (contains '" + blocker + "')"));
            cargo.m_shared.m_teleportable = ok;
        }

        // ---------- helpers ----------
        public static bool IsCargo(ItemDrop.ItemData item)
        {
            return item != null && item.m_dropPrefab != null && CargoNames.IsCargo(item.m_dropPrefab.name);
        }

        /// <summary>Piece prefab name of a cargo item; null when the item is not a cargo (callers check IsCargo first).</summary>
        public static string PieceNameOf(ItemDrop.ItemData item)
        {
            return item != null && item.m_dropPrefab != null ? CargoNames.PieceOf(item.m_dropPrefab.name) : null;
        }

        public static GameObject GetCargoPrefab(Piece piece)
        {
            if (piece == null || ObjectDB.instance == null) return null;
            return ObjectDB.instance.GetItemPrefab(PrefabPrefix + Utils.GetPrefabName(piece.gameObject));
        }

        public static ItemDrop.ItemData FindCargoFor(Inventory inv, Piece piece)
        {
            if (inv == null || piece == null) return null;
            var want = PrefabPrefix + Utils.GetPrefabName(piece.gameObject);
            foreach (var it in inv.GetAllItems())
                if (it.m_dropPrefab != null && it.m_dropPrefab.name == want) return it;
            return null;
        }

        private static readonly Dictionary<string, Piece> TablePieces = new Dictionary<string, Piece>();

        /// <summary>The piece a cargo stands for. Looked up in ZNetScene first; pieces that are not networked prefabs
        /// (the terrain tools raise_v2 / paved_road_v2, ...) are not there, so the hammer's piece tables are searched too.
        /// Without that fallback those cargos could never be unpacked.</summary>
        public static Piece PrefabPieceOf(ItemDrop.ItemData cargo)
        {
            var name = PieceNameOf(cargo);
            if (name == null) return null;
            var go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            var piece = go != null ? go.GetComponent<Piece>() : null;
            return piece != null ? piece : PieceFromTables(name);
        }

        public static Piece PieceFromTables(string pieceName)
        {
            Piece known;
            if (TablePieces.TryGetValue(pieceName, out known) && known != null) return known;
            var db = ObjectDB.instance;
            if (db == null || db.m_items == null) return null;
            foreach (var item in db.m_items)
            {
                var drop = item != null ? item.GetComponent<ItemDrop>() : null;
                var table = drop != null ? drop.m_itemData.m_shared.m_buildPieces : null;
                if (table == null || table.m_pieces == null) continue;
                foreach (var pg in table.m_pieces)
                {
                    if (pg == null || Utils.GetPrefabName(pg) != pieceName) continue;
                    var piece = pg.GetComponent<Piece>();
                    if (piece != null) { TablePieces[pieceName] = piece; return piece; }
                }
            }
            return null;
        }
    }
}

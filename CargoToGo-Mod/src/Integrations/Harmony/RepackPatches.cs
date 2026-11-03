using Kydl.Common;
using System;
using HarmonyLib;
using UnityEngine;

namespace CargoToGo
{
    // MIGRATION_DECISIONS A5: every Harmony patch lives in src/Integrations/Harmony. ALT + destroy turns a piece back into a cargo (feature Repack: [Repack] Enable).
    // ---------------------------------------------------------------- ALT + destroy -> back into a cargo
    [HarmonyPatch(typeof(Player), "RemovePiece")]
    internal static class Player_RemovePiece
    {
        public static Piece Pending;
        public static float PendingTime;

        private static bool Prefix(Player __instance)
        {
            Pending = null;
            try
            {
                if (!ModSettings.On || !ModSettings.Repack.Value || __instance != Player.m_localPlayer || !Packer.Alt) return true;
                var piece = __instance.GetHoveringPiece();
                if (piece == null) return true;
                if (!Packer.IsBuiltFromCargo(piece) && !ModSettings.AltDestroyAnyPiece.Value) return true;
                if (CargoFactory.GetCargoPrefab(piece) == null) { ModLog.Warn("ALT-destroy of '" + Utils.GetPrefabName(piece.gameObject) + "': no cargo prefab exists, using normal destroy."); return true; }
                if (!__instance.GetInventory().HaveEmptySlot())
                {
                    __instance.Message(MessageHud.MessageType.Center, Loc.T("cg_no_slot"));
                    ModLog.Info("ALT-destroy of '" + Utils.GetPrefabName(piece.gameObject) + "' blocked for " + Packer.PName(__instance) + ": no free inventory slot.");
                    return false;
                }
                var nv = piece.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) return true;
                nv.ClaimOwnership();            // so the removal runs on this machine
                Pending = piece;
                PendingTime = Time.time;
            }
            catch (Exception e) { ModLog.Error("RemovePiece hook failed" + " | " + e); Pending = null; }
            return true;
        }

        private static void Postfix(bool __result) { if (!__result) Pending = null; }
    }

    [HarmonyPatch(typeof(Piece), nameof(Piece.DropResources))]
    internal static class Piece_DropResources
    {
        private static bool Prefix(Piece __instance)
        {
            try
            {
                var pending = Player_RemovePiece.Pending;
                if (pending == null || !ReferenceEquals(pending, __instance)) return true;
                Player_RemovePiece.Pending = null;
                var pieceName = Utils.GetPrefabName(__instance.gameObject);
                if (Packer.FreeKeyActive(__instance)) { ModLog.Verbose("Repack of '" + pieceName + "' skipped: free-build world key is active, vanilla drops nothing."); return true; }
                if (Time.time - Player_RemovePiece.PendingTime > 1.5f)
                {
                    ModLog.Warn("Repack of '" + pieceName + "' timed out (removal took >1.5s); normal drops used.");
                    return true;
                }

                var p = Player.m_localPlayer;
                var prefab = CargoFactory.GetCargoPrefab(__instance);
                if (p == null || prefab == null) return true;
                var cargo = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
                cargo.m_dropPrefab = prefab;
                cargo.m_stack = 1;
                cargo.m_quality = 1;
                if (!p.GetInventory().AddItem(cargo))
                {
                    ModLog.Warn("Repack of '" + pieceName + "' by " + Packer.PName(p) + ": AddItem failed, normal material drops used.");
                    return true;   // no room: fall back to normal drops
                }
                p.Message(MessageHud.MessageType.Center, Loc.T("cg_repacked") + " " + Localization.instance.Localize(__instance.m_name));
                ModLog.Info("Cargo repacked from piece '" + pieceName + "' by " + Packer.PName(p) + " (normal drops skipped).");
                return false;      // skip the normal material drops
            }
            catch (Exception e) { ModLog.Error("Piece.DropResources hook failed" + " | " + e); return true; }
        }
    }
}

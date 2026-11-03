using Kydl.Common;
using System;
using HarmonyLib;
using UnityEngine;

namespace CargoToGo
{
    // MIGRATION_DECISIONS A5: every Harmony patch lives in src/Integrations/Harmony. Packing and building with a cargo (feature Packing: [PackInWorld] Enable). Decisions live in Packer / CargoFactory / PlaceState.
    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    internal static class Player_TryPlacePiece
    {
        private static bool Prefix(Player __instance, Piece piece, ref bool __result)
        {
            try
            {
                PlaceState.Clear();
                if (__instance != Player.m_localPlayer || piece == null) return true;

                if (ModSettings.On && ModSettings.PackInWorld.Value && Packer.PackChord)
                {
                    Packer.TryPack(__instance, piece);
                    __result = false;           // never place while Ctrl+Alt is held
                    return false;
                }
                if (!ModSettings.On || __instance.NoCostCheat()) return true;
                var cargo = CargoFactory.FindCargoFor(__instance.GetInventory(), piece);
                if (cargo != null)
                {
                    PlaceState.Piece = piece; PlaceState.Cargo = cargo;
                    ModLog.Verbose("Placing '" + Utils.GetPrefabName(piece.gameObject) + "' with a cargo.");
                }
            }
            catch (Exception e) { ModLog.Error("TryPlacePiece hook failed" + " | " + e); }
            return true;
        }
    }

    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    internal static class Player_UpdatePlacement { private static void Postfix() { PlaceState.Clear(); } }

    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
    internal static class Player_ConsumeResources
    {
        private static bool Prefix(Player __instance, Piece.Requirement[] requirements)
        {
            try
            {
                if (PlaceState.Cargo == null || PlaceState.Piece == null) return true;
                if (!ReferenceEquals(requirements, PlaceState.Piece.m_resources)) return true;
                var cargo = PlaceState.Cargo;
                PlaceState.Cargo = null;
                var removed = __instance.GetInventory().RemoveItem(cargo);
                if (removed) ModLog.Info("Cargo used: '" + Utils.GetPrefabName(PlaceState.Piece.gameObject) + "' built from a cargo by " + Packer.PName(__instance) + ".");
                else ModLog.Warn("Cargo for '" + Utils.GetPrefabName(PlaceState.Piece.gameObject) + "' could not be removed from the inventory; falling back to normal material use.");
                return !removed; // consumed the cargo -> skip normal material use
            }
            catch (Exception e) { ModLog.Error("ConsumeResources hook failed" + " | " + e); return true; }
        }
    }

    // Mark the freshly placed piece as "built from cargo".
    [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
    internal static class Piece_SetCreator
    {
        private static void Postfix(Piece __instance)
        {
            try
            {
                if (PlaceState.Cargo == null || PlaceState.Piece == null) return;
                if (Utils.GetPrefabName(__instance.gameObject) != Utils.GetPrefabName(PlaceState.Piece.gameObject)) return;
                var nv = __instance.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid()) { nv.GetZDO().Set(Packer.BuiltFromCargoKey, true); ModLog.Verbose("Tagged placed '" + __instance.name + "' as built from cargo."); }
            }
            catch (Exception e) { ModLog.Error("SetCreator hook failed" + " | " + e); }
        }
    }

    // Having a cargo (or ALT-packing from chests) counts as "can build" for the ghost / placement check.
    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Piece), typeof(Player.RequirementMode) })]
    internal static class Player_HaveRequirements
    {
        private static void Postfix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
        {
            try
            {
                if (__result || !ModSettings.On || mode == Player.RequirementMode.IsKnown || piece == null || __instance != Player.m_localPlayer) return;

                var ok = CargoFactory.FindCargoFor(__instance.GetInventory(), piece) != null;
                if (!ok && ModSettings.PackInWorld.Value && Packer.PackChord && piece == __instance.GetSelectedPiece())
                    ok = Packer.CanPack(__instance, piece);
                if (!ok) return;

                if (piece.m_craftingStation != null &&
                    !CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, __instance.transform.position)) return;
                if (!string.IsNullOrEmpty(piece.m_dlc) && !DLCMan.instance.IsDLCInstalled(piece.m_dlc)) return;
                __result = true;
            }
            catch (Exception e) { ModLog.Error("HaveRequirements hook failed" + " | " + e); }
        }
    }

    // ---------------------------------------------------------------- Ctrl+Alt + left click in the hammer menu
    [HarmonyPatch(typeof(Hud), "OnLeftClickPiece")]
    internal static class Hud_OnLeftClickPiece
    {
        private static void Postfix()
        {
            try
            {
                var p = Player.m_localPlayer;
                if (p == null || !ModSettings.On || !ModSettings.PackInWorld.Value || !Packer.PackChord) return;
                var piece = p.GetSelectedPiece();
                if (piece != null) Packer.TryPack(p, piece);
            }
            catch (Exception e) { ModLog.Error("Hud.OnLeftClickPiece hook failed" + " | " + e); }
        }
    }
}

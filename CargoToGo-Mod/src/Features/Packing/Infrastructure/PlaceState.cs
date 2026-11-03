using Kydl.Common;
using UnityEngine;

namespace CargoToGo
{
    // ---------------------------------------------------------------- building with cargo
    internal static class PlaceState
    {
        public static Piece Piece;
        public static ItemDrop.ItemData Cargo;
        public static void Clear() { Piece = null; Cargo = null; }
    }
}

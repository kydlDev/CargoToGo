using System.Collections.Generic;

namespace CargoToGo
{
    /// <summary>All player-facing texts of CargoToGo (English). Registered with the shared Kydl.Common.Loc; keys start with cg_.</summary>
    internal static class CgText
    {
        public static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            { "cg_cant_pack", "This piece can't be packed into a cargo." },
            { "cg_no_slot", "No free inventory slot for a cargo." },
            { "cg_missing", "Missing materials:" },
            { "cg_packed", "Packed:" },
            { "cg_repacked", "Repacked:" },
            { "cg_unread", "Can't read this cargo's contents; not destroyed." },
            { "cg_unpacked", "Cargo unpacked: materials returned." },
            { "cg_unpacked_drop", "Cargo unpacked (some materials dropped: inventory full)." },
            { "cg_unpacked_free", "Cargo unpacked: no materials returned (free build)." },
            { "cg_cant_leave", "Cargo can't leave your inventory." },
            { "cg_cant_drop", "Cargo can't be dropped. Ctrl+Alt+Right-click unpacks it." },
            { "cg_cant_drop_foreign", "Cargo can't be dropped. Take it into your inventory first." },
            { "cg_use_only_build", "Cargo contents are only usable for building." },
            { "cg_unpack_own", "Cargo can only be unpacked from your own inventory." },
        };
    }
}
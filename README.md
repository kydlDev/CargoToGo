# CargoToGo

Pack the materials for any build piece into a single **cargo** item, carry it in your inventory, and build with it later.

Gather the wood, stone and nails for a house once, turn each piece into one compact item, and take it with you instead of hauling stacks of raw materials around. Cargos go to your gravestone when you die and stay there until you pick them up.

## How it works

| Action | Controls |
|---|---|
| **Pack a cargo** | Select a piece in the hammer menu, then **Ctrl + Alt + Left click**. Works on the piece in the build menu and (by default) in the world. |
| **Build with a cargo** | Select the piece and place it as usual. If you hold a cargo for that piece, it is used up instead of the materials. |
| **Repack a piece** | **Alt + Destroy** (middle mouse by default) on a piece that was built from a cargo. You get the cargo back instead of loose materials. |
| **Unpack a cargo** | **Ctrl + Alt + Right click** the cargo in your inventory. It is destroyed and the materials are returned. |

Materials come from nearby chests first (20 m by default), then from your inventory. A cargo holds exactly what the piece costs and needs one free inventory slot.

## Cargo rules

- A cargo **stays in your inventory**: it can't be dropped, moved to a chest or used for anything except building its piece (your gravestone is the one exception).
- Cargos don't stack and weigh as much as the materials inside.
- A cargo passes through a portal only if every material inside it could.
- Crafting station requirements still apply when you place the piece.

Also included: a **Cargos** tab in the build menu, key hints in build mode, and optional support for **AzuCraftyBoxes**. The FAQ on the mod page answers the how-to and troubleshooting questions (death and gravestones, portals, weight, multiplayer, what to send with a bug report).

## Configuration

The config file is generated on first launch at `BepInEx/config/com.kydldev.CargoToGo.cfg`. Every setting is also on the CargoToGo tab of the KY hub (**F6**, needs the KydlCore mod, which the mod manager installs).

| Setting | Default | Description |
|---|---|---|
| `General` / `Enable Mod` | `true` | Master switch. Off = no packing, repacking, key hints or Cargos tab; cargos you already carry stay safe and can still be unpacked. |
| `General` / `Enable Containers` | `true` | Allow packing materials from nearby chests. |
| `General` / `Enable Pack In World` | `true` | Ctrl + Alt + Left click in the world packs a cargo instead of placing the piece. |
| `General` / `Enable Repack` | `true` | Alt + Destroy turns a piece that was built from a cargo back into a cargo. |
| `General` / `Enable Effects` | `true` | Play build dust, sound and the piece's break effect when packing. |
| `General` / `Enable Key Hints` | `true` | Show the Pack cargo / Repack hints in the build-mode key hint bar. |
| `General` / `Enable Cargo Tab` | `true` | Add the Cargos tab to the build menu. Needs a game restart. |
| `General` / `Weight multiplier` | `1` | Cargo weight = weight of its contents x this value. Needs a game restart. |
| `Containers` / `Range` | `20` | Radius in metres to search for chests when packing. `0` turns CargoToGo's own search off. |
| `Containers` / `UseAzuCraftyBoxes` | `true` | Also pack from containers reached by AzuCraftyBoxes, if installed. Needs `Enable Containers`. |
| `Containers` / `ContainersFirst` | `true` | Take materials from chests before your inventory. |
| `Repack` / `AnyPiece` | `false` | Alt + Destroy returns a cargo even for pieces that were not built from one. |
| `DebugLog` / `Enable` | `false` | Also write DEBUG lines to the BepInEx log. |

## Multiplayer

Client-side: nobody on a server needs CargoToGo. If the server and a player both have it, the versions must match exactly.

## Help

Log lines from this mod are tagged `CargoToGo` in `BepInEx/LogOutput.log`; turn on `DebugLog` and include those lines when you report a problem. See [CHANGELOG.md](CHANGELOG.md) for what changed in each version.

## Credits

Created by **kydldev**.

---

![KydlDev](https://raw.githubusercontent.com/kydlDev/.github/main/branding/KydlDev-banner-960x240.png)

## Get help and stay in touch
Questions, bug reports, ideas or mod requests? Come say hi in the **KydlDev Discord**: **https://discord.gg/tX2CcTNC4W**
Everyone is welcome, and it is the quickest way to reach me. Source code and issues: https://github.com/kydlDev

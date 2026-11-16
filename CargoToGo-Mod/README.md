# CargoToGo

Pack the materials for any build piece into a single **cargo** item, carry it in your inventory, and build with it later.

Gather the wood, stone and nails for a house once, turn each piece into one compact item, and take it with you instead of hauling stacks of raw materials around. The cargo items should be transferred to the gravestone upon death.

## How it works

| Action | Controls |
|---|---|
| **Pack a cargo** | Select a piece in the hammer menu, then **Ctrl + Alt + Left click**. Works on the piece in the build menu and (by default) in the world. |
| **Build with a cargo** | Select the piece and place it as usual. If you hold a cargo for that piece, it is used up instead of the materials. |
| **Repack a piece** | **Alt + Destroy** (middle mouse by default) on a piece that was built from a cargo. You get the cargo back instead of loose materials. |
| **Unpack a cargo** | **Ctrl + Alt + Right click** the cargo in your inventory. It is destroyed and the materials are returned. |

When you pack, materials are taken from nearby chests first (20 m by default) and then from your inventory. A cargo always contains exactly the materials the piece costs, and it needs one free inventory slot.

If the world has free building enabled, packing needs no materials and unpacking returns none, the same as vanilla building and deconstructing.

## Cargo rules

- Cargos are **unstackable** and weigh as much as the materials inside (adjustable in the config).
- A cargo **stays in your inventory**. It can't be dropped, moved to a chest or used for anything except building its piece.
- **Death:** your cargos go to your gravestone together with the rest of your items. See [Death and gravestones](#death-and-gravestones).
- Portals: a cargo can pass through a portal only if every material inside it can. A cargo holding ores or ingots is blocked just like the loose items would be. World portal settings are respected.
- Crafting station requirements for the piece still apply when you place it.

## Death and gravestones

When you die, your cargos are moved to your gravestone together with the rest of your items, and they stay there until you pick them up, even if the gravestone unloads and reloads or you relog.

- **Take all** (or moving a cargo over by hand) puts it back into your inventory.
- While a cargo is in a gravestone it can't be dropped on the ground, moved to another container, unpacked or built from. It is locked until it is back in your inventory.
- Like every other item, cargos are deleted if the world's death rules delete unequipped items.

## The Cargos tab

A fifth tab, **Cargos**, is added to the build menu. It lists only the pieces you currently hold a cargo for and updates live as you pack, build and unpack.

## Key hints

While in build mode, the key hint bar shows **Pack cargo** and **Repack** with your current key bindings (keyboard only).

## AzuCraftyBoxes (optional)

If [AzuCraftyBoxes](https://valheim.hexium.gg/mods/Azumatt/AzuCraftyBoxes) is installed, CargoToGo can also pack from the containers it reaches. This follows AzuCraftyBoxes' own container range, chest toggles, item include/exclude rules, "leave one", drawers and bags. It works even if CargoToGo's own range is set to `0`. AzuCraftyBoxes is not required.

## Configuration

The config file is generated on first launch at `BepInEx/config/com.kydldev.CargoToGo.cfg`. Every setting is also on the CargoToGo tab of the KY hub (**F6**, needs the KydlCore mod, which the mod manager installs).

| Setting | Default | Description |
|---|---|---|
| `General` / `Enable mod` | `true` | Master switch. Off = no packing, repacking, key hints or Cargos tab; cargos you already carry stay safe and can still be unpacked. |
| `General` / `WeightMultiplier` | `1` | Cargo weight = weight of its contents x this value. Needs a game restart. |
| `Containers` / `Enable` | `true` | Allow packing materials from nearby chests. |
| `Containers` / `Range` | `20` | Radius in metres to search for chests when packing. `0` turns CargoToGo's own search off (combine with `UseAzuCraftyBoxes` to rely only on AzuCraftyBoxes' range). |
| `Containers` / `UseAzuCraftyBoxes` | `true` | Also pack from containers reached by AzuCraftyBoxes, if installed. Needs `Containers` / `Enable`. |
| `Containers` / `ContainersFirst` | `true` | Take materials from chests before your inventory. |
| `PackInWorld` / `Enable` | `true` | Ctrl + Alt + Left click in the world packs a cargo instead of placing the piece. |
| `Repack` / `Enable` | `true` | Alt + Destroy turns a piece that was built from a cargo back into a cargo. |
| `Repack` / `AnyPiece` | `false` | Alt + Destroy returns a cargo even for pieces that were not built from one. |
| `Effects` / `Enable` | `true` | Play build dust, sound and the piece's break effect when packing. |
| `KeyHints` / `Enable` | `true` | Show the Pack cargo / Repack hints in the build-mode key hint bar. |
| `CargoTab` / `Enable` | `true` | Add the Cargos tab to the build menu. Needs a game restart. |
| `DebugLog` / `Enable` | `false` | Also write DEBUG lines to the BepInEx log. |

## Multiplayer

CargoToGo is a client-side mod: nobody on a server is required to have it. If the server and a player both have it, the versions must match exactly (a player with a different version is refused). Players without CargoToGo can join, but they cannot take cargos out of a death gravestone. The settings are read from each player's own config.

## Troubleshooting

Log lines from this mod are tagged `CargoToGo` in `BepInEx/LogOutput.log`. If something goes wrong, enable `DebugLog` and include the CargoToGo lines when reporting an issue.

Every death that involves cargos logs a line such as `Death of <name>: 3 cargo(s) moved to the gravestone.` If you lose a cargo on death, that line (or a warning in its place) is the first thing to include in your report.

See [CHANGELOG.md](CHANGELOG.md) for what changed in each version.

## Credits

Created by **kydldev**.

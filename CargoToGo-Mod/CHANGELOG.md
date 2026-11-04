# Changelog

## 2.0.0

- Internal port to the KydlDev spec (new folder layout, KydlCore feature registry, one hub tab, TestLab suite). No change to gameplay or to the config file.
- Checked in game on 2026-11-04 (clean start and quit with all mods loaded, no errors from this mod).
- Network: CargoToGo stays a client-side mod (nobody is required to have it), but a server that has it now refuses clients that have a different version (`VersionCheckOnly`, patch-strict). Players without CargoToGo can still join; they cannot take cargos out of a death gravestone.
- Breaking: CargoToGo now needs the KydlCore mod (hard dependency; the mod manager installs it).
- All settings are now also on the **CargoToGo > Settings** tab of the KY hub (F6), with the same controls as the config file.
- Log lines now use the shared KydlDev format (`VerboseLogging` still switches the detail lines on).

## 1.1.0

### Fixed
- **Cargos were destroyed when you died.** They now travel to your gravestone together with the rest of your items and are still there when the gravestone reloads (walking back to it after respawning, relogging, another player loading it).

### Changed
- **Cargos in a gravestone are locked.** They can be recovered into your own inventory with **Take all** (or by moving them over). While in a gravestone they can't be dropped on the ground, moved to another container, unpacked, used or built from.
- Clearer messages when you try to drop a cargo you dragged out of a gravestone, or to unpack a cargo that isn't in your own inventory.

### Added
- Each death that involves cargos writes a line to the BepInEx log (`Death of <name>: N cargo(s) moved to the gravestone.`), with a warning if the numbers don't add up. Useful when reporting an issue.

### Notes
- World modifiers that delete items on death still delete cargos, like any other item.

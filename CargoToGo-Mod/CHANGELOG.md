# Changelog

## 2.0.1

- Changed: needs KydlCore 1.1.1 or newer (the KY hub shows every settings category of the mod as its own tab).
- Changed: all the **Enable** switches are now together in `[General]` and have new names: `Enable Mod` (was `EnableAll`) and `Enable Containers`, `Enable Pack In World`, `Enable Repack`, `Enable Effects`, `Enable Key Hints`, `Enable Cargo Tab` (each was `Enable` in its own section). The values you had set are taken over; the old entries are removed from your config file.
- Changed: `Enable Mod` is now the first setting of `[General]`, in the F1 menu and in the F6 hub (your value is taken over).
- Changed: `[General] WeightMultiplier` is now `[General] Weight multiplier` and is the last setting of the list (the value is taken over).

### Testing
- Unit tests: 27 passed. Integration tests: 38 passed.
- Property-based tests (FsCheck, included in the unit tests above): 8 property checks with generated inputs.
- Coverage of the testable code: 100%. Mutation testing (Stryker): 100% of the mutants are caught by the tests.
- Performance benchmarks (BenchmarkDotNet): 4.
- Static analysis (SonarQube): no Medium or High issues.
- In game with DrakesTestLab: the full suite of 6 tests per run, run 3 times in a row on the release build, all green (the release requirement).

## 2.0.0

- Tested in game with the full TestLab suite (6 tests): 3 green runs in a row on 2026-11-05, plus the ecosystem run with all 12 KydlDev mods loaded together.
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

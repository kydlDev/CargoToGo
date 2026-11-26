I lost a cargo when I died, what should I send you?

Turn on `[DebugLog] Enable`, then look in `BepInEx/LogOutput.log` for the lines tagged `CargoToGo`. Every death with cargos writes a line like `Death of <name>: 3 cargo(s) moved to the gravestone.` (or a warning in its place): send that line and the ones around it, with a bug report on the KydlDev Discord or GitHub. Also check the world's death rules: if they delete unequipped items, cargos are deleted like every other item.

# Contributing to Pin a Build Cost

## Report a bug

Open a [GitHub issue](https://github.com/GKJ-Super-Fantasy-Kingdom/pin-a-build-cost/issues) with the mod, game, and BepInEx versions, your kingdom and day, and the steps to reproduce the problem. Include the relevant log excerpt. For interface or input issues, include the UI scale and whether you used a controller or keyboard and mouse.

## Submit a change

Fork the repository, create a branch, and follow the build and packaging instructions below. Use the assemblies from your own game installation and BepInEx profile as build references. Keep game binaries, save files, and local build output out of commits.

Describe the player-visible change in your pull request. List the checks you ran and, where relevant, which controller actions and UI scales you tested.

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) for each commit. Start the subject with a type, an optional scope, and a short description. Use an exclamation mark before the colon for a breaking change.

Examples include `feat(ui): show controller prompts`, `fix(save): restore the current day`, and `chore: update build tooling`.

Accepted types are `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`. Use the same format for pull request titles so GitHub can use the title as the merge or squash commit subject.


## Integrating Purchase Cards

Construction pins use the actual prefab on each native card. Buildings added to that list can be pinned without registering their names or resource types. Standard upgrade pins read the price for the chosen building, upgrade and tier. If a mod adds a second upgrade at the same tier, each choice keeps its own price when you revisit the menu. Dedicated hooks also cover capacity purchases, evolution outposts and supported world-site repair, rebuild and unlock dialogs.

Mods that adjust the game's price, available-stock or construction-eligibility methods are reflected in the pin. The witch's bonfire unlock uses the star quote from the most recently opened dialog. Mods with their own menus, private inventories or purchase-only discounts can optionally use `PinABuildCost.PinCostApi.Attach` when drawing the card. Supply a stable owner and a mod-qualified operation ID, a current price getter and a completion check. Optional stock and eligibility getters let the pin use the same rules as the purchase.

Evolution pins follow the unit and the selected Outpost plot. Using that plot for another unit retires its pin. Removing a starting-resource upgrade tier also retires its pin. When filtering or recycling an individual card or its purchase control, call `Attach` again when drawing its next offer. A custom `readStock` wallet must cover the combined price even during global free building. Integrations whose purchases waive that wallet can pass `honorFreeBuilding: true` to the extended overload. Additional eligibility rules still apply.

The pin uses the game's button artwork and focus highlight. When another action occupies the space beside a purchase, the pin uses its own row. The HUD uses the resource bar's frame and font, lets pointer input pass through, and leaves room for the building panel, header controls and Daybook ledger. If neither side has enough space, the readout hides until room opens up. The target stays pinned.

## Local import

1. Coordinate a free test session if others are using the game. In Thunderstore Mod Manager, select **Super Fantasy Kingdom** and a disposable test profile.
2. Use **Settings → Import local mod**, then choose `dist/PinABuildCost-0.2.3.zip`. Confirm the mod and both BepInEx dependencies are enabled. Enable Daybook in the same profile for the HUD check.
3. Use **Start modded** and open a building plot. Each offered building should have a **Pin Cost** action. Look for `Pin a Build Cost 0.2.3 ready.` in the BepInEx log.

## Controller playtest

1. Using only a controller, open a plot and navigate to an unaffordable building. Verify the pin prompt and focus highlight. Activate the pin once. The build menu stays open and no materials are spent. Check normal navigation, tab switching, confirm and back.
2. Close and reopen the list. Compare the HUD with the card cost minus available stock. Pin a different building, then unpin it. Pin again and build something unrelated. The original target should remain.
3. Meet one material requirement and check that its row stays in place with a green check. Meet the rest to reach **Ready**, then spend one material. Only its box should reopen. Complete the pinned building and check the panel disappears.
4. Pin an upgrade. Check the building name, upgrade name and target level. Upgrade another instance first, then the pinned instance. Only the latter should clear the pin. Repeat on a small building with a scaling price when available.
5. Switch to keyboard and mouse. Use Tab on a focused or hovered card, then click its pin row. Switch back to the controller and check the prompt updates. Try ordinary confirm on the separate pin row.
   For both input schemes, focus a Tavern worker control and pin **Add chamber**. Open **Repair Church**, focus **Cancel**, replace the pin and then unpin it. Each press should change the pin once, retain focus and leave the purchase untouched. Open the Outpost bonus list and move between its bonus cards.
6. Start in **Windowed Full Screen**. At UI scales **1** and **1.5**, pin a long target name with several missing materials. Expand the Daybook ledger and food breakdown together. Check that the counts, pin and ledger remain readable. With Production Bottlenecks enabled, open a blocked building and check that its status and the native Upgrade control stay in place.

## Build and offline checks

The Release project references the installed game's managed assemblies and BepInEx core in an existing profile. The checks use 64-bit Python and the game's embedded Mono runtime in a separate process.

```powershell
dotnet build tests/PinChecks.csproj -c Release
python tests/run.py
python package.py
```

The default paths use the Steam install on D: and the local Daybook Dev profile. For other paths, pass `-p:GameDir="..."` and `-p:ProfileDir="..."` to the build and `--game "..." --profile "..."` to the check runner. Offline checks cover changing prices and stock, eligibility, pin identity and completion, and the native checkpoint writer on 0.8.069 and 0.8.070. Embedded Mono can report unresolved Unity internal calls outside a scene. A successful check run exits with code 0. The controller playtest above requires a running game.

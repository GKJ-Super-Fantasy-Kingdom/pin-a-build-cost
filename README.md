# Pin a Build Cost

Pin a building in the construction list or the next upgrade on an existing building. A panel beside the material bar shows how many more of each material you need. **Ready** means you have the materials. Other kingdom or building prerequisites still apply.

## Controls

- On a focused building or upgrade, use the pin prompt. The default is **Y** on Xbox, **Triangle** on PlayStation, or **Tab** on keyboard. The prompt follows the game's UI/Tab binding.
- You can also click **Pin cost**, or navigate to that row and use the normal UI confirm action. The row stays available when the game disables the purchase button. Pinning keeps the menu open.
- **Replace pin** switches to another target. **Unpin** removes the current target. A construction pin clears when that building design is built at any plot. An upgrade pin follows the chosen building instance and level. Leaving the menu or reaching **Ready** keeps the pin.
- A pin lasts for the current play session. Loading a run clears it. Demolishing a building clears its upgrade pin.

## Local import

1. Coordinate a free test session if others are using the game. In Thunderstore Mod Manager, select **Super Fantasy Kingdom** and a disposable test profile.
2. Use **Settings → Import local mod**, then choose `dist/PinABuildCost-0.1.0.zip`. Confirm the mod and both BepInEx dependencies are enabled. Enable Daybook in the same profile for the HUD check.
3. Use **Start modded** and open a building plot. Each offered building should have a **Pin cost** action. Look for `Pin a Build Cost 0.1.0 ready.` in the BepInEx log.

## Controller playtest

1. Using only a controller, open a plot and navigate to an unaffordable building. Verify the pin prompt and focus highlight. Activate the pin once. The build menu stays open and no materials are spent. Check normal navigation, tab switching, confirm and back.
2. Close and reopen the list. Compare the HUD with the card cost minus available stock. Pin a different building, then unpin it. Pin again and build something unrelated. The original target should remain.
3. Gain enough resources to reach **Ready**, then spend some. The shortage should return. Complete the pinned building and check the panel disappears.
4. Pin an upgrade. Check the building name, upgrade name and target level. Upgrade another instance first, then the pinned instance. Only the latter should clear the pin. Repeat on a small building with a scaling price when available.
5. Switch to keyboard and mouse. Use Tab on a focused or hovered card, then click its pin row. Switch back to the controller and check the prompt updates. Try ordinary confirm on the separate pin row.
6. At UI scales **1** and **1.5**, check a long target name and several missing materials. With Daybook enabled, check the compact strip, the expanded Alt ledger and the controller worker view. Verify the resource icons, counts, ledger and pin panel remain readable.

## Build and offline checks

The Release project references the installed game's managed assemblies and BepInEx core in an existing profile. The checks use 64-bit Python and the game's embedded Mono runtime in a separate process.

```powershell
dotnet build tests/PinChecks.csproj -c Release
python tests/run.py
python package.py
```

The default paths use the Steam install on D: and the local Daybook Dev profile. For other paths, pass `-p:GameDir="..."` and `-p:ProfileDir="..."` to the build and `--game "..." --profile "..."` to the check runner. Offline checks cover pin transitions, shortages, upgrade pricing and installing the shipped Harmony hooks against game 0.8.067. While compiling Unity calls outside a scene, embedded Mono can report unresolved Unity internal calls. A successful check run prints 16 PASS lines and exits with code 0. The controller playtest above requires a running game.

## Contributing and license

Bug reports and pull requests are welcome. See the [contribution guide](CONTRIBUTING.md) for local testing and reporting instructions.

The original mod code is available under the [MIT License](LICENSE). Super Fantasy Kingdom, its game assets, and third-party dependencies retain their respective licenses and ownership.

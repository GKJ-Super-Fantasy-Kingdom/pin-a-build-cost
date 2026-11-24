# Pin a Build Cost 0.1.0 compatibility audit

Tested on Super Fantasy Kingdom 0.8.067 with BepInEx 5.4.23.5. The live runs used a copy of the reserved `Test - Mods` profile, a separate game data directory and a copy of a day-12 Human kingdom. The test driver operated the real game, building cards and Unity input system. Only the mod DLL ships in the package.

## Released mods in the live game

| Package and setup | Comparison and outcome |
| --- | --- |
| ownlyme/LoneBonfire 1.1.1, stock package | Started with a funded native Bonfire card, the kingdom-level requirement satisfied and enough bonfire unlocks. After building a separate prefab of the same bonfire type, the original pin stayed **Ready** standalone. With LoneBonfire, the city rejected another bonfire and the pin changed to **Materials ready · requirements pending**. The material check still passed in both runs. |
| sfkbutter/DemolishAll 1.0.3, stock package | Its Harmony reverse patch could not find the two-argument `UIOverlayMessage.SetAccept(ResourceAmount, UnityAction)`. The game provides a three-argument overload. The error occurred before Pin a Build Cost loaded. With the Tavern's Add chamber upgrade pinned, the extra demolition button was absent. External demolition and its focus behavior are blocked on this build. |
| ILM/CheatMenu 1.0.3, Coins multiplier 2, remaining settings at defaults | Separate two-mod run compared with standalone. A fixture priced the native Hunter card at 10 coins. From an empty purse, granting 2 coins left the pin 8 short standalone. With the released CheatMenu DLL the same grant credited 4 coins and the pin read 6 short. |
| GKJ/ProductionBottlenecks 0.1.2 and GKJ/Daybook 0.4.0, Daybook `ShowOverlay = true` | Both ran with the two external packages above. With a Tavern board shortage induced, its **Needs Boards** row and upgrade pin action were visible and separate. The pinned HUD cleared the building panel, compact Daybook and expanded daily ledger at both 1.0× and 1.5× in a 1920 × 1080 window. |

Both standalone and combined runs completed the native Lumberyard demolition confirmation. The world became interactive, the upgrade pin cleared and reopening construction gave the controller an interactable card. Upgrading a different instance kept the pin. Raising the pinned instance to its target tier cleared it.

## Versioned package inspection

- DemolishAll injects its demolition action into building overlays and routes its banish confirmation through a reverse-patched game method. The signature in the 1.0.3 binary explains the startup error above. [DemolishAll](https://thunderstore.io/c/super-fantasy-kingdom/p/sfkbutter/DemolishAll/)
- LoneBonfire learns the bonfire's building type on initialization. Its prefix on `CityManager.CanBuild` rejects that type if the city already has one. The pin queries this same method on each refresh. [LoneBonfire](https://thunderstore.io/c/super-fantasy-kingdom/p/ownlyme/LoneBonfire/)
- CheatMenu 1.0.3 modifies positive coin awards in `ResourceManager.AddResource` and includes configurable production-yield and speed patches. Its coin multiplier defaults to 1. [CheatMenu](https://thunderstore.io/c/super-fantasy-kingdom/p/ILM/CheatMenu/)

## Native and fixture coverage

- The 0.8.067 run covered 12 large construction cards, 6 small cards, 1 port and a stone rebuild confirmation. It also found 13 cards on the large Outpost bonus list. The current build excludes that list from pinning. The special, trap and world-special lists had no offers in this save.
- An injected building appeared in the ordinary construction list. Replacing its prefab cleared the old pin. An injected alternative upgrade on the same building and tier reproduced an identity collision. After the fix, replacing the pin selected its distinct price, reopening the panel retained it, and removing the upgrade cleared the pin.
- A price fixture changed a pinned Hunter from 12 boards to 7 stone and 3 boards while the menu was closed. With 2 of each available, the HUD changed from 10 boards short to 5 stone and 1 board short. Restocking and withdrawing stock updated it again.
- A virtual keyboard and gamepad exercised the game's Tab binding and controller north button in the running game. Both pinned, replaced and removed a target. The controller retained focus on the pin row after removal.
- The offline suite passed 34 checks, including a patched virtual upgrade getter, runtime inventory and price changes, variant identity, and installation of the Harmony hooks against the installed game assembly.

## UI layout verification

The isolated game reported **1920 × 1080 FullScreenWindow** and **1280 × 720 Windowed** in two additional runs. At both 1× and 1.5×, the pin used the game's button sprite, selected-state art and resource header font. Native Upgrade and Cancel had independent pointer targets. Temporarily adding a third action to the upgrade strip moved the pin into its own row, and removing that action returned the pin to the strip. Hiding the inline pin left the native Upgrade button and Production Bottlenecks status row at the same coordinates.

With Daybook's expanded ledger open, a long building name and three shortages fit inside the viewport. The pin cleared the ledger, food dropdown, spell control and guardian bars. Its frame and text passed pointer input through. Both display runs passed all 46 live assertions. The 720p window at 1.5× had a taller Tavern panel than the viewport, with the Production Bottlenecks status below the bottom edge even when the inline pin control was hidden.

### Checklist playtest

The material checklist received a further 1920 × 1080 borderless run with Daybook and Production Bottlenecks together at both UI scales. All 54 live assertions passed. A three-material fixture showed a checked Boards row beside two shortages. Restocking checked the same rows, spending reopened only Boards, and changing the live price removed obsolete rows. All checks stayed visible while a separate eligibility rule blocked purchase. The compact and expanded ledgers, native Upgrade action and production status kept their spacing and pointer targets. Keyboard and virtual controller pinning, replacement and removal also passed.

## Panel shortcuts and Outpost bonuses on 0.8.068

The retest used all ten local mods from **GKJ - Controller Test**, including Daybook 0.4.0 and Production Bottlenecks 0.1.3. Kingdom Charters 0.1.0 reported **Forester** in a disposable copy of the day-nine Human save. The game ran at 1920 × 1080 in borderless fullscreen, UI scale 1.5. The saved kingdom already had a church, so the probe used the game's deposit spawner to place its damaged-church prefab and opened its native three-board repair dialog.

| Focus before pressing Tab or controller north | Observed result with each input scheme |
| --- | --- |
| Tavern worker-minus button | Pin, Replace and Unpin each changed the Add chamber pin once. The upgrade level, worker setting and focus stayed in place. |
| Repair Church's Cancel button | Pin, Replace and Unpin each changed the Church pin once. Cancel kept focus and the dialog stayed open. |
| Farm or Hunter construction card | Pin selected Farm, Replace switched to Hunter and Unpin cleared Hunter. The purchase card retained focus. |
| A tab control in a list with multiple offers | No row advertised a shortcut. Pressing it left the pin unchanged. |
| Outpost bonus list | All 13 bonus cards kept their native controls. A D-pad press moved between bonus cards and pressing the pin shortcut left the pin unchanged. The world-special list was empty and also had no pin controls. |

All 56 assertions passed using virtual devices through Unity's input system. Each tested shortcut press was held across seven frames before release. The offline suite passed 36 checks. Physical controller operation and custom bindings remain hands-on checks.

## Repair Church action row

The Repair Church action row received a further 1920 × 1080 borderless retest on 0.8.068 with all ten local mods. The first layout revision fit at rest but dropped the button when Cancel enlarged on controller focus. The final revision lets the native dialog reserve the full **Replace Pin** label, binding glyph and focus-animation clearance before positioning the controls.

All 244 assertions in the final shortcut and layout run passed. Each combination of **Pin Cost**, **Unpin** and **Replace Pin**, 1× and 1.5× UI scale, and virtual keyboard and gamepad was checked with focus on the price, the pin and Cancel. The full text and binding stayed visible on one line, all three pointer targets were distinct, and the button stayed inline throughout the native focus animation. A shortcut press with Cancel selected changed the pin once and retained focus. The same run rechecked Tavern, ordinary construction cards and the Outpost bonus list. The offline suite passed 37 checks, including preservation of mixed-case supplied names.

A second live run passed 47 checks. Farm and Hunter construction cards showed all three title-cased pin actions with both input schemes at both scales. A test offer exercised the **Bonfire Unlock** title and the **Materials Ready · Requirements Pending** and **Cost Unavailable** statuses in the real HUD. Its price and eligibility were supplied by the test driver. The title and status text stayed inside the visible frame at both scales.

## November 24 compatibility fixes on 0.8.068

The audit build before Castle support had DLL SHA-256 `26741b3668d79b4229c069f54a1f98ab527716c7c107e29f984c49637bb49b44`. The standalone offline run passed 81 assertions. The optional paired run passed those assertions and three Bonfire comparisons. Both runs used the installed game's Mono runtime and managed assemblies. The scene fixture supplies object visibility, destruction and frame numbers.

| Case | Result in the final build |
| --- | --- |
| Evolution and starting-resource sites | The same unit on two plots produced distinct offers. Deactivating or destroying the selected plot retired its offer, as did evolving the unit. A retained world tier adopted its new price. Completing the tier or removing it from a shortened or null cost table retired it. |
| Filtered cards and input | Closing and reopening a parent kept its binding. Individually hiding, recycling or destroying either the card or purchase control retired its sibling pin button. The stale button could no longer change the pin. A normal button activation changed it once per frame. Disabled-map hold and release bindings suppressed the shortcut. Enabled actions kept native timing. These were lifecycle and binding-policy fixtures. |
| Price and wallet compatibility | With seven boards available, the game's real affordability method accepted separate entries of three and seven. The pin showed Ready with a combined shortage of three. A private wallet respected its own stock under global free building. Opting in waived the cost and kept a separate purchase gate effective. Both public API overloads were exercised. |
| ownlyme/LoneBonfire 1.1.1 | In a modeled city with an existing Bonfire, the funded repeatable offer was Ready standalone. Loading the released DLL's eligibility patch changed the same pin to Materials Ready · Requirements Pending. Removing the city's Bonfire restored Ready. This exercised the released patch and the native eligibility methods offline. |

To repeat the paired run, add `--lone-bonfire "path/to/LoneBonfire.dll"` to `python tests/run.py` after the Release build. No external mod is required by the plugin. The ZIP contains the tested DLL, manifest, decoded 256 × 256 icon, README, contribution guide and license. Local links in both packaged guides were checked.

This build received offline verification while the shared live slot was reserved. The earlier Daybook, Production Bottlenecks, keyboard and virtual-controller gameplay results above apply to the earlier DLL. The controller test profile and saves were untouched.

## Castle release verification

The packaged 0.1.0 DLL has SHA-256 `f9ad2efe8cbca9b926ccc78db6019f4b8737b09f85c5de6fbcc0c9f79b9fd5a1`. A fresh Release build matched the DLL used for the Castle screenshots byte for byte. The release suite passed 82 standalone offline checks, including installation of the Castle hook against the game assembly, and 85 with the LoneBonfire 1.1.1 purchase patch.

The Castle build ran in an isolated Human kingdom on game 0.8.068. At 1× and 1.5× the Castle offered separate pin buttons for Increase storage and Wall, even with both purchases unaffordable. Reopening the panel retained the Wall pin. In a funded purchase fixture, buying storage preserved the Wall pin and buying the Wall cleared it. Keyboard input and a virtual gamepad exercised the shortcuts.

The README screenshots are from a further in-game run with Daybook 0.4.0 enabled. The driver pinned Increase storage once, then focused the Wall’s Replace Pin button at each scale. The checklist continued to show two ingots missing. Both frames were captured at 1920 × 1080 in borderless fullscreen.

## Remaining coverage

Physical controller prompts, remapping and navigation still need a hands-on pass. Other kingdoms, a released building or upgrade content pack, and each special purchase need their own playtests. Mods with private purchase logic can supply their current price, inventory, eligibility and completion through `PinCostApi.Attach`. The witch's star quote refreshes when its dialog reopens.

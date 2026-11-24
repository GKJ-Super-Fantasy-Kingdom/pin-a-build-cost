# Pin a Build Cost

Pin a building or upgrade to keep its material checklist beside the resource bar while you play. Close the building panel and you can still see what you need before you return to buy it.

## Features

- Track one purchase at a time, including an unaffordable upgrade such as the Castle’s **Increase storage** or **Wall**.
- Each material gets a green check when you have enough. Spend some of it and the row shows how much you still need. The checklist refreshes every half second.
- **Ready** means the purchase check passes. **Materials Ready · Requirements Pending** means you have the materials but another rule still blocks the purchase.
- Completing the pinned build or upgrade clears the checklist. Demolishing a building also clears its upgrade pin.

## How to Use

1. Open a construction card or supported upgrade and choose **Pin Cost**. Click it with the mouse, or focus it with a controller and use the game’s confirm button.
2. For the shortcut, focus the offer and press **Tab** on keyboard, **Y** on Xbox, or **Triangle** on PlayStation by default. A panel with one offer also accepts the shortcut from another control, such as **Cancel** in Repair Church.
3. Choose **Replace Pin** on another offer to switch targets. Moving focus there keeps your original pin until you activate the button. Choose **Unpin** on the tracked offer to clear it. Loading a run also clears the pin.

## Screenshots

Both captures show version 0.1.0 in-game at 1920 × 1080 in borderless fullscreen with simulated controller input.

![Castle at 1× with Increase storage pinned and an unchecked row for two missing ingots](https://raw.githubusercontent.com/GKJ-Super-Fantasy-Kingdom/pin-a-build-cost/main/docs/screenshots/0.1.0/castle-storage-wall-focus-1x.png)

**1× UI scale.** The checklist shows **Ingots · 2 short**, and **Unpin** beneath Increase storage identifies the tracked upgrade.

![Castle at 1.5× with controller focus on the Wall’s Replace Pin button and the Storage checklist still visible](https://raw.githubusercontent.com/GKJ-Super-Fantasy-Kingdom/pin-a-build-cost/main/docs/screenshots/0.1.0/castle-storage-wall-focus-1.5x.png)

**1.5× UI scale.** The white outline shows focus on the Wall’s **Replace Pin** button. The checklist above still tracks Increase storage.

## Compatibility

- Tested on Super Fantasy Kingdom 0.8.068. The checklist follows changes to the game’s prices, available stock and purchase rules. Mods with private inventories or their own purchase menus can add pin support through the optional integration API.
- With [Daybook](https://thunderstore.io/c/super-fantasy-kingdom/p/GKJ/Daybook/) 0.4.0 and [Production Bottlenecks](https://thunderstore.io/c/super-fantasy-kingdom/p/GKJ/ProductionBottlenecks/) 0.1.2, the pin left room for the expanded ledger and the building’s production status at both 1× and 1.5×.
- With [LoneBonfire](https://thunderstore.io/c/super-fantasy-kingdom/p/ownlyme/LoneBonfire/) 1.1.1, a second Bonfire shows **Materials Ready · Requirements Pending** when you have enough materials but already own a Bonfire.
- With [CheatMenu](https://thunderstore.io/c/super-fantasy-kingdom/p/ILM/CheatMenu/) 1.0.3, a doubled coin award reduced the pinned shortage by the full amount received.
- On game 0.8.067, [DemolishAll](https://thunderstore.io/c/super-fantasy-kingdom/p/sfkbutter/DemolishAll/) 1.0.3 failed to add its demolition button, so that action could not be tested. The game’s own demolition action cleared the demolished building’s upgrade pin.
- The shortcut follows the game’s UI/Tab binding. If a panel disables that action and your binding uses a custom hold or release, click the pin button or use focused controller confirm.

## Installation

1. Select **Super Fantasy Kingdom** and your profile in Thunderstore Mod Manager. Use **Install with App** on this listing.
2. Allow the manager to install [BepInExPack](https://thunderstore.io/c/super-fantasy-kingdom/p/BepInEx/BepInExPack/) and [HideManagerGameObject_Config](https://thunderstore.io/c/super-fantasy-kingdom/p/BepInEx/HideManagerGameObject_Config/), then launch the modded game.

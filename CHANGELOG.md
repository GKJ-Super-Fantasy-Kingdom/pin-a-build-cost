# Changelog

## 0.2.1

- Checklist rows use the purchase card's material icons when space allows. Checked materials and Ready use green, while missing quantities and pending requirements use amber.
- The checklist keeps a gutter around the native building panel's title ribbon, including its projecting ends.
- Opening a building panel gives an available native purchase its normal controller focus. The pin button provides an initial selection when the game has none.

## 0.2.0 (local test build)

- Save the selected purchase with the kingdom checkpoint. Continuing or replaying a dawn restores its building and upgrade, reads the live price and clears retired or completed targets.
- The Castle remembers whether you pinned Storage or Wall. Upgrades of identical buildings are matched by their saved map positions.

## 0.1.0

- Pin one construction or upgrade offer and track its materials beside the resource bar. The checklist updates when costs or stock change.
- Pin **Increase storage** or **Wall** in the Castle, including while the purchase is unaffordable. Buying the tracked upgrade clears its pin.
- Pin, replace and unpin with mouse, keyboard or controller. Native input prompts follow the focused offer.
- Mods can supply their own price, purchase rules and inventory through the optional `PinCostApi.Attach` API.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PinABuildCost
{
    [HarmonyPatch(typeof(UIOverlayBuildingSelection), "AddBuildingButtons")]
    internal static class BuildCards
    {
        // At this call the game has drawn the card and still holds its exact prefab,
        // even if it is about to disable the purchase button for insufficient stock.
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var draw = AccessTools.Method(typeof(UIOverlayBuildingSelection), "DisplayCosts");
            var canBuild = AccessTools.Method(typeof(BuildingCity), nameof(BuildingCity.CanBeBuilt));
            int drawAt = code.FindIndex(i => i.Calls(draw));
            int checkAt = code.FindIndex(i => i.Calls(canBuild));
            if (drawAt < 2 || checkAt <= drawAt || !LoadsLocal(code[drawAt - 2].opcode) ||
                code.Count(i => i.Calls(draw)) != 1 || code.Count(i => i.Calls(canBuild)) != 1)
                return PinPlugin.UnsupportedLayout(code, "construction");

            code.InsertRange(checkAt, new[] {
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(code[drawAt - 2].opcode, code[drawAt - 2].operand),
                new CodeInstruction(OpCodes.Ldarg_2),
                CodeInstruction.Call(typeof(BuildCards), nameof(Attach))
            });
            return code;
        }

        internal static bool LoadsLocal(OpCode op) => op == OpCodes.Ldloc || op == OpCodes.Ldloc_S ||
            op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1 || op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3;

        internal static bool IsBonusList(BuildingSize size) =>
            size == BuildingSize.WorldBig || size == BuildingSize.WorldSpecial;

        private static void Attach(BuildingCity prefab, GameObject card, BuildingSize size)
        {
            if (IsBonusList(size)) return;
            PinPlugin.TryAttach(() => {
                bool InRoster() => RaceManager.Instance.GetBuildings(prefab.GetBuildingSize()).Contains(prefab.gameObject);
                bool trackedRoster = InRoster();
                var target = new PinTarget(prefab, prefab.GetTitle(), () => prefab.GetCost(),
                    permitted: _ => CanOffer(prefab), complete: () => trackedRoster && !InRoster());
                PinRow.Create(card, card.GetComponent<Button>(), target);
            });
        }

        internal static bool CanOffer(BuildingCity prefab)
        {
            var type = prefab.GetBuildingType();
            var city = CityManager.Instance;
            // Both the roster filter and the purchase check are mod extension points.
            return city.CanBuild(type) &&
                (type != BuildingType.WorldDifficulty || WorldManager.Instance.CanBuildBonfires() > 0) &&
                (RaceManager.Instance.GetCanBeBuiltMultipleTimes().Contains(type) || city.GetBuilding(type) == null) &&
                prefab.CanBeBuilt();
        }
    }

    [HarmonyPatch(typeof(UIBuildingOverlayElementUpgrade), nameof(UIBuildingOverlayElementUpgrade.Draw),
        new Type[] { typeof(Building) })]
    internal static class UpgradeCards
    {
        private static readonly Func<UIBuildingOverlayElementUpgrade, Building, ResourceAmount[]> Price =
            AccessTools.MethodDelegate<Func<UIBuildingOverlayElementUpgrade, Building, ResourceAmount[]>>(
                AccessTools.Method(typeof(UIBuildingOverlayElementUpgrade), "GetCosts"));

        private static void Prefix(out int __state)
        {
            __state = OverlayManager.Instance.GetOverlaySelection().childCount;
        }

        private static void Postfix(UIBuildingOverlayElementUpgrade __instance, Building building,
            int ___amountOfUpgrades, int __state)
        {
            if (building.GetUpgradeAmount() >= ___amountOfUpgrades) return;
            var list = OverlayManager.Instance.GetOverlaySelection();
            for (int i = __state; i < list.childCount; i++)
            {
                var card = list.GetChild(i);
                var purchase = card.Find("Upgrade/Container/Button")?.GetComponent<Button>();
                var title = card.Find("Upgrade/Title")?.GetComponent<TMP_Text>();
                if (purchase == null || title == null) continue;
                PinPlugin.TryAttach(() => {
                    var target = new PinTarget(building, building.GetTitle() + " · " + title.text,
                        () => Price(__instance, building), building.GetUpgradeAmount() + 1,
                        cost => building.CanBeUpgraded() && ResourceManager.Instance.CanBeBuilt(cost),
                        complete: TrackRemoval(building, __instance), definition: __instance);
                    var row = PinRow.Create(card.gameObject, purchase, target,
                        actionStrip: purchase.transform.parent as RectTransform, rightButton: purchase.transform as RectTransform);
                    GamepadManager.Instance.SetGameObjectToSelect(row.gameObject);
                });
                break;
            }
        }

        internal static Func<bool> TrackRemoval(Building building, UIBuildingOverlayElement element)
        {
            var overlay = building.GetComponent<UIBuildingOverlay>();
            if (overlay == null || overlay.buttons?.Contains(element) != true) return null;
            return () => overlay == null || overlay.buttons?.Contains(element) != true;
        }
    }

    [HarmonyPatch(typeof(UITabController), "ChangeTab")]
    internal static class TabFocus
    {
        private static void Postfix(UITabController __instance, int tab)
        {
            var manager = GamepadManager.Instance;
            var content = __instance.transform.Find("Tab" + tab + "/ScrollArea/Viewport/Content");
            // GenerateSelection initializes the tab before the outer overlay becomes visible.
            if (content == null || manager == null || content.GetComponentInChildren<PinRow>(true) == null) return;
            var current = manager.GetGameObjectToSelect();
            if (current != null && current.activeSelf && current.transform.parent == content &&
                current.GetComponent<Button>()?.IsInteractable() == true) return;
            foreach (var button in content.GetComponentsInChildren<Button>(true))
            {
                if (button.transform.parent != content || !button.gameObject.activeSelf || !button.IsInteractable()) continue;
                manager.SetGameObjectToSelect(button.gameObject, true);
                break;
            }
        }
    }

    [HarmonyPatch(typeof(CityManager), nameof(CityManager.Build),
        new Type[] { typeof(BuildingCity), typeof(Vector2), typeof(bool) })]
    internal static class BuildCompletion
    {
        private static void Postfix(BuildingCity buildingPrototype, bool fromSave, BuildingCity __result)
        {
            if (!fromSave && __result != null) PinPlugin.Instance?.Built(buildingPrototype);
        }
    }
}

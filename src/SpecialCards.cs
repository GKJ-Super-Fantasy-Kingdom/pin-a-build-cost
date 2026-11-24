using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PinABuildCost
{
    // Castle storage and fortifications are one-time city events, each with its own purchase row.
    [HarmonyPatch(typeof(UIBuildingOverlayElementCityEvent), nameof(UIBuildingOverlayElementCityEvent.Draw),
        new Type[] { typeof(Building) })]
    internal static class CastleUpgradeCards
    {
        private static readonly AccessTools.FieldRef<UIBuildingOverlayElementCityEvent, ResourceAmount[]> Costs =
            AccessTools.FieldRefAccess<UIBuildingOverlayElementCityEvent, ResourceAmount[]>("m_Cost");

        private static void Postfix(UIBuildingOverlayElementCityEvent __instance, Building building, GameObject __result)
        {
            if (!(building is BuildingCastle) || __result == null) return;
            var offer = __instance;
            var cityEvent = offer.eventToTrigger;
            if (cityEvent == CityEvent.None || cityEvent == CityEvent.Ritual || !(Costs(offer)?.Length > 0)) return;
            PinPlugin.TryAttach(() => {
                var purchase = __result.GetComponent<Button>();
                if (purchase == null) return;
                string title = __result.transform.Find("TextName").GetComponent<TMP_Text>().text;
                if (cityEvent == CityEvent.UpgradeBase1 || cityEvent == CityEvent.UpgradeBase2 || cityEvent == CityEvent.UpgradeBase3)
                {
                    var description = __result.transform.Find("TextDescription")?.GetComponent<TMP_Text>()?.text;
                    if (!string.IsNullOrWhiteSpace(description)) title = description;
                }
                var row = PinRow.Create(__result, purchase, ForEvent(building, offer, title));
                row.UseAsFocusFallback = true;
            });
        }

        internal static PinTarget ForEvent(Building building, UIBuildingOverlayElementCityEvent offer, string title)
        {
            var cityEvent = offer.eventToTrigger;
            var retired = UpgradeCards.TrackRemoval(building, offer);
            return new PinTarget(building, building.GetTitle() + " · " + title, () => Costs(offer),
                permitted: cost => CityManager.Instance.DidCityEventHappen(offer.ConditionGreyedOut) &&
                    ResourceManager.Instance.CanBeBuilt(cost),
                complete: () => offer.eventToTrigger != cityEvent || retired?.Invoke() == true ||
                    CityManager.Instance.DidCityEventHappen(cityEvent) ||
                    !CityManager.Instance.DidCityEventHappen(offer.Condition),
                operation: "castle", definition: offer);
        }
    }

    [HarmonyPatch(typeof(UIBuildingOverlayElementPortal), nameof(UIBuildingOverlayElementPortal.Draw),
        new Type[] { typeof(Building) })]
    internal static class CapacityCards
    {
        private static readonly Func<UIBuildingOverlayElementPortal, int, ResourceAmount[]> Price =
            AccessTools.MethodDelegate<Func<UIBuildingOverlayElementPortal, int, ResourceAmount[]>>(
                AccessTools.Method(typeof(UIBuildingOverlayElementPortal), "GetCost"));

        private static void Postfix(UIBuildingOverlayElementPortal __instance, Building building,
            GameObject __result)
        {
            if (__result == null) return;
            PinPlugin.TryAttach(() => {
                var title = __result.transform.Find("TextName").GetComponent<TMP_Text>().text;
                var row = PinRow.Create(__result, __result.GetComponent<Button>(), ForCapacity(building, __instance, title));
                row.UseAsFocusFallback = true;
            });
        }

        internal static PinTarget ForCapacity(Building building, UIBuildingOverlayElementPortal offer, string title)
        {
            var resource = building.GetBuildingType() == BuildingType.Stables ? ResourceType.Carrier : ResourceType.Chest;
            int next = building.GetResourceQuantity(resource) + 1;
            var retired = UpgradeCards.TrackRemoval(building, offer);
            var needsSpace = AccessTools.FieldRefAccess<UIBuildingOverlayElementPortal, bool>("checkIfSpaceForUnit");
            return new PinTarget(building, building.GetTitle() + " · " + title,
                () => Price(offer, building.GetResourceQuantity(resource)), next,
                cost => ResourceManager.Instance.CanBeBuilt(cost) && !DaytimeManager.Instance.IsNight() &&
                    (!needsSpace(offer) || UnitManager.Instance.CanAddMoreUnits()),
                () => retired?.Invoke() == true || building.GetResourceQuantity(resource) >= next,
                "capacity", definition: offer);
        }
    }

    // AddUnitButtons receives only a position. Capture the actual plot passed by the
    // caller while its cards are being drawn, including mod-owned evolution plots.
    [HarmonyPatch(typeof(UIOverlayBuildingSelection), nameof(UIOverlayBuildingSelection.GenerateSelection))]
    internal static class EvolutionPlot
    {
        internal static GameObject Current;
        private static void Prefix(GameObject gridButton, BuildingSize size, out GameObject __state)
        {
            __state = Current;
            Current = size == BuildingSize.Evolution ? gridButton : null;
        }
        private static void Finalizer(GameObject __state) => Current = __state;
    }

    [HarmonyPatch(typeof(UIOverlayBuildingSelection), "AddUnitButtons")]
    internal static class EvolutionCards
    {
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var price = AccessTools.Method(typeof(UnitManager), nameof(UnitManager.GetUpgradeCost), new[] { typeof(UnitBase) });
            var display = AccessTools.Method(typeof(UIOverlayBuildingSelection), "DisplayCosts");
            int priceAt = code.FindIndex(i => i.Calls(price));
            int drawAt = code.FindIndex(i => i.Calls(display));
            if (priceAt < 1 || drawAt < priceAt + 3 || !BuildCards.LoadsLocal(code[priceAt - 1].opcode) ||
                !BuildCards.LoadsLocal(code[drawAt - 2].opcode) || code.Count(i => i.Calls(display)) != 1 ||
                code.Count(i => i.Calls(price)) != 1)
                return PinPlugin.UnsupportedLayout(code, "evolution outpost");
            code.InsertRange(drawAt + 1, new[] {
                new CodeInstruction(code[priceAt - 1].opcode, code[priceAt - 1].operand),
                new CodeInstruction(code[drawAt - 2].opcode, code[drawAt - 2].operand),
                CodeInstruction.Call(typeof(EvolutionCards), nameof(Attach))
            });
            return code;
        }

        private static void Attach(Unit unit, GameObject card)
        {
            PinPlugin.TryAttach(() => {
                var plot = EvolutionPlot.Current;
                if (plot == null) return;
                var target = ForPlot(unit, plot, card.transform.Find("TextName").GetComponent<TMP_Text>().text);
                PinRow.Create(card, card.GetComponent<Button>(), target);
            });
        }

        internal static PinTarget ForPlot(Unit unit, GameObject plot, string title) =>
            new PinTarget(unit, title + " · Outpost", () => new[] { UnitManager.Instance.GetUpgradeCost(unit) },
                permitted: cost => plot != null && plot.activeInHierarchy && unit.GetRank() == 2 &&
                    unit.CanRankUp() && ResourceManager.Instance.CanBeBuilt(cost[0]),
                complete: () => plot == null || !plot.activeInHierarchy || unit.IsEvolved(),
                operation: "evolution", definition: plot);
    }
}

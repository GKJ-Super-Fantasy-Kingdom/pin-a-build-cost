using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PinABuildCost;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

internal static class AuditChecks
{
    internal static void Run(Action<bool, string> check, Harmony harmony)
    {
        var plot = SceneFixture.New<GameObject>();
        var unit = SceneFixture.New<Unit>();
        var offer = EvolutionCards.ForPlot(unit, plot, "Archer");
        var differentSite = EvolutionCards.ForPlot(unit, SceneFixture.New<GameObject>(), "Archer");
        check(!offer.IsFinished() && offer.IsSameOffer(EvolutionCards.ForPlot(unit, plot, "Archer")) &&
            !offer.IsSameOffer(differentSite), "reopening the same Outpost unpins, another plot replaces");
        plot.SetActive(false);
        check(offer.IsFinished() && !differentSite.IsFinished(),
            "another unit consuming the selected plot retires only that site's evolution offer");
        AccessTools.Field(typeof(UnityEngine.Object), "m_CachedPtr").SetValue(plot, IntPtr.Zero);
        check(offer.IsFinished(), "destroyed Outpost plot retires without consulting its scene state");
        AccessTools.Field(typeof(UnitBase), "m_Rank").SetValue(unit, 3);
        check(differentSite.IsFinished(), "evolving the pinned unit retires its offer on a remaining plot");

        ResourceAmount[] tiers = { new ResourceAmount(ResourceType.Board, 3), new ResourceAmount(ResourceType.Stone, 8) };
        int level = 0;
        var first = WorldBuildOffers.StartingTier(unit, "Starting resource", 1, () => tiers, () => level, () => true, _ => true);
        var second = WorldBuildOffers.StartingTier(unit, "Starting resource", 2, () => tiers, () => level, () => true, _ => true);
        tiers[1] = new ResourceAmount(ResourceType.Board, 4);
        check(!second.IsFinished() && second.Price().Single().amount == 4, "retained world tier follows a changed price");
        tiers = tiers.Take(1).ToArray();
        check(second.IsFinished() && second.Price() == null && !first.IsFinished(), "shrinking the tier list retires only missing tiers");
        level = 1;
        check(first.IsFinished(), "purchasing a retained world tier clears it");
        tiers = null;
        check(second.IsFinished() && second.Price() == null, "removing the entire tier schedule is safe");

        CheckCardLifecycle(check);
        var keyboard = new InputBinding("<Keyboard>/tab");
        check(PinRow.SupportsBinding(false, null, keyboard), "plain disabled-map button binding retains shortcut");
        keyboard.overrideInteractions = "hold";
        check(!PinRow.SupportsBinding(false, null, keyboard) && PinRow.SupportsBinding(true, null, keyboard),
            "remapped hold is suppressed in a disabled map and delegated to an enabled action");
        var controller = new InputBinding("<Gamepad>/buttonNorth", interactions: "press(behavior=1)");
        check(!PinRow.SupportsBinding(false, null, controller) && PinRow.SupportsBinding(true, null, controller),
            "release-only controller binding never falls back to an initial press");
        check(!PinRow.SupportsBinding(false, "hold", new InputBinding("<Keyboard>/tab")),
            "disabled action-level interaction also suppresses the shortcut");
        controller.overridePath = "";
        check(!PinRow.SupportsBinding(true, null, controller), "unbound shortcut has no prompt");

        var manager = SceneFixture.New<ResourceManager>();
        harmony.Patch(AccessTools.Method(typeof(ResourceManager), nameof(ResourceManager.GetResourceAvailableAmount)),
            prefix: new HarmonyMethod(typeof(AuditChecks), nameof(SevenAvailable)));
        var split = new[] { new ResourceAmount(ResourceType.Board, 3), new ResourceAmount(ResourceType.Board, 7) };
        Func<ResourceType, string> name = type => type.ToString();
        var native = new PinTarget(unit, "Split quote", () => split, permitted: manager.CanBeBuilt);
        var quote = native.Read(false, type => manager.GetResourceAvailableAmount(type), name);
        check(quote.Status == "Ready" && quote.Items.Single().Text == "Board · 3 short",
            "installed game's affordability method accepts separate entries with seven available");
        var externalDll = Environment.GetEnvironmentVariable("PIN_TEST_LONE_BONFIRE");
        if (!string.IsNullOrEmpty(externalDll)) CheckLoneBonfire(check, harmony, manager, externalDll);
        bool gate = true;
        int wallet = 7;
        // Substitute the renderer only. An integration calls the actual public overloads.
        harmony.Patch(AccessTools.Method(typeof(PinRow), "Create"),
            prefix: new HarmonyMethod(typeof(AuditChecks), nameof(CaptureAttached)));
        PinPlugin.Instance = SceneFixture.New<PinPlugin>();
        var card = SceneFixture.New<GameObject>();
        var purchase = SceneFixture.New<Button>(card);
        PinCostApi.Attach(card, purchase, unit, "tests:wallet", "Own wallet", () => split, () => false, () => gate, _ => wallet);
        var custom = attached;
        PinCostApi.Attach(card, purchase, unit, "tests:waiver", "Shared free mode", () => split, () => false, () => gate,
            _ => wallet, honorFreeBuilding: true);
        var optedIn = attached;
        PinPlugin.Instance = null;
        check(custom.Read(true, _ => 100, name).Status == null && !custom.Read(true, _ => 100, name).Items[0].IsMet,
            "private wallet with a passing eligibility callback still pays its combined costs");
        check(optedIn.Read(true, _ => 0, name).Status == "Ready" && optedIn.Read(true, _ => 0, name).Items[0].Text == "Board · Free",
            "integration can opt its private wallet into free mode");
        gate = false;
        check(optedIn.Read(true, _ => 0, name).Status == "Materials Ready · Requirements Pending",
            "waiving a wallet leaves the mod's additional purchase gate active");
        wallet = 10;
        gate = true;
        check(custom.Read(false, _ => 0, name).Status == "Ready", "restocking the private wallet restores readiness");
        var api = typeof(PinCostApi).GetMethods().Where(method => method.Name == "Attach").ToArray();
        check(api.Any(method => method.GetParameters().Length == 9 && method.GetParameters()[8].IsOptional) &&
            api.Any(method => method.GetParameters().Length == 10 && method.GetParameters()[9].ParameterType == typeof(bool)),
            "original optional-stock Attach overload and explicit free-mode overload both ship");
    }

    private static PinTarget attached;

    private static void CheckLoneBonfire(Action<bool, string> check, Harmony harmony, ResourceManager stock, string dll)
    {
        var released = Assembly.LoadFrom(dll);
        MainManager.Instance = SceneFixture.New<MainManager>();
        CityManager.Instance = SceneFixture.New<CityManager>();
        RaceManager.Instance = SceneFixture.New<RaceManager>();
        WorldManager.Instance = SceneFixture.New<WorldManager>();
        RaceDataManager.Instance = SceneFixture.New<RaceDataManager>();
        ResourceManager.Instance = stock;
        var data = AccessTools.Field(typeof(RaceDataManager), "m_RaceData");
        data.SetValue(RaceDataManager.Instance, Activator.CreateInstance(data.FieldType));
        var buildings = new List<BuildingCity>();
        AccessTools.Field(typeof(CityManager), "m_Buildings").SetValue(CityManager.Instance, buildings);
        AccessTools.Field(typeof(CityManager), "m_Outposts").SetValue(CityManager.Instance, new List<BuildingCity>());
        AccessTools.Field(typeof(RaceManager), "canBeBuiltMultipleTimes").SetValue(RaceManager.Instance, new[] { BuildingType.WorldDifficulty });
        var prefab = SceneFixture.New<BuildingCity>();
        var placed = SceneFixture.New<BuildingOutpostDifficulty>();
        var type = AccessTools.Field(typeof(BuildingCity), "type");
        type.SetValue(prefab, BuildingType.WorldDifficulty);
        type.SetValue(placed, BuildingType.WorldDifficulty);
        AccessTools.Field(typeof(BuildingCity), "cost").SetValue(prefab, new[] { new ResourceAmount(ResourceType.Board, 1) });
        var pin = new PinTarget(prefab, "Bonfire", () => prefab.GetCost(), permitted: _ => BuildCards.CanOffer(prefab));
        string Status() => pin.Read(false, _ => 7, resource => resource.ToString()).Status;
        buildings.Add(placed);
        check(Status() == "Ready", "standalone funded construction pin permits a repeatable Bonfire");
        AccessTools.Method(released.GetType("LoneBonfire.Patch_LearnBonfireType", true), "Postfix").Invoke(null, new object[] { placed });
        harmony.PatchAll(released.GetType("LoneBonfire.Patch_CapBonfire", true));
        check(Status() == "Materials Ready · Requirements Pending", "released LoneBonfire purchase patch blocks the funded repeat offer");
        buildings.Clear();
        check(Status() == "Ready", "LoneBonfire permits the funded offer when the city's Bonfire is removed");
    }
    private static bool CaptureAttached(PinTarget target)
    {
        attached = target;
        return false;
    }

    private static bool SevenAvailable(ref int __result)
    {
        __result = 7;
        return false;
    }

    private static void CheckCardLifecycle(Action<bool, string> check)
    {
        foreach (bool removeCard in new[] { true, false })
        foreach (bool destroy in new[] { true, false })
        {
            var card = SceneFixture.New<GameObject>();
            var buy = SceneFixture.New<GameObject>();
            var root = SceneFixture.New<GameObject>();
            var row = SceneFixture.New<PinRow>(root);
            AccessTools.Field(typeof(PinRow), "cardRect").SetValue(row, SceneFixture.New<RectTransform>(card));
            AccessTools.Field(typeof(PinRow), "purchaseButton").SetValue(row, SceneFixture.New<Button>(buy));
            var source = removeCard ? card : buy;
            var link = SceneFixture.New<PinCardLink>(source);
            link.Row = row;
            var owner = SceneFixture.New<PinPlugin>();
            AccessTools.Field(typeof(PinRow), "owner").SetValue(row, owner);
            var target = new PinTarget(link, "Card", null);
            AccessTools.Field(typeof(PinRow), "target").SetValue(row, target);
            check(row.HasVisibleOffer, "active card and purchase permit shortcut selection");
            // The child is still activeSelf when the entire panel goes away.
            SceneFixture.HiddenByParent.Add(card);
            SceneFixture.HiddenByParent.Add(buy);
            AccessTools.Method(typeof(PinCardLink), "OnDisable").Invoke(link, null);
            check(!row.HasVisibleOffer, "hidden parent panel has no eligible offer");
            SceneFixture.HiddenByParent.Remove(card);
            SceneFixture.HiddenByParent.Remove(buy);
            check(row.HasVisibleOffer && !SceneFixture.Destroyed.Contains(root), "reopening the parent panel preserves the binding");
            var activate = AccessTools.Method(typeof(PinRow), "Activate");
            SceneFixture.Frame++;
            activate.Invoke(row, null);
            activate.Invoke(row, null);
            check(owner.Pinned == target, "visible pin accepts confirmation once per frame");
            if (destroy)
                AccessTools.Method(typeof(PinCardLink), "OnDestroy").Invoke(link, null);
            else
            {
                source.SetActive(false);
                check(!row.HasVisibleOffer, "hidden " + (removeCard ? "card" : "purchase") + " immediately loses shortcut eligibility");
                AccessTools.Method(typeof(PinCardLink), "OnDisable").Invoke(link, null);
            }
            source.SetActive(true);
            SceneFixture.Frame++;
            activate.Invoke(row, null);
            check(!row.HasVisibleOffer && !root.activeSelf && SceneFixture.Destroyed.Contains(root) && owner.Pinned == target,
                (destroy ? "destroyed " : "recycled ") + (removeCard ? "card" : "purchase") + " leaves no eligible or clickable stale row");
        }
    }
}

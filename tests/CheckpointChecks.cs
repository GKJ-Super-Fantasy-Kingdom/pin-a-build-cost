using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using PinABuildCost;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;
using SuperFantasyKingdom.Tavern;
using SuperFantasyKingdom.UI;
using UnityEngine;

internal static class CheckpointChecks
{
    private static GameData native;
    private static JObject template;
    private static readonly Dictionary<Building, UIBuildingOverlay> overlays = new Dictionary<Building, UIBuildingOverlay>();
    private static bool Overlay(Building building, ref UIBuildingOverlay __result)
    {
        overlays.TryGetValue(building, out __result);
        return false;
    }
    private static string Root => Environment.GetEnvironmentVariable("PIN_CHECKPOINT_FIXTURE");
    private static bool Generate(ref GameData __result) { __result = native; return false; }
    private static bool SavePath(string file, ref string __result)
    {
        if (Path.GetFileName(file) != file) throw new InvalidOperationException("Unexpected fixture path");
        __result = Path.Combine(Root, file + ".data");
        return false;
    }
    private static bool KingdomPath(ref string __result) { __result = "human_game"; return false; }
    private static bool Title(Building __instance, ref string __result) { __result = __instance.name; return false; }
    private static bool Skip() => false;

    internal static string NativeJson(GameData data)
    {
        var doc = (JObject)template.DeepClone();
        doc["runID"] = data.runID;
        doc["day"] = data.day;
        doc["kingdom"] = (int)data.kingdom;
        doc["hash"] = data.hash;
        return doc.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static void Field(object owner, string field, object value) =>
        AccessTools.Field(owner.GetType(), field).SetValue(owner, value);

    private static T Place<T>(GameData.BuildingData data) where T : BuildingCity
    {
        var building = SceneFixture.New<T>(SceneFixture.New<GameObject>());
        SceneFixture.Names[building] = data.name + "(Clone)";
        var location = SceneFixture.New<Transform>();
        SceneFixture.Locations[building] = location;
        SceneFixture.Positions[location] = data.position;
        Field(building, "type", data.type);
        Field(building, "m_IsUpgraded", data.isUpgraded);
        return building;
    }

    internal static void Run(Action<bool, string> check, Harmony harmony)
    {
        var original = Path.Combine(Root, "original.data");
        template = File.Exists(original) ? JObject.Parse(File.ReadAllText(original)) : new JObject {
            ["runID"] = "fixture-kingdom", ["kingdom"] = 0, ["day"] = 9, ["hash"] = "fixture-dawn",
            ["buildings"] = JArray.Parse("[{'name':'HumanCastle','position':{'x':0,'y':-15}},"
                + "{'name':'HumanHouse','position':{'x':-3,'y':-14}},{'name':'HumanHouse','position':{'x':-3,'y':-18}}]") };
        template["neighborModTestField"] = new JObject { ["enabled"] = true };
        var file = Path.Combine(Root, "human_game.data");
        File.WriteAllText(file, template.ToString());
        foreach (var type in new[] { typeof(PinCheckpoints.ReadGameData), typeof(PinCheckpoints.SaveScope),
            typeof(PinCheckpoints.WriteGameData), typeof(PinCheckpoints.LoadKingdom) }) harmony.PatchAll(type);
        void Stub(Type type, string method, string handler) => harmony.Patch(AccessTools.Method(type, method),
            prefix: new HarmonyMethod(typeof(CheckpointChecks), handler));
        Stub(typeof(SaveManager), "GetFilePath", nameof(SavePath));
        Stub(typeof(GameDataManager), "GenerateGameData", nameof(Generate));
        Stub(typeof(GameDataManager), "GetPath", nameof(KingdomPath));
        Stub(typeof(TavernSaveManager), "GetPath", nameof(KingdomPath));
        Stub(typeof(Building), "GetTitle", nameof(Title));
        Stub(typeof(UpgradeCards), "Overlay", nameof(Overlay));
        // The fixture reconstructs the scene, then invokes the normal patched Load boundary.
        Stub(typeof(GameDataManager), nameof(GameDataManager.Load), nameof(Skip));
        var saves = SaveManager.Instance = SceneFixture.New<SaveManager>();
        var manager = GameDataManager.Instance = SceneFixture.New<GameDataManager>();
        var plugin = PinPlugin.Instance = SceneFixture.New<PinPlugin>();
        RaceManager.Instance = SceneFixture.New<RaceManager>();
        CityManager.Instance = SceneFixture.New<CityManager>();
        GameManager.Instance = SceneFixture.New<GameManager>();
        Field(GameManager.Instance, "m_IsGameLoaded", true);
        var placed = new List<BuildingCity>();
        Field(CityManager.Instance, "m_Buildings", placed);
        Field(CityManager.Instance, "m_Outposts", new List<BuildingCity>());
        var events = new List<CityEvent> { CityEvent.None };
        Field(CityManager.Instance, "m_CityEvents", events);
        native = saves.LoadGameData("human_game");
        Field(manager, "m_RunID", native.runID);
        Field(RaceManager.Instance, "race", native.kingdom);
        check(PinCheckpoints.For(native) == null, "legacy kingdom loads without a pin");

        var castleData = native.buildings.First(b => b.name.Contains("Castle"));
        var castle = Place<BuildingCastle>(castleData);
        var restoredCastle = Place<BuildingCastle>(castleData);
        UIBuildingOverlayElementCityEvent Event(CityEvent id, int amount)
        {
            var e = new UIBuildingOverlayElementCityEvent { eventToTrigger = id };
            Field(e, "m_Cost", new[] { new ResourceAmount(ResourceType.Board, amount) });
            return e;
        }
        var storage = Event(CityEvent.UpgradeBase1, 5);
        var wall = Event(CityEvent.WallsWood, 6);
        var pinnedStorage = CastleUpgradeCards.ForEvent(castle, storage, "Increase storage");
        var pinnedWall = CastleUpgradeCards.ForEvent(castle, wall, "Wall");
        plugin.Toggle(pinnedStorage);
        manager.Save();
        var dawnStorage = File.ReadAllText(file);
        var data = saves.LoadGameData("human_game");
        var first = PinCheckpoints.For(data);
        string originalJson = NativeJson(native);
        check(first != null && data.hash == native.hash &&
            dawnStorage.StartsWith(originalJson.Substring(0, originalJson.Length - 1), StringComparison.Ordinal) &&
            (bool)JObject.Parse(dawnStorage)["neighborModTestField"]["enabled"],
            "native save writer and loader round-trip the pin in a copied profile without changing native fields");
        var liveStorage = Event(CityEvent.UpgradeBase1, 18);
        var liveWall = Event(CityEvent.WallsWood, 12);
        var choices = new UIBuildingOverlayElement[] { liveWall, liveStorage };
        placed.Add(restoredCastle);
        var overlay = overlays[restoredCastle] = SceneFixture.New<UIBuildingOverlay>();
        overlay.buttons = choices.ToList();
        void Continue(GameData loaded)
        {
            Field(manager, "m_GameData", loaded);
            manager.Load();
            AccessTools.Method(typeof(PinPlugin), "Restore").Invoke(plugin, null);
        }
        Continue(data);
        var rebuilt = plugin.Pinned;
        check(rebuilt != null && ReferenceEquals(rebuilt.Source, restoredCastle) && rebuilt.Price()[0].amount == 18 &&
            ReferenceEquals(rebuilt.Definition, liveStorage), "Castle Storage rebinds to its reconstructed event and live price even when rows reorder");
        events.Add(CityEvent.UpgradeBase1);
        check(first.ResolveChoice(restoredCastle, choices) == null, "completed Castle Storage is discarded");
        events.Remove(CityEvent.UpgradeBase1);
        check(first.ResolveChoice(restoredCastle, new[] { liveWall }) == null &&
            first.ResolveChoice(restoredCastle, new[] { liveStorage, Event(CityEvent.UpgradeBase1, 1) }) == null,
            "missing and ambiguous event definitions clear instead of selecting Wall");

        plugin.Toggle(pinnedWall);
        native.day++;
        native.hash = "next-checkpoint";
        manager.Save();
        var dawnWall = File.ReadAllText(file);
        var later = PinCheckpoints.For(saves.LoadGameData("human_game"));
        Continue(saves.LoadGameData("human_game"));
        check(plugin.Pinned.Definition == liveWall && plugin.Pinned.Price()[0].amount == 12,
            "next checkpoint keeps the replacement Wall and current quote");
        check(File.ReadAllText(file + ".bak") == dawnStorage &&
            PinCheckpoints.For(JsonUtility.FromJson<GameData>(File.ReadAllText(file + ".bak"))).choice == first.choice,
            "replaying the native dawn backup restores Storage instead of the later Wall pin");
        Continue(JsonUtility.FromJson<GameData>(dawnStorage));
        check(plugin.Pinned.Definition == liveStorage, "continue from replayed dawn rebinds the prior Storage pin in the HUD model");
        Continue(JsonUtility.FromJson<GameData>(dawnWall));
        events.Add(CityEvent.WallsWood);
        check(later.ResolveChoice(restoredCastle, choices) == null, "completed Wall is discarded independently");
        events.Clear(); events.Add(CityEvent.None);
        foreach (var changed in new[] { "runID", "kingdom", "day", "hash" })
        {
            var mismatch = JObject.Parse(dawnWall);
            mismatch[changed] = changed == "runID" || changed == "hash" ? new JValue("other") : new JValue(777);
            check(PinCheckpoints.For(JsonUtility.FromJson<GameData>(mismatch.ToString())) == null, "reject pin belonging to another " + changed);
        }
        var broken = JObject.Parse(dawnWall); broken["pinABuildCostV1"] = "old-or-malformed";
        check(PinCheckpoints.For(JsonUtility.FromJson<GameData>(broken.ToString())) == null, "malformed extension leaves the native save readable");
        plugin.Toggle(plugin.Pinned);
        manager.Save();
        check(PinCheckpoints.For(saves.LoadGameData("human_game")) == null, "unpin is recorded at the following checkpoint");

        var tavern = SceneFixture.New<TavernSaveManager>();
        var beforeTavern = JsonUtility.FromJson<GameData>(dawnStorage);
        beforeTavern.hash = ""; // TavernSaveManager.Load clears it before editing the day.
        Field(tavern, "m_GameData", beforeTavern);
        TavernFoodManager.Instance = SceneFixture.New<TavernFoodManager>();
        Field(TavernFoodManager.Instance, "cook", SceneFixture.New<TavernCook>());
        tavern.Save();
        var afterTavern = saves.LoadGameData("human_game");
        check(afterTavern.day == beforeTavern.day && PinCheckpoints.For(afterTavern)?.choice == first.choice,
            "tavern checkpoint rewrite carries its own Storage pin into the new native day and hash");
        Continue(afterTavern);
        Continue(JsonUtility.FromJson<GameData>(template.ToString()));
        check(plugin.Pinned == null && plugin.Snapshot() == null, "continuing a legacy save clears a previously active pin");
        Field(manager, "m_RunID", "different-run");
        Continue(afterTavern);
        check(plugin.Pinned == null && plugin.Snapshot() == null, "different active run cannot restore a valid pin from the old run");
        Field(manager, "m_RunID", native.runID);

        // Use the building records from the copied kingdom, not the order of their UI cards.
        var pair = native.buildings.GroupBy(b => b.name).First(g => g.Count() >= 2).Take(2).ToArray();
        var owner = Place<BuildingCity>(pair[1]);
        var other = Place<BuildingCity>(pair[0]);
        var replacement = Place<BuildingCity>(pair[1]);
        var upgrade = new UIBuildingOverlayElementUpgrade { Cost = new[] { new ResourceAmount(ResourceType.Board, 9) } };
        Field(upgrade, "amountOfUpgrades", 12);
        var exact = CheckpointOffer.Capture(UpgradeCards.ForUpgrade(owner, upgrade, "Upgrade"));
        placed.Add(other); placed.Add(replacement);
        var houseOverlay = overlays[replacement] = SceneFixture.New<UIBuildingOverlay>();
        houseOverlay.buttons = new List<UIBuildingOverlayElement> { upgrade };
        plugin.Toggle(UpgradeCards.ForUpgrade(owner, upgrade, "Upgrade"));
        manager.Save();
        Continue(saves.LoadGameData("human_game"));
        check(plugin.Pinned?.Source == replacement, "saving and continuing restores the selected instance among identical houses");
        check(exact.LocateBuilding(new[] { other, replacement }) == replacement &&
            exact.LocateBuilding(new[] { other }) == null, "two buildings of one design resolve by native position; demolition leaves no match");
        var resolved = exact.ResolveChoice(replacement, new[] { upgrade });
        int price = resolved.Price()[0].amount;
        upgrade.Cost[0].amount *= 2;
        check(resolved.Price()[0].amount == price * 2, "rebound upgrade keeps querying the live price getter");
        Field(replacement, "m_IsUpgraded", exact.tier);
        check(exact.ResolveChoice(replacement, new[] { upgrade }) == null, "already purchased tier never rolls forward to the next tier");
        var prefab = Place<BuildingCity>(pair[0]);
        var newPrefab = Place<BuildingCity>(pair[0]);
        Field(RaceManager.Instance, "buildingPrototypes", new[] { prefab.gameObject });
        Field(RaceManager.Instance, "canBeBuiltMultipleTimes", new[] { prefab.GetBuildingType() });
        var design = CheckpointOffer.Capture(BuildCards.ForPrefab(prefab));
        Field(RaceManager.Instance, "buildingPrototypes", new[] { newPrefab.gameObject });
        Field(newPrefab, "cost", new[] { new ResourceAmount(ResourceType.Ingot, 13) });
        var restoredDesign = design.ResolveDesign(new[] { newPrefab });
        check(restoredDesign?.Source == newPrefab && restoredDesign.Price()[0].amount == 13,
            "construction pin resolves the active prefab and newly quoted price");
        check(design.ResolveDesign(Array.Empty<BuildingCity>()) == null && design.ResolveDesign(new[] { prefab, newPrefab }) == null,
            "retired or ambiguous construction design cannot inherit the pin");
        Field(RaceManager.Instance, "canBeBuiltMultipleTimes", Array.Empty<BuildingType>());
        check(design.ResolveDesign(new[] { newPrefab }) == null, "an already placed single-instance design clears on continue");
        PinPlugin.Instance = null;
    }
}

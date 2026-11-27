using System;
using System.Reflection;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using PinABuildCost;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;
using SuperFantasyKingdom.UI;

internal static class Checks
{
    private static void Check(bool ok, string description)
    {
        if (!ok) throw new Exception(description);
        Console.WriteLine("PASS " + description);
    }

    public static int Run()
    {
        try
        {
            Check(BuildCards.IsBonusList(BuildingSize.WorldBig) && BuildCards.IsBonusList(BuildingSize.WorldSpecial),
                "both native Outpost bonus lists retain their original controls");
            Check(!BuildCards.IsBonusList(BuildingSize.Big) && !BuildCards.IsBonusList(BuildingSize.Small) &&
                !BuildCards.IsBonusList(BuildingSize.Evolution), "construction and evolution remain distinct from bonus lists");
            // Managed identity only. No Unity scene or native object is constructed.
            var first = (BuildingCity)FormatterServices.GetUninitializedObject(typeof(BuildingCity));
            var second = (BuildingCity)FormatterServices.GetUninitializedObject(typeof(BuildingCity));
            var suppliedName = new PinTarget(first, "Rivière · add side chamber", null, 2);
            Check(suppliedName.Title == "Rivière · add side chamber · Lv 2",
                "localized and mod-supplied titles keep their original spelling and case");
            var mill = new PinTarget(first, "Mill", null);
            var same = new PinTarget(first, "Mill", null);
            var other = new PinTarget(second, "Mill", null);
            var level = new PinTarget(first, "Mill upgrade", null, 2);
            Check(mill.Action(null) == "Pin Cost" && mill.Toggle(null) == mill, "pin from empty");
            Check(same.Action(mill) == "Unpin" && same.Toggle(mill) == null, "reopened card removes its pin");
            Check(other.Action(mill) == "Replace Pin" && other.Toggle(mill) == other, "replace a same-named target");
            Check(mill.Matches(first, 0) && !mill.Matches(second, 0) && !level.Matches(first, 0), "build completion matches prefab and operation");
            Check(!level.CompletedUpgrade(second, 2) && !level.CompletedUpgrade(first, 1) &&
                level.CompletedUpgrade(first, 2) && !mill.CompletedUpgrade(first, 2), "upgrade matches instance and level");

            var costs = new[] { new ResourceAmount(ResourceType.Board, 3), new ResourceAmount(ResourceType.Board, 7),
                new ResourceAmount(ResourceType.Stone, 2) };
            int boards = 7;
            Func<ResourceType, int> stock = t => t == ResourceType.Board ? boards : 2;
            Func<ResourceType, string> name = t => t.ToString();
            var mixed = PinTarget.Describe(costs, stock, name);
            Check(mixed.Items.Select(item => item.Resource).SequenceEqual(new[] { ResourceType.Board, ResourceType.Stone }),
                "checklist carries one native resource identity per aggregated material");
            var repriced = PinTarget.Describe(new[] { new ResourceAmount(ResourceType.Ingot, 5) }, stock, _ => "Material");
            Check(repriced.Items.Single().Resource == ResourceType.Ingot && repriced.Items.Single().Text == "Material · 2/5 · 3 short",
                "runtime price changes keep the icon tied to the live type independently of its localized name");
            var nativeOffer = new PinTarget(first, "Native split price", () => costs,
                permitted: quote => quote.All(part => stock(part.type) >= part.amount));
            var split = nativeOffer.Read(false, stock, name);
            Check(split.Status == "Ready" && split.Items[0].Text == "Board · 7/10 · 3 short",
                "native purchase permits each split entry while the checklist keeps the total");
            Check(mixed.Items.Length == 2 && mixed.Items[0].Text == "Board · 7/10 · 3 short" && !mixed.Items[0].IsMet &&
                mixed.Items[1].Text == "Stone · 2/2" && mixed.Items[1].IsMet && mixed.Status == null,
                "combine repeated costs and retain a checked row alongside a shortage");
            boards = 10;
            var ready = PinTarget.Describe(costs, stock, name);
            Check(ready.Status == "Ready" && ready.Items.All(row => row.IsMet) && ready.Items[0].Text == "Board · 10/10",
                "all material rows stay checked when ready");
            boards = 4;
            var reopened = PinTarget.Describe(costs, stock, name);
            Check(!reopened.Items[0].IsMet && reopened.Items[0].Text == "Board · 4/10 · 6 short" && reopened.Items[1].IsMet,
                "spending unchecks only the affected requirement");
            Check(PinTarget.Describe(costs, t => 0, name).Items.All(row => !row.IsMet), "multiple missing materials");
            Check(PinTarget.Describe(new ResourceAmount[0], stock, name).Status == "Ready" &&
                PinTarget.Describe(null, stock, name) == CostChecklist.Unavailable, "empty versus unknown price");
            Check(PinTarget.Describe(new[] {new ResourceAmount(ResourceType.None, -1)}, stock, name) == CostChecklist.Unavailable,
                "unavailable price sentinel");

            ResourceAmount[] live = { new ResourceAmount(ResourceType.Board, 10) };
            bool gate = true;
            var addedBuilding = new PinTarget(first, "Content pack mill", () => live,
                permitted: quote => gate && quote.All(part => stock(part.type) >= part.amount));
            Check(addedBuilding.Read(false, stock, name).Items.Single().Text == "Board · 4/10 · 6 short", "added building reads current stock");
            live = new[] { new ResourceAmount(ResourceType.Stone, 5) };
            Check(addedBuilding.Read(false, stock, name).Items.Single().Text == "Stone · 2/5 · 3 short", "open pin follows a new resource mix without a city event");
            live[0].amount = 2;
            gate = false;
            var blocked = addedBuilding.Read(false, stock, name);
            Check(blocked.Status.Contains("Requirements Pending") && blocked.Items.Single().IsMet,
                "checked materials respect an external eligibility gate");
            var waived = addedBuilding.Read(true, _ => throw new Exception("free cost queried stock"), name);
            Check(waived.Status.Contains("Requirements Pending") && waived.Items.Single().IsMet &&
                waived.Items[0].Text == "Stone · Free", "free requirements show a check without claiming stock");
            gate = true;
            Check(addedBuilding.Read(false, stock, name).Status == "Ready", "external gate can reopen");
            Check(addedBuilding.Read(false, t => 0, name).Items.Single().Text == "Stone · 0/2 · 2 short", "withdrawn stock restores the shortage");
            var capacity = new PinTarget(first, "Chest", () => live, 2, operation: "capacity");
            Check(!capacity.IsSameOffer(level) && !capacity.CompletedUpgrade(first, 2), "capacity and level upgrades on the same source stay independent");
            var definition = new FixedUpgrade();
            var originalOffer = new PinTarget(first, "Forge", () => live, 1, definition: definition);
            var reopenedOffer = new PinTarget(first, "Forge", () => live, 1, definition: definition);
            var alternateOffer = new PinTarget(first, "Forge", () => costs, 1, definition: new FixedUpgrade());
            Check(alternateOffer.Toggle(originalOffer) == alternateOffer && reopenedOffer.Toggle(originalOffer) == null,
                "same-tier upgrade alternatives on one building can be replaced and reopened");
            Check(!addedBuilding.IsSameOffer(other), "a replacement instance with the same name is a distinct offer");
            bool retired = false;
            var variant = new PinTarget(first, "Variant", () => live, complete: () => retired,
                operation: "test:variant", readStock: _ => 2);
            Check(!variant.IsFinished() && variant.Read(false, _ => 0, name).Status == "Ready", "custom inventory takes priority for its offer");
            retired = true;
            Check(variant.IsFinished(), "variant retirement is authoritative even when the old prefab survives");
            var wallet = new PinTarget(first, "Private wallet", () => live, readStock: _ => 0);
            Check(wallet.Read(true, stock, name).Status == null && !wallet.Read(true, stock, name).Items[0].IsMet,
                "global free building does not waive a mod's private wallet by default");

            // Install and remove the shipped hooks in this separate Mono process. No game is running here.
            var harmony = new Harmony("sfk.pinabuildcost.offlinechecks");
            try
            {
                var plugin = typeof(PinPlugin).Assembly;
                AuditChecks.Run(Check, harmony);
                CheckpointChecks.Run(Check, harmony);
                foreach (var type in new[] { "BuildCards", "UpgradeCards", "TabFocus", "BuildCompletion",
                    "CapacityCards", "CastleUpgradeCards", "WorldBuildOffers", "WorldUnlockCompletion", "EvolutionPlot", "EvolutionCards" })
                {
                    harmony.PatchAll(plugin.GetType("PinABuildCost." + type, true));
                    Check(true, "installed " + type + " against the game assembly");
                }
                var derived = new FixedUpgrade();
                var getter = (Func<UIBuildingOverlayElementUpgrade, Building, ResourceAmount[]>)plugin
                    .GetType("PinABuildCost.UpgradeCards").GetField("Price", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                Check(getter(derived, first)[0].amount == 17, "upgrade pricing uses the virtual override");
                var method = AccessTools.Method(typeof(FixedUpgrade), "GetCosts");
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(Checks), nameof(Reprice)));
                Check(getter(derived, first)[0].type == ResourceType.Stone && getter(derived, first)[0].amount == 9,
                    "already-bound upgrade getter sees another Harmony patch");
                foreach (var pair in new[] { ("AddBuildingButtons", typeof(BuildCards)), ("AddUnitButtons", typeof(EvolutionCards)) })
                {
                    // HarmonyX's game-runtime ILGenerator comes from Mono's mscorlib.
                    var reader = typeof(PatchProcessor).GetMethods().Single(m => m.Name == "GetOriginalInstructions" &&
                        m.GetParameters().Length == 2 && !m.GetParameters()[1].IsOut);
                    var original = (System.Collections.Generic.List<CodeInstruction>)reader.Invoke(null,
                        new object[] { AccessTools.Method(typeof(UIOverlayBuildingSelection), pair.Item1), null });
                    var rewrite = pair.Item2 == typeof(BuildCards) ? BuildCards.Transpiler(original) : EvolutionCards.Transpiler(original);
                    Check(rewrite.Count(i => i.Calls(AccessTools.Method(pair.Item2, "Attach"))) == 1,
                        "actual game IL attaches exactly one pin per priced card in " + pair.Item1);
                }
            }
            finally { harmony.UnpatchSelf(); }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private sealed class FixedUpgrade : UIBuildingOverlayElementUpgrade
    {
        protected override ResourceAmount[] GetCosts(Building building) =>
            new[] { new ResourceAmount(ResourceType.Board, 17) };
    }

    private static void Reprice(ref ResourceAmount[] __result) =>
        __result = new[] { new ResourceAmount(ResourceType.Stone, 9) };
}

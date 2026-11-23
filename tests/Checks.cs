using System;
using System.Reflection;
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
            // Managed identity only. No Unity scene or native object is constructed.
            var first = (BuildingCity)FormatterServices.GetUninitializedObject(typeof(BuildingCity));
            var second = (BuildingCity)FormatterServices.GetUninitializedObject(typeof(BuildingCity));
            var mill = new PinTarget(first, "Mill", null);
            var same = new PinTarget(first, "Mill", null);
            var other = new PinTarget(second, "Mill", null);
            var level = new PinTarget(first, "Mill upgrade", null, 2);
            Check(mill.Action(null) == "Pin cost" && mill.Toggle(null) == mill, "pin from empty");
            Check(same.Action(mill) == "Unpin" && same.Toggle(mill) == null, "reopened card removes its pin");
            Check(other.Action(mill) == "Replace pin" && other.Toggle(mill) == other, "replace a same-named target");
            Check(mill.Matches(first, 0) && !mill.Matches(second, 0) && !level.Matches(first, 0), "build completion matches prefab and operation");
            Check(!level.CompletedUpgrade(second, 2) && !level.CompletedUpgrade(first, 1) &&
                level.CompletedUpgrade(first, 2) && !mill.CompletedUpgrade(first, 2), "upgrade matches instance and level");

            var costs = new[] { new ResourceAmount(ResourceType.Board, 3), new ResourceAmount(ResourceType.Board, 7),
                new ResourceAmount(ResourceType.Stone, 2) };
            int boards = 7;
            Func<ResourceType, int> stock = t => t == ResourceType.Board ? boards : 2;
            Func<ResourceType, string> name = t => t.ToString();
            Check(PinTarget.Describe(costs, stock, name) == "Board · 3 short", "sum repeated costs and omit satisfied resources");
            boards = 10;
            Check(PinTarget.Describe(costs, stock, name) == "Ready", "materials ready");
            boards = 4;
            Check(PinTarget.Describe(costs, stock, name) == "Board · 6 short", "spending restores shortage");
            Check(PinTarget.Describe(costs, t => 0, name).Split('\n').Length == 2, "multiple missing materials");
            Check(PinTarget.Describe(new ResourceAmount[0], stock, name) == "Ready" &&
                PinTarget.Describe(null, stock, name) == "Cost unavailable", "empty versus unknown price");
            Check(PinTarget.Describe(new[] {new ResourceAmount(ResourceType.None, -1)}, stock, name) == "Cost unavailable",
                "unavailable price sentinel");

            // Install and remove the shipped hooks in this separate Mono process. No game is running here.
            var harmony = new Harmony("sfk.pinabuildcost.offlinechecks");
            try
            {
                var plugin = typeof(PinPlugin).Assembly;
                foreach (var type in new[] { "BuildCards", "UpgradeCards", "TabFocus", "BuildCompletion" })
                {
                    harmony.PatchAll(plugin.GetType("PinABuildCost." + type, true));
                    Check(true, "installed " + type + " against the game assembly");
                }
                var derived = new FixedUpgrade();
                var getter = (Func<UIBuildingOverlayElementUpgrade, Building, ResourceAmount[]>)plugin
                    .GetType("PinABuildCost.UpgradeCards").GetField("Price", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                Check(getter(derived, first)[0].amount == 17, "upgrade pricing uses the virtual override");
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
}

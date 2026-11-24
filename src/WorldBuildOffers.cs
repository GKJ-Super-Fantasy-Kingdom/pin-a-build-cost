using System;
using System.Text.RegularExpressions;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;
using SuperFantasyKingdom.Clickable;
using SuperFantasyKingdom.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PinABuildCost
{
    // Repairs and permanent world upgrades share a confirmation dialog with unrelated
    // purchases. Identify the operation from its actual callback, never the displayed text.
    [HarmonyPatch(typeof(UIOverlayMessage), nameof(UIOverlayMessage.SetAccept),
        new Type[] { typeof(ResourceAmount), typeof(UnityAction), typeof(bool) })]
    internal static class WorldBuildOffers
    {
        private static void Postfix(ResourceAmount resourceAmount, UnityAction call, Button __result)
        {
            if (__result == null || call == null) return;
            PinPlugin.TryAttach(() => {
                var target = ForCallback(call, resourceAmount);
                if (target == null) return;
                var buttons = (RectTransform)__result.transform.parent;
                PinRow.Create(buttons.gameObject, __result, target, buttons,
                    __result.transform as RectTransform, buttons.Find("Cancel") as RectTransform);
            });
        }

        internal static PinTarget ForCallback(UnityAction call, ResourceAmount quote)
            => ForSource(call.Target, call.Method.Name, quote);

        internal static PinTarget ForSource(object source, string callback, ResourceAmount quote)
        {
            if (source is BuildingWitch witch && callback == "UnlockBonfire")
            {
                int count = RaceDataManager.Instance.GetBonfiresUnlocked();
                // This unlock quotes a star threshold in the dialog. There is no price getter.
                return Single(witch, "Bonfire Unlock", () => quote,
                    () => RaceDataManager.Instance.GetBonfiresUnlocked() > count, count + 1);
            }
            if (callback != "Callback") return null;
            if (source is RepairableBuilding repair)
            {
                var prefab = AccessTools.FieldRefAccess<RepairableBuilding, BuildingCity>(repair, "toSpawn");
                return Single(repair, prefab.GetTitle(), () => repair.cost);
            }
            if (source is BuildingSpotWorld spot)
            {
                var resource = AccessTools.FieldRefAccess<BuildingSpotWorld, ResourceType>(spot, "resource");
                string title = RaceManager.Instance.GetResourceData(resource)?.GetName() ?? resource.ToString();
                var price = AccessTools.MethodDelegate<Func<BuildingSpotWorld, int>>(
                    AccessTools.Method(typeof(BuildingSpotWorld), "GetTotalCost"));
                return Single(spot, title + " · Rebuild", () => new ResourceAmount(quote.type, price(spot)));
            }
            if (source is BuildingWorldStartingResource site)
            {
                int next = site.GetUpgrade() + 1;
                var field = AccessTools.FieldRefAccess<BuildingWorldStartingResource, ResourceAmount[]>("cost");
                return StartingTier(site, Title(site), next, () => field(site), site.GetUpgrade,
                    () => ResourceManager.Instance.GetStars() >=
                        AccessTools.FieldRefAccess<BuildingWorldStartingResource, int>(site, "requiredStars"),
                    cost => ResourceManager.Instance.CanBeBuilt(cost));
            }
            if (source is RaceUnlockablePlaceholder unlock)
            {
                return Single(unlock, Title(unlock),
                    () => AccessTools.FieldRefAccess<RaceUnlockablePlaceholder, ResourceAmount>(unlock, "cost"),
                    allowed: () => {
                        var gate = AccessTools.FieldRefAccess<RaceUnlockablePlaceholder, RaceEvent>(unlock, "requirement");
                        return gate == RaceEvent.None || RaceDataManager.Instance.HasRaceEventFlag(gate);
                    });
            }
            return null;
        }

        internal static PinTarget StartingTier(Component site, string title, int next, Func<ResourceAmount[]> tiers,
            Func<int> level, Func<bool> unlocked, Func<ResourceAmount[], bool> affordable)
        {
            ResourceAmount[] ReadTier()
            {
                var prices = tiers();
                return prices != null && next > 0 && next <= prices.Length ? new[] { prices[next - 1] } : null;
            }
            return new PinTarget(site, title, ReadTier, next, cost => unlocked() && affordable(cost),
                () => level() >= next || next > (tiers()?.Length ?? 0), "world");
        }

        private static string Title(BuildingWorld site)
        {
            var title = site.GetTitle();
            return string.IsNullOrWhiteSpace(title)
                ? Regex.Replace(site.name.Replace("(Clone)", "").Trim(), "(?<=[a-z])(?=[A-Z])", " ") : title;
        }

        private static PinTarget Single(Component owner, string title, Func<ResourceAmount> price,
            Func<bool> complete = null, int level = 0, Func<bool> allowed = null) =>
            new PinTarget(owner, title, () => new[] { price() }, level,
                cost => (allowed == null || allowed()) && ResourceManager.Instance.CanBeBuilt(cost),
                complete, "world");
    }

    [HarmonyPatch(typeof(RaceUnlockablePlaceholder), nameof(RaceUnlockablePlaceholder.Upgrade))]
    internal static class WorldUnlockCompletion
    {
        private static void Postfix(RaceUnlockablePlaceholder __instance, bool fromSave)
        {
            if (!fromSave) PinPlugin.Instance?.Completed(__instance, "world");
        }
    }
}

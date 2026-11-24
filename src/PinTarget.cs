using System;
using System.Collections.Generic;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;
using UnityEngine;

namespace PinABuildCost
{
    internal readonly struct MaterialCheck
    {
        internal readonly ResourceType Resource;
        internal readonly string Text;
        internal readonly bool IsMet;

        internal MaterialCheck(ResourceType resource, string text, bool isMet)
        {
            Resource = resource;
            Text = text;
            IsMet = isMet;
        }
    }

    internal sealed class CostChecklist
    {
        internal const string Ready = "Ready";
        internal static readonly CostChecklist Unavailable = new CostChecklist(Array.Empty<MaterialCheck>(), "Cost Unavailable");
        internal readonly MaterialCheck[] Items;
        internal readonly string Status;

        internal CostChecklist(MaterialCheck[] items, string status)
        {
            Items = items;
            Status = status;
        }
    }

    internal sealed class PinTarget
    {
        internal readonly Component Source;
        internal readonly string Title;
        internal readonly int Level;
        internal readonly Func<ResourceAmount[]> Price;
        private readonly Func<bool> complete;
        private readonly Func<ResourceAmount[], bool> permitted;
        private readonly string operation;
        private readonly object definition;
        internal string Operation => operation;
        internal object Definition => definition;
        private readonly Func<ResourceType, int> readStock;
        private readonly bool honorFreeBuilding;
        private readonly bool purchaseChecksStock;

        internal PinTarget(Component source, string name, Func<ResourceAmount[]> price, int level = 0,
            Func<ResourceAmount[], bool> permitted = null, Func<bool> complete = null, string operation = "",
            Func<ResourceType, int> readStock = null, object definition = null,
            bool? honorFreeBuilding = null)
        {
            Source = source;
            Level = level;
            Price = price;
            this.permitted = permitted;
            this.operation = operation;
            this.definition = definition;
            this.readStock = readStock;
            this.honorFreeBuilding = honorFreeBuilding ?? (readStock == null);
            purchaseChecksStock = readStock == null;
            this.complete = complete;
            Title = level == 0 ? name : name + " · Lv " + level;
        }

        internal bool Matches(Component source, int level, string action = "") =>
            ReferenceEquals(Source, source) && Level == level && operation == action;

        internal bool IsSameOffer(PinTarget other) => other != null &&
            Matches(other.Source, other.Level, other.operation) && ReferenceEquals(definition, other.definition);

        internal const string ReplaceLabel = "Replace Pin";
        internal string Action(PinTarget pinned) => pinned == null ? "Pin Cost" :
            IsSameOffer(pinned) ? "Unpin" : ReplaceLabel;

        internal PinTarget Toggle(PinTarget pinned) =>
            IsSameOffer(pinned) ? null : this;

        internal bool CompletedUpgrade(Building source, int achieved) =>
            operation == "" && Level > 0 && ReferenceEquals(Source, source) && achieved >= Level;

        internal bool IsFinished() => complete?.Invoke() == true ||
            (operation == "" && Level > 0 && Source is Building building && building.GetUpgradeAmount() >= Level);

        internal CostChecklist Read(bool free, Func<ResourceType, int> stock, Func<ResourceType, string> name) =>
            Describe(Price(), readStock ?? stock, name, free && honorFreeBuilding, permitted,
                purchaseChecksStock && (!free || honorFreeBuilding));

        internal static CostChecklist Describe(ResourceAmount[] price, Func<ResourceType, int> stock,
            Func<ResourceType, string> name, bool free = false, Func<ResourceAmount[], bool> permitted = null,
            bool purchaseChecksStock = true)
        {
            if (price == null) return CostChecklist.Unavailable;
            var totals = new Dictionary<ResourceType, long>();
            foreach (var part in price)
            {
                if (part.amount < 0) return CostChecklist.Unavailable;
                if (part.amount == 0) continue;
                totals.TryGetValue(part.type, out var amount);
                totals[part.type] = amount + part.amount;
            }

            var items = new MaterialCheck[totals.Count];
            int index = 0;
            bool allMet = true;
            foreach (var entry in totals)
            {
                long missing = free ? 0 : entry.Value - stock(entry.Key);
                bool met = missing <= 0;
                allMet &= met;
                string amount = free ? "Free" : met ? $"{entry.Value}/{entry.Value}" : $"{missing} short";
                items[index++] = new MaterialCheck(entry.Key, name(entry.Key) + " · " + amount, met);
            }
            // Native purchases can accept separate entries for the same resource. Keep
            // the total visible, but let their actual purchase predicate decide Ready.
            bool ready = permitted == null ? allMet : (purchaseChecksStock || allMet) && permitted(price);
            string status = ready ? CostChecklist.Ready : allMet ? "Requirements Pending" : null;
            return new CostChecklist(items, status);
        }
    }
}

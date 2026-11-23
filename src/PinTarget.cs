using System;
using System.Collections.Generic;
using System.Text;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;

namespace PinABuildCost
{
    internal sealed class PinTarget
    {
        internal readonly Building Source;
        internal readonly string Title;
        internal readonly int Level;
        internal readonly Func<ResourceAmount[]> Price;

        internal PinTarget(Building source, string name, Func<ResourceAmount[]> price, int level = 0)
        {
            Source = source;
            Level = level;
            Price = price;
            Title = level == 0 ? name : name + " · Lv " + level;
        }

        internal bool Matches(Building source, int level) => ReferenceEquals(Source, source) && Level == level;

        internal string Action(PinTarget pinned) => pinned == null ? "Pin cost" :
            Matches(pinned.Source, pinned.Level) ? "Unpin" : "Replace pin";

        internal PinTarget Toggle(PinTarget pinned) =>
            pinned != null && Matches(pinned.Source, pinned.Level) ? null : this;

        internal bool CompletedUpgrade(Building source, int achieved) =>
            Level > 0 && ReferenceEquals(Source, source) && achieved >= Level;

        internal static string Describe(ResourceAmount[] price, Func<ResourceType, int> stock,
            Func<ResourceType, string> name)
        {
            if (price == null) return "Cost unavailable";
            var totals = new Dictionary<ResourceType, long>();
            foreach (var part in price)
            {
                if (part.amount < 0) return "Cost unavailable";
                if (part.amount == 0) continue;
                totals.TryGetValue(part.type, out var amount);
                totals[part.type] = amount + part.amount;
            }

            var lines = new StringBuilder();
            foreach (var entry in totals)
            {
                long missing = entry.Value - stock(entry.Key);
                if (missing <= 0) continue;
                if (lines.Length > 0) lines.Append('\n');
                lines.Append(name(entry.Key)).Append(" · ").Append(missing).Append(" short");
            }
            return lines.Length == 0 ? "Ready" : lines.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;
using SuperFantasyKingdom.Clickable;
using SuperFantasyKingdom.UI;
using UnityEngine;
using UnityEngine.Localization;
using Object = UnityEngine.Object;

namespace PinABuildCost
{
    // Serializable identity only. A price, inventory or scene object from yesterday
    // is never an input to today's HUD.
    internal sealed class CheckpointOffer
    {
        public string kind, asset, component, choice;
        public int tier, buildingType, size, unitId;
        public float x, y;
        public bool prefabOwner;

        private static string Asset(string name) => name.Replace("(Clone)", "").Trim();
        private static string TypeId(object value) => value.GetType().Assembly.GetName().Name + "/" + value.GetType().FullName;
        private static LocalizedString Text(object offer, string field) =>
            AccessTools.Field(offer.GetType(), field)?.GetValue(offer) as LocalizedString;
        private static string TextKey(LocalizedString text) => text == null || text.IsEmpty ? "" :
            text.TableReference.ToString() + "/" + text.TableEntryReference.ToString();
        private static string Choice(UIBuildingOverlayElement offer)
        {
            string key = TypeId(offer);
            if (offer is UIBuildingOverlayElementCityEvent city) return key + "/" + (int)city.eventToTrigger;
            if (offer is UIBuildingOverlayElementPortal portal)
                return key + "/" + AccessTools.FieldRefAccess<UIBuildingOverlayElementPortal, ResourceType>(portal, "resource");
            return key + "/" + TextKey(Text(offer, "stringTitle")) + "/" + TextKey(Text(offer, "stringDescription"));
        }

        internal static CheckpointOffer Capture(PinTarget target)
        {
            if (target?.Source == null || target.IsFinished()) return null;
            var source = target.Source;
            var record = new CheckpointOffer { kind = target.Operation, tier = target.Level,
                component = TypeId(source), asset = Asset(source.name) };
            Vector2 position = source.transform.position;
            if (source is BuildingCity city)
            {
                record.buildingType = (int)city.GetBuildingType();
                record.size = (int)city.GetBuildingSize();
                if (record.IsCustom) record.prefabOwner = !city.gameObject.scene.IsValid();
                if (target.Operation == "" && target.Level == 0) record.kind = "build";
                if (target.Operation == "" && target.Level > 0) record.kind = "upgrade";
            }
            if (target.Definition is UIBuildingOverlayElement offer) record.choice = Choice(offer);
            if (source is Unit unit && target.Definition is GameObject plot)
            {
                record.unitId = unit.GetIdentifier();
                record.asset = unit.GetEntityIdentifier();
                record.choice = Asset(plot.name);
                position = plot.transform.position;
            }
            if (record.kind == "world") record.choice = WorldKey(source);
            record.x = position.x;
            record.y = position.y;
            return record;
        }

        internal static T Unique<T>(IEnumerable<T> candidates) where T : class
        {
            using (var enumerator = candidates.GetEnumerator())
            {
                if (!enumerator.MoveNext()) return null;
                var match = enumerator.Current;
                return enumerator.MoveNext() ? null : match;
            }
        }

        internal bool At(string name, Vector2 position) =>
            Asset(name) == asset && Mathf.Abs(position.x - x) < 0.001f && Mathf.Abs(position.y - y) < 0.001f;

        private bool SameBuilding(BuildingCity city) => city != null && TypeId(city) == component &&
            (int)city.GetBuildingType() == buildingType && (int)city.GetBuildingSize() == size && Asset(city.name) == asset;

        internal bool IsCustom => kind?.StartsWith("mod:", StringComparison.Ordinal) == true;

        internal Component FindCustomOwner()
        {
            if (prefabOwner)
                return Unique(RaceManager.Instance.GetBuildings((BuildingSize)size).Where(go => go != null)
                    .Select(go => go.GetComponent<BuildingCity>()).Where(SameBuilding).Distinct());
            var parts = component?.Split(new[] { '/' }, 2);
            if (parts == null || parts.Length != 2) return null;
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == parts[0]);
            var type = assembly?.GetType(parts[1]);
            if (type == null || !typeof(Component).IsAssignableFrom(type)) return null;
            return Unique(Object.FindObjectsOfType(type).OfType<Component>().Where(c =>
                TypeId(c) == component && At(c.name, c.transform.position) && (!(c is BuildingCity city) || SameBuilding(city))));
        }

        internal PinTarget Resolve()
        {
            if (kind == "world") return ResolveWorld();
            if (kind == "evolution")
            {
                var plot = RaceManager.Instance.GetBuildingButton(new Vector2(x, y));
                if (plot == null || !plot.gameObject.activeInHierarchy || Asset(plot.name) != choice ||
                    (plot.GetComponent<BuildingButton>()?.size != BuildingSize.Evolution &&
                     plot.GetComponent<BuildingSpotWorld>()?.size != BuildingSize.Evolution)) return null;
                var unit = Unique(UnitManager.Instance.GetAllUnits().OfType<Unit>().Where(u =>
                    u.GetIdentifier() == unitId && u.GetEntityIdentifier() == asset && u.GetRank() == 2 &&
                    u.HasRanks() && u.GetRace() != RaceManager.Instance.GetRace()));
                if (unit == null) return null;
                var evolved = AddressablesManager.Instance.GetUnit(unit.GetEntityIdentifier() + "3");
                return EvolutionCards.ForPlot(unit, plot.gameObject, evolved != null ? evolved.GetName() : unit.GetName());
            }
            if (kind == "build")
            {
                var roster = RaceManager.Instance.GetBuildings((BuildingSize)size);
                return ResolveDesign(roster.Where(go => go != null).Select(go => go.GetComponent<BuildingCity>()));
            }
            if (kind != "upgrade" && kind != "castle" && kind != "capacity") return null;
            var city = LocateBuilding(CityManager.Instance.GetBuildings().Concat(CityManager.Instance.GetOutposts()));
            var buttons = city == null ? null : UpgradeCards.Overlay(city)?.buttons;
            return buttons == null ? null : ResolveChoice(city, buttons);
        }

        internal BuildingCity LocateBuilding(IEnumerable<BuildingCity> buildings) =>
            Unique(buildings.Distinct().Where(c => SameBuilding(c) && !c.ShouldRemove() && At(c.name, c.transform.position)));

        internal PinTarget ResolveDesign(IEnumerable<BuildingCity> designs)
        {
            var prefab = Unique(designs.Distinct().Where(SameBuilding));
            if (prefab == null || BuildCards.IsBonusList((BuildingSize)size) ||
                (!RaceManager.Instance.GetCanBeBuiltMultipleTimes().Contains(prefab.GetBuildingType()) &&
                 CityManager.Instance.GetBuilding(prefab.GetBuildingType()) != null)) return null;
            return BuildCards.ForPrefab(prefab);
        }

        internal PinTarget ResolveChoice(BuildingCity city, IEnumerable<UIBuildingOverlayElement> buttons)
        {
            var offer = Unique(buttons.Where(b => (b is UIBuildingOverlayElementUpgrade ||
                b is UIBuildingOverlayElementCityEvent || b is UIBuildingOverlayElementPortal) && Choice(b) == choice));
            string Label(string field) => Text(offer, field)?.GetLocalizedString() ?? city.GetTitle();
            PinTarget target = null;
            if (kind == "castle" && city is BuildingCastle && offer is UIBuildingOverlayElementCityEvent evt &&
                evt.eventToTrigger != CityEvent.None && evt.eventToTrigger != CityEvent.Ritual)
            {
                bool storage = evt.eventToTrigger == CityEvent.UpgradeBase1 || evt.eventToTrigger == CityEvent.UpgradeBase2 ||
                    evt.eventToTrigger == CityEvent.UpgradeBase3;
                target = CastleUpgradeCards.ForEvent(city, evt, Label(storage ? "stringDescription" : "stringTitle"));
                if (!(target.Price()?.Length > 0)) return null;
            }
            if (kind == "upgrade" && offer is UIBuildingOverlayElementUpgrade upgrade &&
                tier <= AccessTools.FieldRefAccess<UIBuildingOverlayElementUpgrade, int>(upgrade, "amountOfUpgrades"))
                target = UpgradeCards.ForUpgrade(city, upgrade, Label("stringTitle"));
            if (kind == "capacity" && offer is UIBuildingOverlayElementPortal portal)
                target = CapacityCards.ForCapacity(city, portal, Label("stringChest"));
            return target != null && target.Level == tier && !target.IsFinished() ? target : null;
        }

        private static string WorldKey(Component source)
        {
            if (source is RepairableBuilding repair)
                return Asset(AccessTools.FieldRefAccess<RepairableBuilding, BuildingCity>(repair, "toSpawn").name);
            if (source is BuildingSpotWorld spot)
                return AccessTools.FieldRefAccess<BuildingSpotWorld, ResourceType>(spot, "resource").ToString();
            return "";
        }

        private PinTarget ResolveWorld()
        {
            var sources = Object.FindObjectsOfType<MonoBehaviour>().Where(c => (c is RepairableBuilding ||
                c is BuildingSpotWorld || c is BuildingWorldStartingResource || c is RaceUnlockablePlaceholder ||
                c is BuildingWitch) && TypeId(c) == component && At(c.name, c.transform.position) && WorldKey(c) == choice);
            var site = Unique(sources);
            if (site == null) return null;
            if (site is BuildingSpotWorld spot && spot.size != BuildingSize.WorldSmall) return null;
            if (site is RaceUnlockablePlaceholder &&
                AccessTools.FieldRefAccess<RaceDataManager, RaceData>(RaceDataManager.Instance, "m_RaceData")
                    ?.worldBuildings?.Contains(site.gameObject.name) == true) return null;
            bool witch = site is BuildingWitch;
            int unlocked = witch ? RaceDataManager.Instance.GetBonfiresUnlocked() : 0;
            if (witch && unlocked >= 4) return null;
            var quote = witch ? new ResourceAmount(ResourceType.Stars, unlocked + 12) : new ResourceAmount(ResourceType.Shards, 0);
            var target = WorldBuildOffers.ForSource(site, witch ? "UnlockBonfire" : "Callback", quote);
            return target != null && target.Level == tier && !target.IsFinished() ? target : null;
        }
    }
}

using System;
using BepInEx;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;
using SuperFantasyKingdom.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PinABuildCost
{
    [BepInPlugin("sfk.pinabuildcost", "Pin a Build Cost", "0.1.0")]
    public sealed class PinPlugin : BaseUnityPlugin
    {
        internal static PinPlugin Instance;
        internal PinTarget Pinned { get; private set; }
        internal event Action Changed;
        private Harmony hooks;
        private PinHud hud;
        private float refreshAt;
        private bool priceDirty;
        private ResourceAmount[] price;
        private string lastProblem;
        private PinCardLink hovered;

        private void Awake()
        {
            Instance = this;
            hooks = new Harmony("sfk.pinabuildcost");
            try
            {
                hooks.PatchAll(typeof(BuildCards));
                hooks.PatchAll(typeof(UpgradeCards));
                hooks.PatchAll(typeof(TabFocus));
                hooks.PatchAll(typeof(BuildCompletion));
            }
            catch
            {
                hooks.UnpatchSelf();
                throw;
            }
            GameManager.OnGameInit += Clear;
            CityManager.OnBuildingBuilt += CityChanged;
            CityManager.OnBuildingRemoved += Removed;
            CityManager.OnBuildingUpgrade += Upgraded;
            Logger.LogInfo("Pin a Build Cost 0.1.0 ready.");
        }

        internal static void TryAttach(Action attach)
        {
            try { attach(); }
            catch (Exception ex) { Instance?.Report(ex); }
        }

        private void Report(Exception ex)
        {
            if (lastProblem != ex.Message) Logger.LogWarning(ex);
            lastProblem = ex.Message;
        }

        internal void Hover(PinCardLink link) => hovered = link;
        internal void Leave(PinCardLink link) { if (hovered == link) hovered = null; }

        internal void Toggle(PinTarget target) => Set(target.Toggle(Pinned));

        private void Set(PinTarget target)
        {
            Pinned = target;
            price = null;
            priceDirty = true;
            refreshAt = 0;
            if (hud != null) hud.gameObject.SetActive(target != null);
            Changed?.Invoke();
        }

        private void Clear() => Set(null);
        internal void Built(BuildingCity prefab)
        {
            if (Pinned?.Matches(prefab, 0) == true) Clear();
        }

        private void CityChanged(BuildingCity building, bool fromSave) => priceDirty = true;

        private void Removed(BuildingCity building, bool cancelJobs)
        {
            priceDirty = true;
            if (Pinned != null && Pinned.Level > 0 && Pinned.Source == building) Clear();
        }

        private void Upgraded(BuildingCity building, bool fromSave)
        {
            priceDirty = true;
            if (!fromSave && Pinned?.CompletedUpgrade(building, building.GetUpgradeAmount()) == true) Clear();
        }

        private void LateUpdate()
        {
            var pad = GamepadManager.Instance;
            if (pad != null && OverlayManager.Instance != null && OverlayManager.Instance.IsOpen())
            {
                var selected = EventSystem.current?.currentSelectedGameObject;
                var link = !pad.IsGamepadInUse() && hovered != null && hovered.isActiveAndEnabled
                    ? hovered : selected == null ? null : selected.GetComponent<PinCardLink>();
                if (link != null && link.isActiveAndEnabled && link.Row != null && link.Row.isActiveAndEnabled)
                    link.Row.CheckShortcut(pad);
            }
            if (Pinned == null || Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + 0.25f;
            if (Pinned.Source == null || GameManager.Instance == null || GameManager.Instance.IsUnloading())
            {
                Clear();
                return;
            }
            if (ResourceManager.Instance == null || RaceManager.Instance == null) return;
            try
            {
                if (hud == null)
                {
                    var header = FindObjectOfType<UIHeader>();
                    if (header != null) hud = PinHud.Create(header);
                    if (hud == null) return;
                }
                if (Pinned.CompletedUpgrade(Pinned.Source, Pinned.Source.GetUpgradeAmount()))
                {
                    Clear();
                    return;
                }
                if (priceDirty)
                {
                    price = Pinned.Price();
                    priceDirty = false;
                }
                hud.Show(Pinned.Title, ResourceManager.Instance.IsEverythingFree() ? "Ready" : PinTarget.Describe(price,
                    type => ResourceManager.Instance.GetResourceAvailableAmount(type), ResourceName));
            }
            catch (Exception ex)
            {
                hud?.Show(Pinned.Title, "Cost unavailable");
                Report(ex);
                refreshAt = Time.unscaledTime + 1;
            }
        }

        private static string ResourceName(ResourceType type)
        {
            var data = RaceManager.Instance.GetResourceData(type);
            return data != null && data.type == type ? data.GetName() : type.ToString();
        }

        private void OnDestroy()
        {
            GameManager.OnGameInit -= Clear;
            CityManager.OnBuildingBuilt -= CityChanged;
            CityManager.OnBuildingRemoved -= Removed;
            CityManager.OnBuildingUpgrade -= Upgraded;
            hooks?.UnpatchSelf();
            if (hud != null) Destroy(hud.gameObject);
            Instance = null;
        }
    }
}

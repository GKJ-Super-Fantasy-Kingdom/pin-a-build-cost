using System;
using System.Collections.Generic;
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
        private string lastProblem;
        private PinCardLink hovered;
        private readonly List<PinRow> visibleRows = new List<PinRow>();
        private PinRow shortcutRow;
        private int lastActivationFrame = -1;
        private static readonly AccessTools.FieldRef<OverlayManager, GameObject> ActivePanel =
            AccessTools.FieldRefAccess<OverlayManager, GameObject>("m_CurrentlyActive");

        private void Awake()
        {
            Instance = this;
            hooks = new Harmony("sfk.pinabuildcost");
            try
            {
                hooks.PatchAll(typeof(BuildCards));
                hooks.PatchAll(typeof(UpgradeCards));
                hooks.PatchAll(typeof(CastleUpgradeCards));
                hooks.PatchAll(typeof(TabFocus));
                hooks.PatchAll(typeof(BuildCompletion));
                hooks.PatchAll(typeof(WorldBuildOffers));
                hooks.PatchAll(typeof(WorldUnlockCompletion));
                hooks.PatchAll(typeof(CapacityCards));
                hooks.PatchAll(typeof(EvolutionPlot));
                hooks.PatchAll(typeof(EvolutionCards));
            }
            catch
            {
                hooks.UnpatchSelf();
                throw;
            }
            GameManager.OnGameInit += Clear;
            CityManager.OnBuildingRemoved += Removed;
            CityManager.OnBuildingUpgrade += Upgraded;
            Logger.LogInfo("Pin a Build Cost 0.1.0 ready.");
        }

        internal static void TryAttach(Action attach)
        {
            try { attach(); }
            catch (Exception ex) { Instance?.Report(ex); }
        }

        internal static List<CodeInstruction> UnsupportedLayout(List<CodeInstruction> original, string screen)
        {
            Instance?.Logger.LogWarning("Unrecognized " + screen + " card layout. Automatic pin actions are unavailable on this screen.");
            return original;
        }

        private void Report(Exception ex)
        {
            if (lastProblem != ex.Message) Logger.LogWarning(ex);
            lastProblem = ex.Message;
        }

        internal void Hover(PinCardLink link) => hovered = link;
        internal void Leave(PinCardLink link) { if (hovered == link) hovered = null; }

        internal void Toggle(PinTarget target) => Set(target.Toggle(Pinned));

        internal void Activate(PinTarget target)
        {
            if (lastActivationFrame == Time.frameCount) return;
            lastActivationFrame = Time.frameCount;
            Toggle(target);
        }

        internal void Register(PinRow row) => visibleRows.Add(row);
        internal void Unregister(PinRow row) => visibleRows.Remove(row);

        private PinRow ResolveShortcut(GamepadManager pad)
        {
            var overlays = OverlayManager.Instance;
            if (visibleRows.Count == 0 || pad == null || overlays == null || !overlays.IsOpen() ||
                (PauseMenuManger.Instance != null && PauseMenuManger.Instance.InMenu())) return null;
            var panel = ActivePanel(overlays);
            if (panel == null || !panel.activeInHierarchy) return null;
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy && !selected.transform.IsChildOf(panel.transform))
                return null;
            var focused = selected == null ? null : selected.GetComponentInParent<PinCardLink>()?.Row;
            var underMouse = hovered != null && !pad.IsGamepadInUse() ? hovered.Row : null;
            int count = 0;
            PinRow only = null, pointer = null;
            foreach (var row in visibleRows)
            {
                if (!row.HasVisibleOffer || !row.transform.IsChildOf(panel.transform)) continue;
                if (row == focused) return row;
                if (row == underMouse) pointer = row;
                only = row;
                count++;
            }
            return pointer != null ? pointer : count == 1 ? only : null;
        }

        internal void RefreshOffer(PinTarget target)
        {
            if (Pinned != null && target.IsSameOffer(Pinned)) Set(target);
        }

        private void Set(PinTarget target)
        {
            Pinned = target;
            refreshAt = 0;
            if (hud != null) hud.gameObject.SetActive(target != null);
            Changed?.Invoke();
        }

        private void Clear() => Set(null);
        internal void Built(BuildingCity prefab)
        {
            if (Pinned?.Matches(prefab, 0) == true) Clear();
        }

        internal void Completed(Component source, string operation)
        {
            if (Pinned?.Matches(source, 0, operation) == true) Clear();
        }

        private void Removed(BuildingCity building, bool cancelJobs)
        {
            if (Pinned != null && ReferenceEquals(Pinned.Source, building)) Clear();
        }

        private void Upgraded(BuildingCity building, bool fromSave)
        {
            if (!fromSave && Pinned?.CompletedUpgrade(building, building.GetUpgradeAmount()) == true) Clear();
        }

        private void LateUpdate()
        {
            var pad = GamepadManager.Instance;
            var next = ResolveShortcut(pad);
            if (next != shortcutRow)
            {
                if (shortcutRow != null) shortcutRow.SetShortcutHint(false);
                shortcutRow = next;
                if (next != null) next.SetShortcutHint(true);
            }
            if (next != null) next.CheckShortcut(pad);
            if (Pinned == null || Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + 0.5f;
            if (Pinned.Source == null || GameManager.Instance == null || GameManager.Instance.IsUnloading())
            {
                Clear();
                return;
            }
            if (ResourceManager.Instance == null || RaceManager.Instance == null) return;
            try
            {
                if (Pinned.IsFinished())
                {
                    Clear();
                    return;
                }
                if (hud == null)
                {
                    var header = FindObjectOfType<UIHeader>();
                    if (header != null) hud = PinHud.Create(header);
                    if (hud == null) return;
                }
                hud.Show(Pinned.Title, Pinned.Read(ResourceManager.Instance.IsEverythingFree(),
                    type => ResourceManager.Instance.GetResourceAvailableAmount(type), ResourceName));
            }
            catch (Exception ex)
            {
                hud?.Show(Pinned.Title, CostChecklist.Unavailable);
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
            CityManager.OnBuildingRemoved -= Removed;
            CityManager.OnBuildingUpgrade -= Upgraded;
            hooks?.UnpatchSelf();
            if (hud != null) Destroy(hud.gameObject);
            Instance = null;
        }
    }
}

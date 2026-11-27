using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Newtonsoft.Json;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Tavern;
using UnityEngine;

namespace PinABuildCost
{
    internal static class PinCheckpoints
    {
        private const string Key = "pinABuildCostV1";
        private sealed class Stamp
        {
            public string runID, hash;
            public int? day;
            public Races? kingdom;
            public CheckpointOffer offer;
        }
        private sealed class Envelope
        {
            public string runID = null, hash = null;
            public int? day = null;
            public Races? kingdom = null;
            [JsonProperty(Key)] public Stamp Pin = null;
        }

        // Both native loaders clear hash after reading. Keep the extension with
        // the exact GameData object through that change and a tavern rewrite.
        private static readonly ConditionalWeakTable<GameData, CheckpointOffer> Loaded =
            new ConditionalWeakTable<GameData, CheckpointOffer>();

        internal static CheckpointOffer Read(string json, GameData data)
        {
            if (data == null || json == null || json.IndexOf("\"" + Key + "\"", StringComparison.Ordinal) < 0) return null;
            try
            {
                var pin = JsonConvert.DeserializeObject<Envelope>(json)?.Pin;
                return pin?.offer != null && !string.IsNullOrEmpty(pin.runID) && pin.runID == data.runID &&
                    pin.kingdom == data.kingdom && pin.day == data.day && pin.hash == data.hash ? pin.offer : null;
            }
            catch (JsonException) { return null; }
        }

        internal static string Write(string json, string run, Races kingdom, CheckpointOffer offer)
        {
            if (offer == null || string.IsNullOrEmpty(run) || string.IsNullOrEmpty(json)) return json;
            var header = JsonConvert.DeserializeObject<Envelope>(json);
            if (header?.runID != run || header.kingdom != kingdom || !header.day.HasValue) return json;
            var pin = new Stamp { runID = run, kingdom = kingdom, day = header.day.Value, hash = header.hash, offer = offer };
            // Preserve native field order, numbers, checksum and other mods' extensions.
            return json.Insert(json.LastIndexOf('}'), ",\"" + Key + "\":" + JsonConvert.SerializeObject(pin));
        }

        internal static CheckpointOffer For(GameData data) =>
            data != null && Loaded.TryGetValue(data, out var offer) ? offer : null;

        [HarmonyPatch(typeof(JsonUtility), nameof(JsonUtility.FromJson), typeof(string), typeof(Type))]
        internal static class ReadGameData
        {
            internal static void Postfix(string __0, object __result)
            {
                if (__result is GameData data && Read(__0, data) is CheckpointOffer pin)
                    Loaded.GetValue(data, _ => pin);
            }
        }

        [HarmonyPatch(typeof(GameDataManager), nameof(GameDataManager.Load))]
        internal static class LoadKingdom
        {
            private static void Postfix(GameDataManager __instance, GameData ___m_GameData)
            {
                PinPlugin.Instance?.QueueRestore(!__instance.IsFirstLoad() && ___m_GameData != null &&
                    ___m_GameData.runID == __instance.GetRunID() && ___m_GameData.kingdom == RaceManager.Instance.GetRace()
                    ? For(___m_GameData) : null);
            }
        }

        internal sealed class SaveContext
        {
            internal string File, Run;
            internal Races Kingdom;
            internal CheckpointOffer Pin;
        }
        [ThreadStatic] private static SaveContext writing;

        [HarmonyPatch]
        internal static class SaveScope
        {
            private static MethodBase[] TargetMethods() => new[] {
                AccessTools.Method(typeof(GameDataManager), nameof(GameDataManager.Save)),
                AccessTools.Method(typeof(TavernSaveManager), nameof(TavernSaveManager.Save)) };

            private static void Prefix(object __instance, out SaveContext __state)
            {
                __state = writing;
                writing = null;
                PinPlugin.TryAttach(() => {
                    var current = new SaveContext();
                    if (__instance is TavernSaveManager tavern)
                    {
                        var data = tavern.GetGameData();
                        if (data == null) return;
                        current.Run = data.runID;
                        current.Kingdom = data.kingdom;
                        current.Pin = For(data);
                    }
                    else
                    {
                        current.Pin = PinPlugin.Instance?.Snapshot();
                        if (current.Pin == null) return;
                        current.Run = ((GameDataManager)__instance).GetRunID();
                        current.Kingdom = RaceManager.Instance.GetRace();
                    }
                    current.File = (string)AccessTools.Method(__instance.GetType(), "GetPath").Invoke(__instance, null);
                    writing = current;
                });
            }

            private static void Finalizer(SaveContext __state) => writing = __state;
        }

        [HarmonyPatch]
        internal static class WriteGameData
        {
            private static MethodBase TargetMethod()
            {
                var manager = typeof(SaveManager);
                const string method = nameof(SaveManager.Save);
                // 0.8.070 adds a backup flag. Both paths still write the current checkpoint.
                return AccessTools.Method(manager, method, new[] { typeof(string), typeof(string), typeof(bool) })
                    ?? AccessTools.Method(manager, method, new[] { typeof(string), typeof(string) });
            }

            private static void Prefix(string __0, ref string __1)
            {
                if (writing?.Pin == null || writing.File != __0) return;
                var json = __1;
                PinPlugin.TryAttach(() => json = Write(json, writing.Run, writing.Kingdom, writing.Pin));
                __1 = json;
            }
        }
    }
}

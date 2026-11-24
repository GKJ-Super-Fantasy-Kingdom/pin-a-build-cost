using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

// The headless runtime has no scene. Keep only the engine-owned state needed
// to exercise the shipped pin, lifecycle callbacks and binding policy.
internal static class SceneFixture
{
    private static readonly Dictionary<Component, GameObject> owners = new Dictionary<Component, GameObject>();
    private static readonly HashSet<GameObject> inactive = new HashSet<GameObject>();
    internal static readonly HashSet<GameObject> HiddenByParent = new HashSet<GameObject>();
    internal static readonly HashSet<Object> Destroyed = new HashSet<Object>();
    internal static readonly Dictionary<Object, string> Names = new Dictionary<Object, string>();
    internal static readonly Dictionary<Component, Transform> Locations = new Dictionary<Component, Transform>();
    internal static readonly Dictionary<Transform, Vector3> Positions = new Dictionary<Transform, Vector3>();
    internal static string Name(Object obj) => Names[obj];
    internal static Transform Location(Component component) => Locations[component];
    internal static void Position(Transform transform, out Vector3 value) => value = Positions[transform];
    internal static object FromJson(string json, object destination, Type type) => JsonConvert.DeserializeObject(json, type);
    internal static string ToJson(object data, bool pretty) => CheckpointChecks.NativeJson((SuperFantasyKingdom.GameData)data);
    internal static int Frame;
    internal static int FrameCount() => Frame;

    internal static T New<T>(GameObject owner = null) where T : Object
    {
        var result = (T)FormatterServices.GetUninitializedObject(typeof(T));
        // This player version checks the cached pointer for Unity's destroyed-object null.
        AccessTools.Field(typeof(Object), "m_CachedPtr").SetValue(result, new IntPtr(1));
        if (result is Component component) owners.Add(component, owner);
        return result;
    }

    internal static GameObject Owner(Component component) => owners[component];
    internal static bool Active(GameObject obj) => !inactive.Contains(obj);
    internal static bool HierarchyActive(GameObject obj) => Active(obj) && !HiddenByParent.Contains(obj);
    internal static bool Enabled(Behaviour component) => HierarchyActive(Owner(component));
    internal static void SetActive(GameObject obj, bool value)
    {
        if (value) inactive.Remove(obj);
        else inactive.Add(obj);
    }
    internal static void Destroy(Object obj, float delay) => Destroyed.Add(obj);
}

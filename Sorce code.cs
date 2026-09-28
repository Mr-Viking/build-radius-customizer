using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System;

namespace BuildRadiusCustomizer
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class BuildRadiusCustomizerPlugin : BaseUnityPlugin
    {
        private const string PluginGUID = "Viking.buildradiuscustomizer";
        private const string PluginName = "BuildRadiusCustomizer";
        private const string PluginVersion = "1.2.0";

        private readonly Harmony harmony = new Harmony(PluginGUID);

        // Configuration Entries
        public static ConfigEntry<bool> IsConfigLocked;
        public static ConfigEntry<bool> ModEnabled;

        private static ConfigEntry<float> _workbenchRadius;
        private static ConfigEntry<float> _stonecutterRadius;
        private static ConfigEntry<float> _forgeRadius;
        private static ConfigEntry<float> _blackForgeRadius;
        private static ConfigEntry<float> _galdrTableRadius;

        // Smart Properties
        public static float WorkbenchRadius => ModEnabled.Value ? _workbenchRadius.Value : 20f;
        public static float StonecutterRadius => ModEnabled.Value ? _stonecutterRadius.Value : 20f;
        public static float ForgeRadius => ModEnabled.Value ? _forgeRadius.Value : 20f;
        public static float BlackForgeRadius => ModEnabled.Value ? _blackForgeRadius.Value : 20f;
        public static float GaldrTableRadius => ModEnabled.Value ? _galdrTableRadius.Value : 20f;

        private void Awake()
        {
            // 0 - General Settings
            IsConfigLocked = Config.Bind("0 - General", "Lock Configuration", true,
                new ConfigDescription("If true, configuration settings will be locked to server-side values via ConditionalConfigSync for non-admin players.", null, new { order = 210 }));

            ModEnabled = BindConfig("0 - General", "Mod Enabled", true, "If false, all custom build radius adjustments are ignored and game default values are used. [Synced with Server]", 200);

            // 1 - Radius Settings
            _workbenchRadius = BindConfig("1 - Radius Settings", "Workbench Radius", 20f, "Maximum building radius for the Workbench. (Vanilla default: 20) [Synced with Server]", 100);
            _stonecutterRadius = BindConfig("1 - Radius Settings", "Stonecutter Radius", 20f, "Maximum building radius for the Stonecutter. (Vanilla default: 20) [Synced with Server]", 99);
            _forgeRadius = BindConfig("1 - Radius Settings", "Forge Radius", 20f, "Maximum building radius for the Forge. (Vanilla default: 20) [Synced with Server]", 98);
            _blackForgeRadius = BindConfig("1 - Radius Settings", "Black Forge Radius", 20f, "Maximum building radius for the Black Forge. (Vanilla default: 20) [Synced with Server]", 97);
            _galdrTableRadius = BindConfig("1 - Radius Settings", "Galdr Table Radius", 20f, "Maximum building radius for the Galdr Table. (Vanilla default: 20) [Synced with Server]", 96);

            harmony.PatchAll();
            Logger.LogInfo($"{PluginName} version {PluginVersion} loaded successfully.");
        }

        private ConfigEntry<T> BindConfig<T>(string group, string name, T value, string description, int order)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, new ConfigDescription(description, null, new { order }));
            configEntry.SettingChanged += OnSettingChanged;
            return configEntry;
        }

        private void OnSettingChanged(object sender, EventArgs e)
        {
            UpdateActiveStationRanges();
        }

        public static void UpdateActiveStationRanges()
        {
            if (Player.m_localPlayer == null) return;

            CraftingStation[] stations = FindObjectsByType<CraftingStation>(FindObjectsSortMode.None);
            foreach (CraftingStation station in stations)
            {
                ZNetView nview = station.GetComponent<ZNetView>();
                if (nview && nview.IsValid())
                {
                    float targetRadius = GetTargetRadius(station.m_name);
                    if (targetRadius > 0f)
                    {
                        station.m_rangeBuild = targetRadius;
                    }
                }
            }
        }

        public static float GetTargetRadius(string stationName)
        {
            switch (stationName)
            {
                case "$piece_workbench":
                    return WorkbenchRadius;
                case "$piece_stonecutter":
                    return StonecutterRadius;
                case "$piece_forge":
                    return ForgeRadius;
                case "$piece_blackforge":
                    return BlackForgeRadius;
                case "$piece_magictable":
                    return GaldrTableRadius;
                default:
                    return -1f;
            }
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }
    }

    [HarmonyPatch(typeof(CraftingStation), "Awake")]
    public static class CraftingStation_Awake_Patch
    {
        public static void Postfix(CraftingStation __instance)
        {
            float targetRadius = BuildRadiusCustomizerPlugin.GetTargetRadius(__instance.m_name);
            if (targetRadius > 0f)
            {
                __instance.m_rangeBuild = targetRadius;
            }
        }
    }
}


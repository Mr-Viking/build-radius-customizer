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
        private const string PluginGUID = "com.Viking.valheim.buildradiuscustomizer";
        private const string PluginName = "BuildRadiusCustomizer";
        private const string PluginVersion = "1.1.0";

        private readonly Harmony harmony = new Harmony(PluginGUID);

        // Configuration Entries
        public static ConfigEntry<float> WorkbenchRadius;
        public static ConfigEntry<float> StonecutterRadius;
        public static ConfigEntry<float> ForgeRadius;
        public static ConfigEntry<float> BlackForgeRadius;
        public static ConfigEntry<float> GaldrTableRadius;

        private void Awake()
        {
            // Bind configs with settings changed listener for real-time updates
            WorkbenchRadius = Config.Bind("1 - Radius Settings", "Workbench Radius", 20f, "Maximum building radius for the Workbench. (Vanilla default: 20)");
            WorkbenchRadius.SettingChanged += OnSettingChanged;

            StonecutterRadius = Config.Bind("1 - Radius Settings", "Stonecutter Radius", 20f, "Maximum building radius for the Stonecutter. (Vanilla default: 20)");
            StonecutterRadius.SettingChanged += OnSettingChanged;

            ForgeRadius = Config.Bind("1 - Radius Settings", "Forge Radius", 20f, "Maximum building radius for the Forge. (Vanilla default: 20)");
            ForgeRadius.SettingChanged += OnSettingChanged;

            BlackForgeRadius = Config.Bind("1 - Radius Settings", "Black Forge Radius", 20f, "Maximum building radius for the Black Forge. (Vanilla default: 20)");
            BlackForgeRadius.SettingChanged += OnSettingChanged;

            GaldrTableRadius = Config.Bind("1 - Radius Settings", "Galdr Table Radius", 20f, "Maximum building radius for the Galdr Table. (Vanilla default: 20)");
            GaldrTableRadius.SettingChanged += OnSettingChanged;

            // Apply patches
            harmony.PatchAll();
            Logger.LogInfo($"{PluginName} version {PluginVersion} loaded successfully.");
        }

        private void OnSettingChanged(object sender, EventArgs e)
        {
            // Forces Valheim to update active station ranges in real-time
            UpdateActiveStationRanges();
        }

        public static void UpdateActiveStationRanges()
        {
            if (Player.m_localPlayer == null) return;

            // Find all active pieces in loading distance and update live bounds
            CraftingStation[] stations = FindObjectsOfType<CraftingStation>();
            foreach (CraftingStation station in stations)
            {
                if (station.m_nview && station.m_nview.IsValid())
                {
                    float targetRadius = GetTargetRadius(station.m_name);
                    if (targetRadius > 0f)
                    {
                        station.m_range = targetRadius;
                    }
                }
            }
        }

        public static float GetTargetRadius(string stationName)
        {
            // Internal technical names for Valheim's building structures
            switch (stationName)
            {
                case "$piece_workbench":
                    return WorkbenchRadius.Value;
                case "$piece_stonecutter":
                    return StonecutterRadius.Value;
                case "$piece_forge":
                    return ForgeRadius.Value;
                case "$piece_blackforge":
                    return BlackForgeRadius.Value;
                case "$piece_magictable": // Internal ID for Galdr Table
                    return GaldrTableRadius.Value;
                default:
                    return -1f;
            }
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }
    }

    // Harmony Patch to modify the radius when a station instantiates or loads into the simulation loop
    [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.Start))]
    public static class CraftingStation_Start_Patch
    {
        public static void Postfix(CraftingStation __instance)
        {
            float targetRadius = BuildRadiusCustomizerPlugin.GetTargetRadius(__instance.m_name);
            if (targetRadius > 0f)
            {
                __instance.m_range = targetRadius;
            }
        }
    }
}

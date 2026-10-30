using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using ServerSync;

namespace BuildRadiusCustomizer
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class BuildRadiusCustomizerPlugin : BaseUnityPlugin
    {
        private const string PluginGUID = "Viking.buildradiuscustomizer";
        private const string PluginName = "BuildRadiusCustomizer";
        private const string PluginVersion = "1.4.0";

        private readonly Harmony harmony = new Harmony(PluginGUID);

        // Initialize ServerSync ConfigSync Instance
        public static readonly ConfigSync ConfigSync = new ConfigSync(PluginGUID)
        {
            DisplayName = PluginName,
            CurrentVersion = PluginVersion,
            MinimumRequiredVersion = PluginVersion
        };

        // Configuration Entries
        public static ConfigEntry<bool> IsConfigLocked;
        public static ConfigEntry<bool> ModEnabled;

        private static ConfigEntry<float> _workbenchRadius;
        private static ConfigEntry<float> _stonecutterRadius;
        private static ConfigEntry<float> _forgeRadius;
        private static ConfigEntry<float> _blackForgeRadius;
        private static ConfigEntry<float> _galdrTableRadius;

        // Configuration Entries for Spawning Settings
        private static ConfigEntry<float> _mobSpawnSuppression;
        private static ConfigEntry<float> _blockSpawnRadius;

        // Smart Properties
        public static float WorkbenchRadius => ModEnabled.Value ? _workbenchRadius.Value : 20f;
        public static float StonecutterRadius => ModEnabled.Value ? _stonecutterRadius.Value : 20f;
        public static float ForgeRadius => ModEnabled.Value ? _forgeRadius.Value : 20f;
        public static float BlackForgeRadius => ModEnabled.Value ? _blackForgeRadius.Value : 20f;
        public static float GaldrTableRadius => ModEnabled.Value ? _galdrTableRadius.Value : 20f;

        // Smart Properties for Spawning
        public static float MobSpawnSuppression => ModEnabled.Value ? _mobSpawnSuppression.Value : 0f;
        public static float BlockSpawnRadius => ModEnabled.Value ? _blockSpawnRadius.Value : 20f;

        private void Awake()
        {
            // 0 - General Settings
            IsConfigLocked = Config.Bind("0 - General", "Lock Configuration", true,
                new ConfigDescription("If true, configuration settings will be locked to server-side values via ConditionalConfigSync for non-admin players.", null, new { order = 210 }));

            // Link the lock property to ServerSync to force server authority enforcement
            ConfigSync.AddLockingConfigEntry(IsConfigLocked);

            ModEnabled = Config.Bind("0 - General", "Mod Enabled", true, new ConfigDescription("If false, all custom adjustments are ignored. [Synced with Server]", null, new { order = 200 }));
            ModEnabled.SettingChanged += OnSettingChanged;
            ConfigSync.AddConfigEntry(ModEnabled).SynchronizedConfig = true;

            // 1 - Radius Settings (Limited strictly between 1-200)
            _workbenchRadius = BindConfig("1 - Radius Settings", "Workbench Radius", 20f, "Maximum building radius for the Workbench. (Vanilla default: 20) [Synced with Server]", 100, 1f, 200f);
            _stonecutterRadius = BindConfig("1 - Radius Settings", "Stonecutter Radius", 20f, "Maximum building radius for the Stonecutter. (Vanilla default: 20) [Synced with Server]", 99, 1f, 200f);
            _forgeRadius = BindConfig("1 - Radius Settings", "Forge Radius", 20f, "Maximum building radius for the Forge. (Vanilla default: 20) [Synced with Server]", 98, 1f, 200f);
            _blackForgeRadius = BindConfig("1 - Radius Settings", "Black Forge Radius", 20f, "Maximum building radius for the Black Forge. (Vanilla default: 20) [Synced with Server]", 97, 1f, 200f);
            _galdrTableRadius = BindConfig("1 - Radius Settings", "Galdr Table Radius", 20f, "Maximum building radius for the Galdr Table. (Vanilla default: 20) [Synced with Server]", 96, 1f, 200f);

            // 2 - Spawning Settings
            _mobSpawnSuppression = Config.Bind("2 - Spawning Settings", "Mob Spawn Suppression Rate", 0f,
                new ConfigDescription("Percentage chance to suppress active mob spawning (0.0 = 0%, 1.0 = 100%). [Synced with Server]",
                new AcceptableValueRange<float>(0f, 1f), new { order = 50 }));

            // Slider option limited strictly between 1-200
            _blockSpawnRadius = Config.Bind("2 - Spawning Settings", "Block Spawn Radius", 20f,
                new ConfigDescription("The radius around base structures where player-base spawn blocking is enforced. (Vanilla default: 20) [Synced with Server]",
                new AcceptableValueRange<float>(1f, 200f), new { order = 49 }));

            // Sync the entries through ServerSync
            ConfigSync.AddConfigEntry(_mobSpawnSuppression).SynchronizedConfig = true;
            ConfigSync.AddConfigEntry(_blockSpawnRadius).SynchronizedConfig = true;

            harmony.PatchAll();
            Logger.LogInfo($"{PluginName} version {PluginVersion} loaded successfully.");
        }

        private ConfigEntry<float> BindConfig(string group, string name, float value, string description, int order, float min, float max)
        {
            ConfigEntry<float> configEntry = Config.Bind(group, name, value,
                new ConfigDescription(description, new AcceptableValueRange<float>(min, max), new { order }));
            configEntry.SettingChanged += OnSettingChanged;

            SyncedConfigEntry<float> syncedEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedEntry.SynchronizedConfig = true;

            return configEntry;
        }

        private void OnSettingChanged(object sender, EventArgs e)
        {
            UpdateActiveStationRanges();
            UpdateSpawnBlockers();
        }

        public static void UpdateActiveStationRanges()
        {
            if (Player.m_localPlayer == null) return;

            CraftingStation[] stations = FindObjectsByType<CraftingStation>(FindObjectsSortMode.None);
            foreach (CraftingStation station in stations)
            {
                ApplyStationRadius(station);
            }
        }

        public static void ApplyStationRadius(CraftingStation station)
        {
            if (station == null) return;
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

        public static void UpdateSpawnBlockers()
        {
            if (Player.m_localPlayer == null) return;

            EffectArea[] effectAreas = FindObjectsByType<EffectArea>(FindObjectsSortMode.None);
            foreach (EffectArea area in effectAreas)
            {
                ApplySpawnBlockerRadius(area);
            }
        }

        public static void ApplySpawnBlockerRadius(EffectArea area)
        {
            if (area == null) return;
            if (ModEnabled.Value && (area.m_type & EffectArea.Type.PlayerBase) != 0)
            {
                SphereCollider component = area.GetComponent<SphereCollider>();
                if (component != null)
                {
                    component.radius = BlockSpawnRadius;
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

    // FIX: Changed from Awake to Start. Start fires after initial vanilla overrides execute.
    [HarmonyPatch(typeof(CraftingStation), "Start")]
    public static class CraftingStation_Start_Patch
    {
        public static void Postfix(CraftingStation __instance)
        {
            BuildRadiusCustomizerPlugin.ApplyStationRadius(__instance);
        }
    }

    // FIX: Using UpdateKnownStationsInRange forces continuous checks as players step into area grids
    [HarmonyPatch(typeof(CraftingStation), "UpdateKnownStationsInRange")]
    public static class CraftingStation_UpdateKnownStations_Patch
    {
        public static void Prefix(CraftingStation __instance)
        {
            BuildRadiusCustomizerPlugin.ApplyStationRadius(__instance);
        }
    }

    [HarmonyPatch(typeof(EffectArea), "Awake")]
    public static class EffectArea_Awake_Patch
    {
        public static void Postfix(EffectArea __instance)
        {
            BuildRadiusCustomizerPlugin.ApplySpawnBlockerRadius(__instance);
        }
    }

    // FIX: Overrides target area colliders continuously as streaming components refresh
    [HarmonyPatch(typeof(EffectArea), "Update")]
    public static class EffectArea_Update_Patch
    {
        private static float _timer = 0f;

        public static void Postfix(EffectArea __instance)
        {
            _timer += Time.deltaTime;
            if (_timer < 2.0f) return; // Only force recalculation every 2 seconds to maximize performance
            _timer = 0f;

            BuildRadiusCustomizerPlugin.ApplySpawnBlockerRadius(__instance);
        }
    }

    [HarmonyPatch(typeof(SpawnSystem), "Awake")]
    public static class SpawnSystem_Awake_Patch
    {
        public static void Postfix(SpawnSystem __instance)
        {
            float suppressionChance = BuildRadiusCustomizerPlugin.MobSpawnSuppression;
            if (suppressionChance <= 0f || __instance.m_spawnLists == null) return;
            foreach (var spawnList in __instance.m_spawnLists)
            {
                if (spawnList?.m_spawners == null) continue;
                for (int i = spawnList.m_spawners.Count - 1; i >= 0; i--)
                {
                    SpawnSystem.SpawnData spawnData = spawnList.m_spawners[i];
                    if (spawnData == null) continue;
                    if (suppressionChance >= 1f)
                    {
                        spawnList.m_spawners.RemoveAt(i);
                    }
                    else
                    {
                        spawnData.m_spawnChance = Mathf.Clamp(spawnData.m_spawnChance * (1f - suppressionChance), 0f, 100f);
                    }
                }
            }
        }
    }
}

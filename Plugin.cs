using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using PerfectRandom.Sulfur.Core;
using UnityEngine;

namespace PerfectOils
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.ryuka.sulfur.perfectoils";
        public const string PluginName = "Perfect Oils";
        public const string PluginVersion = "1.3.7";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        internal ConfigEntry<bool> Enabled { get; private set; }
        internal ConfigEntry<bool> ShowRemovedTraitsWithStrikethrough { get; private set; }
        internal ConfigEntry<bool> DetailedLogging { get; private set; }
        internal TraitConfiguration TraitSettings { get; private set; }

        internal OilTraitService OilTraits { get; private set; }
        internal TooltipStrikeRenderer TooltipRenderer { get; private set; }

        private const float InitializationRetryInterval = 1f;

        private Harmony _harmony;
        private AsyncAssetLoading _assetLoader;
        private float _nextInitializationAttempt;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind(
                "General",
                "Enabled",
                true,
                "Enable Perfect Oils. Individual undesirable traits can be selected in the Traits section.");

            ShowRemovedTraitsWithStrikethrough = Config.Bind(
                "Display",
                "ShowRemovedTraitsWithStrikethrough",
                true,
                "Keep the original undesirable-trait text in oil tooltips and draw a strikethrough over lines currently disabled by this mod.");

            DetailedLogging = Config.Bind(
                "Debug",
                "DetailedLogging",
                false,
                "Log each classified oil modifier and tooltip line. Keep disabled during normal play.");

            TraitSettings = new TraitConfiguration(Config);
            OilTraits = new OilTraitService(Logger, TraitSettings);
            TooltipRenderer = new TooltipStrikeRenderer(OilTraits, TraitSettings, Logger);

            // Any suppression-affecting toggle should take effect immediately, even on
            // weapons that are already oiled. The master switch plus every per-trait toggle
            // re-runs the durability refresh and re-applies oil modifiers on loaded items.
            Enabled.SettingChanged += OnSuppressionSettingChanged;
            foreach (ConfigEntry<bool> traitSetting in TraitSettings.AllSettings())
            {
                traitSetting.SettingChanged += OnSuppressionSettingChanged;
            }

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            Logger.LogInfo("[PerfectOils] Patches applied. Waiting for the SULFUR asset databases.");
        }

        private void Update()
        {
            // Always build the oil index, even when the global switch starts disabled.
            // This allows the mod to be enabled later without restarting asset loading.
            if (OilTraits == null || OilTraits.IsInitialized)
            {
                return;
            }

            if (Time.unscaledTime < _nextInitializationAttempt)
            {
                return;
            }

            if (_assetLoader == null)
            {
                _assetLoader = UnityEngine.Object.FindFirstObjectByType<AsyncAssetLoading>();
            }

            if (_assetLoader == null || !_assetLoader.loadingDone)
            {
                _nextInitializationAttempt = Time.unscaledTime + InitializationRetryInterval;
                return;
            }

            TryInitializeOilTraits(_assetLoader);
        }

        internal void NotifyAssetsReady(AsyncAssetLoading assetLoader)
        {
            if (assetLoader == null)
            {
                return;
            }

            _assetLoader = assetLoader;
            TryInitializeOilTraits(assetLoader);
        }

        private void TryInitializeOilTraits(AsyncAssetLoading assetLoader)
        {
            if (OilTraits == null || OilTraits.IsInitialized)
            {
                return;
            }

            bool initialized = OilTraits.Initialize(
                assetLoader,
                DetailedLogging.Value);

            if (!initialized)
            {
                _nextInitializationAttempt = Time.unscaledTime + InitializationRetryInterval;
                return;
            }

            OilTraits.RefreshDurabilityFlags(Enabled.Value);
        }

        private void OnSuppressionSettingChanged(object sender, EventArgs eventArgs)
        {
            if (OilTraits == null || !OilTraits.IsInitialized)
            {
                return;
            }

            // Durability is driven by the shared CostsDurability flag and updates globally.
            OilTraits.RefreshDurabilityFlags(Enabled.Value);

            // Stat modifiers are baked per item, so rebuild the player's loaded items to make
            // the new config visible without forcing a re-oil or a save reload.
            OilReapplyService.ReapplyToLoadedItems(Logger);
        }

        private void OnDestroy()
        {
            Enabled.SettingChanged -= OnSuppressionSettingChanged;
            if (TraitSettings != null)
            {
                foreach (ConfigEntry<bool> traitSetting in TraitSettings.AllSettings())
                {
                    traitSetting.SettingChanged -= OnSuppressionSettingChanged;
                }
            }

            if (OilTraits != null)
            {
                OilTraits.RestoreOriginalDefinitions();
            }

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
                Log = null;
            }
        }
    }
}

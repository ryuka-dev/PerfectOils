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
        public const string PluginVersion = "1.3.5";

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

            Enabled.SettingChanged += OnDurabilitySettingChanged;
            TraitSettings.RemoveExtraDurabilityCost.SettingChanged += OnDurabilitySettingChanged;

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

        private void OnDurabilitySettingChanged(object sender, EventArgs eventArgs)
        {
            if (OilTraits != null && OilTraits.IsInitialized)
            {
                OilTraits.RefreshDurabilityFlags(Enabled.Value);
            }
        }

        private void OnDestroy()
        {
            Enabled.SettingChanged -= OnDurabilitySettingChanged;
            if (TraitSettings != null)
            {
                TraitSettings.RemoveExtraDurabilityCost.SettingChanged -= OnDurabilitySettingChanged;
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

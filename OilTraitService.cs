using System;
using System.Collections.Generic;
using BepInEx.Logging;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.CharacterStats;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.Stats;

namespace PerfectOils
{
    internal sealed class OilTraitService
    {
        internal sealed class OilDefinitionInfo
        {
            private readonly Dictionary<ItemModifierContainer, NegativeOilTrait> _traitsByModifier =
                new Dictionary<ItemModifierContainer, NegativeOilTrait>();

            internal readonly EnchantmentDefinition Definition;
            internal readonly bool HasMoreBulletDrop;

            internal OilDefinitionInfo(
                EnchantmentDefinition definition,
                bool hasMoreBulletDrop)
            {
                Definition = definition;
                HasMoreBulletDrop = hasMoreBulletDrop;
            }

            internal void SetTraits(
                ItemModifierContainer modifier,
                NegativeOilTrait traits)
            {
                if (modifier != null)
                {
                    _traitsByModifier[modifier] = traits;
                }
            }

            internal NegativeOilTrait GetTraits(ItemModifierContainer modifier)
            {
                NegativeOilTrait traits;
                return modifier != null &&
                       _traitsByModifier.TryGetValue(modifier, out traits)
                    ? traits
                    : NegativeOilTrait.None;
            }
        }

        private struct ModifierSignature
        {
            internal readonly ItemAttributes Attribute;
            internal readonly StatModType ModType;
            internal readonly float Value;
            internal readonly NegativeOilTrait Traits;

            internal ModifierSignature(
                ItemModifierContainer modifier,
                NegativeOilTrait traits)
            {
                Attribute = modifier.attribute;
                ModType = modifier.modType;
                Value = modifier.value;
                Traits = traits;
            }

            internal bool Matches(ItemAttributes attribute, StatModifier modifier)
            {
                return modifier != null &&
                       Attribute == attribute &&
                       ModType == modifier.Type &&
                       Math.Abs(Value - modifier.Value) <= 0.000001f;
            }
        }

        private readonly ManualLogSource _log;
        private readonly TraitConfiguration _settings;
        private readonly Dictionary<uint, List<ModifierSignature>> _classifiedBySourceId =
            new Dictionary<uint, List<ModifierSignature>>();
        private readonly Dictionary<ItemDefinition, OilDefinitionInfo> _oilInfoByItem =
            new Dictionary<ItemDefinition, OilDefinitionInfo>();
        private readonly Dictionary<EnchantmentDefinition, OilDefinitionInfo> _oilInfoByDefinition =
            new Dictionary<EnchantmentDefinition, OilDefinitionInfo>();
        private readonly Dictionary<EnchantmentDefinition, bool> _originalDurabilityFlags =
            new Dictionary<EnchantmentDefinition, bool>();

        private bool _initialized;
        private bool _loggedFirstRuntimeSuppression;
        private bool _loggedFirstBulletDropSuppression;
        private bool _warnedUnavailableDatabase;
        private bool _warnedEmptyDatabase;
        private bool _warnedNoOilDefinitions;

        internal bool IsInitialized
        {
            get { return _initialized; }
        }

        internal OilTraitService(
            ManualLogSource log,
            TraitConfiguration settings)
        {
            _log = log;
            _settings = settings;
        }

        internal bool Initialize(
            AsyncAssetLoading assets,
            bool detailedLogging)
        {
            if (_initialized)
            {
                return true;
            }

            if (assets == null ||
                assets.itemDatabase == null ||
                assets.enchantmentDatabase == null)
            {
                if (!_warnedUnavailableDatabase)
                {
                    _log.LogWarning(
                        "[PerfectOils] Asset loading completed, but an oil database was unavailable; initialization will be retried.");
                    _warnedUnavailableDatabase = true;
                }

                return false;
            }

            List<ItemDefinition> items = assets.itemDatabase.GetRawList();
            if (items == null)
            {
                if (!_warnedUnavailableDatabase)
                {
                    _log.LogWarning(
                        "[PerfectOils] Item database returned no raw item list; initialization will be retried.");
                    _warnedUnavailableDatabase = true;
                }

                return false;
            }

            if (items.Count == 0)
            {
                if (!_warnedEmptyDatabase)
                {
                    _log.LogWarning(
                        "[PerfectOils] Item database is present but still empty; initialization will be retried.");
                    _warnedEmptyDatabase = true;
                }

                return false;
            }

            int oilItems = 0;
            int uniqueDefinitions = 0;
            int classifiedModifiers = 0;
            int enabledModifiers = 0;
            int moreBulletDropModifiers = 0;
            int moreBulletDropOils = 0;
            int negativeBulletSpeedModifiers = 0;
            int negativeDamageFlatModifiers = 0;
            int negativeDamagePercentModifiers = 0;
            int negativeBulletSizeModifiers = 0;
            int negativeRpmModifiers = 0;
            var resolvedBulletDropAttributes = new HashSet<ItemAttributes>();

            for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                ItemDefinition oilItem = items[itemIndex];
                if (!IsOilItem(oilItem))
                {
                    continue;
                }

                oilItems++;

                EnchantmentDefinition definition;
                try
                {
                    definition = assets.enchantmentDatabase[oilItem.appliesEnchantment];
                }
                catch (Exception exception)
                {
                    _log.LogWarning(
                        "[PerfectOils] Could not resolve enchantment for oil '" +
                        SafeOilName(oilItem) + "': " + exception.Message);
                    continue;
                }

                if (definition == null)
                {
                    continue;
                }

                OilDefinitionInfo info;
                if (!_oilInfoByDefinition.TryGetValue(definition, out info))
                {
                    List<ItemModifierContainer> modifiers = definition.modifiersApplied;
                    bool hasMoreBulletDrop = false;

                    if (modifiers != null)
                    {
                        for (int modifierIndex = 0;
                             modifierIndex < modifiers.Count;
                             modifierIndex++)
                        {
                            ItemModifierContainer modifier = modifiers[modifierIndex];
                            ItemAttribute attribute = TryGetAttribute(modifier);
                            if (!NegativeTraitPolicy.IsMoreBulletDropMarker(
                                    modifier,
                                    attribute))
                            {
                                continue;
                            }

                            hasMoreBulletDrop = true;

                            if (resolvedBulletDropAttributes.Add(modifier.attribute))
                            {
                                _log.LogInfo(
                                    "[PerfectOils] Resolved the visible More Bullet Drop trait from game metadata " +
                                    "[oil='" + SafeDefinitionName(definition, oilItem) +
                                    "', attribute=" + modifier.attribute +
                                    "(" + (int)modifier.attribute + ")" +
                                    ", type=" + modifier.modType +
                                    ", value=" + modifier.value +
                                    ", label='" + SafeAttributeLabel(attribute) + "'].");
                            }
                        }
                    }

                    info = new OilDefinitionInfo(definition, hasMoreBulletDrop);
                    _oilInfoByDefinition.Add(definition, info);
                    uniqueDefinitions++;

                    if (hasMoreBulletDrop)
                    {
                        moreBulletDropOils++;
                    }

                    uint sourceId = (uint)definition.id.AsGlobalId();
                    var signatures = new List<ModifierSignature>();

                    if (modifiers != null)
                    {
                        for (int modifierIndex = 0;
                             modifierIndex < modifiers.Count;
                             modifierIndex++)
                        {
                            ItemModifierContainer modifier = modifiers[modifierIndex];
                            if (modifier == null)
                            {
                                continue;
                            }

                            ItemAttribute attribute = TryGetAttribute(modifier);
                            NegativeOilTrait traits = NegativeTraitPolicy.Classify(
                                modifier,
                                info.HasMoreBulletDrop,
                                attribute);

                            info.SetTraits(modifier, traits);

                            if (traits == NegativeOilTrait.None)
                            {
                                continue;
                            }

                            signatures.Add(new ModifierSignature(modifier, traits));
                            classifiedModifiers++;

                            if ((traits & NegativeOilTrait.MoreBulletDrop) != 0)
                            {
                                moreBulletDropModifiers++;
                            }

                            if ((traits & NegativeOilTrait.NegativeBulletSpeed) != 0)
                            {
                                negativeBulletSpeedModifiers++;
                            }

                            if ((traits & NegativeOilTrait.NegativeDamage) != 0)
                            {
                                if (modifier.modType == StatModType.Flat)
                                {
                                    negativeDamageFlatModifiers++;
                                }
                                else
                                {
                                    negativeDamagePercentModifiers++;
                                }
                            }

                            if ((traits & NegativeOilTrait.NegativeBulletSize) != 0)
                            {
                                negativeBulletSizeModifiers++;
                            }

                            if ((traits & NegativeOilTrait.NegativeRpm) != 0)
                            {
                                negativeRpmModifiers++;
                            }

                            if (_settings.ShouldRemove(traits))
                            {
                                enabledModifiers++;
                            }

                            if (detailedLogging)
                            {
                                _log.LogInfo(
                                    "[PerfectOils] Classified " +
                                    NegativeTraitPolicy.Describe(traits) +
                                    " from '" + SafeDefinitionName(definition, oilItem) +
                                    "' [active=" + _settings.ShouldRemove(traits) +
                                    ", attribute=" + modifier.attribute +
                                    ", type=" + modifier.modType +
                                    ", value=" + modifier.value +
                                    ", label='" + SafeAttributeLabel(attribute) + "'].");
                            }
                        }
                    }

                    if (signatures.Count > 0)
                    {
                        _classifiedBySourceId[sourceId] = signatures;
                    }

                    _originalDurabilityFlags[definition] = definition.CostsDurability;
                }

                _oilInfoByItem[oilItem] = info;
            }

            if (oilItems == 0 || uniqueDefinitions == 0)
            {
                if (!_warnedNoOilDefinitions)
                {
                    _log.LogWarning(
                        "[PerfectOils] Scanned " + items.Count +
                        " item definitions but found no runtime enchantment oils; initialization will be retried.");
                    _warnedNoOilDefinitions = true;
                }

                return false;
            }

            _initialized = true;

            if (moreBulletDropOils == 0)
            {
                _log.LogWarning(
                    "[PerfectOils] No visible 'Bullet drop' oil modifier was resolved from the current game metadata. " +
                    "RemoveMoreBulletDrop will remain inactive rather than guessing an unrelated attribute.");
            }

            _log.LogInfo(
                "[PerfectOils] Scanned " + items.Count + " item definitions. " +
                "Indexed " + oilItems + " oil items across " +
                uniqueDefinitions + " unique enchantments; " +
                classifiedModifiers + " known negative modifiers were classified, and " +
                enabledModifiers + " are currently enabled for suppression by config. " +
                "MoreBulletDrop oils=" + moreBulletDropOils +
                ", classified modifiers=" + moreBulletDropModifiers + ". " +
                "Signed-value traits: bulletSpeed=" + negativeBulletSpeedModifiers +
                ", damageFlat=" + negativeDamageFlatModifiers +
                ", damagePercent=" + negativeDamagePercentModifiers +
                ", bulletSize=" + negativeBulletSizeModifiers +
                ", rpm=" + negativeRpmModifiers + ".");

            return true;
        }

        internal bool ShouldSuppress(
            ItemAttributes attribute,
            StatModifier modifier)
        {
            if (!_initialized || modifier == null)
            {
                return false;
            }

            List<ModifierSignature> signatures;
            if (!_classifiedBySourceId.TryGetValue(
                    modifier.SourceId,
                    out signatures))
            {
                return false;
            }

            for (int i = 0; i < signatures.Count; i++)
            {
                ModifierSignature signature = signatures[i];
                if (!signature.Matches(attribute, modifier) ||
                    !_settings.ShouldRemove(signature.Traits))
                {
                    continue;
                }

                if ((signature.Traits & NegativeOilTrait.MoreBulletDrop) != 0 &&
                    !_loggedFirstBulletDropSuppression)
                {
                    _loggedFirstBulletDropSuppression = true;
                    _log.LogInfo(
                        "[PerfectOils] More Bullet Drop suppression is active; " +
                        "the first matching oil modifier was blocked " +
                        "[attribute=" + attribute +
                        "(" + (int)attribute + ")" +
                        ", type=" + modifier.Type +
                        ", value=" + modifier.Value +
                        ", sourceId=" + modifier.SourceId + "].");
                }

                if (!_loggedFirstRuntimeSuppression)
                {
                    _loggedFirstRuntimeSuppression = true;
                    _log.LogInfo(
                        "[PerfectOils] Runtime suppression is active; the first configured oil modifier was blocked " +
                        "[trait=" + NegativeTraitPolicy.Describe(signature.Traits) +
                        ", attribute=" + attribute +
                        ", sourceId=" + modifier.SourceId + "].");
                }

                return true;
            }

            return false;
        }

        internal bool TryGetOilInfo(
            InventoryItem item,
            out OilDefinitionInfo info)
        {
            info = null;
            if (!_initialized ||
                item == null ||
                item.itemDefinition == null)
            {
                return false;
            }

            ItemDefinition itemDefinition = item.itemDefinition;
            if (_oilInfoByItem.TryGetValue(itemDefinition, out info))
            {
                return true;
            }

            if (!itemDefinition.appliesEnchantment.IsValid)
            {
                return false;
            }

            try
            {
                EnchantmentDefinition definition =
                    AssetAccess.GetAsset(itemDefinition.appliesEnchantment);
                return definition != null &&
                       _oilInfoByDefinition.TryGetValue(definition, out info);
            }
            catch
            {
                info = null;
                return false;
            }
        }

        internal void RefreshDurabilityFlags(bool pluginEnabled)
        {
            bool removeDurabilityCost =
                pluginEnabled &&
                _settings.RemoveExtraDurabilityCost.Value;

            foreach (KeyValuePair<EnchantmentDefinition, bool> pair
                     in _originalDurabilityFlags)
            {
                if (pair.Key != null)
                {
                    pair.Key.CostsDurability = removeDurabilityCost
                        ? false
                        : pair.Value;
                }
            }
        }

        internal void RestoreOriginalDefinitions()
        {
            foreach (KeyValuePair<EnchantmentDefinition, bool> pair
                     in _originalDurabilityFlags)
            {
                if (pair.Key != null)
                {
                    pair.Key.CostsDurability = pair.Value;
                }
            }

            _classifiedBySourceId.Clear();
            _oilInfoByItem.Clear();
            _oilInfoByDefinition.Clear();
            _originalDurabilityFlags.Clear();
            _initialized = false;
            _loggedFirstRuntimeSuppression = false;
            _loggedFirstBulletDropSuppression = false;
            _warnedUnavailableDatabase = false;
            _warnedEmptyDatabase = false;
            _warnedNoOilDefinitions = false;
        }

        private static ItemAttribute TryGetAttribute(
            ItemModifierContainer modifier)
        {
            if (modifier == null ||
                modifier.attribute == ItemAttributes.None)
            {
                return null;
            }

            try
            {
                return AssetAccess.GetAsset(modifier.attribute);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsOilItem(ItemDefinition itemDefinition)
        {
            return itemDefinition != null &&
                   itemDefinition.IsEnchantment &&
                   itemDefinition.appliesEnchantment.IsValid;
        }

        private static string SafeOilName(ItemDefinition oilItem)
        {
            if (oilItem == null)
            {
                return "<null oil>";
            }

            if (!string.IsNullOrEmpty(oilItem.displayName))
            {
                return oilItem.displayName;
            }

            return oilItem.id.ToString();
        }

        private static string SafeDefinitionName(
            EnchantmentDefinition definition,
            ItemDefinition oilItem)
        {
            if (definition != null &&
                !string.IsNullOrEmpty(definition.enchantmentName))
            {
                return definition.enchantmentName;
            }

            return SafeOilName(oilItem);
        }

        private static string SafeAttributeLabel(ItemAttribute attribute)
        {
            if (attribute == null)
            {
                return "<unknown>";
            }

            if (!string.IsNullOrEmpty(attribute.itemDescriptionName))
            {
                return attribute.itemDescriptionName;
            }

            if (!string.IsNullOrEmpty(attribute.simplifiedIncreaseString))
            {
                return attribute.simplifiedIncreaseString;
            }

            return "<unnamed>";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.CharacterStats;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.Stats;
using PerfectRandom.Sulfur.Core.UI;
using PerfectRandom.Sulfur.Core.UI.Inventory;

namespace PerfectOils
{
    /// <summary>
    /// Re-applies oil (enchantment) modifiers to the player's currently loaded items so
    /// that suppression config changes take effect immediately.
    ///
    /// Oil stat modifiers are baked into each item's <see cref="ItemStats"/> the moment the
    /// oil is applied (or the save is loaded). Flipping a Traits config flag therefore does
    /// nothing to a weapon that is already oiled - the modifier is still in the stat list.
    /// To honour a live config change we remove and re-add each enchantment's modifiers,
    /// which re-runs the <c>ItemStats.AddModifier</c> suppression patch under the new config.
    /// This mirrors what the durability path already does via RefreshDurabilityFlags.
    /// </summary>
    internal static class OilReapplyService
    {
        private const string StashGridsFieldName = "StashInventoryItemGrids";

        // SULFUR 0.19 made InventoryUI.StashInventoryItemGrids private. The public Stashes
        // dictionary added in the same build is not an equivalent replacement: RegisterStash
        // only records PlayerStash stations, while RegisterStashItemGrid is also called by the
        // repair/showcase frame under its own identifier, and ClearCachedStashInventories
        // empties the grid dictionary without touching Stashes. Reading the field keeps exactly
        // the set of grids this service covered before. Public is included in the lookup so the
        // field being made public again would not break it.
        private static readonly FieldInfo StashGridsField = ResolveStashGridsField();

        private static bool stashGridsFieldReported;

        internal static void ReapplyToLoadedItems(ManualLogSource log)
        {
            try
            {
                var items = new HashSet<InventoryItem>();
                CollectEquipped(items);
                CollectInventory(items, log);

                int rebuilt = 0;
                foreach (InventoryItem item in items)
                {
                    if (RebuildItem(item))
                    {
                        rebuilt++;
                    }
                }

                if (rebuilt > 0 && log != null)
                {
                    log.LogInfo(
                        "[PerfectOils] Re-applied oil suppression to " + rebuilt +
                        " loaded item(s) after a settings change.");
                }
            }
            catch (Exception exception)
            {
                if (log != null)
                {
                    log.LogWarning(
                        "[PerfectOils] Live re-apply skipped (no active run or unexpected state): " +
                        exception.Message);
                }
            }
        }

        private static void CollectEquipped(HashSet<InventoryItem> items)
        {
            GameManager gameManager = StaticInstance<GameManager>.Instance;
            if (gameManager == null || gameManager.EquipmentManager == null)
            {
                return;
            }

            Dictionary<InventorySlot, InventoryItem> equipped =
                gameManager.EquipmentManager.EquippedItems;
            if (equipped == null)
            {
                return;
            }

            foreach (KeyValuePair<InventorySlot, InventoryItem> pair in equipped)
            {
                if (pair.Value != null)
                {
                    items.Add(pair.Value);
                }
            }
        }

        private static void CollectInventory(HashSet<InventoryItem> items, ManualLogSource log)
        {
            UIManager uiManager = StaticInstance<UIManager>.Instance;
            if (uiManager == null || uiManager.InventoryUI == null)
            {
                return;
            }

            InventoryUI inventoryUI = uiManager.InventoryUI;

            ItemGrid backpack = inventoryUI.PlayerBackpackGrid;
            if (backpack != null)
            {
                AddRange(items, backpack.AllItems());
            }

            Paperdoll paperdoll = inventoryUI.paperdoll;
            if (paperdoll != null)
            {
                AddRange(items, paperdoll.AllItems());
            }

            // Storage chests / stashes. The dictionary stays null until the player opens a
            // stash; each opened stash grid is registered and its items become live, so any
            // currently loaded stash is rebuilt too. Equipping a weapon does not rebuild its
            // enchantment stats, so a stale stash weapon would otherwise keep old modifiers.
            Dictionary<string, ItemGrid> stashes = GetStashGrids(inventoryUI, log);
            if (stashes != null)
            {
                foreach (KeyValuePair<string, ItemGrid> stash in stashes)
                {
                    if (stash.Value != null)
                    {
                        AddRange(items, stash.Value.AllItems());
                    }
                }
            }
        }

        private static FieldInfo ResolveStashGridsField()
        {
            FieldInfo field = typeof(InventoryUI).GetField(
                StashGridsFieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            return field != null && field.FieldType == typeof(Dictionary<string, ItemGrid>)
                ? field
                : null;
        }

        private static Dictionary<string, ItemGrid> GetStashGrids(
            InventoryUI inventoryUI, ManualLogSource log)
        {
            if (StashGridsField == null)
            {
                if (!stashGridsFieldReported)
                {
                    stashGridsFieldReported = true;
                    if (log != null)
                    {
                        log.LogWarning(
                            "[PerfectOils] InventoryUI." + StashGridsFieldName +
                            " could not be resolved; oils on weapons in open storage chests " +
                            "will not be re-applied when a setting changes.");
                    }
                }

                return null;
            }

            return (Dictionary<string, ItemGrid>)StashGridsField.GetValue(inventoryUI);
        }

        private static void AddRange(HashSet<InventoryItem> items, List<InventoryItem> source)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                {
                    items.Add(source[i]);
                }
            }
        }

        private static bool RebuildItem(InventoryItem item)
        {
            if (item == null || item.stats == null)
            {
                return false;
            }

            List<EnchantmentDefinition> enchantments = item.enchantments;
            if (enchantments == null || enchantments.Count == 0)
            {
                return false;
            }

            bool rebuilt = false;
            for (int e = 0; e < enchantments.Count; e++)
            {
                EnchantmentDefinition enchantment = enchantments[e];
                if (enchantment == null || enchantment.modifiersApplied == null)
                {
                    continue;
                }

                uint sourceId = (uint)enchantment.id.AsGlobalId();

                // Drop the source's current modifiers, then re-add them. The re-add goes
                // through ItemStats.AddModifier, where the suppression patch decides per the
                // live config whether each modifier survives.
                item.stats.RemoveModifiersFromList(enchantment.modifiersApplied, sourceId);

                List<ItemModifierContainer> modifiers = enchantment.modifiersApplied;
                for (int m = 0; m < modifiers.Count; m++)
                {
                    ItemModifierContainer modifier = modifiers[m];
                    if (modifier == null)
                    {
                        continue;
                    }

                    // The game resolves this marker into concrete attributes at apply time;
                    // it should never sit in a resolved enchantment, but skip it defensively
                    // so a re-apply can never inject the raw random-attribute marker.
                    if (modifier.attribute == ItemAttributes.EnchantmentAddRandomOilAttributes)
                    {
                        continue;
                    }

                    item.stats.AddModifier(
                        modifier.attribute,
                        new StatModifier(modifier.value, modifier.modType, sourceId));
                }

                rebuilt = true;
            }

            if (rebuilt)
            {
                // Push the rebuilt stats onto the live weapon instance (cooldown, ADS, etc.).
                try
                {
                    item.SyncWithInstancedVersion();
                }
                catch
                {
                    // A missing instanced weapon (item sitting in a grid) is harmless.
                }
            }

            return rebuilt;
        }
    }
}

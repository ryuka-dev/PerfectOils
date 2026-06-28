# Perfect Oils — Architecture & Maintenance Notes

Internal documentation for maintainers. The user-facing description lives in
[`Thunderstore/README.md`](../Thunderstore/README.md); this file explains *how* the mod works and
*why*, based on the reverse-engineered SULFUR game code.

## Goal

SULFUR weapon "oils" (enchantments) apply a mix of positive and negative stat modifiers. Perfect
Oils selectively removes the **negative** modifiers the player opts into, while leaving positive
ones (and unrelated mods) untouched. It never replaces a weapon, projectile, loot, movement, or
damage consumer — it only prevents specific modifiers from being applied.

## How oils work in the game (reversed)

- An oil is an `ItemDefinition` with `IsEnchantment == true` and a valid `appliesEnchantment`
  (`EnchantmentId`). That id resolves to an `EnchantmentDefinition` carrying
  `List<ItemModifierContainer> modifiersApplied`.
- Each `ItemModifierContainer` is `{ ItemAttributes attribute, StatModType modType, float value }`.
  `StatModType` is `Flat = 100`, `PercentAdd = 200`, `PercentMult = 300`.
- A **negative `value` always means a reduction** for all three mod types
  (`CharacterStat.CalculateFinalValue`: `Flat` adds, `PercentAdd` multiplies by `1 + Σ`,
  `PercentMult` multiplies by `1 + value`).
- Applying an oil (`InventoryItem.AddEnchantment`, or `SetupEnchantmentsFromData` on load) calls
  `ItemStats.AddModifier(ItemAttributes, StatModifier)` once per modifier, with
  `sourceId = (uint)EnchantmentDefinition.id.AsGlobalId()`.
  - `GlobalId` is a packed `uint`: `fullUniqueId = type(2 for Enchantment) + (enchantmentId.value << 16)`.
    It is deterministic, so the same oil definition always produces the same `sourceId`.
- Weapon/projectile stats are read **live** from `ItemStats` at fire time (e.g.
  `Weapon.Damage`, `RPM` → cooldown in `SyncEnchantments`, and in the projectile spawn:
  `ProjectileScale`, `ProjectileMass`, `ProjectileDrag`, `ProjectileTimeScale`). Max durability is
  read live via `InventoryItem.DurabilityMax => GetAttribute(MaxDurability)`. Because reads are
  live, dropping a modifier at apply time is enough to change behaviour.

## Trait → attribute mapping

Classification lives in [`NegativeTraitPolicy.cs`](../NegativeTraitPolicy.cs).

| Trait flag | Attribute(s) | Suppressed when | Default |
|---|---|---|---|
| DisableAiming | `DisableADS` | value > 0 | on |
| ExtraAmmoConsumeChance | `ConsumeExtraAmmoChance` | value > 0 | on |
| DecreaseAccuracyWhenMoving | `AimMovingBonus` | value < 0 | on |
| DecreaseMoveSpeed | `ItemStat_MoveSpeed` | value < 0 | on |
| DecreaseJumpPower | `ItemStat_JumpPower` | value < 0 | on |
| DecreaseLootChanceMultiplier | `ItemStat_LootChanceMultiplier` | value < 0 | on |
| DisableMoneyDrops | `DisableLootMoney` | value > 0 | on |
| DisableOrganDrops | `DisableLootOrgans` | value > 0 | on |
| MoreBulletDrop | `ProjectileMass` (the visible "Bullet drop" line) + same-oil `ProjectileTimeScale`/`ProjectileMass` companions | see note | off |
| MoreDrag | `ProjectileDrag` | value > 0 | off |
| NegativeBulletSpeed | `ProjectileTimeScale` | value < 0 | off |
| NegativeDamage | `Damage`, `DamageModifier` | value < 0 | off |
| NegativeBulletSize | `ProjectileScale` | value < 0 | off |
| NegativeRpm | `RPM` | value < 0 | off |
| NegativeMaxDurability | `MaxDurability` | value < 0 | off |
| MoreRecoil | `KickMultiplier` | value > 0 (recoil = `KickMultiplier * KickPower`) | off |
| MoreSpread | `Spread` | value > 0 (wider cone) | off |
| NegativeReloadSpeed | `ReloadSpeed` | value < 0 (drives reload animation speed) | off |
| FasterDurabilityLoss | `DurabilityLoss` | value > 0 (per-shot loss multiplier, base 1) | off |
| SelfDamage | `EnchantmentSelfDamage` | value > 0 (damages wielder on fire; read live, unlike `EnchantmentDurabilityCost`) | off |
| NegativeProjectileForce | `ProjectileForce` | value < 0 (launch velocity = `1 + ProjectileForce`) | off |
| ExtraDurabilityCost | `EnchantmentDurabilityCost` | value > 0 (handled via the durability flag, see below) | off |

**Bullet drop note:** the visible "Bullet drop" tooltip row is *not* `ProjectileGravityFactor`
(only the Rocket Launcher oil uses that). It is a `ProjectileMass` increase whose `ItemAttribute`
label is literally "Bullet drop". `IsMoreBulletDropMarker` detects it from the attribute's
description metadata rather than a hard-coded attribute id. A reduced `ProjectileTimeScale` on the
same oil is tagged with both `NegativeBulletSpeed` and `MoreBulletDrop`, so either toggle removes
that shared companion.

## Runtime suppression

- [`Patches/ItemStatsPatches.cs`](../Patches/ItemStatsPatches.cs) is a Harmony prefix on
  `ItemStats.AddModifier(ItemAttributes, StatModifier)` (the enum overload — the
  `ItemAttribute` overload delegates to it, and `AddEnchantment`/`AddModifiersFromList` call the
  enum one directly). Returning `false` drops the modifier.
- [`OilTraitService.cs`](../OilTraitService.cs) indexes, per oil definition, exact
  `ModifierSignature`s keyed by `sourceId` → `{ attribute, modType, value, traits }`. At runtime
  `ShouldSuppress` looks up by `StatModifier.SourceId`, matches `attribute`/`modType`/`value`
  (epsilon), and drops the modifier only if `TraitConfiguration.ShouldRemove(traits)` is true for
  the current config. Config is read live, so the decision always reflects current settings.

## Durability cost (separate mechanism)

`EnchantmentDurabilityCost` is **not** consumed through `ItemStats`. `TakeDurabilityLossFromShoot`
iterates `enchantments`, skips any with `CostsDurability == false`, and otherwise adds the oil's
`EnchantmentDurabilityCost` value (or a default of 1). So `RemoveExtraDurabilityCost` works by
flipping the shared `EnchantmentDefinition.CostsDurability` flag (`OilTraitService.RefreshDurabilityFlags`,
restored on shutdown). This updates globally and instantly. This is unrelated to `MaxDurability`,
which *is* a normal live-read stat and is handled by the `NegativeMaxDurability` trait.

## Live re-application

Stat modifiers are baked into each item's `ItemStats` at apply/load time, and equipping a weapon
does **not** rebuild them. So flipping a trait toggle would not affect an already-oiled weapon on
its own. [`OilReapplyService.cs`](../OilReapplyService.cs) closes this gap:

- On any trait `SettingChanged` (and the master `Enabled`), [`Plugin.cs`](../Plugin.cs) calls
  `RefreshDurabilityFlags` + `OilReapplyService.ReapplyToLoadedItems`.
- It collects the player's loaded oiled items — equipped (`EquipmentManager.EquippedItems`),
  backpack (`InventoryUI.PlayerBackpackGrid`), paperdoll, and opened stashes
  (`InventoryUI.StashInventoryItemGrids`) — and for each enchantment removes then re-adds its
  modifiers (re-running the suppression prefix), then calls `SyncWithInstancedVersion`.
- Everything is wrapped in null-checks + try/catch, so it no-ops cleanly in menus or off the main
  thread (e.g. a direct `.cfg` file edit handled by BepInEx's watcher).
- Not covered: a stash never opened this session (its grid is unregistered; those items aren't live
  and will be rebuilt the next time a setting changes while that stash is open), and
  vendor/service-station grids (intentionally, to avoid touching items the player does not own).

## Tooltip strikethrough

[`TooltipStrikeRenderer.cs`](../TooltipStrikeRenderer.cs) postfixes `ItemDescription.Setup` and wraps
removed-trait lines in TextMeshPro `<s>…</s>`. It uses the same classifier and config as runtime
suppression, and re-evaluates on every tooltip rebuild, so it always reflects current settings.

## Verifying against a new game build

Oil data is **not** in the managed DLLs; it lives in Addressables. After a SULFUR update, re-verify
the trait→attribute mapping and `ItemAttributes`/`StatModType` enum values. Recipes for decompiling
the game and dumping the oil definitions (ilspycmd + UnityPy) are in the maintainer's notes; the key
checks are:

1. `ItemAttributes` enum indices still match (the game indexes `ItemStats.itemAttributes` by them).
2. Each negative trait still maps to the attribute the game actually reads for that effect.
3. The initialization log line in `OilTraitService` reports non-zero counts for the signed-value
   traits, confirming the live Addressables data was classified.

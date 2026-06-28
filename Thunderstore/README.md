# Perfect Oils 1.3.6

A BepInEx 5 mod for **SULFUR** that selectively removes undesirable weapon-oil traits while preserving positive effects and the original localized descriptions.

## Removed traits

Every supported trait has its own config switch.

The default profile only removes effects that have relatively little impact on weapon balance:

- Disable Aiming
- Extra Ammo Consume Chance
- Decrease Accuracy When Moving
- Decrease Move Speed
- Decrease Jump Power
- Decrease Loot Chance Multiplier
- Disable Money Drops
- Disable Organ Drops

The following stronger stat-changing removals are available but default to disabled:

- More Bullet Drop
- More Drag
- Negative Bullet Speed
- Negative Damage
- Negative Bullet Size
- Negative RPM
- Negative Max Durability
- More Recoil
- More Spread
- Slower Reload
- Faster Durability Loss
- Self Damage
- Reduced Projectile Force
- Extra Oil Durability Cost

The signed-value rules only suppress modifiers in the harmful direction:

- `ProjectileTimeScale < 0`: negative bullet speed;
- `Damage` or `DamageModifier < 0`: negative damage, for both Flat and percentage modifier types;
- `ProjectileScale < 0`: negative bullet size;
- `RPM < 0`: negative fire rate;
- `MaxDurability < 0`: reduced maximum durability cap;
- `KickMultiplier > 0`: more recoil;
- `Spread > 0`: wider spread cone;
- `ReloadSpeed < 0`: slower reload;
- `DurabilityLoss > 0`: faster per-shot durability loss;
- `EnchantmentSelfDamage > 0`: damages the wielder on fire;
- `ProjectileForce < 0`: weaker launch velocity.

Positive bullet-speed, damage, bullet-size, RPM, max-durability, and projectile-force modifiers, along with recoil-reducing, accuracy-improving, faster-reload, and durability-saving modifiers, remain active.

`More Bullet Drop` also suppresses its same-oil reduced-speed or increased-mass companion modifiers. Because reduced projectile speed belongs to both categories, it is removed when either `RemoveMoreBulletDrop` or `RemoveNegativeBulletSpeed` is enabled.

## Tooltip behavior

The game still shows the original oil effects. Lines for traits currently disabled by Perfect Oils are wrapped in TextMeshPro's strikethrough tag.

This works for:

- hovering an oil item;
- hovering an oil listed on an enchanted weapon;
- randomly selected oil enchantments.

The tooltip and runtime suppression use the same trait classifier and the same config values, so their states remain consistent.

## Implementation

Perfect Oils keeps `EnchantmentDefinition.modifiersApplied` intact. At database load it indexes exact modifier signatures by:

- enchantment source ID;
- `ItemAttributes` value;
- `StatModType`;
- modifier value;
- classified negative-trait flags.

A targeted prefix on `ItemStats.AddModifier(ItemAttributes, StatModifier)` ignores only a matching oil modifier whose corresponding config option is enabled. No weapon, loot, movement, aiming, projectile, or damage consumer is replaced.

The index contains every known negative signature regardless of the current settings, so config changes do not require rebuilding the asset database.

Oil stat modifiers are baked into each item's `ItemStats` when the oil is applied or the save is loaded, so a changed setting cannot retroactively alter an already-oiled weapon by itself. To make settings take effect live, changing any trait toggle (or the master `Enabled` switch) re-applies oil modifiers to the player's currently loaded items - equipped items, the backpack, the paperdoll, and any opened stash - by removing and re-adding each enchantment's modifiers so they pass through the suppression prefix again under the new config. Items in a stash that has never been opened this session are rebuilt the next time a setting changes while that stash is loaded. Tooltip changes are evaluated whenever the tooltip is rebuilt. The durability-cost setting updates the shared oil definitions immediately.

## Requirements

- SULFUR
- BepInExPack `5.4.2305`

## Build

1. Copy `LocalPaths.props.example` to `LocalPaths.props`.
2. Set `SulfurManagedDir` and `BepInExCoreDir`.
3. Build `PerfectOils.csproj` in Release mode.
4. Copy `PerfectOils.dll` to `BepInEx/plugins/PerfectOils/`.

No game DLLs, Unity assemblies, or BepInEx binaries are included in this source package.

## Config

Generated at:

`BepInEx/config/com.ryuka.sulfur.perfectoils.cfg`

### General

- `Enabled = true`
- `RemoveExtraDurabilityCost = false`

### Traits

Enabled by default:

- `RemoveDisableAiming = true`
- `RemoveExtraAmmoConsumeChance = true`
- `RemoveDecreaseAccuracyWhenMoving = true`
- `RemoveDecreaseMoveSpeed = true`
- `RemoveDecreaseJumpPower = true`
- `RemoveDecreaseLootChanceMultiplier = true`
- `RemoveDisableMoneyDrops = true`
- `RemoveDisableOrganDrops = true`

Disabled by default:

- `RemoveMoreBulletDrop = false`
- `RemoveMoreDrag = false`
- `RemoveNegativeBulletSpeed = false`
- `RemoveNegativeDamage = false`
- `RemoveNegativeBulletSize = false`
- `RemoveNegativeRpm = false`
- `RemoveNegativeMaxDurability = false`
- `RemoveMoreRecoil = false`
- `RemoveMoreSpread = false`
- `RemoveNegativeReloadSpeed = false`
- `RemoveFasterDurabilityLoss = false`
- `RemoveSelfDamage = false`
- `RemoveNegativeProjectileForce = false`

### Display / Debug

- `ShowRemovedTraitsWithStrikethrough = true`
- `DetailedLogging = false`

The existing `General.RemoveExtraDurabilityCost` key is retained for compatibility with v1.1.x configuration files.

## Runtime verification

The initialization log reports separate counts for:

- negative bullet speed;
- flat negative damage;
- percentage negative damage;
- negative bullet size;
- negative RPM.

This verifies the actual Addressables data loaded by the current game build, which is not stored in the managed DLLs.

## Author

ryuka
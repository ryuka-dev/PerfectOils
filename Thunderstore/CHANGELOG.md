# Changelog

## 1.3.7

* Compatibility with SULFUR 0.19. The game made its open-stash inventory grids private, which broke the live re-apply introduced in 1.3.6. Changing a trait setting once again updates oiled weapons that are sitting in a storage chest you have opened this session, not just the ones you are carrying.
* No gameplay or configuration changes. Existing settings files carry over untouched.

## 1.3.6

* Added `RemoveNegativeMaxDurability`: oils that lower a weapon's maximum durability cap (for example -30% or a flat reduction) can now be suppressed. Positive max-durability bonuses are kept. Disabled by default.
* Added `RemoveMoreRecoil`, `RemoveMoreSpread`, and `RemoveNegativeReloadSpeed`: the downside direction of the recoil (`KickMultiplier`), spread (`Spread`), and reload-speed (`ReloadSpeed`) stats can now be suppressed. Only the harmful direction is removed - recoil-reducing, accuracy-improving, and faster-reload modifiers are kept. All disabled by default.
* Added `RemoveFasterDurabilityLoss`, `RemoveSelfDamage`, and `RemoveNegativeProjectileForce`: suppress oils that increase the per-shot durability-loss multiplier (`DurabilityLoss`), damage the wielder on fire (`EnchantmentSelfDamage`), or reduce projectile launch force (`ProjectileForce`). Durability-saving and force-bonus modifiers are kept. All disabled by default. This completes coverage of every harmful-direction stat in the current oil data.
* Fixed trait toggles not taking effect until a reload: changing any suppression setting in-game now re-applies oil modifiers to the player's currently loaded items (equipped, backpack, paperdoll, and opened stashes) immediately, so already-oiled weapons update without re-oiling or restarting.
* Extended the live update to the master `Enabled` switch as well as every per-trait toggle (previously only the durability-cost option updated live).

## 1.3.5

* Fixed the More Bullet Drop option not working correctly.
* Fixed missing strikethrough text for Bullet Drop effects.
* Improved detection of Bullet Drop oil traits.
* Removed the unfinished stat-reading workaround from previous test versions.
* Fixed compilation errors from the diagnostic update.

## 1.3.0

- Added configurable removal of negative RPM modifiers.
- Added individual configuration options for all supported negative oil traits.
- Enabled only lower-impact quality-of-life removals by default.
- Disabled balance-sensitive ballistic, damage, bullet-size, RPM, and durability removals by default.
- Added 14-language localization for the SULFUR Config interface.

## 1.2.1

- Removed obsolete source-file references from the project.
- Replaced the deprecated Unity object lookup API.

## 1.2.0

- Added configurable removal of negative Bullet Speed.
- Added configurable removal of negative flat and percentage Damage.
- Added configurable removal of negative Bullet Size.
- Unified runtime suppression and tooltip-strikethrough decisions.

## 1.1.1

- Fixed oil detection for the current SULFUR item database.
- Added database retry behavior and clearer diagnostic logging.

## 1.1.0

- Preserved original oil descriptions.
- Added strikethrough display for effects removed by the mod.
- Applied suppression when oil modifiers are added to weapon stats.


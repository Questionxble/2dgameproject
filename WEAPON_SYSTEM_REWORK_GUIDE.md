# Weapon System Rework - Animation Event Based System

## Overview

The weapon system has been successfully reworked from a timer-based approach to an animation event-based system. This eliminates hard-coded duration values and makes attack durations scaleable with animation speed.

## Key Changes Made

### 1. Animation Event Methods Added

The following public methods have been added to `WeaponClassController.cs` for Unity Animation Events:

**Valor Shard (Melee Chain Attacks):**
- `ValorAttack1Start()` - Call when first attack (attackType=0) damage box should appear
- `ValorAttack1End()` - Call when first attack damage box should disappear
- `ValorAttack2Start()` - Call when second attack (attackType=1) damage box should appear
- `ValorAttack2End()` - Call when second attack damage box should disappear
- `ValorThrustStart()` - Call when thrust attack (attackType=2) damage box should appear
- `ValorThrustEnd()` - Call when thrust attack damage box should disappear

**Whisper Shard (Melee):**
- `WhisperMeleeAttackStart()` - Call when damage box should appear
- `WhisperMeleeAttackEnd()` - Call when damage box should disappear

**Storm Shard (Projectile):**
- `StormShardAttackEvent1()` - Call when first projectile should fire
- `StormShardAttackEvent2()` - Call when second projectile should fire

### 2. Animation State Tracking

- Added `IsPlayingAttackAnimation()` method to prevent attack spam
- No attacks can be performed while any attack animation is playing
- Animation events control the exact timing of damage/projectile spawning

### 3. Player Movement Integration

- Added `OnAttackAnimationEnd()` animation event method to `PlayerMovement.cs`
- Added overloaded `TriggerAttackAnimation(int attackType)` without duration parameter
- Animation events now control when attack state resets

### 4. Removed Hard-coded Timers

**Serialized Fields Removed:**
- `swordDuration` - No longer needed
- `swordAnimationDelay` - Replaced by animation events
- `daggerDuration` - No longer needed  
- `daggerAnimationDelay` - Replaced by animation events

**Methods Removed:**
- `DelayedSwordAttack()` - Replaced by animation events
- `DelayedDaggerAttack()` - Replaced by animation events
- `CreateDaggerAttack()` - Replaced by `CreateMeleeDamageObject()`

## Unity Setup Required

### 1. Animation Events Setup

For each weapon attack animation, add the following animation events at the appropriate keyframes:

**Valor Shard Left Click Chain Attacks:**
- **Animation attackType=0**: Add `ValorAttack1Start` when first swing damage should begin, `ValorAttack1End` when it should end
- **Animation attackType=1**: Add `ValorAttack2Start` when second swing damage should begin, `ValorAttack2End` when it should end  
- **Animation attackType=2**: Add `ValorThrustStart` when thrust damage should begin, `ValorThrustEnd` when it should end
- Add `OnAttackAnimationEnd` at the very end of each animation

**Whisper Shard Left Click Attack:**
- Add `WhisperMeleeAttackStart` event when damage should begin  
- Add `WhisperMeleeAttackEnd` event when damage should end
- Add `OnAttackAnimationEnd` event at the very end of the animation

**Storm Shard Left Click Attacks:**
- **Animation 1**: Add `StormShardAttackEvent1` when projectile should fire
- **Animation 2**: Add `StormShardAttackEvent2` when projectile should fire
- Add `OnAttackAnimationEnd` event at the end of both animations

### 2. Animation Event Function Names

Use these exact function names in Unity's Animation Event inspector:

**Valor Shard Chain:**
- `ValorAttack1Start` (no parameters) - First attack damage start
- `ValorAttack1End` (no parameters) - First attack damage end
- `ValorAttack2Start` (no parameters) - Second attack damage start
- `ValorAttack2End` (no parameters) - Second attack damage end
- `ValorThrustStart` (no parameters) - Thrust attack damage start
- `ValorThrustEnd` (no parameters) - Thrust attack damage end

**Whisper Shard:**
- `WhisperMeleeAttackStart` (no parameters)
- `WhisperMeleeAttackEnd` (no parameters)

**Storm Shard:**
- `StormShardAttackEvent1` (no parameters)
- `StormShardAttackEvent2` (no parameters)

**Universal:**
- `OnAttackAnimationEnd` (no parameters)

### 3. Animation Timing Guidelines

**Melee Attacks (Valor/Whisper):**
- `AttackStart` should trigger when the weapon swing reaches the damage zone
- `AttackEnd` should trigger when the weapon swing exits the damage zone
- This creates realistic hit windows that match visual weapon movement

**Projectile Attacks (Storm):**
- `AttackEvent1`/`AttackEvent2` should trigger when the casting motion reaches its peak
- Timing should match when you would expect a projectile to be launched visually

**Animation End:**
- `OnAttackAnimationEnd` should be the very last event in every attack animation
- This ensures the attack state is properly reset and new attacks can begin

## Benefits of New System

1. **Scaleable with Animation Speed**: Attack durations automatically scale with animation playback speed
2. **More Responsive**: No hard-coded delays that might feel sluggish
3. **Visually Accurate**: Damage timing matches exactly with visual weapon movement
4. **Prevents Spam**: Universal animation state checking prevents attack spamming
5. **Easier Balancing**: Adjust attack timing by moving animation events, not code values

## Testing the Changes

1. Equip each shard type (Valor, Whisper, Storm)
2. Perform left-click attacks
3. Verify that:
   - Only one attack can be performed at a time
   - Damage/projectiles appear at the correct visual timing
   - Attacks feel responsive and natural
   - Animation speed changes affect attack duration correctly

## Notes

- The old timer-based system has been completely removed
- All duration-related serialized fields have been removed from the inspector
- Storm Shard auto-fire still uses the interval system but individual shots use animation events
- Right-click attacks and special abilities still use their existing systems
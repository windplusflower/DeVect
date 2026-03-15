# DeVect (Gu Zhang Ji Qi Ren)

[**中文说明**](README.md) | **English Version**

`DeVect`, titled `骨杖寂骑人` in Chinese, is a Hollow Knight mod that brings Defect-style orb gameplay from *Slay the Spire* into the Knight's combat kit.

## Overview

Instead of casting the three vanilla spells directly, you convert them into three orbiting orb types: `Yellow`, `White`, and `Black`.

- Fireball spells create a `Yellow Orb`
- Shriek spells create a `White Orb`
- Dive spells create a `Black Orb`
- Every `3` valid nail hits, all active orbs trigger their own `passive` once
- If your slots are full, casting again pushes the rightmost orb out and triggers that orb's `evocation`

The result keeps Hollow Knight's spell inputs, but turns combat into a more Defect-like rhythm of storing, cycling, and evoking orbs.

## Name Meaning

`DeVect` is a reshaped name based on `The Defect`.

- It keeps the mechanical, unstable sound of `Defect`
- `Vect` can be read as a nod to `Vessel`, tying the name back to the Knight's identity in Hollow Knight
- Put together, the name sounds like both a malfunctioning construct and a vessel that has broken away from its intended role

The Chinese title, `骨杖寂骑人`, leans more into the Hollow Knight side of the fusion:

- `骨杖` echoes the nail and the Knight's bone-themed imagery
- `寂骑人` keeps the playful naming flavor while pointing to the Knight's silence, emptiness, and vessel identity

## Core Rules

- `Vengeful Spirit / Shade Soul` maps to `Yellow Orb`
- `Howling Wraiths / Abyss Shriek` maps to `White Orb`
- `Desolate Dive / Descending Dark` maps to `Black Orb`
- Spells still cost Soul, and `Spell Twister` still reduces the cost
- If you have not unlocked a given spell line yet, the matching orb cannot be generated
- All orb damage scales from your real current nail damage, not vanilla spell damage
- `Strength` and `Fury of the Fallen` raise base nail damage first, so they also raise orb value indirectly

## Orb Slots

| Rule | Effect |
|------|--------|
| Default capacity | Up to `3` active orbs |
| Flukenest | Expands the orb system to `4` slots |
| New orb order | New orbs enter from the left and push older ones right |
| Evocation order | The rightmost orb is the oldest and evokes first |
| Scene transitions | Orb order and stored values are preserved |

## White Spell / Dark Spell Tiers

Each Hollow Knight spell line has two tiers:

- `White spell`: the base version
- `Dark spell`: the upgraded version

In this mod, each orb reads the tier of its own matching spell line and uses different base values.

That means:

- Yellow checks the current tier of the fireball line
- White checks the current tier of the shriek line
- Black checks the current tier of the dive line

## Orb Types

### Yellow Orb

| Item | White spell | Dark spell |
|------|-------------|------------|
| Created by | Fireball spells | Fireball spells |
| Passive | Deals about `1/4` of current nail damage to one random nearby enemy | Deals about `1/3` of current nail damage to one random nearby enemy |
| Evocation | Deals about `0.75x` current nail damage to one random nearby enemy | Deals `1x` current nail damage to one random nearby enemy |
| Role | Steady chip damage and lighter early pressure | Stronger, more reliable single-target pressure |

### White Orb

| Item | White spell | Dark spell |
|------|-------------|------------|
| Created by | Shriek spells | Shriek spells |
| Starting damage | About `1/4` of current nail damage | About `1/3` of current nail damage |
| Passive | Hits all enemies in range for current stored damage, then loses `1` damage | Hits all enemies in range for current stored damage, then loses `1` damage |
| Extra rule | It shatters and disappears when its damage reaches `0` | It shatters and disappears when its damage reaches `0` |
| Evocation | Hits all enemies in range for `2x` its current stored damage | Hits all enemies in range for `2x` its current stored damage |
| Role | Milder early AoE scaling | Stronger sustained area control and wave clear |

Note: the White-vs-Dark difference for White Orb is mainly in its `starting damage`. Its passive decay and `2x` evocation structure stay the same.

### Black Orb

| Item | White spell | Dark spell |
|------|-------------|------------|
| Created by | Dive spells | Dive spells |
| Starting storage | About `0.75x` current nail damage | `1x` current nail damage |
| Passive | Does not hit immediately; stores about `0.75x` current nail damage instead | Does not hit immediately; stores `1x` current nail damage instead |
| Evocation | Hits the lowest-HP enemy in range for all stored damage | Hits the lowest-HP enemy in range for all stored damage |
| Role | Softer charge-up single-target orb | Full-strength burst, execute, and boss payoff orb |

## Charm Synergy

| Charm | Effect |
|------|--------|
| `Shaman Stone` | No longer buffs vanilla spells; it now buffs all three orb lines by percentage |
| `Flukenest` | No longer changes fireballs; it expands orb capacity from `3` to `4` |
| `Spell Twister` | Still only reduces Soul cost |
| `Soul Catcher` / `Soul Eater` | Currently unchanged beyond their normal behavior |
| `Fragile/Unbreakable Strength` / `Fury of the Fallen` | Indirectly buff orb value because orb scaling uses real current nail damage |

### Current Shaman Stone Rule

Shaman Stone no longer uses the old flat `+2` model. It now works like this:

- `extra damage = ceil(baseNailDamage * 0.2 * damageScale)`

Where:

- `baseNailDamage` is your real current nail damage after existing modifiers such as `Strength` and `Fury of the Fallen`
- `damageScale` is the base multiplier of that specific damage event relative to nail damage

Example:

- Yellow Orb `dark-spell passive` uses a base scale of `1/3`
- So its Shaman bonus becomes `ceil(baseNailDamage * 0.2 / 3)`

Additional notes:

- Yellow calculates Shaman bonus separately for passive and evocation because those two events use different scales
- White only gets its Shaman bonus once when its starting damage is created; later passive hits and `2x` evocation simply inherit the stored value
- Black gets Shaman bonus separately on starting storage and on each passive storage gain

## How It Plays

- Yellow is the most stable orb and works best as repeatable single-target pressure
- White is better for crowd control and sustained AoE pacing
- Black gets the most value when you can consistently land nail hits, trigger passives, build a large stored orb, and then force an evocation at the right moment
- If you only have the white-tier version of a spell, the matching orb is intentionally weaker until that spell line is upgraded

## Suggested Playstyles

- `Yellow-focused`: stable single-target pressure with frequent fireball conversions
- `White-focused`: repeated area damage and wave clear built around 3-hit passive timing
- `Black-focused`: build charge, then burst down priority targets with forced evocation
- `Mixed rotation`: the closest feel to a real Defect-style orb cycle inside Hollow Knight combat

## Tips

- If your spells are not coming out normally, that is usually intended: they have been converted into orbs
- If a direction never generates an orb, first check whether that spell line has been unlocked yet
- If you want faster passive triggers, focus on landing steady nail hits instead of only storing orbs in advance
- Once `Flukenest` gives you a fourth slot, orb order matters much more because your evocation order changes noticeably

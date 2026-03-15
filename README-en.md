# DeVect (Gu Zhang Ji Qi Ren)

[**中文说明**](README.md) | **English Version**

`DeVect`, titled `骨杖寂骑人` in Chinese, is a Hollow Knight mod that brings Defect-style orb gameplay from *Slay the Spire* into the Knight's moveset.

## Introduction

Instead of casting the three vanilla spells directly, you convert them into three orbiting orb types: Yellow, White, and Black. These orbs trigger passive effects while you fight with your nail, and they evoke when you cast again at full capacity.

### Name Meaning

`DeVect` is a reshaped name based on `The Defect`.

- It keeps the mechanical, unstable sound of `Defect`
- `Vect` can be read as a nod to `Vessel`, tying the name back to the Knight's identity in Hollow Knight
- Put together, the name sounds like both a malfunctioning construct and a vessel that has broken away from its intended role

The Chinese title, `骨杖寂骑人`, leans more into the Hollow Knight side of the fusion:

- `骨杖` echoes the nail and the Knight's bone-themed imagery
- `寂骑人` keeps the playful naming flavor while pointing to the Knight's silence, emptiness, and vessel identity

## Core Rules

- `Vengeful Spirit / Shade Soul` creates a `Yellow Orb`
- `Howling Wraiths / Abyss Shriek` creates a `White Orb`
- `Desolate Dive / Descending Dark` creates a `Black Orb`
- Spells still cost Soul, and `Spell Twister` still reduces the cost
- Every `3` valid nail hits, all active orbs trigger their `passive` once
- If your slots are full, casting again pushes the rightmost orb out and triggers that orb's `evocation`

## Orb Slots

| Rule | Effect |
|------|--------|
| Default capacity | Up to `3` active orbs |
| Flukenest | Expands the orb system to `4` slots |
| New orb order | New orbs enter from the left and push older ones right |
| Evocation order | The rightmost orb is the oldest and evokes first |
| Scene transitions | Orb order and stored values are preserved |

## Orb Types

### Yellow Orb

| Item | Effect |
|------|--------|
| Created by | Fireball spells |
| Passive | Deals about `1/3` of your current nail damage to one random nearby enemy |
| Evocation | Deals `1x` your current nail damage to one random nearby enemy |
| Role | Steady chip damage and reliable single-target pressure |

### White Orb

| Item | Effect |
|------|--------|
| Created by | Shriek |
| Starting damage | About `1/3` of your current nail damage |
| Passive | Hits all enemies in range for its current stored damage, then loses `1` damage |
| Extra rule | It shatters and disappears when its damage reaches `0` |
| Evocation | Hits all enemies in range for `2x` its current stored damage |
| Role | Crowd control, wave clear, and sustained AoE pressure |

### Black Orb

| Item | Effect |
|------|--------|
| Created by | Dive |
| Starting storage | About `0.75x` your current nail damage |
| Passive | Does not hit immediately; stores an extra `1x` current nail damage instead |
| Evocation | Hits the lowest-HP enemy in range for all stored damage |
| Role | Charge-up burst, execution, and boss-focused payoff |

## Charm Synergy

| Charm | Effect |
|------|--------|
| `Shaman Stone` | No longer buffs vanilla spells; it now directly increases orb damage and stored damage |
| `Flukenest` | No longer changes fireballs; it expands orb capacity from `3` to `4` |
| `Spell Twister` | Still only reduces Soul cost |
| `Soul Catcher` / `Soul Eater` | Currently unchanged beyond their normal behavior |
| `Fragile/Unbreakable Strength` / `Fury of the Fallen` | Indirectly buff orb damage because orb scaling uses real current nail damage |

### Exact Shaman Stone Benefits

- Yellow passive and evocation gain `+2` damage
- White starting damage gains `+2`
- Black starting storage and each passive storage gain `+2`

## How It Plays

- All orb damage scales from your real current nail damage, not vanilla spell damage
- Yellow picks random nearby enemies; White and Black use the same close-range combat area
- Black gets much stronger if you can consistently land enough nail hits to trigger passives
- White is best for sustained AoE pressure, Black is your payoff burst orb, and Yellow is the most stable all-round option

## Suggested Playstyles

- `Yellow-focused`: stable single-target pressure
- `White-focused`: frequent area damage and wave clear
- `Black-focused`: build charge, then burst down priority targets
- `Mixed rotation`: the closest feel to a real Defect-style orb cycle inside Hollow Knight combat

## Tips

- If your spells are not coming out normally, that is expected; they have been converted into orbs
- If you want faster passive triggers, focus on landing steady nail hits instead of only storing orbs in advance
- Once `Flukenest` gives you a fourth slot, orb order matters much more because evocation order changes noticeably

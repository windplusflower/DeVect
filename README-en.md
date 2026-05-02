# DeVect

[**中文说明**](README.md) | **English Version**

`DeVect` is a Hollow Knight mod that turns the Knight's spell inputs into a Defect-style orb system.

Combat revolves around `Lightning Form` and `Ice Form`, with `form swapping`, `passive triggers`, `queued orb generation`, `full-slot evocation`, and `ice shield absorption` as the main mechanics.

## Core Gameplay

- `Spell button only`: swap between `Lightning Form` and `Ice Form`
- `Down + spell`: use the current form's small skill
- `Up + spell`: use the current form's big skill
- Every `form swap` immediately triggers the `passive` of all active orbs
- New orbs are generated in sequence with a short delay between each one
- When your slots are full, the new orb still enters and the rightmost old orb gets pushed out and `evokes`

The system operates through form swapping, orb order, queued generation, and ice shield absorption.

## Inputs and Costs

This readme uses `Spirit/Wave`, `Dive`, and `Shriek` as shorthand for the three spell inputs.

### Form Swap

- Costs `1` spell cast
- Swaps between `Lightning Form` and `Ice Form`
- Immediately triggers all current orb passives

### Small Skill

- Costs `1` spell cast
- `Lightning Form`: generate `1` Lightning Orb
- `Ice Form`: generate `1` Ice Orb

### Big Skill

- Costs `3` spell casts
- `Lightning Form`: generate a number of Lightning Orbs equal to `the total Lightning Orbs already generated in the current room`
- `Ice Form`: generate `number of enemies within 20 range + 2` Ice Orbs, then immediately deal `1x` current nail damage to all enemies in that radius; if `Shaman Stone` is equipped, this becomes `ceil(current nail damage x 4/3)`

Extra notes:

- `Spell Twister` still reduces spell cost normally, so it also lowers the total cost of swapping, small skills, and big skills
- The extra Lightning Orbs created by Lightning big skill are also added to the current room count
- If you have not generated any Lightning Orbs in the current room yet, your first Lightning big skill will spend Soul but generate no orbs

## Unlock Rules

- `Spirit/Wave` is the input used for `form swapping`
- As long as the `Vengeful Spirit / Shade Soul` line is unlocked, Lightning Form can generate Lightning Orbs; Lightning Orb damage no longer splits by white/dark spell tier
- `Dive` is the input used for the `small skill`; without the dive spell line, Ice Form cannot generate Ice Orbs
- `Shriek` is the input used for the `big skill`
- If the orb type of your current form is not unlocked yet, that form's small skill and big skill will not generate orbs

## Orb Slot Rules

| Rule | Effect |
|------|--------|
| Default capacity | `3` active slots |
| `Flukenest` | Expands the system to `4` slots |
| Entry order | New orbs always enter from the left and push older ones to the right |
| Evocation order | The rightmost orb is the oldest and evokes first |
| Queued generation | During orb generation you can still move and keep casting to queue more, but you cannot attack |
| Scene transitions | Clear active orbs, clear the pending queue, and reset the room's Lightning count |
| Bench save | Clears current orbs and current ice shield |

Extra notes:

- Your current form does not reset on normal scene transitions
- Ice shield is not cleared by normal room transitions, but it is cleared when saving at a bench

## Orb Design

## Lightning Orb

Lightning Orb has the following effects.

### Passive

- Deals `ceil(current nail damage x 1/3)` to one random nearby enemy

### Evocation

- Deals `ceil(current nail damage x 2/3)` to one random nearby enemy

## Ice Orb

Ice Orb does not deal damage directly. Its job is to build ice shield.

### Passive

- Gain `1` ice petal

### Evocation

- Gain `3` ice petals

### Ice Shield Rules

- Every `4` petals `=` `1` damage blocked
- Fewer than `4` petals are only stored and do not block damage yet
- You can store up to `16` petals total, which means up to `4` damage blocked
- Damage is absorbed by complete shield layers first

## Numerical Design

All major numbers are built around `current nail damage`.

- Lightning Orb damage scales from your current nail damage
- Ice big skill damage uses your current nail damage by default; with `Shaman Stone`, it becomes `ceil(current nail damage x 4/3)`
- `Strength` and `Fury of the Fallen` raise nail damage first, then indirectly raise these orb values
- For balance, the Knight's own nail swings and nail arts are clamped to `1` damage against enemies

As a result, orb damage and Ice-form big skill damage scale with your current nail damage, while the Knight's normal nail and nail-art hits still land as `1` damage against enemies.

## Charm Synergy

| Charm | Current effect |
|------|----------------|
| `Shaman Stone` | Buffs Lightning Orb damage and raises Ice-form big skill AoE to `ceil(current nail damage x 4/3)` |
| `Flukenest` | Expands orb capacity from `3` to `4` |
| `Spell Twister` | Reduces Soul cost for swaps, small skills, and big skills as normal |
| `Soul Catcher` / `Soul Eater` | Keep their normal behavior |
| `Strength` / `Fury of the Fallen` | Indirectly buff orb performance by raising current nail damage |

### Shaman Stone

Shaman Stone has two effects.

- Lightning Orb bonus damage formula: `ceil(current nail damage x 0.2 x event multiplier)`
- Ice-form big skill AoE damage: `ceil(current nail damage x 4/3)`

Example:

- Lightning passive always uses a `1/3` multiplier
- Its Shaman bonus becomes `ceil(current nail damage x 0.2 x 1/3)`

Extra notes:

- Lightning evocation uses the same rule, with bonus damage `ceil(current nail damage x 0.2 x 2/3)`
- Ice Orb shield generation itself is not affected by `Shaman Stone`

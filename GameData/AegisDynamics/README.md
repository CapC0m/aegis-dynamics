# Aegis Dynamics

KSP 1.12 mod adding regen-cooled heatshield engines inspired by Stoke Space's Andromeda upper stage. Combines a heatshield and ring of thrust chambers into a single integrated part, with thrust vector control via differential throttling.

## Features

- **Single TweakScalable engine** (1.25m to 7.5m) replacing five fixed variants
- **Adjustable chamber count** (6 to 36) — each chamber adds thrust and mass
- **Active heatshield cooling** — burns propellant during reentry to absorb heat
- **Stock gimbal TVC** with slight thrust reduction during gimbal action
- **CryoTanks compatibility** — auto-swaps to LH2 + Oxidizer when CryoTanks is installed
- **ReStock compatibility** — adjusted attach node positioning

## Dependencies
### Required dependencies
- ModuleManager
- B9PartSwitch

### Recommended dependencies
- Waterfall
- StockWaterfallEffects
- TweakScale
- MechJeb2
- CryoTanks
- ReStock

## Installation
Drop the `GameData/AegisDynamics` folder into your KSP `GameData` directory.
Should be supported by CKAN too.

## Usage
The Aegis Thureos appears in the Engines tab of the VAB. Place it as the bottom of your stack. The chamber count slider in the part action window controls thrust and mass; TweakScale right-click options control size.

During reentry, active cooling activates automatically when heat flux exceeds the threshold. It draws from your craft's LiquidFuel and Oxidizer reserves at the engine's mixture ratio.



## Changelog
## v0.3.2 — Stock plume integration
### Stock plume integration
This update solely focuses on making Waterfall and Stock Waterfall Effects a **required** dependancy instead of a required one. The mod now works without these mods installed, although they are still strongly recommended for your eye's comfort.


## v0.3.1 — Single TweakScalable Engine, Stock Gimbal TVC
### TVC redesign

The differential-throttle TVC system is replaced with **stock ModuleGimbal**. This makes the engine behave like any standard gimbaled engine for SAS, MechJeb, and other autopilots. Benefits:

- Smoother control, especially through TimeWarp
- Full MechJeb compatibility (no more KSP Community Fixes interaction issues)
- 5° equivalent gimbal range, configurable in cfg

To preserve some of the old "differential thrust" feel, gimbaling now applies a **slight thrust reduction** (up to 10% at full deflection). 


### Reentry balance

Cooling is now more fuel-efficient (10,000 kJ/unit vs 8,000), but the part is less invulnerable when cooling runs out:
- skinMaxTemp reduced from 3300K to 1700K
- thermalMassModifier reduced from 2.0 to 1.0
- Active cooling becomes essential rather than optional during steep reentries

### Mass scaling

Mass scaling is tuned to L^2.0 (was L^2.5 in v0.2 conceptually). 
Default 3.75m: 7.4t with 18 chambers. Smooth across all TweakScale sizes.


## v0.3.0
### Major change

The five fixed-size variants (Aspis, Pelta, Hoplon, Thureos, Scutum) and the composite architecture parts (Shield 3.75m + Chamber) are replaced by a single **Aegis Thureos** engine. Use TweakScale to set size from 1.25m to 7.5m via the part's right-click menu. All previous features (chamber count slider, active cooling, CryoTanks compatibility, differential throttle TVC) carry over to the unified part.

### Why this change

- Single part to maintain instead of five variants plus two composite parts
- Continuous size choice via TweakScale instead of discrete steps
- Cleaner parts panel
- All sizes share the same balance pass

### Breaking changes

⚠ Existing craft using Aspis, Pelta, Hoplon, Scutum, the previous Thureos, Aegis Shield 3.75m, or Aegis Chamber will not load. Recreate the engine on affected craft using the new Aegis Thureos at the appropriate scale.

### Other changes

- TweakScale is now necessary to adapt to all formfactors
- Composite architecture (Shield + Chamber) deferred — may return in a future release if there's user interest
  

## v0.2.0
- Added composite architecture: Aegis Shield 3.75m + Chamber
- Added active heatshield cooling for integrated variants
- Added chamber count slider with live mass and thrust scaling via `IPartMassModifier`
- Added CryoTanks compatibility (hydrolox mode for engines and cooling)
- Engine rebalance: smaller variants up, larger down, smoother scaling
- License switch to MIT (after discussing with the community, the use of AI make licensing tricky at best. For now, MIT seems more permissive, may totally un-license later. Feedback appreciated)
- Removed deprecated CleanupStoke.cfg patch

## v0.1.3
- Renamed mod from "Stoke Engine" to "Aegis Dynamics"
- Greek-themed variant names (Aspis, Pelta, Hoplon, Thureos, Scutum)

## v0.1.2
- ReStock compatibility patch
- KSP-AVC version file

## v0.1.1
- Initial public release


## Development & Licensing

Aegis Dynamics is developed with substantial AI coding assistance (Anthropic's Claude). All architectural decisions, balance tuning, debugging, and integration testing are performed by the human author. AI assistance is disclosed for transparency, not as a legal disclaimer. (after discussing with the community, the use of AI make licensing tricky at best. For now, MIT seems more permissive, may totally un-license later. Feedback appreciated)

Licensed under the [MIT License](LICENSE). You can use, modify, redistribute, or fork this mod freely (including commercially). Just keep the copyright notice with any redistribution.

## Contributing

Bug reports and pull requests welcome. The mod is small enough that significant contributions can land quickly. Open an issue to discuss before writing patches for major architectural changes.

## Credits

- Inspired by Stoke Space's Andromeda upper stage design
- Built on Anthropic's Claude as a development collaborator
- KSP modding ecosystem: ModuleManager, B9PartSwitch, Waterfall, ReStock, CryoTanks, MechJeb
- Stock KSP heatshield meshes used by reference

## Source

[https://github.com/CapC0m/aegis-dynamics](#)
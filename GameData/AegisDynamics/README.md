# Aegis Dynamics

KSP 1.12 mod adding regen-cooled heatshield engines inspired by Stoke Space's Andromeda upper stage. Combines a heatshield and ring of thrust chambers into a single integrated part, with thrust vector control via differential throttling.

## Features

**Two architectures**, both shipped:

- **Integrated parts** (Aspis through Scutum, 5 sizes): single-part heatshield engines with built-in TVC. Stock-playable, no MechJeb required.
- **Composite architecture** (Shield 3.75m + Chamber): modular shield with 24 attach nodes; users place individual chambers manually. Requires MechJeb for differential throttle TVC. Mostly added for development purposes.

**Active heatshield cooling**: integrated parts consume propellant during reentry to dissipate convective heat. Replaces the stock Ablator system. Cooling auto-activates above threshold flux.

**Chamber count slider**: in-editor adjustment of how many chambers each engine has. Thrust and mass scale accordingly.

**Per-chamber visualization**: chambers spawn procedurally as transforms with individual Waterfall plumes. PAW shows live per-chamber throttle percentages.

**CryoTanks compatibility**: when CryoTanks is installed, all Aegis engines burn LqdHydrogen + Oxidizer at hydrolox Isp (450 vac / 380 sl).

**ReStock compatibility**: integrated parts adapt their attach node positions when ReStock is detected.

## Parts list

### Integrated variants (no extra dependencies for TVC)

| Part | Diameter | Default chambers | Thrust per chamber | Total thrust at default |
|---|---|---|---|---|
| Aspis | 1.25m | 6 | 30 kN | 180 kN |
| Pelta | 1.875m | 9 | 40 kN | 360 kN |
| Hoplon | 2.5m | 12 | 50 kN | 600 kN |
| Thureos | 3.75m | 18 | 60 kN | 1080 kN |
| Scutum | 5m | 24 | 80 kN | 1920 kN |

Each variant accepts the chamber count slider (range 6–24 chambers per part). Thrust and mass adjust live in the VAB.

### Composite architecture (MechJeb required for TVC)

| Part | Description |
|---|---|
| Aegis Shield 3.75m | Passive 3.75m heatshield with 24 attach nodes for chambers |
| Aegis Chamber | Individual 37.5 kN thrust chamber, attaches to shield mount nodes |

To use: place the shield, then attach 24 chambers to its ring nodes (use 6x symmetry for efficiency). Enable differential throttle in MechJeb's Attitude Adjustment.

## Dependencies

**Required**:
- ModuleManager (4.x)
- B9PartSwitch (2.x)
- Waterfall (0.10.x)

**Recommended**:
- Stock Waterfall Effects — for engine plume templates

**Optional but supported**:
- MechJeb — required for composite architecture TVC; helpful for integrated parts too
- CryoTanks — switches all Aegis engines to hydrolox propellants
- ReStock — visual adjustments for integrated parts

## Installation

1. Install dependencies via CKAN, or download from their respective pages
2. Download the latest Aegis Dynamics release zip
3. Extract to your KSP install — should look like `GameData/AegisDynamics/`
4. Verify the mod loads: launch KSP, check the parts list under Engines

CKAN support: pending listing. For now, install manually.

## Usage tips

**Integrated variants**: place like a heatshield. The chamber count slider is in the part's editor PAW. Differential TVC is automatic and works with stock SAS, MechJeb, or any flight assistance mod that uses ITorqueProvider.

**Composite architecture**: place the shield. Open chamber's part info in the parts panel. Use 6x symmetry to attach 6 chambers at once; repeat 4 times to fill all 24 nodes. Save as a subassembly for reuse on future craft.

**Active cooling**: works automatically during reentry. Skin temperature is held below 2400 K by consuming propellant from connected tanks. Plan your reentry budget — significant heating eats real fuel.

**With CryoTanks**: install CryoTanks, then any Aegis part automatically uses LH2/Oxidizer instead of LF/Oxidizer. Use CryoTanks-style hydrolox tanks for proper drain ratios. Existing LF/Ox craft will need migration.

## Known limitations

- Composite architecture chambers must be placed manually (KSP symmetry can't populate internal ring nodes; 4 rounds of 6x symmetry works)
- Composite architecture requires MechJeb for thrust vector control
- All current parts use stock heatshield meshes with cfg modifications; custom models are deferred
- Per-chamber plume variation on integrated parts is not yet implemented
- RealFuels and RSS compatibility not yet supported
- Mass, cost, power, cooling etc : all those parameters will probably be re-balanced in the future. Feedback wanted !

## Changelog

### v0.2.0 (current)
- Added composite architecture: Aegis Shield 3.75m + Chamber
- Added active heatshield cooling for integrated variants
- Added chamber count slider with live mass and thrust scaling via `IPartMassModifier`
- Added CryoTanks compatibility (hydrolox mode for engines and cooling)
- Engine rebalance: smaller variants up, larger down, smoother scaling
- License switch to MIT (after discussing with the community, the use of AI make licensing tricky at best. For now, MIT seems more permissive, may totally un-license later. Feedback appreciated)
- Removed deprecated CleanupStoke.cfg patch

### v0.1.3
- Renamed mod from "Stoke Engine" to "Aegis Dynamics"
- Greek-themed variant names (Aspis, Pelta, Hoplon, Thureos, Scutum)

### v0.1.2
- ReStock compatibility patch
- KSP-AVC version file

### v0.1.1
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
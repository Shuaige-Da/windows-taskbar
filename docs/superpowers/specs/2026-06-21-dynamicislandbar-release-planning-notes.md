# DynamicIslandBar Release Planning Notes

> Status: Brainstorming in progress. This is a working planning record, not the final approved design spec.

## Context

Project: `DynamicIslandBar`

Current direction:
- Turn the current prototype into a distributable Windows desktop product.
- Keep package size as small as practical.
- Reach a release quality that feels formal and dependable.
- Add a visual settings experience in a later implementation phase, but include a lightweight polished settings surface in V1.
- Leave room for more customization styles and future extensibility.

## Decisions Locked So Far

### Product Goal

The product should evolve from the current custom capsule taskbar prototype into a real Windows application that users can download, install, run long-term, and trust.

### V1 Positioning

V1 is a `stable installable release`, not just an internal prototype build.

That means V1 should prioritize:
- installability
- stability
- clean uninstall
- configuration persistence
- low operational friction
- a small but product-quality settings surface

### Settings Scope for V1

Chosen direction: `core settings + a small amount of visual customization`

V1 should include:
- core behavior settings
- module visibility toggles
- startup behavior
- basic layout/display adjustments
- a few predefined visual styles
- limited appearance customization

V1 should not try to become a full theme editor yet.

### Distribution Strategy

Chosen direction: `offline single-machine installer`

Implications:
- no mandatory cloud dependency
- no V1 auto-update requirement
- manual update delivery is acceptable in V1
- focus on a small, reliable installer footprint

### OS Support Strategy

Chosen direction: `architect for Windows 10 and 11 compatibility, but only promise Windows 11 at launch`

Implications:
- V1 testing and release claims target Windows 11
- architecture should avoid blocking a later Windows 10 support phase
- Windows-specific behaviors should be isolated behind clearly defined system integration layers

### Release Quality Floor

Chosen direction: `installer + clean uninstall + crash logging`

This is the minimum acceptable definition of "formal delivery" for V1.

V1 must include:
- install package
- uninstall path
- crash logging or failure log capture

V1 does not yet require:
- code signing
- import/export settings
- built-in diagnostics center

## Recommended Product Roadmap

Three roadmap options were considered:

1. Core shell first
2. Platform skeleton first
3. Visual settings first

Recommended approach: `core shell first`

Reason:
- best match for a stable installable V1
- best package size control
- lowest risk of over-engineering too early
- strongest path to a trustworthy first public release

## Recommended Phase Breakdown

### V1: Stable Installable Release

Goal:
- Users can install it, keep it running, and use it with confidence.

Expected content:
- main capsule shell
- core system interaction modules
- stable background behavior
- persistent configuration
- startup option
- basic in-app permission handling
- crash logging
- offline installer and uninstall flow
- small polished settings surface
- a few visual presets

### V2: Product Maturity Release

Goal:
- Make the product feel mature and operationally solid.

Likely focus:
- richer settings
- broader compatibility work
- diagnostics
- import/export
- better recovery and troubleshooting flows
- performance and memory optimization

### V3: Customization and Style Expansion

Goal:
- Deep user customization and a more expressive product identity.

Likely focus:
- theme system
- style packs
- advanced layout customization
- extensible module architecture
- more ambitious visual language

## V1 Explicitly Out of Scope

These items should remain out of V1 unless requirements change:
- automatic update system
- account system
- cloud sync
- plugin marketplace
- full visual theme editor
- broad Windows 10 compatibility promise
- large complex settings surface
- heavy runtime/dependency additions without strong justification

## Product/Architecture Direction Emerging

The product is naturally decomposing into three workstreams:

1. `Release-grade desktop shell`
- runtime stability
- system integration
- installer/uninstaller
- logs
- packaging

2. `Settings system`
- configuration model
- persistence
- lightweight V1 UI
- later visual settings expansion

3. `Customization foundation`
- visual preset model
- future theme/style abstractions
- future module/style extensibility boundaries

## Important Findings from Current Technical Work

Recent implementation work on the current prototype established:
- WiFi behavior is affected by Windows location/admin restrictions, not just app logic.
- App-level permission prompts are now conceptually part of the product direction.
- Audio device enumeration had a COM interface issue that was fixed.
- Hover/click interaction patterns for WiFi, volume, and running-app panels are being refined into a more productized interaction model.

These findings matter for V1 because they indicate the product will need:
- better system capability detection
- clearer error and permission messaging
- safer integration boundaries around Windows APIs

## Open Questions for Tomorrow

These still need to be defined before the final design spec can be written:

1. Which new V1 features must be added beyond the current capsule behavior?
2. What exact settings must exist in the first visual settings surface?
3. Which visual presets should ship in V1?
4. What installer technology should be used for smallest practical footprint and clean uninstall?
5. What logging strategy should be used for crashes and recoverable failures?
6. What exact startup/runtime model should the app use:
   - tray/background app
   - startup task
   - single-instance enforcement
7. What configuration format should be used:
   - JSON
   - versioned config schema
   - migration strategy
8. What release checklist defines "ready to ship" for V1?

## Recommended Next Discussion Order

Tomorrow's planning session should continue in this order:

1. List candidate V1 new features
2. Split them into:
   - must-have for V1
   - optional for V1
   - move to V2
3. Define V1 settings information architecture
4. Define release packaging strategy
5. Define logging/error-handling strategy
6. Define technical architecture boundaries
7. Convert the approved plan into a formal design spec

## Resume Prompt

When resuming tomorrow, start from:

`We are continuing the DynamicIslandBar V1 release planning. First task: list the new V1 features that must be added beyond the current prototype.`

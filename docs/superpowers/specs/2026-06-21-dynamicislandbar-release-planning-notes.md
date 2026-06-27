# DynamicIslandBar Release Planning Notes

> Status: V1 baseline accepted on 2026-06-27. This document is now the living release plan for the first public-quality Windows build.

## V1 Baseline Snapshot

This project now has a first accepted V1 baseline.

GitHub branches:
- `win-ui1.0`: V1 functional baseline.
- `win-ui1.0-优化版`: V1 optimized baseline after code-simplifier-style cleanup.

Current V1 definition:
- A polished capsule taskbar shell that can run as a Windows desktop app.
- Bottom taskbar mode that visually replaces the system taskbar area.
- Top island mode that keeps system modules inside a compact capsule.
- Running app icons with activate/minimize behavior.
- App overflow folder for icons that no longer fit.
- Right-click app actions for visibility, favorites, launch/open, and close/exit flows.
- Hover-driven WiFi, volume, running-app, overflow, and permission panels.
- WiFi list, WiFi settings entry, sound settings entry, and audio device interactions.
- Permission prompt model for sensitive system capabilities.
- Glass capsule visual system with flowing marquee border, icon-accent glow, and matching floating panels.
- Appearance controls for style, opacity, shadow, glow brightness, glow thickness, glow speed, capsule thickness, and capsule length.
- Persistent local configuration for behavior, appearance, favorites, hidden apps, and known launch paths.
- Regression tests covering core layout, config, visual contracts, app actions, system text, and running-app behavior.

Verification for accepted V1:
- `dotnet test D:\UI-win\DynamicIslandBar.Tests\DynamicIslandBar.Tests.csproj` passes.
- `dotnet build D:\UI-win\DynamicIslandBar\DynamicIslandBar.csproj` passes with 0 warnings and 0 errors.

V1 is now considered the first product baseline, not a throwaway prototype. Future work should build on `win-ui1.0-优化版` unless a specific comparison with the raw V1 baseline is needed.

## Context

Project: `DynamicIslandBar`

Current direction:
- Turn the accepted V1 baseline into a distributable Windows desktop product.
- Keep package size as small as practical.
- Reach a release quality that feels formal and dependable.
- Replace the current right-click settings surface with a fuller visual settings page in a later implementation phase.
- Leave room for more customization styles, background images, resizing, drag positioning, and future extensibility.

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

### V1: Accepted Capsule Baseline

Goal:
- Lock the first usable capsule taskbar experience and stop treating the core interaction model as experimental.

Delivered content:
- main capsule shell
- core system interaction modules
- stable background behavior
- persistent configuration
- basic in-app permission handling
- lightweight right-click settings surface
- visual presets and appearance sliders
- running app integration
- dual bottom/top layout modes
- matching glass floating panels

Still required before a downloadable public installer:
- installer and uninstall flow
- startup option
- crash logging or failure log capture
- single-instance enforcement review
- release packaging size pass
- final smoke test checklist on a clean Windows 11 machine

### V1.1: Release Packaging Pass

Goal:
- Turn the accepted V1 baseline into an installable `.exe` release.

Focus:
- choose installer technology
- exclude debug/build artifacts from release output
- add clean uninstall path
- add startup option
- add crash/failure logging
- document minimum runtime requirement or publish self-contained if size is acceptable
- define release folder structure and version naming

### V1.2: Settings Page Pass

Goal:
- Move customization from the right-click menu into a clearer visual settings page while keeping the current menu as a quick-access path if useful.

Focus:
- style selection
- opacity/shadow/glow controls
- capsule thickness and length controls
- module visibility controls
- startup behavior
- background image upload foundation
- reset-to-default action

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

These items remain out of the accepted V1 baseline unless requirements change:
- automatic update system
- account system
- cloud sync
- plugin marketplace
- full visual theme editor
- broad Windows 10 compatibility promise
- heavy runtime/dependency additions without strong justification

These can be reconsidered after V1 packaging is stable:
- full visual theme editor
- import/export settings
- advanced diagnostics center
- code signing
- Windows 10 support claim

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

## Remaining Open Questions

These still need to be defined for the downloadable release pass:

1. Which installer technology gives the best balance of small size, fast install, and clean uninstall?
2. Should the first public package be framework-dependent or self-contained?
3. What crash/failure logging format should be used?
4. Should startup be controlled by registry, Startup folder shortcut, or installer option?
5. How should single-instance enforcement behave if the user launches the app twice?
6. What exact release checklist defines "ready to upload"?
7. Which visual settings should move first into the future settings page?
8. Should the right-click quick settings remain after the visual settings page exists?

## Recommended Next Work Order

Next implementation should continue in this order:

1. Clean repository hygiene:
   - ignore `bin/obj`, screenshots, temp debug files, and local backups
   - remove tracked build artifacts in a dedicated cleanup commit if approved
2. Define installer strategy.
3. Add startup and single-instance behavior.
4. Add crash/failure logging.
5. Create release packaging command.
6. Build the visual settings page.
7. Add background image customization foundation.
8. Run clean-machine Windows 11 smoke testing.

## Resume Prompt

When resuming release work, start from:

`We have accepted DynamicIslandBar V1 on branch win-ui1.0-优化版. Next task: turn the V1 baseline into a clean installable Windows release by choosing packaging, startup behavior, single-instance behavior, and crash logging.`

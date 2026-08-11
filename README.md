# Luminary

> **Work in progress.** Luminary is in early development — core concepts are in place but many features are incomplete or not yet started.

A desktop lighting control application for theatrical and live event lighting. Luminary lets you configure DMX fixtures, build cue lists, and transmit live lighting data to hardware via the Art-Net protocol.

## Status

### Implemented
- Fixture model with pluggable capability types: dimmer, RGB color, pan/tilt (standard and fine-channel variants), organised into a manufacturer → model → mode library
- DMX universe model (512 channels) with a pluggable, per-universe output: disabled, **Art-Net 3** (broadcast), or **Art-Net 4** (discovery + unicast)
- Art-Net transmission over UDP at ~40 fps with per-universe sequence numbering and correct 15-bit Port-Address (Net / Sub-Net / Universe):
  - **Art-Net 3** — broadcast or fixed-IP; compatible with the majority of gear
  - **Art-Net 4** — discovers nodes via `ArtPoll` / `ArtPollReply` and unicasts each universe only to its subscribers, with manual fallback targets, plus a responder so Luminary appears in other software's source lists
- Fixture list and capability editor UI with custom level faders and an XY pan/tilt pad
- In-app patch: a **Patch** window to add/remove universes and fixtures, choose each universe's output mode and settings, and set a fixture's personality / universe / DMX address — including a searchable Add-Fixture browser (manufacturer → fixture → mode)
- Cue model with major/minor numbering, labels, fade times, and per-fixture snapshots
- Cue list editing: record (snapshots the live output), delete, reorder (renumbering), inline rename, and per-cue fade times — kept in numeric order at all times
- Cue playback: a selected/active split (click selects, GO fires and advances), driven by a time-based crossfade engine
- Per-property fade behaviour: parameters fade or snap during a crossfade, attributed on the capability
- HTP/LTP merging: manual (fader) and playback (cue) layers combine into the DMX output per property — Highest- or Latest-Takes-Precedence — with the live cue value shown as an indicator on each fader and pad
- Show files: JSON save/load (`.lum`) behind a `ShowStore` abstraction — New / Open / Save / Save As, stable fixture IDs, and unsaved-changes prompts on new / open / quit
- App icon and a startup splash screen

### Not yet implemented
- Additional output protocols (sACN, USB-DMX, …)
- Synchronised multi-universe output (Art-Net `ArtSync`)
- External fixture personalities (planned via plug-in DLLs)
- Group control
- Effects engine
- Small DSL for macros
- Keybinding
- Midi controller input

## Tech Stack

- **C# / .NET 10** — application runtime
- **Avalonia 12** — cross-platform XAML UI (Fluent theme)
- **CommunityToolkit.Mvvm** — MVVM reactive bindings
- **System.Text.Json** — show-file serialization
- **Art-Net over UDP** — DMX transmission protocol

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Run

```bash
dotnet run
```

### Build

```bash
dotnet build
```

## Project Structure

```
src/
├── ArtNet/          # Art-Net packet formatting, discovery, and UDP transmission
├── Model/           # Domain model: universes, fixtures, cue lists, capabilities, outputs
├── Persistence/     # Show-file store: IShowStore, DTOs, JSON implementation
├── ViewModel/       # MVVM view models (reactive state)
└── View/            # Avalonia XAML UI
Assets/              # Theme, brushes, and the app icon
```

## Architecture

Luminary follows MVVM:

- **Model** — `ShowService` is the central document: it owns universes, fixtures, and cue lists. Fixtures are typed via `FixtureDefinition` (from a `FixtureLibrary`, keyed manufacturer → model → mode) and expose control abstractions through `FixtureCapability` subclasses (Dimmer, Color, PanTilt, and fine-channel variants). Each `Universe` has a pluggable `UniverseOutput` — `ArtNetOutput` (broadcast) or `ArtNet4Output` (discovery / unicast).
- **ViewModel** — each UI panel has a corresponding ViewModel wired to Model state via observable properties. Loading a show rebuilds the show-bound panels through a single `LoadShow` swap.
- **View** — three-column main window with a menu bar (File / Patch); the **Patch** configuration, the searchable Add-Fixture browser, and the unsaved-changes prompt open as separate windows, and a splash window shows during startup.
- **ArtNet** — `ArtNetService` reconciles per-universe senders against the show each frame and transmits on a ~25 ms timer with sequence numbering. Art-Net 3 universes broadcast; Art-Net 4 universes route through `ArtNet4Controller`, which binds port 6454, discovers subscribers by broadcasting `ArtPoll` and parsing `ArtPollReply` (tracked in a `NodeTable`), unicasts each universe only to its subscribers plus any manual targets, and answers inbound `ArtPoll` so Luminary is itself discoverable.
- **Persistence** — `IShowStore` abstracts the show-file format (`JsonShowStore` today); dedicated DTOs keep the file a stable contract, decoupled from the runtime model — personalities referenced by name, outputs type-discriminated, fixtures by stable id, live channel state excluded. Change detection compares the current serialization against the last saved/loaded snapshot.

### Value pipeline

Each controllable value is a `CapabilityParameter` with two layers — a **manual** value (the fader/pad) and a **playback** value (cues) — that merge into the DMX output by that parameter's `MergeMode` (`Htp` = the larger of the two, `Ltp` = whichever changed most recently). The merged output is what the ArtNet service transmits, and recording a cue captures this output ("record what you see").

Cue playback runs through `CrossfadeEngine`, a UI-thread timer (~40 fps) that interpolates each parameter's playback layer from the current on-stage output toward the cue's recorded value over the cue's fade time. Each parameter also carries a `FadeBehavior` (`Fade` = interpolate, `Snap` = jump immediately) so, for example, intensity can fade while position snaps.

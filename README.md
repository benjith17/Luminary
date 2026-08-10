# Luminary

> **Work in progress.** Luminary is in early development — core concepts are in place but many features are incomplete or not yet started.

A desktop lighting control application for theatrical and live event lighting. Luminary lets you configure DMX fixtures, build cue lists, and transmit live lighting data to hardware via the Art-Net protocol.

## Status

### Implemented
- Fixture model with pluggable capability types: dimmer, RGB color, pan/tilt (standard and fine-channel variants)
- DMX universe model (512 channels) with a pluggable output — Art-Net today, with configurable IP / port / Art-Net universe (or disabled)
- Live Art-Net output over UDP at ~40 fps, reconciled to the current universes each frame
- Fixture list and capability editor UI with custom level faders and an XY pan/tilt pad
- In-app patch: a **Patch** window to add/remove universes and fixtures, choose each universe's output type and settings, and set a fixture's personality / universe / DMX address
- Cue model with major/minor numbering, labels, fade times, and per-fixture snapshots
- Cue list editing: record (snapshots the live output), delete, reorder (renumbering), inline rename, and per-cue fade times — kept in numeric order at all times
- Cue playback: a selected/active split (click selects, GO fires and advances), driven by a time-based crossfade engine
- Per-property fade behaviour: parameters fade or snap during a crossfade, attributed on the capability
- HTP/LTP merging: manual (fader) and playback (cue) layers combine into the DMX output per property — Highest- or Latest-Takes-Precedence — with the live cue value shown as an indicator on each fader and pad
- Show files: JSON save/load (`.lum`) behind a `ShowStore` abstraction — New / Open / Save / Save As, stable fixture IDs, and unsaved-changes prompts on new / open / quit

### Not yet implemented
- Additional output protocols (sACN, USB-DMX, …)
- External fixture personalities (planned via plug-in DLLs)
- Group control
- Effects engine

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
├── ArtNet/          # Art-Net packet formatting and UDP transmission
├── Model/           # Domain model: universes, fixtures, cue lists, capabilities, outputs
├── Persistence/     # Show-file store: IShowStore, DTOs, JSON implementation
├── ViewModel/       # MVVM view models (reactive state)
└── View/            # Avalonia XAML UI
Assets/              # Theme and brush definitions
```

## Architecture

Luminary follows MVVM:

- **Model** — `ShowService` is the central document: it owns universes, fixtures, and cue lists. Fixtures are typed via `FixtureDefinition` (from a `FixtureLibrary`) and expose control abstractions through `FixtureCapability` subclasses (Dimmer, Color, PanTilt, and fine-channel variants). Each `Universe` has a pluggable `UniverseOutput` (`ArtNetOutput` today).
- **ViewModel** — each UI panel has a corresponding ViewModel wired to Model state via observable properties. Loading a show rebuilds the show-bound panels through a single `LoadShow` swap.
- **View** — three-column main window with a menu bar (File / Patch); the **Patch** configuration and unsaved-changes prompt open as separate windows.
- **ArtNet** — `ArtNetService` reconciles a sender per universe against the show each frame (creating, rebuilding on settings change, or disposing them) and transmits Art-Net packets on a ~25 ms timer.
- **Persistence** — `IShowStore` abstracts the show-file format (`JsonShowStore` today); dedicated DTOs keep the file a stable contract, decoupled from the runtime model — personalities referenced by name, outputs type-discriminated, fixtures by stable id, live channel state excluded. Change detection compares the current serialization against the last saved/loaded snapshot.

### Value pipeline

Each controllable value is a `CapabilityParameter` with two layers — a **manual** value (the fader/pad) and a **playback** value (cues) — that merge into the DMX output by that parameter's `MergeMode` (`Htp` = the larger of the two, `Ltp` = whichever changed most recently). The merged output is what the ArtNet service transmits, and recording a cue captures this output ("record what you see").

Cue playback runs through `CrossfadeEngine`, a UI-thread timer (~40 fps) that interpolates each parameter's playback layer from the current on-stage output toward the cue's recorded value over the cue's fade time. Each parameter also carries a `FadeBehavior` (`Fade` = interpolate, `Snap` = jump immediately) so, for example, intensity can fade while position snaps.

# Luminary

> **Work in progress.** Luminary is in early development — core concepts are in place but many features are incomplete or not yet started.

A desktop lighting control application for theatrical and live event lighting. Luminary lets you configure DMX fixtures, build cue lists, and transmit live lighting data to hardware via the Art-Net protocol.

## Status

### Implemented
- Fixture model with pluggable capability types: dimmer, RGB color, pan/tilt (standard and fine-channel variants)
- DMX universe model (512 channels per universe, configurable target IP)
- Live Art-Net output over UDP at ~40 fps
- Fixture list and capability editor UI with custom level faders and an XY pan/tilt pad
- Cue model with major/minor numbering, labels, fade times, and per-fixture snapshots
- Cue list editing: record (snapshots the live output), delete, reorder (renumbering), inline rename, and per-cue fade times — kept in numeric order at all times
- Cue playback: a selected/active split (click selects, GO fires and advances), driven by a time-based crossfade engine
- Per-property fade behaviour: parameters fade or snap during a crossfade, attributed on the capability
- HTP/LTP merging: manual (fader) and playback (cue) layers combine into the DMX output per property — Highest- or Latest-Takes-Precedence — with the live cue value shown as an indicator on each fader and pad

### Not yet implemented
- Show file save/load
- Fixture library / patch workflow
- Group control
- Effects engine
- Universe and fixture configuration from the UI (currently hardcoded test data)

## Tech Stack

- **C# / .NET 10** — application runtime
- **Avalonia 12** — cross-platform XAML UI (Fluent theme)
- **CommunityToolkit.Mvvm** — MVVM reactive bindings
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
├── Model/           # Domain model: universes, fixtures, cue lists, capabilities
├── ViewModel/       # MVVM view models (reactive state)
└── View/            # Avalonia XAML UI
Assets/              # Theme and brush definitions
```

## Architecture

Luminary follows MVVM:

- **Model** — `ShowService` is the central document: it owns universes, fixtures, and cue lists. Fixtures are typed via `FixtureDefinition` and expose control abstractions through `FixtureCapability` subclasses (Dimmer, Color, PanTilt, and fine-channel variants).
- **ViewModel** — each UI panel has a corresponding ViewModel wired to Model state via observable properties.
- **View** — three-column main window: fixture inventory (left), capability editor (center), effects/cue list (right).
- **ArtNet** — `ArtNetService` reads universe channel state from `ShowService` and transmits Art-Net packets on a ~25 ms timer.

### Value pipeline

Each controllable value is a `CapabilityParameter` with two layers — a **manual** value (the fader/pad) and a **playback** value (cues) — that merge into the DMX output by that parameter's `MergeMode` (`Htp` = the larger of the two, `Ltp` = whichever changed most recently). The merged output is what the ArtNet service transmits, and recording a cue captures this output ("record what you see").

Cue playback runs through `CrossfadeEngine`, a UI-thread timer (~40 fps) that interpolates each parameter's playback layer from the current on-stage output toward the cue's recorded value over the cue's fade time. Each parameter also carries a `FadeBehavior` (`Fade` = interpolate, `Snap` = jump immediately) so, for example, intensity can fade while position snaps.

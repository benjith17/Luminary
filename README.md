# Luminary

> **Work in progress.** Luminary is in early development — core concepts are in place but many features are incomplete or not yet started.

A desktop lighting control application for theatrical and live event lighting. Luminary lets you configure DMX fixtures, build cue lists, and transmit live lighting data to hardware via the Art-Net protocol.

## Status

### Implemented
- Fixture model with pluggable capability types: dimmer, RGB color, pan/tilt (standard and fine-channel variants)
- DMX universe model (512 channels per universe, configurable target IP)
- Live Art-Net output over UDP at ~40 fps
- Basic fixture list and capability editor UI
- Cue model with major/minor numbering, labels, fade times, and per-fixture snapshots
- Cue list and effects panels (UI shell present)

### Not yet implemented
- Show file save/load
- Cue playback and crossfade engine
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

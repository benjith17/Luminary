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
- In-app patch: a **Patch** window to add/remove universes and fixtures, choose each universe's output mode and settings, and set a fixture's global number / personality / universe / DMX address — including a searchable Add-Fixture browser (manufacturer → fixture → mode)
- Global fixture numbers: each fixture has a short, unique, patch-independent number (console-style) used to reference it — auto-assigned and editable, kept distinct from the internal stable id
- Cue model with major/minor numbering, labels, fade times, and per-fixture snapshots
- Cue list editing: record (snapshots the live output), delete, reorder (renumbering), inline rename, and per-cue fade times — kept in numeric order at all times
- Cue playback: a selected/active split (click selects, GO fires and advances), driven by a time-based crossfade engine
- Per-property fade behaviour: parameters fade or snap during a crossfade, attributed on the capability
- HTP/LTP merging: manual (fader) and playback (cue) layers combine into the DMX output per property — Highest- or Latest-Takes-Precedence — with the live cue value shown as an indicator on each fader and pad
- Macro scripting: a small, line-oriented DSL for firing cues and setting fixtures, edited and run from a **Macros** window — with `wait`/`fade` timing, fixture ranges, `repeat` loops, live diagnostics, and Run/Stop (see [Macro scripting](#macro-scripting))
- Blackout: a live output kill that transmits zeros while leaving the underlying look intact, so releasing it instantly restores the stage
- Show files: JSON save/load (`.lum`) behind a `ShowStore` abstraction — New / Open / Save / Save As, stable fixture IDs, saved macros, and unsaved-changes prompts on new / open / quit
- App icon and a startup splash screen

### Not yet implemented
- Additional output protocols (sACN, USB-DMX, …)
- Synchronised multi-universe output (Art-Net `ArtSync`)
- External fixture personalities (planned via plug-in DLLs)
- Group control
- Effects engine
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
├── Macros/          # Macro language: lexer, parser, AST, interpreter (host-agnostic)
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
- **View** — three-column main window with a menu bar (File / Patch / Macros); the **Patch** configuration, the **Macros** editor, the searchable Add-Fixture browser, and the unsaved-changes prompt open as separate windows, and a splash window shows during startup.
- **ArtNet** — `ArtNetService` reconciles per-universe senders against the show each frame and transmits on a ~25 ms timer with sequence numbering. Art-Net 3 universes broadcast; Art-Net 4 universes route through `ArtNet4Controller`, which binds port 6454, discovers subscribers by broadcasting `ArtPoll` and parsing `ArtPollReply` (tracked in a `NodeTable`), unicasts each universe only to its subscribers plus any manual targets, and answers inbound `ArtPoll` so Luminary is itself discoverable.
- **Persistence** — `IShowStore` abstracts the show-file format (`JsonShowStore` today); dedicated DTOs keep the file a stable contract, decoupled from the runtime model — personalities referenced by name, outputs type-discriminated, fixtures by stable id, macros as name + source, live channel state excluded. Change detection compares the current serialization against the last saved/loaded snapshot.
- **Macros** — a self-contained scripting pipeline (`Lexer` → `Parser` → AST → `MacroInterpreter`) with no UI or model dependencies. The parser recovers per line and collects diagnostics rather than throwing, so the editor can point at every error at once. The interpreter is an async tree-walk that drives an `IMacroHost`; the ViewModel's `MacroHost` implements it against the live fixtures and cue list (see [Macro scripting](#macro-scripting)).

### Value pipeline

Each controllable value is a `CapabilityParameter` with two layers — a **manual** value (the fader/pad) and a **playback** value (cues) — that merge into the DMX output by that parameter's `MergeMode` (`Htp` = the larger of the two, `Ltp` = whichever changed most recently). The merged output is what the ArtNet service transmits, and recording a cue captures this output ("record what you see").

Cue playback runs through `CrossfadeEngine`, a UI-thread timer (~40 fps) that interpolates each parameter's playback layer from the current on-stage output toward the cue's recorded value over the cue's fade time. Each parameter also carries a `FadeBehavior` (`Fade` = interpolate, `Snap` = jump immediately) so, for example, intensity can fade while position snaps.

### Macro scripting

Macros are short scripts — one command per line — for firing cues and driving fixtures. They're written and run in the **Macros** window and saved with the show. A program is parsed on Run; if it has any error it isn't run, and problems are reported with line numbers.

```
# comments start with #
go                       # fire the selected cue and advance (like the GO button)
goto 2                   # jump to and fire cue 2
2                        # bare number = shorthand for goto
goto 2.3                 # cue 2.3 (major.minor)

wait 2s                  # wait — units s / ms; a bare number is seconds
wait 500ms

L5 @ 80%                 # set fixture 5's dimmer (capability chosen by value count)
L5 @ 50 170              # two values → pan / tilt
L5 @ 255 30 120          # three values → RGB
L5 @ 128                 # raw 0–255 also accepted (percent scales to the parameter)

L3 set Color 60% 100% 80%   # address a capability by name
L3 set Color.2 30% 90% 24%  # the 2nd Color capability, for fixtures with more than one
L3 set Color _ 50% _        # '_' leaves a parameter untouched

L2 @ 80% fade 3s         # fade over 3s (non-blocking — the script continues)

L1..8 @ 100%             # a range of fixtures
L1 + L3 + L5 @ 255 0 0   # a union of fixtures
L1 + L3..6 @ 50%         # ranges and singles mix (spaces optional)

repeat 4                 # loop a block N times
  go
  wait 1s
end
```

Key behaviours:

- **Fixtures are referenced by their global number** (`L5`), so renaming or re-addressing a fixture never breaks a macro.
- **`@` infers the capability** from the number of values given (1 → dimmer, 2 → pan/tilt, 3 → color, for a typical fixture); **`set <name>`** targets a capability explicitly. Capabilities declare their own macro name and parameters, so plug-in capabilities are addressable too.
- **`fade` is non-blocking** — it starts a background fade of the manual layer and the script moves on; **`wait` is the only thing that blocks**, so it's how you sequence. Fixture commands drive the manual layer while `go`/`goto` drive the playback layer, and the two merge through the normal HTP/LTP rules.
- **Numbers are integers** — every `.` is a separator (`2.3`, `Color.2`) and `..` is a range, so fractional times are written in milliseconds (`500ms`).
- **Run/Stop** — one macro runs at a time; Stop cancels it and freezes any in-progress fades where they are.

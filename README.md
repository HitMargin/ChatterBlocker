# ChatterBlocker

A UnityModManager mod for *A Dance of Fire and Ice* (ADOFAI) that blocks keyboard chatter by debouncing key-down events with a configurable interval.

## Features

- **Configurable debounce interval** — adjust the minimum time between key events (0–9999ms)
- **Async & sync input support** — works with both SkyHook (async) and Unity Input (sync) paths
- **In-game settings UI** — integrated into the pause menu's Advanced tab
- **Multi-language** — supports all 14 game languages
- **Toggle on/off** — via UMM mod list toggle

## Installation

1. Place `ChatterBlocker.dll` and `Info.json` into `Mods/ChatterBlocker/`
2. Enable the mod in UnityModManager
3. Adjust the interval in Pause Menu → Settings → Advanced → Chatter Blocker

## Build

```bash
dotnet build ChatterBlocker.slnx
```

Requires .NET Framework 4.8.1 targeting pack and the game assemblies in `../adofai-libs/`.

## License

MIT

# Lumen Player — Project Roadmap

## Current state
- Projects: App (WPF), Media (classlib), Playlist (classlib), Tests (xunit).
- Target frameworks: net10.0-windows / net10.0.
- Git: initialized locally and pushed to https://github.com/itscpawork11/LumenPlayer (branch main).
- CI: GitHub Actions workflow runs restore/build/test/publish; latest run successful.
- UI: static prototype + VideoView wired; no verified visual inspection.
- Media: MediaEngine scaffolded; not functionally tested.

## Phases and gates
1. Phase 1 (Visual foundation) — UI prototype + theme resources. Gate: build/test pass (CI green).
2. Phase 2 (Media engine) — LibVLC integration, playback controls. Gate: build + smoke playback (pending).
3. Phase 3 (Playlist & files) — open, drag&drop, navigation (pending).
4. Phase 4 (Audio/subtitles) (pending).
5. Phase 5 (Tests & quality) (pending).
6. Phase 6 (Publish) — self-contained artifact produced and downloaded.
7. Phase 7 (Final docs) (pending).

## Publish / visual validation
- Latest successful GitHub Actions run: #3, conclusion: success.
- Published self-contained Windows x64 artifact: `LumenPlayer-win-x64` (~203 MB).
- Artifact downloaded and extracted; contains `LumenPlayer.exe`, LibVLCSharp, and `libvlc` native files.
- Attempted to launch `LumenPlayer.exe` on this Windows machine: process started and remained responsive, but no main window handle/title was observed in this session. Visual inspection of layout is therefore **not verified**.
- No media playback test performed.

## Open blockers
- Visual inspection requires an interactive Windows desktop session or user-run launch.
- Functional media tests pending.

## Decisions
- Use LibVLCSharp 3.10.2 + VideoLAN.LibVLC.Windows 3.0.24 (LGPL native).
- Keep independent Lumen Player identity.
- Self-contained publish for Windows x64.

## Next
- User to launch artifact on an interactive Windows desktop and report visual/functional issues.
- Then implement and test real playback controls.

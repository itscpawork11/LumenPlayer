# Lumen Player — Project Roadmap

## Current state (Phase 0 inventory)
- Projects: App (WPF), Media (classlib), Playlist (classlib), Tests (xunit).
- Target frameworks: net10.0-windows / net10.0.
- .NET SDK: not installed locally (blocker for build/test/visual inspection).
- Git: not a repository locally; no GitHub remote configured.
- CI workflow: `.github/workflows/ci.yml` created locally, not pushed/executed.
- UI: static prototype only; no media functionality.

## Phases and gates
1. Phase 1 (Visual foundation) — UI prototype + theme resources. Gate: build/test pass (blocked by SDK).
2. Phase 2 (Media engine) — LibVLC integration, playback controls. Gate: build + smoke playback.
3. Phase 3 (Playlist & files) — open, drag&drop, navigation.
4. Phase 4 (Audio/subtitles).
5. Phase 5 (Tests & quality).
6. Phase 6 (Publish).
7. Phase 7 (Final docs).

## Open blockers
- .NET 10 SDK installation authorization.
- GitHub repository connection/push authorization.
- Visual inspection requires running WPF app.

## Decisions
- Use LibVLCSharp 3.10.2 + VideoLAN.LibVLC.Windows 3.0.24 (LGPL native).
- Keep independent Lumen Player identity.
- No media functionality until SDK available; code prepared but unverified.

## GitHub / repository status
- Is this a Git repository? **No** (no `.git` directory found).
- Remote configured? **No**.
- Uncommitted changes? Not applicable (no Git repository).
- `.gitignore`: **Added** (`.bin/obj/.vs/TestResults` etc.).

## CI readiness
- `.github/workflows/ci.yml`: present, uses `windows-latest`, installs .NET 10 via `actions/setup-dotnet`, runs restore/build/test in correct order.
- Status: **Ready locally but not pushed or executed**.

## Obvious pre-build issues (not a substitute for real build)
- `MediaEngine` uses LibVLCSharp APIs (`MediaPlayer.Media`, `LibVLC()`, events) that must be validated by actual compilation.
- `MainWindow.xaml` references `LibVLCSharp.WPF.VideoView`; namespace/package must resolve at build time.
- Test project currently only contains a trivial smoke test; no media/UI tests yet.
- No `.gitignore` existed before; now added.

## Answers
- Linked to GitHub repository? **No**.
- Workflow ready to run? **Locally yes; cannot run until pushed to GitHub.**
- Clear errors to fix before push? **No confirmed compile errors (no SDK), but API usage is unverified.**
- Single action required from user: **Push/connect this project to a GitHub repository (authorization required), then the workflow can run; separately, .NET 10 SDK is required for local build/test.**
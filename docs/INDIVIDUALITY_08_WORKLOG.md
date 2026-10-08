# V0.8 / V0.8.1 Individuality Update — Worklog

**Current status:** V0.8 implementation remains preserved and V0.8.1 Verification & Dual Presence changes are implemented on the dirty `codex/individuality-08` worktree. Quick is complete (build, 316 tests, four-personality／six-week logical simulations and model-level Dual Presence); isolated WPF smoke passed; Standard short stress is running in an independent process. Do not load raw telemetry by default or wait in Codex. The installed V0.8 application and real profile were not changed by this development pass; see the installation record below.

## M0: repository and stability gate

Baseline: `codex/home-07`, commit `8b45bfec23905480b72a754933775b37bb0a2672`. Clean working tree before creating `codex/individuality-08`. No merge, force push, branch deletion, or history rewrite. Scope is development and validation only.

Baseline Release: 0 warnings/errors; all 270 tests pass. Only diagnostic changes precede the stability gate: allow 2–8 hour stress duration, sample GC collections/managed heap/process handles/GDI/USER/native and WPF window counts, periodically show/hide isolated UI, persist 5-minute checkpoints, and identify the exact test process instead of selecting another run's latest report.

Pre-gate started 2026-09-27, requested 7200 seconds, seed 70, isolated profile, 24 room items (21 furniture + 3 extra toys), 5 toys including defaults. The existing room capacity is retained. Gameplay code remains V0.7. Result pending; personality implementation must wait for the gate assessment.

## Architecture audit (plans, not completed features)

- ActionSelection currently has no character boundary. Eligibility must be checked before learned scores and again at PetWindow's action/creative dispatch. Appearance switching must cancel incompatible actions. Saved historical works must remain visible.
- Cat speech also originates from Companion startup, care, quiet mode, rename, greeting, periodic thoughts and reward feedback. Blocking only WriteNote is insufficient. Separate character speech from system descriptions, with a final guard in PetWindow.Say.
- Girl currently traverses some feline sequence phases but usually displays Sit. Centralize expression mapping, prevent cat-only sound and scratch/knead/lick expressions, and preserve simple safe human fallback.
- Girl drawing can reuse GeneratedDrawing's validated bounded vector strokes. Add an optional template identifier to CreativeWork; render old stored Drawing or legacy Pattern unchanged. Existing procedural drawing remains available only for legacy diagnostics/tests, not new production creations.
- Base Personality is already separately stored in OrganismSnapshot. Keep it immutable; add optional bounded adaptation evidence/offsets to LearningState, cache effective personality outside the frame loop.
- Sequence completion needs to distinguish successful completion from safety/target-loss/care interruption before learning transitions. Event milestones must depend on actual contact, use, sleep or accepted care, not selecting an intent.
- Companion memories currently record every accepted care and sometimes claim sleep completion too early. Replace future summaries with factual low-frequency events; preserve old records.
- Existing LocationHabit GUIDs, 32-entry bound and soft selection remain. Social/Bond proximity and Independence must remain separate influences.
- New optional fields alone do not require schema v4. Reassess whether older binaries may silently lose identity data; document any protective version bump with full v3 preservation tests.
- All simulation clocks and random seeds must be injectable; distinguish logical-time decision models from WPF navigation and real two-hour soak evidence.

## Planned validation

M1 capability matrix and extreme legacy creative preference; M2 fixed-seed 16-template sheets at 100/150/200%, bounded geometry and legacy rendering; M3 minimum-evidence/rate/offset/restart/corruption tests; M4 bounded successful transitions with decay and loop resistance; M5 soft behavior composition and need priority; M6 authentic deduplicated milestones; M7 four profiles in identical multi-hour context; M8 multi-week contrasting histories and neutral control; M9 WPF expression/UI diagnostics; M10 a second full two-hour native soak; M11 A–W report with raw evidence and limitations.

## Active continuation checkpoint

Pre-gate PID 30924; UTC start 2026-09-27T09:53:23; isolated root `%USERPROFILE%/AppData/Local/Temp/DesktopLifeSmoke/c0f28a7580964adb8e4ba6c91b2cff07`; wrapper session 50487. Read process identity before using a PID because it may be reused. Expected final evidence `artifacts/verification/0.8/pre-gate`. Native application reports and 5-minute JSONL checkpoints remain in the isolated root even if the wrapper is interrupted. Do not count an incomplete run as a passed gate.

Thread heartbeat `desktop-life-v0-8` created successfully to check and continue this user-requested staged task every 15 minutes, bounded to 32 runs. Remain quiet while unchanged; after gate completion proceed with implementation. Stop the continuation when the requested development and validation are complete. No V0.8 feature changes have been made yet. No commit, push, installation or real-profile modification in this phase.

Analysis helper `scripts/analyze-soak.py` validated against the existing V0.7 report; it preserves missing metrics as unavailable and emits no leak-free verdict. Compare 10-minute medians and post-20-minute slopes, not just first and last samples.

## M0 gate assessment
Completed 7311.38 seconds, 7200 samples elapsed, normal shutdown, 0 exceptions, 0 phase stalls, 157 navigation failures/recoveries. Working set 247.08→304.09 MiB, peak 331.42; private 139.38→168.25 MiB, peak 198.39. Private ten-minute medians peaked around 190.27 then fell to 169–171; no sustained linear rise. Handles 1575–1646, GDI 365–373, native windows 80→83 then flat; WPF windows 29, RoomWindow 21, ToyWindow 5 throughout. GC 963/222/22; allocations 11267.93 MiB (92.47 MiB/min); machine CPU 0.883%. Proceed to M1: observed resources stabilize/recede; this does not prove absence of leaks. Raw report and analysis in artifacts/verification/0.8/pre-gate.

## M1
281 tests PASS (+11 capability cases); Release 0 warnings/errors. Selector eligibility precedes learned preference; runtime actions and creative dispatch also guard identity, appearance switch clears incompatible actions and speech. Cat Say is suppressed, care descriptions stay system-authored. Girl scratch/box sequences fall back safely; feline paw overlay and grooming pose are Cat-only.

## M2
285 tests PASS (+4 bounded/deterministic template, repeat suppression, small jitter and legacy roundtrip tests); Release and full WPF smoke PASS. 16 original vector templates rendered at 100/150/200%; 100/150 sheets visually inspected. Legacy GeneratedDrawing and Pattern renderer retained without regeneration. New Girl notes use short local context templates and runtime creative cooldowns (drawing 45s, notes 90s).

## M3
294 tests PASS (+9 adaptation/migration cases); Release clean. Four optional evidence tracks, one evidence per trait/minute, threshold 8, 7-day evidence decay, maximum active drift 0.006/day and offset ±0.12. Offline gaps do not add drift. Schema v4 protects identity metadata against older writers; v2/v3 payload migration tested. Initial new-test failures were invalid fixtures missing required save metadata; fixtures corrected without weakening validation.

## M4
298 tests PASS (+4 completion/interrupt, recent-loop, bounded-pair and invalid-pair tests). Runtime completion distinguishes normal completion from interruption; Play requires real contact. 32 bounded pairs, 14-day weight decay, one update per pair/minute, 10-minute adjacency limit, 3-entry recent-use suppression. Selection integration follows in M5.

## M5
302 tests PASS (+4 integration cases), Release clean before the user requested skipping subsequent tests. Effective personality is cached on event/minute updates; existing ActionSelection and HomeRoutine use personality/transition biases. Wake branch weighting and location distance preferences remain soft; Bond remains separately stored. Critical fatigue overrides learned transitions.

## Revised user scope (2026-09-27)
The user explicitly instructed: 「跳過測試，繼續」. From this point, do not run automated tests, WPF smoke, identity/longitudinal simulation acceptance or the second two-hour soak. Continue implementation and compile only. Earlier PASS results apply only to their earlier code state. The heartbeat automation could not be paused because the app reports it no longer exists; no replacement automation is created.

## M6 implementation
Real furniture contact/use and successful sequence completion now feed deduplicated milestone records, with a six-hour cooldown, six milestone kinds and a shared maximum of thirty memories. Up to six milestone records are retained within that same budget. Routine accepted-care summaries in the product are limited to one per thirty minutes and describe the action actually requested/performed, not an invented completed sleep. Historical records remain. New furniture use supplies Curiosity evidence. These additions have not been tested under the revised scope.

## M7–M10 revised scope
Identity and longitudinal simulation acceptance and final two-hour soak skipped by explicit user instruction. No statistics fabricated. M9 UI adds system identity description and advanced base/effective/offset, transition/furniture and milestone diagnostics. Character switches clear outstanding expression/transition context. Final solution Release compilation succeeded with zero warnings/errors; no tests run after the scope change. Standalone packaging and A–W documentation follow; no install or GitHub upload.

## M11 delivery
A–W report: docs/UPDATE_08.md; capability and art-pipeline documentation updated. Final standalone 0.8.0-20260927-233148, SHA256 BAD8773F9C20BA8DDF9BE6F7C9AA0E5366C3070DB9AEF2812249C7FBA44FD8B6. Solution Release compilation: zero warnings/errors. No executable smoke or test run after the user requested skipping tests. No installation, launch against real data, GitHub upload or merge.

## User-requested installation — 2026-09-27 23:39 Asia/Taipei

User explicitly requested installation after packaging. Gracefully stopped V0.7 using its shutdown IPC; no forced termination. Backed up actual user JSON files through an external Windows process to `%USERPROFILE%\AppData\Local\DesktopLife\upgrade-backups\before-0.8-20260927-233859` before migration. Installed build `0.8.0-20260927-233148` using `scripts/install.ps1 -Launch`, updated Desktop/Start menu shortcuts, and launched independently under WMI (PID 25936).

Basic installation verification: process remains running from the installed V0.8 path; startup log present; profile schema migrated 3 to 4; all 10 original room IDs retained; desktop icon backup SHA256 unchanged (`B3AC64AF447C0BD6A998176255AD8DA66470447E85DC44CCB766538A6C62C0F6`). Evidence: `artifacts/verification/0.8/profile-backup.json` and `installed-profile.json`. This is installation/startup verification, not the skipped test suites or final soak. No Git upload or merge performed.

## V0.8.1 implementation — 2026-09-28

Added `scripts/verify.ps1` with Quick／Standard／Full modes, independent worker launch, process lock, fixed `artifacts/verification/latest/` summaries, raw-run isolation and failure packages. Quick now reports Release build PASS, 316／316 tests PASS, four-personality／six-week logical simulations PASS and Dual Presence model tests PASS. A direct isolated WPF smoke including Cat sleep／Girl doodle, capability boundaries, save/reload, mode switching and hide/show passed. Standard worker run `20260928-003001-5f9e56a5` is currently RUNNING at the short stress stage; no Codex polling is required.

Added `PresenceMode`, v5 migration, `CharacterProfile`, `CharacterRuntime`, runtime-only household occupancy, light other-character awareness, active care target UI and Dual Presence diagnostics. The existing active character keeps its complete v4 history; the newly introduced character receives defaults. Room, furniture, toys and display surfaces are shared while needs, personality, bond, behavior sequence and habits remain per-character. The first V0.8.1 standalone packaging should use `scripts/publish.ps1` and produce a `0.8.1-YYYYMMDD-HHMMSS` build; it has not been installed over the real V0.8 profile in this phase.

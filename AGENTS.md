# AGENTS.md

Instructions for coding agents (Codex, Claude, Gemini, ...) working in this repo.

**The full guidance lives in [`CLAUDE.md`](CLAUDE.md), read it first.** It is written for any
agent, not just Claude, and it is the single source of truth for this repository: the critical
rules, the project layout, the build/run/test commands, the editor automation, the assembly
boundaries, the architecture, the conventions and the known gotchas.

This file exists only so that tools which look for `AGENTS.md` find the entry point. It
deliberately duplicates nothing, so the two cannot drift.

Orientation, in the order it is usually needed:

- [`CLAUDE.md`](CLAUDE.md): everything above.
- `.codex/PROJECT_CONTEXT.md`: completed work, verification status, known limitations and
  continuation priorities.
- [`Assets/ShipSimulator/Documentation/`](Assets/ShipSimulator/Documentation/): operator guide,
  physics model, vessel parameter sources and confidence, scenario plan and roadmap.
- `CLAUDE.local.md`: machine-specific notes, where the toolchain lives on this particular box and
  the concrete commands to run it. Not committed; if it is missing, you are on a different
  machine and should write your own.

---
title: 'dev-up / dev-down Local Stack Scripts'
type: 'feature'
created: '2026-07-23'
status: 'done'
context: []
baseline_commit: '560555ac36db4519ffef2e3eb4ffe548744d92ac'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Epic 9 retro Action Item #3 (owner: Charlie, opportunistic): local dev stack standup (Azurite + `func start` + `swa start`) has recurring friction — Azurite not running, a stale orphaned SWA CLI process on port 4280 from a previous session, and no single documented command to bring the whole stack up. `npx swa` alone is also a trap: it resolves to an unrelated npm package, not Azure's SWA CLI, since nothing pins the real one anywhere in this repo.

**Approach:** Two new scripts, `scripts/dev-up.sh` and `scripts/dev-down.sh`, matching `docker-compose up`/`down` semantics: `dev-up.sh` starts Azurite, `func start`, the Vite dev server, and the SWA CLI emulator as detached background processes (each logging to its own file), then exits, printing the URL to open. `dev-down.sh` reads the same PID-tracking file and stops everything cleanly.

## Boundaries & Constraints

**Always:**
- Match `infra/deploy.sh`'s existing style: `#!/usr/bin/env bash`, `set -euo pipefail`, a `SCRIPT_DIR` resolution line, clear `echo`-based progress/error messages with remediation steps — not silent failures.
- `dev-up.sh` must be idempotent: if `scripts/.dev-pids` already lists a PID that's still alive (`kill -0 $pid`), skip re-starting that specific service and report it's already running.
- Use the verified-correct SWA CLI invocation: `npx -y @azure/static-web-apps-cli@latest start http://localhost:5173 --api-devserver-url http://localhost:7071 --swa-config-location client/public` — never bare `npx swa` (resolves to an unrelated package).
- Before starting the SWA CLI, check if port 4280 is already bound. If so, inspect the owning process (`lsof -i :4280`, then `ps -p <pid> -o command=`); only offer to kill it if the command line looks like a stale `swa`/`static-web-apps-cli` process. Prompt for interactive confirmation before killing — default to **not** killing (and exiting with a clear manual-cleanup message) if unconfirmed or non-interactive.
- All new runtime artifacts (`scripts/.dev-pids`, `scripts/.dev-logs/`, Azurite's data directory) must be added to `.gitignore`.
- `func start`'s readiness check: if it doesn't bind port 7071 within a timeout, surface the last ~10 lines of its log file directly in the terminal (a SQL firewall block or missing `local.settings.json` are the two known real-world causes) — do not attempt to work around either.
- Azurite data directory: use a dedicated location under `scripts/` (not `api/`), so `api/`'s own working tree stays clean.

**Ask First:**
- None expected — the interactive-confirmation-before-kill behavior above is itself the safety gate for the one action with real blast radius (killing another process).

**Never:**
- Do not attempt to fix the SQL Server firewall block (an Azure-side network rule) or the real-Blob-Storage-auth "connection refused" issue (separate, already-logged `deferred-work.md` item) — both are explicitly out of scope; surface clear errors, don't build workarounds.
- Do not auto-kill a process on port 4280 without confirming first that it looks like a stale SWA CLI process — never blindly kill "whatever is on that port."
- Do not add a root-level `package.json` just to wrap these scripts with `npm run dev-up` — this project explicitly has no root `package.json` (project-context.md: "all npm commands run from within `client/`"); these are plain shell scripts, invoked directly.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fresh stack start | Nothing running, `scripts/.dev-pids` absent | Azurite, `func start`, Vite, SWA CLI all start; PIDs recorded; final message prints `http://localhost:4280` | N/A |
| Re-run `dev-up.sh` with stack already up | `scripts/.dev-pids` has live PIDs for all 4 | Each already-running service is detected and skipped, reported as "already running" | N/A |
| Stale SWA CLI process on port 4280 | Orphaned process from a prior session still bound to 4280 | User is shown the process info and prompted to confirm killing it before the new SWA CLI starts | If declined or non-interactive, exit with a manual-cleanup message — do not start SWA CLI on a conflicting port |
| `api/local.settings.json` missing | Fresh clone, no local settings file | `func start` fails fast | Script surfaces a clear message pointing to `api/local.settings.json.example` and `infra/deploy.sh`'s post-deploy instructions |
| SQL Server firewall blocks `func start` | Valid `local.settings.json`, but SQL connection unreachable | `func start` doesn't bind port 7071 within the readiness timeout | Script surfaces the last ~10 lines of `func`'s log and states this needs Ralf's manual Azure-side intervention — no retry/workaround attempted |
| `dev-down.sh` run | `scripts/.dev-pids` has some live, some already-dead PIDs | Live PIDs are killed; dead ones are skipped without error; `scripts/.dev-pids` is cleared/removed after | N/A |
| `dev-down.sh` run with no stack up | `scripts/.dev-pids` absent or empty | Script reports nothing to stop and exits 0 | N/A |

</frozen-after-approval>

## Code Map

- `scripts/dev-up.sh` -- new script: Azurite + `func start` + Vite + SWA CLI orchestration, PID tracking, readiness polling, stale-port-4280 detection
- `scripts/dev-down.sh` -- new script: reads `scripts/.dev-pids`, stops each tracked process
- `.gitignore` -- add `scripts/.dev-pids`, `scripts/.dev-logs/`, and the new Azurite data directory

## Tasks & Acceptance

**Execution:**
- [x] `.gitignore` -- add entries for `scripts/.dev-pids`, `scripts/.dev-logs/`, `scripts/.azurite-data/`
- [x] `scripts/dev-up.sh` -- implement: `SCRIPT_DIR`/`ROOT_DIR` resolution; a small helper function to check-if-running-and-skip-or-start-and-record-PID per service; Azurite started via `azurite --silent --location scripts/.azurite-data --debug scripts/.azurite-data/debug.log` (background, logged to `scripts/.dev-logs/azurite.log`); readiness check via port 10000 (blob) being bound; `func start` from `api/` (background, logged), readiness via port 7071, with the missing-`local.settings.json` and SQL-firewall error paths from the I/O matrix; Vite dev server via `npm run dev` from `client/` (background, logged), readiness via port 5173; port-4280 stale-process check + confirm-before-kill, then SWA CLI start (background, logged) with the exact verified invocation; final summary printing the URL and log locations
- [x] `scripts/dev-down.sh` -- implement: read `scripts/.dev-pids`, for each entry check `kill -0`, kill live ones (`kill`, then `kill -9` after a short grace period if still alive), report what was stopped vs. already dead, remove the PID file when done. Correction found via live end-to-end testing: `npx -y @azure/static-web-apps-cli@latest start ...` spawns a grandchild process (`msha/server.js`) that survives killing only the tracked top-level PID, leaving port 4280 held. Added a `kill_tree` helper (via `pgrep -P`) that recursively kills the whole descendant tree, not just the tracked PID, for both the TERM and KILL escalation steps.

**Acceptance Criteria:**
- Given a fresh clone with a valid `api/local.settings.json`, when `./scripts/dev-up.sh` is run, then Azurite, the Functions host, Vite, and the SWA CLI emulator are all running and the script prints `http://localhost:4280` to open.
- Given the stack is already fully up, when `./scripts/dev-up.sh` is run again, then no duplicate processes are spawned and each already-running service is reported as such.
- Given a stale process is bound to port 4280, when `./scripts/dev-up.sh` runs and the user declines the kill prompt, then the script exits with a clear manual-cleanup message and does not attempt to start a conflicting SWA CLI instance.
- Given the stack is up, when `./scripts/dev-down.sh` is run, then all tracked live processes are stopped and `scripts/.dev-pids` no longer references them.

## Spec Change Log

- **2026-07-23, review round 1 (patches, no loopback):** All findings were implementation-level, non-frozen fixes — no intent_gap or bad_spec. Applied: Azurite readiness now polls all 3 ports (blob/queue/table), not just blob; SWA CLI pinned to `@^2.0.0` instead of `@latest` (tested version: 2.0.10); added a `command -v func` precondition check (previously a missing `func` CLI silently burned the full 45s timeout before showing a misleading SQL-firewall hint); added a `client/public/staticwebapp.config.json` existence precheck before starting SWA CLI; added an `EXIT` trap reminding the user to run `dev-down.sh` on any non-zero exit (previously a mid-sequence failure left earlier-started services orphaned with no pointer to clean up); `tail` calls on log files now go through a `safe_tail` wrapper that doesn't itself fail if the log is missing/empty; the stale-port-4280 kill now uses the same recursive `kill_tree` as `dev-down.sh` (not a single-PID `kill`) and re-verifies the port is actually free before proceeding, since live testing had already proven a single-PID kill can leave a surviving grandchild bound to the port.
  A process-identity check (matching the tracked PID's own command line against a keyword, to guard against PID-reuse false positives) was also attempted for `is_running()`, but **reverted** after live testing proved it broke idempotency: `npm run dev`'s own process never contains "vite" in its argv (only a child process does — `npm`/`npx` frequently exec into a *child*, not always replacing their own argv), so the check produced a false negative and spawned a duplicate Vite dev server on port 5174. Kept the original plain `kill -0` check instead — simpler, and the PID-reuse scenario it aimed to guard against is a low-probability risk for single-developer local dev tooling, not worth the demonstrated fragility.

## Design Notes

**Why background-and-exit instead of one long-running foreground script:** matches `docker-compose up -d`/`down` semantics — two independently-invokable commands rather than a single foreground process the developer must keep a terminal pinned to and `Ctrl+C` (which would also need signal-trap cleanup logic for 4 child processes). Simpler, and directly matches the retro's own "dev-up/dev-down" framing.

**Why `--api-devserver-url` instead of `--api-location`:** the project already runs `func start` as its own explicit step (confirmed via multiple story Dev Notes referencing `func start` + `swa start` as separate commands) — SWA CLI's `--api-devserver-url` mode proxies to an already-running API rather than spawning its own, matching established practice.

## Verification

**Commands:**
- `bash -n scripts/dev-up.sh` -- expected: no syntax errors
- `bash -n scripts/dev-down.sh` -- expected: no syntax errors
- `./scripts/dev-up.sh` then `./scripts/dev-down.sh` -- expected: full stack starts, is reachable, then cleanly stops with no orphaned processes (`ps aux | grep -E 'azurite|func|vite|swa'` shows nothing lingering)

**Actually run, live, during this pass (not just described):** Full up → idempotent re-run (verified no duplicate processes, including catching and fixing a real regression where a naive fix broke Vite idempotency) → teardown with zero orphaned processes/ports → a simulated stale-port-4280 scenario (confirmed it correctly identifies the process and safely refuses to kill non-interactively) → `command -v`/missing-file precondition checks verified in isolation.

## Suggested Review Order

**Process lifecycle (the core mechanism)**

- Entry point: idempotency check — plain `kill -0`, deliberately NOT keyword-matched (see Spec Change Log for why the keyword-matched version was reverted after live testing broke it)
  [`dev-up.sh:29`](../../scripts/dev-up.sh#L29)

- Background-and-track: `exec` inside the subshell so the tracked PID is the real process, not a subshell wrapper
  [`dev-up.sh:47`](../../scripts/dev-up.sh#L47)

- `kill_tree` — recursive kill via `pgrep -P`, added after live testing found `npx ... swa start` spawns a grandchild that survives a single-PID kill
  [`dev-down.sh:19`](../../scripts/dev-down.sh#L19)

- Same `kill_tree` reused for the stale-port-4280 case in `dev-up.sh`, now with a re-verify-port-freed check
  [`dev-up.sh:69`](../../scripts/dev-up.sh#L69)

**Safety gate (the one action with real blast radius)**

- Stale-port-4280 detection + identify-before-confirm-before-kill, defaults to NOT killing when unconfirmed or non-interactive
  [`dev-up.sh:151`](../../scripts/dev-up.sh#L151)

**Service-specific wiring**

- The verified-correct SWA CLI invocation (pinned `@^2.0.0`, not bare `npx swa` which resolves to an unrelated package)
  [`dev-up.sh:180`](../../scripts/dev-up.sh#L180)

- `func start` readiness failure path — the SQL-firewall-hint message, scoped to the one specific known cause
  [`dev-up.sh:117`](../../scripts/dev-up.sh#L117)

**Peripherals**

- Partial-failure reminder trap
  [`dev-up.sh:16`](../../scripts/dev-up.sh#L16)

- New gitignore entries
  [`.gitignore:112`](../../.gitignore#L112)

**Manual checks:**
- Re-run `./scripts/dev-up.sh` while already up — confirm idempotent "already running" messages, no duplicate processes.
- Open `http://localhost:4280` in a browser after `dev-up.sh` completes — confirm the app loads (auth-emulated).

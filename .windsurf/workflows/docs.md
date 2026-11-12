---
description: Rescan project and update documentation index
---

This workflow helps you rescan the project and keep the progressive documentation in `/docs` aligned with the codebase.

## 1) Quick project scan (what changed?)

1. Review core entry points and confirm they still match the docs:
   - `Program.cs` (DI registrations, auth, endpoints)
   - `WalkieTalkie.csproj` (target framework, packages)
   - `Components/Routes.razor` (public vs protected routes)
   - `Services/*` (major behavior changes)
   - `Data/*` (schema / seeding changes)

2. Identify the feature area you touched (pick one):
   - Authentication / PIN management
   - Groups / membership
   - Lobby chat
   - Voice / speech (push-to-talk, TTS)
   - Logging / telemetry
   - Azure deployment

## 2) Docs folder rescan

1. Open `/docs/index.md` and confirm it includes links for any new docs you added.
2. If you introduced a new feature or non-trivial fix, add a new markdown file under `/docs/` and link it from `/docs/index.md`.
3. If you changed behavior that affects users, update the latest release notes file:
   - Prefer updating the newest `docs/v*.md` release notes, or add a new release notes file if you’ve bumped the app version.

## 3) Consistency checks

1. Version consistency:
   - Confirm the version shown in the app (Home page reads from `auth.json`) matches the latest release notes headline.
2. Group-aware behavior consistency (if applicable):
   - If you changed group membership or chat visibility rules, ensure `groups-feature-implementation.md` and related chat docs still reflect the rules.
3. Azure notes consistency (if applicable):
   - If you changed authentication or DB paths, update the Azure troubleshooting docs.
4. Logging consistency (if applicable):
   - If you added/changed log event types, update `structured-logging-system.md`.

## 4) Optional: add a short “what changed” entry

Add a brief section near the top of the relevant doc describing:
- What changed
- Why it changed
- How to test it

## 5) Final step

Ensure:
- `/docs/index.md` links to any new or renamed docs
- Any new screenshots under `/docs/` are referenced from at least one markdown file
- Release notes (if updated) mention user-visible changes

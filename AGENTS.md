# Repository Guidelines

## Project Structure & Module Organization

Work from `cyber-resistance/`. `Scripts/` holds feature-grouped C# code; `Scenes/` holds matching `.tscn` files. Content is under `Data/`, visuals under `Assets/`, resources under `Resources/`, and teaching material under `Materials/`. Docker labs live in `Dockerfiles/` and `Scenarios/`. Treat `.godot/` as generated; let Godot maintain `.uid` and `.import` files.

## Build, Test, and Development Commands

```bash
cd cyber-resistance
dotnet build Cyber-Resistance-Nov25.sln
godot --path . --editor
./install-docker-machines.sh
godot --headless --path . --export-release CyberResistance Export/CyberResistance
```

These commands compile the solution, open the Godot 4.4.1 Mono editor, prepare local lab containers, and export the configured release. The install script replaces `player_machine` and `scenario1`.

## Coding Style & Naming Conventions

Use tabs and put braces on separate lines. Use `PascalCase` for classes/public methods, `camelCase` for locals/parameters, and `_camelCase` for private fields. Keep identifiers in English and ASCII; UI text may remain Portuguese. Match C# filenames to classes (`QuizManager.cs`), use `PascalCase` for Godot nodes, and lower camel case for new scene/data files. Preserve casing in `res://...` paths and validate JSON.

## Testing Guidelines

There is no automated test suite or coverage gate. Changes must compile cleanly and be play-tested in the affected scene. Watch Godot output for errors. For scenario changes, rebuild the image and verify its documented SSH or Telnet flow.

## Git Ownership (Non-Negotiable)

Only the repository owner may change version-control state. Agents must never stage or commit; create, switch, or delete branches; merge, rebase, cherry-pick, reset, restore, stash, or clean; create/delete tags; fetch, pull, or push; edit Git configuration/remotes; or manipulate pull requests. Agents may only inspect and compare with read-only commands such as `git status`, `git diff`, `git log`, `git show`, and `git branch --list`. They may suggest commit messages, branch names, and PR text. Follow the concise Portuguese style: `feat: implementa rotina do tutor Hubner`.

## Scope & Decision Boundaries (Non-Negotiable)

Perform only the work expressly requested and change only what it requires. Do not include unrelated cleanup, refactors, fixes, dependency updates, or formatting. Report other opportunities as suggestions without modifying them. Ask before important decisions affecting behavior, architecture, data, compatibility, security, or scope. Resolve trivial implementation details with sound judgment and established patterns.

## Security & Configuration

Lab credentials and vulnerable sudo rules are educational fixtures only. Bind container ports to `127.0.0.1`; never place real credentials, tokens, personal saves, or generated exports in tracked files.

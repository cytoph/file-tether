# file-tether

A package-manager-style CLI that keeps chosen files and folders up to date with their remote sources.

Some files just need to be the same across machines - shared `CLAUDE.md` fragments, agent skill folders, editor settings, tooling config. file-tether pulls them from a GitHub repository or Gist to their local destinations, tells you when they are outdated, and never overwrites anything without asking. Checking is automatable; applying stays an explicit, interactive act.

## Install

Download the binary for your platform from [Releases](https://github.com/cytoph/file-tether/releases), put it somewhere on your `PATH`, done - the binaries are self-contained, no .NET runtime required.

## Quick start

```
file-tether init
```

This scaffolds the config directory (`%APPDATA%\file-tether\` on Windows, `~/.config/file-tether/` on Linux, `~/Library/Application Support/file-tether/` on macOS) with a commented example `tether.yml`. Pass `--env` to also scaffold a `.env` stub (never overwrites an existing one). Edit `tether.yml`:

```yaml
tethers:
  - type: github
    token: ${GITHUB_TOKEN}          # optional for public repos
    repo: you/your-config-repo
    files:
      - path: claude/shared-rules.md
        destination: ~/.claude/shared-rules.md
        # name: shared-rules         # optional; defaults to "you/your-config-repo:claude/shared-rules.md"
      - path: claude/skills/my-skill   # a folder - pulled as one atomic unit
        destination: ~/.claude/skills/my-skill

  - type: gist
    token: ${GITHUB_TOKEN}
    id: abc123def456
    destination: ~/.claude/gist-file.md
    # name: gist-file               # optional; defaults to "gist:abc123def456/<filename or source path>"
```

Every entry gets a display name - either the explicit `name:` above, or one derived from its source (`owner/repo:path` for GitHub, `gist:id/filename` for a Gist). Names show up in `check`/`status`/`pull` output and can be used to target a single entry: `file-tether check shared-rules`.

Secrets never live in `tether.yml` - any value can be a `${VAR}` reference, resolved from the `.env` file next to the config or from OS environment variables. Then:

```
file-tether check     # what's outdated? (no changes)
file-tether pull      # review a summary, confirm once, apply
```

## Commands

| Command | What it does |
| --- | --- |
| `check [name]` | Lists entries with pending pulls and conflicts. Exit code 1 when something is pending, 0 when everything is up to date - automation-friendly. |
| `pull [name]` | Shows one summary table of everything pending and asks once to apply all non-conflicted pulls. Conflicts are never auto-applied - each is resolved individually afterward (apply / skip / view diff). `--yes` applies all non-conflicted pulls without prompting and skips conflicts. |
| `status [name]` | The local view - entries, pulled versions, local modifications. No network, so it never claims things are up to date - only what was true as of the last `check`/`pull`. |
| `init` | Scaffolds the config directory with a commented example `tether.yml`. `--env` also scaffolds a commented `.env` template. |

`check`, `pull`, and `status` all accept an optional `name` argument to target a single entry by its display name (case-insensitive exact match). All commands accept `--config <path>` to override the config location and `--verbose` for debug output.

## Conflict handling

file-tether fingerprints every file it writes. If you edit a managed file locally, it is never silently overwritten. `check` flags it as a conflict, and `pull` includes it in the summary table but excludes it from the single batch confirmation - each conflicted entry (and any new entry whose destination is already occupied) is resolved on its own afterward, with a diff available before deciding. Under `--yes`, conflicted entries are skipped and listed at the end. Folders are treated as atomic units - any local change inside a managed folder marks the whole folder as modified, and applying replaces it wholesale.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success / everything up to date |
| 1 | Pending changes (`check` only) |
| 2 | Configuration error |
| 3 | Network or provider error |

## Building from source

```
dotnet build FileTether.slnx
dotnet test FileTether.slnx
```

Requires the .NET 10 SDK.

## License

[MIT](LICENSE)

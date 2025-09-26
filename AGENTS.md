# Repository Guidelines

This guide defines the standards for contributing to this repository. Keep changes focused, documented, and verified locally before opening a pull request.

## Project Structure & Module Organization
- `src/` – application code organized by domain (e.g., `core/`, `services/`, `cli/`).
- `tests/` – mirrors `src/` structure; shared fixtures in `tests/fixtures/`.
- `scripts/` – developer tasks; provide both `.sh` and `.ps1` where possible.
- `docs/`, `assets/`, `.github/workflows/` – documentation, static files, CI.
- Root config (recommended): `.editorconfig`, `pre-commit` config, linter configs.

## Build, Test, and Development Commands
Use script entrypoints to keep tooling consistent across OSes:
- Setup: `scripts/setup.(sh|ps1)` – install dependencies and pre-commit hooks.
- Develop: `scripts/dev.(sh|ps1)` – run the app/service locally.
- Build: `scripts/build.(sh|ps1)` – produce build artifacts.
- Test: `scripts/test.(sh|ps1)` – run unit tests with coverage.
- Lint/Format: `scripts/lint*` / `scripts/format*` – static checks and formatting.
Example (PowerShell): `./scripts/test.ps1`  •  Example (bash): `bash scripts/test.sh`

## Coding Style & Naming Conventions
- Indentation: 4 spaces (Python); 2 spaces (JS/TS). UTF-8, LF line endings.
- Names: `snake_case` (Python), `camelCase` (JS/TS), `PascalCase` for classes/types.
- Files: modules `snake_case.py`; tests `test_*.py` or `*.test.ts`.
- Tools (recommended): Black + Ruff (Python), Prettier + ESLint (JS/TS). Run formatters before commit.

## Testing Guidelines
- Place tests in `tests/` mirroring `src/` paths; integration tests in `tests/integration/`.
- Strive for ≥80% line coverage; include regression tests for bug fixes.
- Fast feedback first: unit tests > integration tests; use fixtures over sleeps.
- Run via `scripts/test.(sh|ps1)`; if Python, it should call `pytest -q --maxfail=1 --disable-warnings --cov`.

## Commit & Pull Request Guidelines
- Follow Conventional Commits: `feat:`, `fix:`, `docs:`, `refactor:`, `chore:`.
  Example: `feat(cli): add pipeline init command`.
- PRs must: describe the change and rationale, link issues (e.g., `Closes #123`), include tests and docs when applicable, and pass CI.
- Keep PRs small and cohesive; prefer follow-ups over large mixes.

## Security & Configuration Tips
- Never commit secrets; provide `.env.example`. Use `.env.local` and CI secrets for real values.
- Pin dependencies where feasible and run `scripts/audit.(sh|ps1)` for vulnerability scans.

## Agent-Specific Instructions
- Keep patches minimal and localized; prefer small, reviewable diffs.
- Adhere to this file’s conventions for any new code, tests, or scripts.
- When adding tools, expose them through `scripts/` and document the usage above.

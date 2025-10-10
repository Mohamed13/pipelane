# Repository Guidelines

Standards for contributing to this repository. Keep changes focused, documented, and verified locally before opening a pull request.

## Project Structure
- `pipelane-api/` — .NET 8 backend (Api, Application, Infrastructure, Domain)
  - `src/` — projects by layer
  - `tests/` — unit tests (`Pipelane.Tests`)
  - `scripts/` — PowerShell and bash helpers for build/test/dev
- `pipelane-front/` — Angular 17 frontend
  - `src/` — standalone components/services
  - `tools/` — helpers (env injection, swagger fetch)
  - `jest.config.js`, `setup-jest.ts` — unit test config
- `.github/workflows/` — CI (if present)

Root configs recommended: `.editorconfig`, pre-commit, linters. Node and .NET artifacts are already ignored in `.gitignore`.

## Build, Test, and Dev
Backend (.NET):
- Setup: `cd pipelane-api` then `dotnet restore`
- Build: `./pipelane-api/scripts/build.(ps1|sh)`
- Test: `./pipelane-api/scripts/test.(ps1|sh)`
- Dev:  `./pipelane-api/scripts/dev.(ps1|sh)` (serves on `http://localhost:5000` by default)
 - Lint (analyzers): `./pipelane-api/scripts/lint.(ps1|sh)`
 - Format: `./pipelane-api/scripts/format.(ps1|sh)`

Frontend (Angular):
- Setup: `cd pipelane-front && npm ci`
- Env: `tools/inject-env.mjs` generates `src/app/core/env.generated.ts` from `.env` (`API_BASE_URL`, defaults to `http://localhost:5000`)
- Build: `npm run build`
- Test: `npm test`
 - Lint: `npm run lint` (Angular ESLint)
 - Format: `npm run format` (Prettier)
- Optional: Generate API types — `npm run gen:api` (starts API, fetches Swagger, generates TS types)

Security/Audit (manual for now):
- Backend: `cd pipelane-api && dotnet list package --vulnerable`
- Frontend: `cd pipelane-front && npm audit`

## Coding Style
- .NET: conventional C# style, nullable enabled, `PascalCase` for public APIs.
- Angular: `camelCase` for variables/functions, `PascalCase` for classes; standalone components with `OnPush` change detection preferred.
- Formatting: use IDE formatters or project linters; keep diffs minimal.

## Testing
- Backend: xUnit tests in `pipelane-api/tests`; run via the scripts above.
- Frontend: Jest tests under `pipelane-front/src/__tests__`; CI-friendly by default.

## Commits & PRs
- Conventional Commits: `feat:`, `fix:`, `docs:`, `refactor:`, `chore:` (e.g., `feat(api): add campaign endpoints`).
- PRs describe rationale, link issues (e.g., `Closes #123`), include tests/docs as applicable, and pass CI.

## Secrets & Config
- Do not commit secrets. Frontend reads `.env` (local only). Backend reads env vars (`DB_CONNECTION`, `ENCRYPTION_KEY`, `JWT_KEY`). Provide `.env.example` when adding new variables.

## Agent-Specific Instructions
- Keep patches minimal and localized; prefer small, reviewable diffs.
- Align with this file for any new code, tests, or scripts.
- When adding tools, expose them through `scripts/` or npm/dotnet scripts and document usage here.

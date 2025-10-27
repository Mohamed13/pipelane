# Pipelane Monorepo

Pipelane regroupe l'API multi-tenant (.NET 8), la console opérateur Angular 20 et le site marketing Astro. Ce dépôt rassemble tout le nécessaire pour développer, tester et publier les trois applications.

## Structure
- `pipelane-api/` : API ASP.NET 8 (EF Core + SQL Server).
- `pipelane-front/` : console Angular (Material, ng-apexcharts).
- `pipelane-marketing/` : site Astro + Tailwind.
- `docs/` : notes techniques et procédures.

## Pré-requis
- .NET 8 SDK
- Node.js LTS + pnpm ou npm
- Docker (SQL Server local via `docker compose`)

## Démarrage rapide
```bash
# Restaurer l’API
cd pipelane-api
dotnet restore
dotnet run

# Installer le front
cd ../pipelane-front
npm ci
npm start

# Site marketing
cd ../pipelane-marketing
npm install
npm run dev
```

## Tests
- Backend : `./scripts/test.ps1` (ou `.sh`) exécute `dotnet test`.
- Front : `npm run build && npm run ui:test`.
- Marketing : `npm run build`, `npm run test:a11y`, `npm run test:lighthouse`.

## Qualité & CI
- Formatage .NET via `dotnet format`.
- ESLint + Prettier sur Angular et Astro.
- Couverture minimale : 60 % statements backend / lines frontend.
- Git hooks Husky (lint, format, tests courts) et pipelines GitHub Actions vérifient build, lint, tests et audits marketing.
- Scripts d’automatisation disponibles dans `package.json` et `scripts/`.

## Conventions de commit
Les messages doivent suivre Conventional Commits :`type(scope): message` (ex. `feat(api): ajout du suivi`).

## Support
Consultez `QUALITY.md` et `CONTRATS.md` pour la documentation qualité et les contrats d’API. `compte_rendu.md` offre un état synthétique du projet.

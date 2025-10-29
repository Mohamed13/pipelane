Tu travailles dans pipelane-front (Angular 20 standalone + Angular Material + ng-apexcharts).
Objectif: 
1) Refondre la page **Connexion** (UX moderne, agréable, dark futuriste).
2) Ajouter/moderniser la **barre de recherche globale** (palette de commande Ctrl+K) avec historique, filtres et navigation rapide.
3) **Réparer le switch FR/EN** (i18n) et uniformiser les traductions (persistante, instantanée).

RÈGLES GÉNÉRALES
- Commits atomiques, messages explicites. 
- Ne casse pas les contrats d’API. Null-safety systématique.
- Respecte le design system (tokens, .glass, contrastes AA). 
- Accessibilité: focus visible, roles ARIA, labels, `prefers-reduced-motion`.
- Tests Jest à chaque gros bloc, puis `/compact`.

========================================================
SECTION A — Design & Tokens (bases communes)
========================================================
Fichiers: src/theme/_tokens.scss, src/styles.scss

Tâches:
1) Vérifie/ajoute utilitaires:
   .container-narrow { max-width: 520px; margin-inline:auto; padding-inline: clamp(16px,4vw,32px); }
   .glass { background: rgba(255,255,255,.06); border:1px solid rgba(255,255,255,.08); backdrop-filter: blur(10px); border-radius:16px; }
   .btn-primary { min-height:44px; padding:0 18px; border-radius:12px; }
   .on-surface { color: rgba(230,234,242,.92); }
   .on-surface-strong { color:#fff; }
   .muted { color: rgba(230,234,242,.64); }

2) Typo clamp:
   h1{ font-size: clamp(24px,3.2vw,32px); line-height:1.15; letter-spacing:-0.012em }
   h2{ font-size: clamp(20px,2.6vw,26px); line-height:1.2 }

Build → OK, /compact.

========================================================
SECTION B — Page de Connexion (UI + UX + erreurs lisibles)
========================================================
Fichiers: src/app/features/auth/login-page.component.{ts,html,scss} (standalone), src/app/core/auth.service.ts

Tâches:
1) Nouveau composant `LoginPageComponent` (standalone, OnPush), route `/login`:
   - Layout centré verticale (min-height:100vh) : 
     gauche (desktop): image ou gradient sobre; droite: card `.glass container-narrow`.
     mobile: stack (card en premier).
   - Card contenu:
     • Logo/nom produit en haut.
     • Titre h1: “Connexion”.
     • Form Reactive: email, mot de passe, Remember me (MatCheckbox), Accès FR/EN (voir Section D).
     • Icone “voir/masquer” mot de passe.
     • Lien “Mot de passe oublié ?” (placeholder route /forgot).
     • Bouton Se connecter (disabled tant que form invalide).
     • Petit séparateur “ou”.
     • Boutons SSO placeholder (Google/Microsoft) `mat-stroked-button` (désactivés si pas configurés).
     • Bas de carte: “Pas de compte ? Créer un compte” (route /signup si existe, sinon disabled).
   - État d’erreur:
     • Afficher `mat-error` sous les champs (email invalide, mdp requis).
     • Si API renvoie 401/403: bannière `mat-card` rouge clair (AA) : “Identifiants invalides”.
     • Si 5xx: bannière orange: “Service indisponible, réessayez.”

2) Auth flow:
   - `AuthService.login(email, password, remember)` → POST /api/auth/login (existant).
   - Si succès: stocker token + claims; si `remember` → localStorage, sinon sessionStorage.
   - Redirect: vers la page d’origine (query `redirect=`) ou `/analytics`.

3) Sécurité & feedback:
   - Désactiver bouton pendant la requête, `mat-progress-spinner` en suffixe.
   - Guard `AuthGuard` redirige vers `/login?redirect=<url>` si non authentifié.

4) Tests Jest:
   - Affichage erreurs de validation.
   - Appel login et redirection.
   - Bannière 401 bien affichée.

Commit: `feat(auth): new LoginPage modern UI/UX with proper validation & error banners`  
/compact.

========================================================
SECTION C — Barre de recherche globale (Ctrl+K Command Palette)
========================================================
Fichiers: src/app/core/search/command-palette.component.{ts,html,scss}, src/app/core/search/search.service.ts, header/shell

Tâches:
1) `SearchService`:
   - `search(term: string, filters?:{type?: 'prospect'|'conversation'|'campaign'|'list'})`
     → interroger endpoints existants (ou stub) et renvoyer observable `CommandItem[]`:
       { id, label, type, subtitle?, route?, icon? }
   - `recent$`: BehaviorSubject<string[]> (derniers 10 termes).
   - Debounce 200ms, cancel en vol, null-safe.

2) `CommandPaletteComponent` (standalone, OnPush):
   - `MatDialog` pleine largeur max 720px, `.glass`, `role="dialog"`, focus input auto.
   - Champ input (Ctrl+K pour ouvrir, ESC pour fermer) avec placeholder: “Rechercher (prospects, conversations, campagnes…)”.
   - Résultats en liste virtuelle; items groupés par type (Prospects / Conversations / Campagnes / Listes).
   - Affichage:
     • label fort, subtitle (muted), icône par type.
     • navigation clavier ↑/↓, Enter pour ouvrir `route`, Tab pour changer de filtre (chips).
   - Historique: si `term=''` → montrer “Recherches récentes”.
   - Pas de résultat: empty state utile + suggestions.

3) Intégration shell:
   - Icône loupe dans la top bar; `(click)` et `Ctrl+K` ouvrent le dialog.
   - **Important**: ignorer raccourcis si focus dans input/textarea (guard).

4) Tests Jest:
   - Debounce & cancel.
   - Navigation clavier items.
   - Persistance récents (localStorage).

Commit: `feat(search): global command palette (Ctrl+K) with recent, filters, keyboard nav`  
/compact.

========================================================
SECTION D — i18n FR/EN : switch instantané + persistance
========================================================
Fichiers: src/app/core/i18n/language.service.ts, assets/i18n/{fr.json,en.json}, app.config, header

Tâches:
1) Implémenter un `LanguageService`:
   - Utiliser **ngx-translate** (si déjà présent) ou Angular i18n alternatif.
   - `current$` BehaviorSubject<'fr'|'en'>; persister `lang` dans localStorage (`pipelane_lang`).
   - `set(lang)` → charger les fichiers et appliquer instantanément.

2) Switch FR/EN (header):
   - Bouton `MatMenu` avec FR/EN; coche sur langue active; `(click)` → `LanguageService.set`.
   - Mettre à jour `dir='ltr'` (les deux langues sont LTR).
   - Pas de hard refresh.

3) Couverture:
   - Ajoute/complète `fr.json` / `en.json` pour les clés:
     `login.title`, `login.email`, `login.password`, `login.remember`, `login.submit`, `login.forgot`, 
     `search.placeholder`, `search.recent`, `search.noResults`, 
     `errors.network`, `errors.invalidCredentials`, etc.
   - Pages clés: login, header, search, errors.
   - Fallback par défaut FR.

4) Fix du bug “ne fonctionne pas”:
   - Vérifier que les pipes/Directives utilisent translate instantané (`| translate`) et que modules chargent `TranslateModule`.
   - Si le shell ne réagit pas: `cdr.markForCheck()` après `set(lang)`.

5) Tests Jest:
   - Switch FR→EN met à jour un label sans reload.
   - Persistance: reload app → conserve la langue choisie.

Commit: `fix(i18n): working FR/EN instant switch with persistence; translations added`  
/compact.

========================================================
SECTION E — Accessibilité, focus, micro-interactions
========================================================
Tâches:
1) Focus ring (global) sur tous les boutons/lien/inputs: outline 2px #75F0FF/60 sur `:focus-visible`.
2) Login: associer `for` aux labels, `aria-invalid` quand erreur, `aria-live="polite"` pour bannière d’erreur.
3) Command Palette: `role="listbox"`, `role="option"`, `aria-activedescendant` mis à jour; ESC ferme; annonces concises.

Tests: a11y lints si présent.  
Commit: `chore(a11y): focus ring + aria improvements for login & search`  
/compact.

========================================================
SECTION F — QA & Tests finaux
========================================================
Tâches:
1) `npm run build && npm run ui:test`
2) Scénarios manuels:
   - Connexion réussie/échouée; Remember me; spinner; redirection post-login.
   - Ctrl+K: ouvrir/fermer; taper, naviguer au clavier; ouvrir un résultat; voir historiques.
   - FR/EN: basculer sans reload; persister au reload.
3) Light perf: vérifier que la palette se charge lazy (dialog).

Commit final: 
`feat(front): polished Login, global Search (Ctrl+K), working FR/EN switch — modern, accessible, and fast`

/compact

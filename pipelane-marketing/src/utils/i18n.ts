import fr from "../i18n/fr.json" assert { type: "json" };
import en from "../i18n/en.json" assert { type: "json" };

export const dictionaries = { fr, en } as const;

export type Locale = keyof typeof dictionaries;
export type Dictionary = typeof dictionaries.fr;
export type NavigationDictionary = Dictionary["navigation"];
export type FooterDictionary = Dictionary["footer"];
export type HomeDictionary = Dictionary["home"];
export type FormsDictionary = Dictionary["forms"];
export type DemoFormDictionary = FormsDictionary["demo"];

export const DEFAULT_LOCALE: Locale = "fr";

export const SUPPORTED_LOCALES: Locale[] = ["fr", "en"];

export function getTranslations(locale: Locale): Dictionary {
  return dictionaries[locale];
}

export function resolveLocaleFromPath(pathname: string): Locale {
  const segments = pathname.split("/").filter(Boolean);
  if (segments[0] === "en") {
    return "en";
  }
  return DEFAULT_LOCALE;
}

export function getAlternateLocale(locale: Locale): Locale {
  return locale === "fr" ? "en" : "fr";
}

export function buildLocalizedPath(pathname: string, target: Locale): string {
  const normalized =
    pathname !== "/" && pathname.endsWith("/") ? pathname.slice(0, -1) : pathname;
  const segments = normalized.split("/").filter(Boolean);

  if (target === DEFAULT_LOCALE) {
    if (segments[0] === "en") {
      const rest = segments.slice(1);
      return rest.length ? `/${rest.join("/")}` : "/";
    }
    return normalized || "/";
  }

  if (segments[0] === "en") {
    return normalized || "/en";
  }

  return "/en";
}

export function formatWithYear(template: string, year: number): string {
  return template.replace("{year}", String(year));
}

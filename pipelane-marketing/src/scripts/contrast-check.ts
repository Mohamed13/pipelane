const parseRgb = (value: string) => {
  if (!value) return null;
  if (value.startsWith("rgb")) {
    const parts = value.replace(/rgba?\(|\)/g, "").split(",").map((part) => part.trim());
    const [r, g, b, a = "1"] = parts;
    return {
      r: Number(r),
      g: Number(g),
      b: Number(b),
      a: Number(a)
    };
  }
  return null;
};

const luminance = ({ r, g, b }: { r: number; g: number; b: number }) => {
  const transform = (channel: number) => {
    const c = channel / 255;
    return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  };
  const R = transform(r);
  const G = transform(g);
  const B = transform(b);
  return 0.2126 * R + 0.7152 * G + 0.0722 * B;
};

const contrastRatio = (foreground: { r: number; g: number; b: number }, background: { r: number; g: number; b: number }) => {
  const L1 = luminance(foreground) + 0.05;
  const L2 = luminance(background) + 0.05;
  return L1 > L2 ? L1 / L2 : L2 / L1;
};

const isTransparent = (color: ReturnType<typeof parseRgb>) => !color || color.a < 0.01;

const resolveBackground = (element: Element | null): ReturnType<typeof parseRgb> => {
  let current: Element | null = element;
  while (current) {
    const styles = window.getComputedStyle(current);
    const bg = parseRgb(styles.backgroundColor);
    if (!isTransparent(bg)) {
      return bg;
    }
    current = current.parentElement;
  }
  const root = window.getComputedStyle(document.body);
  return parseRgb(root.backgroundColor) ?? { r: 0, g: 0, b: 0, a: 1 };
};

const evaluateContrast = () => {
  const nodes = document.querySelectorAll<HTMLElement>(
    "h1, h2, h3, h4, p, button, a.btn-primary, a.btn-ghost, span.chip"
  );
  const offenders: Array<{ element: HTMLElement; ratio: number; threshold: number }> = [];

  nodes.forEach((node) => {
    const styles = window.getComputedStyle(node);
    const fg = parseRgb(styles.color);
    if (!fg) return;
    const bg = resolveBackground(node);
    if (!bg) return;

    const ratio = contrastRatio(fg, bg);
    const fontSizePx = parseFloat(styles.fontSize);
    const weight = parseInt(styles.fontWeight, 10);
    const isBold = !Number.isNaN(weight) && weight >= 600;
    const isLarge = fontSizePx >= 18.66 || (isBold && fontSizePx >= 14);
    const threshold = isLarge ? 3 : 4.5;
    if (ratio + 0.01 < threshold) {
      offenders.push({ element: node, ratio, threshold });
    }
  });

  if (offenders.length) {
    console.groupCollapsed(`⚠️ Contrast check: ${offenders.length} élément(s) sous le seuil AA`);
    offenders.forEach(({ element, ratio, threshold }) => {
      console.warn(
        `Contraste ${ratio.toFixed(2)} < ${threshold.toFixed(1)} pour`,
        element,
        'fond =',
        resolveBackground(element)
      );
    });
    console.groupEnd();
  }
};

document.readyState === "loading"
  ? document.addEventListener("DOMContentLoaded", evaluateContrast)
  : evaluateContrast();

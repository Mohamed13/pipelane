import { Directive, ElementRef, Input, OnDestroy, OnInit, inject } from '@angular/core';

type RevealAnimation =
  | 'fade'
  | 'fade-up'
  | 'scale'
  | 'scale-in'
  | 'stagger'
  | ''
  | null
  | undefined;

@Directive({
  standalone: true,
  selector: '[plRevealOnScroll]',
})
export class RevealOnScrollDirective implements OnInit, OnDestroy {
  @Input('plRevealOnScroll') animation: RevealAnimation = 'fade-up';

  private observer?: IntersectionObserver;
  private readonly prefersReducedMotion =
    window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
  private readonly host = inject(ElementRef) as ElementRef<HTMLElement>;

  ngOnInit(): void {
    const element = this.host.nativeElement;
    if (this.prefersReducedMotion) {
      this.instantReveal(element);
      return;
    }

    const animation = (this.animation ?? 'fade-up').toLowerCase();
    this.applyInitialState(element, animation);

    this.observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            this.playAnimation(entry.target as HTMLElement, animation);
            this.observer?.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.2 },
    );

    if (animation === 'stagger') {
      Array.from(element.children).forEach((child, index) => {
        const childEl = child as HTMLElement;
        childEl.style.transitionDelay = `${index * 70}ms`;
        this.observer?.observe(childEl);
      });
    } else {
      this.observer.observe(element);
    }
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  private applyInitialState(element: HTMLElement, animation: string): void {
    switch (animation) {
      case 'scale':
      case 'scale-in':
        element.classList.add('anim-scale-in');
        break;
      case 'stagger':
        element.classList.add('anim-stagger');
        Array.from(element.children).forEach((child) => child.classList.add('anim-stagger-child'));
        break;
      case 'fade':
      case 'fade-up':
      default:
        element.classList.add('reveal-init');
        break;
    }
  }

  private playAnimation(element: HTMLElement, animation: string): void {
    switch (animation) {
      case 'scale':
      case 'scale-in':
        element.classList.add('anim-in-view');
        break;
      case 'stagger':
        element.classList.add('anim-in-view');
        break;
      case 'fade':
      case 'fade-up':
      default:
        element.classList.add('reveal-visible');
        element.classList.remove('reveal-init');
        break;
    }
  }

  private instantReveal(element: HTMLElement): void {
    if (this.animation === 'stagger') {
      element.classList.add('anim-in-view');
      Array.from(element.children).forEach((child) =>
        (child as HTMLElement).classList.add('anim-in-view'),
      );
    } else {
      element.classList.add('reveal-visible');
      element.classList.remove('reveal-init');
    }
  }
}

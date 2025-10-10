import { Directive, ElementRef, OnDestroy, OnInit } from '@angular/core';

@Directive({
  standalone: true,
  selector: '[plRevealOnScroll]'
})
export class RevealOnScrollDirective implements OnInit, OnDestroy {
  private observer?: IntersectionObserver;

  constructor(private readonly host: ElementRef<HTMLElement>) {}

  ngOnInit(): void {
    const element = this.host.nativeElement;
    element.classList.add('reveal-init');
    this.observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          element.classList.add('reveal-visible');
          element.classList.remove('reveal-init');
          this.observer?.unobserve(element);
        }
      });
    }, { threshold: 0.2 });
    this.observer.observe(element);
  }

  ngOnDestroy(): void {
    if (this.observer) {
      this.observer.disconnect();
    }
  }
}


import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PipelaneLogoComponent } from '../app/shared/ui/pipelane-logo/pipelane-logo.component';

describe('PipelaneLogoComponent', () => {
  let fixture: ComponentFixture<PipelaneLogoComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PipelaneLogoComponent],
    }).compileComponents();
  });

  afterEach(() => {
    if (fixture) {
      fixture.destroy();
    }
  });

  it('renders the default inline variant with matching gradient reference', () => {
    fixture = TestBed.createComponent(PipelaneLogoComponent);
    fixture.detectChanges();

    const svg: SVGElement | null = fixture.nativeElement.querySelector('svg');
    expect(svg).toBeTruthy();

    const gradient = svg?.querySelector('linearGradient');
    expect(gradient).toBeTruthy();

    const gradientId = gradient?.getAttribute('id') ?? '';
    expect(gradientId).toMatch(/^plFlowDefault/);

    const path = svg?.querySelector('path');
    expect(path?.getAttribute('stroke')).toBe(`url(#${gradientId})`);
  });

  it('switches to the compact inline variant without a background rect', () => {
    fixture = TestBed.createComponent(PipelaneLogoComponent);
    fixture.componentInstance.variant = 'compact';
    fixture.detectChanges();

    const svg: SVGElement | null = fixture.nativeElement.querySelector('svg');
    expect(svg).toBeTruthy();

    expect(svg?.querySelector('rect')).toBeNull();

    const text = svg?.querySelector('text');
    expect(text?.getAttribute('fill')).toBe('#0D0F12');
  });

  it('falls back to an img element when inline mode is disabled', () => {
    fixture = TestBed.createComponent(PipelaneLogoComponent);
    fixture.componentInstance.inline = false;
    fixture.componentInstance.variant = 'compact';
    fixture.detectChanges();

    const img: HTMLImageElement | null = fixture.nativeElement.querySelector('img');
    expect(img).toBeTruthy();
    expect(img?.getAttribute('src')).toBe('assets/brand/pipelane-compact.svg');
    expect(img?.getAttribute('alt')).toBe('Pipelane logo');
  });

  it('assigns unique gradient identifiers for each instance', () => {
    const fixtureA = TestBed.createComponent(PipelaneLogoComponent);
    fixtureA.detectChanges();

    const fixtureB = TestBed.createComponent(PipelaneLogoComponent);
    fixtureB.detectChanges();

    const idA = fixtureA.nativeElement.querySelector('linearGradient')?.getAttribute('id');
    const idB = fixtureB.nativeElement.querySelector('linearGradient')?.getAttribute('id');

    expect(idA).toBeTruthy();
    expect(idB).toBeTruthy();
    expect(idA).not.toBe(idB);

    fixtureA.destroy();
    fixtureB.destroy();
  });
});

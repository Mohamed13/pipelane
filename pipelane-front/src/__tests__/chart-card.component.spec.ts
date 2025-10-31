import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ChartCardComponent, ChartCardConfig } from '../app/shared/ui/chart-card.component';

describe('ChartCardComponent', () => {
  let fixture: ComponentFixture<ChartCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChartCardComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ChartCardComponent);
  });

  it('renders empty state when series has no values', () => {
    const component = fixture.componentInstance;
    const config: ChartCardConfig = {
      title: 'Demo chart',
      series: [],
      options: {},
      emptyState: { title: 'No data yet', message: 'Add activity to populate this chart.' },
    };

    component.config = config;
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('No data yet');
    expect(element.querySelector('mat-spinner')).toBeNull();
  });

  it('renders spinner while loading', () => {
    const component = fixture.componentInstance;
    const config: ChartCardConfig = {
      title: 'Loading chart',
      series: [],
      options: {},
      loading: true,
    };

    component.config = config;
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    const spinner = element.querySelector('mat-spinner');
    expect(spinner).not.toBeNull();
    expect(element.textContent).toContain('Fetching insights…');
  });
});

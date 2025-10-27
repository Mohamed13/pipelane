import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { TourService } from '../app/core/tour.service';
import { ShepherdService } from 'angular-shepherd';

class ShepherdStub {
  start = jest.fn();
  addSteps = jest.fn();
  modal = false;
  defaultStepOptions: Record<string, unknown> = {};
  private handlers: Record<string, () => void> = {};

  tourObject = {
    on: (event: string, handler: () => void) => {
      this.handlers[event] = handler;
    },
  };

  trigger(event: string) {
    this.handlers[event]?.();
  }
}

describe('TourService', () => {
  let service: TourService;
  let shepherd: ShepherdStub;
  let createdElements: HTMLElement[];

  const addTarget = (dataTour: string) => {
    const el = document.createElement('div');
    el.setAttribute('data-tour', dataTour);
    document.body.appendChild(el);
    createdElements.push(el);
  };

  beforeEach(() => {
    jest.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [
        TourService,
        { provide: ShepherdService, useClass: ShepherdStub },
        provideRouter([]),
      ],
    });

    service = TestBed.inject(TourService);
    shepherd = TestBed.inject(ShepherdService) as unknown as ShepherdStub;
    localStorage.removeItem('pipelane_tour_done');
    createdElements = [];
  });

  afterEach(() => {
    jest.useRealTimers();
    localStorage.removeItem('pipelane_tour_done');
    createdElements.forEach((el) => el.remove());
  });

  it('starts tour on first launch and marks completion', () => {
    addTarget('onboarding-email');
    service.initialize();
    expect(shepherd.addSteps).toHaveBeenCalled();

    jest.runAllTimers();
    expect(shepherd.start).toHaveBeenCalledTimes(1);

    shepherd.trigger('complete');
    expect(localStorage.getItem('pipelane_tour_done')).toBe('true');
  });

  it('does not auto start when no targets are present', () => {
    service.initialize();

    jest.runAllTimers();
    expect(shepherd.start).not.toHaveBeenCalled();
  });

  it('does not auto start tour when already completed', () => {
    localStorage.setItem('pipelane_tour_done', 'true');
    service.initialize();

    jest.runAllTimers();
    expect(shepherd.start).not.toHaveBeenCalled();
  });

  it('replays tour when requested', () => {
    addTarget('onboarding-email');
    localStorage.setItem('pipelane_tour_done', 'true');
    service.replay();

    expect(shepherd.addSteps).toHaveBeenCalled();
    expect(shepherd.start).toHaveBeenCalled();
  });
});

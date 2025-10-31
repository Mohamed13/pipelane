import { TestBed } from '@angular/core/testing';
import { MatIconRegistry } from '@angular/material/icon';

import { IconService } from '../app/core/icon.service';

describe('IconService', () => {
  it('configures Material Symbols as default font set', () => {
    const setDefaultFontSetClass = jest.fn();
    const registerFontClassAlias = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        IconService,
        {
          provide: MatIconRegistry,
          useValue: {
            setDefaultFontSetClass,
            registerFontClassAlias,
          },
        },
      ],
    });

    TestBed.inject(IconService);

    expect(setDefaultFontSetClass).toHaveBeenCalledWith('material-symbols-rounded');
    expect(registerFontClassAlias).toHaveBeenCalledWith('material-symbols-rounded', 'msr');
  });
});

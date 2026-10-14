import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of } from 'rxjs';
import { PsgcAddressComponent } from './psgc-address.component';
import { PsgcIndex, PsgcService } from '../services/psgc.service';
import { emptyPsgcAddress } from '../learner';
import { PsgcAddress } from '../models';

const INDEX: PsgcIndex = {
  version: 'test',
  regions: [{ code: '1300000000', name: 'NCR' }, { code: '0700000000', name: 'Region VII' }],
  provinces: [
    { code: '0702200000', name: 'Cebu', regionCode: '0700000000' },
    { code: '0701200000', name: 'Bohol', regionCode: '0700000000' },
  ],
  cities: [
    { code: '1381300000', name: 'Quezon City', regionCode: '1300000000', provinceCode: null },
    { code: '0702201000', name: 'Alcantara', regionCode: '0700000000', provinceCode: '0702200000' },
    { code: '0701201000', name: 'Tagbilaran', regionCode: '0700000000', provinceCode: '0701200000' },
    { code: '0730600000', name: 'Cebu City', regionCode: '0700000000', provinceCode: null },
  ],
};

const BARANGAYS: Record<string, { code: string; name: string }[]> = {
  '1381300000': [{ code: '1381300001', name: 'Alicia' }],
  '0702201000': [{ code: '0702201001', name: 'Cabadiangan' }],
  '0701201000': [{ code: '0701201001', name: 'Booy' }],
  '0730600000': [{ code: '0730600001', name: 'Adlaon' }],
};

describe('PsgcAddressComponent', () => {
  function create(address: PsgcAddress = emptyPsgcAddress()): PsgcAddressComponent {
    TestBed.configureTestingModule({
      imports: [PsgcAddressComponent],
      providers: [{
        provide: PsgcService,
        useValue: { index: () => of(INDEX), barangaysOf: (c: { code: string }) => of(BARANGAYS[c.code] ?? []) },
      }],
    });
    const fixture = TestBed.createComponent(PsgcAddressComponent);
    fixture.componentRef.setInput('address', address);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('skips the province step for a region with no provinces (NCR)', () => {
    const c = create();
    c.selectRegion('1300000000');
    expect(c.showProvinceStep()).toBeFalse();
    expect(c.cities().map(x => x.name)).toEqual(['Quezon City']);
  });

  it('changing the region clears province, city and barangay', () => {
    const c = create();
    c.selectRegion('0700000000');
    c.selectProvince('0702200000');
    c.selectCity('0702201000');
    c.selectBarangay('0702201001');
    expect(c.address.barangayName).toBe('Cabadiangan');

    c.selectRegion('1300000000');
    expect(c.address.provinceCode).toBeNull();
    expect(c.address.cityMunicipalityCode).toBeNull();
    expect(c.address.barangayCode).toBeNull();
    expect(c.barangays()).toEqual([]);
  });

  it('changing the province clears city and barangay', () => {
    const c = create();
    c.selectRegion('0700000000');
    c.selectProvince('0702200000');
    c.selectCity('0702201000');
    c.selectBarangay('0702201001');
    expect(c.address.barangayName).toBe('Cabadiangan');

    c.selectProvince('0701200000');
    expect(c.address.provinceName).toBe('Bohol');
    expect(c.address.cityMunicipalityCode).toBeNull();
    expect(c.address.cityMunicipalityName).toBeNull();
    expect(c.address.barangayCode).toBeNull();
    expect(c.address.barangayName).toBeNull();
    expect(c.barangays()).toEqual([]);
    expect(c.cities().map(x => x.name)).toEqual(['Tagbilaran']);

    c.selectCity('0701201000');
    c.selectBarangay('0701201001');
    c.selectProvince(c.NO_PROVINCE);
    expect(c.address.provinceCode).toBeNull();
    expect(c.address.cityMunicipalityCode).toBeNull();
    expect(c.address.barangayCode).toBeNull();
    expect(c.barangays()).toEqual([]);
  });

  it('offers independent cities in a region that has provinces', () => {
    const c = create();
    c.selectRegion('0700000000');
    expect(c.hasIndependentCities()).toBeTrue();
    c.selectProvince(c.NO_PROVINCE);
    expect(c.cities().map(x => x.name)).toEqual(['Cebu City']);
    c.selectCity('0730600000');
    expect(c.address.provinceCode).toBeNull();
    expect(c.barangays().map(b => b.name)).toEqual(['Adlaon']);
  });

  it('restores selections from an existing address', () => {
    const c = create({ ...emptyPsgcAddress(), regionCode: '0700000000', cityMunicipalityCode: '0730600000', barangayCode: '0730600001' });
    expect(c.provinceChoice()).toBe(c.NO_PROVINCE);
    expect(c.barangays().length).toBe(1);
  });

  it('ignores a stale barangay response for a previously selected city', () => {
    const pending: Record<string, Subject<{ code: string; name: string }[]>> = {
      A: new Subject(), B: new Subject(),
    };
    const byCity: Record<string, Observable<{ code: string; name: string }[]>> = {
      '0702201000': pending['A'], '0730600000': pending['B'],
    };
    TestBed.configureTestingModule({
      imports: [PsgcAddressComponent],
      providers: [{ provide: PsgcService, useValue: { index: () => of(INDEX), barangaysOf: (c: { code: string }) => byCity[c.code] } }],
    });
    const fixture = TestBed.createComponent(PsgcAddressComponent);
    fixture.componentRef.setInput('address', emptyPsgcAddress());
    fixture.detectChanges();
    const c = fixture.componentInstance;

    c.selectRegion('0700000000');
    c.selectProvince('0702200000');
    c.selectCity('0702201000');
    c.selectProvince(c.NO_PROVINCE);
    c.selectCity('0730600000');
    pending['B'].next([{ code: '0730600001', name: 'Adlaon' }]);
    pending['A'].next([{ code: '0702201001', name: 'Cabadiangan' }]);

    expect(c.barangays().map(b => b.name)).toEqual(['Adlaon']);
  });

  it('emits changed after every selection', () => {
    const c = create();
    const emitted: PsgcAddress[] = [];
    c.changed.subscribe(a => emitted.push({ ...a }));
    c.selectRegion('1300000000');
    c.selectCity('1381300000');
    c.selectBarangay('1381300001');
    expect(emitted.length).toBe(3);
    expect(emitted[2].barangayName).toBe('Alicia');
  });
});

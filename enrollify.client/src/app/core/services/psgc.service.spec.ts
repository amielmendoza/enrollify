import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PsgcIndex, PsgcService } from './psgc.service';

describe('PsgcService', () => {
  it('retries the index on the next call after a failed load', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(PsgcService);
    const http = TestBed.inject(HttpTestingController);

    let failed = false;
    service.index().subscribe({ error: () => (failed = true) });
    http.expectOne('psgc/index.json').flush('missing', { status: 404, statusText: 'Not Found' });
    expect(failed).toBeTrue();

    let loaded: PsgcIndex | null = null;
    service.index().subscribe(ix => (loaded = ix));
    http.expectOne('psgc/index.json').flush({ version: 'v1', regions: [], provinces: [], cities: [] });
    expect(loaded!.version).toBe('v1');
    http.verify();
  });
});

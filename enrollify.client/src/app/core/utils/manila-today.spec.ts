import { manilaToday } from './manila-today';

describe('manilaToday', () => {
  it('rolls to the next calendar day after 16:00 UTC (Manila is UTC+8)', () => {
    expect(manilaToday(new Date('2026-06-01T16:30:00Z'))).toBe('2026-06-02');
  });

  it('stays on the same day just before Manila midnight', () => {
    expect(manilaToday(new Date('2026-06-01T15:59:59Z'))).toBe('2026-06-01');
  });

  it('handles the year boundary', () => {
    expect(manilaToday(new Date('2026-12-31T20:00:00Z'))).toBe('2027-01-01');
  });
});

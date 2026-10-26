const MANILA_OFFSET_MS = 8 * 3600 * 1000;

/**
 * Today's calendar date in Manila (UTC+8, no DST) as yyyy-MM-dd, regardless of
 * the browser's timezone. `now` is injectable so callers/tests don't depend on
 * the wall clock.
 */
export function manilaToday(now: Date = new Date()): string {
  return new Date(now.getTime() + MANILA_OFFSET_MS).toISOString().slice(0, 10);
}

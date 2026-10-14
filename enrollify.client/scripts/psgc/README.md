# PSGC lookup data

`public/psgc/` holds Enrollify's copy of the Philippine Standard Geographic Code (PSGC),
generated from the Philippine Statistics Authority's official publication. Address pickers
load `index.json` (regions, provinces, cities/municipalities) up front and one
`barangays/<provinceCode or regionCode>.json` file when a city is chosen.

## Refreshing (PSA republishes several times a year)

1. Download the latest PSGC Publication datafile (.xlsx) from https://psa.gov.ph/classification/psgc
2. From `enrollify.client/`: `python scripts/psgc/build_psgc.py <path-to.xlsx>`
3. Check the printed counts (about 18 regions, 80+ provinces, 1,600+ cities/municipalities,
   42,000+ barangays, 0 unresolved). PSA appends footnote markers to a few names ("City of Lucenaa/",
   "Fuentes*"); the script strips them and prints each one — confirm none was really part of a name.
   Then commit `public/psgc/`.

Records store codes AND names, so a refresh never changes how existing addresses read.

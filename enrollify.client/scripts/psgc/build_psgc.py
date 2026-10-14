"""
Build Enrollify's PSGC lookup files from the PSA's official PSGC publication workbook.

    python scripts/psgc/build_psgc.py <PSGC-publication.xlsx>      (run from enrollify.client/)

Writes public/psgc/index.json and public/psgc/barangays/<code>.json. Python 3 standard library
only (zipfile + ElementTree read the .xlsx directly). Codes are 10-digit STRINGS: Excel stores
them as numbers, which drops the leading zero of regions 01-09 — normalize_code() restores it.
"""
import json
import re
import sys
import zipfile
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

MAIN = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
DOC_REL = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
PKG_REL = 'http://schemas.openxmlformats.org/package/2006/relationships'
OUT = Path('public/psgc')


def q(tag):
    return '{%s}%s' % (MAIN, tag)


def read_rows(xlsx, sheet_name='PSGC'):
    with zipfile.ZipFile(xlsx) as z:
        shared = []
        if 'xl/sharedStrings.xml' in z.namelist():
            for si in ET.fromstring(z.read('xl/sharedStrings.xml')).findall(q('si')):
                shared.append(''.join(t.text or '' for t in si.iter(q('t'))))
        wb = ET.fromstring(z.read('xl/workbook.xml'))
        rels = ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))
        targets = {r.get('Id'): r.get('Target') for r in rels.findall('{%s}Relationship' % PKG_REL)}
        sheets = list(wb.find(q('sheets')))
        sheet = next((s for s in sheets if s.get('name', '').strip().upper() == sheet_name), None)
        if sheet is None:
            sys.exit(f'Sheet "{sheet_name}" not found; sheets are: {[s.get("name") for s in sheets]}')
        target = targets[sheet.get('{%s}id' % DOC_REL)].lstrip('/')
        path = target if target.startswith('xl/') else 'xl/' + target
        for row in ET.fromstring(z.read(path)).iter(q('row')):
            cells = {}
            for c in row.findall(q('c')):
                col = re.match(r'[A-Z]+', c.get('r')).group(0)
                kind, v = c.get('t'), c.find(q('v'))
                if kind == 's' and v is not None:
                    val = shared[int(v.text)]
                elif kind == 'inlineStr':
                    val = ''.join(t.text or '' for t in c.iter(q('t')))
                else:
                    val = v.text if v is not None else ''
                cells[col] = (val or '').strip()
            yield cells


def normalize_code(raw):
    s = (raw or '').strip()
    if re.fullmatch(r'\d+\.0+', s):
        s = s.split('.')[0]
    if not re.fullmatch(r'\d{1,10}', s):
        return None
    return s.zfill(10)


def clean_name(raw):
    # PSA appends footnote markers to some names ("City of Lucenaa/", "Fuentes*"); they aren't part of the name.
    return re.sub(r'\s*(?:[a-z]/|\*+)$', '', re.sub(r'\s+', ' ', (raw or '').strip()))


def main(xlsx):
    rows = read_rows(xlsx)
    header = None
    for cells in rows:
        texts = {k: v.lower() for k, v in cells.items()}
        if any('psgc' in v for v in texts.values()) and any('geographic level' in v for v in texts.values()):
            header = cells
            break
    if header is None:
        sys.exit('Header row (with "PSGC" and "Geographic Level" columns) not found.')
    code_col = next(k for k, v in header.items() if '10-digit' in v.lower()) if any('10-digit' in v.lower() for v in header.values()) \
        else next(k for k, v in header.items() if 'psgc' in v.lower())
    name_col = next(k for k, v in header.items() if v.strip().lower() == 'name')
    level_col = next(k for k, v in header.items() if 'geographic level' in v.lower())

    places = {}  # code -> (name, level)
    for cells in rows:
        code = normalize_code(cells.get(code_col))
        level = (cells.get(level_col) or '').strip().lower()
        name = clean_name(cells.get(name_col))
        if code and level and name:
            places[code] = (name, level)

    def of(*levels):
        return {c: n for c, (n, lv) in places.items() if lv in levels}

    regions, provinces = of('reg'), of('prov')
    cities = of('city', 'mun', 'sgu')        # SGU: BARMM special geographic areas behave like municipalities
    submuns, barangays = of('submun'), of('bgy')

    region_of = lambda c: c[:2] + '0' * 8
    province_slot = lambda c: c[:5] + '0' * 5
    city_slot = lambda c: c[:7] + '000'

    city_rows = []
    for code, name in cities.items():
        p = province_slot(code)
        province = p if p in provinces else None
        city_rows.append({'code': code, 'name': name, 'regionCode': region_of(code), 'provinceCode': province})

    by_file = defaultdict(lambda: defaultdict(list))
    city_index = {c['code']: c for c in city_rows}
    unresolved = []
    for code, name in barangays.items():
        parent = city_slot(code)
        if parent in submuns:                 # e.g. Manila's districts: roll up to the city itself
            parent = province_slot(parent)
        if parent not in city_index:
            parent = province_slot(code) if province_slot(code) in city_index else None
        if parent is None:
            unresolved.append((code, name))
            continue
        city = city_index[parent]
        by_file[city['provinceCode'] or city['regionCode']][parent].append({'code': code, 'name': name})

    missing_regions = {c['regionCode'] for c in city_rows} - set(regions)
    print(f'regions={len(regions)} provinces={len(provinces)} cities/municipalities={len(city_rows)} '
          f'barangays={len(barangays)} unresolved={len(unresolved)} files={len(by_file)}')
    if unresolved:
        print('First unresolved barangays:', unresolved[:10])
        sys.exit('Some barangays could not be placed under a city/municipality — inspect the codes above.')
    if missing_regions or len(regions) < 15 or len(barangays) < 40000:
        sys.exit(f'Sanity check failed (missing regions: {sorted(missing_regions)}).')

    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'barangays').mkdir(exist_ok=True)
    for old in (OUT / 'barangays').glob('*.json'):
        old.unlink()
    index = {
        'version': Path(xlsx).name,
        'regions': sorted(({'code': c, 'name': n} for c, n in regions.items()), key=lambda r: r['code']),
        'provinces': sorted(({'code': c, 'name': n, 'regionCode': region_of(c)} for c, n in provinces.items()), key=lambda r: r['name']),
        'cities': sorted(city_rows, key=lambda r: r['name']),
    }
    (OUT / 'index.json').write_text(json.dumps(index, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    for key, cities_in_file in by_file.items():
        payload = {c: sorted(bs, key=lambda b: b['name']) for c, bs in cities_in_file.items()}
        (OUT / 'barangays' / f'{key}.json').write_text(json.dumps(payload, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    print(f'Wrote {OUT / "index.json"} and {len(by_file)} barangay files.')


if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])

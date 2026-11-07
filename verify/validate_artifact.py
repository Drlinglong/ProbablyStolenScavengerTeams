"""Read-only package/text/art checks. Does not load the game or edit images."""
from pathlib import Path
import hashlib
import csv
import argparse
import collections
import json
import re
import sys
import xml.etree.ElementTree as ET

from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument('--game-root', type=Path, help='Optional local installation for assembly provenance')
args = parser.parse_args()

root = Path(__file__).resolve().parents[1]
package = root / 'package' / 'ScavengerTeams'
manifest = ET.parse(package / 'manifest.xml').getroot()
assert manifest.findtext('ID') == 'ps_scavenger_teams'
assert manifest.findtext('ModVersion') == '0.3.0-dev'
assert manifest.findtext('GameVersions/Version') == '0.46D'
assert manifest.findtext('Assemblies/Assembly') == 'ScavengerTeams.dll'
assert not list(manifest.find('Capabilities'))
locale_keys = None
tables = {}
for locale in ('zh','en','fr','de','ja','ru','es','pl'):
    path = root / f'content/Localization/{locale}.csv'
    rows = list(csv.reader(line for line in path.read_text(encoding='utf-8-sig').splitlines()
                           if line.strip() and not line.lstrip().startswith('#')))
    assert all(len(row) == 2 and row[0] for row in rows), path
    keys = [row[0] for row in rows]
    assert len(keys) == len(set(keys)) == 52, path
    if locale_keys is not None: assert set(keys) == locale_keys
    locale_keys = set(keys)
    tables[locale] = dict(rows)
    assert (package / f'Localization/{locale}.csv').read_bytes() == path.read_bytes()
for locale, table in tables.items():
    for key, value in table.items():
        source = tables['zh'][key]
        assert value.strip(), (locale, key, 'empty value')
        assert collections.Counter(re.findall(r'\{\d+\}', value)) == collections.Counter(re.findall(r'\{\d+\}', source)), (locale, key, 'placeholder drift')
        assert value.count('\\n') == source.count('\\n'), (locale, key, 'dialogue boundary drift')
        assert re.findall(r'</?[^>]+>', value) == re.findall(r'</?[^>]+>', source), (locale, key, 'tag drift')
assert (package / 'LICENSE').read_bytes() == (root / 'LICENSE').read_bytes()
for alias in ('zh-Hans','zh-CN'):
    assert (package / f'Localization/{alias}.csv').read_bytes() == (root / 'content/Localization/zh.csv').read_bytes()
assert {p.name for p in package.rglob('*.dll')} == {'ScavengerTeams.dll'}
image = Image.open(package / 'Textures/Items/printer_crate.png')
assert image.size == (48, 48) and image.mode == 'RGBA'
assert image.getchannel('A').getextrema() == (0, 255), 'Missing transparency or opaque object'
sprite_keys = ['printer_crate', 'metal_crate', 'electronic_crate', 'ore_crate']
sprite_hashes = set()
for key in sprite_keys:
    path = package / f'Textures/Items/{key}.png'
    sprite = Image.open(path)
    assert sprite.size == (48, 48) and sprite.mode == 'RGBA', key
    assert sprite.getchannel('A').getextrema() == (0, 255), key
    sprite_hashes.add(hashlib.sha256(path.read_bytes()).hexdigest())
assert len(sprite_hashes) == 4, 'Material icons must not be accidental duplicate files'
assert (root / 'assets/source/printer-crate-original-style-two-spools-v4.png').is_file()
assert (root / 'assets/source/printer-crate-generated.png').is_file()
assert (root / 'assets/source/PROMPT.md').is_file()
for folder in ('src', 'docs', 'content', 'verify'):
    for path in (root / folder).rglob('*'):
        if path.suffix.lower() not in ('.cs', '.md', '.xml', '.py', '.csv'):
            continue
        value = path.read_text(encoding='utf-8')
        assert '\ufffd' not in value and not re.search(r'\?{3,}', value), str(path)
source_sizes = {p.name: len(p.read_text(encoding='utf-8').splitlines()) for p in (root / 'src').glob('*.cs')}
assert all(size <= 500 for size in source_sizes.values())
receipt = {
    'date': '2026-11-07',
    'status': 'local-candidate; original four routes accepted; bottle route pending in-game testing',
    'game_build': '26496118',
    'game_bundle_version': '0.46D',
    'mod_version': manifest.findtext('ModVersion'),
    'art_source': 'assets/source/printer-crate-original-style-two-spools-v4.png',
    'other_art_sources': ['assets/source/metal-crate-yellow-0.2.0.png',
                          'assets/source/electronic-crate-messy-0.2.0.png',
                          'assets/source/ore-crate-metallic-0.2.0.png'],
    'game_assembly_sha256': hashlib.sha256((args.game_root / 'Probably Stolen_Data/Managed/Assembly-CSharp.dll').read_bytes()).hexdigest() if args.game_root else None,
    'locales': {locale: len(table) for locale, table in tables.items()},
    'source_lines': source_sizes,
    'package_files': {str(p.relative_to(package)).replace('\\', '/'): hashlib.sha256(p.read_bytes()).hexdigest()
                      for p in sorted(package.rglob('*')) if p.is_file()},
    'runtime_sprite': {'width': 48, 'height': 48, 'mode': 'RGBA', 'inventory_cells': [3, 3]},
}
out = root / 'outputs/verification'
out.mkdir(parents=True, exist_ok=True)
(out / 'candidate-receipt.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('PASS manifest, package contents, transparent 48x48 PNG, UTF-8 integrity, source size boundaries')
print('Receipt:', out / 'candidate-receipt.json')

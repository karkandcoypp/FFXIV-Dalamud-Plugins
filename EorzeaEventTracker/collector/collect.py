#!/usr/bin/env python3
"""Conservative Lodestone collector: discovers topics, extracts explicit US Pacific event windows.
Claims of full reward extraction are deliberately avoided: image-only rewards need manual review.
"""
import argparse
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urljoin, urlparse
from zoneinfo import ZoneInfo

import requests
from bs4 import BeautifulSoup

BASE = 'https://na.finalfantasyxiv.com'
INDEX = BASE + '/lodestone/topics/'
PACIFIC = ZoneInfo('America/Los_Angeles')
UA = {'User-Agent': 'EorzeaEventTracker/0.1 (personal informational tool)'}
EVENT_TERMS = ('event', 'campaign', 'collaboration', "wake", 'rising', 'valentione',
               'starlight', 'hatching', 'moonfire', 'treasure trove', 'make it rain',
               'little ladies', 'heavensturn', 'fall guys', 'rhapsody', 'brick mountains')
# Official special-site layouts vary. We only trust explicit timestamp spans.
STAMP = re.compile(r'(?:(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday),?\s+)?'
                   r'(?:January|February|March|April|May|June|July|August|September|October|November|December)'
                   r'\s+\d{1,2},?\s+20\d{2}\s+at\s+\d{1,2}:\d{2}\s*(?:a\.m\.|p\.m\.|AM|PM)', re.I)


def parse_stamp(value: str, zone: str) -> str:
    value = re.sub(r'^(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday),?\s+', '', value, flags=re.I)
    value = re.sub(r'a\.m\.', 'AM', value, flags=re.I)
    value = re.sub(r'p\.m\.', 'PM', value, flags=re.I)
    value = re.sub(r'\s+', ' ', value).strip()
    for pattern in ('%B %d, %Y at %I:%M %p', '%B %d %Y at %I:%M %p'):
        try:
            dt = datetime.strptime(value, pattern).replace(tzinfo=ZoneInfo(zone))
            return dt.astimezone(timezone.utc).isoformat().replace('+00:00', 'Z')
        except ValueError:
            continue
    raise ValueError('Unparsed timestamp: ' + value)


def page(url, session=None):
    host = urlparse(url)
    if host.scheme != 'https' or host.hostname != 'na.finalfantasyxiv.com':
        raise ValueError('Only official NA Lodestone URLs are accepted')
    res = (session or requests).get(url, timeout=15, headers=UA, allow_redirects=False)
    if 300 <= res.status_code < 400:
        raise ValueError('Unexpected redirect from official page')
    res.raise_for_status()
    return BeautifulSoup(res.text, 'html.parser')


def extract(url, session=None):
    soup = page(url, session)
    for tag in soup(['script', 'style', 'noscript']): tag.decompose()
    meta = soup.find('meta', property='og:title')
    title = meta.get('content', '') if meta else ''
    if not title and soup.title:
        title = soup.title.get_text(' ', strip=True)
    if not title:
        title = next((h.get_text(' ', strip=True) for h in soup.find_all('h1')
                      if h.get_text(' ', strip=True) not in ('', 'Topics', 'News')), '')
    title = title.split(' | FINAL FANTASY XIV')[0].strip()
    if not title:
        return None
    text = soup.get_text(' ', strip=True)
    times = STAMP.findall(text)
    zone = 'America/Los_Angeles' if re.search(r'\bP[SD]T\b|Pacific', text) else None
    if len(times) < 2 or not zone:
        return None  # Do not invent dates for unstructured / timezone-ambiguous pages.
    start, end = parse_stamp(times[0], zone), parse_stamp(times[1], zone)
    if start >= end: return None
    ident = hashlib.sha256(url.encode()).hexdigest()[:16]
    requirements = []
    for match in re.finditer(r'\bLevel\s+\d{1,3}\b', text, re.I):
        val = match.group(0)
        if val not in requirements: requirements.append(val)
    return dict(id=ident, title=title[:160], url=url, startsAt=start, endsAt=end,
                description='', requirements=requirements[:6], steps=[], rewards=[],
                verified=False, notes=['Dates extracted automatically from official site.',
                                       'Reward names, NPC location and quest steps require review.'])


def discover(session=None):
    soup = page(INDEX, session)
    seen = set()
    for a in soup.select('a[href]'):
        href = urljoin(BASE, a['href']).split('?')[0].split('#')[0]
        title = a.get_text(' ', strip=True)
        if not (href.startswith(BASE + '/lodestone/topics/detail/') or
                href.startswith(BASE + '/lodestone/special/')): continue
        if any(k in (title + ' ' + href).lower() for k in EVENT_TERMS):
            if href not in seen:
                seen.add(href)
                yield href


def run(args):
    override_file = Path(args.overrides)
    overrides = json.loads(override_file.read_text(encoding='utf-8')) if override_file.exists() else {}
    pinned = [url for url in overrides if url.startswith(BASE + '/lodestone/')]
    urls = list(dict.fromkeys([*args.url, *pinned, *(discover() if not args.no_discover else [])]))
    events = []
    errors = []
    for url in urls[:args.limit]:
        try:
            e = extract(url)
            if not e:
                print('SKIP (uncertain dates):', url)
                continue
            manual = overrides.get(e['id']) or overrides.get(url) or {}
            for key in ('title', 'description', 'requirements', 'steps', 'rewards', 'notes', 'verified', 'tracking', 'imageUrl', 'translations'):
                if key in manual: e[key] = manual[key]
            events.append(e)
            print('OK:', e['title'])
        except Exception as exc:
            print('ERROR:', url, exc)
            errors.append(url)
    if errors:
        raise RuntimeError('Collection failed; keeping the previous feed unchanged')
    output = dict(schemaVersion=1, generatedAt=datetime.now(timezone.utc).isoformat(),
                  events=sorted(events, key=lambda e: e['startsAt']))
    target = Path(args.output)
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_suffix(target.suffix + '.tmp')
    temporary.write_text(json.dumps(output, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    temporary.replace(target)
    print(f'Wrote {len(events)} entries to {target}')


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--url', action='append', default=[], help='Official event URL (repeatable)')
    ap.add_argument('--no-discover', action='store_true')
    ap.add_argument('--limit', type=int, default=25)
    ap.add_argument('--overrides', default='config/overrides.json')
    ap.add_argument('--output', default='config/events.json')
    run(ap.parse_args())

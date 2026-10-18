#!/usr/bin/env python3
"""Conservative Lodestone collector: discovers topics, extracts explicit US Pacific event windows.
Exact reward/quest matches and simple shop exchanges are enriched from version-pinned XIVAPI data.
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

try:
    from .enrich import GameData, enrich
    from .translate import RussianTranslator
    from .npc import NpcLocator, retain_failed_locations
    from .routes import apply_map_routes
except ImportError:
    from enrich import GameData, enrich
    from translate import RussianTranslator
    from npc import NpcLocator, retain_failed_locations
    from routes import apply_map_routes

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


def special_links(soup, url, session=None):
    article = soup.select_one('.news__detail__wrapper') or soup.select_one('.news__detail') or soup
    targets = []
    for a in article.select('a[href]'):
        target = urljoin(url, a['href'])
        parsed = urlparse(target)
        if parsed.scheme != 'https' or parsed.username or parsed.port not in (None, 443):
            continue
        if parsed.hostname == 'sqex.to' and re.fullmatch(r'/[A-Za-z0-9_-]{1,64}', parsed.path):
            response = (session or requests).get(target, timeout=15, headers=UA, allow_redirects=False)
            if response.status_code not in (301, 302, 303, 307, 308):
                raise ValueError('Official short link unavailable')
            target = urljoin(target, response.headers.get('Location', ''))
        target = target.split('?')[0].split('#')[0]
        if target.startswith(BASE + '/lodestone/special/') and target not in targets:
            targets.append(target)
    return targets


def extract(url, session=None, game_data=None, follow=True):
    soup = page(url, session)
    detail_soup = None
    detail_issue = None
    detail_url = None
    if follow and '/topics/detail/' in url:
        try:
            specials = special_links(soup, url, session)
        except Exception:
            specials = []
            detail_issue = 'Special-site link unavailable.'
        if len(specials) == 1:
            linked = extract(specials[0], session, game_data, follow=False)
            if linked:
                return linked
            # Repeated collaborations keep current dates in the announcement and rewards on a timeless special site.
            detail_soup = page(specials[0], session)
            detail_url = specials[0]
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
    if len(times) != 2 or not zone:
        return None  # Do not invent dates for unstructured / timezone-ambiguous pages.
    start, end = parse_stamp(times[0], zone), parse_stamp(times[1], zone)
    if start >= end: return None
    ident = hashlib.sha256(url.encode()).hexdigest()[:16]
    requirements = []
    for match in re.finditer(r'\bLevel\s+\d{1,3}\b', text, re.I):
        val = match.group(0)
        if val not in requirements: requirements.append(val)
    event = dict(id=ident, title=title[:160], url=url, startsAt=start, endsAt=end,
                description='', requirements=requirements[:6], steps=[], rewards=[],
                verified=False, notes=['Dates extracted automatically from official site.',
                                       'Reward names, NPC location and quest steps require review.'])
    if game_data is not None:
        enrich(event, detail_soup if detail_soup is not None else soup, lambda target: page(target, session), game_data)
        if detail_url:
            event['automation']['sources'].append(detail_url)
        if detail_issue:
            event['notes'].append(detail_issue)
            event['automation']['issues'].append(detail_issue)
    return event


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
    target = Path(args.output)
    previous = {}
    if target.exists():
        try:
            previous = {e['id']: e for e in json.loads(target.read_text(encoding='utf-8'))['events']}
        except (ValueError, KeyError, TypeError):
            pass
    override_file = Path(args.overrides)
    overrides = json.loads(override_file.read_text(encoding='utf-8')) if override_file.exists() else {}
    pinned = [url for url in overrides if url.startswith(BASE + '/lodestone/')]
    discovered = []
    errors = []
    if not args.no_discover:
        try:
            discovered = list(discover())
        except Exception as exc:
            errors.append(dict(url=INDEX, reason=str(exc)))
    urls = list(dict.fromkeys([*args.url, *pinned, *discovered]))
    api = None if getattr(args, 'no_enrich', False) else GameData()
    translator = None if getattr(args, 'no_translate', False) else RussianTranslator(
        getattr(args, 'translation_cache', None) or target.with_name('translation-cache.json'))
    if translator:
        translator.seed(overrides.values())
    events = {}
    report = []
    for url in urls[:args.limit]:
        old = next((e for e in previous.values() if e.get('url') == url), None)
        try:
            reviewed = overrides.get(url, {}).get('tracking') is not None
            e = extract(url, game_data=None if reviewed else api, follow=not reviewed)
            if not e:
                raise ValueError('Uncertain dates; not publishing guessed event windows')
            old = previous.get(e['id'], old)
            automation = e.get('automation', {})
            if old and any(issue.startswith(('Reward source unavailable', 'Quest data unavailable', 'Shop data unavailable',
                                            'Special-site link unavailable', 'Game data unavailable'))
                           for issue in automation.get('issues', [])):
                # Preserve evidence on transient API/source failure instead of erasing known rewards.
                e = dict(old)
                note = 'Data source unavailable; retaining previously collected details.'
                e['notes'] = list(dict.fromkeys(e.get('notes', []) + [note]))[:200]
                e['verified'] = False
                e['automation'] = dict(automation, status='stale')
            retain_failed_locations(e, old)
            manual = overrides.get(e['id']) or overrides.get(e.get('url')) or overrides.get(url) or {}
            for key in ('title', 'description', 'requirements', 'steps', 'rewards', 'notes', 'verified', 'tracking', 'imageUrl', 'translations', 'locations'):
                if key in manual:
                    e[key] = manual[key]
            if manual.get('tracking') is not None:
                e['automation'] = dict(status='reviewed', sources=[e.get('url', url)], issues=[])
            if api:
                apply_map_routes(e, NpcLocator(api))
            if translator:
                translator.apply(e, old)
            events[e['id']] = e
            report.append(dict(url=url, eventId=e['id'], **e.get('automation', {'status': 'basic'})))
            print('OK:', e['title'], e.get('automation', {}).get('status', 'basic'))
        except Exception as exc:
            print('PARTIAL:', url, exc)
            errors.append(dict(url=url, reason=str(exc)))
            if old:
                events[old['id']] = old
    # A short discovery page or temporary source failure must not remove still-running events.
    now = datetime.now(timezone.utc)
    for ident, old in previous.items():
        try:
            if ident not in events and datetime.fromisoformat(old['endsAt'].replace('Z', '+00:00')) > now:
                events[ident] = old
                report.append(dict(eventId=ident, url=old.get('url'), status='retained'))
        except (KeyError, ValueError, TypeError):
            continue
    if not events and (errors or previous):
        raise RuntimeError('No usable event data; keeping the previous feed unchanged')
    output = dict(schemaVersion=1, generatedAt=now.isoformat(),
                  events=sorted(events.values(), key=lambda e: e['startsAt']))
    if len(output['events']) > 500:
        raise RuntimeError('Event limit exceeded; keeping the previous feed unchanged')
    if translator:
        translator.save()
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_suffix(target.suffix + '.tmp')
    temporary.write_text(json.dumps(output, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    temporary.replace(target)
    report_path = Path(getattr(args, 'report', None) or target.with_name('collection-report.json'))
    report_path.write_text(json.dumps(dict(generatedAt=now.isoformat(), events=report, errors=errors),
                                      indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print(f'Wrote {len(events)} entries to {target}; {len(errors)} source issue(s)')


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--url', action='append', default=[], help='Official event URL (repeatable)')
    ap.add_argument('--no-discover', action='store_true')
    ap.add_argument('--no-translate', action='store_true', help='Disable automatic Russian translation')
    ap.add_argument('--translation-cache', help='Persistent translation cache and quota state')
    ap.add_argument('--no-enrich', action='store_true', help='Disable game-data enrichment')
    ap.add_argument('--report', help='Collection diagnostics JSON path')
    ap.add_argument('--limit', type=int, default=25)
    ap.add_argument('--overrides', default='config/overrides.json')
    ap.add_argument('--output', default='config/events.json')
    run(ap.parse_args())

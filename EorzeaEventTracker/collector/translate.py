"""Cached, quota-bounded public en->ru translation. Reviewed event translations always win."""
import hashlib
from html import unescape
import json
from datetime import datetime, timezone
from pathlib import Path
import re
import time
import requests

ENDPOINT = 'https://api.mymemory.translated.net/get'
MACHINE_NOTE = 'Russian text includes automatic translation by MyMemory; original English reward names are retained in tooltips.'
MACHINE_RU = 'Русский текст включает машинный перевод MyMemory; исходные английские названия наград сохранены во всплывающих подсказках.'
PENDING_NOTE = 'Some Russian translations are pending; English text is shown until they are available.'
PENDING_RU = 'Часть русского перевода ещё не готова. До его получения показывается английский текст.'


def strings(event):
    # Prioritize the visible title, reward/quest names and map destinations over diagnostics.
    values = [event.get('title', '')]
    tracking = event.get('tracking', {})
    for q in tracking.get('quests', []):
        values.append(q.get('name', ''))
    for r in tracking.get('rewards', []):
        values.extend([r.get('name', ''), r.get('currency', '')])
    for loc in event.get('locations', []):
        values.extend([loc.get('npc', ''), loc.get('zone', '')])
    for key in ('rewards', 'requirements', 'steps'):
        values.extend(event.get(key, []))
    values.append(event.get('description', ''))
    values.extend(event.get('notes', []))
    return list(dict.fromkeys(s for s in values if isinstance(s, str) and s.strip()))


class RussianTranslator:
    def __init__(self, cache_path, session=None, run_limit=1200, daily_limit=4500):
        self.path = Path(cache_path)
        self.session = session or requests.Session()
        self.data = {'entries': {}, 'day': '', 'used': 0}
        if self.path.exists():
            try:
                cached = json.loads(self.path.read_text(encoding='utf-8'))
                if isinstance(cached.get('entries'), dict):
                    self.data = cached
            except (ValueError, OSError, AttributeError):
                pass
        today = datetime.now(timezone.utc).date().isoformat()
        if self.data.get('day') != today:
            self.data.update(day=today, used=0)
        self.run_limit, self.daily_limit = run_limit, daily_limit
        self.used = 0
        self.blocked = False
        self.new = 0

    @staticmethod
    def key(text):
        return hashlib.sha256(('en|ru:' + text).encode('utf-8')).hexdigest()

    @staticmethod
    def valid(source, translated):
        if not isinstance(translated, str):
            return False
        numbers = lambda text: sorted(re.findall(r'\d+', text))
        return (bool(re.search('[А-Яа-яЁё]', translated)) and '<' not in translated and '>' not in translated
                and not re.search(r'https?://|[\x00-\x08\x0b\x0c\x0e-\x1f]', translated)
                and len(translated) <= min(10000, max(200, len(source) * 6))
                and numbers(source) == numbers(translated)
                and sorted(re.findall(r'\{[^}]*\}', source)) == sorted(re.findall(r'\{[^}]*\}', translated)))

    def seed(self, events):
        # Conflicting manual translations must stay event-scoped, not become global glossary entries.
        candidates = {}
        for event in events:
            for source, ru in event.get('translations', {}).get('ru', {}).items():
                if self.valid(source, ru):
                    candidates.setdefault(source, set()).add(ru)
        for source, variants in candidates.items():
            if len(variants) == 1:
                self.data['entries'][self.key(source)] = dict(source=source, text=variants.pop(), provider='reviewed')
            elif self.data['entries'].get(self.key(source), {}).get('provider') == 'reviewed':
                self.data['entries'].pop(self.key(source), None)

    def translate(self, text):
        key = self.key(text)
        cached = self.data['entries'].get(key)
        if isinstance(cached, dict) and cached.get('source') == text and self.valid(text, cached.get('text')):
            return cached['text']
        if len(text.encode('utf-8')) > 450 and len(text) <= 10000 and not re.search(r'https?://', text):
            chunks, current = [], ''
            for word in text.split():
                if len(word.encode('utf-8')) > 400:
                    return None
                if len((current + ' ' + word).encode('utf-8')) > 400:
                    chunks.append(current)
                    current = word
                else:
                    current = (current + ' ' + word).strip()
            if current:
                chunks.append(current)
            parts = [self.translate(chunk) for chunk in chunks]
            if all(parts):
                value = ' '.join(parts)
                if self.valid(text, value):
                    self.data['entries'][key] = dict(source=text, text=value, provider='MyMemory', machine=True)
                    return value
            return None
        if (self.blocked or len(text.encode('utf-8')) > 450 or re.search(r'https?://', text)
                or not re.search('[A-Za-z]', text) or re.search(r'\{[^}]*\}', text)):
            return None
        if self.used + len(text) > self.run_limit or self.data['used'] + len(text) > self.daily_limit:
            return None
        self.used += len(text)
        self.data['used'] += len(text)
        try:
            time.sleep(.12)
            response = self.session.get(ENDPOINT, params={'q': text, 'langpair': 'en|ru', 'mt': 1},
                                        timeout=12, allow_redirects=False)
            if response.status_code != 200 or len(response.content) > 200000:
                self.blocked = True
                return None
            data = response.json()
            if data.get('responseStatus') != 200 or data.get('quotaFinished'):
                self.blocked = True
                return None
            # Do not accept a fuzzy translation-memory match for a different English sentence.
            candidates = [unescape(m.get('translation', '')).strip() for m in data.get('matches', [])
                          if unescape(m.get('segment', '')).strip() == text
                          and str(m.get('source', '')).lower().startswith('en')
                          and str(m.get('target', '')).lower().startswith('ru')]
            if not candidates and data.get('responseData', {}).get('match') == 1:
                candidates = [unescape(data['responseData'].get('translatedText', '')).strip()]
            result = next((value for value in candidates if self.valid(text, value)), None)
            if result:
                self.data['entries'][key] = dict(source=text, text=result, provider='MyMemory', machine=True)
                self.new += 1
                return result
        except (requests.RequestException, ValueError, KeyError, TypeError):
            self.blocked = True
        return None

    def apply(self, event, previous=None):
        ru = event.setdefault('translations', {}).setdefault('ru', {})
        old = (previous or {}).get('translations', {}).get('ru', {})
        missing = []
        translated = 0
        for text in strings(event)[:450]:
            if text in ru:
                continue
            if len(ru) >= 498:
                missing.append(text)
                continue
            value = old.get(text) if self.valid(text, old.get(text)) else self.translate(text)
            if value:
                ru[text] = value
                translated += 1
            elif re.search('[A-Za-z]', text):
                missing.append(text)
        if translated:
            event['notes'] = list(dict.fromkeys(event.get('notes', []) + [MACHINE_NOTE]))[:200]
            if len(ru) < 500:
                ru[MACHINE_NOTE] = MACHINE_RU
        if missing:
            event['notes'] = list(dict.fromkeys(event.get('notes', []) + [PENDING_NOTE]))[:200]
            if len(ru) < 500:
                ru[PENDING_NOTE] = PENDING_RU
        event.setdefault('automation', {})['russian'] = dict(status='partial' if missing else 'complete',
                translated=translated, pending=len(missing), provider='MyMemory + reviewed glossary')
        return event

    def save(self):
        self.path.parent.mkdir(parents=True, exist_ok=True)
        temp = self.path.with_suffix(self.path.suffix + '.tmp')
        temp.write_text(json.dumps(self.data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
        temp.replace(self.path)

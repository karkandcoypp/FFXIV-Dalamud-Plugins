"""Read-only, resumable wiki fact research. Full article text stays in the local cache."""
import argparse,concurrent.futures,json,re,time,urllib.request,urllib.parse
from pathlib import Path
API='https://ffxiv.consolegameswiki.com/mediawiki/api.php'
def get(params):
    url=API+'?'+urllib.parse.urlencode(params)
    for attempt in range(4):
        try:
            with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'BagSense item research/0.7 (read-only; 2 concurrent requests)'}),timeout=90) as response:
                result=json.load(response)
            if 'error' in result: raise ValueError(str(result['error']))
            return result
        except Exception:
            if attempt==3: raise
            time.sleep(2**attempt)
def collect(audit,cache):
    rows=json.loads(audit.read_text(encoding='utf-8'))['NeedsResearch'];cache.mkdir(parents=True,exist_ok=True)
    batches=[rows[i:i+40] for i in range(0,len(rows),40)]
    def batch(pair):
        index,items=pair;target=cache/f'batch-{index:03}.json'
        if target.exists(): return index
        result=get(dict(action='query',prop='revisions',titles='|'.join(i['Name'] for i in items),rvprop='ids|content',rvslots='main',redirects=1,format='json',formatversion=2))
        target.write_text(json.dumps(result,ensure_ascii=False),encoding='utf-8');time.sleep(.5);return index
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        for i in pool.map(batch,enumerate(batches)): print('cached batch',i,flush=True)
    pages=[]
    for target in sorted(cache.glob('batch-*.json')):
        pages.extend(json.loads(target.read_text(encoding='utf8'))['query']['pages'])
    (cache/'pages.json').write_text(json.dumps(pages,ensure_ascii=False),encoding='utf8')
    print('PAGES',len(pages),'EXIST',sum('missing' not in p for p in pages))
def semantic(cache):
    pages=json.loads((cache/'pages.json').read_text(encoding='utf8'))
    names=[]
    for page in pages:
        text=page.get('revisions',[{}])[0].get('slots',{}).get('main',{}).get('content','')
        if re.search(r'{{vendor table\b',text,re.I): names.append(page['title'])
    jobs=[('quests','[[Requires item::+]]','|?Requires item|?Has quest level|?Has quest giver|?Requires quest')]
    props=''.join('|?Sells item '+str(i) for i in range(1,5))+'|?Has vendor|?Has vendor location|?Has vendor coordinates'+''.join('|?Has item cost '+str(i) for i in range(1,5))+'|?Has item additional requirement|?Has vendor.Has vendor quest unlock|?Has vendor.Has vendor misc unlock|?Is historical'
    for i in range(0,len(names),1):
        query=' OR '.join('[[Has item cost '+str(n)+'::?;'+title+';?;?]]' for title in names[i:i+1] for n in range(1,5))
        jobs.append((f'vendors-{i:03}',query,props))
    def fetch(job):
        key,query,props=job;offset=0;all_rows={}
        while True:
            target=cache/f'{key}-{offset}.json'
            if target.exists(): result=json.loads(target.read_text(encoding='utf8'))
            else:
                result=get(dict(action='ask',query=query+props+'|limit=500|offset='+str(offset),format='json'))
                target.write_text(json.dumps(result,ensure_ascii=False),encoding='utf8');time.sleep(.5)
            rows=result['query']['results']
            if isinstance(rows,dict): all_rows.update(rows)
            next_offset=result.get('query-continue-offset')
            if next_offset is None or next_offset<=offset:break
            offset=next_offset
            if offset>20000:raise ValueError('Unexpected semantic result size')
        print(key,len(all_rows),flush=True)
        return key,all_rows
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool: results=dict(pool.map(fetch,jobs))
    (cache/'semantic.json').write_text(json.dumps(results,ensure_ascii=False),encoding='utf8')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--audit',type=Path,required=True);p.add_argument('--cache',type=Path,required=True);p.add_argument('--semantic',action='store_true');a=p.parse_args();semantic(a.cache) if a.semantic else collect(a.audit,a.cache)

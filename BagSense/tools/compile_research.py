"""Compile factual relationships only; article prose stays in the private research cache."""
import csv,json,re,collections,argparse,unicodedata,html
from pathlib import Path

def key(s):
    return ''.join(c for c in s.casefold().replace('’',"'") if unicodedata.category(c)!='Co').strip()
def field(text,name):
    m=re.search(r'^\|\s*'+re.escape(name)+r'\s*=\s*([^\n]*)',text,re.M|re.I);return m[1].strip() if m else ''
def compile(cache,source,output):
    items={int(r['#']):r for r in csv.DictReader((source/'Item.csv').open(encoding='utf-8-sig')) if r['Name']}
    byname=collections.defaultdict(list)
    for i,r in items.items():byname[key(r['Name'])].append(i)
    quests=list(csv.DictReader((source/'Quest.csv').open(encoding='utf-8-sig')))
    questnames=collections.defaultdict(list)
    for q in quests:
        if q['Name']:questnames[key(q['Name'])].append(int(q['#']))
    def itemid(name):
        found=byname.get(key(re.sub(r' \(item\)$', '', name, flags=re.I)),[]);return found[0] if len(found)==1 else 0
    def questid(name):
        found=questnames.get(key(name),[]);return found[0] if len(found)==1 else 0
    facts=collections.defaultdict(list);requirements=[];pages=json.loads((cache/'pages.json').read_text(encoding='utf8'))
    extras=cache.parent/(cache.name+'-extra')/'pages.json'
    profiles=json.loads(extras.read_text(encoding='utf8')) if extras.exists() else []
    profile_map={p['title']:p for p in profiles}
    targets=set(json.loads((output.parent/'research-targets-v6.json').read_text(encoding='utf8')))
    revision_map={p['title']:p.get('revisions',[{}])[0].get('revid',0) for p in pages+profiles}
    def add(i,kind,context,page,**extra):
        if i not in items or not context:return
        d=dict(Kind=kind,Context=context,WikiTitle=page['title'],Revision=revision_map.get(page['title'],0));d.update(extra)
        d['Requirements']=[html.unescape(re.sub(r'<[^>]*>','',str(v.get('fulltext','') if isinstance(v,dict) else v))).strip() for v in d.get('Requirements',[])]
        if d.get('Npcs'):
            p=profile_map.get(d['Npcs'][0],{})
            raw=p.get('revisions',[{}])[0].get('slots',{}).get('main',{}).get('content','')
            xy=re.fullmatch(r'\s*([0-9.]+)\s*,\s*([0-9.]+)\s*',field(raw,'coordinates'))
            if xy and 1 <= float(xy[1]) < 100 and 1 <= float(xy[2]) < 100:
                d.update(MapX=float(xy[1]),MapY=float(xy[2]))
                if not d.get('Zone'):d['Zone']=html.unescape(field(raw,'location'))
        if d not in facts[i]: facts[i].append(d)
    for page in pages:
        text=page.get('revisions',[{}])[0].get('slots',{}).get('main',{}).get('content','');gt=field(text,'id-gt')
        if not gt.isdigit():continue
        i=int(gt)
        if i not in items or key(items[i]['Name'])!=key(page['title'] if field(text,'name').upper()=='{{PAGENAME}}' else field(text,'name') or page['title']):continue
        typ=field(text,'type');material=field(text,'material-type')
        if field(text,'island-sanctuary').lower() in ('y','true','yes'):
            add(i,'IslandUse',typ or 'Island Sanctuary',page,Quests=[questid('Seeking Sanctuary')] if questid('Seeking Sanctuary') else [])
        if field(text,'cosmic-exploration').lower() in ('y','true','yes'):
            add(i,'CosmicMaterial',typ or 'Cosmic Exploration',page,Quests=[questid('A Cosmic Homecoming')] if questid('A Cosmic Homecoming') else [])
        if typ.lower()=='currency' or material.lower()=='currency':add(i,'CurrencyUse',items[i]['Name'],page)
        if material.lower().startswith(('airship','submersible')):add(i,'CompanyPart',material,page,Requirements=['Free Company workshop access and voyage permissions'])
        if 'fully primed aetherial wheel' in field(text,'description').lower():
            action=re.search(r'company action (.+?)(?:\s*\(|[.])',field(text,'description'),re.I)
            add(i,'CompanyAction',action[1] if action else 'Free Company action',page,Requirements=['Aetherial Wheel Stand and Free Company permissions'])
        if field(text,'desynthesizable').lower() in ('y','true','yes'):
            reward_file=cache/'desynthesis-010000.json';desynth=json.loads(reward_file.read_text(encoding='utf8')).get('Sources',{}).get(str(i),{}) if reward_file.exists() else {}
            add(i,'Desynthesis',field(text,'desynth-class') or 'Desynthesis',page,Results=[r['Id'] for r in desynth.get('Rewards',[]) if r['Id'] in items])
        split=re.split(r'==\s*Used [Ff]or\s*==',text,maxsplit=1);used=split[1] if len(split)>1 else ''
        if re.search(r'{{\s*(retired|obsolete)\b',text,re.I):
            add(i,'Historical','Historical / retired item',page,Requirements=['Retired / obsolete; see the referenced revision'])
        for mission in re.findall(r'^\|\s*mission\s*=\s*([^\n]+)',text,re.M):
            if mission.strip():add(i,'WikiMission',mission.strip(),page,Quests=[questid('A Cosmic Homecoming')] if questid('A Cosmic Homecoming') else [])
        results=list(dict.fromkeys(itemid(n.strip()) for n in re.findall(r'{{\s*contains\s*\|([^{}|]+)',text,re.I)))
        results=[r for r in results if r]
        if results:
            appraisal=re.search(r'(?:take these items to|take them to)\s*\[\[([^]|]+)[^]]*\]\]\s+in\s+\[\[([^]|]+)',text,re.I)
            extra={}
            if appraisal:
                extra.update(Npcs=[appraisal[1]],Zone=appraisal[2])
                coordinates=re.search(r'{{coords\|\s*([0-9.]+)\s*,\s*([0-9.]+)',text[appraisal.end():],re.I)
                if coordinates:extra.update(MapX=float(coordinates[1]),MapY=float(coordinates[2]))
            if items[i]['Name'].endswith('Logogram'):
                extra.update(Npcs=['Drake'],Zone='The Forbidden Land, Eureka Pyros')
            add(i,'LogogramAppraisal' if items[i]['Name'].endswith('Logogram') else 'Container','Possible contents',page,Results=results,**extra)
        if items[i]['Name'].startswith('Forgotten Fragment of '):
            possible=[itemid(n.strip()) for n in re.findall(r'{{\s*item icon\s*\|([^{}|]+)',used,re.I)]
            possible=list(dict.fromkeys(r for r in possible if r))
            rank=re.search(r'Resistance rank required for appraisal:\s*(\d+)',text,re.I)
            add(i,'FragmentAppraisal','Lost Actions',page,Npcs=['Resistance Appraiser'],Zone='Gangos',MapX=5.4,MapY=5.4,
                Results=possible,Requirements=['Resistance rank ≥ '+rank[1]] if rank else [])
        for guide in re.findall(r'{{\s*(?:main|see also)\s*\|([^{}|]+)',used,re.I):
            if not any(s in guide.lower() for s in ['item','recipe']):add(i,'ContentUse',guide.strip(),page)
        # Explicit manual exchange table rows. Never confuse NPC with a quest giver.
        for row in used.split('|-'):
            cols=row.split('||')
            if len(cols)<5:continue
            rewards=[itemid(n.strip()) for n in re.findall(r'{{\s*item icon\s*\|([^{}|]+)',cols[0],re.I)]
            rewards=[r for r in rewards if r]
            npcs=re.findall(r'\[\[([^\]|]+)',cols[1]);zones=re.findall(r'\[\[([^\]|]+)',cols[2])
            costs=[];unresolved=False
            for title,count in re.findall(r'\[\[File:[^\]]*?\blink=([^\]]+)\]\]\s*(\d+)',cols[-1],re.I):
                n=itemid(title.strip());unresolved|=not n
                costs.append(dict(ItemId=n,Name=title.strip(),Count=int(count)))
            if rewards and costs and npcs and not unresolved and any(c['ItemId']==i for c in costs):
                add(i,'RelicExchange' if npcs[0] in ('Jalzahn','Gerolt') else 'WikiExchange',' / '.join(items[r]['Name'] for r in rewards),page,
                    Npcs=npcs[:1],Zone=zones[0] if zones else '',Costs=costs,Results=rewards,Repeatable=True)
        # Genuine Used For quest sections only; links elsewhere are not treated as hand-ins.
        for qname in re.findall(r'{{\s*quest list row\s*\|([^{}|]+)',used,re.I):
            qid=questid(qname)
            if qid:add(i,'QuestReference',qname.strip(),page,Quests=[qid])
        # Explicit system links in a Used For section are factual context, not inferred quantity.
        if used and not facts[i]:
            systems=[n for n in re.findall(r'\[\[([^\]|]+)',used) if not n.startswith(('File:','Category:')) and not itemid(n)]
            systems += [n for n in re.findall(r'{{(?:i|action icon)\|([^{}|]+)',used,re.I) if not itemid(n)]
            if systems:add(i,'ContentUse',systems[0],page)
    # FilterGroup is a typed game discriminator, including virtual special-storage entries.
    system_groups={37:('LogosMneme','Logos Actions'),38:('ProgressPoints','The Bozjan Southern Front'),
        39:('LostAction','Lost Actions'),43:('Placeholder','Items'),52:('ProgressPoints','Occult Crescent'),
        53:('ProgressPoints','Phantom Job'),54:('ProgressPoints','Occult Crescent'),55:('ProgressPoints','Cosmic Exploration'),
        56:('ProgressPoints','Cosmic Exploration'),59:('ProgressPoints','Cosmic Exploration'),
        48:('IslandUse','Island Sanctuary'),50:('CosmicMaterial','Cosmic Exploration')}
    for i in targets:
        row=items.get(i)
        if not row:continue
        group=int(row['FilterGroup'])
        if not facts[i] and 'it is now utterly useless' in row['Description']:
            add(i,'Historical',row['Name'],dict(title=row['Name']),Evidence='Game description: explicitly obsolete',Requirements=['Game description explicitly marks obsolete'])
        if not facts[i] and 'Used to change the class of a squadron member' in row['Description']:
            add(i,'SquadronClass',row['Name'],dict(title='Adventurer Squadrons'),Evidence='Game description: squadron class change')
        if not facts[i] and ('Documentation of noteworthy accomplishments for Season' in row['Description']):
            add(i,'PvpSeason',row['Name'],dict(title='The Feast' if 'Feast' in row['Description'] else 'Crystalline Conflict'),Evidence='Game description: seasonal accomplishment')
        if not facts[i] and 'can be traded with Orbitingway' in row['Description']:
            add(i,'LotteryTicket',row['Name'],dict(title='Cosmic Exploration'),Evidence='Game description: Orbitingway lottery ticket',Npcs=['Orbitingway'],Zone='Auxesia')
        if group in system_groups and not facts[i]:
            kind,guide=system_groups[group]
            add(i,kind,row['Name'],dict(title=guide),Evidence='Game FilterGroup '+str(group))
    semantic=json.loads((cache/'semantic.json').read_text(encoding='utf8'))
    def values(record,name):return record.get(name,{}).get('item',[])
    for title,row in semantic.get('quests',{}).items():
        qid=questid(title)
        if not qid:continue
        profile=profile_map.get(title,{})
        raw=profile.get('revisions',[{}])[0].get('slots',{}).get('main',{}).get('content','')
        if field(raw,'id-gt') != str(qid):continue
        for req in row['printouts'].get('Requires item',[]):
            names=values(req,'Has required item name');qty=values(req,'Has required item quantity');hq=values(req,'Has required item hq')
            if len(names)!=1 or len(qty)!=1 or not isinstance(qty[0],(int,float)) or qty[0]!=int(qty[0]) or qty[0]<=0:continue
            i=itemid(names[0]['fulltext'])
            if not i:continue
            requirements.append(dict(ItemId=i,QuestId=qid,Count=int(qty[0]),Quality='hq' if hq==['t'] else 'any' if hq==['f'] else 'unknown',WikiTitle=title, ItemName=items[i]['Name'], QuestName=next(q['Name'] for q in quests if int(q['#'])==qid)))
            add(i,'QuestReference',title,dict(title=title),Quests=[qid],RequiredCount=int(qty[0]),Quality='hq' if hq==['t'] else 'any' if hq==['f'] else 'unknown')
    for group,rows in semantic.items():
        if group=='quests':continue
        for title,row in rows.items():
            props=row['printouts'];npcs=[p['fulltext'] for p in props.get('Has vendor',[])];zones=[p['fulltext'] for p in props.get('Has vendor location',[])]
            if not npcs:continue
            costs=[];results=[];unknown=False
            for n in range(1,5):
                for cost in props.get('Has item cost '+str(n),[]):
                    names=values(cost,'Has item currency');qty=values(cost,'Has item value')
                    if len(names)!=1 or len(qty)!=1 or qty[0]<=0:unknown=True;continue
                    i=itemid(names[0]);unknown|=not i
                    costs.append(dict(ItemId=i,Name=names[0],Count=int(qty[0]),Hq=values(cost,'Is item HQ')==['t'],Collectability=int((values(cost,'Has item collectability') or [0])[0])))
                for reward in props.get('Sells item '+str(n),[]):
                    for r in values(reward,'Sells item'):
                        rid=itemid(r['fulltext'])
                        if rid:results.append(rid)
            if not costs or not results:continue
            prereqs=[p['fulltext'] for p in props.get('Has vendor quest unlock',[])]
            qs=[questid(q) for q in prereqs if questid(q)]
            reqs=props.get('Has item additional requirement',[])+props.get('Has vendor misc unlock',[])
            if unknown:reqs.append('Unresolved additional cost; verify complete price with NPC')
            for cost in costs:
                if not cost['ItemId']:continue
                add(cost['ItemId'],'RelicExchange' if npcs[0].split(' (')[0] in ('Gerolt','Jalzahn','Ardashir','Zlatan','House Manderville Artisan','Dodokkuli') else 'WikiExchange',' / '.join(items[r]['Name'] for r in results),dict(title=title.split('#')[0]),
                    Quests=qs,Npcs=npcs,Zone=zones[0] if zones else '',Costs=costs,Results=results,Requirements=reqs,Repeatable=True)
    gathered=cache/'gather-missions.json'
    if gathered.exists():
        # Match the item page title to its validated game ID, including wiki disambiguation.
        page_ids={p['title']:int(field(p.get('revisions',[{}])[0].get('slots',{}).get('main',{}).get('content',''),'id-gt')) for p in pages if field(p.get('revisions',[{}])[0].get('slots',{}).get('main',{}).get('content',''),'id-gt').isdigit()}
        for title,row in json.loads(gathered.read_text(encoding='utf8')).items():
            props=row['printouts'];name=(props.get('Has canonical name') or [title])[0]
            for item in props.get('Has mission gathered items',[]):
                i=page_ids.get(item['fulltext']) or itemid(item['fulltext'])
                if i in items and i in targets:
                    add(i,'MissionGather',name,dict(title=title),Quests=[questid('A Cosmic Homecoming')] if questid('A Cosmic Homecoming') else [])
    doc=dict(Schema=1,Research=[dict(ItemId=i,ItemName=items[i]['Name'],Uses=u) for i,u in sorted(facts.items()) if i in targets and u],QuestRequirements=requirements,
             Provenance='FFXIV Console Games Wiki; factual names, quantities and relationships only; article revision IDs retained where available')
    output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(doc,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf8')
    print('ITEMS',len(facts),'USES',sum(map(len,facts.values())),'QUEST COUNTS',len(requirements));print(collections.Counter(u['Kind'] for uses in facts.values() for u in uses))
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--cache',type=Path,required=True);p.add_argument('--source',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();compile(a.cache,a.source,a.output)

#!/usr/bin/env python3
"""GuideVault read-only catalog-driven OCR discovery. No AI inference or metadata updates."""
import argparse, json, os, re, sys, time, tempfile, unicodedata, statistics
from collections import Counter, defaultdict
from pathlib import Path
REV = 'content-intelligence-v5.1-region-context'
DATA = Path(os.environ.get("GUIDEVAULT_DATA", "/data"))
def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))

def save(p, value):
    tmp=p.with_name(p.name+'.tmp')
    tmp.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    os.replace(tmp,p)

def listing(root):
    out=[]
    for p in root.glob('*/*/manifest.json'):
        try:
            m=read(p); d=p.parent
            if m.get('revision')!='v8.0-phase1' or m.get('fingerprint')!=d.name: continue
            report=d/'catalog-report.json'; fields={}
            if report.exists():
                try: fields={f['field']:f.get('current') for f in read(report).get('publication',{}).get('fields',[])}
                except (ValueError,KeyError,TypeError): pass
            out.append({'key':d.parent.name+'/'+d.name,'directory':d,'total':m['totalPages'],'label':' | '.join(str(fields.get(k) or '?') for k in ('magazineTitle','issueNumber','coverDate')),'mtime':(report if report.exists() else p).stat().st_mtime})
        except (OSError,ValueError,KeyError,TypeError): continue
    return sorted(out,key=lambda x:x['mtime'],reverse=True)

def load_pages(directory):
    m=read(directory/'manifest.json'); count=m['totalPages']
    if not isinstance(count,int) or not 1<=count<=10000: raise ValueError('Invalid manifest count')
    pages=[]
    for n in range(1,count+1):
        p=read(directory/f'page-{n:06d}.json')
        if p.get('page')!=n or p.get('revision')!=m['revision'] or not isinstance(p.get('text'),str): raise ValueError(f'Invalid page index: {n}')
        # Include layout lines as separate coordinate evidence; not old interpretation results.
        lines=[[l.get('text',''),l.get('x'),l.get('y'),l.get('width'),l.get('height')] for l in p.get('lines',[]) if l.get('text')]
        v=directory/('cover-vision-phase45.json' if n==1 else f'contents-vision-phase47-{n:06d}.json')
        vision=[]
        if v.exists():
            obj=read(v); vision=[[l.get('text',''),l.get('x'),l.get('y'),l.get('width'),l.get('height')] for l in obj.get('lines',[]) if l.get('text')]
        pages.append({'page':n,'source':p.get('source'),'warning':p.get('warning'),'text':p['text'],'layout_size':[p.get('width'),p.get('height'),p.get('units')],'layout_lines':lines,'cached_vision_lines':vision})
    return m,pages


# Normalized token matching, with offsets into original text for review evidence.
WORD = re.compile(r"[^\W_]+(?:['’][^\W_]+)*", re.UNICODE)
NUMERAL = re.compile(r'(?:\d+|i|ii|iii|iv|v|vi|vii|viii|ix|x|xi|xii|xiii|xiv|xv|xvi|xvii|xviii|xix|xx)$', re.I)

def tokens(text):
    return [unicodedata.normalize('NFKC', m.group()).replace('’', "'").casefold() for m in WORD.finditer(text)]

def catalog_rows(obj):
    # Accept only a recognizable game collection, not the relationship list.
    def rows(value):
        if isinstance(value, list): return value
        if isinstance(value, dict): return list(value.values())
        return []
    def valid(value):
        rr=rows(value)
        return rr if rr and all(isinstance(r,dict) and isinstance(r.get('title'),str) and r.get('id') and isinstance(r.get('platform'),str) for r in rr) else None
    if isinstance(obj,list):
        rr=valid(obj)
        if rr: return rr
    found=[]
    def walk(node):
        if not isinstance(node,dict): return
        for k,v in node.items():
            if k.casefold()=='games':
                rr=valid(v)
                if rr: found.append(rr)
            elif isinstance(v,dict): walk(v)
    walk(obj)
    if len(found)!=1: raise ValueError('Could not identify one game collection with id/title/platform fields. No files changed; inspect catalog schema.')
    return found[0]

def build_trie(rows,deadline):
    trie={}; unique=set()
    for r in rows:
        if time.monotonic()>=deadline: raise TimeoutError('Budget expired while building title index')
        title=r['title'].strip(); key=tuple(tokens(title))
        if not key: continue
        unique.add(key); node=trie
        for t in key: node=node.setdefault(t,{})
        node.setdefault(None,[]).append(game_info(r))
    return trie,len(unique)

def find_matches(text,trie,deadline):
    ms=list(WORD.finditer(text)); ts=[unicodedata.normalize('NFKC',m.group()).replace('’',"'").casefold() for m in ms]
    i=0
    while i<len(ts):
        if i%64==0 and time.monotonic()>=deadline: raise TimeoutError('Magazine budget expired during page scan')
        node=trie; j=i; best=None
        while j<len(ts) and ts[j] in node:
            node=node[ts[j]]; j+=1
            if None in node: best=(j,node[None])
        if best is None: i+=1; continue
        end, games=best
        # Never reduce a numbered sequel to its base title, even if the sequel is absent from the catalog.
        if end<len(ts) and NUMERAL.fullmatch(ts[end]) and not re.search(r'[.!?\n]',text[ms[end-1].end():ms[end].start()]):
            i=end+1; continue
        a,b=ms[i].start(),ms[end-1].end()
        left=max(0,a-100); right=min(len(text),b+140)
        flags=['unverified_coverage']
        if end-i==1: flags.append('single_word_title')
        if len({g['platform'] for g in games})>1: flags.append('multiple_catalog_platforms')
        if len({g['id'] for g in games})>1: flags.append('multiple_catalog_ids')
        yield {'key':' '.join(ts[i:end]),'printed_title':text[a:b],'quote':text[left:right], 'games':games,'flags':flags}
        i=end

def scan_page(p,trie,deadline):
    sources=[('ocr',p['text'])]
    for field in ('layout_lines','cached_vision_lines'):
        sources.extend((field,row[0]) for row in p[field] if isinstance(row[0],str))
    seen={}
    for name,text in sources:
        if time.monotonic()>=deadline: raise TimeoutError('Magazine budget expired during page scan')
        for hit in find_matches(text,trie,deadline):
            key=hit['key']; evidence={'page':p['page'],'source':name,'printed_title':hit['printed_title'],'quote':hit['quote']}
            if key not in seen:
                seen[key]={'page':p['page'],'title_key':key,'catalog_candidates':hit['games'],'flags':hit['flags'],'evidence':[],'status':'unverified_mention','kind':None}
            ev=seen[key]['evidence']
            if len(ev)<3 and not any(e['quote']==evidence['quote'] for e in ev): ev.append(evidence)
    return list(seen.values())

def refine(hits, pages, deadline):
    """Rank candidates, never silently delete or promote to confirmed records."""
    from collections import Counter
    bypage={p['page']:p for p in pages}
    contents=set()
    for p in pages:
        if time.monotonic()>=deadline: raise TimeoutError('Budget expired during refinement')
        lines=[r[0] for r in p['layout_lines']+p['cached_vision_lines'] if isinstance(r[0],str)]
        if any(tokens(x)==['contents'] or tokens(x)==['table','of','contents'] for x in lines): contents.add(p['page'])
    def line_match(key,line):
        ts=tokens(line); k=key.split()
        if ts==k: return True
        # A title with a printed contents-page locator, or parenthetical platform.
        tail=ts[len(k):]
        return ts[:len(k)]==k and bool(tail) and (all(t.isdecimal() for t in tail) and bool(re.search(r'\.{2,}|\s{2,}',line)) or tail in (['nintendo'],['nes'],['genesis'],['game','boy'],['sega'],['master','system']))
    common={'up','one','two','three','max','vs','it','go','on','in','a','i','the','and','of','to','no','yes'}
    ad_patterns=[r'\bcontest entry form\b',r'\border back issues\b',r'\bcredit card\b',r'\bdealer[’\']?s?\b',r'\ball rights reserved\b',r'\btrademarks? of\b',r'\bphone\s*\(?\d{3}',r'\bmail order\b',r'\bcall now\b',r'\bgrand prize\b',r'\b(?:inc|corp)\.\s*\d{2,}']
    tip=re.compile(r'\b(passwords?|sound test|level select|secret room|unlimited|extra lives|hidden|walkthrough|strategy|tips|codes|map[s]?|continue[s]?)\b',re.I)
    pageinfo={}
    for p in pages:
        txt=p['text']; ad=[s for s in ad_patterns if re.search(s,txt,re.I)]
        pageinfo[p['page']]={'advertising_signals':len(ad),'contents_page':p['page'] in contents}
    toc_keys={x['title_key'] for x in hits if x['page'] in contents and any(line_match(x['title_key'],r[0]) for r in bypage[x['page']]['layout_lines']+bypage[x['page']]['cached_vision_lines'] if isinstance(r[0],str))}
    tiers=Counter(); reviewed=[]
    for hit in hits:
        if time.monotonic()>=deadline: raise TimeoutError('Budget expired during refinement')
        p=bypage[hit['page']]; key=hit['title_key']; single=len(key.split())==1
        rows=p['layout_lines']+p['cached_vision_lines']
        heading=[r for r in rows if isinstance(r[0],str) and any(line_match(k,r[0]) for k in [key]+hit.get('matched_alias_keys',[]))]
        # Retain all matching lines including coordinates for manual review.
        local=[]
        for r in heading:
            if len(r)<5 or not all(isinstance(z,(int,float)) for z in r[1:5]): continue
            x,y,w,h=r[1:5]
            for other in rows:
                if len(other)<5 or not isinstance(other[0],str) or not all(isinstance(z,(int,float)) for z in other[1:5]): continue
                xx,yy,ww,hh=other[1:5]
                if yy>=y+h*0.6 and yy<=y+max(h*6,100) and abs(xx-x)<=max(w*.5,80) and tip.search(other[0]):
                    if other[0] not in local: local.append(other[0])
        signals=[]
        if heading: signals.append('standalone_title_line')
        if key in toc_keys: signals.append('listed_in_contents')
        if local: signals.append('nearby_gameplay_heading')
        if pageinfo[hit['page']]['advertising_signals']: signals.append('possible_advertising_page')
        if single: signals.append('single_word_title')
        if len(key.replace(' ',''))<=2 or key in common: signals.append('high_collision_title')
        if hit['page'] in contents:
            tier='contents_lead' if heading else 'low_priority'
        elif 'high_collision_title' in signals:
            tier='review' if heading and (key in toc_keys or local) else 'low_priority'
        elif pageinfo[hit['page']]['advertising_signals']>=2:
            tier='possible_advertisement'
        elif heading and (local or key in toc_keys): tier='priority_review'
        elif heading or (not single and key in toc_keys): tier='review'
        elif not single: tier='unverified_mention'
        else: tier='low_priority'
        item=dict(hit, review_tier=tier, review_signals=signals, heading_evidence=heading, nearby_gameplay_text=local[:8]); reviewed.append(item); tiers[tier]+=1
    order={'priority_review':0,'review':1,'contents_lead':2,'unverified_mention':3,'possible_advertisement':4,'low_priority':5}
    reviewed.sort(key=lambda x:(order[x['review_tier']],x['page'],x['title_key']))
    # Contents entries without exact catalog matches are leads, not inferred titles.
    toc_lines=[{'page':p['page'],'lines':p['layout_lines']+p['cached_vision_lines']} for p in pages if p['page'] in contents]
    return reviewed,dict(tiers),toc_lines


# Lower bounds only: deliberately use conservative platform-era years, not game dates.
# Unknown dates/platforms stay eligible. A future game release alone is a warning
# because previews can precede release; chronology is not a deletion rule.
PLATFORM_MIN = {
    'sony playstation':1994, 'sony playstation 2':2000, 'sony playstation 3':2006,
    'sony playstation 4':2013, 'sony playstation 5':2020, 'sony psp':2004,
    'sony playstation vita':2011, 'nintendo ds':2004, 'nintendo 3ds':2011,
    'nintendo wii':2006, 'nintendo wii u':2012, 'nintendo switch':2017,
    'nintendo game boy advance':2001, 'nintendo game boy color':1998,
    'nintendo gamecube':2001, 'nintendo 64':1996, 'sega dreamcast':1998,
    'sega saturn':1994, 'sega 32x':1994, 'sega cd':1991,
    'microsoft xbox':2001, 'microsoft xbox 360':2005, 'microsoft xbox one':2013,
    'atari jaguar':1993, '3do interactive multiplayer':1993,
}

def game_info(r):
    g={k:r[k] for k in ('id','title','platform')}
    for k in ('releaseDate','releaseYear','year','ReleaseDate','ReleaseYear','Year'):
        v=r.get(k)
        if isinstance(v,(str,int)) and not isinstance(v,bool):
            m=re.search(r'\b(?:19[3-9]\d|20\d{2})\b',str(v))
            if m: g['release_year']=int(m.group()); g['release_year_field']=k; break
    return g

def issue_year(label):
    # Only use an explicit four-digit year in the cover-date field.
    date=label.split(' | ')[-1] if ' | ' in label else ''
    m=re.findall(r'\b(?:19\d{2}|20\d{2})\b',date)
    return int(m[0]) if len(set(m))==1 else None

def chronology(g,year):
    if year is None: return 'unknown_issue_date'
    floor=PLATFORM_MIN.get(' '.join(tokens(g['platform'])))
    release=g.get('release_year')
    if floor and floor>year+1: return 'platform_later_era'
    if isinstance(release,int) and release>year+1: return 'release_later_than_issue'
    if isinstance(release,int) and release>year: return 'possible_preview'
    return 'not_excluded' if release or floor else 'unknown_game_date'

def build_aliases(rows,deadline):
    # Explicit alternate titles and colon-subtitle abbreviations only. Never
    # infer arbitrary word prefixes or remove a numbered sequel.
    exact=defaultdict(set); aliases=defaultdict(dict)
    for r in rows:
        exact[' '.join(tokens(r['title']))].add(r['title'])
    for r in rows:
        if time.monotonic()>=deadline: raise TimeoutError('Budget expired building aliases')
        title=r['title']; full=' '.join(tokens(title)); variants=[]
        if ':' in title: variants.append((title.split(':',1)[0],'subtitle_abbreviation'))
        for field in ('alternateTitles','alternateNames','aliases','alternativeTitles'):
            val=r.get(field,[])
            if isinstance(val,str): val=[val]
            if isinstance(val,list):
                for v in val:
                    if isinstance(v,dict): v=v.get('title') or v.get('name')
                    if isinstance(v,str): variants.append((v,'catalog_alias'))
        for raw,kind in variants:
            key=' '.join(tokens(raw)); kk=key.split(); ff=full.split()
            if not key or key==full or len(key.replace(' ',''))<4 or key in exact: continue
            if any(NUMERAL.fullmatch(x) for x in ff if x not in kk): continue
            aliases[key][str(r['id'])]=(game_info(r),kind)
    # Ambiguous aliases across canonical titles are never auto-selected.
    return {k:list(v.values()) for k,v in aliases.items() if len({' '.join(tokens(g['title'])) for g,_ in v.values()})==1}

ROMAN_NUMBERS = dict(zip('ii iii iv v vi vii viii ix x xi xii xiii xiv xv xvi xvii xviii xix xx'.split(), range(2,21)))
def numeric_key(value):
    ts=tokens(value)
    # Only terminal sequel numerals, with a substantive title before them.
    if len(ts)>=3 and ts[-1] in ROMAN_NUMBERS:
        ts[-1]=str(ROMAN_NUMBERS[ts[-1]])
    return ' '.join(ts)

def numeral_index(rows,deadline):
    index=defaultdict(dict)
    for r in rows:
        if time.monotonic()>=deadline: raise TimeoutError('Budget expired building numeral index')
        k=numeric_key(r['title']); ts=k.split()
        if len(ts)>=3 and ts[-1].isdigit() and 2<=int(ts[-1])<=20:
            index[k][str(r['id'])]=game_info(r)
    return index

def numeral_hits(p,index,deadline):
    result={}
    for field in ('layout_lines','cached_vision_lines'):
        for row in p[field]:
            if time.monotonic()>=deadline: raise TimeoutError('Budget expired matching sequel headings')
            raw=row[0]; clean=re.sub(r'\.{2,}\s*\d+(?:\s*[-,–]\s*\d+)*\s*$','',raw)
            k=numeric_key(clean)
            for g in index.get(k,{}).values():
                canonical=' '.join(tokens(g['title'])); alias=' '.join(tokens(clean))
                if canonical==alias: continue
                h=result.setdefault(canonical,{'page':p['page'],'title_key':canonical,'catalog_candidates':[],'flags':['unverified_coverage','numeral_equivalence'],'evidence':[],'status':'unverified_mention','kind':None,'matched_alias_keys':[]})
                if not any(z['id']==g['id'] for z in h['catalog_candidates']): h['catalog_candidates'].append(g)
                if alias not in h['matched_alias_keys']: h['matched_alias_keys'].append(alias)
                ev={'page':p['page'],'source':field,'printed_title':clean,'quote':raw,'match_method':'terminal_numeral_equivalence'}
                if ev not in h['evidence']: h['evidence'].append(ev)
    return list(result.values())

def strong_promotion(page):
    text=page['text']
    # Strong commercial signals; copyright/trademark alone is not sufficient.
    patterns=[r'\border back issues\b',r'\bcredit card\b',r'\bpostage and handling\b',r'\bcheck or money order\b',r'\bcontest entry form\b',r'\bgrand prize\b',r'\bsubscription(?:s| form)?\b',r'\bsubscribe now\b',r'\b(?:name|city)\s+(?:address|state)\b']
    signals=[pat for pat in patterns if re.search(pat,text,re.I)]
    return signals if len(signals)>=2 else []

def alias_hits(p,aliases,deadline):
    hits={}
    for field in ('layout_lines','cached_vision_lines'):
        for r in p[field]:
            if time.monotonic()>=deadline: raise TimeoutError('Budget expired matching aliases')
            raw=r[0]; key=' '.join(tokens(raw))
            if key not in aliases: continue
            options=aliases[key]; games=[g for g,_ in options]
            canonical=' '.join(tokens(games[0]['title']))
            h=hits.setdefault(canonical,{'page':p['page'],'title_key':canonical,'catalog_candidates':games,'flags':['unverified_coverage','alternate_title_match'],'evidence':[],'status':'unverified_mention','kind':None,'matched_alias_keys':[]})
            if key not in h['matched_alias_keys']: h['matched_alias_keys'].append(key)
            h['evidence'].append({'page':p['page'],'source':field,'printed_title':raw,'quote':raw,'match_method':options[0][1]})
    return list(hits.values())

PLATFORM_HEADINGS={
    'nintendo':'Nintendo Entertainment System','nes':'Nintendo Entertainment System',
    'nintendo entertainment system':'Nintendo Entertainment System',
    'genesis':'Sega Genesis','sega genesis':'Sega Genesis','mega drive':'Sega Genesis',
    'sega master system':'Sega Master System','master system':'Sega Master System',
    'turbografx 16':'NEC TurboGrafx-16','turhogr atx 16':'NEC TurboGrafx-16',
    'turhogra tx 16':'NEC TurboGrafx-16','turhogra tx16':'NEC TurboGrafx-16',
    'turhogratx 16':'NEC TurboGrafx-16','game boy':'Nintendo Game Boy',
    'super nintendo':'Super Nintendo Entertainment System','snes':'Super Nintendo Entertainment System',
    'game gear':'Sega Game Gear','arcade':'Arcade','atari 2600':'Atari 2600',
}

def platform_key(p):
    k=' '.join(tokens(p)); return {'turbografx 16':'nec turbografx 16','sega mega drive':'sega genesis','nintendo super nintendo entertainment system':'super nintendo entertainment system','nes':'nintendo entertainment system'}.get(k,k)

def coords(r):
    return len(r)>=5 and isinstance(r[0],str) and all(isinstance(x,(int,float)) for x in r[1:5]) and r[3]>0 and r[4]>0

def projected_headings(p):
    # Both OCR sources may use different image sizes. Fit scale using shared
    # unique lines and only project if their coordinates agree reasonably.
    a=defaultdict(list); b=defaultdict(list)
    for r in p['layout_lines']:
        if coords(r): a[' '.join(tokens(r[0]))].append(r)
    for r in p['cached_vision_lines']:
        if coords(r): b[' '.join(tokens(r[0]))].append(r)
    pairs=[(a[k][0],b[k][0]) for k in a.keys()&b.keys() if len(a[k])==len(b[k])==1 and len(k)>4]
    if len(pairs)<4: return []
    sx=statistics.median(x[1]/y[1] for x,y in pairs if y[1]>0) if any(y[1]>0 for x,y in pairs) else 0
    sy=statistics.median(x[2]/y[2] for x,y in pairs if y[2]>0) if any(y[2]>0 for x,y in pairs) else 0
    if not (0.5<sx<2 and 0.5<sy<2): return []
    if statistics.median(abs(x[1]-y[1]*sx)+abs(x[2]-y[2]*sy) for x,y in pairs)>45: return []
    return [[r[0],r[1]*sx,r[2]*sy,r[3]*sx,r[4]*sy] for r in p['cached_vision_lines'] if coords(r) and ' '.join(tokens(r[0])) in PLATFORM_HEADINGS]

def title_line_match(key,line):
    ts=tokens(line); ks=key.split()
    if ts==ks: return True
    # Strip a page locator only with explicit leader dots, avoiding sequel numerals.
    m=re.match(r'^(.*?)\.{2,}\s*([0-9]+(?:\s*[,–-]\s*[0-9]+)*)\s*$',line)
    return bool(m and tokens(m.group(1))==ks)

def toc_extract(hits,pages,toc,deadline):
    bypage=defaultdict(list)
    for h in hits: bypage[h['page']].append(h)
    toc_ids={t['page'] for t in toc}; leads=[]
    for p in pages:
        if p['page'] not in toc_ids: continue
        for source in ('layout_lines','cached_vision_lines'):
            rows=[r for r in p[source] if coords(r)]
            if not rows: continue
            anchors=[r for r in rows if ' '.join(tokens(r[0])) in PLATFORM_HEADINGS]
            if source=='layout_lines': anchors+=projected_headings(p)
            # infer left edges of columns from title lines, not standalone page numbers
            xclusters=[]
            for x in sorted(r[1] for r in rows if len(tokens(r[0])) and not all(t.isdecimal() for t in tokens(r[0]))):
                if not xclusters or abs(x-statistics.median(xclusters[-1]))>55: xclusters.append([x])
                else: xclusters[-1].append(x)
            cols=[statistics.median(c) for c in xclusters if len(c)>=5]
            def col(r): return min(range(len(cols)),key=lambda i:abs(cols[i]-r[1])) if cols else 0
            if not cols: continue
            # Same-column headings, or the last heading in the preceding column.
            for h in bypage[p['page']]:
                keys=[h['title_key']]+h.get('matched_alias_keys',[])
                for r in rows:
                    if time.monotonic()>=deadline: raise TimeoutError('Budget expired interpreting contents')
                    if not any(title_line_match(k,r[0]) for k in keys): continue
                    same=[a for a in anchors if col(a)==col(r) and a[2]<r[2]]
                    previous=[a for a in anchors if col(a)==col(r)-1]
                    anchor=max(same,key=lambda a:a[2]) if same else (max(previous,key=lambda a:a[2]) if previous else None)
                    heading_scope='same_column' if same else ('preceding_column_unverified' if anchor else None)
                    platform=PLATFORM_HEADINGS.get(' '.join(tokens(anchor[0]))) if anchor else None
                    m=re.search(r'\.{2,}\s*([0-9]+(?:\s*[,–-]\s*[0-9]+)*)\s*$',r[0]); loc=None; loc_ev=None
                    if m: loc=m.group(1); loc_ev=r[0]
                    else:
                        # Right-hand locator on same baseline within the column; no inferred offset.
                        end=cols[col(r)+1]-12 if col(r)+1<len(cols) else float('inf')
                        nums=[n for n in rows if re.fullmatch(r'\d{1,3}(?:\s*[,–-]\s*\d{1,3})*',n[0].strip()) and n[1]>=r[1]+r[3]+8 and n[1]<end and abs((n[2]+n[4]/2)-(r[2]+r[4]/2))<=max(7,min(r[4],n[4])*.32)]
                        if len(nums)==1: loc=nums[0][0]; loc_ev=nums[0][0]
                    if loc and any(int(z)>len(pages)+10 or int(z)<1 for z in re.findall(r'\d+',loc)): loc=None; loc_ev=None
                    leads.append({'title_key':h['title_key'],'contents_digital_page':p['page'],'source':source,'printed_title':r[0],'title_box':r[1:5],'platform_hint':platform,'platform_heading':anchor[0] if anchor else None,'platform_heading_scope':heading_scope,'platform_heading_source':('cached_vision_lines_projected' if source=='layout_lines' and anchor not in rows else source) if anchor else None,'printed_locator':loc,'locator_evidence':loc_ev,'status':'unverified_contents_lead'})
    return leads

# Deliberately limited to heading-adjacent lines, never infer a classification
# from unrelated words elsewhere on a page.
KIND_RULES=[('Map / level guide',r'\b(?:maps?|route)\b'),('Cheats / passwords',r'\b(?:passwords?|passw\w{2,}|sound test|level select|unlimited|invincib\w*|extra lives|extra continues|secret code|hidden screen)\b'),('Walkthrough',r'\bwalkthrough\b'),('Strategy guide',r'\bstrategy guide\b'),('Gameplay tips',r'\b(?:tips|tricks|super screen|find the scrolls|how to|beat\b)')]

def classify_local(h,p):
    keys=[h['title_key']]+h.get('matched_alias_keys',[]); results=[]
    for source in ('layout_lines','cached_vision_lines'):
        rows=[r for r in p[source] if coords(r)]
        for r in rows:
            if not any(title_line_match(k,r[0]) for k in keys): continue
            x,y,w,ht=r[1:5]
            for n in rows:
                xx,yy,ww,hh=n[1:5]
                if not (y+ht*.7<=yy<=y+max(ht*3,100) and abs(xx-x)<=max(55,w*.28)): continue
                for kind,pat in KIND_RULES:
                    if re.search(pat,n[0],re.I):
                        item={'kind_hint':kind,'page':p['page'],'source':source,'title_line':r[0],'quote':n[0]}
                        if item not in results: results.append(item)
    return results

def consolidate(ranked,pages,toc,year,deadline):
    leads=toc_extract(ranked,pages,toc,deadline); bytitle=defaultdict(list); leadmap=defaultdict(list)
    pmap={p['page']:p for p in pages}
    for l in leads: leadmap[l['title_key']].append(l)
    tiers=Counter(); dates=Counter()
    for h in ranked:
        if time.monotonic()>=deadline: raise TimeoutError('Budget expired consolidating')
        games=[]
        for g in h['catalog_candidates']:
            status=chronology(g,year); dates[status]+=1; games.append(dict(g,chronology=status))
        h['catalog_candidates']=games
        eligible=[g for g in games if g['chronology'] not in ('platform_later_era','release_later_than_issue')]
        h['eligible_catalog_candidates']=eligible
        if not eligible and games:
            h['previous_review_tier']=h['review_tier']; h['review_tier']='chronology_review'
        lds=leadmap[h['title_key']]
        hints=sorted({l['platform_hint'] for l in lds if l['platform_hint']})
        h['contents_platform_hints']=hints
        h['platform_supported_catalog_candidates']=[g for g in eligible if platform_key(g['platform']) in {platform_key(x) for x in hints}]
        strong_hints={platform_key(l['platform_hint']) for l in lds if l['platform_hint'] and l.get('platform_heading_scope')=='same_column'}
        h['platform_conflict']=bool(len(strong_hints)==1 and eligible and not any(platform_key(g['platform']) in strong_hints for g in eligible))
        h['promotion_signals']=strong_promotion(pmap[h['page']])
        if h['promotion_signals'] and h['review_tier']!='contents_lead':
            h['review_tier']='possible_advertisement'
        elif h['platform_conflict'] and h['review_tier'] in ('priority_review','review'):
            h['review_tier']='identity_review'
        h['kind_evidence']=[] if h['review_tier'] in ('contents_lead','possible_advertisement','chronology_review','low_priority','identity_review') else classify_local(h,pmap[h['page']])
        if h['kind_evidence'] and h['review_tier']=='review': h['review_tier']='priority_review'
        tiers[h['review_tier']]+=1; bytitle[h['title_key']].append(h)
    groups=[]
    for key,items in bytitle.items():
        if time.monotonic()>=deadline: raise TimeoutError('Budget expired grouping titles')
        relevant=[x for x in items if x['review_tier'] in ('priority_review','review')]
        lds=leadmap[key]
        if not relevant and not lds and not any(x['review_tier']=='identity_review' for x in items): continue
        games={str(g['id']):g for x in items for g in x['eligible_catalog_candidates']}
        supported={str(g['id']):g for x in items for g in x['platform_supported_catalog_candidates']}
        evidence=[e for x in relevant for e in x['kind_evidence']]
        # Same title across pages is a group of leads, NOT a contiguous article.
        groups.append({'title_key':key,'catalog_candidates':list(games.values()),'platform_supported_catalog_candidates':list(supported.values()),'contents_leads':lds,'review_digital_pages':sorted({x['page'] for x in relevant}),'all_matched_digital_pages':sorted({x['page'] for x in items}),'kind_hints':sorted({e['kind_hint'] for e in evidence}),'kind_evidence':evidence,'status':'unverified_group','article_range':None,'identity_review_digital_pages':sorted({x['page'] for x in items if x['review_tier']=='identity_review'})})
    groups.sort(key=lambda g:(not bool(g['kind_evidence']),not bool(g['review_digital_pages']),g['title_key']))
    return groups,leads,dict(tiers),dict(dates)


# Section navigation is separate from candidate verification: a TOC interval is
# not proof that every page is about the listed title.
def section_locators(p, total):
    w,h,*_=p.get('layout_size') or [0,0]
    if not isinstance(w,(int,float)) or not isinstance(h,(int,float)) or not w or not h: return []
    out=[]
    for source in ('layout_lines','cached_vision_lines'):
        rows=[r for r in p[source] if coords(r)]
        if not rows: continue
        sw=w
        if source=='cached_vision_lines':
            # Coordinates may come from a different raster; use the greater extent.
            sw=max(w,max(r[1]+r[3] for r in rows))
        for r in rows:
            text,x,y,rw,rh=r[:5]; clean=text.strip(' |—_')
            if y>h*.95 or not re.search('[A-Za-z]',clean): continue
            letters=[ch for ch in clean if ch.isalpha()]
            if not letters or sum(ch.isupper() for ch in letters)/len(letters)<.8: continue
            # Feature-style TOCs have spaced display headings rather than dense lists.
            if rh<max(16,h*.012) or len(tokens(clean))>12: continue
            m=re.fullmatch(r'(.+?)[\s.—–-]+(\d{1,3})',clean)
            title=None; loc=None; ev=None
            if m and (rw>=sw*.46 or re.search(r'\.{2,}|[—–]',clean)):
                title=m[1].strip(' .—–_-|'); loc=int(m[2]); ev=text
            else:
                nums=[n for n in rows if re.fullmatch(r'\d{1,3}',n[0].strip()) and n[1]>=x+rw+8
                      and abs((n[2]+n[4]/2)-(y+rh/2))<=max(6,min(rh,n[4])*.35)
                      and n[1]-x>=sw*.26]
                if len(nums)==1:
                    title=clean; loc=int(nums[0][0]); ev=[text,nums[0][0]]
            if not title or not 1<=loc<=total or not tokens(title) or len(title)<4: continue
            if loc<=p['page'] or not re.search('[A-Za-z]',title): continue
            # Exclude publication date/footer and obvious form labels.
            if re.search(r'\b(?:copyright|staff|january|february|march|april|may|june|july|august|september|october|november|december)\b',title,re.I): continue
            out.append({'contents_digital_page':p['page'],'heading':title,'printed_start':loc,'source':source,'box':[x,y,rw,rh],'locator_evidence':ev})
    # Prefer clean vision headings where duplicate observations share a locator.
    # Conflicting headings remain as observations; only one distinct printed
    # start defines a possible boundary.
    seen=set(); result=[]
    for x in sorted(out,key=lambda z:(z['printed_start'],z['source']!='cached_vision_lines')):
        key=(tuple(tokens(x['heading'])),x['printed_start'])
        if key not in seen: result.append(x); seen.add(key)
    return result

def footer_map(pages,label,year,deadline):
    name=label.split('|')[0].strip()
    mast=r'\s+'.join(re.escape(t) for t in tokens(name)) if name not in ('','?','Explicit index directory') else None
    anchors=[]; conflicts=[]
    for p in pages:
        if time.monotonic()>=deadline: raise TimeoutError('Budget expired mapping footers')
        height=p['layout_size'][1]
        if not isinstance(height,(int,float)) or not height: continue
        nums=defaultdict(list)
        for r in p['layout_lines']:
            if not coords(r) or r[2]+r[4]/2<height*.89: continue
            t=r[0]; matches=[]
            if mast:
                m=re.search(r'(?<!\d)(\d{1,3})\s*[. |]*'+mast+r'\b',t,re.I)
                if m: matches.append(int(m[1]))
            if year:
                m=re.search(r'(?<!\d)'+str(year)+r'\s+(\d{1,3})(?:\s*[|.]?\s*)$',t)
                if m and re.search(r'\b(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)',t,re.I): matches.append(int(m[1]))
            for n in matches:
                if 1<=n<=len(pages) and abs(p['page']-n)<=max(12,len(pages)*.2): nums[n].append(t)
        if len(nums)==1:
            n=next(iter(nums)); anchors.append({'printed':n,'digital':p['page'],'offset':p['page']-n,'evidence':nums[n]})
        elif len(nums)>1: conflicts.append({'digital':p['page'],'printed_options':sorted(nums),'evidence':dict(nums)})
    # A lone footer offset is too weak to support interpolation. Only support
    # offsets observed on multiple distinct pages; keep outliers in audit.
    counts=Counter(a['offset'] for a in anchors)
    accepted=[a for a in anchors if counts[a['offset']]>=2]
    def locate(n):
        exact=[a for a in accepted if a['printed']==n]
        if len(exact)==1: return {'digital':exact[0]['digital'],'method':'footer_anchor','support':[exact[0]]}
        if len(exact)>1: return {'digital':None,'method':'duplicate_printed_footer','support':exact}
        left=[a for a in accepted if a['printed']<n]; right=[a for a in accepted if a['printed']>n]
        if left and right:
            l=max(left,key=lambda a:a['printed']); r=min(right,key=lambda a:a['printed'])
            if l['offset']==r['offset'] and l['digital']<r['digital'] and not any(l['digital']<x['digital']<r['digital'] for x in conflicts):
                return {'digital':n+l['offset'],'method':'same_offset_between_anchors','support':[l,r]}
        return {'digital':None,'method':'unresolved','support':[]}
    return {'anchors':accepted,'unconfirmed_anchors':[a for a in anchors if a not in accepted],'conflicts':conflicts,'offset_counts':dict(counts)},locate

def section_report(ranked,pages,toc,label,year,deadline):
    toc_pages={t['page'] for t in toc}; pmap={p['page']:p for p in pages}; total=len(pages)
    found={n:section_locators(pmap[n],total) for n in toc_pages}
    # Follow at most two immediate pages only when the preceding TOC contains
    # several feature starts and the next page has several later numbered headings.
    continuations=[]
    for n in sorted(toc_pages):
        if len({x['printed_start'] for x in found[n]})<3: continue
        prior=found[n]
        for k in (n+1,n+2):
            if k in toc_pages or k not in pmap or k>min(12,total): break
            rows=section_locators(pmap[k],total)
            distinct={x['printed_start'] for x in rows}
            if len(distinct)<3 or sum(z>min(x['printed_start'] for x in prior) for z in distinct)<3: break
            found[k]=rows; continuations.append(k); prior=rows
    mapping,locate=footer_map(pages,label,year,deadline)
    obs=[x for n in sorted(found) for x in found[n]]
    starts=sorted({x['printed_start'] for x in obs})
    bytitle=defaultdict(list)
    for h in ranked: bytitle[h['title_key']].append(h)
    records=[]
    for n in starts:
        if time.monotonic()>=deadline: raise TimeoutError('Budget expired building section leads')
        observations=[x for x in obs if x['printed_start']==n]
        # Preserve alternate OCR readings; prefer clean vision over fused OCR.
        best=min(observations,key=lambda x:(x['source']!='cached_vision_lines',-(x['box'][0]+x['box'][2]),-len(x['heading'])))
        heading=best['heading']; hk=' '.join(tokens(heading))
        nxt=next((x for x in starts if x>n),None)
        start=locate(n); end=locate(nxt-1) if nxt else {'digital':None,'method':'no_next_locator','support':[]}
        # TOC end is an upper bound only; unnumbered headings, inserts, or
        # omitted entries may occur between locators. Never certify a range.
        a,b=start['digital'],end['digital']
        candidate_pages=list(range(a,b+1)) if a and b and a<=b else []
        exact_keys=[k for k,items in bytitle.items() if k==hk or any(hk in x.get('matched_alias_keys',[]) for x in items)]
        title_options=[]
        for k in exact_keys:
            games={str(g['id']):g for h in bytitle[k] for g in h.get('eligible_catalog_candidates',[])}
            supported=sorted({h['page'] for h in bytitle[k] if h['page'] in candidate_pages or (a is not None and h['page']==a)})
            title_options.append({'title_key':k,'method':'exact_heading_or_catalog_alias','catalog_candidates':list(games.values()),'title_mention_digital_pages':supported})
        if not title_options and a is not None:
            # Only propose an identity for review, never use this as a match.
            generic={'second','first','quest','world','power','information','feature','round','guide','game','player','players','watch','corner','editor','classified','journal','contest','spotlight','issue','next','baseball','loaded','playing'}
            distinctive={t for t in tokens(heading) if len(t)>=5 and t not in generic}
            window=set(candidate_pages[:10]) if candidate_pages else set(range(a,min(total,a+7)+1))
            contextual=[]
            for k,items in bytitle.items():
                if not distinctive.intersection(tokens(k)) or len(tokens(k))<2: continue
                local=[h for h in items if h['page'] in window and h.get('eligible_catalog_candidates')]
                if not local: continue
                games={str(g['id']):g for h in local for g in h['eligible_catalog_candidates']}
                contextual.append({'title_key':k,'method':'shared_distinctive_word_near_section_start_review_only','catalog_candidates':list(games.values()),'title_mention_digital_pages':sorted({h['page'] for h in local})})
            if len(contextual)==1: title_options=contextual
        records.append({'heading':heading,'contents_observations':observations,'printed_start':n,'next_printed_locator':nxt,'printed_end_upper_bound':nxt-1 if nxt else None,
                        'digital_start':start,'digital_end_upper_bound':end,'possible_digital_pages':candidate_pages,
                        'no_readable_text_pages':[i for i in candidate_pages if not pmap[i]['text'].strip() and not pmap[i]['cached_vision_lines']],
                        'title_options':title_options,'status':'unverified_section_lead','note':'TOC-based navigation proposal, not a confirmed article or game link. The next known locator is an upper bound, not proof of article length.'})
    return {'contents_pages':sorted(toc_pages),'possible_contents_continuation_pages':sorted(set(continuations)),'footer_mapping':mapping,'sections':records,'note':'Separate experimental navigation report; original candidate rankings unchanged. No metadata changed.'}

# Independent directory-profile extraction. A profile is not a review or walkthrough.
PROFILE_BY = re.compile(r'^\s*BY\s+([A-Z][A-Z0-9 .&’\'-]{1,45})\s*$', re.I)
PROFILE_GENERIC = set('action adventure arcade basketball boxing combat driving football golf puzzle racing roleplaying shooter simulation soccer sports strategy tennis theme title volleyball wrestling'.split())
PROFILE_PLATFORMS = [('Sega Genesis',r'\b(?:genesis|mega drive)\b'),('Sega Game Gear',r'\bgame gear\b'),('Sega CD',r'\b(?:sega[ -]?cd|cd[ -]?rom)\b'),('Sega Master System',r'\bmaster system\b'),('Nintendo Entertainment System',r'\b(?:nes|nintendo entertainment system)\b'),('Super Nintendo Entertainment System',r'\b(?:snes|super nintendo)\b')]
def profile_report(ranked,pages,sections,deadline):
    hits=defaultdict(list)
    for h in ranked: hits[h['page']].append(h)
    records=[]; detected=[]; excluded=[]; unresolved=[]
    for p in pages:
        if time.monotonic()>=deadline: raise TimeoutError('Budget expired extracting game profiles')
        page=p['page']; size=p.get('layout_size') or []
        if len(size)<2 or not all(isinstance(x,(int,float)) and x>0 for x in size[:2]): continue
        W,H=size[:2]; lines=[r for r in p['layout_lines'] if coords(r)]
        by=[r for r in lines if PROFILE_BY.fullmatch(r[0]) and r[3]<W*.45 and r[4]<H*.05]
        labels=[r for r in lines if re.search(r'\b(?:theme|cart\s*size|cartsize)\b',r[0],re.I)]
        # Repeated bylines AND repeated metadata, or exceptionally dense bylines.
        if len(by)<3 or (len(labels)<2 and len(by)<6): continue
        if page==1 or strong_promotion(p) or re.search(r'\b(?:reader service card|introductory subscription)\b',p['text'],re.I):
            excluded.append(page); continue
        detected.append(page)
        # A repeated vertical platform rail is strong page-level evidence only if legible.
        rails=[r[0] for r in lines if r[4]>H*.45 and r[3]<W*.12]
        rail_platforms={name for name,pat in PROFILE_PLATFORMS if any(len(re.findall(pat,t,re.I))>=2 for t in rails)}
        for anchor in by:
            ax,ay,aw,ah=anchor[1:5]; column=0 if ax+aw/2<W/2 else 1
            # Build the paragraph immediately above this publisher line from the same column.
            def samecol(r): return (0 if r[1]+r[3]/2<W/2 else 1)==column
            prior=[r[2] for r in by if samecol(r) and r[2]<ay]
            lower=max(ay-H*.15, max(prior,default=0)+ah)
            body=[r for r in lines if samecol(r) and lower<r[2]<ay and abs((r[1]+r[3]/2)-(ax+aw/2))<W*.15 and len(tokens(r[0]))>=3 and not PROFILE_BY.fullmatch(r[0]) and not re.search(r'\b(?:theme|cart\s*size|cartsize)\b',r[0],re.I) and r[4]<H*.04]
            body.sort(key=lambda r:r[2]); paragraph=[]; prev=ay
            for r in reversed(body):
                if prev-r[2]>H*.055: break
                paragraph.append(r); prev=r[2]
            paragraph.reverse()
            if len(paragraph)<2 or sum(len(tokens(r[0])) for r in paragraph)<12: continue
            top=paragraph[0][2]; text=' '.join(r[0] for r in paragraph); pub=PROFILE_BY.fullmatch(anchor[0]).group(1)
            # The title belongs to this block only if it occurs above the paragraph,
            # not merely somewhere on the page or in a description of another game.
            heads=[r for r in lines if samecol(r) and lower<=r[2]<top and top-r[2]<H*.105 and r[1]<paragraph[0][1]+W*.08 and r[4]<H*.1]
            heads.sort(key=lambda r:(r[2],r[1])); keys=[]
            for h in hits[page]:
                if not h.get('eligible_catalog_candidates'): continue
                canonical=h['title_key']; aliases=h.get('matched_alias_keys',[])
                matches=[]
                for i,r in enumerate(heads):
                    strings=[r[0]]
                    if i+1<len(heads):
                        nxt=heads[i+1]
                        if abs(nxt[1]-r[1])<W*.06 and 0<nxt[2]-r[2]<H*.027: strings.append(r[0]+' '+nxt[0])
                    for raw in strings:
                        k=' '.join(tokens(raw))
                        if not k or k in PROFILE_GENERIC: continue
                        if k==canonical or numeric_key(k)==numeric_key(canonical) or (k in aliases and len(k.split())>=2):
                            # Do not resolve a shortened heading if a following line completes a different title.
                            if len(strings)>1 and raw==strings[0] and k in aliases: continue
                            matches.append((len(k.split()),raw,r))
                if matches:
                    best=max(matches,key=lambda x:x[0]); keys.append((h,best))
            # Multi-title blocks remain unresolved, rather than choosing an arbitrary game.
            keys.sort(key=lambda x:x[1][0],reverse=True)
            if len(keys)>1 and keys[0][1][0]>keys[1][1][0] and tokens(keys[1][1][1])==tokens(keys[0][1][1])[:keys[1][1][0]]: keys=keys[:1]
            explicit={name for name,pat in PROFILE_PLATFORMS if re.search(pat,text,re.I)}
            platforms=sorted(explicit or rail_platforms)
            common={'digital_page':page,'publisher_text':pub,'description_ocr':text,'description_boxes':paragraph,'publisher_box':anchor,'platform_hints':platforms,'platform_evidence_scope':'profile_text' if explicit else 'page_rail' if rail_platforms else 'unknown','kind':'Game profile / catalog description','status':'unverified_profile'}
            if len(keys)!=1:
                unresolved.append(dict(common,reason='ambiguous_title' if keys else 'title_not_read_or_matched',heading_ocr=[r[0] for r in heads],title_keys=[h['title_key'] for h,_ in keys])); continue
            h,best=keys[0]; games=h['eligible_catalog_candidates']; supported=[g for g in games if platform_key(g['platform']) in {platform_key(t) for t in platforms}]
            # Without explicit platform evidence a single catalog ID is still only a possibility.
            records.append(dict(common,title_key=h['title_key'],heading_text=best[1],heading_box=best[2],catalog_candidates=games,platform_supported_catalog_candidates=supported,identity_status='platform_conflict' if platforms and not supported else 'platform_supported_unique' if len(supported)==1 else 'platform_ambiguous_or_unknown'))
    return {'directory_pages':detected,'excluded_promotion_pages':excluded,'profiles':records,'unresolved_profiles':unresolved,'note':'Independent layout evidence only. Publisher bylines and bounded text blocks support catalog profiles, not verified game links or reviews. Existing candidate and section reports are preserved.'}


def masthead_collision(hit, page, label):
    """Suppress cover branding only when all observed matching headings are masthead-sized."""
    if hit.get('page') != 1 or not page or ' | ' not in label: return False
    title=tokens(label.split(' | ',1)[0])
    if not title or title==['?']: return False
    def branding(text):
        ts=tokens(text)
        return bool(ts and (ts==title or (len(ts)==1 and len(ts[0])>=4 and ts[0] in title)))
    ev=hit.get('evidence',[])
    if not ev or not all(branding(e.get('printed_title','')) for e in ev): return False
    size=page.get('layout_size',[])
    if len(size)<2 or not all(isinstance(v,(int,float)) and v>0 for v in size[:2]): return False
    w,h=size[:2]
    lines=[r for field in ('layout_lines','cached_vision_lines') for r in page.get(field,[]) if coords(r) and branding(r[0]) and any(tokens(r[0])==tokens(e.get('printed_title','')) for e in ev)]
    return bool(lines and all(r[2]<h*.30 and r[3]>=w*.20 and r[4]>=h*.035 for r in lines))


def weak_alias(hit):
    ev=hit.get('evidence',[])
    return bool(ev and all(e.get('match_method') in ('catalog_alias','subtitle_abbreviation') and len(tokens(e.get('printed_title','')))==1 and tokens(e['printed_title'])[0].isalpha() for e in ev)
                and not hit.get('nearby_gameplay_text') and not hit.get('kind_evidence'))


def fragmented_heading(key,p):
    # A split title such as SUPER / MARIO BROS. or FINAL FANTASY / VII
    # is not reliable evidence for the shorter catalog identity.
    if not p: return False
    for field in ('layout_lines','cached_vision_lines'):
        rows=[r for r in p.get(field,[]) if coords(r)]
        for r in rows:
            if not title_line_match(key,r[0]): continue
            x,y,w,h=r[1:5]
            for n in rows:
                if n is r: continue
                xx,yy,ww,hh=n[1:5]; nt=tokens(n[0])
                if not nt or not .55<=hh/h<=1.65 or abs(xx-x)>max(25,w*.22): continue
                if 0<=y-(yy+hh)<=h*.9 and nt in (['super'],['the'],['new'],['ultimate']): return True
                if 0<=yy-(y+h)<=h*.9 and len(nt)==1 and NUMERAL.fullmatch(nt[0]): return True
    return False


def page_platform_hint(p):
    # A standalone platform heading is useful, but never assume a platform from the magazine brand.
    if not p: return None, []
    size=p.get('layout_size',[])
    if len(size)<2 or not isinstance(size[1],(int,float)) or size[1]<=0: return None, []
    found=[]
    for field in ('layout_lines','cached_vision_lines'):
        for r in p.get(field,[]):
            if not coords(r) or r[2]>size[1]*.25: continue
            k=' '.join(tokens(r[0])); platform=PLATFORM_HEADINGS.get(k)
            if platform and k not in ('nintendo','sega','arcade'):
                found.append({'platform':platform,'digital_page':p['page'],'source':field,'quote':r[0]})
    keys={platform_key(x['platform']) for x in found}
    return (found[0]['platform'] if len(keys)==1 else None),found


def discovery_main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--root',type=Path,default=DATA/'document-index/v8.0-phase1')
    ap.add_argument('--catalog',type=Path,default=DATA/'config/launchbox.integration.json')
    ap.add_argument('--output',type=Path,default=DATA/'content-intelligence-results')
    ap.add_argument('--budget-seconds',type=int,default=120)
    ap.add_argument('--label', help='Publication label, including cover date, for explicit index directory')
    ap.add_argument('--issue-year',type=int,help='Optional explicit publication year when the catalog report has no cover date')
    pick=ap.add_mutually_exclusive_group(); pick.add_argument('--list',action='store_true'); pick.add_argument('--latest',action='store_true'); pick.add_argument('--select'); pick.add_argument('--index-directory',type=Path)
    a=ap.parse_args()
    if not 1<=a.budget_seconds<=120: ap.error('Budget must be between 1 and 120 seconds')
    if a.issue_year is not None and not 1900<=a.issue_year<=2100: ap.error('Invalid issue year')
    if a.index_directory: directory=a.index_directory; label=a.label or 'Explicit index directory'
    else:
        if not a.root.is_dir(): raise ValueError(f'Index root not found: {a.root}')
        items=listing(a.root)
        if a.list or not (a.latest or a.select):
            for x in items: print(f'{x["label"]} | {x["total"]} pages | {x["key"]}')
            return
        matches=items[:1] if a.latest else [x for x in items if x['key']==a.select or a.select.casefold() in x['label'].casefold()]
        if len(matches)!=1: raise ValueError(f'Selection returned {len(matches)} indexes; use --list and an exact key.')
        directory=matches[0]['directory']; label=matches[0]['label']
    start=time.monotonic(); deadline=start+a.budget_seconds
    # No inference worker in this pass; the budget is cooperative, not an OS I/O timeout.
    manifest,pages=load_pages(directory)
    rows=catalog_rows(read(a.catalog)); trie,unique=build_trie(rows,deadline); aliases=build_aliases(rows,deadline); numerals=numeral_index(rows,deadline)
    a.output.mkdir(parents=True,exist_ok=True)
    out=Path(tempfile.mkdtemp(prefix='gv-ci-catalog-',dir=a.output)); print(f'Output: {out}',flush=True)
    save(out/'magazine-evidence.json',{'revision':REV,'source_fingerprint':manifest['fingerprint'],'total_pages':len(pages),'source_pages':pages})
    print(f'Selected {label}; {len(pages)} indexed pages; {len(rows)} catalog entries; {unique} normalized titles.',flush=True)
    hits=[]; done=[]; stop=None; suppressed=[]
    try:
        for p in pages:
            if time.monotonic()>=deadline: raise TimeoutError('Magazine time budget')
            result=scan_page(p,trie,deadline)
            existing={x['title_key']:x for x in result}
            for h in alias_hits(p,aliases,deadline)+numeral_hits(p,numerals,deadline):
                if h['title_key'] not in existing:
                    result.append(h); existing[h['title_key']]=h
                else:
                    old=existing[h['title_key']]; old['evidence'].extend(h['evidence']); old['matched_alias_keys']=sorted(set(old.get('matched_alias_keys',[])+h.get('matched_alias_keys',[])))
                    old['catalog_candidates']=list({str(g['id']):g for g in old['catalog_candidates']+h['catalog_candidates']}.values())
                    old['flags']=sorted(set(old['flags']+h['flags']))
            for hit in result:
                if masthead_collision(hit,p,label): suppressed.append({'reason':'publication_masthead','record':hit})
                else: hits.append(hit)
            done.append(p['page'])
            if len(done)%10==0 or len(done)==len(pages): print(f'{len(done)}/{len(pages)} pages scanned; {len(hits)} unverified title/page candidates',flush=True)
    except TimeoutError as e: stop=str(e)
    except KeyboardInterrupt: stop='user_cancelled'
    report={'revision':REV,'label':label,'source_fingerprint':manifest['fingerprint'],'catalog_entries':len(rows),'unique_catalog_titles':unique,'total_pages':len(pages),'scanned_pages':done,'unscanned_pages':[p['page'] for p in pages if p['page'] not in done],'complete_discovery':len(done)==len(pages),'stop_reason':stop,'seconds':round(time.monotonic()-start,2),'candidate_count':len(hits),'candidates':hits,'suppressed_evidence':suppressed,'records':[],'note':'Exact normalized token matches and conservative heading aliases are unverified mentions, not articles or confirmed game links. Platform lists are catalog possibilities, not printed evidence. No metadata changed. No AI invoked.'}
    try:
        ranked,tiers,toc=refine(hits,pages,deadline)
        year=a.issue_year if a.issue_year is not None else issue_year(label)
        groups,leads,tiers,dates=consolidate(ranked,pages,toc,year,deadline)
        report.update(candidates=ranked, review_counts=tiers, contents_evidence=toc, contents_leads=leads, title_groups=groups, chronology_counts=dates, issue_year=year, refinement_complete=True)
        save(out/'title-groups.json',{'revision':REV,'source_fingerprint':manifest['fingerprint'],'groups':groups,'note':'Unverified grouped leads only. Printed locators are not reader page IDs.'})
        print(f'{len(groups)} grouped title leads; {len(leads)} contents observations; chronology: '+json.dumps(dates),flush=True)
        print('Review tiers: '+json.dumps(tiers),flush=True)
        sections=section_report(ranked,pages,toc,label,year,deadline)
        save(out/'section-leads.json',{'revision':REV,'source_fingerprint':manifest['fingerprint'],**sections})
        report['section_summary']={'contents_pages':sections['contents_pages'],'possible_contents_continuation_pages':sections['possible_contents_continuation_pages'],'section_leads':len(sections['sections']),'mapped_starts':sum(x['digital_start']['digital'] is not None for x in sections['sections'])}
        print('Section navigation: '+json.dumps(report['section_summary']),flush=True)
        profiles=profile_report(ranked,pages,sections,deadline)
        save(out/'profile-leads.json',{'revision':REV,'source_fingerprint':manifest['fingerprint'],**profiles})
        report['profile_summary']={'directory_pages':len(profiles['directory_pages']),'profile_leads':len(profiles['profiles']),'unresolved_profiles':len(profiles['unresolved_profiles']),'platform_supported_unique':sum(r['identity_status']=='platform_supported_unique' for r in profiles['profiles'])}
        print('Game profiles: '+json.dumps(report['profile_summary']),flush=True)
        save(out/'review-queue.json',{'revision':REV,'source_fingerprint':manifest['fingerprint'],'records':[x for x in ranked if x['review_tier'] in ('priority_review','review','contents_lead','identity_review')],'note':'Unverified review leads only; no game relationships created.'})
    except TimeoutError as e:
        report.update(refinement_complete=False,stop_reason=str(e)); stop=str(e)
    report['seconds']=round(time.monotonic()-start,2)
    save(out/'content-candidates.json',report)
    print(f'{len(hits)} unverified title/page candidates; {len(done)}/{len(pages)} pages scanned in {time.monotonic()-start:.2f}s. No metadata changed.',flush=True)
    if stop: sys.exit(2)
    return out



def visual_pipeline(a, out):
    active_deadline = a.visual_deadline
    import argparse, json, os, re, sys, time, signal, subprocess, tempfile, zipfile, io, importlib.util
    from pathlib import Path
    from collections import defaultdict, Counter
    import csv, unicodedata, shutil

    def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
    def save(p,obj):
        q=p.with_suffix('.tmp'); q.write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n',encoding='utf-8'); os.replace(q,p)
    def natural(s): return tuple((1,int(x)) if x.isdigit() else (0,x.casefold()) for x in re.split(r'(\d+)',s))
    def source_reader(path,count):
        from PIL import Image, ImageOps
        if path.suffix.lower() in ('.cbz','.zip'):
            z=zipfile.ZipFile(path)
            names=sorted((n for n in z.namelist() if not n.startswith('__MACOSX/') and not Path(n).name.startswith('.') and Path(n).suffix.lower() in ('.jpg','.jpeg','.png','.webp','.bmp','.gif','.tif','.tiff')),key=natural)
            if len(names)!=count: z.close(); raise ValueError(f'Archive image count {len(names)} != index count {count}; refusing to assume page order')
            def get(n):
                with Image.open(io.BytesIO(z.read(names[n-1]))) as im: return ImageOps.exif_transpose(im).convert('RGB'),names[n-1]
            return get,z.close
        if path.suffix.lower()=='.pdf':
            s=subprocess.check_output(['pdfinfo',str(path)],text=True,errors='replace')
            m=re.search(r'^Pages:\s*(\d+)',s,re.M)
            if not m or int(m[1])!=count: raise ValueError('PDF page count differs from index or could not be read')
            def get(n):
                with tempfile.TemporaryDirectory() as t:
                    dest=Path(t)/'page'; subprocess.run(['pdftoppm','-f',str(n),'-l',str(n),'-r','180','-singlefile','-png',str(path),str(dest)],check=True,stdout=subprocess.DEVNULL)
                    with Image.open(dest.with_suffix('.png')) as im: return im.convert('RGB'),f'PDF page {n}'
            return get,lambda:None
        raise ValueError('Pilot supports PDF and ZIP-based CBZ only; no archive conversion is performed')

    def crop_box(record,page,im):
        w,h=page['layout_size'][:2]; iw,ih=im.size
        if abs((iw/ih)/(w/h)-1)>.06: raise ValueError('Original page aspect ratio differs from OCR coordinates')
        anchor=record['publisher_box']; top=min(r[2] for r in record['description_boxes']); col=int((anchor[1]+anchor[3]/2)>=w/2)
        x0,x1=col*w/2,(col+1)*w/2
        y0=max(0,top-h*.14); y1=min(h,top+h*.018)
        return (round(x0*iw/w),round(y0*ih/h),round(x1*iw/w),round(y1*ih/h))

    ROMAN=dict(zip('ii iii iv v vi vii viii ix x xi xii xiii xiv xv xvi xvii xviii xix xx'.split(),map(str,range(2,21))))

    def tokens(s):
        s=unicodedata.normalize('NFKC',s).casefold()
        s=re.sub(r'(?<=[a-z])(?=[0-9])|(?<=[0-9])(?=[a-z])',' ',s)
        return re.findall(r'[^\W_]+',s)

    def key(s):
        t=tokens(s)
        # Numerals inside titles are normalized only after at least one title token.
        t=[ROMAN.get(x,x) if i>0 else x for i,x in enumerate(t)]
        if len(t)>2 and t[-2]=='part' and t[-1].isdigit(): t.pop(-2)
        if t and t[0]=='the': t=t[1:]
        return ' '.join(t)

    def full_keys(s):
        k=key(s); out={k} if k else set(); t=k.split()
        # T2 versus Terminator 2, but only with the complete multi-word subtitle.
        # Never collapse the subtitle or use a bare T2 as identity evidence.
        if len(t)>=4 and t[1].isdigit() and len(t[0])>=1 and t[0].isalpha():
            out.add(' '.join([t[0][0],*t[1:]]))
        return out

    def geometry(rec):
        rows=[]
        for text,b in zip(rec.get('recognized_lines',[]),rec.get('quad_boxes',[])):
            if len(b)!=8 or not all(isinstance(v,(float,int)) for v in b): continue
            x,y,X,Y=min(b[::2]),min(b[1::2]),max(b[::2]),max(b[1::2])
            if X>x and Y>y: rows.append((str(text),x,y,X,Y))
        return rows

    def extract(rec):
        rows=geometry(rec)
        themes=[r for r in rows if key(r[0])=='theme']
        sizes=[r for r in rows if key(r[0])=='cart size']
        anchors=[(t,s) for t in themes for s in sizes if s[1]>t[3] and abs(t[2]-s[2])<=max(t[4]-t[2],s[4]-s[2])]
        if len(anchors)!=1: return None, [], 'missing_or_ambiguous_header_anchor'
        t,s=anchors[0]; h=max(t[4]-t[2],s[4]-s[2]); left=t[1]; top=min(t[2],s[2]); bottom=max(t[4],s[4])+2.5*h
        found=[]
        for r in rows:
            txt,x,y,X,Y=r
            if X>left+0.15*h or x<0 or y<top-.35*h or Y>bottom or Y-y>3*h or Y-y<.45*h: continue
            if not re.search(r'[A-Za-z]',txt) or key(txt) in {'theme','cart size','new','action','sports','adventure','simulation'} or re.match(r'^BY\s',txt,re.I): continue
            found.append(r)
        found.sort(key=lambda r:(r[2],r[1]))
        if not found or len(found)>3: return None, [], 'missing_or_ambiguous_title_lines'
        if any(b[2]-a[4]>1.5*h for a,b in zip(found,found[1:])): return None, [], 'disjoint_title_lines'
        return ' '.join(r[0] for r in found), [list(r) for r in found], 'layout_header'

    def platform(rec):
        hits=[]
        for t,x,y,X,Y in geometry(rec):
            # Side rail is a tall narrow vertical text region, not a body mention.
            if Y-y<3*(X-x): continue
            k=key(t)
            for pat,p in [(r'\bgenesis\b','Sega Genesis'),(r'\bmega drive\b','Sega Genesis'),(r'\bgame gear\b','Sega Game Gear'),(r'\bmaster system\b','Sega Master System'),(r'\bsega cd\b','Sega CD'),(r'\bmega cd\b','Sega CD')]:
                if re.search(pat,k): hits.append((p,t))
        return hits

    def resolve(report,rows,gv,year):
        if year is None: raise ValueError('Supply publication year; no date inferred from file name')
        index=defaultdict(dict)
        for row in rows:
            g=gv.game_info(row); variants=[row['title']]
            for field in ('alternateTitles','alternateNames','aliases','alternativeTitles'):
                v=row.get(field,[]); v=[v] if isinstance(v,str) else v
                if isinstance(v,list):
                    for a in v:
                        if isinstance(a,dict): a=a.get('title') or a.get('name')
                        if isinstance(a,str):
                            # Reject aliases dropping a numbered sequel or substantial subtitle.
                            main=key(row['title']).split(); alias=key(a).split()
                            if not set(x for x in main if x.isdigit()).issubset(alias): continue
                            if ':' in row['title'] and len(alias)<len(main): continue
                            variants.append(a)
            for v in variants:
                for k in full_keys(v): index[k][str(g['id'])]=g
        page_hints=defaultdict(set)
        edge_evidence=defaultdict(list)
        for page, observations in report.get('page_rails',{}).items():
            for observation in observations:
                for plat in observation.get('platforms',[]):
                    page_hints[int(page)].add(plat)
                    edge_evidence[int(page)].append([plat, observation['text']])
        for r in report['records']:
            for p,t in platform(r): page_hints[r['digital_page']].add(p)
        out=[]
        for r in report['records']:
            if r.get('status')!='ocr_completed': continue
            title,lines,reason=extract(r); own=platform(r); ph=sorted({p for p,t in own} | page_hints[r['digital_page']]); scope='crop_rail' if own else 'page_edge_ocr' if edge_evidence[r['digital_page']] else 'same_page_rail' if ph else 'unknown'
            candidates={}
            if title:
                for k in full_keys(title): candidates.update(index.get(k,{}))
            assessed=[]
            for g in candidates.values():
                if title and ':' in title and ':' in g['title']:
                    lhs=key(title.split(':',1)[1]); rhs=key(g['title'].split(':',1)[1])
                    if lhs and rhs and lhs!=rhs and not set(lhs.split()).issubset(rhs.split()): continue
                date=gv.chronology(g,year); conflict=bool(ph and g['platform'] not in ph)
                assessed.append({**g,'chronology':date,'platform_conflict':conflict})
            eligible=[g for g in assessed if not g['platform_conflict'] and g['chronology'] not in ('release_later_than_issue','platform_later_era')]
            if not title: status=reason
            elif not assessed: status='no_full_title_match'
            elif not eligible: status='platform_or_date_conflict'
            elif len(ph)>1: status='platform_evidence_conflict'
            elif len(eligible)>1: status='multiple_catalog_matches'
            elif len(ph)!=1: status='platform_unresolved'
            else: status='unique_review_lead'
            out.append(dict(sample=r['sample'],digital_page=r['digital_page'],title=title,title_lines=lines,platform_hints=ph,platform_evidence_scope=scope,platform_evidence=own or edge_evidence[r['digital_page']],catalog_candidates=assessed,eligible_catalog_candidates=eligible,status=status,coverage_status='unverified',source_ocr_truncated=r.get('token_limit_reached',False)))
        return dict(revision='visual-profile-resolver-v1.1',source_fingerprint=report.get('source_fingerprint'),source=report.get('source'),issue_year=year,records=out,summary=dict(Counter(r['status'] for r in out)),note='Same-page rail hints are tentative. Full title and subtitle retained. No metadata changes or inferred missing catalog IDs.')


    def tesseract(im,psm=11,tsv=True):
        buf=io.BytesIO(); im.save(buf,format='PNG')
        cmd=['tesseract','stdin','stdout','-l','eng','--psm',str(psm)]
        if tsv: cmd.append('tsv')
        r=subprocess.run(cmd,input=buf.getvalue(),stdout=subprocess.PIPE,stderr=subprocess.PIPE,timeout=max(.05,min(12,active_deadline-time.monotonic())),check=True,env={**os.environ,'OMP_THREAD_LIMIT':'2'})
        return r.stdout.decode('utf-8',errors='replace')

    def recognize_crop(im):
        groups=defaultdict(list)
        for row in csv.DictReader(io.StringIO(tesseract(im)),delimiter='\t'):
            if row.get('text','').strip() and row.get('level')=='5':
                groups[tuple(row[k] for k in ('block_num','par_num','line_num'))].append(row)
        labels=[]; boxes=[]
        for rows in groups.values():
            x=min(int(r['left']) for r in rows); y=min(int(r['top']) for r in rows)
            X=max(int(r['left'])+int(r['width']) for r in rows); Y=max(int(r['top'])+int(r['height']) for r in rows)
            labels.append(' '.join(r['text'] for r in rows)); boxes.append([x,y,X,y,X,Y,x,Y])
        return labels,boxes

    def read_rails(im):
        w,h=im.size; observations=[]
        patterns=[(r'\bgenesis\b','Sega Genesis'),(r'\bmega\s*drive\b','Sega Genesis'),(r'\bgame\s*gear\b','Sega Game Gear'),(r'\bmaster\s*system\b','Sega Master System'),(r'\b(?:sega|mega)\s*cd\b','Sega CD')]
        # Both edges are sampled because the two sides of a spread can differ.
        for side,box in [('left',(0,0,round(w*.055),h)),('right',(w-round(w*.055),0,w,h))]:
            strip=im.crop(box)
            for angle in (-90,90):
                rotated=strip.rotate(angle,expand=True)
                raw=tesseract(rotated,6,False); normalized=' '.join(tokens(raw))
                hits=sorted({plat for pat,plat in patterns if re.search(pat,normalized)})
                if hits:
                    observations.append(dict(side=side,angle=angle,text=raw.strip(),platforms=hits)); break
        return observations

    def retry_rails(im):
        # Narrower strips avoid illustrations adjacent to vertical labels. Rotate,
        # threshold, and read smaller sections rather than a long noisy strip.
        from PIL import ImageOps, ImageEnhance
        w,h=im.size; found=[]
        patterns=[(r'\bgenesis\b','Sega Genesis'),(r'\bmega\s*drive\b','Sega Genesis'),(r'\bgame\s*gear\b','Sega Game Gear'),(r'\bmaster\s*system\b','Sega Master System'),(r'\b(?:sega|mega)\s*cd\b','Sega CD')]
        for side in ('left','right'):
            edge=round(w*.045); inner=round(w*.012)
            x,X=(inner,edge) if side=='left' else (w-edge,w-inner)
            strip=im.crop((x,round(h*.10),X,round(h*.90)))
            for angle in (-90,90):
                rotated=ImageOps.autocontrast(ImageOps.grayscale(strip.rotate(angle,expand=True)))
                # PSM 7: a single horizontal line of repeated platform labels.
                # Invert white lettering on a dark rail into black on white.
                rotated=ImageOps.invert(rotated) if rotated.histogram()[:128] and sum(rotated.histogram()[:128])>rotated.width*rotated.height/2 else rotated
                rotated=rotated.resize((rotated.width*2,rotated.height*2))
                try: raw=tesseract(rotated,7,False).strip()
                except (subprocess.SubprocessError,OSError): continue
                normalized=' '.join(tokens(raw))
                # Demand repeated matching labels to guard against incidental text.
                hits=sorted({plat for pat,plat in patterns if len(re.findall(pat,normalized))>=2})
                if hits:
                    found.append(dict(side=side,angle=angle,text=raw,platforms=hits,method='narrow_rotated_rail')); break
        return found

    def reuse_worker(a,out,evidence):
        report=load(a.reuse_report)
        if report.get('source_fingerprint')!=a.fingerprint or report.get('engine')!='tesseract': raise ValueError('Reuse requires a matching Tesseract report')
        if report.get('status')!='completed' or report.get('completed')!=report.get('selected'): raise ValueError('Reuse requires a completed prior report')
        if a.source is not None and a.source != Path(report['source']): raise ValueError('Reuse source path mismatch')
        report['reused_report']=str(a.reuse_report); report['prior_elapsed_seconds']=report.pop('elapsed_seconds',None); report.pop('resolution_status',None)
        if a.cached_only:
            report['status']='completed'; save(out/'visual-profile-results.json',report); print('Cached OCR reused; no images or AI processed.',flush=True); return
        report['status']='retrying_rails'; report['rail_retry_pages']=[]; report['rail_retry_attempted']=[]
        rawfile=out/'visual-profile-results.json'; save(rawfile,report)
        path=a.source or Path(report['source']); report['source']=str(path)
        if not path.is_file(): raise ValueError('Prior source missing; supply --source')
        get,close=source_reader(path,evidence['total_pages'])
        try:
            targets=sorted({r['digital_page'] for r in report['records'] if not report['page_rails'].get(str(r['digital_page']))})
            for n in targets:
                if time.monotonic() >= a.visual_deadline:
                    report['status']='budget_reached'; save(rawfile,report); return
                im,entry=get(n); prior={r['source_entry'] for r in report['records'] if r['digital_page']==n}
                if prior!={entry}: raise ValueError(f'Source page identity changed at {n}')
                observations=retry_rails(im); report['rail_retry_attempted'].append(n)
                if observations:
                    report['page_rails'][str(n)]=observations; report['rail_retry_pages'].append(n)
                save(rawfile,report)
                print(f'Page {n}: {sorted({p for o in observations for p in o["platforms"]}) or "unresolved"}',flush=True)
        finally: close()
        report['status']='completed'; save(rawfile,report)
        print(f'Rail retry: {len(report["rail_retry_pages"])}/{len(report["rail_retry_attempted"])} previously unresolved pages recovered; prior crop OCR preserved.',flush=True)

    def worker(a,out):
        from PIL import Image
        profiles=load(a.results/'profile-leads.json'); evidence=load(a.results/'magazine-evidence.json')
        if profiles.get('source_fingerprint')!=a.fingerprint or evidence.get('source_fingerprint')!=a.fingerprint: raise ValueError('Reports do not match the selected source fingerprint')
        if not a.cached_only and not shutil.which('tesseract'): raise ValueError('Tesseract executable is not available')
        if a.reuse_report: return reuse_worker(a,out,evidence)
        pages={p['page']:p for p in evidence['source_pages']}; records=profiles['unresolved_profiles'][:a.limit]
        report=dict(source_fingerprint=a.fingerprint,source=None,selected=len(records),completed=0,records=[],page_rails={},status='preparing',engine='tesseract',note='Unverified suggestions. No metadata changed.')
        rawfile=out/'visual-profile-results.json'; save(rawfile,report)
        path=a.source; report['source']=str(path); save(rawfile,report); print('Source:',path,flush=True)
        get,close=source_reader(path,evidence['total_pages']); current=None; page_img=None
        try:
            for i,r in enumerate(records,1):
                if time.monotonic() >= a.visual_deadline:
                    report['status']='budget_reached'; save(rawfile,report); return
                started=time.monotonic(); n=r['digital_page']; p=pages[n]
                if n!=current:
                    page_img,entry=get(n); current=n
                    if str(n) not in report['page_rails']:
                        try: report['page_rails'][str(n)]=read_rails(page_img)
                        except (subprocess.SubprocessError,OSError) as exc: report['page_rails'][str(n)]=[]; print('Rail OCR failed:',n,str(exc),flush=True)
                        save(rawfile,report)
                box=crop_box(r,p,page_img); crop=page_img.crop(box); crop.thumbnail((1600,1600)); filename=f'crop-{i:03d}-page-{n:03d}.png'
                if a.save_crops: crop.save(out/filename)
                rec=dict(sample=i,digital_page=n,source_entry=entry,crop_box_original_pixels=list(box),publisher=r['publisher_text'],description_ocr=r['description_ocr'],status='prepared')
                if a.save_crops: rec['crop_file']=filename
                try:
                    labels,boxes=recognize_crop(crop)
                    rec.update(status='ocr_completed',recognized_lines=labels,quad_boxes=boxes,token_limit_reached=False)
                    title,_,reason=extract(rec); rec['extracted_title']=title; rec['heading_status']=reason
                    report['completed']+=1
                except (subprocess.SubprocessError,OSError,ValueError) as exc: rec.update(status='ocr_error',error=str(exc))
                rec['seconds']=round(time.monotonic()-started,3); report['records'].append(rec); report['status']='recognizing'; save(rawfile,report)
                print(f"{i}/{len(records)} page {n}: {rec.get('extracted_title') or rec.get('heading_status') or rec['status']} ({rec['seconds']}s)",flush=True)
            # Reuse the existing page-edge retry pass for fresh scans, after title OCR.
            report['rail_retry_pages']=[]; report['rail_retry_attempted']=[]
            targets=sorted({r['digital_page'] for r in report['records'] if not report['page_rails'].get(str(r['digital_page']))})
            for n in targets:
                if time.monotonic()>=a.visual_deadline:
                    report['status']='budget_reached'; save(rawfile,report); return
                try:
                    im,entry=get(n)
                    prior={r['source_entry'] for r in report['records'] if r['digital_page']==n}
                    if prior!={entry}: raise ValueError(f'Source page identity changed at {n}')
                    observations=retry_rails(im)
                except (subprocess.SubprocessError,OSError) as exc:
                    observations=[]; print('Rail retry failed:',n,str(exc),flush=True)
                report['rail_retry_attempted'].append(n)
                if observations:
                    report['page_rails'][str(n)]=observations; report['rail_retry_pages'].append(n)
                save(rawfile,report)
            print(f"Rail retry: {len(report['rail_retry_pages'])}/{len(report['rail_retry_attempted'])} pages recovered.",flush=True)
        finally: close()
        report['status']='completed'; save(rawfile,report)

    def section_hints(result, report, profiles):
        # Infer only inside a bracket of identical directly recognized rail labels.
        # Never promote this weaker evidence to a unique_review_lead.
        direct={}
        for r in result['records']:
            if r['platform_hints']:
                direct.setdefault(r['digital_page'],set()).update(r['platform_hints'])
        pages=sorted(set(profiles.get('directory_pages',[])))
        anchors=sorted(direct)
        inferred={}
        for n in pages:
            if n in direct: continue
            left=max((x for x in anchors if x<n),default=None)
            right=min((x for x in anchors if x>n),default=None)
            if left is None or right is None or right-left>16: continue
            if len(direct[left])!=1 or direct[left]!=direct[right]: continue
            # Do not bridge a long gap in detected directory pages.
            chain=[left]+[x for x in pages if left<x<right]+[right]
            if max(b-a for a,b in zip(chain,chain[1:]))>4: continue
            inferred[n]={'platform':next(iter(direct[left])),'anchor_pages':[left,right],'status':'tentative_section_hint'}
        for r in result['records']:
            hint=inferred.get(r['digital_page'])
            if not hint or r['platform_hints']: continue
            r['section_platform_hint']=hint
            r['section_compatible_candidates']=[g for g in r['eligible_catalog_candidates'] if g['platform']==hint['platform']]
            if len(r['section_compatible_candidates'])==1 and r['status'] in ('platform_unresolved','multiple_catalog_matches'):
                r['section_review_status']='tentative_unique_review_lead'
        result['section_hint_summary']={'direct_pages':len(direct),'inferred_pages':len(inferred),'tentative_unique_review_leads':sum(r.get('section_review_status')=='tentative_unique_review_lead' for r in result['records'])}
        result['section_hints']={str(k):v for k,v in inferred.items()}
        result['note']+=' Section hints are bracketed hypotheses only; original platform evidence, eligibility and status remain unchanged.'
        return result

    def finalize(a,out,report):
        from types import SimpleNamespace
        gv=SimpleNamespace(game_info=game_info, chronology=chronology)
        rows=catalog_rows(load(a.catalog)); result=resolve(report,rows,gv,a.year)
        result=section_hints(result,report,load(a.results/'profile-leads.json')); result['revision']='visual-profile-resolver-v1.2-section-hints'
        print('Section hints:',json.dumps(result['section_hint_summary']),flush=True)
        result['engine']='tesseract'; result['selected']=report.get('selected',0); result['completed']=report.get('completed',0)
        save(out/'visual-profile-resolved.json',result)
        print('Resolution:',json.dumps(result['summary']),flush=True)
        print('Extracted headings:',sum(bool(r.get('title')) for r in result['records']),flush=True)

    started=time.monotonic()
    try:
        worker(a,out)
    except Exception as exc:
        f=out/'visual-profile-results.json'
        report=load(f) if f.exists() else {'source_fingerprint':a.fingerprint,'records':[],'completed':0,'selected':0}
        report.update(status='error',error=str(exc))
        save(f,report)
        print('Visual stage error:',exc,flush=True)
        return 1
    f=out/'visual-profile-results.json'
    report=load(f)
    if report.get('completed') and time.monotonic() < a.deadline-1:
        try:
            finalize(a,out,report)
            report['resolution_status']='completed'
        except Exception as exc:
            report['resolution_status']='error'
            report['resolution_error']=str(exc)
            print('Resolution error:',exc,flush=True)
    report['elapsed_seconds']=round(time.monotonic()-started,2)
    save(f,report)
    print(f"Visual stage: {report.get('completed',0)}/{report.get('selected',0)} regions; {report.get('status')}; {report['elapsed_seconds']}s. No metadata changed.",flush=True)
    return 0 if report.get('status')=='completed' and (not report.get('completed') or report.get('resolution_status')=='completed') else 2

def page_context(p, title_count=0):
    """Classify only explicit page-wide signals, not arbitrary gameplay words."""
    if not p: return {'kind':'unclassified','signals':[]}
    if p['page']==1: return {'kind':'cover','signals':['first digital page (possible cover)']}
    lines=[r[0] for f in ('layout_lines','cached_vision_lines') for r in p.get(f,[]) if coords(r)]
    text=' '.join(tokens(p.get('text','')+' '+ ' '.join(lines)))
    if re.search(r'\b(?:contents|table of contents)\b',text) and any(region_key(r)=='contents' or region_key(r)=='table of contents' for r in lines): return {'kind':'contents','signals':['explicit contents heading']}
    if any(region_key(r) in ('scoreboard','sega scoreboard','high scores','high score board') for r in lines) and re.search(r'\b(?:top \d+ scores|highest score|high score)\b',text): return {'kind':'scoreboard','signals':['scoreboard and score-list language']}
    if strong_promotion(p): return {'kind':'advertisement','signals':['multiple commercial signals']}
    checklist=[q for q in ('software checklist','game checklist','games checklist','games for your reference','check off the ones','check off the games') if q in text]
    if title_count>=5 and checklist: return {'kind':'checklist','signals':checklist}
    size=p.get('layout_size',[])
    h=size[1] if len(size)>=2 and isinstance(size[1],(int,float)) else 0
    review=[]
    for f in ('layout_lines','cached_vision_lines'):
        for r in p.get(f,[]):
            if not coords(r) or not h or r[2]>h*.30: continue
            t=' '.join(tokens(r[0]))
            if len(t.split())<=8 and re.search(r'\b(?:special(?: \w+){0,3} review|game review|in depth review|review)\b',t) and (t.endswith('review') or t.endswith('reviews')):
                review.append(r[0])
    if review: return {'kind':'review','signals':sorted(set(review))}
    preview=[r[0] for f in ('layout_lines','cached_vision_lines') for r in p.get(f,[]) if coords(r) and h and r[2]<h*.25 and ' '.join(tokens(r[0])) in ('game preview','special preview','preview','sneak preview','first look')]
    if preview: return {'kind':'preview','signals':sorted(set(preview))}
    return {'kind':'unclassified','signals':[]}


def context_title_heading(r,p):
    # Exact observed alias is accepted only for a full, prominent heading.
    # Do not drop sequel numerals, nor use arbitrary short subtitle aliases.
    keys={r['title_key'],*r.get('matched_alias_keys',[])}
    for e in r.get('evidence',[]):
        if e.get('match_method')=='catalog_alias' and len(tokens(e.get('printed_title','')))>=2:
            keys.add(' '.join(tokens(e['printed_title'])))
    size=p.get('layout_size',[]) if p else []
    if len(size)<2 or not all(isinstance(v,(int,float)) and v>0 for v in size[:2]): return False
    w,h=size[:2]
    for f in ('layout_lines','cached_vision_lines'):
        for line in p.get(f,[]):
            if not coords(line): continue
            if line[2]>h*.32 or line[3]<w*.10 or line[4]<h*.018: continue
            for k in keys:
                if title_line_match(k,line[0]) and not fragmented_heading(k,p):
                    # Require the printed heading to recur in body OCR, not just a masthead.
                    phrase=' '.join(tokens(line[0])); body=' '.join(tokens(p.get('text','')))
                    if len(phrase)>=4 and len(re.findall(r'(?<!\w)'+re.escape(phrase)+r'(?!\w)',body))>=2:
                        return True
    return False


# Use the OCR text and geometry already present in the index. The new pass is
# deliberately bounded to explicit tips columns, prominent features in those
# pages, and game-specific paragraphs in hardware features.
def region_text(s):
    for a,b in [('â„¢','™'),('â€œ','“'),('â€\x9d','”'),('â€','”'),('â€™','’'),('Â®','®')]:
        s=s.replace(a,b)
    return s.replace('™','').replace('®','')


def region_key(s):
    return ' '.join(tokens(region_text(s)))


def region_leads(pages, candidates, contexts, year, explicit_scope=None):
    # Hardware-specific signals establish an inferred platform only when the
    # issue editorial expressly identifies its own manufacturer's newsletter.
    # Do not infer platform from brand name or catalog majority alone.
    editorial=[p for p in pages if p['page']<=3 and re.search(r'\bteam sega\s+newsletter\b',p.get('text',''),re.I) and re.search(r'\bour\b.{0,45}\bsega games\b',p.get('text',''),re.I|re.S)]
    hardware=[p for p in pages if re.search(r'\bpower base\b',p.get('text',''),re.I) and re.search(r'\bsega\s*[-\n]?\s*scope\b',p.get('text',''),re.I) and re.search(r'\blight\s+phaser\b',region_text(p.get('text','')),re.I)]
    platform='Sega Master System' if editorial and hardware and year is not None and 1985<=year<=1990 and (not explicit_scope or platform_key(explicit_scope)=='sega master system') else None
    scope=[]
    if platform:
        scope=[{'platform':platform,'digital_page':editorial[0]['page'],'source':'ocr','quote':'Team Sega Newsletter; own Sega games (editorial context)'}, {'platform':platform,'digital_page':hardware[0]['page'],'source':'ocr','quote':'Power Base; SegaScope 3-D; Light Phaser (hardware context)'}]
    # Catalog identity is grounded in actual discovery reports, including low-tier
    # matches. Index the canonical full title and observed catalog aliases, not
    # arbitrary prefixes. Never strip a numeral or manufacture a catalog ID.
    pool=defaultdict(dict); provenance=defaultdict(list)
    for i,r in enumerate(candidates):
        keys={r.get('title_key',''),*r.get('matched_alias_keys',[])}
        for c in r.get('catalog_candidates',[]):
            keys2=keys|{region_key(c['title'])}
            for k in keys2:
                if k:
                    pool[k][c['id']]=c
                    if len(provenance[k])<16: provenance[k].append({'file':'content-candidates.json','record_index':i,'digital_page':r['page']})
    records=[]
    action=re.compile(r'\b(?:press|push|jump|punch\w*|kick\w*|defeat|beat|attack|avoid|trap|receive|move|shoot|stop|protect|rescue|enemies|levels?|weakness\w*|controller|joystick|button\w*|continue)\b',re.I)
    def emit(p, title, kind, rows, signals):
        key=region_key(title); cs=list(pool.get(key,{}).values()); local,local_ev=page_platform_hint(p)
        inferred=local or explicit_scope or platform
        if local and (explicit_scope or platform) and platform_key(local)!=platform_key(explicit_scope or platform): inferred=None; conflict=True
        else: conflict=len({platform_key(x['platform']) for x in local_ev})>1
        viable=[c for c in cs if c.get('chronology') not in ('release_later_than_issue','platform_later_era') and not c.get('platform_conflict') and (not inferred or platform_key(c['platform'])==platform_key(inferred))]
        # One compatible ID is a tentative lead, never a confirmed relationship.
        selected=viable[0] if len(viable)==1 and not conflict and inferred else None
        if not cs or not selected:
            # Keep unresolved full titles including titles not present in the
            # discovered catalog. This is especially important for sequels.
            selected=None
        text=' '.join(region_text(r[0]) for r in rows)
        records.append({'digital_page':p['page'],'printed_title':title,'content_kind':kind,'catalog_candidates':cs,'selected':selected,'evidence':{'region_kind':kind,'region_signals':signals,'region_boxes':rows,'quotes':[{'quote':text,'source':'layout_lines'}], 'issue_platform_scope':inferred,'issue_platform_evidence':scope if inferred==platform and platform else [],'page_platform_evidence':local_ev,'context_compatible_catalog_ids':[c['id'] for c in viable], 'platform_conflict':conflict,'identity_method':'inferred_hardware_and_editorial' if selected and inferred==platform and platform else 'local_platform' if selected and local else 'unique_catalog_identity_tentative' if selected else None,'catalog_source_refs':provenance.get(key,[])}})
    for p in pages:
        pc=contexts[p['page']]['kind']
        if pc in ('cover','checklist','advertisement','contents','scoreboard'): continue
        rows=[r for r in p.get('layout_lines',[]) if coords(r)]
        if not rows: continue
        median=statistics.median(r[4] for r in rows)
        # A tips heading may be split over two adjacent lines in one column.
        tip_heads=[]
        for r in rows:
            k=region_key(r[0]); x,y,w,h=r[1:5]
            if re.fullmatch(r'(?:tips|hints) from (?:the )?(?:team|readers|experts|pros)',k): tip_heads.append(r)
            elif k in ('tips from','hints from') and any(region_key(q[0]) in ('the team','the readers','the experts','the pros') and abs(q[1]-x)<median*2 and 0<=q[2]-y-h<median*2 for q in rows): tip_heads.append(r)
        for head in tip_heads[:2]:
            x,y,w,h=head[1:5]
            col=sorted([r for r in rows if r[2]>y+h and abs(r[1]-x)<=max(median*2,w*.20)],key=lambda r:r[2])
            for j,r in enumerate(col):
                t=region_text(r[0]); m=re.match(r'^\s*[“"‘\']?\s*In\s+([^,]{2,65}),\s*(.*)$',t,re.I)
                if not m: continue
                title=m.group(1).strip(); key=region_key(title)
                if not key or len(key.split())>9: continue
                block=[r]; closed=bool(re.search(r'[”"]\s*$',t))
                for nxt in col[j+1:j+10]:
                    if closed or nxt[2]-(block[-1][2]+block[-1][4])>median*1.5 or re.match(r'^\s*[“"‘\']?\s*In\s+[^,]+,',region_text(nxt[0]),re.I) or re.search(r'\bAge\s+\d+\b',nxt[0]): break
                    block.append(nxt); closed=bool(re.search(r'[”"]\s*$',region_text(nxt[0])))
                body=' '.join(region_text(q[0]) for q in block)
                if len(tokens(body))>=10 and action.search(body): emit(p,title,'Tips and tricks',block,['bounded title-led tip in a tips column'])
        # A prominent exact game heading outside the tips column, with nearby
        # body prose and game-specific language, supports a separate feature.
        if tip_heads:
            for r in rows:
                key=region_key(r[0]); x,y,w,h=r[1:5]
                if key not in pool or h<median*2.2 or fragmented_heading(key,p) or any(abs(x-head[1])<max(median*3,head[3]*.25) for head in tip_heads): continue
                nearby=sorted([q for q in rows if 0<q[2]-y-h<median*18 and max(0,min(x+w,q[1]+q[3])-max(x,q[1]))>=min(w,q[3])*.25 and q[4]<median*1.6],key=lambda q:q[2])[:16]
                text=' '.join(q[0] for q in nearby)
                if len(tokens(text))>=35 and len(set(m.group(0).casefold() for m in action.finditer(text)))>=2:
                    emit(p,region_text(r[0]),'Game feature / article', [r]+nearby,['prominent full title with local gameplay prose'])
        # Hardware feature: a full title in a screenshot caption plus a
        # nearby repeated title in prose and game-specific mechanics. No
        # promotion based on the hardware name alone or a single mention.
        if p in hardware:
            body=region_key(p.get('text',''))
            for j,r in enumerate(rows):
                key=region_key(r[0]); x,y,w,h=r[1:5]
                if key not in pool or len(key.split())<2 or len(re.findall(r'(?<!\w)'+re.escape(key)+r'(?!\w)',body))<2: continue
                prev=rows[j-1] if j else None
                if not prev or abs(prev[1]-x)>median*2 or not action.search(prev[0]): continue
                # Caption text supports the game-specific gameplay claim;
                # require a second title occurrence in the article OCR.
                emit(p,region_text(r[0]).strip(' .'),'Game feature / gameplay information',[prev,r],['gameplay screenshot caption and repeated full title in hardware article'])
            # Preserve full preview titles, including the 3-D suffix split
            # over lines. The catalog may not contain these titles; retain as
            # unresolved evidence rather than substitute a base game.
            for j,r in enumerate(rows[:-1]):
                q=rows[j+1]
                if abs(q[1]-r[1])>median*2 or not 0<=q[2]-r[2]<=median*2: continue
                joined=' '.join(region_text(v[0]) for v in (r,q))
                match=re.match(r'^(.{4,110}?)\s+are\s+in\s+(?:the\s+)?(?:design|development)\s+stages\b',joined,re.I)
                if not match: continue
                names=re.split(r'\s+and\s+',match.group(1),flags=re.I)
                if len(names)!=2 or any(len(tokens(n))<2 or len(tokens(n))>7 for n in names): continue
                for title in names:
                    if not re.search(r'\b(?:3\s*[- ]?\s*d|[ivx]+|\d+)\s*$',title,re.I) and region_key(title) not in pool: continue
                    emit(p,title.strip(),'Game preview',[r,q],['explicit design/development-stage statement; full title preserved'])
    return records, platform, scope


def unified_review(out, document_key=None):
    """Create a read-only review contract; never promote a candidate into a relationship."""
    out=Path(out)
    base=read(out/'content-candidates.json')
    fp=base['source_fingerprint']; total=base['total_pages']
    key=document_key or base.get('document_key')
    if key and (not re.fullmatch(r'[0-9a-f]{64}/[0-9a-f]{64}',key) or key.split('/')[-1]!=fp):
        raise ValueError('Document key does not match report fingerprint')
    def optional(name):
        p=out/name
        if not p.exists(): return None
        d=read(p)
        if d.get('source_fingerprint')!=fp: raise ValueError('Fingerprint mismatch in '+name)
        return d
    profiles=optional('profile-leads.json') or {}
    visual=optional('visual-profile-resolved.json') or {}
    raw=optional('visual-profile-results.json') or {}
    indexed=optional('review-queue.json') or {}
    sections=optional('section-leads.json') or {}
    page_evidence=optional('magazine-evidence.json') or {}
    # Scope must be explicitly stated in editorial/cover audience wording,
    # not inferred from a magazine name, release date, or catalog majority.
    # A single-platform issue hint is still tentative for any individual article.
    scope_patterns=[('Nintendo Entertainment System',r'(?:nintendo entertainment system|nes)'),
                    ('Super Nintendo Entertainment System',r'(?:super nintendo entertainment system|super nintendo|snes)'),
                    ('Sega Genesis',r'(?:sega genesis|genesis|mega drive)'),
                    ('Sega Game Gear',r'(?:sega game gear|game gear)'),
                    ('Sega Master System',r'(?:sega master system|master system)'),
                    ('Sega CD',r'(?:sega cd|mega cd)')]
    scope_evidence=[]
    for p in page_evidence.get('source_pages',[]):
        if not 1<=p['page']<=3: continue
        for source,raw_text in [('ocr',p.get('text','')),('cached_vision_lines','\n'.join(x[0] for x in p.get('cached_vision_lines',[])))]:
            text=re.sub(r'(?<=\w)-\s*\n\s*(?=\w)','',raw_text)
            text=' '.join(text.split())
            for platform,pattern in scope_patterns:
                for m in re.finditer(r'(?i)\b(?:games you play on your|source for|official magazine (?:for|of)|magazine (?:for|dedicated to)|guide (?:for|to))\s+(?:the\s+)?'+pattern+r'\b(?:\s+(?:players|owners|games|system)\b)?',text):
                    # Avoid treating one member of a listed set as exclusive.
                    if re.match(r'\s*(?:,|/|&|and\b|or\b)',text[m.end():],re.I): continue
                    scope_evidence.append({'platform':platform,'digital_page':p['page'],'source':source,'quote':m.group()})
    scope_platforms={r['platform'] for r in scope_evidence}
    issue_scope=next(iter(scope_platforms)) if len(scope_platforms)==1 else None
    games={}; unresolved=[]; consumed=set(); navigation=[]
    page_map={p['page']:p for p in page_evidence.get('source_pages',[])}
    suppressed=list(base.get('suppressed_evidence',[]))
    title_counts=Counter(r['page'] for r in base.get('candidates',[]))
    contexts={n:page_context(p,title_counts[n]) for n,p in page_map.items()}
    regions, inferred_platform, inferred_scope=region_leads(list(page_map.values()),base.get('candidates',[]),contexts,base.get('issue_year'),issue_scope)
    save(out/'region-leads.json',{'revision':REV,'source_fingerprint':fp,'records':regions,'inferred_platform':inferred_platform,'inferred_platform_evidence':inferred_scope})
    deferred=[]
    def page(n):
        if type(n)!=int or not 1<=n<=total: raise ValueError('Invalid digital page: '+str(n))
        return n
    def add(source, index, n, title, kind, candidates, selected, confidence, evidence, status):
        n=page(n)
        ref={'file':source,'record_index':index}
        item={'digital_page':n,'printed_title':title,'content_kind':kind,'confidence':confidence,
              'source_status':status,'evidence':evidence,'source_ref':ref,'catalog_candidates':candidates,
              'coverage_status':'unverified', 'viable_catalog_candidates':[c for c in candidates if eligible(c) and (evidence.get('context_compatible_catalog_ids') is None or c.get('id') in evidence['context_compatible_catalog_ids'])]}
        if selected is None:
            unresolved.append(item); return
        gid=selected.get('id')
        if not gid or not any(x.get('id')==gid for x in candidates): raise ValueError('Selected game missing from candidate set')
        g=games.setdefault(gid,{'game_id':gid,'title':selected['title'],'platform':selected['platform'],
                                'review_status':'pending_review','evidence':[]})
        if (g['title'],g['platform'])!=(selected['title'],selected['platform']): raise ValueError('Inconsistent catalog identity '+gid)
        g['evidence'].append(item)
    def eligible(c):
        return c.get('chronology') not in ('release_later_than_issue','platform_later_era') and not c.get('platform_conflict',False)
    kind='Game profile / catalog description'
    prior=profiles.get('unresolved_profiles',[])
    rawmap={r['sample']:r for r in raw.get('records',[])}
    if len(rawmap)!=len(raw.get('records',[])): raise ValueError('Duplicate visual sample numbers')
    for i,r in enumerate(visual.get('records',[])):
        sample=r['sample']; original=rawmap.get(sample)
        if not original or original['digital_page']!=r['digital_page']: raise ValueError('Visual report sample mismatch')
        # Associate only the exact source block: a sample number by itself is not sufficient.
        if 1<=sample<=len(prior):
            p=prior[sample-1]
            if (p['digital_page'],p['description_ocr'],p['publisher_text'])==(original['digital_page'],original.get('description_ocr'),original.get('publisher')):
                consumed.add(sample-1)
        direct=[g for g in r.get('eligible_catalog_candidates',[]) if eligible(g)]
        tentative=[g for g in r.get('section_compatible_candidates',[]) if eligible(g)]
        selected=None; confidence='unresolved'
        if r['status']=='unique_review_lead' and len(direct)==1 and r.get('platform_hints') and not r.get('source_ocr_truncated'):
            selected=direct[0]; confidence='platform_supported_review'
        elif r.get('section_review_status')=='tentative_unique_review_lead' and len(tentative)==1 and not r.get('source_ocr_truncated'):
            selected=tentative[0]; confidence='section_inferred_review'
        add('visual-profile-resolved.json',i,r['digital_page'],r.get('title'),kind,r.get('catalog_candidates',[]),selected,confidence,
            {'heading_lines':r.get('title_lines',[]),'platform_hints':r.get('platform_hints',[]),'platform_evidence_scope':r.get('platform_evidence_scope'),
             'platform_evidence':r.get('platform_evidence',[]),'section_platform_hint':r.get('section_platform_hint'),
             'description_ocr':original.get('description_ocr'),'publisher':original.get('publisher'),'sample':sample,
             'source_entry':original.get('source_entry'),'crop_box_original_pixels':original.get('crop_box_original_pixels'),
             'source_ocr_truncated':r.get('source_ocr_truncated',False)},r['status'])
    for i,r in enumerate(profiles.get('profiles',[])):
        supported=[g for g in r.get('platform_supported_catalog_candidates',[]) if eligible(g)]
        selected=supported[0] if r.get('identity_status')=='platform_supported_unique' and len(supported)==1 else None
        add('profile-leads.json',i,r['digital_page'],r.get('heading_text'),kind,r.get('catalog_candidates',[]),selected,
            'platform_supported_review' if selected else 'unresolved',{'collection':'profiles','description_ocr':r.get('description_ocr'),
            'publisher':r.get('publisher_text'),'heading_box':r.get('heading_box'),'description_boxes':r.get('description_boxes'),
            'platform_hints':r.get('platform_hints',[]),'platform_evidence_scope':r.get('platform_evidence_scope')},r['identity_status'])
    for i,r in enumerate(prior):
        if i in consumed: continue
        add('profile-leads.json',i,r['digital_page'],None,kind,[],None,'unresolved',{'collection':'unresolved_profiles',
            'description_ocr':r.get('description_ocr'),'publisher':r.get('publisher_text'),'heading_ocr':r.get('heading_ocr',[])},r['reason'])
    for i,r in enumerate(indexed.get('records',[])):
        p=page_map.get(r['page'])
        reason='publication_masthead' if masthead_collision(r,p,base['label']) else ('unsupported_single_word_alias' if weak_alias(r) else None)
        if reason:
            suppressed.append({'reason':reason,'source_ref':{'file':'review-queue.json','record_index':i},'record':r}); continue
        # Only a canonical full heading + local gameplay evidence
        # and a unique context-compatible catalog ID can become a tentative review entry.
        candidates=r.get('catalog_candidates',[])
        headings=r.get('heading_evidence',[])
        exact=any(title_line_match(r['title_key'],h[0]) for h in headings)
        local_platform,local_platform_evidence=page_platform_hint(p)
        context=local_platform or issue_scope
        toc_supported=[c for c in r.get('platform_supported_catalog_candidates',[]) if eligible(c)]
        # If independent platform signals disagree, leave the record unresolved.
        local_conflict=bool(len({platform_key(x['platform']) for x in local_platform_evidence})>1 or (local_platform and issue_scope and platform_key(local_platform)!=platform_key(issue_scope)))
        toc_conflict=bool(context and toc_supported and any(platform_key(c['platform'])!=platform_key(context) for c in toc_supported))
        supported=[c for c in candidates if eligible(c) and context and platform_key(c['platform'])==platform_key(context)]
        if not context: supported=toc_supported
        selected=None
        pc=contexts.get(r['page'],{'kind':'unclassified','signals':[]})
        gameplay=bool(r.get('nearby_gameplay_text') or r.get('kind_evidence'))
        if (r['page']>1 and exact and len(tokens(r['title_key']))>=2 and not local_conflict and not toc_conflict
            and r.get('review_tier') in ('priority_review','review') and not r.get('promotion_signals') and not r.get('platform_conflict')
            and pc['kind'] not in ('cover','checklist','advertisement','contents','scoreboard') and gameplay and not fragmented_heading(r['title_key'],p) and len({c['id'] for c in supported})==1):
            selected=supported[0]
        viable=[c for c in candidates if eligible(c)]
        # Without platform evidence, a unique date-compatible catalog candidate is
        # a tentative identity only, and only on an explicitly labelled review
        # with a prominent full title and repeated body mentions.
        full_review=(pc['kind'] in ('review','preview') and context_title_heading(r,p) and not local_conflict and not toc_conflict
                     and not r.get('platform_conflict') and not r.get('promotion_signals'))
        if full_review:
            options=supported if context or toc_supported else viable
            if len({c['id'] for c in options})==1: selected=options[0]
        kinds=sorted({e['kind_hint'] for e in r.get('kind_evidence',[]) if e.get('kind_hint')})
        kind=r.get('kind') or (kinds[0]+' (review lead)' if selected and len(kinds)==1 else 'Game title heading (coverage unverified)' if selected else 'Unclassified title mention')
        if full_review: kind='Game review (explicit heading)' if pc['kind']=='review' else 'Game preview (explicit heading)'
        elif pc['kind']=='cover': kind='Cover mention'
        elif pc['kind']=='checklist': kind='Catalog / checklist reference'
        elif pc['kind']=='advertisement': kind='Possible advertisement'
        elif pc['kind']=='scoreboard': kind='High-score / competition reference'
        elif pc['kind']=='contents': kind='Contents reference'
        evidence={'review_tier':r.get('review_tier'),'flags':r.get('flags',[]),'quotes':r.get('evidence',[]),'heading_evidence':headings,
             'nearby_gameplay_text':r.get('nearby_gameplay_text',[]),'kind_evidence':r.get('kind_evidence',[]),
             'issue_platform_scope':issue_scope,'issue_platform_evidence':scope_evidence,'page_platform_evidence':local_platform_evidence,
             'contents_platform_hints':r.get('contents_platform_hints',[]),'fragmented_heading':fragmented_heading(r['title_key'],p),'platform_conflict':local_conflict or toc_conflict or r.get('platform_conflict',False),
             'page_context':pc,'context_compatible_catalog_ids':[c['id'] for c in supported] if (context or toc_supported) and not (local_conflict or toc_conflict or r.get('platform_conflict')) else None,'identity_method':'chronology_only_tentative' if selected and full_review and not context and not toc_supported else 'context_supported' if selected else None}
        title=next((e['printed_title'] for e in r.get('evidence',[]) if e.get('printed_title') and e.get('source') in ('layout_lines','cached_vision_lines')),r['title_key'])
        if pc['kind'] in ('cover','checklist','contents','scoreboard'):
            # These references never create a game lead. Link to an existing lead
            # only when the same full title resolves to one compatible ID.
            keys={r['title_key'],*r.get('matched_alias_keys',[])}
            exact_ref=any(title_line_match(k,h[0]) for k in keys for h in headings) and not any(fragmented_heading(k,p) for k in keys)
            if pc['kind']=='scoreboard':
                # Score rows are prefixed by a numeric score, not a title heading.
                exact_ref=any((m:=re.match(r'^\s*[0-9][0-9, .]*\s+(.+)$',region_text(e.get('quote','')))) and region_key(m.group(1)) in keys for e in r.get('evidence',[]) if e.get('source')=='layout_lines')
            deferred.append((i,r,title,kind,candidates,(supported if context or toc_supported else viable) if exact_ref and not evidence['platform_conflict'] else [],evidence))
        else:
            add('review-queue.json',i,r['page'],title,kind,candidates,selected,'section_inferred_review' if selected else 'unresolved',evidence,r['status'])
    for i,r in enumerate(sections.get('sections',[])):
        nav={'source_ref':{'file':'section-leads.json','record_index':i},**r}
        navigation.append(nav)
        start=r.get('digital_start',{}).get('digital')
        options=r.get('title_options',[])
        if not start or not options: continue
        # Keep the complete navigation lead, and only propose game identities
        # supported by an in-section mention and a stated platform scope.
        # Never select one game if competing title options exist.
        if len(options)!=1: continue
        opt=options[0]
        if opt.get('method') not in ('exact_heading_or_catalog_alias','shared_distinctive_word_near_section_start_review_only'): continue
        local=opt.get('title_mention_digital_pages',[])
        if not local: continue
        candidates=opt.get('catalog_candidates',[])
        supported=[c for c in candidates if eligible(c) and issue_scope and platform_key(c.get('platform',''))==platform_key(issue_scope)]
        selected=supported[0] if len({c['id'] for c in supported})==1 else None
        if not selected: continue
        # A table of contents names a feature, not necessarily a walkthrough.
        # A compound or contextual heading can represent only part of it.
        contextual=opt['method']!='exact_heading_or_catalog_alias'
        compound=bool(re.search(r'\s(?:&|and|/|vs\.?)\s',r['heading'],re.I))
        add('section-leads.json',i,start,r['heading'],'Game feature / article (TOC lead)',candidates,selected,'section_inferred_review',
            {'collection':'sections','identity_method':opt['method'],'identity_scope':'partial_or_contextual' if contextual or compound else 'exact_heading_or_alias',
             'title_mention_digital_pages':local,'issue_platform_scope':issue_scope,'issue_platform_evidence':scope_evidence,
             'contents_observations':r.get('contents_observations',[]),'printed_start':r.get('printed_start'),
             'digital_start_mapping':r.get('digital_start'),'digital_end_upper_bound':r.get('digital_end_upper_bound'),
             'possible_digital_pages':r.get('possible_digital_pages',[]),'no_readable_text_pages':r.get('no_readable_text_pages',[]),
             'range_status':'unverified_navigation_upper_bound'},'unverified_section_lead')
        nav['related_review_game_ids']=[selected['id']]

    for i,r in enumerate(regions):
        selected=r['selected']
        if selected:
            # Replace only the same full title/page unresolved observation; preserve all original reports.
            unresolved[:]=[e for e in unresolved if not (e['digital_page']==r['digital_page'] and any(c['id']==selected['id'] for c in e['catalog_candidates']) and region_key(e['printed_title'] or '') in (region_key(r['printed_title']),region_key(selected['title'])))]
        add('region-leads.json',i,r['digital_page'],r['printed_title'],r['content_kind'],r['catalog_candidates'],selected,'section_inferred_review' if selected else 'unresolved',r['evidence'],'unverified_region_lead')
    for i,r,title,kind,candidates,viable,evidence in deferred:
        if inferred_platform and not evidence.get('platform_conflict') and (not evidence.get('issue_platform_scope') or platform_key(evidence['issue_platform_scope'])==platform_key(inferred_platform)) and viable:
            filtered=[c for c in viable if platform_key(c['platform'])==platform_key(inferred_platform)]
            viable=filtered; evidence['context_compatible_catalog_ids']=[c['id'] for c in viable]; evidence['issue_platform_scope']=inferred_platform; evidence['issue_platform_evidence']=inferred_scope
        selected=viable[0] if len(viable)==1 and viable[0]['id'] in games else None
        add('review-queue.json',i,r['page'],title,kind,candidates,selected,'section_inferred_review' if selected else 'unresolved',evidence,r['status'])
    # Only exact matching ID and page can cross-reference existing weaker mentions; never merge on title alone.
    by_game_page={(gid,e['digital_page']) for gid,g in games.items() for e in g['evidence']}
    for r in unresolved:
        r['related_review_game_ids']=sorted({c['id'] for c in r['catalog_candidates'] if (c.get('id'),r['digital_page']) in by_game_page})
    for g in games.values():
        g['digital_pages']=sorted({e['digital_page'] for e in g['evidence']})
        def priority(e):
            k=e['content_kind'].casefold()
            if 'review (explicit' in k: return 0
            if 'feature / article' in k or 'cheat' in k or 'guide' in k or 'tips' in k: return 1
            if 'profile' in k or 'preview' in k: return 2
            if 'cover' in k or 'checklist' in k or 'contents reference' in k or 'high-score' in k: return 4
            if 'gameplay information' in k: return 1
            return 3
        g['primary_digital_page']=min(g['evidence'],key=lambda e:(priority(e),e['digital_page']))['digital_page']
        g['content_kinds']=sorted({e['content_kind'] for e in g['evidence']})
        g['confidence']='platform_supported_review' if any(e['confidence']=='platform_supported_review' for e in g['evidence']) else 'section_inferred_review'
    ordered=sorted(games.values(),key=lambda g:(g['confidence']!='platform_supported_review',g['title'].casefold(),g['platform'],g['game_id']))
    report={'revision':'unified-magazine-review-v1','document_key':key,'source_fingerprint':fp,'label':base['label'],
            'source':raw.get('source'),'issue_year':base.get('issue_year'),'total_pages':total,
            'issue_platform_scope':issue_scope,'issue_platform_evidence':scope_evidence,
            'complete_index_discovery':base.get('complete_discovery',False),'visual_status':raw.get('status','not_requested'),
            'visual_resolution_status':raw.get('resolution_status','not_requested'),'games':ordered,'unresolved_evidence':unresolved,'navigation_leads':navigation,
            'suppressed_evidence':suppressed,
            'engine_revision':REV,'inferred_platform_scope':inferred_platform,'inferred_platform_evidence':inferred_scope,'page_contexts':[{ 'digital_page':n,**c} for n,c in sorted(contexts.items()) if c['kind']!='unclassified'],
            'summary':{'region_review_observations':len(regions),'suppressed_evidence':len(suppressed),'distinct_game_review_rows':len(ordered),'platform_supported_review':sum(g['confidence']=='platform_supported_review' for g in ordered),
                       'section_inferred_review':sum(g['confidence']=='section_inferred_review' for g in ordered),'unresolved_evidence':len(unresolved),
                       'indexed_review_observations':len(indexed.get('records',[])),'visual_records':len(visual.get('records',[])),
                       'original_profile_blocks_reused':len(consumed),'navigation_leads':len(navigation),
                       'section_article_review_entries':sum(any(e['source_ref']['file']=='section-leads.json' for e in g['evidence']) for g in ordered)},
            'note':'Read-only review contract. No confirmed relationships or verified content coverage. Game IDs are grouped only when a report supplies a unique platform-supported or tentative section candidate. Article leads require in-section title evidence and explicit issue platform scope; TOC boundaries are unverified upper bounds, not confirmed article lengths. Original evidence remains in the source reports. Record indices are zero-based.'}
    save(out/'magazine-review.json',report)
    print('Magazine review:',json.dumps(report['summary']),flush=True)
    return report


def entry():
    started=time.monotonic()
    import subprocess, signal
    ap=argparse.ArgumentParser(add_help=False)
    ap.add_argument('--review-results',type=Path,help='Build unified review from existing reports only')
    ap.add_argument('--document-key',help='Optional exact document/fingerprint for review export')
    ap.add_argument('--visual',action='store_true')
    ap.add_argument('--source',type=Path)
    ap.add_argument('--reuse-report',type=Path)
    ap.add_argument('--retry-rails',action='store_true')
    ap.add_argument('--save-crops',action='store_true')
    ap.add_argument('--limit',type=int,default=165)
    ap.add_argument('--internal-run',action='store_true',help=argparse.SUPPRESS)
    ap.add_argument('--absolute-deadline',type=float,help=argparse.SUPPRESS)
    a,rest=ap.parse_known_args()
    if a.review_results and not a.internal_run:
        if rest or a.visual or a.source or a.reuse_report or a.retry_rails or a.save_crops: ap.error('--review-results is a standalone cached export')
        unified_review(a.review_results,a.document_key); return 0
    if '--help' in rest or '-h' in rest:
        print("Visual options: --visual --source /library/...cbz | --visual --reuse-report /data/.../visual-profile-results.json [--retry-rails] [--limit 165] [--save-crops]")
        sys.argv=[sys.argv[0],*rest]; discovery_main(); return 0
    if not 1<=a.limit<=165: ap.error('--limit must be 1–165')
    if a.visual and not (a.source or a.reuse_report): ap.error('--visual requires --source or --reuse-report')
    if not a.visual and (a.source or a.reuse_report or a.retry_rails or a.save_crops): ap.error('Visual options require --visual')
    if a.retry_rails and not a.reuse_report: ap.error('--retry-rails requires --reuse-report')
    if a.visual and '--list' in rest: ap.error('--visual requires a magazine selection, not --list')
    helper=argparse.ArgumentParser(add_help=False)
    helper.add_argument('--budget-seconds',type=int,default=120)
    helper.add_argument('--catalog',type=Path,default=DATA/'config/launchbox.integration.json')
    b,_=helper.parse_known_args(rest)
    if not 1<=b.budget_seconds<=120: ap.error('Budget must be 1–120 seconds')
    if not a.internal_run:
        deadline=started+b.budget_seconds
        proc=subprocess.Popen([sys.executable,str(Path(__file__).resolve()),*sys.argv[1:],'--internal-run','--absolute-deadline',str(deadline)],start_new_session=True)
        try:
            rc=proc.wait(timeout=max(.01,deadline-time.monotonic()-.25))
            print(f'Total processing: {time.monotonic()-started:.2f}s / {b.budget_seconds}s. No metadata changed.',flush=True)
            return rc
        except (subprocess.TimeoutExpired,KeyboardInterrupt) as exc:
            try: os.killpg(proc.pid,signal.SIGKILL)
            except ProcessLookupError: pass
            proc.wait()
            print('Stopped at time budget or cancellation. Completed atomic reports retained; interrupted stage may be incomplete. No metadata changed.',flush=True)
            return 2
    a.deadline=a.absolute_deadline
    # Discovery uses its original cooperative timer, bounded by the outer process watchdog.
    sys.argv=[sys.argv[0],*rest]
    out=discovery_main()
    if out is None: return 0
    key=a.document_key or next((rest[i+1] for i,v in enumerate(rest[:-1]) if v=='--select' and re.fullmatch(r'[0-9a-f]{64}/[0-9a-f]{64}',rest[i+1])),None)
    if not a.visual:
        unified_review(out,key); return 0
    report=read(out/'content-candidates.json')
    profiles=read(out/'profile-leads.json')
    a.fingerprint=report['source_fingerprint']
    a.year=report.get('issue_year')
    a.results=out
    a.catalog=b.catalog
    a.cached_only=bool(a.reuse_report and not a.retry_rails)
    a.visual_deadline=a.deadline-5
    if a.year is None:
        print('Visual stage skipped: issue year unknown; use --issue-year.',flush=True); return 2
    if not profiles.get('unresolved_profiles'):
        print('Visual stage skipped: no unresolved profile regions.',flush=True); unified_review(out,key); return 0
    if time.monotonic()>=a.visual_deadline:
        print('Visual stage skipped: insufficient remaining budget.',flush=True); return 2
    rc=visual_pipeline(a,out)
    if time.monotonic()<a.deadline-0.5: unified_review(out,key)
    return rc

if __name__=='__main__':
    try: sys.exit(entry())
    except (OSError,ValueError,KeyError,TypeError,TimeoutError) as e:
        print(f'ERROR: {e}',file=sys.stderr); sys.exit(1)

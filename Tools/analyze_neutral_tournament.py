"""Audit a level-2 factorial bot tournament; export every legal Neutral subset."""
from pathlib import Path
from collections import defaultdict,Counter
from itertools import combinations
import csv,json,math,hashlib,statistics
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Tests/Reports/Neutral1000'
def write_csv(name,rows):
    rows=list(rows)
    with (OUT/name).open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=list(rows[0]));writer.writeheader();writer.writerows(rows)
def ci_cluster(records,seed=913):
    grouped=defaultdict(list)
    for g in records:
        if g['winner'] in ['Cursermancer','Villager']:grouped[g['pair']].append(g['winner']=='Cursermancer')
    values=np.array([[sum(v),len(v)] for v in grouped.values()],dtype=float)
    if not len(values):return [None,None]
    rng=np.random.default_rng(seed)
    chosen=rng.integers(0,len(values),(3000,len(values)))
    totals=values[chosen].sum(axis=1)
    return list(np.quantile(totals[:,0]/totals[:,1]*100,[.025,.975]))
def cell(records):
    resolved=[g for g in records if g['winner'] in ['Cursermancer','Villager']]
    clusters=defaultdict(list)
    for g in resolved:clusters[g['pair']].append(g['winner']=='Cursermancer')
    values=[sum(v)/len(v) for v in clusters.values()]
    mean=sum(g['winner']=='Cursermancer' for g in resolved)/len(resolved) if resolved else 0
    variance=statistics.variance(values)/len(values) if len(values)>1 else 0
    return mean,variance,len(values)
def pvalue(value,se):return math.erfc(abs(value/se)/math.sqrt(2)) if se else (1.0 if value==0 else 0.0)
def bh(rows,p='p_value',q='q_fdr'):
    ranked=sorted(enumerate(rows),key=lambda v:v[1][p]);previous=1
    for rank in range(len(ranked),0,-1):
        i,row=ranked[rank-1];previous=min(previous,row[p]*len(ranked)/rank);rows[i][q]=previous
def stats(records):
    wins=Counter(g['winner'] for g in records);n=wins['Cursermancer']+wins['Villager']
    return {'games':len(records),'resolved':n,'cur_wins':wins['Cursermancer'],'villager_wins':wins['Villager'],
        'unresolved':wins['Unresolved'],'errors':wins['Error'],'cur_win_pct':100*wins['Cursermancer']/n if n else None,
        'mean_action_turns':statistics.mean(g['actionTurns'] for g in records) if records else None,
        'mean_seconds':statistics.mean(g['seconds'] for g in records) if records else None}
def main():
    raw=json.loads((OUT/'matches.json').read_text(encoding='utf-8-sig'));games=raw['games'];groups=raw['groups']
    assert raw['complete'] and raw['requested']==raw['recorded']==len(games)==1000
    assert sorted(g['id'] for g in games)==list(range(1,1001))
    assert Counter(g['firstPlayer'] for g in games)=={'Cursermancer':500,'Villager':500}
    assert len(groups)==8 and all(len(g['assets'])==2 for g in groups)
    translations={e['source']:e['english'] for e in json.loads((ROOT/'Tools/Localization/catalog.json').read_text(encoding='utf-8'))['entries'] if e['key'].startswith('card.name.')}
    names={a:translations.get(n,n) for group in groups for a,n in zip(group['assets'],group['names'])}
    for g in games:
        assert g['level']==2 and len(g['neutral'])==8
        assert g['neutral']==[group['assets'][(g['mask']>>i)&1] for i,group in enumerate(groups)]
        assert len(g['curs'])==2 and not g['errors'] if g['winner']!='Error' else True
        if g['winner']=='Villager':assert g['livingCurs']==0
        if g['winner']=='Cursermancer':assert 0<g['livingCurs']>=g['livingVillagers']
    coverage=Counter(g['mask'] for g in games)
    assert len(coverage)==256 and Counter(coverage.values())=={4:244,2:12}
    appearance=Counter(a for g in games for a in g['neutral']);assert set(appearance.values())=={500}
    pairs=defaultdict(list)
    for g in games:pairs[g['pair']].append(g)
    assert len(pairs)==500
    for pair in pairs.values():
        assert len(pair)==2 and len({g['mask'] for g in pair})==len({g['seed'] for g in pair})==1
        assert {g['firstPlayer'] for g in pair}=={'Cursermancer','Villager'}
        assert pair[0]['curs']==pair[1]['curs'],'Paired games selected different hidden roles.'
    overall=stats(games);overall['ci95_cluster_bootstrap']=ci_cluster(games)
    overall['simulation_seconds']=raw['seconds'];overall['total_frames']=sum(g['frames'] for g in games)
    starts=[]
    for side in ['Cursermancer','Villager']:
        chosen=[g for g in games if g['firstPlayer']==side]
        starts.append({'first_player':side,**stats(chosen),'ci95_cluster_bootstrap':ci_cluster(chosen)})
    differences=[]
    for pair in pairs.values():
        byside={g['firstPlayer']:g for g in pair}
        if all(g['winner'] in ['Cursermancer','Villager'] for g in pair):
            differences.append(int(byside['Cursermancer']['winner']=='Cursermancer')-int(byside['Villager']['winner']=='Cursermancer'))
    rng=np.random.default_rng(111);d=np.array(differences,dtype=float)
    start_effect={'cur_first_minus_village_first_pp':float(d.mean()*100),
        'ci95':list(np.quantile(d[rng.integers(0,len(d),(3000,len(d)))].mean(axis=1)*100,[.025,.975])),
        'discordant_cur_first_better':int(sum(d==1)),'discordant_village_first_better':int(sum(d==-1))}
    subset_records=defaultdict(list)
    for g in games:
        neutral=g['neutral']
        for n in range(1,9):
            for subset in combinations(neutral,n):subset_records[subset].append(g)
    assert len(subset_records)==3**8-1
    subset_rows=[]
    for subset,records in sorted(subset_records.items(),key=lambda item:(len(item[0]),item[0])):
        subset_rows.append({'size':len(subset),'assets':' | '.join(subset),'cards':' + '.join(names[a] for a in subset),**stats(records)})
    write_csv('all-6560-legal-subsets.csv',subset_rows)
    write_csv('neutral-pairs-112.csv',(r for r in subset_rows if r['size']==2))
    combos=[]
    for mask in range(256):
        records=[g for g in games if g['mask']==mask];assets=records[0]['neutral']
        combos.append({'mask':mask,'assets':' | '.join(assets),'cards':' + '.join(names[a] for a in assets),
            'seeds':len({g['seed'] for g in records}),'first_cur':sum(g['firstPlayer']=='Cursermancer' for g in records),
            'first_villager':sum(g['firstPlayer']=='Villager' for g in records),**stats(records)})
    write_csv('full-neutral-combinations-256.csv',combos)
    singles=[];contrasts=[]
    for i,group in enumerate(groups):
        cells=[]
        for bit,asset in enumerate(group['assets']):
            chosen=[g for g in games if asset in g['neutral']];cells.append(cell(chosen))
            singles.append({'group':group['number'],'asset':asset,'card':names[asset],**stats(chosen),
                'ci95_cluster_bootstrap':ci_cluster(chosen,500+i*2+bit)})
        effect=(cells[1][0]-cells[0][0]);se=math.sqrt(cells[0][1]+cells[1][1])
        contrasts.append({'group':group['number'],'a':names[group['assets'][0]],'b':names[group['assets'][1]],
            'a_asset':group['assets'][0],'b_asset':group['assets'][1],'b_minus_a_cur_win_pp':effect*100,
            'ci95_low_pp':(effect-1.96*se)*100,'ci95_high_pp':(effect+1.96*se)*100,'p_value':pvalue(effect,se)})
    bh(contrasts);write_csv('neutral-16-cards.csv',singles);write_csv('neutral-8-group-contrasts.csv',contrasts)
    interactions=[]
    for i,j in combinations(range(8),2):
        values={}
        for a in range(2):
            for b in range(2):values[a,b]=cell([g for g in games if (g['mask']>>i)&1==a and (g['mask']>>j)&1==b])
        delta=values[1,1][0]-values[1,0][0]-values[0,1][0]+values[0,0][0]
        se=math.sqrt(sum(v[1] for v in values.values()))
        interactions.append({'group_a':groups[i]['number'],'group_b':groups[j]['number'],
            'a0':names[groups[i]['assets'][0]],'a1':names[groups[i]['assets'][1]],'b0':names[groups[j]['assets'][0]],'b1':names[groups[j]['assets'][1]],
            'interaction_pp':delta*100,'ci95_low_pp':(delta-1.96*se)*100,'ci95_high_pp':(delta+1.96*se)*100,
            'p_value':pvalue(delta,se),'min_seed_pairs_per_cell':min(v[2] for v in values.values())})
    bh(interactions);write_csv('neutral-28-group-interactions.csv',interactions)
    write_csv('matches-1000.csv',({'id':g['id'],'pair':g['pair'],'seed':g['seed'],'mask':g['mask'],'level':g['level'],
        'first_player':g['firstPlayer'],'winner':g['winner'],'action_turns':g['actionTurns'],'rituals':g['rituals'],
        'frames':g['frames'],'seconds':g['seconds'],'maximum_seats':g['maximumSeats'],
        'neutral':' + '.join(names[a] for a in g['neutral']),'neutral_assets':' | '.join(g['neutral']),'curs':' | '.join(g['curs'])} for g in games))
    pilots={}
    for profile in ['control','fast']:
        p=json.loads((OUT/('pilot-'+profile+'.json')).read_text(encoding='utf-8-sig'))
        assert p['complete'] and p['recorded']==16 and p['errorMatches']==0
        pilots[profile]={'seconds':p['seconds'],'per_match':statistics.mean(g['seconds'] for g in p['games']),
            'frames_per_match':statistics.mean(g['frames'] for g in p['games']),'cur_wins':p['curWins'],'village_wins':p['villageWins']}
    speed={'pilots':pilots,'pilot_speedup':pilots['control']['seconds']/pilots['fast']['seconds'],
        'previous_editor_100_seconds':400.5467719,'previous_editor_mean_seconds':4.005467719,
        'current_mean_seconds':overall['mean_seconds'],'historical_per_match_speedup':4.005467719/overall['mean_seconds']}
    comparison=json.loads((OUT/'pilot-comparison.json').read_text(encoding='utf-8')) if (OUT/'pilot-comparison.json').exists() else {}
    source=json.loads((OUT/'source-manifest.json').read_text(encoding='utf-8'))
    source_changed=[f['source'] for f in source['files'] if hashlib.sha256((ROOT/f['source']).read_bytes()).hexdigest()!=f['sha256']]
    if source_changed:raise AssertionError('Source changed during experiment: '+str(source_changed))
    analysis={'overall':overall,'first_player':starts,'first_player_paired_effect':start_effect,'groups':groups,'names':names,
        'single_cards':singles,'group_contrasts':contrasts,'interactions':interactions,'full_combos':combos,
        'pair_rows':[r for r in subset_rows if r['size']==2],'subset_counts':dict(Counter(r['size'] for r in subset_rows)),
        'coverage_counts':dict(Counter(coverage.values())),'speed':speed,'pilot_comparison':comparison,
        'errors':[g for g in games if g['winner'] not in ['Cursermancer','Villager']],
        'interpretation':'Conditional win rates for these bot versions at level 2, not human card-strength rankings. 256 full decks have only 2/4 games each. Confidence intervals resample seed pairs; group contrast/interaction p-values use cluster-level standard errors and Benjamini-Hochberg FDR within 8 and 28 tests, respectively.'}
    (OUT/'analysis.json').write_text(json.dumps(analysis,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'overall':overall,'coverage':analysis['coverage_counts'],'subset_counts':analysis['subset_counts'],
        'significant_main_groups':[r for r in contrasts if r['q_fdr']<.05],
        'significant_interactions':[r for r in interactions if r['q_fdr']<.05],'speed':speed},ensure_ascii=False,indent=2))
if __name__=='__main__':main()

"""Setup Trader and Jester VFX assets and scene bindings."""
from pathlib import Path
import sys,re,shutil,json
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'Tools'))
from migrate_scene_ui import Scene,SCRIPTS,guid,ensure_meta,ref
for name in ['NeutralSkillFx','SkillFxGraphic','SkillFxBurst']:
    ensure_meta(ROOT/f'Assets/Scripts/{name}.cs');SCRIPTS[name]=guid(f'Assets/Scripts/{name}.cs')
backup=ROOT/'Backups/2026-11-01-trader-jester-fx'
for rel in ['Assets/Scenes/MainBoard.unity','Assets/Scenes/TutorialBoard.unity','Assets/UI/Localization/GameLocalization.asset','Tools/Localization/catalog.json']:
    dest=backup/rel
    if not dest.exists():dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/rel,dest)

def fresh(path):
    s=Scene.__new__(Scene);s.path=ROOT/path;s.path.parent.mkdir(parents=True,exist_ok=True)
    s.header='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n';s.records={9223372036854775807:'SceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots: []\n'};s.types={9223372036854775807:1660057539};s.stripped={};s.next_id=7500000000000000000;s.panels=[];s.buttons=[];s.manifest=[];return s
def save_prefab(s):
    s.path.write_text(s.header+''.join(f'--- !u!{s.types[n]} &{n}\n{body}' for n,body in s.records.items() if n!=9223372036854775807),encoding='utf-8');ensure_meta(s.path)
def center(s,tr):
    for field in ['m_AnchorMin','m_AnchorMax','m_Pivot']:s.set(tr,field,'{x: 0.5, y: 0.5}')
def glyph(s,go,kind):
    s.renderer(go)
    return s.mono(go,'SkillFxGraphic',f'''  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  glyph: {kind}
  paperColor: {{r: 1, g: 0.89, b: 0.66, a: 1}}
  inkColor: {{r: 0.67, g: 0.08, b: 0.2, a: 1}}
  stroke: 2
''')
def canvas_group(s,go):
    c=s.add(225,f'''CanvasGroup:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {ref(go)}
  m_Enabled: 1
  m_Alpha: 1
  m_Interactable: 0
  m_BlocksRaycasts: 0
  m_IgnoreParentGroups: 0
''');s.attach(go,c);return c

prefabs={}
for name,kind,size in [('SkillBallotPaper',0,(31,39)),('SkillComicSpark',1,(10,14)),('SkillVoteImpact',3,(52,52))]:
    path=f'Assets/Prefabs/Effects/{name}.prefab'
    if not (ROOT/path).exists():
        s=fresh(path);go,tr=s.node(name,rect=(0,0,*size));center(s,tr);component=glyph(s,go,kind);save_prefab(s)
    else:s=Scene(path,True);component=s.find_script(SCRIPTS['SkillFxGraphic'])
    prefabs[name]=f'{{fileID: {component}, guid: {guid(path)}, type: 3}}'
path='Assets/Prefabs/Effects/SkillComicBurst.prefab'
if not (ROOT/path).exists():
    s=fresh(path);go,tr=s.node('Skill comic burst',rect=(0,0,136,68));center(s,tr);group=canvas_group(s,go)
    graphic=glyph(s,go,2)
    title,_=s.label('Exclamation',tr,(8,9,120,46),'DEAL!',24,center=True,tint=(.38,.045,.08,1))
    s.records[title]=s.records[title].replace('    m_FontStyle: 0','    m_FontStyle: 1').replace('    m_BestFit: 0','    m_BestFit: 1').replace('    m_MinSize: 10','    m_MinSize: 15')
    formula,_=s.label('Trader equation',tr,(-32,73,200,24),'',17,center=True,tint=(1,.88,.6,1))
    s.set(s.game_object(formula),'m_IsActive','0')
    s.records[formula]=s.records[formula].replace('    m_FontStyle: 0','    m_FontStyle: 1')
    burst=s.mono(go,'SkillFxBurst',f'  graphic: {ref(graphic)}\n  title: {ref(title)}\n  formula: {ref(formula)}\n  group: {ref(group)}\n');save_prefab(s)
else:s=Scene(path,True);burst=s.find_script(SCRIPTS['SkillFxBurst'])
prefabs['burst']=f'{{fileID: {burst}, guid: {guid(path)}, type: 3}}'

for scene in ['MainBoard','TutorialBoard']:
    s=Scene(f'Assets/Scenes/{scene}.unity',True)
    if s.find_script(SCRIPTS['NeutralSkillFx']) is not None:continue
    view=s.find_script(guid('Assets/Scripts/VoteEffectsView.cs'));viewGo=s.game_object(view)
    go,tr=s.node('Trader and Jester skill VFX · no input blocking',7000000000000000013,(0,0,0,0))
    for field,value in [('m_AnchorMin','{x: 0, y: 0}'),('m_AnchorMax','{x: 1, y: 1}'),('m_Pivot','{x: 0.5, y: 0.5}'),('m_AnchoredPosition','{x: 0, y: 0}'),('m_SizeDelta','{x: 0, y: 0}')]:s.set(tr,field,value)
    s.canvas(go,250);canvas_group(s,go)
    fx=s.mono(viewGo,'NeutralSkillFx',f'''  layer: {ref(tr)}
  ballotPrefab: {prefabs['SkillBallotPaper']}
  sparkPrefab: {prefabs['SkillComicSpark']}
  ringPrefab: {prefabs['SkillVoteImpact']}
  burstPrefab: {prefabs['burst']}
  traderInk: {{r: 0.71, g: 0.4, b: 0.09, a: 1}}
  jesterInk: {{r: 0.67, g: 0.08, b: 0.2, a: 1}}
  parchment: {{r: 1, g: 0.89, b: 0.66, a: 1}}
  traderPunch: 1.14
  jesterPunch: 1.18
  maxParticles: 48
''');s.set(view,'skillFx',ref(fx));s.save()

asset=ROOT/'Assets/UI/Localization/GameLocalization.asset';text=asset.read_text(encoding='utf-8-sig')
catalog=ROOT/'Tools/Localization/catalog.json';data=json.loads(catalog.read_text(encoding='utf-8'))
for i,(en,vi) in enumerate([('DEAL!','Chốt!'),('NO BALLOTS','Chưa có phiếu')]):
    key=f'skill.fx.{i}';entry=dict(key=key,source=en,english=en,vietnamese=vi)
    block='  - key: '+json.dumps(key)+'\n'+''.join('    '+f+': '+json.dumps(entry[f],ensure_ascii=False)+'\n' for f in ['source','english','vietnamese'])
    pattern=rf'(?ms)^  - key: "?{re.escape(key)}"?\n.*?(?=^  - key:|\Z)'
    text=re.sub(pattern,lambda _:block,text) if re.search(pattern,text) else text.rstrip()+'\n'+block
    old=next((n for n,e in enumerate(data['entries']) if e['key']==key),None)
    if old is None:data['entries'].append(entry)
    else:data['entries'][old]=entry
asset.write_text(text,encoding='utf-8');catalog.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Authored Trader/Jester FX only. CardUI prefab geometry was not edited.')
if all((ROOT/f'Assets/UI/SkillFX/{name}.png').exists() for name in ['trader-paper','jester-paper','trader-burst','jester-burst','skill-spark','skill-impact']):
    import runpy
    runpy.run_path(str(ROOT/'Tools/apply_painted_skill_fx.py'))

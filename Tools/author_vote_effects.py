"""Configure vote HUD layout, history feed, and icon bindings."""
from pathlib import Path
import sys,re,uuid,shutil,json
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'Tools'))
from migrate_scene_ui import Scene,SCRIPTS,guid,ensure_meta,ref,color

backup=ROOT/'Backups/2026-11-01-vote-presentation'
for rel in ['Assets/Prefabs/CardUI.prefab','Assets/Scenes/MainBoard.unity','Assets/Scenes/TutorialBoard.unity','Assets/UI/Localization/GameLocalization.asset','Tools/Localization/catalog.json']:
    dest=backup/rel
    if not dest.exists():dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/rel,dest)
for name in ['VoteEffectFeed','CardVotePresentation','CardVoteHint','VoteEffectRow','VoteEffectsView']:
    ensure_meta(ROOT/f'Assets/Scripts/{name}.cs');SCRIPTS[name]=guid(f'Assets/Scripts/{name}.cs')
for name,path in [('VerticalLayoutGroup','Layout/VerticalLayoutGroup.cs'),('LayoutElement','Layout/LayoutElement.cs')]:
    SCRIPTS[name]=guid('Library/PackageCache/com.unity.ugui/Runtime/UGUI/UI/Core/'+path)

icons=['detective-evidence','snooper-eye','gambler-wager','ballot-token']
template=(ROOT/'Assets/UI/ModeIcons/mode-village.png.meta').read_text(encoding='utf-8')
for name in icons:
    path=ROOT/f'Assets/UI/VoteEffects/{name}.png';meta=path.with_name(path.name+'.meta')
    if not meta.exists():
        body=re.sub(r'^guid: \w+',lambda _: 'guid: '+uuid.uuid4().hex,template,count=1,flags=re.M)
        body=re.sub(r'(maxTextureSize:) \d+',r'\g<1> 256',body)
        body=re.sub(r'^  spriteMeshType: \d+', '  spriteMeshType: 0',body,flags=re.M)
        meta.write_text(body,encoding='utf-8')
def art(name):return '{fileID: 21300000, guid: '+guid(f'Assets/UI/VoteEffects/{name}.png')+', type: 3}'
def icon(s,name,parent,rect,sprite,raycast=False):
    go,tr=s.node(name,parent,rect);im=s.image(go,art(sprite),raycast=raycast);s.set(im,'m_PreserveAspect','1');return go,tr,im
def label(s,name,parent,rect,text,size=16,center=False):
    return s.label(name,parent,rect,text,size=size,center=center)
def hint(s,go,kind):s.mono(go,'CardVoteHint','  kind: '+kind+'\n')
def fontfield(s,comp,key,value):s.records[comp]=re.sub(r'(?m)^    '+key+r':.*$', '    '+key+': '+str(value),s.records[comp])
def anchored(s,tr,anchor,pivot,pos,size):
    for field,value in [('m_AnchorMin',anchor),('m_AnchorMax',anchor),('m_Pivot',pivot),('m_AnchoredPosition',pos),('m_SizeDelta',size)]:
        s.set(tr,field,'{x: '+str(value[0])+', y: '+str(value[1])+'}')
def stretch(s,tr):
    s.set(tr,'m_AnchorMin','{x: 0, y: 0}');s.set(tr,'m_AnchorMax','{x: 1, y: 1}')
    s.set(tr,'m_Pivot','{x: 0.5, y: 0.5}');s.set(tr,'m_AnchoredPosition','{x: 0, y: 0}');s.set(tr,'m_SizeDelta','{x: 0, y: 0}')

s=Scene('Assets/Prefabs/CardUI.prefab',True)
if s.find_script(SCRIPTS['CardVotePresentation']) is None:
    ci=s.find_script(guid('Assets/Scripts/CardInstance.cs'));go=s.game_object(ci);visual=7743153093351533012
    def field(name):return int(re.search(r'^  '+name+r': \{fileID: (-?\d+)\}',s.records[ci],re.M)[1])
    # Preserve old suspicion/mark state objects for the existing mechanics and snapshots.
    suspected=field('suspectedMark');im=s.component(suspected,script=SCRIPTS['Image'])
    s.set(im,'m_Color','{r: 1, g: 1, b: 1, a: 0}');s.set(im,'m_RaycastTarget','0')
    s.set(s.component(suspected,type_=224),'m_SizeDelta','{x: 0, y: 0}')
    s.set(s.game_object(field('hatContainer')),'m_IsActive','0')
    for f,name,sprite,y in [('snooperMark','Snooper target','snooper-eye',-76),('gamblerMark','Gambler wager','gambler-wager',-108)]:
        mark=field(f);tr=s.component(mark,type_=224);s.parent(tr,visual)
        anchored(s,tr,(1,1),(1,1),(-3,y),(28,28));s.set(tr,'m_LocalScale','{x: 1, y: 1, z: 1}')
        image=s.component(mark,script=SCRIPTS['Image']);s.set(image,'m_Sprite',art(sprite));s.set(image,'m_PreserveAspect','1');s.set(image,'m_RaycastTarget','1')
        hint(s,mark,'Snooper' if f=='snooperMark' else 'Gambler')
    vote,voteTr=s.node('Vote badge · click for explanation',visual,(0,0,90,30),False)
    anchored(s,voteTr,(.5,0),(.5,1),(0,-7),(90,30));s.image(vote,tint=(.12,.025,.035,.98),raycast=True);hint(s,vote,'Votes')
    icon(s,'Ballot',voteTr,(3,3,24,24),'ballot-token')
    count,_=label(s,'Vote count',voteTr,(29,0,25,30),'0',20,True);fontfield(s,count,'m_FontStyle',1)
    word,_=label(s,'Vote unit',voteTr,(55,0,34,30),'votes',12,True)
    badge,detectiveTr=s.node('Detective suspicion · not votes',visual,(0,0,48,28),False)
    anchored(s,detectiveTr,(1,1),(1,1),(-3,-42),(48,28));s.image(badge,tint=(.12,.025,.035,.95),raycast=True);hint(s,badge,'Detective')
    icon(s,'Evidence',detectiveTr,(0,0,28,28),'detective-evidence')
    suspicion,_=label(s,'Suspicion total',detectiveTr,(28,0,20,28),'+1',13,True);fontfield(s,suspicion,'m_FontStyle',1)
    frame,frameTr=s.node('Skill activation border',visual,(0,0,152,232));stretch(s,frameTr)
    group=s.add(225,f'''CanvasGroup:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {ref(frame)}
  m_Enabled: 1
  m_Alpha: 0
  m_Interactable: 0
  m_BlocksRaycasts: 0
  m_IgnoreParentGroups: 0
''');s.attach(frame,group)
    for n,rect in [('Top',(0,-2,152,2)),('Bottom',(0,232,152,2)),('Left',(-2,0,2,232)),('Right',(152,0,2,232))]:
        eg,_=s.node(n,frameTr,rect);s.image(eg,tint=(1,.83,.45,1))
    s.mono(go,'CardVotePresentation',f'''  voteBadge: {ref(vote)}
  suspicionBadge: {ref(badge)}
  voteCount: {ref(count)}
  voteWord: {ref(word)}
  suspicionCount: {ref(suspicion)}
  voteMotion: {ref(voteTr)}
  suspicionMotion: {ref(detectiveTr)}
  snooperMotion: {ref(s.component(field('snooperMark'),type_=224))}
  gamblerMotion: {ref(s.component(field('gamblerMark'),type_=224))}
  activationFrame: {ref(group)}
''')
    # Keep the legacy numeric state; render the count in a separate badge.
    s.set(field('voteText'),'m_Enabled','0')
    s.path.write_text(s.header+''.join(f'--- !u!{s.types[n]} &{n}\n{body}' for n,body in s.records.items()),encoding='utf-8')

rowpath=ROOT/'Assets/Prefabs/VoteEffectRow.prefab'
if not rowpath.exists():
    row=Scene.__new__(Scene);row.path=rowpath;row.header='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n';row.records={9223372036854775807:'SceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots: []\n'};row.types={9223372036854775807:1660057539};row.stripped={};row.next_id=7200000000000000000;row.panels=[];row.buttons=[];row.manifest=[]
    go,tr=row.node('Vote effect row',rect=(0,0,1184,114));bg=row.image(go,tint=(.13,.035,.045,.96),raycast=False)
    row.mono(go,'LayoutElement','  m_IgnoreLayout: 0\n  m_MinWidth: -1\n  m_MinHeight: 114\n  m_PreferredWidth: -1\n  m_PreferredHeight: 114\n  m_FlexibleWidth: 1\n  m_FlexibleHeight: 0\n  m_LayoutPriority: 1\n')
    _,_,im=icon(row,'Source icon',tr,(10,12,38,38),'ballot-token')
    source,_=label(row,'Source',tr,(58,8,1100,24),'Trader (number 20)',17);fontfield(row,source,'m_FontStyle',1)
    reason,_=label(row,'Rule explanation',tr,(58,38,610,68),'Actual rule',15)
    changes,_=label(row,'Before and after',tr,(700,36,465,72),'2 → 1 votes',15)
    rowComponent=row.mono(go,'VoteEffectRow',f'''  source: {ref(source)}
  reason: {ref(reason)}
  changes: {ref(changes)}
  icon: {ref(im)}
  background: {ref(bg)}
  ballotIcon: {art('ballot-token')}
  detectiveIcon: {art('detective-evidence')}
  snooperIcon: {art('snooper-eye')}
  gamblerIcon: {art('gambler-wager')}
''')
    rowpath.write_text(row.header+''.join(f'--- !u!{row.types[n]} &{n}\n{body}' for n,body in row.records.items() if n!=9223372036854775807),encoding='utf-8')
    ensure_meta(rowpath)
else:
    row=Scene('Assets/Prefabs/VoteEffectRow.prefab',True);rowComponent=row.find_script(SCRIPTS['VoteEffectRow'])

for scene in ['MainBoard','TutorialBoard']:
    s=Scene(f'Assets/Scenes/{scene}.unity',True)
    if s.find_script(SCRIPTS['VoteEffectsView']) is not None:continue
    tm=s.find_script(guid('Assets/Scripts/TurnManager.cs'));tmGo=s.game_object(tm)
    feed=s.mono(tmGo,'VoteEffectFeed');s.set(tm,'voteEffects',ref(feed))
    ui=s.find_script(guid('Assets/Scripts/GameStartUI.cs'));uiGo=s.game_object(ui);hand=7000000000000000108
    controller=s.mono(uiGo,'VoteEffectsView')
    _,_,button,buttonLabel=s.button('Vote details',hand,(604,13,246,48),'Vote details',controller,'Toggle','VoteEffectsView')
    panel,panelTr=s.panel('Vote resolution · original to final',hand,(14,64,1216,214),False)
    title,_=label(s,'Resolution heading',panelTr,(16,10,650,26),'VOTE RESOLUTION',20)
    subtitle,_=label(s,'Resolution explanation',panelTr,(16,42,1184,36),'Original ballots → Neutral effects → Verdict.',15)
    s.button('Replay vote effects',panelTr,(982,8,108,28),'Replay',controller,'Replay','VoteEffectsView')
    s.button('Close vote effects',panelTr,(1100,8,100,28),'Close',controller,'Close','VoteEffectsView')
    scrollGo,scrollTr=s.node('Vote effects scroll',panelTr,(16,84,1184,114));s.image(scrollGo,tint=(0,0,0,0),raycast=True)
    s.mono(scrollGo,'RectMask2D','  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n')
    content,contentTr=s.node('Effect rows',scrollTr,(0,0,1184,0))
    s.mono(content,'VerticalLayoutGroup','''  m_Padding:
    m_Left: 0
    m_Right: 0
    m_Top: 0
    m_Bottom: 0
  m_ChildAlignment: 0
  m_Spacing: 8
  m_ChildForceExpandWidth: 1
  m_ChildForceExpandHeight: 0
  m_ChildControlWidth: 1
  m_ChildControlHeight: 1
  m_ChildScaleWidth: 0
  m_ChildScaleHeight: 0
  m_ReverseArrangement: 0
''')
    s.mono(content,'ContentSizeFitter','  m_HorizontalFit: 0\n  m_VerticalFit: 2\n')
    scroll=s.mono(scrollGo,'ScrollRect',f'''  m_Content: {ref(contentTr)}
  m_Horizontal: 0
  m_Vertical: 1
  m_MovementType: 2
  m_Elasticity: 0.1
  m_Inertia: 1
  m_DecelerationRate: 0.135
  m_ScrollSensitivity: 28
  m_Viewport: {ref(scrollTr)}
  m_HorizontalScrollbar: {{fileID: 0}}
  m_VerticalScrollbar: {{fileID: 0}}
  m_HorizontalScrollbarVisibility: 0
  m_VerticalScrollbarVisibility: 0
  m_HorizontalScrollbarSpacing: 0
  m_VerticalScrollbarSpacing: 0
  m_OnValueChanged:
    m_PersistentCalls:
      m_Calls: []
''')
    empty,_=label(s,'No effects yet',panelTr,(34,110,1148,60),'No public vote changes yet.',17)
    for k,v in {'panel':ref(panel),'rows':ref(contentTr),'rowPrefab':f'{{fileID: {rowComponent}, guid: {guid("Assets/Prefabs/VoteEffectRow.prefab")}, type: 3}}','heading':ref(title),'subtitle':ref(subtitle),'empty':ref(empty),'reviewLabel':ref(buttonLabel),'scroll':ref(scroll),'reviewButton':ref(button)}.items():s.set(controller,k,v)
    s.save()
    # Solid inset and last-sibling presentation keep the fan from covering the audit.
    s=Scene(f'Assets/Scenes/{scene}.unity',True)
    controller=s.find_script(SCRIPTS['VoteEffectsView'])
    panel=int(re.search(r'^  panel: \{fileID: (\d+)\}',s.records[controller],re.M)[1]);tr=s.component(panel,type_=224)
    if not any('m_Name: "Vote audit opaque backing"' in b for b in s.records.values()):
        fill,fillTr=s.node('Vote audit opaque backing',tr,(8,8,1200,198));s.image(fill,tint=(.12,.024,.033,1))
        s.set_children(tr,[fillTr]+[c for c in s.children(tr) if c!=fillTr])
    s.save()
print('Authored compact vote badges, mark icons, activation borders and resolution panels.')
SCRIPTS['LocalizedLabel']=guid('Assets/Scripts/LocalizedLabel.cs')
for scene in ['MainBoard','TutorialBoard']:
    s=Scene(f'Assets/Scenes/{scene}.unity',True)
    controller=s.find_script(SCRIPTS['VoteEffectsView'])
    panel=int(re.search(r'^  panel: \{fileID: (\d+)\}',s.records[controller],re.M)[1]);tr=s.component(panel,type_=224)
    if not any('m_Name: "Vote audit opaque backing"' in b for b in s.records.values()):
        fill,fillTr=s.node('Vote audit opaque backing',tr,(8,8,1200,198));s.image(fill,tint=(.12,.024,.033,1))
        s.set_children(tr,[fillTr]+[c for c in s.children(tr) if c!=fillTr])
    for n,body in list(s.records.items()):
        if s.types[n]!=1:continue
        if not any('m_Name: "'+name+'"' in body for name in ['Replay vote effects Label','Close vote effects Label']):continue
        text=s.component(n,script=SCRIPTS['Text'])
        if text is not None and s.component(n,script=SCRIPTS['LocalizedLabel']) is None:
            value='Replay' if 'Replay vote effects Label' in body else 'Close'
            s.mono(n,'LocalizedLabel',f'  label: {ref(text)}\n  tmpLabel: {{fileID: 0}}\n  originalTextMaterial: {{fileID: 0}}\n  sourceText: {value}\n')
    s.save()

# Resize mark art without changing its public/private state references.
if '--reset-badge-layout' not in sys.argv:
    print('Preserved the manually authored CardUI badge transforms. Use --reset-badge-layout only to explicitly replace them.')
    sys.exit(0)
s=Scene('Assets/Prefabs/CardUI.prefab',True);hud=s.find_script(SCRIPTS['CardVotePresentation'])
def hudref(name):return int(re.search(r'^  '+name+r': \{fileID: (-?\d+)\}',s.records[hud],re.M)[1])
for name,y in [('snooperMotion',-80),('gamblerMotion',-126)]:anchored(s,hudref(name),(1,1),(1,1),(-3,y),(40,40))
anchored(s,hudref('suspicionMotion'),(0,1),(0,1),(3,-22),(76,40))
anchored(s,hudref('voteMotion'),(.5,0),(.5,1),(0,-7),(108,36))
for tr in s.children(hudref('suspicionMotion')):
    go=s.game_object(tr)
    if 'm_Name: "Evidence"' in s.records[go]:s.set(tr,'m_AnchoredPosition','{x: 0, y: 0}');s.set(tr,'m_SizeDelta','{x: 40, y: 40}')
counter=hudref('suspicionCount');ctr=s.component(s.game_object(counter),type_=224)
s.set(ctr,'m_AnchoredPosition','{x: 40, y: 0}');s.set(ctr,'m_SizeDelta','{x: 34, y: 40}')
for key,val in [('m_FontSize',17),('m_MaxSize',17),('m_MinSize',12),('m_BestFit',1)]:fontfield(s,counter,key,val)
count=hudref('voteCount');word=hudref('voteWord')
for text,pos,size,fontsize in [(count,(36,0),(28,36),22),(word,(66,0),(40,36),14)]:
    tr=s.component(s.game_object(text),type_=224);s.set(tr,'m_AnchoredPosition',f'{{x: {pos[0]}, y: {pos[1]}}}');s.set(tr,'m_SizeDelta',f'{{x: {size[0]}, y: {size[1]}}}');fontfield(s,text,'m_FontSize',fontsize);fontfield(s,text,'m_MaxSize',fontsize)
for tr in s.children(hudref('voteMotion')):
    if 'm_Name: "Ballot"' in s.records[s.game_object(tr)]:s.set(tr,'m_AnchoredPosition','{x: 2, y: -2}');s.set(tr,'m_SizeDelta','{x: 32, y: 32}')
s.path.write_text(s.header+''.join(f'--- !u!{s.types[n]} &{n}\n{body}' for n,body in s.records.items()),encoding='utf-8')

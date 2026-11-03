"""Configure notebook slots and token layout for guided tutorial."""
from pathlib import Path
import sys,re,json
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from migrate_scene_ui import Scene,SCRIPTS,UGUI,guid,ensure_meta,ref,sprite,color
ROOT=Path(__file__).resolve().parents[2]
ensure_meta(ROOT/'Assets/Scripts/TutorialSpotlight.cs')
ensure_meta(ROOT/'Assets/Scripts/DialogueModalGuard.cs')
SCRIPTS['LayoutElement']=guid(UGUI+'Layout/LayoutElement.cs')
SCRIPTS['GridLayoutGroup']=guid(UGUI+'Layout/GridLayoutGroup.cs')
SCRIPTS['HorizontalLayoutGroup']=guid(UGUI+'Layout/HorizontalLayoutGroup.cs')
SCRIPTS['DialogueModalGuard']=guid('Assets/Scripts/DialogueModalGuard.cs')
for name in ['TutorialSpotlight','LocalizedLabel','TutorialController','OwnershipLegend','NoteSlot']:
    SCRIPTS[name]=guid('Assets/Scripts/'+name+'.cs')
story=json.loads((Path(__file__).parent/'tutorial_story.json').read_text(encoding='utf-8'))
def save(s):
    if 9223372036854775807 in s.records:s.save()
    else:s.path.write_text(s.header+''.join(f'--- !u!{s.types[i]} &{i}\n{b}' for i,b in s.records.items()),encoding='utf-8')
def text_value(s,i,value):
    field='m_text' if re.search(r'(?m)^  m_text:',s.records[i]) else 'm_Text'
    s.records[i]=re.sub(r'(?ms)^  '+field+r': .*?(?=^  [A-Za-z_][A-Za-z_0-9]*:|\Z)',lambda _: '  '+field+': '+json.dumps(value,ensure_ascii=False)+'\n',s.records[i])
def named(s,name):
    return next((i for i in s.records if s.types[i]==1 and s.value(i,'m_Name').strip('"')==name),None)
def localize(s):
    for i,b in list(s.records.items()):
        if s.types[i]!=114:continue
        tmp='guid: f4688fdb7df04437aeb418b961361dc5,' in b
        legacy=f'guid: {SCRIPTS["Text"]},' in b
        if not(tmp or legacy):continue
        go=s.game_object(i)
        if s.component(go,script=SCRIPTS['LocalizedLabel']) is None:
            material=s.value(i,'m_sharedMaterial') if tmp else '{fileID: 0}'
            s.mono(go,'LocalizedLabel',f'  label: {ref(0 if tmp else i)}\n  tmpLabel: {ref(i if tmp else 0)}\n  originalTextMaterial: {material}\n  sourceText: ""\n')
def clone_slot(s,tr,parent):
    go=s.game_object(tr);ids=[go]+list(map(int,re.findall(r'component: \{fileID: (-?\d+)\}',s.records[go])))
    mapping={i:s.alloc() for i in ids}
    for i in ids:s.types[mapping[i]]=s.types[i];s.records[mapping[i]]=re.sub(r'fileID: (-?\d+)',lambda m:'fileID: '+str(mapping.get(int(m[1]),int(m[1]))),s.records[i])
    s.parent(mapping[tr],parent)
    return mapping[tr]

for path in ['Assets/Scenes/MainBoard.unity','Assets/Scenes/TutorialBoard.unity','Assets/Prefabs/NotePanel.prefab']:
    s=Scene(path,allow_migrated=True);row=1583333837
    slots=[tr for tr in s.children(row) if s.component(s.game_object(tr),script=SCRIPTS['NoteSlot']) is not None]
    if len(slots)==6:slots.append(clone_slot(s,slots[-1],row))
    assert len(slots)==7,(path,len(slots))
    for index,tr in enumerate(slots):
        s.set(s.game_object(tr),'m_Name','"Note Seat '+str(index+1)+'"')
        s.set(tr,'m_AnchoredPosition',f'{{x: {142+136*index}, y: -60}}')
        # Captions are siblings so NoteSlot never mistakes decoration for a draggable token.
        caption='Note Seat '+str(index+1)+' Number'
        if named(s,caption) is None:
            label,label_tr=s.label(caption,row,(94+136*index,-12,96,22),str(index+1),size=15,center=True)
        caption_go=named(s,caption)
        if s.component(caption_go,script=SCRIPTS['LayoutElement']) is None:
            s.mono(caption_go,'LayoutElement','  m_IgnoreLayout: 1\n  m_MinWidth: -1\n  m_MinHeight: -1\n  m_PreferredWidth: -1\n  m_PreferredHeight: -1\n  m_FlexibleWidth: -1\n  m_FlexibleHeight: -1\n  m_LayoutPriority: 1\n')
    grid=s.component(s.game_object(row),script=SCRIPTS['GridLayoutGroup'])
    if grid is not None:
        s.set(grid,'m_ChildAlignment','4');s.set(grid,'m_Spacing','{x: 40, y: 0}');s.set(grid,'m_Constraint','1');s.set(grid,'m_ConstraintCount','7')
    horizontal=s.component(s.game_object(row),script=SCRIPTS['HorizontalLayoutGroup'])
    if horizontal is not None:s.set(horizontal,'m_ChildAlignment','4');s.set(horizontal,'m_Spacing','40')
    # Modal windows must sort above the independent dialogue canvases (order 200).
    template=Scene('Assets/Scenes/MainBoard.unity',allow_migrated=True)
    for go,order in [(1927024418,500)]+([] if path.endswith('.prefab') else [(684432614,400)]):
        canvas=s.component(go,type_=223)
        if canvas is None:
            body=template.records[7000000000000000003]
            body=re.sub(r'm_GameObject: \{fileID: -?\d+\}',f'm_GameObject: {ref(go)}',body)
            canvas=s.add(223,body);s.attach(go,canvas)
        s.set(canvas,'m_OverrideSorting','1');s.set(canvas,'m_SortingOrder',str(order))
        if s.component(go,script=SCRIPTS['GraphicRaycaster']) is None:
            s.mono(go,'GraphicRaycaster','  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n')
        if s.component(go,script=SCRIPTS['DialogueModalGuard']) is None:s.mono(go,'DialogueModalGuard')
    if not path.endswith('.prefab'):
        ui=s.find_script(guid('Assets/Scripts/GameStartUI.cs'));turns=s.find_script(guid('Assets/Scripts/TurnManager.cs'))
        for field,component in [('rulesPanel',ui),('discoverPanel',turns)]:
            go=int(re.search(r'fileID: (-?\d+)',s.value(component,field))[1])
            if go and s.component(go,script=SCRIPTS['DialogueModalGuard']) is None:s.mono(go,'DialogueModalGuard')
    for name,source in [('Note Title',story['ui'][9][0]),('Note Subtitle',story['ui'][10][0]),('Suspects Section Label',story['ui'][11][0]),('Pool Section Label',story['ui'][12][0]),('Bottom Hint',story['ui'][13][0])]:
        go=named(s,name);text=s.component(go,script=SCRIPTS['Text']) if go else None
        if text is None and go is not None:text=s.component(go,script='f4688fdb7df04437aeb418b961361dc5')
        if text is not None:text_value(s,text,source)
    localize(s);save(s)

s=Scene('Assets/Scenes/TutorialBoard.unity',allow_migrated=True)
guide=7000000000000000251;overlay=7000000000000000327
controller=s.find_script(SCRIPTS['TutorialController'])
# Distinct, persistent headings separate action from the purpose of that action.
for name,y,value in [('Tutorial Action Heading',155,story['ui'][0][0]),('Tutorial Purpose Heading',336,story['ui'][1][0])]:
    go=named(s,name)
    if go is None:s.label(name,guide,(20,y,244,20),value,size=13,tint=(.97,.83,.6,1))
    else:text_value(s,s.component(go,script=SCRIPTS['Text']),value)
s.set(7000000000000000270,'m_AnchoredPosition','{x: 20, y: -182}');s.set(7000000000000000270,'m_SizeDelta','{x: 244, y: 140}')
s.set(7000000000000000274,'m_AnchoredPosition','{x: 20, y: -364}');s.set(7000000000000000274,'m_SizeDelta','{x: 244, y: 150}')
spot=s.find_script(SCRIPTS['TutorialSpotlight'])
if spot is None:
    area,area_tr=s.node('Tutorial Spotlight Play Area',overlay,(0,0,100,100))
    s.set(area_tr,'m_AnchorMin','{x: 0, y: 0}');s.set(area_tr,'m_AnchorMax','{x: 1, y: 1}');s.set(area_tr,'m_Pivot','{x: 0.5, y: 0.5}')
    s.set(area_tr,'m_AnchoredPosition','{x: 151, y: -52}');s.set(area_tr,'m_SizeDelta','{x: -314, y: -136}')
    masks=[]
    for index in range(15):
        go,tr=s.node('Tutorial Shade '+str(index+1),overlay,(0,0,100,100),active=False)
        s.set(tr,'m_AnchorMin','{x: 0.5, y: 0.5}');s.set(tr,'m_AnchorMax','{x: 0.5, y: 0.5}');s.set(tr,'m_Pivot','{x: 0.5, y: 0.5}')
        s.image(go,tint=(.025,.008,.008,.57),raycast=False);masks.append(tr)
    s.set_children(overlay,[area_tr]+masks+[c for c in s.children(overlay) if c not in masks+[area_tr]])
    spot=s.mono(s.game_object(overlay),'TutorialSpotlight','  overlay: '+ref(overlay)+'\n  area: '+ref(area_tr)+'\n  masks:\n'+''.join('  - '+ref(tr)+'\n' for tr in masks))
s.set(controller,'spotlight',ref(spot));s.set(controller,'sourceActionLabel',ref(7000000000000000336));s.set(controller,'targetActionLabel',ref(7000000000000000344))
for frame,image,caption,text in [(7000000000000000330,7000000000000000332,7000000000000000334,7000000000000000336),(7000000000000000338,7000000000000000340,7000000000000000342,7000000000000000344)]:
    s.set(image,'m_Sprite','{fileID: 0}');s.set(image,'m_Type','0');s.set(image,'m_FillCenter','1')
    s.set(image,'m_Color',color((1,.85,.35,.15) if frame==7000000000000000330 else (.48,1,.7,.15)))
    tint=(1,.85,.35,.95) if frame==7000000000000000330 else (.48,1,.7,.95)
    for edge,anchor_min,anchor_max,pivot,pos,size in [
        ('Top',(0,1),(1,1),(.5,0),(0,0),(0,2)),('Bottom',(0,0),(1,0),(.5,1),(0,0),(0,2)),
        ('Left',(0,0),(0,1),(1,.5),(0,0),(2,0)),('Right',(1,0),(1,1),(0,.5),(0,0),(2,0))]:
        name='Focus Edge '+edge+' '+str(frame)
        if named(s,name) is None:
            go,tr=s.node(name,frame,(0,0,2,2));s.image(go,tint=tint,raycast=False)
            s.set(tr,'m_AnchorMin',f'{{x: {anchor_min[0]}, y: {anchor_min[1]}}}');s.set(tr,'m_AnchorMax',f'{{x: {anchor_max[0]}, y: {anchor_max[1]}}}')
            s.set(tr,'m_Pivot',f'{{x: {pivot[0]}, y: {pivot[1]}}}');s.set(tr,'m_AnchoredPosition',f'{{x: {pos[0]}, y: {pos[1]}}}');s.set(tr,'m_SizeDelta',f'{{x: {size[0]}, y: {size[1]}}}')
    s.set(caption,'m_SizeDelta','{x: 248, y: 34}');s.set(caption,'m_AnchoredPosition','{x: 0, y: 8}')
    s.records[text]=re.sub(r'(?m)^    m_FontSize:.*$', '    m_FontSize: 14',s.records[text]);s.records[text]=s.records[text].replace('m_FontStyle: 0','m_FontStyle: 1')
    go=s.game_object(caption)
    # A Text and Image must be separate Graphics, with the background rendered first.
    legacy_image=s.component(go,script=SCRIPTS['Image'])
    if legacy_image is not None:
        ids=list(map(int,re.findall(r'component: \{fileID: (-?\d+)\}',s.records[go])))
        renderers=[i for i in ids if s.types[i]==222];remove=[legacy_image]+renderers[1:]
        s.components(go,[i for i in ids if i not in remove])
        for i in remove:s.records.pop(i,None);s.types.pop(i,None)
    bg_name='Tutorial Callout Background '+str(frame)
    if named(s,bg_name) is None:
        bg,bg_tr=s.node(bg_name,frame,(0,0,248,34))
        s.set(bg_tr,'m_AnchorMin','{x: 0.5, y: 1}');s.set(bg_tr,'m_AnchorMax','{x: 0.5, y: 1}');s.set(bg_tr,'m_Pivot','{x: 0.5, y: 0}')
        s.set(bg_tr,'m_AnchoredPosition','{x: 0, y: 8}')
        s.image(bg,sprite('painted-panel'),sliced=True,ppu=16,raycast=False)
        s.set_children(frame,[bg_tr]+[c for c in s.children(frame) if c!=bg_tr])
localize(s);save(s)

lesson=ROOT/'Assets/UI/Tutorial/FirstMatchLesson.asset'
source=lesson.read_text(encoding='utf-8')
blocks=re.split(r'(?m)(?=^  - stage: )',source)
for i in range(1,len(blocks)):
    stage=int(re.search(r'  - stage: (\d+)',blocks[i])[1]);updates={'purpose':story['purposes'][stage][0]}
    if 'successes' in story:updates['success']=story['successes'][stage][0]
    updates.update({key:pair[0] for key,pair in story['overrides'].get(str(stage),{}).items()})
    for field,value in updates.items():blocks[i]=re.sub(r'(?m)^    '+field+r': .*$',lambda _: '    '+field+': '+json.dumps(value,ensure_ascii=False),blocks[i])
lesson.write_text(''.join(blocks),encoding='utf-8')
# Retain font and default-language settings when updating the catalog.
catalog=ROOT/'Assets/UI/Localization/GameLocalization.asset';head=catalog.read_text(encoding='utf-8').split('  entries:\n')[0]
entries=json.loads((Path(__file__).parent/'catalog.json').read_text(encoding='utf-8'))['entries']
tail='  entries:\n'+''.join('  - key: '+json.dumps(e['key'],ensure_ascii=False)+'\n'+''.join('    '+k+': '+json.dumps(e[k],ensure_ascii=False)+'\n' for k in ['source','english','vietnamese']) for e in entries)
catalog.write_text(head+tail,encoding='utf-8')
print('Saved seven notebook seats, stronger tutorial focus, bilingual action/purpose headings and 27 revised explanations.')

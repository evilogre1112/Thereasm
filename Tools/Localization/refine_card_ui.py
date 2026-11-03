"""Update card presentation UI and localized text bindings."""
import sys,json,re
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from migrate_scene_ui import Scene,SCRIPTS,guid,ref,sprite,color
ROOT=Path(__file__).resolve().parents[2]
SCRIPTS['LocalizedLabel']=guid('Assets/Scripts/LocalizedLabel.cs')
SCRIPTS['CardOwnershipView']=guid('Assets/Scripts/CardOwnershipView.cs')

def save_prefab(s):
    s.path.write_text(s.header+''.join(f'--- !u!{s.types[i]} &{i}\n{b}' for i,b in s.records.items()),encoding='utf-8')

def remove_name_cover(s):
    # Keep the English card names in the artwork visible.
    names=['Localized Printed Name','Localized Card Name','Localized printed name']
    roots=[i for i in s.records if s.types[i]==1 and s.value(i,'m_Name').strip('"') in names]
    def remove_tree(go):
        tr=s.component(go,type_=224)
        if tr:
            for child in list(s.children(tr)):remove_tree(s.game_object(child))
            parent=int(re.search(r'm_Father: \{fileID: (-?\d+)\}',s.records[tr])[1])
            if parent:s.set_children(parent,[c for c in s.children(parent) if c!=tr])
        ids=list(map(int,re.findall(r'component: \{fileID: (-?\d+)\}',s.records[go])))+[go]
        for i in ids:s.records.pop(i,None);s.types.pop(i,None)
    for go in roots:remove_tree(go)
    for i,b in list(s.records.items()):
        if s.types[i]==114 and f'guid: {SCRIPTS["CardOwnershipView"]},' in b:
            s.records[i]=re.sub(r'(?m)^  nameLabel:.*\n','',b)

def centered(s,tr,anchor,pos,size,pivot=(.5,.5)):
    s.set(tr,'m_AnchorMin',f'{{x: {anchor[0]}, y: {anchor[1]}}}')
    s.set(tr,'m_AnchorMax',f'{{x: {anchor[0]}, y: {anchor[1]}}}')
    s.set(tr,'m_Pivot',f'{{x: {pivot[0]}, y: {pivot[1]}}}')
    s.set(tr,'m_AnchoredPosition',f'{{x: {pos[0]}, y: {pos[1]}}}')
    s.set(tr,'m_SizeDelta',f'{{x: {size[0]}, y: {size[1]}}}')

s=Scene('Assets/Prefabs/CardUI.prefab',allow_migrated=True)
remove_name_cover(s)
# Ownership label sits between the card corner numbers.
centered(s,7000000000000000003,(.5,1),(0,-3),(104,22),(.5,1))
s.set(7000000000000000005,'m_Sprite',sprite('painted-panel'))
s.set(7000000000000000005,'m_Type','1')
s.set(7000000000000000005,'m_PixelsPerUnitMultiplier','18')
s.records[7000000000000000009]=re.sub(r'(?m)^    m_FontSize:.*$', '    m_FontSize: 12',s.records[7000000000000000009])
s.records[7000000000000000009]=re.sub(r'(?m)^    m_MaxSize:.*$', '    m_MaxSize: 12',s.records[7000000000000000009])
for k,v in {'mineColor':(232/255,202/255,145/255,1),'opponentColor':(201/255,138/255,116/255,1),'neutralColor':(190/255,179/255,162/255,1)}.items():s.set(7000000000000000018,k,color(v))
s.set(7000000000000000001,'m_EffectDistance','{x: 1, y: -1}')
# Use the UI frame for dialogue and center it over its source card.
s.parent(2916674441050353613,7743153093351533012)
centered(s,7543394727792848595,(.5,1),(0,14),(220,112),(.5,0))
s.set(5935317536343511308,'m_AnchoredPosition','{x: 0, y: 0}')
s.set(5935317536343511308,'m_SizeDelta','{x: -34, y: -30}')
bubble=s.component(1802676877374893831,script=SCRIPTS['Image'])
s.set(bubble,'m_Sprite',sprite('painted-panel'));s.set(bubble,'m_Type','1');s.set(bubble,'m_PixelsPerUnitMultiplier','12')
s.set(4978690059932338980,'m_fontColor',color((248/255,236/255,213/255,1)))
s.records[4978690059932338980]=re.sub(r'(?m)^    rgba: \d+','    rgba: 4292218104',s.records[4978690059932338980],count=1)
s.set(4978690059932338980,'m_fontSizeMax','20')
s.set(4978690059932338980,'m_fontSize','20')
# Preserve the inspection text material and FloatingUI animation.
s.set(4238040166034335745,'m_Sprite',sprite('painted-panel'));s.set(4238040166034335745,'m_Type','1');s.set(4238040166034335745,'m_PixelsPerUnitMultiplier','10')
s.set(4238040166034335745,'m_Color','{r: 1, g: 1, b: 1, a: 1}')
save_prefab(s)

# The neutral draft is an independent prefab, so it needs its own localized labels.
s=Scene('Assets/Prefabs/DraftCard.prefab',allow_migrated=True)
s.set(1802676877374893831,'m_IsActive','0')
remove_name_cover(s)
for i,b in list(s.records.items()):
    if s.types[i]!=114 or 'guid: f4688fdb7df04437aeb418b961361dc5,' not in b:continue
    go=s.game_object(i)
    if s.component(go,script=SCRIPTS['LocalizedLabel']) is not None:continue
    material=s.value(i,'m_sharedMaterial')
    s.mono(go,'LocalizedLabel',f'  label: {{fileID: 0}}\n  tmpLabel: {ref(i)}\n  originalTextMaterial: {material}\n  sourceText: ""\n')
save_prefab(s)

for scene in ['MainBoard','TutorialBoard']:
    s=Scene(f'Assets/Scenes/{scene}.unity',allow_migrated=True)
    legend_component=s.find_script(guid('Assets/Scripts/OwnershipLegend.cs'))
    s.set(legend_component,'deck',ref(s.find_script(guid('Assets/Scripts/DeckManager.cs'))))
    for i in list(s.records):
        if s.types[i]!=1:continue
        name=s.value(i,'m_Name').strip('"')
        if name in ['Cur Hand Tab Label','Village Hand Tab Label','Neutral Hand Tab Label']:
            text=s.component(i,script=SCRIPTS['Text'])
            s.records[text]=re.sub(r'(?m)^    m_FontSize:.*$', '    m_FontSize: 16',s.records[text])
            s.records[text]=re.sub(r'(?m)^    m_MaxSize:.*$', '    m_MaxSize: 16',s.records[text])
        if name=='Ownership Legend':
            tr=s.component(i,type_=224)
            centered(s,tr,(0,1),(24,-80),(350,68),(0,1))
            text=s.component(i,script=SCRIPTS['Text'])
            s.records[text]=re.sub(r'(?m)^    m_FontSize:.*$', '    m_FontSize: 17',s.records[text])
            s.records[text]=re.sub(r'(?m)^    m_MaxSize:.*$', '    m_MaxSize: 17',s.records[text])
            s.records[text]=re.sub(r'(?m)^    m_Alignment:.*$', '    m_Alignment: 0',s.records[text])
    s.save()

catalog=ROOT/'Assets/UI/Localization/GameLocalization.asset'
data=json.loads((ROOT/'Tools/Localization/catalog.json').read_text(encoding='utf-8'))
head=catalog.read_text(encoding='utf-8').split('  entries:\n')[0]
lines=['  entries:']
for entry in data['entries']:
    lines.append('  - key: '+json.dumps(entry['key'],ensure_ascii=False))
    for k in ['source','english','vietnamese']:lines.append('    '+k+': '+json.dumps(entry[k],ensure_ascii=False))
catalog.write_text(head+'\n'.join(lines)+'\n',encoding='utf-8')
print('Refined saved card/draft prefabs and hand captions; retained original wave components/materials.')

"""Validate actual saved Unity scenes, direct references and UI region boundaries."""
from pathlib import Path
import itertools
import json
import math
import re
import struct

ROOT=Path(__file__).resolve().parents[1]
CHECKS=0
GENERATED=7000000000000000000

def expect(condition,message):
    global CHECKS
    CHECKS+=1
    if not condition: raise AssertionError(message)

def parse(path):
    source=path.read_text(encoding='utf-8-sig')
    matches=list(re.finditer(r'(?m)^--- !u!(\d+) &(-?\d+)(?: stripped)?\n',source))
    ids=[int(m[2]) for m in matches]
    expect(len(ids)==len(set(ids)),f'Duplicate Unity object ID in {path.name}')
    return {int(m[2]):(int(m[1]),source[m.end():matches[i+1].start() if i+1<len(matches) else len(source)]) for i,m in enumerate(matches)}

def field(body,key):
    m=re.search(rf'(?m)^  {re.escape(key)}: (.*)$',body)
    expect(m is not None,f'Missing serialized field {key}')
    return m[1]

def local(body,key):
    return int(re.search(r'fileID: (-?\d+)',field(body,key))[1])

def v2(body,key):
    return tuple(map(float,re.findall(r'[xy]: (-?[\d.]+)',field(body,key))))

def components(records,go):
    return list(map(int,re.findall(r'component: \{fileID: (-?\d+)\}',records[go][1])))

def get_component(records,go,type_):
    return next((c for c in components(records,go) if records[c][0]==type_),None)

def named(records,name):
    def unity_name(body):
        value=field(body,'m_Name')
        # Unity re-saves quoted names with YAML's \xHH as well as JSON's \uHHHH.
        return json.loads(re.sub(r'\\x([0-9a-fA-F]{2})',r'\\u00\1',value)) if value.startswith('"') else value
    return next((id_ for id_,(t,b) in records.items() if t==1 and unity_name(b)==name),None)

def script_guid(path):
    return re.search(r'^guid: (\w+)',(ROOT/path).read_text(),re.M)[1]

view_guid=script_guid('Assets/Scripts/GameStartUI.cs.meta')
image_guid='fe87c0e1cc204ed48ad3b37840f39efc'
button_guid='4e29b1a8efbd4b44bb3f3716e73f07ff'
guid_index={}
for base in (ROOT/'Assets',ROOT/'Library/PackageCache/com.unity.ugui'):
    for meta in base.rglob('*.meta'):
        text=meta.read_text(encoding='utf-8-sig',errors='ignore')
        match=re.search(r'^guid: (\w+)',text,re.M)
        if match: guid_index[match[1]]=meta

def validate_external(body,context):
    for match in re.finditer(r'\{fileID: (-?\d+), guid: (\w+), type: \d+\}',body):
        file_id,asset_guid=int(match[1]),match[2]
        if asset_guid.startswith('0000000000000000'): continue
        expect(asset_guid in guid_index,f'Unresolved asset GUID {asset_guid} in {context}')
        meta=guid_index[asset_guid]
        if file_id==11500000: expect(meta.name.endswith('.cs.meta'),f'Script reference points at another asset in {context}')
        if meta.name.endswith('.png.meta'):
            metadata=meta.read_text()
            mode=re.search(r'^  spriteMode: (\d+)',metadata,re.M)
            if mode and int(mode[1])==2:
                expect(file_id in map(int,re.findall(r'internalID: (-?\d+)',metadata)),f'Invalid sliced sprite ID {file_id} in {context}')
            elif mode and int(mode[1])==1:
                expect(file_id==21300000,f'Invalid single sprite ID in {context}')

scenes={}
for name in ('MenuScenes','MainBoard','TutorialBoard'):
    path=ROOT/f'Assets/Scenes/{name}.unity'
    records=parse(path); scenes[name]=records
    source=path.read_text()
    controller=[i for i,(t,b) in records.items() if t==114 and f'guid: {view_guid},' in b]
    expect(len(controller)==1,f'{name} must contain exactly one authored UI controller')
    body=records[controller[0]][1]
    canvas_go=local(body,'m_GameObject')
    expect(get_component(records,canvas_go,223) is not None,f'{name}: UI root is missing its real Canvas')
    for key in ('theme','canvasRoot','safeFrame','background','rulesPanel'):
        expect(local(body,key)!=0,f'{name}: missing {key} reference')
    if name!='MenuScenes':
        for key in ('turns','deck','boardLayout','boardArea','phaseLabel','instruction','setupPanel','inputShield','resultPanel'):
            expect(local(body,key)!=0,f'{name}: missing board binding {key}')
        for modal in ('CemeteryViewPanel','DiscoverPanel','DraftPanel','NotePanel'):
            modal_go=named(records,modal)
            expect(modal_go is not None and field(records[modal_go][1],'m_IsActive')=='0',
                   f'{name}: startup modal {modal} covers the board/hand')
        dimmer=named(records,'Canvas Dimmer')
        dimmer_group=get_component(records,dimmer,225)
        expect(dimmer_group is not None and field(records[dimmer_group][1],'m_Alpha')=='0',f'{name}: dimmer starts opaque')
        expect(field(records[dimmer_group][1],'m_BlocksRaycasts')=='0',f'{name}: dimmer blocks initial input')
    for id_,(ty,data) in records.items():
        for target in map(int,re.findall(r'\{fileID: (-?\d+)\}',data)):
            expect(target==0 or target in records,f'{name}: dangling local reference {target} from {id_}')
        if id_>=GENERATED and id_!=9223372036854775807: validate_external(data,f'{name}:{id_}')
        if ty==224 and id_>=GENERATED:
            parent=local(data,'m_Father')
            if parent:
                expect(f'{{fileID: {id_}}}' in records[parent][1].split('m_Father:')[0],f'{name}: transform parent/child mismatch for {id_}')
    cursor=named(records,'Cursor Canvas')
    has_custom_cursor = any(f'guid: {script_guid("Assets/Scripts/CustomCusor.cs.meta")},' in b for _,b in records.values())
    if has_custom_cursor:
        expect(cursor is not None and get_component(records,cursor,223) is not None,f'{name}: cursor is missing its dedicated Canvas')
    # Serialized buttons use persistent events so their handlers are visible in Inspector.
    for id_,(ty,data) in records.items():
        if id_>=GENERATED and ty==114 and f'guid: {button_guid},' in data:
            expect('m_MethodName:' in data and 'm_CallState: 2' in data,f'Unwired authored button: {name}:{id_}')

def rect(records,name):
    go=named(records,name)
    expect(go is not None,f'Missing region {name}')
    body=records[get_component(records,go,224)][1]
    x,y=v2(body,'m_AnchoredPosition');w,h=v2(body,'m_SizeDelta')
    return (x,-y,w,h)

def disjoint(a,b):
    x,y,w,h=a;X,Y,W,H=b
    return x+w<=X or X+W<=x or y+h<=Y or Y+H<=y

regions={n:rect(scenes['MainBoard'],n) for n in ('Header — status and instructions','Controls — buttons and setup','Board Area — move or resize here','Hand Dock — tabs and cards')}
for scene_name in ('MainBoard','TutorialBoard'):
    expect(named(scenes[scene_name],'Toggle Hand Button') is None,f'{scene_name}: obsolete hand-toggle button returned')
    expect('m_MethodName: ToggleHand' not in (ROOT/f'Assets/Scenes/{scene_name}.unity').read_text(encoding='utf-8-sig'),f'{scene_name}: obsolete hand-toggle event returned')
sidebar_rows=[]
for go in (1889529060,7000000000000000050,7000000000000000059):
    body=scenes['MainBoard'][get_component(scenes['MainBoard'],go,224)][1]
    x,y=v2(body,'m_AnchoredPosition');w,h=v2(body,'m_SizeDelta');sidebar_rows.append((x,-y,w,h))
expect(sidebar_rows==[(22,70,240,50),(22,136,240,50),(22,202,240,50)],'Sidebar actions are not compact, uniformly spaced rows')
for (an,a),(bn,b) in itertools.combinations(regions.items(),2): expect(disjoint(a,b),f'Overlapping regions: {an}/{bn}')
for width,height in ((1280,720),(1366,768),(1920,1080),(2560,1440),(1024,768),(3440,1440),(800,600)):
    scale=min(width/1600,height/900);ox=(width-1600*scale)/2;oy=(height-900*scale)/2
    for name,(x,y,w,h) in regions.items():
        expect(ox+x*scale>=0 and oy+y*scale>=0 and ox+(x+w)*scale<=width and oy+(y+h)*scale<=height,f'{name} outside viewport {width}x{height}')

runtime=(ROOT/'Assets/Scripts/GameStartUI.cs').read_text(encoding='utf-8-sig')
expect('PrepareCursorAndTarget' not in runtime,'Regression: runtime cursor Canvas construction returned')
for forbidden in ('Resources.Load','CreateDynamicFontFromOSFont','AddComponent<Canvas>','new GameObject('):
    expect(forbidden not in runtime,f'Runtime UI generation/lookup returned: {forbidden}')
expect('Resources.Load' not in (ROOT/'Assets/Scripts/GameUIAssets.cs').read_text(),'Skin still loads by a runtime resource string')
for path in [ROOT/'Assets/UI/Themes/ThereasmTheme.asset',ROOT/'Assets/Prefabs/DraftCard.prefab']:
    records=parse(path)
    for _,body in records.values(): validate_external(body,path.name)

# Validate saved online controls and their component references.
menu=scenes['MenuScenes']; board=scenes['MainBoard']
mode_names=('Mode — Phe Dân làng','Mode — Phe Cur','Mode — Online')
mode_tiles=[id_ for id_,(ty,b) in menu.items() if ty==1 and field(b,'m_Name').strip('"').startswith('Mode — ')]
expect(len(mode_tiles)==3,'Main menu must expose exactly three game modes')
for name in mode_names: expect(named(menu,name) is not None,f'Missing mode card: {name}')
expect(named(menu,'Mode — Hai người') is None,'Local same-screen mode is still exposed')
expect(named(menu,'Online Play Button') is None,'Online entry is duplicated above its mode card')
menu_source=(ROOT/'Assets/Scenes/MenuScenes.unity').read_text()
expect('m_MethodName: PlaySolo' not in menu_source,'Menu still invokes removed local multiplayer')
expect('PlaySolo' not in (ROOT/'Assets/Scripts/MainMenu.cs').read_text(),'Removed local-play entry API remains')
theme_source=(ROOT/'Assets/UI/Themes/ThereasmTheme.asset').read_text()
for field_name,icon_name in (('villagerModeIcon','mode-village'),('curModeIcon','mode-cur'),('onlineModeIcon','mode-online')):
    icon_path=ROOT/f'Assets/UI/ModeIcons/{icon_name}.png'
    icon_guid=script_guid(f'Assets/UI/ModeIcons/{icon_name}.png.meta')
    expect(icon_guid in field(theme_source,field_name),f'Theme uses a card portrait instead of {icon_name}')
    expect(f'fileID: 21300000, guid: {icon_guid}' in menu_source,f'Mode icon is not saved in scene: {icon_name}')
    header=icon_path.read_bytes()[:33]
    expect(header[25]==6,f'{icon_name} must retain RGBA transparency')
    expect(min(struct.unpack('>II',header[16:24]))>=1024,f'Low-resolution mode icon: {icon_name}')
def scripted(records,path):
    target=script_guid(path+'.meta')
    found=[b for ty,b in records.values() if ty==114 and f'guid: {target},' in b]
    expect(len(found)==1,f'Expected one saved component: {path}')
    return found[0]
lobby=scripted(menu,'Assets/Scripts/OnlineLobbyView.cs')
for key in ('panel','roomCode','status','role','createButton','joinButton','startButton','leaveButton'):
    expect(local(lobby,key) in menu,f'Missing online lobby reference: {key}')
session=scripted(menu,'Assets/Scripts/OnlineSession.cs')
validate_external(session,'Photon session config')
expect('fileID: 11400000, guid:' in field(session,'settings'),'Photon settings must reference a saved config asset')
bridge=scripted(board,'Assets/Scripts/OnlineGameBridge.cs')
for key in ('turns','deck'): expect(local(bridge,key) in board,f'Missing online bridge {key}')
for name,records,keys in (('Menu',menu,('onlineLobby','rulesHint')),('Board',board,('menuButton','swapRoleButton','handDock','rulesHint'))):
    view=scripted(records,'Assets/Scripts/GameStartUI.cs')
    for key in keys: expect(local(view,key) in records,f'{name}: missing online UI binding {key}')
controls=('Online Title','Room Code Caption','Room Code Input','Online Role Status','Choose Cur','Choose Village',
          'Create Room','Join Room','Start Online Match','Online Connection Status','Leave Online Room','Close Online Lobby')
online_rects={name:rect(menu,name) for name in controls}
for name,(x,y,w,h) in online_rects.items():
    expect(x>=0 and y>=0 and x+w<=1000 and y+h<=640,f'Online control outside room panel: {name}')
for (an,a),(bn,b) in itertools.combinations(online_rects.items(),2):
    expect(disjoint(a,b),f'Overlapping online controls: {an}/{bn}')
config=ROOT/'Assets/Networking/PhotonOnlineConfig.asset'
config_records=parse(config)
for _,body in config_records.values(): validate_external(body,config.name)
config_body=next(iter(config_records.values()))[1]
expect(field(config_body,'appIdFusion')=='REMOVED','Wrong Fusion App ID')
expect(field(config_body,'region')=='asia','Both peers must default to the same region')

# Validate the tutorial scene references to gameplay controls and assets.
tutorial=scenes['TutorialBoard']
guide=scripted(tutorial,'Assets/Scripts/TutorialController.cs')
exercise_deck=scripted(tutorial,'Assets/Scripts/DeckManager.cs')
expect(field(exercise_deck,'separateHandCards')=='1','Tutorial hand cards overlap instead of exposing full click targets')
expect(float(field(exercise_deck,'separatedCardGap'))>=16,'Tutorial hand cards need a visible gap')
for key in ('turns','deck','overlay','sourceFrame','targetFrame','directionArrow','sourceOutline','targetOutline',
            'progressFill','progress','title','instruction','purpose','feedback','continueLabel','header','headerHint',
            'continueButton','practiceButton','villageTab','startControl','endControl','checkControl','revealControl',
            'cemeteryControl','setupPanel','resultPanel'):
    expect(local(guide,key) in tutorial,f'Tutorial: missing saved reference {key}')
validate_external(guide,'Tutorial lesson binding')
expect('fileID: 11400000, guid:' in field(guide,'lesson'),'Tutorial lesson must be a direct asset reference')
lesson=ROOT/'Assets/UI/Tutorial/FirstMatchLesson.asset'
lesson_source=lesson.read_text(encoding='utf-8-sig')
stages=list(map(int,re.findall(r'^  - stage: (\d+)',lesson_source,re.M)))
expect(stages==list(range(40)),'Tutorial steps are missing, duplicated or reordered')
for name in ('title','instruction','purpose','success'):
    expect(len(re.findall(rf'^    {name}: ".+"$',lesson_source,re.M))==40,f'Tutorial lacks editable {name} text')
for _,data in parse(lesson).values(): validate_external(data,'Tutorial lesson')
card_guids=re.findall(r'guid: (\w+)',lesson_source.split('  cards:\n')[1].split('  practiceScene:')[0])
numbers=[]
for card_guid in card_guids:
    card=guid_index[card_guid].with_suffix('').read_text(encoding='utf-8-sig')
    number=int(field(card,'cardNumber')); numbers.append(number)
    expect(int(field(card,'owner'))==(0 if number<=3 else 1),'Wrong exercise card ownership')
    if number!=9: expect(local(card,'cardSkill')==0,'Unexpected passive skill could invalidate the exercise')
    else:
        skill_guid=re.search(r'guid: (\w+)',field(card,'cardSkill'))[1]
        skill=guid_index[skill_guid].with_suffix('').read_text(encoding='utf-8-sig')
        expect(int(field(skill,'skillCost'))==5,'Hunter exercise requires the expected five-heart cost')
expect(numbers==[1,2,3,9,10,11,12],'Tutorial must use the fixed seven-card exercise')
build=(ROOT/'ProjectSettings/EditorBuildSettings.asset').read_text()
expect('enabled: 1\n    path: Assets/Scenes/TutorialBoard.unity' in build,'Tutorial scene is not enabled in Build Settings')
expect('m_MethodName: PlayTutorial' in (ROOT/'Assets/Scenes/MenuScenes.unity').read_text(),'Menu tutorial button is not wired')
guide_regions={n:rect(tutorial,n) for n in ('Tutorial Progress','Tutorial Step Title','Tutorial Action Instruction',
    'Tutorial Why This Matters','Tutorial Success Feedback','Tutorial Continue','Tutorial Restart','Tutorial Exit','Tutorial Rulebook')}
for name,(x,y,w,h) in guide_regions.items():
    expect(x>=0 and y>=0 and x+w<=284 and y+h<=744,f'Tutorial guide outside left column: {name}')
for (an,a),(bn,b) in itertools.combinations(guide_regions.items(),2): expect(disjoint(a,b),f'Tutorial guide overlap: {an}/{bn}')
for name in ('Tutorial Source Frame','Tutorial Target Frame'):
    go=named(tutorial,name)
    image=next(tutorial[c][1] for c in components(tutorial,go) if f'guid: {image_guid},' in tutorial[c][1])
    expect(field(image,'m_RaycastTarget')=='0','Tutorial focus frame blocks real input')
    expect(field(image,'m_FillCenter')=='1','Tutorial highlight does not cover the full card')
    alpha=float(re.search(r'a: ([\d.]+)',field(image,'m_Color'))[1])
    expect(0<alpha<=.25,'Tutorial highlight is opaque and hides the card artwork')

# Verify font coverage for the Vietnamese text used by the scenes.
def glyph(font,code):
    data=font.read_bytes(); n=struct.unpack_from('>H',data,4)[0]
    tables={}
    for i in range(n):
        tag,_,offset,length=struct.unpack_from('>4sIII',data,12+16*i);tables[tag]=(offset,length)
    off,_=tables[b'cmap'];count=struct.unpack_from('>H',data,off+2)[0]
    for i in range(count):
        platform,encoding,sub=struct.unpack_from('>HHI',data,off+4+i*8)
        pos=off+sub;fmt=struct.unpack_from('>H',data,pos)[0]
        if fmt==4 and code<=65535:
            segments=struct.unpack_from('>H',data,pos+6)[0]//2
            for j in range(segments):
                end=struct.unpack_from('>H',data,pos+14+j*2)[0]
                start=struct.unpack_from('>H',data,pos+16+segments*2+j*2)[0]
                if start<=code<=end:
                    delta=struct.unpack_from('>h',data,pos+16+segments*4+j*2)[0]
                    ro_pos=pos+16+segments*6+j*2
                    ro=struct.unpack_from('>H',data,ro_pos)[0]
                    val=(code+delta)&65535 if not ro else struct.unpack_from('>H',data,ro_pos+ro+(code-start)*2)[0]
                    if val:return True
        elif fmt==12:
            groups=struct.unpack_from('>I',data,pos+12)[0]
            for j in range(groups):
                start,end,glyph_id=struct.unpack_from('>III',data,pos+16+j*12)
                if start<=code<=end and glyph_id+code-start:return True
    return False

font=ROOT/'Assets/TextMesh Pro/Fonts/LiberationSans.ttf'
for char in set('ĐIỀU KHIỂN LỜI NGUYỀN BẮT ĐẦU DÂN LÀNG SỔ TAY NGHĨA TRANG')-set(' '): expect(glyph(font,ord(char)),f'Font lacks Vietnamese character {char}')
for name in ('village-night','ritual-table','painted-panel'):
    png=ROOT/f'Assets/Resources/ThereasmUI/{name}.png'
    header=png.read_bytes()[:33];w,h=struct.unpack('>II',header[16:24])
    expect(header[:8]==b'\x89PNG\r\n\x1a\n' and w>=1000 and h>=900,f'Invalid artwork: {name}')

print(f'PASS: {CHECKS} saved-scene, asset, event, font and layout checks.')
print('This checks saved configuration and geometry. Runtime/visual verification is a separate Unity Play Mode check.')

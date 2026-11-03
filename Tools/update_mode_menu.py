"""One-time three-mode menu update. Keeps all artwork as editable, direct Sprite refs."""
from migrate_scene_ui import *

def named(s, name):
    return next(i for i,b in s.records.items() if s.types[i]==1 and s.value(i,'m_Name').strip('"')==name)
def descendants(s, rect):
    found=[]
    for child in s.children(rect): found += [child] + descendants(s,child)
    return found
def text(s, root, name):
    go=next(s.game_object(tr) for tr in descendants(s,root) if s.value(s.game_object(tr),'m_Name').strip('"')==name)
    return s.component(go,script=SCRIPTS['Text']),s.component(go,type_=224)
def drop_tree(s, go):
    rect=s.component(go,type_=224)
    transforms=[rect]+descendants(s,rect)
    s.parent(rect,0)
    removed=[]
    for tr in transforms:
        child=s.game_object(tr)
        for comp in list(map(int,re.findall(r'component: \{fileID: (-?\d+)\}',s.records[child]))):
            removed.append(comp)
            s.records.pop(comp,None);s.types.pop(comp,None)
        s.records.pop(child,None);s.types.pop(child,None)
    for id_,body in list(s.records.items()):
        for deleted in removed: body=re.sub(rf'(?m)^  - \{{fileID: {deleted}\}}\n','',body)
        s.records[id_]=body
def icon(name):
    return f'{{fileID: 21300000, guid: {guid(f"Assets/UI/ModeIcons/{name}.png")}, type: 3}}'

s=Scene('Assets/Scenes/MenuScenes.unity',True)
if any('m_Name: "Mode — Online"' in b for b in s.records.values()): raise RuntimeError('Already updated; preserve manual menu edits.')
folder=ROOT/'Assets/UI/ModeIcons';ensure_meta(folder)
template=(ROOT/'Assets/Resources/ThereasmUI/ritual-table.png.meta').read_text()
for name in ('mode-village','mode-cur','mode-online'):
    path=folder/(name+'.png'); ensure_meta(path)
    metadata=re.sub(r'(?m)^guid: .*',f'guid: {guid(path)}',template)
    metadata=re.sub(r'(?m)^  spriteBorder: .*','  spriteBorder: {x: 0, y: 0, z: 0, w: 0}',metadata)
    metadata=re.sub(r'(?m)^  textureCompression: .*','  textureCompression: 0',metadata)
    metadata=re.sub(r'(?m)^    textureCompression: .*','    textureCompression: 0',metadata)
    metadata=re.sub(r'(?m)^    spriteID: .*',f'    spriteID: {uuid.uuid4().hex}',metadata)
    path.with_name(path.name+'.meta').write_text(metadata,encoding='utf-8')
view=s.find_script(SCRIPTS['GameStartUI'])
mode_specs=[
 ('Mode — Phe Dân làng','ĐẤU VỚI MÁY CURSERMANCER','Phe Dân làng','mode-village','Đấu với bot Cursermancer. Quan sát nghi thức,\nsuy luận và dập tắt nguồn lây.'),
 ('Mode — Phe Cur','ĐẤU VỚI MÁY DÂN LÀNG','Phe Cursermancer','mode-cur','Đấu với bot Dân làng. Chọn Cursermancer\nbí mật, lây nhiễm và bảo vệ\nlời nguyền.'),
 ('Mode — Hai người','HAI NGƯỜI · HAI THIẾT BỊ','Online','mode-online','Tạo hoặc vào phòng bằng mã.\nChọn phe Cursermancer hoặc Dân làng\nđể đấu cùng bạn bè.')]
for old,tag,title,art,description in mode_specs:
    tile=named(s,old);root=s.component(tile,type_=224)
    if title=='Online': s.set(tile,'m_Name',scalar('Mode — Online'))
    for name,value in [('Mode Tagline',tag),('Mode Title',title),('Mode Description',description)]:
        component,tr=text(s,root,name);s.set(component,'m_Text',scalar(value))
        if name=='Mode Title':s.position(tr,(170,72,258,48))
        if name=='Mode Description':s.position(tr,(170,126,258,92))
    portrait=next(s.game_object(tr) for tr in descendants(s,root) if s.value(s.game_object(tr),'m_Name').strip('"')=='Portrait — replace Source Image here')
    s.set(portrait,'m_Name',scalar('Mode Symbol — replace Source Image here'))
    s.position(s.component(portrait,type_=224),(24,70,128,128))
    image=s.component(portrait,script=SCRIPTS['Image']);s.set(image,'m_Sprite',icon(art));s.set(image,'m_PreserveAspect',1)
    if title=='Online':
        button_go=next(s.game_object(tr) for tr in descendants(s,root) if s.value(s.game_object(tr),'m_Name').strip('"')=='Play Button')
        button=s.component(button_go,script=SCRIPTS['Button'])
        s.records[button]=re.sub(r'm_Target: \{fileID: \d+\}',f'm_Target: {ref(view)}',s.records[button])
        s.records[button]=s.records[button].replace('MainMenu, Assembly-CSharp','GameStartUI, Assembly-CSharp').replace('m_MethodName: PlaySolo','m_MethodName: OpenOnline')
        label,_=text(s,root,'Play Button Label');s.set(label,'m_Text',scalar('Chơi online'))
# Remove the redundant top-level online entry; the third mode card owns the action now.
drop_tree(s,named(s,'Online Play Button'))
# Old hidden UI must not retain a persistent call to the removed local-play API.
for id_,body in list(s.records.items()):
    if 'm_MethodName: PlaySolo' in body:
        body=re.sub(r'm_Target: \{fileID: \d+\}',f'm_Target: {ref(view)}',body)
        s.records[id_]=body.replace('MainMenu, Assembly-CSharp','GameStartUI, Assembly-CSharp').replace('m_MethodName: PlaySolo','m_MethodName: OpenOnline')
description=named(s,'Difficulty Description')
s.set(s.component(description,script=SCRIPTS['Text']),'m_Text',scalar('Đấu máy · Cấp 1 · 1 Cur\nOnline luôn có 2 Cur; cấp độ chỉ áp dụng khi đấu máy.'))
# Reserve enough title height for the font metrics.
title_go=named(s,'Game Title');title_text=s.component(title_go,script=SCRIPTS['Text'])
s.records[title_text]=s.records[title_text].replace('m_VerticalOverflow: 0','m_VerticalOverflow: 1')
s.save()
theme=ROOT/'Assets/UI/Themes/ThereasmTheme.asset'
data=theme.read_text()
for old,new,art in [('villagerPortrait','villagerModeIcon','mode-village'),('curPortrait','curModeIcon','mode-cur'),('localPlayPortrait','onlineModeIcon','mode-online')]:
    data=re.sub(rf'(?m)^  {old}: .*',f'  {new}: {icon(art)}',data)
theme.write_text(data,encoding='utf-8')
print('Saved three-mode menu, online route and direct generated icon references.')

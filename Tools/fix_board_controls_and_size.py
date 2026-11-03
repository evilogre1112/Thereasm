"""Saved Unity layout/import settings. Preserve image pixels, sprite IDs and references."""
from pathlib import Path
import sys,re,json,struct,shutil
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
from migrate_scene_ui import Scene,SCRIPTS,guid,ref,ensure_meta
backup=ROOT/'Backups/2026-11-01-ui-size'
def preserve(path):
    target=backup/path.relative_to(ROOT)
    if not target.exists():target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,target)
ensure_meta(ROOT/'Assets/Scripts/ButtonPressFeedback.cs')
SCRIPTS['ButtonPressFeedback']=guid('Assets/Scripts/ButtonPressFeedback.cs')
for relative in ['Assets/Scenes/MainBoard.unity','Assets/Scenes/TutorialBoard.unity']:
    path=ROOT/relative;preserve(path);s=Scene(relative,allow_migrated=True)
    end=800393272;tr=s.component(end,type_=224);dock=7000000000000000108
    s.parent(tr,dock)
    s.set(tr,'m_AnchorMin','{x: 1, y: 1}');s.set(tr,'m_AnchorMax','{x: 1, y: 1}')
    s.set(tr,'m_Pivot','{x: 1, y: 1}');s.set(tr,'m_AnchoredPosition','{x: -284, y: -13}')
    s.set(tr,'m_SizeDelta','{x: 290, y: 48}')
    ui=s.find_script(guid('Assets/Scripts/GameStartUI.cs'))
    text=int(re.search(r'fileID: (-?\d+)',s.value(ui,'endLabel'))[1]);label=s.component(s.game_object(text),type_=224)
    s.set(label,'m_AnchorMin','{x: 0, y: 0}');s.set(label,'m_AnchorMax','{x: 1, y: 1}')
    s.set(label,'m_Pivot','{x: 0.5, y: 0.5}');s.set(label,'m_AnchoredPosition','{x: 0, y: 0}');s.set(label,'m_SizeDelta','{x: -24, y: 0}')
    flash_go=next((i for i in s.records if s.types[i]==1 and s.value(i,'m_Name').strip('"')=='End Turn Press Flash'),None)
    if flash_go is None:
        flash_go,flash_tr=s.node('End Turn Press Flash',tr,(0,0,100,100));flash=s.image(flash_go,tint=(1,.83,.43,0),raycast=False)
        s.set(flash_tr,'m_AnchorMin','{x: 0, y: 0}');s.set(flash_tr,'m_AnchorMax','{x: 1, y: 1}')
        s.set(flash_tr,'m_Pivot','{x: 0.5, y: 0.5}');s.set(flash_tr,'m_AnchoredPosition','{x: 0, y: 0}');s.set(flash_tr,'m_SizeDelta','{x: -12, y: -12}')
        children=s.children(tr);children.remove(flash_tr);children.insert(max(0,len(children)-1),flash_tr);s.set_children(tr,children)
    else:flash=s.component(flash_go,script=SCRIPTS['Image'])
    component=s.component(end,script=SCRIPTS['ButtonPressFeedback'])
    if component is None:
        s.mono(end,'ButtonPressFeedback',f'  button: {ref(s.component(end,script=SCRIPTS["Button"]))}\n  flash: {ref(flash)}\n  content: {ref(label)}\n  flashColor: {{r: 1, g: 0.83, b: 0.43, a: 1}}\n  pressedScale: 0.94\n  confirmationSeconds: 0.3\n  pulseWhenAvailable: 1\n')
    if 'TutorialBoard' in relative:
        arrow=s.component(s.game_object(7000000000000000346),script=SCRIPTS['Text'])
        s.records[arrow]=s.records[arrow].replace('m_Alignment: 3','m_Alignment: 4')
        s.set(arrow,'m_RaycastTarget','0')
    s.save()

changed=[]
for folder in ['Assets/Sprites','Assets/Button','Assets/Resources/ThereasmUI','Assets/UI']:
    for path in (ROOT/folder).rglob('*.png'):
        data=path.read_bytes()[:24]
        if data[:8]!=b'\x89PNG\r\n\x1a\n':continue
        width,height=struct.unpack('>II',data[16:24])
        if width*height<65536:continue
        meta=path.with_suffix('.png.meta')
        if not meta.exists():continue
        source=meta.read_text(encoding='utf-8-sig')
        if 'TextureImporter:' not in source:continue
        preserve(meta)
        maximum=int(re.search(r'(?m)^  maxTextureSize: (\d+)',source)[1])
        match=re.search(r'(?ms)^  - serializedVersion: \d+\n    buildTarget: Standalone\n.*?(?=^  - serializedVersion:|^  [A-Za-z_]|\Z)',source)
        block=match[0] if match else ('  - serializedVersion: 4\n    buildTarget: Standalone\n    maxTextureSize: '+str(maximum)+'\n    resizeAlgorithm: 0\n    textureFormat: -1\n    textureCompression: 1\n    compressionQuality: 50\n    crunchedCompression: 0\n    allowsAlphaSplitting: 0\n    overridden: 0\n    ignorePlatformSupport: 0\n    androidETC2FallbackOverride: 0\n    forceMaximumCompressionQuality_BC6H_BC7: 0\n')
        for key,value in [('maxTextureSize',maximum),('textureFormat',25),('textureCompression',2),('compressionQuality',100),('crunchedCompression',0),('overridden',1)]:
            block=re.sub(r'(?m)^    '+key+': .*$',f'    {key}: {value}',block)
        updated=source[:match.start()]+block+source[match.end():] if match else source.replace('  spriteSheet:\n',block+'  spriteSheet:\n')
        assert 'buildTarget: Standalone\n' in updated
        meta.write_text(updated,encoding='utf-8')
        changed.append({'asset':str(path.relative_to(ROOT)).replace('\\','/'),'width':width,'height':height,'standaloneFormat':'BC7','maxTextureSize':maximum,'sourcePixelsUnchanged':True})
report=ROOT/'Tests/Reports/UIAndSizeOptimization';report.mkdir(parents=True,exist_ok=True)
(report/'texture-settings.json').write_text(json.dumps(changed,ensure_ascii=False,indent=2),encoding='utf-8')
print('Saved End Turn beside hand tabs, click feedback and',len(changed),'BC7 texture import overrides. Original pixels/sprite IDs preserved.')
import runpy
runpy.run_path(str(ROOT/'Tools/compact_sidebar.py'))

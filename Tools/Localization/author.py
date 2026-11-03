"""Setup localization and card ownership UI bindings across scenes and prefabs."""
from pathlib import Path
import sys,json,re,uuid
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools'))
from migrate_scene_ui import Scene,SCRIPTS,ref,guid,sprite
for name in ['GameLocalization','LocalizedLabel','LanguageController','CardOwnershipView','OwnershipLegend']:
    SCRIPTS[name]=guid('Assets/Scripts/'+name+'.cs')
SCRIPTS['Outline']=guid('Library/PackageCache/com.unity.ugui/Runtime/UGUI/UI/Core/VertexModifiers/Outline.cs')
catalog_guid='5ed3a45d2c1d47c2ba1ae8e516c6356f'
font_guid=guid('Assets/UI/Localization/Thereasm Bilingual SDF.asset')
catalog=ROOT/'Assets/UI/Localization/GameLocalization.asset'
assert not any(SCRIPTS['LanguageController'] in (ROOT/f'Assets/Scenes/{name}.unity').read_text(encoding='utf-8-sig') for name in ['MenuScenes','MainBoard','TutorialBoard']),'Already authored: edit catalog and scene objects in place.'
entries=json.loads((Path(__file__).parent/'catalog.json').read_text(encoding='utf-8'))['entries']
scalar=lambda value:json.dumps(value,ensure_ascii=False)
text='''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
'''+f'  m_Script: {{fileID: 11500000, guid: {SCRIPTS["GameLocalization"]}, type: 3}}\n  m_Name: GameLocalization\n  m_EditorClassIdentifier: \n  defaultLanguage: 0\n  readableFont: {{fileID: 12800000, guid: {guid("Assets/TextMesh Pro/Fonts/LiberationSans.ttf")}, type: 3}}\n  readableTmpFont: {{fileID: 11400000, guid: {font_guid}, type: 2}}\n  entries:\n'
for entry in entries:
    text+='  - key: '+scalar(entry['key'])+'\n'+''.join('    '+key+': '+scalar(entry[key])+'\n' for key in ['source','english','vietnamese'])
catalog.write_text(text,encoding='utf-8');catalog.with_suffix('.asset.meta').write_text('fileFormatVersion: 2\nguid: '+catalog_guid+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
def findgo(s,name):
    return next(i for i,t in s.types.items() if t==1 and s.value(i,'m_Name').strip('"')==name)
def localize(s):
    for i,body in list(s.records.items()):
        if s.types[i]!=114:continue
        tmp='guid: f4688fdb7df04437aeb418b961361dc5,' in body
        legacy=f'guid: {SCRIPTS["Text"]},' in body
        if not (tmp or legacy):continue
        go=s.game_object(i)
        if s.component(go,script=SCRIPTS['LocalizedLabel']) is not None:continue
        # Room-code input is user-entered. Placeholder/captions remain localizable.
        go_name=s.value(go,'m_Name')
        if 'Room Code Text' in go_name:continue
        key='m_text' if tmp else 'm_Text'
        value=re.search(r'(?ms)^  '+re.escape(key)+r': (.*?)(?=^  [A-Za-z_][A-Za-z_0-9]*:|\Z)',body)[1].rstrip()
        s.mono(go,'LocalizedLabel','  label: '+ref(0 if tmp else i)+'\n  tmpLabel: '+ref(i if tmp else 0)+'\n  sourceText: '+value+'\n')

for name in ['MenuScenes','MainBoard','TutorialBoard']:
    s=Scene('Assets/Scenes/'+name+'.unity',allow_migrated=True)
    assert s.find_script(SCRIPTS['LanguageController']) is None,'Already localized'
    frame=7000000000000000013;root=7000000000000000001
    language=s.mono(root,'LanguageController',f'  catalog: {{fileID: 11400000, guid: {catalog_guid}, type: 2}}\n  previewLanguage: 0\n  englishButton: {{fileID: 0}}\n  vietnameseButton: {{fileID: 0}}\n')
    # Reserve room for language controls in the menu and board sidebar.
    if name=='MenuScenes':bounds=[(842,32,116,32),(966,32,116,32)]
    else:bounds=[(46,167,110,30),(164,167,110,30)]
    buttons=[]
    for caption,method,bounds_ in zip(['English','Tiếng Việt'],['English','Vietnamese'],bounds):
        button_go,button_rect,button,label=s.button('Language '+caption,frame,bounds_,caption,language,method,cls='LanguageController')
        buttons.append(button)
    s.set(language,'englishButton',ref(buttons[0]));s.set(language,'vietnameseButton',ref(buttons[1]))
    if name!='MenuScenes':
        dock=7000000000000000108
        legend,legend_rect=s.label('Ownership Legend',dock,(20,252,1204,22),'Xanh: bài của bạn · Cam: bài đối thủ · Tím: trung lập. Nhãn theo nhóm bài gốc, không tiết lộ vai trò bí mật.',size=13)
        s.set(legend_rect,'m_AnchorMin','{x: 0, y: 0}');s.set(legend_rect,'m_AnchorMax','{x: 1, y: 0}');s.set(legend_rect,'m_Pivot','{x: 0.5, y: 0}');s.set(legend_rect,'m_AnchoredPosition','{x: 0, y: 8}');s.set(legend_rect,'m_SizeDelta','{x: -40, y: 22}')
        labels=[]
        for label_name in ['Cur Hand Tab Label','Village Hand Tab Label','Neutral Hand Tab Label']:
            go=findgo(s,label_name);component=s.component(go,script=SCRIPTS['Text']);labels.append(component)
            s.records[component]=s.records[component].replace('m_FontSize: 18','m_FontSize: 14').replace('m_MaxSize: 18','m_MaxSize: 14')
        s.mono(root,'OwnershipLegend','  curTab: '+ref(labels[0])+'\n  villageTab: '+ref(labels[1])+'\n  neutralTab: '+ref(labels[2])+'\n  legend: '+ref(legend)+'\n')
        s.set(684432616,'m_Spacing','{x: 12, y: 28}')
    localize(s);s.save()

s=Scene('Assets/Prefabs/CardUI.prefab',allow_migrated=True)
root=247635257939008791;visual=7743153093351533012;visual_go=s.game_object(visual)
outline=s.mono(visual_go,'Outline','  m_EffectColor: {r: 0.435, g: 0.863, b: 0.922, a: 0.85}\n  m_EffectDistance: {x: 2, y: -2}\n  m_UseGraphicAlpha: 1\n')
badge,badge_rect=s.node('Ownership Badge - public faction only',visual,(0,0,148,22))
s.set(badge_rect,'m_AnchorMin','{x: 0, y: 1}');s.set(badge_rect,'m_AnchorMax','{x: 1, y: 1}');s.set(badge_rect,'m_Pivot','{x: 0.5, y: 0}');s.set(badge_rect,'m_AnchoredPosition','{x: 0, y: 3}');s.set(badge_rect,'m_SizeDelta','{x: -4, y: 22}')
background=s.image(badge,sprite('painted-panel'),tint=(.28,.23,.23,1),sliced=True,ppu=10,raycast=False)
owner,owner_rect=s.label('Owner Label',badge_rect,(3,1,142,20),'CỦA BẠN',size=13,center=True)
for tr in [owner_rect]:
    s.set(tr,'m_AnchorMin','{x: 0, y: 0}');s.set(tr,'m_AnchorMax','{x: 1, y: 1}');s.set(tr,'m_AnchoredPosition','{x: 0, y: 0}');s.set(tr,'m_SizeDelta','{x: -6, y: -2}');s.set(tr,'m_Pivot','{x: 0.5, y: 0.5}')
s.records[owner]=s.records[owner].replace('m_BestFit: 0','m_BestFit: 1').replace('m_MinSize: 10','m_MinSize: 8')
# Keep the original English names in the artwork unobstructed.
s.mono(root,'CardOwnershipView','  ownerLabel: '+ref(owner)+'\n  badgeBackground: '+ref(background)+'\n  frame: '+ref(outline)+'\n  mineColor: {r: 0.435, g: 0.863, b: 0.922, a: 1}\n  opponentColor: {r: 1, g: 0.722, b: 0.49, a: 1}\n  neutralColor: {r: 0.804, g: 0.686, b: 0.957, a: 1}\n')
# Place inspection text beside the enlarged card.
desc=s.component(findgo(s,'DescriptionPanel'),type_=224)
s.set(desc,'m_AnchorMin','{x: 0.5, y: 0.5}');s.set(desc,'m_AnchorMax','{x: 0.5, y: 0.5}');s.set(desc,'m_Pivot','{x: 0.5, y: 0.5}');s.set(desc,'m_AnchoredPosition','{x: 239, y: 0}');s.set(desc,'m_SizeDelta','{x: 300, y: 360}')
interaction=s.records[7195455903689334091];description_id=int(re.search(r'descriptionText: \{fileID: (-?\d+)\}',interaction)[1])
s.set(description_id,'m_enableAutoSizing',1);s.set(description_id,'m_fontSize',16);s.set(description_id,'m_fontSizeMin',12);s.set(description_id,'m_fontSizeMax',16)
localize(s)
s.path.write_text(s.header+''.join(f'--- !u!{s.types[i]} &{i}\n{body}' for i,body in s.records.items()),encoding='utf-8')
for folder in ['Assets/UI/Localization']:
    p=ROOT/(folder+'.meta')
    if not p.exists():p.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
print('Saved bilingual catalog, language buttons, ownership legends/badges and readable card inspection fonts.')

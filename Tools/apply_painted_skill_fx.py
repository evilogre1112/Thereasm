"""Import skill effect sprites and bind FX prefab references."""
from pathlib import Path
import sys,re,uuid,shutil
from PIL import Image
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'Tools'))
from migrate_scene_ui import Scene,guid
names=['trader-paper','jester-paper','trader-burst','jester-burst','skill-spark','skill-impact']
template=(ROOT/'Assets/UI/ModeIcons/mode-village.png.meta').read_text(encoding='utf-8')
for name in names:
    path=ROOT/f'Assets/UI/SkillFX/{name}.png';meta=path.with_name(path.name+'.meta')
    if meta.exists():continue
    image=Image.open(path);assert image.mode=='RGBA',f'{name}: image needs real alpha'
    bounds=image.getchannel('A').getbbox();assert bounds,f'{name}: empty image'
    l,t,r,b=bounds;l=max(0,l-4);t=max(0,t-4);r=min(image.width,r+4);b=min(image.height,b+4)
    text=re.sub(r'^guid: \w+',lambda _: 'guid: '+uuid.uuid4().hex,template,count=1,flags=re.M)
    text=re.sub(r'(maxTextureSize:) \d+',r'\g<1> 256',text)
    text=re.sub(r'^  spriteMode: \d+','  spriteMode: 2',text,flags=re.M)
    text=text.replace('  internalIDToNameTable: []',f'  internalIDToNameTable:\n  - first:\n      213: 21300000\n    second: {name}')
    sheet=f'''  spriteSheet:
    serializedVersion: 2
    sprites:
    - serializedVersion: 2
      name: {name}
      rect:
        serializedVersion: 2
        x: {l}
        y: {image.height-b}
        width: {r-l}
        height: {b-t}
      alignment: 0
      pivot: {{x: 0.5, y: 0.5}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      customData:
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: {uuid.uuid4().hex}
      internalID: 21300000
      vertices: []
      indices:
      edges: []
      weights: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable:
      {name}: 21300000
'''
    text=re.sub(r'  spriteSheet:.*?(?=  mipmapLimitGroupName:)',lambda _:sheet,text,flags=re.S)
    meta.write_text(text,encoding='utf-8')
def sprite(name):return '{fileID: 21300000, guid: '+guid(f'Assets/UI/SkillFX/{name}.png')+', type: 3}'
mapping={'SkillBallotPaper':('trader-paper','jester-paper'),'SkillComicBurst':('trader-burst','jester-burst'),
         'SkillComicSpark':('skill-spark','skill-spark'),'SkillVoteImpact':('skill-impact','skill-impact')}
for prefab,(trader,jester) in mapping.items():
    rel=f'Assets/Prefabs/Effects/{prefab}.prefab';backup=ROOT/'Backups/2026-11-01-painted-skill-fx'/rel
    if not backup.exists():backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/rel,backup)
    s=Scene(rel,True);component=s.find_script(guid('Assets/Scripts/SkillFxGraphic.cs'))
    for field,value in [('m_Sprite',sprite(trader)),('traderSprite',sprite(trader)),('jesterSprite',sprite(jester)),
                        ('m_Type','0'),('m_PreserveAspect','1'),('m_FillCenter','1'),('m_UseSpriteMesh','0'),('m_PixelsPerUnitMultiplier','1')]:s.set(component,field,value)
    s.records[component]=re.sub(r'(?m)^  (paperColor|inkColor|stroke):.*\n','',s.records[component])
    s.path.write_text(s.header+''.join(f'--- !u!{s.types[n]} &{n}\n{body}' for n,body in s.records.items()),encoding='utf-8')
print('Bound six painted sprite assets. PNG pixels and CardUI geometry unchanged.')

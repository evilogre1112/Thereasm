"""Bake original short sound designs to WAV and serialize editable Unity references.

Offline synthesis uses its own fixed seed; none of it runs in the game.
Existing music, click sounds and CardUI prefab are untouched.
"""
from pathlib import Path
import json, math, re, shutil, sys, uuid, wave
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
from migrate_scene_ui import Scene,SCRIPTS,guid,ensure_meta,ref
SR=22050
OUT=ROOT/'Assets/AudioClip/Skills'
OUT.mkdir(parents=True,exist_ok=True)
ensure_meta(OUT)
for name in ['SkillAudio','SkillSoundBank']:
    ensure_meta(ROOT/f'Assets/Scripts/{name}.cs')
    SCRIPTS[name]=guid(f'Assets/Scripts/{name}.cs')

def sound(seconds,seed):
    t=np.arange(round(seconds*SR))/SR
    rng=np.random.default_rng(seed)
    n=rng.normal(0,1,len(t))
    # Paper grain / breath, without a piercing white-noise top end.
    grain=np.convolve(n,np.ones(7)/7,mode='same')
    return t,np.zeros_like(t),grain

def tone(t,freq,decay=10,start=0,amp=1,glide=0):
    u=np.maximum(0,t-start)
    return amp*np.sin(2*np.pi*(freq*u+.5*glide*u*u))*np.exp(-u*decay)*(t>=start)*np.minimum(u/.003,1)

def bell(t,freq,start=0,amp=1):
    return sum(tone(t,freq*ratio,decay,start,amp*weight) for ratio,decay,weight in [(1,7,1),(2.71,13,.23),(4.13,22,.09)])

def noise(t,n,start,duration,amp=1):
    u=(t-start)/duration
    return n*amp*np.sin(np.pi*np.clip(u,0,1))**2*((u>=0)&(u<=1))

def tap(t,n,start=0,freq=160,amp=1):
    u=np.maximum(0,t-start)
    return tone(t,freq,65,start,amp)+n*amp*1.1*np.exp(-u*95)*(t>=start)*np.minimum(u/.001,1)

def build(key,seed):
    duration={'Hunter':.46,'Witch':.74,'Invoker':.76,'Sacrifier':.74,'Preacher':.69,'Cursermancer':.67,
              'PaperThrow':.28,'PaperLand':.16,'NoEffect':.22}.get(key,.57)
    t,y,n=sound(duration,seed)
    if key=='PaperThrow':
        y=noise(t,n,.01,.25,2.7)*(1+.35*np.sin(2*np.pi*39*t))
        y+=tone(t,280,18,.025,.11,1000)
    elif key=='PaperLand': y=tap(t,n,.006,190,.65)+noise(t,n,.015,.095,.8)
    elif key=='Trader':
        for i,f in enumerate([430,640,850]):y+=tap(t,n,.015+i*.08,210,.35)+bell(t,f,.04+i*.08,.28)
        y+=noise(t,n,.04,.25,.7)
    elif key=='Jester':
        for i,f in enumerate([440,660,390]):y+=tone(t,f,18,.02+i*.09,.55,650 if i<2 else -500)+tap(t,n,.02+i*.09,190,.35)
    elif key=='Snooper': y=noise(t,n,.01,.27,1.3)+bell(t,890,.1,.25)+tone(t,530,16,.02,.17,1000)
    elif key=='Gambler':
        for i in range(5):y+=tap(t,n,.01+i*.045,370+i*20,.3)
        y+=bell(t,880,.21,.42)+bell(t,1320,.25,.22)
    elif key=='GamblerLoss':
        for i,f in enumerate([520,390,260]):y+=tap(t,n,.02+i*.08,f,.45)
        y+=noise(t,n,.13,.26,1.1)
    elif key=='Hunter':
        y=tap(t,n,.009,83,1)+noise(t,n,.004,.045,4.5)+noise(t,n,.035,.22,.8)
        y+=tone(t,130,24,.018,.65,-170)
    elif key=='Witch':
        y=noise(t,n,.01,.62,1.8)+tone(t,180,5,.035,.3,900)+bell(t,760,.28,.22)
        y+=tone(t,188,6,.035,.13,940)
    elif key=='Invoker':
        y=noise(t,n,.01,.52,1.25)+tone(t,290,6,.02,.22,700)+bell(t,660,.2,.45)+bell(t,990,.25,.19)
    elif key=='Detective': y=tap(t,n,.015,320,.7)+tap(t,n,.09,510,.28)+bell(t,820,.1,.1)
    elif key=='Preacher': y=bell(t,660,.02,.5)+bell(t,990,.07,.25)+noise(t,n,.02,.3,.3)
    elif key=='Elder': y=tap(t,n,.018,125,.85)+tone(t,220,13,.06,.3)+tap(t,n,.095,180,.32)
    elif key=='Saboteur': y=noise(t,n,.008,.12,2)+tap(t,n,.012,650,.38)+tone(t,340,18,.06,.3,-650)
    elif key=='GameMaster':
        y=noise(t,n,.015,.37,1.5)
        for i,f in enumerate([220,330,440]):y+=tap(t,n,.04+i*.09,f,.35)+bell(t,f*2,.05+i*.09,.18)
    elif key=='Prankster': y=tone(t,180,11,.015,.6,2400)+tap(t,n,.02,160,.55)+tap(t,n,.17,510,.3)
    elif key=='Blind': y=bell(t,510,.02,.45)+bell(t,765,.13,.25)+noise(t,n,.04,.38,.6)
    elif key=='Totem': y=tap(t,n,.02,160,.7)+bell(t,320,.025,.26)+bell(t,480,.05,.14)
    elif key=='VillageChief': y=tap(t,n,.015,130,.85)+tap(t,n,.16,170,.65)+bell(t,440,.19,.12)
    elif key=='Controller': y=noise(t,n,.01,.31,1.6)+tone(t,680,10,.02,.24,-1000)+tap(t,n,.28,170,.72)
    elif key=='Sacrifier': y=bell(t,175,.02,.6)+bell(t,262,.07,.25)+noise(t,n,.08,.45,.5)
    elif key=='Cursermancer': y=noise(t,n,.01,.51,1.6)+tone(t,155,7,.03,.4,-90)+bell(t,466,.19,.14)
    elif key=='NoEffect': y=tap(t,n,.012,160,.32)+noise(t,n,.018,.1,.3)
    else: raise ValueError(key)
    # Gentle edge fades and a fixed headroom ceiling; no clipping or DC offset.
    y-=np.mean(y)
    y*=np.minimum(t/.004,1)*np.minimum((duration-t)/.025,1)
    y=np.tanh(y*.85)
    y*=.66/max(np.max(np.abs(y)),.001)
    path=OUT/(re.sub(r'(?<!^)(?=[A-Z])','-',key).lower()+'.wav')
    with wave.open(str(path),'wb') as stream:
        stream.setnchannels(1);stream.setsampwidth(2);stream.setframerate(SR)
        stream.writeframes((y*32767).astype('<i2').tobytes())
    meta=path.with_name(path.name+'.meta')
    if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
    asset_guid=re.search(r'^guid: (\w+)',meta.read_text(),re.M)[1]
    meta.write_text(f'''fileFormatVersion: 2
guid: {asset_guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 22050
    compressionFormat: 1
    quality: 0.8
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
''',encoding='utf-8')
    return dict(key=key,path=str(path.relative_to(ROOT)).replace('\\','/'),duration=duration,bytes=path.stat().st_size,guid=asset_guid)

keys=['Trader','Jester','Snooper','Gambler','GamblerLoss','Hunter','Witch','Invoker','Detective','Preacher','Elder','Saboteur',
      'GameMaster','Prankster','Blind','Totem','VillageChief','Controller','Sacrifier','Cursermancer','PaperThrow','PaperLand','NoEffect']
manifest=[build(key,804+i) for i,key in enumerate(keys)]
bank=ROOT/'Assets/AudioClip/Skills/CardSkillSounds.asset'
ensure_meta(bank)
bank.write_text(f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPTS['SkillSoundBank']}, type: 3}}
  m_Name: CardSkillSounds
  m_EditorClassIdentifier:
  cues:
'''+''.join(f"  - key: {m['key']}\n    clip: {{fileID: 8300000, guid: {m['guid']}, type: 3}}\n    volume: {0.6 if m['key'] in ['PaperThrow','PaperLand'] else 0.8}\n" for m in manifest),encoding='utf-8')

backup=ROOT/'Backups/2026-11-02-skill-audio'
for scene in ['MainBoard','TutorialBoard']:
    rel=f'Assets/Scenes/{scene}.unity'
    s=Scene(rel,True)
    if s.find_script(SCRIPTS['SkillAudio']) is not None:continue
    dest=backup/rel;dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists():shutil.copy2(ROOT/rel,dest)
    go,tr=s.node('Card skill audio - editable sound bank',7000000000000000013,(0,0,0,0))
    # Copy the scene's existing serialized 2D source, replacing its playback state.
    template=next(body for id_,body in s.records.items() if s.types[id_]==82)
    template=re.sub(r'm_GameObject: \{fileID: \d+\}',f'm_GameObject: {ref(go)}',template)
    for field,value in [('m_PlayOnAwake','0'),('m_audioClip','{fileID: 0}'),('m_Volume','1'),('m_Pitch','1'),('Loop','0'),('Mute','0'),('DopplerLevel','0')]:
        template=re.sub(rf'(?m)^  {field}:.*$',f'  {field}: {value}',template)
    audio=s.add(82,template);s.attach(go,audio)
    s.mono(go,'SkillAudio',f'''  bank: {{fileID: 11400000, guid: {guid('Assets/AudioClip/Skills/CardSkillSounds.asset')}, type: 2}}
  source: {ref(audio)}
  masterVolume: 0.55
  maxVoices: 8
  soundEnabled: 1
''')
    s.save()

(ROOT/'Docs/SKILL_AUDIO_ASSETS.json').write_text(json.dumps({'source':'Original offline synthesis; no external recordings or runtime synthesis.',
    'format':'Mono PCM16 WAV, 22050 Hz','assets':manifest},indent=2)+'\n',encoding='utf-8')
print(f'Authored {len(manifest)} original skill clips, {sum(m["bytes"] for m in manifest)} bytes; both board scenes reference the sound bank directly.')

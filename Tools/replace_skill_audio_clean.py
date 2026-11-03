"""Update skill audio asset clips with cleaned audio sources."""
from pathlib import Path
import hashlib,json,re,shutil,wave
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
BACKUP=ROOT/'Backups/2026-11-02-clean-skill-audio'
manifest_path=ROOT/'Docs/SKILL_AUDIO_ASSETS.json'
manifest=json.loads(manifest_path.read_text(encoding='utf-8'))

def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
protected=list((ROOT/'Assets/Scripts').rglob('*'))
protected=[p for p in protected if p.is_file()]
protected+=list((ROOT/'Assets/Scenes').rglob('*.unity'))
protected+=list((ROOT/'Assets/Prefabs').rglob('*.prefab'))
protected+=[ROOT/'Assets/AudioClip/Skills/CardSkillSounds.asset']
baseline={str(p.relative_to(ROOT)).replace('\\','/'):digest(p) for p in protected}

def pluck(t,f,start=0,amp=1,decay=15,glide=0,harmonic=.12,attack=.006):
    u=np.maximum(0,t-start)
    phase=2*np.pi*(f*u+.5*glide*u*u)
    env=(1-np.exp(-u/attack))*np.exp(-u*decay)*(t>=start)
    # Keep synthesized harmonics below Nyquist.
    return amp*env*(np.sin(phase)+harmonic*np.sin(2*phase)*np.exp(-u*12))

def chime(t,f,start=0,amp=1,decay=12):
    return pluck(t,f,start,amp,decay,harmonic=.08)+pluck(t,f*2.5,start,amp*.09,decay*1.7,harmonic=0)

def body(t,start=0,amp=1,f=170):
    return pluck(t,f,start,amp,40,-f*.65,harmonic=.16,attack=.003)

def design(key,t):
    y=np.zeros_like(t)
    if key=='PaperThrow':
        y=pluck(t,410,.008,.8,24,2600,.05,.008)+pluck(t,820,.01,.09,30,1200,0)
    elif key=='PaperLand':y=body(t,.005,.8,210)+pluck(t,520,.008,.12,60,harmonic=0,attack=.003)
    elif key=='Trader':
        for i,f in enumerate([523.25,659.25,783.99]):y+=chime(t,f,.015+i*.073,.6,17)
        y+=body(t,.018,.2,190)
    elif key=='Jester':
        for i,f in enumerate([587.33,880,440]):y+=pluck(t,f,.012+i*.087,.6,23,200 if i<2 else -160,.16)
    elif key=='Snooper':y=chime(t,783.99,.014,.6,15)+chime(t,1174.66,.115,.22,20)
    elif key=='Gambler':
        for i,f in enumerate([290,350,420]):y+=body(t,.012+i*.045,.3,f)
        y+=chime(t,880,.16,.65,15)+chime(t,1318.51,.205,.2,19)
    elif key=='GamblerLoss':
        for i,f in enumerate([523.25,392,261.63]):y+=pluck(t,f,.012+i*.082,.6,21,harmonic=.07)
    elif key=='Hunter':
        y=body(t,.005,1,100)+pluck(t,500,.005,.32,65,-1700,.12,.002)
        y+=pluck(t,160,.018,.4,28,-190,.08,.003)
    elif key=='Witch':
        for i,f in enumerate([293.66,440,587.33]):y+=chime(t,f,.018+i*.085,.45,11)
        y+=pluck(t,320,.015,.25,9,700,.05,.025)
    elif key=='Invoker':
        for i,f in enumerate([329.63,493.88,659.25]):y+=chime(t,f,.018+i*.095,.5,10)
    elif key=='Detective':y=body(t,.012,.6,310)+chime(t,830.61,.065,.28,22)
    elif key=='Preacher':y=chime(t,659.25,.02,.55,9)+chime(t,987.77,.07,.22,12)
    elif key=='Elder':y=body(t,.012,.75,135)+pluck(t,220,.045,.28,18,harmonic=.09)
    elif key=='Saboteur':y=pluck(t,740,.012,.55,29,-700,.1)+body(t,.05,.35,185)
    elif key=='GameMaster':
        for i,f in enumerate([392,523.25,659.25]):y+=pluck(t,f,.012+i*.085,.52,17,harmonic=.12)
    elif key=='Prankster':y=pluck(t,230,.012,.7,18,1450,.12)+body(t,.145,.35,420)
    elif key=='Blind':y=chime(t,523.25,.02,.5,12)+chime(t,783.99,.125,.25,15)
    elif key=='Totem':y=body(t,.014,.6,165)+pluck(t,330,.025,.35,14,harmonic=.16)
    elif key=='VillageChief':y=body(t,.012,.75,145)+body(t,.15,.6,180)+chime(t,440,.18,.12,22)
    elif key=='Controller':y=pluck(t,610,.012,.4,14,-800,.08)+body(t,.22,.65,190)
    elif key=='Sacrifier':y=chime(t,196,.02,.6,8)+chime(t,293.66,.07,.2,11)
    elif key=='Cursermancer':y=pluck(t,196,.02,.55,9,-80,.08)+chime(t,392,.1,.15,14)
    elif key=='NoEffect':y=body(t,.008,.5,190)
    else:raise ValueError(key)
    # Apply fades without nonlinear saturation.
    fade=np.sin(np.minimum(t/.004,1)*np.pi/2)**2
    fade*=np.sin(np.minimum(np.maximum(t[-1]-t,0)/.035,1)*np.pi/2)**2
    y-=np.mean(y)
    y*=fade
    peak=.34 if key in ['PaperThrow','PaperLand'] else .40 if key=='NoEffect' else .50
    y*=peak/max(np.max(np.abs(y)),1e-8)
    return y

metrics=[]
for asset in manifest['assets']:
    path=ROOT/asset['path'];meta=path.with_name(path.name+'.meta')
    for original in [path,meta]:
        backup=BACKUP/original.relative_to(ROOT)
        if not backup.exists():backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(original,backup)
    with wave.open(str(path),'rb') as stream:
        sr=stream.getframerate();frames=stream.getnframes()
    t=np.arange(frames)/sr;y=design(asset['key'],t)
    pcm=np.rint(y*32767).astype('<i2')
    with wave.open(str(path),'wb') as stream:
        stream.setnchannels(1);stream.setsampwidth(2);stream.setframerate(sr);stream.writeframes(pcm.tobytes())
    # Uncompressed import avoids short-transient codec artifacts. GUID stays intact.
    text=meta.read_text(encoding='utf-8')
    original_guid=re.search(r'^guid: (\w+)',text,re.M)[1]
    text=re.sub(r'(?m)^(    compressionFormat:) .*$',r'\1 0',text)
    meta.write_text(text,encoding='utf-8')
    assert re.search(r'^guid: (\w+)',text,re.M)[1]==asset['guid']==original_guid
    assert np.max(np.abs(y))<=.501 and np.all(np.isfinite(y))
    assert pcm[0]==0 and pcm[-1]==0 and path.stat().st_size==asset['bytes']
    spectrum=np.abs(np.fft.rfft(y*np.hanning(len(y))))**2
    freqs=np.fft.rfftfreq(len(y),1/sr)
    treble=float(spectrum[freqs>5000].sum()/max(spectrum.sum(),1e-20))
    assert treble<.001, (asset['key'],treble)
    metrics.append({'key':asset['key'],'peak':round(float(np.max(np.abs(y))),4),
                    'rms':round(float(np.sqrt(np.mean(y*y))),4),'energy_above_5khz':treble,
                    'sha256':digest(path),'guid_unchanged':True})

after={rel:digest(ROOT/rel) for rel in baseline}
assert after==baseline,'A protected game script, scene, prefab or sound bank changed.'
report={'passed':True,'clips_replaced':len(metrics),'design':'Clean tonal/percussive cues; no noise layers, saturation or codec compression.',
        'runtime_code_scenes_prefabs_bank_unchanged':True,'protected_hashes':baseline,'assets':metrics}
report_dir=ROOT/'Tests/Reports/SkillAudioClean';report_dir.mkdir(parents=True,exist_ok=True)
(report_dir/'AssetChecks.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
manifest['source']='Original clean offline synthesis; no noise layers, distortion or external recordings.'
manifest['import']='PCM uncompressed; original clip GUIDs and timing preserved.'
manifest_path.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print(f'Replaced {len(metrics)} WAVs with clean cues. All game scripts, scenes, prefabs, bank settings and clip GUIDs unchanged.')

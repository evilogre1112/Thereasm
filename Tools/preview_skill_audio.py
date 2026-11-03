"""Export a short, labeled listening sample using the actual baked game clips."""
from pathlib import Path
import json,wave,re
import numpy as np
root=Path(__file__).resolve().parents[1]
manifest=json.loads((root/'Docs/SKILL_AUDIO_ASSETS.json').read_text())
assets={a['key']:a for a in manifest['assets']}
folder=root/'Tests/Reports/SkillAudio';folder.mkdir(parents=True,exist_ok=True)
master=float(re.search(r'(?m)^  masterVolume: ([0-9.]+)$',(root/'Assets/Scenes/MainBoard.unity').read_text(encoding='utf-8-sig'))[1])
keys=['Trader','PaperThrow','PaperLand','Jester','Snooper','Gambler','GamblerLoss','Hunter','Witch','Invoker','Preacher','Detective']
pieces=[];timeline=[];cursor=0
for key in keys:
    with wave.open(str(root/assets[key]['path']),'rb') as stream:
        sr=stream.getframerate();samples=np.frombuffer(stream.readframes(stream.getnframes()),dtype='<i2').astype(float)
    # Match the scene's master volume and the sound bank's gain.
    gain=master*(.6 if key in ['PaperThrow','PaperLand'] else .8)
    pieces.extend([(samples*gain).astype('<i2'),np.zeros(round(sr*.22),dtype='<i2')])
    timeline.append({'sound':key,'at_seconds':round(cursor,3)})
    cursor+=len(samples)/sr+.22
with wave.open(str(folder/'SkillSounds-preview.wav'),'wb') as stream:
    stream.setnchannels(1);stream.setsampwidth(2);stream.setframerate(sr);stream.writeframes(np.concatenate(pieces).tobytes())
(folder/'preview-timeline.json').write_text(json.dumps(timeline,indent=2)+'\n',encoding='utf-8')
print('Saved actual skill sound listening sample:',round(cursor,2),'seconds.')

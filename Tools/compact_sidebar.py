"""Remove the obsolete hand-toggle UI and close the gaps in the saved sidebar."""
from pathlib import Path
import sys,re,shutil
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
from migrate_scene_ui import Scene,SCRIPTS

for relative in ['Assets/Scenes/MainBoard.unity','Assets/Scenes/TutorialBoard.unity']:
    path=ROOT/relative;backup=ROOT/'Backups/2026-11-01-compact-sidebar'/relative
    if not backup.exists():backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,backup)
    s=Scene(relative,allow_migrated=True)
    toggle=next((i for i in s.records if s.types[i]==1 and s.value(i,'m_Name').strip('"')=='Toggle Hand Button'),None)
    if toggle is not None:
        removed=set()
        def collect(go):
            rect=s.component(go,type_=224)
            for child in s.children(rect):collect(s.game_object(child))
            removed.add(go);removed.update(map(int,re.findall(r'component: \{fileID: (-?\d+)\}',s.records[go])))
        collect(toggle)
        tr=s.component(toggle,type_=224);parent=int(re.search(r'fileID: (-?\d+)',s.value(tr,'m_Father'))[1])
        s.set_children(parent,[c for c in s.children(parent) if c!=tr])
        for i in removed:s.records.pop(i,None);s.types.pop(i,None)
        for i,body in list(s.records.items()):
            for dead in removed:
                body=re.sub(r'(?m)^\s*- \{fileID: '+str(dead)+r'\}\n','',body)
                body=body.replace('{fileID: '+str(dead)+'}','{fileID: 0}')
            s.records[i]=body
    if 'MainBoard' in relative:
        # Uniform 50px rows and 16px gaps directly below the title.
        for go,y in [(1889529060,70),(7000000000000000050,136),(7000000000000000059,202)]:
            tr=s.component(go,type_=224)
            s.set(tr,'m_AnchoredPosition',f'{{x: 22, y: -{y}}}');s.set(tr,'m_SizeDelta','{x: 240, y: 50}')
            for child in s.children(tr):
                if s.component(s.game_object(child),script=SCRIPTS['Text']) is not None:
                    s.set(child,'m_AnchorMin','{x: 0, y: 0}');s.set(child,'m_AnchorMax','{x: 1, y: 1}')
                    s.set(child,'m_Pivot','{x: 0.5, y: 0.5}');s.set(child,'m_AnchoredPosition','{x: 0, y: 0}');s.set(child,'m_SizeDelta','{x: -24, y: 0}')
    s.set(s.component(800393272,type_=224),'m_AnchoredPosition','{x: -22, y: -13}')
    s.save()
print('Removed hand-toggle buttons in both scenes; Purge / Notes / Cemetery now form three evenly spaced rows.')

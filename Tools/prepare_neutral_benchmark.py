"""Prepare only the isolated copy: retain decision/rule source, adapt presentation waits."""
from pathlib import Path
import hashlib,json,shutil
root=Path(__file__).resolve().parents[1]
target=root/'Backups/TutorialValidationProject'
manifest=[]
for source in sorted((root/'Assets/Scripts').glob('*.cs')):
    destination=target/'Assets/Scripts'/source.name
    original=source.read_bytes();text=source.read_text(encoding='utf-8-sig')
    count=text.count('new WaitForSeconds(')
    if count:text=text.replace('new WaitForSeconds(','BotBatchPacing.Wait(')
    destination.write_text(text,encoding='utf-8')
    manifest.append({'source':str(source.relative_to(root)).replace('\\','/'),'sha256':hashlib.sha256(original).hexdigest(),
        'presentation_wait_calls':count,'copy_sha256':hashlib.sha256(destination.read_bytes()).hexdigest()})
runtime=target/'Assets/NeutralBenchmark';runtime.mkdir(exist_ok=True)
shutil.copy2(root/'Tests/Players/NeutralTournamentRunner.cs',runtime/'NeutralTournamentRunner.cs')
shutil.copy2(root/'Tests/Unity/NeutralTournamentBuild.cs',target/'Assets/Editor/NeutralTournamentBuild.cs')
output=root/'Tests/Reports/Neutral1000';output.mkdir(exist_ok=True)
(output/'source-manifest.json').write_text(json.dumps({'files':manifest,'transformation':'Only new WaitForSeconds(...) -> BotBatchPacing.Wait(...), in isolated copy only.'},indent=2),encoding='utf-8')
print('Prepared standalone benchmark with',sum(m['presentation_wait_calls'] for m in manifest),'presentation wait adapters; original source untouched.')

"""Extract an official, user-downloaded Unity SDK package, preserving every asset GUID.

Unity imports the resulting assets normally on refresh. Only Assets/Photon paths are accepted.
No tar paths are extracted directly, and SDK code is not modified by this tool.
"""
from pathlib import Path, PurePosixPath
import hashlib
import json
import re
import sys
import tarfile

package=Path(sys.argv[1]).resolve()
destination=Path(sys.argv[2]).resolve()
groups={}
with tarfile.open(package,'r:gz') as archive:
    for member in archive.getmembers():
        parts=PurePosixPath(member.name).parts
        if len(parts)!=2 or not re.fullmatch(r'[0-9a-fA-F]{32}',parts[0]) or not member.isfile(): continue
        if parts[1] in ('pathname','asset','asset.meta'):
            groups.setdefault(parts[0],{})[parts[1]]=archive.extractfile(member).read()
assets=[]
for asset_guid,entries in groups.items():
    name=entries.get('pathname',b'').decode('utf-8-sig').strip().replace('\\','/')
    path=PurePosixPath(name)
    if path.is_absolute() or '..' in path.parts or path.parts[:2]!=('Assets','Photon'):
        raise ValueError('SDK package contains an unexpected asset path: '+name)
    metadata=entries.get('asset.meta',b'')
    if not re.search(rb'(?m)^guid: '+asset_guid.encode()+rb'\s*$',metadata):
        raise ValueError('SDK asset GUID mismatch: '+name)
    output=(destination/Path(*path.parts)).resolve()
    if not output.is_relative_to(destination): raise ValueError('Asset escapes destination: '+name)
    assets.append((output,entries))
if not any(str(p).endswith('Fusion.Runtime.dll') for p,_ in assets): raise ValueError('Not a Fusion SDK package.')
for output,entries in assets:
    if 'asset' in entries:
        output.parent.mkdir(parents=True,exist_ok=True)
        output.write_bytes(entries['asset'])
    else: output.mkdir(parents=True,exist_ok=True)
    output.with_name(output.name+'.meta').write_bytes(entries['asset.meta'])
destination.mkdir(parents=True,exist_ok=True)
report={'package':package.name,'sha256':hashlib.sha256(package.read_bytes()).hexdigest(),'assetCount':len(assets)}
(destination/'FusionPackageImport.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))

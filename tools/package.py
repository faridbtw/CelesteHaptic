#!/usr/bin/env python3
import pathlib, zipfile
root=pathlib.Path(__file__).resolve().parents[1]
release=root/'releases';release.mkdir(exist_ok=True)
output=release/'CelesteHaptic-macOS-v1.0.zip'
files=[]
for name in ['README.md','LICENSE','VALIDATION.txt','build.sh','assets','ThirdParty','src','tests','tools']:
    path=root/name
    files.extend([path] if path.is_file() else [p for p in path.rglob('*') if p.is_file() and not {'bin','obj','__pycache__'}.intersection(p.relative_to(root).parts)])
files.extend(root.glob('*.command'))
files.extend([root/'dist/payload/CelesteHaptic.dll',root/'dist/payload/HapticsBridge',root/'dist/installer-arm64/Installer',root/'dist/installer-x64/Installer'])
with zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as archive:
    for path in sorted(set(files)):
        assert path.name not in {'Celeste.exe','FNA.dll','Steamworks.NET.dll'} and path.suffix!='.log'
        archive.write(path,'CelesteHaptic/'+str(path.relative_to(root)))
with zipfile.ZipFile(output) as archive:
    assert archive.testzip() is None
    names=archive.namelist()
    assert 'CelesteHaptic/Install.command' in names
    for name in ['Install.command','Uninstall.command','dist/payload/HapticsBridge','dist/installer-arm64/Installer','dist/installer-x64/Installer']:
        assert archive.getinfo('CelesteHaptic/'+name).external_attr>>16 & 0o100, name+' lost execute permission'
print(str(output)+' — '+str(round(output.stat().st_size/1024/1024,1))+' MiB, '+str(len(names))+' files; ZIP verified')

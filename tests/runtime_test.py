#!/usr/bin/env python3
"""Run the actual managed sender/native companion without touching game saves."""
import os, pathlib, re, shutil, subprocess, tempfile
root = pathlib.Path(__file__).resolve().parents[1]
game = pathlib.Path.home()/'Library/Application Support/Steam/steamapps/common/Celeste'
resources = game/'Celeste.app/Contents/Resources'
assert subprocess.run(['pgrep','-x','Celeste'],stdout=subprocess.DEVNULL).returncode != 0, 'Quit Celeste before this test'
with tempfile.TemporaryDirectory(prefix='celestehaptic-runtime-') as d:
    sandbox = pathlib.Path(d)
    refs = sandbox/'Celeste.app/Contents/Resources'
    home = sandbox/'CelesteHaptic'
    refs.mkdir(parents=True); home.mkdir()
    for name in ['Celeste.exe','FNA.dll','Steamworks.NET.dll']:
        (refs/name).symlink_to(resources/name)
    shutil.copy2(root/'dist/payload/CelesteHaptic.dll',refs/'CelesteHaptic.dll')
    shutil.copy2(root/'dist/payload/HapticsBridge',home/'HapticsBridge')
    test = sandbox/'RuntimeTests.exe'
    subprocess.run(['csc','-nologo','-out:'+str(test),'-reference:'+str(refs/'Celeste.exe'),'-reference:'+str(refs/'FNA.dll'),'-reference:'+str(refs/'CelesteHaptic.dll'),str(root/'tests/RuntimeTests.cs')],check=True)
    env = dict(os.environ,MONO_PATH=str(refs))
    subprocess.run(['mono',str(test)],env=env,check=True,timeout=25)
    managed = (home/'CelesteHaptic.log').read_text()
    native = (home/'HapticsBridge.log').read_text()
    print(managed); print(native)
    assert managed.count('Gameplay effect:') == 8
    match = re.search(r'Received frames: (\d+); rendered frames: (\d+)',native)
    assert match and int(match[1])>48000 and int(match[2])>48000
    assert 'Game connected.' in native
    assert not subprocess.run(['pgrep','-f',str(home/'HapticsBridge')],stdout=subprocess.DEVNULL).returncode == 0, 'Companion still running after shutdown'
    print('PASS: real waveforms received/rendered, automatic companion lifecycle, clean native shutdown.')

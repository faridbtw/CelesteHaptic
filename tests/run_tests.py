#!/usr/bin/env python3
"""Requires Mono/csc and a built dist. --controller adds a real USB/Bluetooth test."""
import os, pathlib, subprocess, sys, tempfile
root = pathlib.Path(__file__).resolve().parents[1]
game = pathlib.Path(os.environ.get('CELESTE_GAME',pathlib.Path.home()/'Library/Application Support/Steam/steamapps/common/Celeste'))
resources = game/'Celeste.app/Contents/Resources'
with tempfile.TemporaryDirectory(prefix='celestehaptic-tests-') as d:
    temp=pathlib.Path(d)
    synth=temp/'SynthTests.exe'
    subprocess.run(['csc','-nologo','-out:'+str(synth),str(root/'src/Adapter/Synth.cs'),str(root/'tests/SynthTests.cs')],check=True)
    subprocess.run(['mono',str(synth)],check=True)
    refs=temp/'refs';refs.mkdir()
    for name in ['Celeste.exe','FNA.dll','Steamworks.NET.dll']:(refs/name).symlink_to(resources/name)
    adapter=temp/'AdapterTests.exe'
    subprocess.run(['csc','-nologo','-out:'+str(adapter),'-reference:'+str(refs/'Celeste.exe'),'-reference:'+str(refs/'FNA.dll'),'-reference:'+str(root/'dist/payload/CelesteHaptic.dll'),str(root/'tests/AdapterTests.cs')],check=True)
    subprocess.run(['mono',str(adapter)],env=dict(os.environ,MONO_PATH=str(root/'dist/payload')+':'+str(refs)),check=True)
for test in ['install_tests.py','verify_bluetooth.py']:
    subprocess.run([sys.executable,str(root/'tests'/test)],check=True)
subprocess.run([str(root/'dist/payload/HapticsBridge'),'--self-test'],check=True)
if '--controller' in sys.argv:subprocess.run([sys.executable,str(root/'tests/runtime_test.py')],check=True)

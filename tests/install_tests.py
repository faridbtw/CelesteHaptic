#!/usr/bin/env python3
"""Exercise the real installer on a temporary copy of the user's game assembly."""
import hashlib, json, pathlib, shutil, subprocess, tempfile
ROOT = pathlib.Path(__file__).resolve().parents[1]
GAME = pathlib.Path.home() / 'Library/Application Support/Steam/steamapps/common/Celeste'
INSTALLER = ROOT / 'dist/installer-arm64/Installer'
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
with tempfile.TemporaryDirectory(prefix='celestehaptic-installer-') as d:
    game = pathlib.Path(d)
    resources = game / 'Celeste.app/Contents/Resources'
    resources.mkdir(parents=True)
    exe = resources / 'Celeste.exe'
    source = GAME / 'Celeste.app/Contents/Resources/Celeste.exe'
    installed = GAME / 'CelesteHaptic/install.json'
    if installed.exists():
        live = json.loads(installed.read_text())
        if digest(source) == live['PatchedHash']:
            source = GAME / ('CelesteHaptic/backups/Celeste.' + live['OriginalHash'] + '.exe')
    shutil.copy2(source, exe)
    original = digest(exe)
    def run(action, success=True):
        result = subprocess.run([str(INSTALLER), action, str(game), str(ROOT/'dist/payload')], capture_output=True, text=True)
        assert (result.returncode == 0) == success, result.stdout + result.stderr
        print((result.stdout + result.stderr).strip())
    run('install')
    state = json.loads((game/'CelesteHaptic/install.json').read_text())
    assert digest(exe) == state['PatchedHash'] and state['OriginalHash'] == original
    assert state['RumbleHooks'] >= 20 and state['MotorHooks'] > 0
    assert 'MasterGain=1.4' in (game/'CelesteHaptic/settings.ini').read_text()
    run('status'); run('install')
    assert json.loads((game/'CelesteHaptic/install.json').read_text())['OriginalHash'] == original
    run('uninstall')
    assert digest(exe) == original and not (resources/'CelesteHaptic.dll').exists()
    run('uninstall'); run('install')
    exe.write_bytes(exe.read_bytes() + b'simulated-external-change')
    tampered = digest(exe)
    run('uninstall', False); run('install', False)
    assert digest(exe) == tampered
    print('PASS: install, status, reinstall, byte-exact restore, repeated uninstall, changed-game protection.')

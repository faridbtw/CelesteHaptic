# CelesteHaptic

DualSense haptics for Celeste on macOS, through USB-C or Bluetooth.

Dashes get a crisp sweep, landings get a short thump, and climbing and gliding get their own textures. CelesteHaptic turns the game's existing vibration events into stereo waveforms for the controller's haptic actuators. These are custom effects, not an official PS5 waveform library.

## Install

[Download the macOS ZIP](https://github.com/faridbtw/CelesteHaptic/releases/latest/download/CelesteHaptic-macOS-v1.0.zip) and unzip it first.

1. Quit Celeste.
2. Open **Install.command**.
3. Connect your DualSense and launch Celeste from Steam as usual.

Keep vibration enabled in Celeste's options. The companion starts and stops with the game. No extra launch options, controller driver, DSX, or Wine are required.

This version supports the vanilla macOS Steam build, tested with Celeste 1.4.0.0. It does not install into Everest. Apple Silicon and Intel installers are included; Celeste itself still uses its bundled Intel runtime.

For Bluetooth, unplug USB and connect **DualSense Wireless Controller** in macOS Bluetooth settings. The native companion supports reconnection and switching between USB and Bluetooth. When the companion is unavailable, Celeste keeps its normal vibration.

## Adjust the strength

Celeste's **Off / Half / On** vibration setting still applies. Haptics stop when the game is paused or loses focus.

For an additional adjustment, quit the game and open this file in your game folder:

```
CelesteHaptic/settings.ini
```

Set `MasterGain=0.5` for gentler effects, `MasterGain=1.4` for the default, or up to `MasterGain=2.0` for stronger effects. `Enabled=false` restores ordinary rumble while keeping the adapter installed. Restart Celeste after changing the file.

## Remove

Quit Celeste and open **Uninstall.command**. The installer keeps a verified copy of your original `Celeste.exe` in `CelesteHaptic/backups` and restores that exact file. Saves and game settings are retained. It refuses to overwrite game files that changed after installation.

Steam's **Verify integrity of game files** also restores the original game assembly. After a game update, verify the files first, keep your backups, remove the old `CelesteHaptic/install.json`, then run the installer again.

For a custom Steam library, drag the game folder into a Terminal command after the installer path:

```sh
"/path/to/CelesteHaptic/Install.command" "/path/to/steamapps/common/Celeste"
```

## Check the controller

Quit the game, then open **Test Haptics.command**. You should feel the left grip, then the right. For wireless testing, unplug USB, connect Bluetooth, and open **Test Bluetooth Haptics.command**.

If effects are silent, first check Celeste's vibration setting and the macOS connection. Try disabling Steam Input for Celeste and close other controller utilities if they compete for the same controller.

Logs are in the game's `CelesteHaptic` folder: `CelesteHaptic.log` and `HapticsBridge.log`.

## Build and implementation

The adapter intercepts 184 rumble call sites in the tested build and leaves the original rumble methods available as a fallback. It selects effects from each call site's context, mixes at 48 kHz stereo, and streams float PCM over localhost port 48113. Four motor-report guards keep SDL rumble from interrupting the replacement while it is connected.

The native transport comes from [SilkHaptic](https://github.com/faridbtw/SilkHaptic). USB uses the DualSense's actuator audio channels; Bluetooth uses direct HID waveform packets and a filtered 3 kHz stream. Effect synthesis and the Celeste adapter live in `src/Adapter`; the reversible Mono.Cecil installer lives in `src/Installer`.

To build, install Mono (for `csc`), the .NET 8 SDK, and the Xcode command-line tools. Run:

```sh
./build.sh
```

`CELESTE_GAME` selects a custom game folder; `DOTNET_EXE` selects a specific SDK executable. Only your local build reads the game's assemblies. The download contains no Celeste game files or assets.

Validation results and remaining limits are recorded in [VALIDATION.txt](VALIDATION.txt). Source is MIT licensed; bundled dependency licenses are in `ThirdParty`. Celeste belongs to its creators. DualSense is a Sony trademark. This is an independent community mod.

# Universal Bridge+WE

**Universal Bridge+WE — Windows-style keyboard editing on iOS via Bluetooth HID**

This feature branch extends [Universal Bridge](https://github.com/noppy3325-droid/universal-bridge)
with optional Windows-style cursor movement, text selection, and word deletion
for iPhone and iPad. It preserves the original project's history, MIT license,
and normal keyboard/mouse behavior when the editing options are disabled.

The implementation is based on upstream v2.0.1 (`be36f00`). It adds independent
settings for Windows-style text editing, an existing iOS Control/Command swap,
and an optional Control/Command swap inside Universal Bridge. Find them at the
top of **Input settings → Keyboard → Text editing**.

| Windows shortcut | Logical Apple action |
| --- | --- |
| Home / End | Command+Left / Right |
| Ctrl+Home / End | Command+Up / Down |
| Ctrl+Left / Right | Option+Left / Right |
| Ctrl+Up / Down | Option+Up / Down |
| Ctrl+Backspace | Option+Backspace |
| Ctrl+Delete | Option+Forward Delete |

Hold Shift for selection. Existing iOS modifier remapping is accommodated without
applying the UB modifier swap twice to generated navigation actions.

This branch also adds an optional **Toggle Between** shortcut, positioned between
**Return to Windows** and **iPad Home**. Its checkbox defaults to off; the default
binding is **Ctrl+Alt+T**, with the existing modifier and key choices available.
Enable it and click **Apply shortcuts** to use one chord to switch
between Windows and iPad. The separate activation/return shortcuts and triple-Escape
return remain available. See [toggle setup and testing](docs/TOGGLE_SHORTCUT.en.md).

See [setup, modifier behavior, held-key rules, and build/test instructions](docs/WINDOWS_TEXT_NAVIGATION.en.md).

The project owner has confirmed the requested editing behavior works on their
Windows 11 PC and iPhone 16e running iOS 27. Automated tests and macOS-to-Windows
cross-compilation are also available. This does not establish compatibility
with every iOS application, Bluetooth adapter, or modifier configuration.

Application filenames, version metadata, and the optional update checker retain
the upstream v2.0.1 conventions. The update checker still targets the original
project and may offer a future upstream build without these editing changes;
disable automatic update checks if you wish to retain this feature build.

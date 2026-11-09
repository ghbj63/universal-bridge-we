# Optional Toggle Between shortcut

This feature is on `codex/toggle-switch-shortcut`, based on the existing
Universal Bridge+WE text-editing implementation.

In **Input settings → Shortcuts**, **Toggle Between** is immediately below
**Return to Windows** and above **iPad Home**. The checkbox to its left is
**unchecked by default**. Its default binding is **Ctrl+Alt+T**. Choose the
modifier combination and key from the same lists as the existing shortcuts;
F22 is available. Check the box and click **Apply shortcuts** to enable it.
Uncheck it and apply to stop reserving that chord globally. **Defaults** restores
Ctrl+Alt+T and unchecks the box; click Apply to save.

- While Windows controls input, the toggle uses the existing activation path.
  The selected Apple device must be ready, and the hotkey and modifiers must be
  released before input ownership changes.
- While the Apple device controls input, the toggle returns to Windows
  immediately, even if Bluetooth sending is stalled. Its trigger, held repeats,
  and release are consumed so they are not typed on the Apple device.
- The independent activation/return shortcuts and triple Escape are preserved.
- The toggle is independent of the iPad action-shortcut enable checkbox and of
  the text-editing/modifier settings.
- An enabled toggle must differ from all configured UB shortcuts. If another
  Windows application owns the requested hotkey, applying it fails and restores
  the prior registered switching keys and settings.
- Older profiles stay disabled and keep their existing bindings. If an older
  binding already uses Ctrl+Alt+T, the new toggle receives an unused fallback;
  it does not take that binding away from the older action.

The UI and tooltip are provided in English, Japanese, and Simplified Chinese.

## Verification

The automated Core suite covers default-off migration, saved F22 bindings,
conflicts, disabled passthrough, immediate return, held repeats, independent
return, triple Escape, and blocked Bluetooth sending. Existing regression and
localization tests also run on macOS.

The Windows-only `--verify-input` verification additionally checks enabling,
registering, disabling, and rolling back conflicts for the toggle. The UI smoke
verification checks saving, duplicate rejection, and default restoration in all
three UI languages. These Windows checks must be run on Windows; cross-compiling
their code does not run them.

For hardware testing, enable the toggle and apply. Switch Windows → Apple,
release the chord, then switch Apple → Windows with the same chord. Try a held
trigger (one switch only), both modifier-release orders, a disconnected Apple
device, and your custom F22 assignment. Confirm that the independent activation
and return keys and triple Escape still work. Finally disable the toggle and
verify that its chord is no longer intercepted.

# Universal Bridge+WE — Windows-style keyboard editing on iOS via Bluetooth HID

This optional feature translates Windows text-editing keys while Universal Bridge
controls the remote device. All three new settings default to off; existing
profiles retain their original behavior. No Bluetooth descriptor changes or
re-pairing are required by this feature.

## Settings

At the top of the Keyboard section in Input settings, under Text editing:

- **Use Windows-style text navigation and selection** enables the table below.
- **iOS already swaps Control and Command** describes the receiver's Hardware
  Keyboard modifier configuration. This option does not change the iOS settings.
  It affects generated navigation actions only.

  To configure modifier key swapping on an iOS 27.0 (may be different for other
  versions) mobile device, go to Settings → General → Keyboard → Hardware Keyboard
  → Modifier Keys. A physical keyboard (or Universal Bridge) usually must be
  connected to the device for Hardware Keyboard settings to appear.
- **Swap Control and Command in UB** independently exchanges physical left/right
  Control with left/right Windows (HID Command) for ordinary forwarded keys.

For the project's primary test configuration, turn the first two options on and
leave the UB swap off. Keep the existing Control/Command exchange on the iPhone.
For a receiver without an iOS swap, leave the second option off; optionally enable
the UB swap to make ordinary Windows Ctrl+C/V/X/A/Z produce Command shortcuts.
Enabling both swaps cancels their effect on ordinary keys. Generated navigation
is resolved directly to the receiver configuration, so the UB swap is not applied
again to those actions.

Changing any of these settings returns control to Windows and releases remote
input. Release all held keys before switching back to the remote device.

## Mappings

| Windows input | Logical Apple output |
| --- | --- |
| Home / End | Command+Left / Right |
| Ctrl+Home / End | Command+Up / Down |
| Ctrl+Left / Right | Option+Left / Right |
| Ctrl+Up / Down | Option+Up / Down |
| Ctrl+Backspace | Option+Backspace |
| Ctrl+Delete | Option+Forward Delete |

Shift is preserved, including right Shift. Either Control key works. Keypad keys
retain their existing physical keypad usages. Alt/Windows-modified combinations
are passed through, subject to the optional UB modifier swap. Existing UB action
and emergency-return shortcuts retain priority and their existing generated
commands; this change does not repair the built-in Home/Search/Next App actions.

## Held keys and overlapping chords

The mapping is chosen on the initial navigation key-down and remains until that
physical key is released. iOS handles repeat; repeated Windows key-down messages
do not produce additional taps. Releasing Control first does not reinterpret a
held mapped arrow. Shift may be pressed or released during a hold.

One HID report has a shared modifier mask. Compatible mapped arrows can coexist.
If an ordinary key or a different navigation family is pressed during a mapped
hold, the older mapped keys are released and suppressed until their physical
release. Press them again to resume navigation. Adding Alt/Windows also cancels
the mapped hold. If an ordinary non-modifier key was already held, a newly pressed
navigation key is forwarded normally. These rules avoid accidentally applying
Option to a subsequent Ctrl+C, or re-pressing a cancelled arrow automatically.

On a change of navigation modifier context, the previous usages are released
under their old modifiers before the new report. On navigation key-up, still-held
physical modifiers are restored. Disconnection, queue failure, emergency return,
and configuration changes use the existing neutral-report/quarantine mechanism.

## Verification

Automated coverage checks the mapping table with Shift, both iOS layouts and both
UB swap choices; modifier release order; incompatible holds; repeat ownership;
configuration changes while keys are held; reconnection; settings compatibility;
and existing routing/protocol/localization behavior.

The full solution can be cross-compiled on macOS with .NET SDK 10.0.402:

```sh
dotnet build UniversalControl.sln -c Release -p:EnableWindowsTargeting=true
dotnet test tests/UniversalControl.Tests -c Release
```

On Windows, build/test/publish with the existing script:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build.ps1 -Test -Publish -PublishDirectory dist/UniversalBridge-text-navigation-win-x64
```

The project owner confirmed that the requested editing behavior works on their
Windows 11 / iPhone 16e (iOS 27) setup. Other configurations still require hardware
testing; the following checks are a suggested matrix, not a claim that every
combination was manually tested. Start in a disposable multiline note. Check all
rows with and without Shift, held repeat, both release orders, selection wrapping,
word deletion, Ctrl+C/V/X/A/Z after navigation, incompatible overlapping holds,
changing settings while holding a key, disconnect/reconnect, mouse operation and
return to Windows. Repeat key cases in ChatGPT and another text editor. Test the
normal iOS layout and optional UB swap separately if available. App-specific
navigation and deletion behavior must be confirmed on the receiver.

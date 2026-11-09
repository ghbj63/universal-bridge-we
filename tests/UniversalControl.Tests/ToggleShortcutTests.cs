using System.Text.Json;
using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class ToggleShortcutTests
{
    private static void Modifiers(InputRouter router, bool down)
    {
        router.Key(0xA2, 0x1D, false, down, 1);
        router.Key(0xA4, 0x38, false, down, 2);
    }
    [Fact] public void OlderProfilesKeepTheirBindingsAndDefaultToggleOff()
    {
        var old = JsonSerializer.Deserialize<AppSettings>("{\"SchemaVersion\":1}")!.Normalize();
        Assert.False(old.ToggleShortcutEnabled);
        Assert.Equal(new(ShortcutModifiers.Control | ShortcutModifiers.Alt, 'T'), old.ToggleShortcut);
        Assert.Equal(ShortcutBinding.DefaultActivate, old.ActivateShortcut);
        Assert.Equal(ShortcutBinding.DefaultReturn, old.ReturnShortcut);
        var custom = (old with { ActivateShortcut = ShortcutBinding.DefaultToggle }).Normalize();
        Assert.Equal(ShortcutBinding.DefaultToggle, custom.ActivateShortcut);
        Assert.NotEqual(custom.ActivateShortcut, custom.ToggleShortcut);
        Assert.Equal(old.AdditionalShortcuts, custom.AdditionalShortcuts);
    }
    [Fact] public void OlderActionBindingWinsOverTheNewToggleDefault()
    {
        var old = new AppSettings();
        var extras = old.AdditionalShortcuts.Select((entry, index) => index == 0 ? entry with { Binding = ShortcutBinding.DefaultToggle } : entry).ToArray();
        var settings = (old with { AdditionalShortcuts = extras, ToggleShortcut = null! }).Normalize();
        Assert.Equal(ShortcutBinding.DefaultToggle, settings.AdditionalShortcuts[0].Binding);
        Assert.NotEqual(ShortcutBinding.DefaultToggle, settings.ToggleShortcut);
        Assert.False(settings.ToggleShortcutEnabled);
    }
    [Fact] public void ToggleChoiceAndEnabledStateRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), "UB-toggle-" + Guid.NewGuid());
        try
        {
            var store = new SettingsStore(path);
            var desired = new AppSettings { ToggleShortcutEnabled = true, ToggleShortcut = new(ShortcutModifiers.Control | ShortcutModifiers.Alt, 0x85) };
            store.Save(desired); var actual = store.Load();
            Assert.True(actual.ToggleShortcutEnabled); Assert.Equal(desired.ToggleShortcut, actual.ToggleShortcut);
            store.Save(actual with { ToggleShortcutEnabled = false }); actual = store.Load();
            Assert.False(actual.ToggleShortcutEnabled); Assert.Equal(desired.ToggleShortcut, actual.ToggleShortcut);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
    [Fact] public void EnabledToggleCannotConflictWithSwitchingOrActionBindings()
    {
        var s = new AppSettings();
        foreach (var binding in new[] { s.ActivateShortcut, s.ReturnShortcut, s.HomeShortcut, s.SearchShortcut, s.NextAppShortcut })
            Assert.Throws<ArgumentException>(() => ShortcutBinding.ValidateAll(s.ActivateShortcut, s.ReturnShortcut, s.HomeShortcut, s.SearchShortcut, s.NextAppShortcut, binding));
        Assert.Throws<ArgumentException>(() => ActionShortcuts.Validate(s.AdditionalShortcuts, s.AdditionalShortcuts[0].Binding));
        Assert.Throws<ArgumentException>(() => ShortcutBinding.ValidateToggle(new(0, 'T')));
        ShortcutBinding.ValidateAll(s.ActivateShortcut, s.ReturnShortcut, s.HomeShortcut, s.SearchShortcut, s.NextAppShortcut, s.ToggleShortcut);
        ShortcutBinding.ValidateToggle(null, s.ActivateShortcut); // Disabled toggle reserves no OS shortcut.
    }
    [Theory]
    [InlineData(0x54, 0x14u)] // T
    [InlineData(0x85, 0u)] // F22
    public async Task ToggleReturnsImmediatelyConsumesRepeatsAndKeepsIndependentKeys(int key, uint scan)
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake);
        var router = new InputRouter(fake, dispatch);
        router.Configure(new() { ToggleShortcutEnabled = true, ToggleShortcut = new(ShortcutModifiers.Control | ShortcutModifiers.Alt, key) });
        await router.ActivateAsync("iPad", () => true);
        Modifiers(router, true);
        Assert.True(router.Key(key, scan, false, true, 3)); Assert.False(router.IsRemote);
        Assert.True(router.Key(key, scan, false, true, 4)); Assert.False(router.IsRemote);
        Assert.True(router.Key(key, scan, false, false, 5)); Modifiers(router, false);
        Assert.True(router.PhysicalIdle);
        await FakeTransport.Until(() => !dispatch.Settling);
        Assert.DoesNotContain(fake.Sent, p => p.Frame.Kind == ReportKind.Keyboard && p.Frame.Payload.Skip(2).Any(v => v != 0));
        Assert.True(await router.ActivateAsync("iPad", () => true));
        Modifiers(router, true); router.Key('Q', 0x10, false, true, 6); Assert.False(router.IsRemote);
    }
    [Fact] public async Task DisabledToggleIsOrdinaryRemoteInputAndDoesNotSuppressLocalKeys()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake);
        var router = new InputRouter(fake, dispatch);
        Assert.False(router.Key('T', 0x14, false, true, 0)); Assert.False(router.Key('T', 0x14, false, false, 1));
        await router.ActivateAsync("iPad", () => true);
        Modifiers(router, true); router.Key('T', 0x14, false, true, 3); Assert.True(router.IsRemote);
        await FakeTransport.Until(() => fake.Sent.Count == 5);
        Assert.Equal(HidReports.Keyboard(5, [0x17]).Payload, fake.Sent.Last().Frame.Payload);
    }
    [Fact] public async Task TripleEscapeStillWorksWithToggleEnabled()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake);
        var router = new InputRouter(fake, dispatch); router.Configure(new() { ToggleShortcutEnabled = true });
        await router.ActivateAsync("iPad", () => true);
        for (var i = 0; i < 3; i++)
        {
            Assert.True(router.Key(0x1B, 1, false, true, i * 100));
            Assert.Equal(i < 2, router.IsRemote);
            Assert.True(router.Key(0x1B, 1, false, false, i * 100 + 1));
        }
        Assert.True(router.PhysicalIdle);
    }
    [Fact] public async Task ToggleReturnsEvenWhenBluetoothSendingIsBlocked()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake);
        var router = new InputRouter(fake, dispatch); router.Configure(new() { ToggleShortcutEnabled = true });
        await router.ActivateAsync("iPad", () => true);
        fake.BeforeSend = ct => Task.Delay(Timeout.Infinite, ct);
        Modifiers(router, true);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.True(router.Key('T', 0x14, false, true, 3));
        Assert.False(router.IsRemote); Assert.True(watch.ElapsedMilliseconds < 100);
    }
}

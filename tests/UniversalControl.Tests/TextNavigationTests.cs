using UniversalControl.Core;
using Xunit;

namespace UniversalControl.Tests;

public sealed class TextNavigationTests
{
    private sealed class Keyboard(bool navigation = true, bool iosSwap = true, bool ubSwap = false)
    {
        public readonly TextNavigationMapper Mapper = new();
        public readonly Dictionary<long, (byte Modifier, byte Usage)> Keys = [];
        public readonly AppSettings Settings = new() { WindowsTextNavigation = navigation, IosControlCommandSwapped = iosSwap, SwapControlCommand = ubSwap };
        public IReadOnlyList<HidFrame> Press(long id, byte usage = 0, byte modifier = 0)
        { Keys[id] = (modifier, usage); return Mapper.Update(id, true, Keys, Settings); }
        public IReadOnlyList<HidFrame> Release(long id)
        { Keys.Remove(id); return Mapper.Update(id, false, Keys, Settings); }
    }
    private static void Report(HidFrame frame, byte modifiers, params byte[] usages)
        => Assert.Equal(HidReports.Keyboard(modifiers, usages).Payload, frame.Payload);

    public static IEnumerable<object[]> Mappings()
    {
        foreach (var ios in new[] { false, true })
        foreach (var ub in new[] { false, true })
        foreach (var shift in new[] { false, true })
        foreach (var row in new (byte Input, bool Ctrl, byte Output, byte Modifier)[] {
            (0x4A,false,0x50,8), (0x4D,false,0x4F,8), (0x4A,true,0x52,8), (0x4D,true,0x51,8),
            (0x4F,true,0x4F,4), (0x50,true,0x50,4), (0x51,true,0x51,4), (0x52,true,0x52,4),
            (0x2A,true,0x2A,4), (0x4C,true,0x4C,4) })
            yield return [ios, ub, shift, row.Input, row.Ctrl, row.Output, row.Modifier];
    }
    [Theory, MemberData(nameof(Mappings))]
    public void EveryMappingResolvesLogicalActionExactlyOnce(bool ios, bool ub, bool shift, byte input, bool ctrl, byte output, byte logical)
    {
        var k = new Keyboard(iosSwap: ios, ubSwap: ub);
        if (ctrl) k.Press(1, modifier: 1);
        if (shift) k.Press(2, modifier: 2);
        Report(k.Press(3, input).Last(), (byte)((ios ? TextNavigationMapper.Swap(logical) : logical) | (shift ? 2 : 0)), output);
        var released = k.Release(3);
        Report(released[0], (byte)((ios ? TextNavigationMapper.Swap(logical) : logical) | (shift ? 2 : 0)));
        Report(released.Last(), TextNavigationMapper.PassthroughModifiers((byte)((ctrl ? 1 : 0) | (shift ? 2 : 0)), k.Settings));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void DisabledNavigationPreservesInputWithIndependentSwap(bool swap)
    {
        var k = new Keyboard(navigation: false, ubSwap: swap);
        Report(k.Press(1, modifier: 0x91).Single(), swap ? (byte)0x98 : (byte)0x91);
        Report(k.Press(2, 0x4A).Single(), swap ? (byte)0x98 : (byte)0x91, 0x4A);
        Report(k.Release(1).Single(), 0, 0x4A);
    }
    [Fact] public void EarlyControlReleaseKeepsMappingUntilArrowReleaseAndShiftStaysLive()
    {
        var k = new Keyboard(); k.Press(1, modifier: 0x10); k.Press(2, 0x50);
        Report(k.Release(1).Single(), 4, 0x50);
        Report(k.Press(3, modifier: 0x20).Single(), 0x24, 0x50);
        Report(k.Release(3).Single(), 4, 0x50);
        Report(k.Release(2).Last(), 0);
    }
    [Fact] public void IncompatibleChordReleasesMappedKeyBeforeRestoringControl()
    {
        var k = new Keyboard(); k.Press(1, modifier: 1); k.Press(2, 0x50);
        var frames = k.Press(3, 0x06); // Ctrl+C while Left remains physically held.
        Assert.Equal(2, frames.Count); Report(frames[0], 4); Report(frames[1], 1, 0x06);
        Report(k.Release(3).Single(), 1); // No phantom arrow press when C is released.
        Report(k.Release(2).Single(), 1);
        Report(k.Press(2, 0x50).Single(), 4, 0x50);
    }
    [Fact] public void ChangingNavigationFamilyCancelsOlderHold()
    {
        var k = new Keyboard(); k.Press(1, modifier: 1); k.Press(2, 0x50);
        var frames = k.Press(3, 0x4A);
        Report(frames[0], 4); Report(frames.Last(), 1, 0x52);
        Report(k.Release(3).Last(), 1); Report(k.Release(2).Single(), 1);
    }
    [Fact] public void AltChordsAndAlreadyHeldOrdinaryKeysAreNotRemapped()
    {
        var k = new Keyboard(); k.Press(1, modifier: 5);
        Report(k.Press(2, 0x50).Single(), 5, 0x50);
        k.Release(2); k.Release(1); k.Press(1, modifier: 1); k.Press(3, 0x06);
        Report(k.Press(2, 0x50).Single(), 1, 0x06, 0x50);
    }
    [Fact] public void IntroducingAltCancelsNavigationUntilRelease()
    {
        var k = new Keyboard(); k.Press(1, modifier: 1); k.Press(2, 0x50);
        var frames = k.Press(3, modifier: 4);
        Report(frames[0], 4); Report(frames.Last(), 5);
        Report(k.Release(3).Single(), 1); Report(k.Release(2).Single(), 1);
    }
    [Fact] public async Task RouterOwnsRepeatAndChangingSettingsReleasesAndQuarantinesHeldKeys()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake);
        var router = new InputRouter(fake, dispatch);
        var settings = new AppSettings { WindowsTextNavigation = true, IosControlCommandSwapped = true };
        router.Configure(settings); Assert.True(await router.ActivateAsync("iPad", () => true));
        router.Key(0xA2, 0x1D, false, true, 1); router.Key(0x25, 0x4B, true, true, 2);
        router.Key(0x25, 0x4B, true, true, 3);
        await FakeTransport.Until(() => fake.Sent.Count >= 4); Assert.Equal(4, fake.Sent.Count);
        Report(fake.Sent.Last().Frame, 4, 0x50);
        router.Configure(settings with { WindowsTextNavigation = false }); Assert.False(router.IsRemote);
        Assert.True(router.Key(0x25, 0x4B, true, false, 4)); Assert.True(router.Key(0xA2, 0x1D, false, false, 5));
        await FakeTransport.Until(() => !dispatch.Settling); Assert.True(router.PhysicalIdle);
        Assert.True(await router.ActivateAsync("iPad", () => true));
        router.Key(0x24, 0x47, true, true, 6);
        await FakeTransport.Until(() => fake.Sent.Last().Frame.Kind == ReportKind.Keyboard && fake.Sent.Last().Frame.Payload[2] == 0x4A);
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task DisconnectClearsMappingBeforeReactivation(bool swap)
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake);
        var router = new InputRouter(fake, dispatch);
        router.Configure(new() { WindowsTextNavigation = true, IosControlCommandSwapped = true, SwapControlCommand = swap });
        Assert.True(await router.ActivateAsync("iPad", () => true));
        router.Key(0xA2, 0x1D, false, true, 1); router.Key(0x25, 0x4B, true, true, 2);
        await FakeTransport.Until(() => fake.Sent.Count == 4);
        fake.Ready = false; router.CheckConnection(); Assert.False(router.IsRemote);
        router.Key(0x25, 0x4B, true, false, 3); router.Key(0xA2, 0x1D, false, false, 4);
        await FakeTransport.Until(() => !dispatch.Settling);
        fake.Ready = true; Assert.True(await router.ActivateAsync("iPad", () => true));
        router.Key(0x41, 0x1E, false, true, 5);
        await FakeTransport.Until(() => fake.Sent.Last().Frame.Payload[2] == 4);
        Report(fake.Sent.Last().Frame, 0, 4);
    }
    [Fact] public async Task JapaneseLanguageTapRestoresMappedNavigationReport()
    {
        var fake = new FakeTransport(); await using var dispatch = new ReportDispatcher(fake);
        var router = new InputRouter(fake, dispatch);
        router.Configure(new() { WindowsTextNavigation = true, IosControlCommandSwapped = true });
        await router.ActivateAsync("iPad", () => true);
        router.Key(0xA2, 0x1D, false, true, 1); router.Key(0x25, 0x4B, true, true, 2);
        router.Key(0xF3, 0x29, false, true, 3, japaneseKeyboard: true);
        await FakeTransport.Until(() => fake.Sent.Count == 6);
        Report(fake.Sent.Last().Frame, 4, 0x50);
    }
    [Fact] public void CompatibleArrowsShareTheMappingAndResetClearsCancelledKeys()
    {
        var k = new Keyboard(); k.Press(1, modifier: 1); k.Press(2, 0x50);
        Report(k.Press(3, 0x52).Single(), 4, 0x50, 0x52);
        Report(k.Release(2).Single(), 4, 0x52);
        k.Mapper.Reset(); k.Keys.Clear(); Report(k.Press(2, 0x50).Single(), 0, 0x50);
    }
    [Fact] public void OldSettingsDefaultOffAndNewSettingsRoundTrip()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(directory);
        try
        {
            var store = new SettingsStore(directory);
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"SchemaVersion\":1}");
            var old = store.Load(); Assert.False(old.WindowsTextNavigation); Assert.False(old.IosControlCommandSwapped); Assert.False(old.SwapControlCommand);
            store.Save(old with { WindowsTextNavigation = true, IosControlCommandSwapped = true, SwapControlCommand = true });
            var saved = store.Load(); Assert.True(saved.WindowsTextNavigation); Assert.True(saved.IosControlCommandSwapped); Assert.True(saved.SwapControlCommand);
        }
        finally { Directory.Delete(directory, true); }
    }
}

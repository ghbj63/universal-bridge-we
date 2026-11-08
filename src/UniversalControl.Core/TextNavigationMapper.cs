namespace UniversalControl.Core;

/// <summary>Maps held navigation keys, keeping physical identities for release and host repeat.</summary>
public sealed class TextNavigationMapper
{
    private readonly Dictionary<long, (byte Usage, byte Modifier)> _mapped = [];
    private readonly HashSet<long> _suppressed = [];
    private byte? _context;
    private byte _lastModifiers;
    private bool _hadUsages;
    public HidFrame CurrentReport { get; private set; } = HidReports.Keyboard(0, []);

    public void Reset()
    { _mapped.Clear(); _suppressed.Clear(); _context = null; _lastModifiers = 0; _hadUsages = false; CurrentReport = HidReports.Keyboard(0, []); }

    public static byte Swap(byte modifiers) => (byte)((modifiers & ~0x99) |
        ((modifiers & 0x11) << 3) | ((modifiers & 0x88) >> 3));
    public static byte PassthroughModifiers(byte modifiers, AppSettings settings)
        => settings.SwapControlCommand ? Swap(modifiers) : modifiers;

    public IReadOnlyList<HidFrame> Update(long id, bool down,
        IReadOnlyDictionary<long, (byte Modifier, byte Usage)> keys, AppSettings settings)
    {
        byte physical = 0;
        foreach (var key in keys.Values) physical |= key.Modifier;
        if (!down) { _mapped.Remove(id); _suppressed.Remove(id); }
        else if (keys.TryGetValue(id, out var pressed) && pressed.Usage != 0)
        {
            var mapping = settings.WindowsTextNavigation ? Map(pressed.Usage, physical, settings) : null;
            // A HID report has one modifier mask for all keys. Cancel an older incompatible
            // navigation hold until its physical release rather than changing its meaning.
            foreach (var old in _mapped.Where(p => mapping is null || p.Value.Modifier != mapping.Value.Modifier).ToArray())
            { _mapped.Remove(old.Key); _suppressed.Add(old.Key); }
            var ordinaryHeld = keys.Any(p => p.Key != id && p.Value.Usage != 0 &&
                !_mapped.ContainsKey(p.Key) && !_suppressed.Contains(p.Key));
            if (mapping is { } value && !ordinaryHeld) _mapped[id] = value;
        }
        var context = _mapped.Count == 0 ? (byte?)null : _mapped.Values.First().Modifier;
        var modifiers = PassthroughModifiers(physical, settings);
        if (context is { } required)
        {
            // Mapping consumes Ctrl. Other modifiers were excluded at key-down; Shift stays live.
            // Cancel if Alt/Windows is introduced during a navigation hold.
            if ((physical & 0xCC) != 0)
            {
                foreach (var key in _mapped.Keys) _suppressed.Add(key);
                _mapped.Clear(); context = null;
            }
            else modifiers = (byte)((physical & 0x22) | required);
        }
        var usages = keys.Where(p => !_suppressed.Contains(p.Key))
            .Select(p => _mapped.TryGetValue(p.Key, out var mapped) ? mapped.Usage : p.Value.Usage).ToArray();
        var frames = new List<HidFrame>(2);
        if (_context != context && _hadUsages)
            frames.Add(HidReports.Keyboard(_lastModifiers, []));
        CurrentReport = HidReports.Keyboard(modifiers, usages);
        frames.Add(CurrentReport);
        _context = context; _lastModifiers = modifiers; _hadUsages = usages.Any(u => u != 0);
        return frames;
    }

    private static (byte Usage, byte Modifier)? Map(byte usage, byte physical, AppSettings settings)
    {
        if ((physical & 0xCC) != 0) return null; // Alt/Windows combinations retain their meaning.
        var control = (physical & 0x11) != 0;
        byte output;
        byte logical;
        if (usage is 0x4A or 0x4D)
        {
            output = usage == 0x4A ? control ? (byte)0x52 : (byte)0x50 : control ? (byte)0x51 : (byte)0x4F;
            logical = 8; // Apple Command
        }
        else if (control && usage is 0x4F or 0x50 or 0x51 or 0x52 or 0x2A or 0x4C)
        { output = usage; logical = 4; } // Apple Option
        else return null;
        // Generated Apple actions are resolved directly to the receiver's physical layout.
        // The optional UB swap applies only to passthrough input, never a second time here.
        return (output, settings.IosControlCommandSwapped ? Swap(logical) : logical);
    }
}

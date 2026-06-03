using System.Runtime.InteropServices;
using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Handlers;

public sealed class PressKeysHandler : IActionHandler
{
    public string Action => "press_keys";

    public Task ExecuteAsync(IrStep step, ExecutionContext ctx)
    {
        var keys = HandlerHelpers.Param(step, "keys");
        SendKeys(keys);
        return Task.CompletedTask;
    }

    private static void SendKeys(string keys)
    {
        var parts = keys.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 1)
        {
            if (TryParseVk(parts[0], out var vk))
                SendKey(vk);
            else
                foreach (var ch in parts[0]) TypeChar(ch);
            return;
        }

        // Hold modifiers then press the main key, then release modifiers in reverse.
        var modVks = parts[..^1].Select(ParseVk).ToArray();
        var mainVk = ParseVk(parts[^1]);

        foreach (var m in modVks) KeyDown(m);
        SendKey(mainVk);
        foreach (var m in modVks.Reverse()) KeyUp(m);
    }

    // ── Win32 SendInput wrappers ─────────────────────────────────────────────

    private static void SendKey(ushort vk) { KeyDown(vk); KeyUp(vk); }

    private static void KeyDown(ushort vk) => SendInput(vk, 0);
    private static void KeyUp(ushort vk) => SendInput(vk, NativeMethods.KEYEVENTF_KEYUP);

    private static void TypeChar(char ch)
    {
        ushort vk = NativeMethods.VkKeyScan(ch);
        bool needShift = (vk & 0x0100) != 0;
        ushort baseVk = (ushort)(vk & 0xFF);
        if (needShift) KeyDown(VK_SHIFT);
        SendKey(baseVk);
        if (needShift) KeyUp(VK_SHIFT);
    }

    private static void SendInput(ushort vk, uint flags)
    {
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = flags }
            }
        };
        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    // ── VK code map ─────────────────────────────────────────────────────────

    private const ushort VK_SHIFT = 0x10;

    private static ushort ParseVk(string key) =>
        TryParseVk(key, out var vk) ? vk
            : throw new InvalidOperationException(
                $"Unrecognised key '{key}'. Use names like Enter, Ctrl, Alt, Shift, F1–F12, or a single letter/digit.");

    private static bool TryParseVk(string key, out ushort vk)
    {
        vk = key.ToUpperInvariant() switch
        {
            "ENTER" or "RETURN"  => 0x0D,
            "TAB"                => 0x09,
            "ESC" or "ESCAPE"    => 0x1B,
            "BACKSPACE"          => 0x08,
            "DELETE" or "DEL"    => 0x2E,
            "CTRL" or "CONTROL"  => 0x11,
            "ALT"                => 0x12,
            "SHIFT"              => 0x10,
            "WIN" or "WINDOWS"   => 0x5B,
            "SPACE"              => 0x20,
            "HOME"               => 0x24,
            "END"                => 0x23,
            "PAGEUP" or "PGUP"   => 0x21,
            "PAGEDOWN" or "PGDN" => 0x22,
            "UP"                 => 0x26,
            "DOWN"               => 0x28,
            "LEFT"               => 0x25,
            "RIGHT"              => 0x27,
            "F1"  => 0x70, "F2"  => 0x71, "F3"  => 0x72, "F4"  => 0x73,
            "F5"  => 0x74, "F6"  => 0x75, "F7"  => 0x76, "F8"  => 0x77,
            "F9"  => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            _ when key.Length == 1 && char.IsLetterOrDigit(key[0])
                => (ushort)char.ToUpper(key[0]),
            _ => 0
        };
        return vk != 0;
    }
}

internal static class NativeMethods
{
    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern ushort VkKeyScan(char ch);

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public INPUTUNION u; }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }
}

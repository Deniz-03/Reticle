using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Reticle.Core.Hotkeys;
using Reticle.Core.Model;

namespace Reticle.App.Input;

/// <summary>
/// Passive global keyboard and mouse listener (<c>WH_KEYBOARD_LL</c> / <c>WH_MOUSE_LL</c>) on a dedicated
/// background thread with its own message loop. The hook callbacks only post an <see cref="InputEvent"/> to the
/// UI dispatcher and always return <c>CallNextHookEx</c>, so input is never blocked, altered or delayed.
/// Forwards key down/up and mouse button down/up (L, R, M, X1, X2); mouse move and wheel are ignored.
/// Injected events are forwarded too, so macro tools that simulate a bound key work.
/// </summary>
public sealed class InputHookService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int HC_ACTION = 0;

    private const int WM_QUIT = 0x0012;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int WM_XBUTTONUP = 0x020C;
    private const int PM_NOREMOVE = 0x0000;

    private const int MouseLeft = 1, MouseRight = 2, MouseMiddle = 3, MouseX1 = 4, MouseX2 = 5;

    /// <summary>Offset of <c>mouseData</c> in <c>MSLLHOOKSTRUCT</c> (after <c>POINT pt</c>).</summary>
    private const int MouseDataOffset = 8;

    private readonly Dispatcher _dispatcher;
    // Kept in fields so the GC never collects the delegates while native code still holds the function pointers.
    private readonly HookProc _keyboardProc;
    private readonly HookProc _mouseProc;
    private Thread? _thread;
    private uint _threadId;
    private volatile bool _disposed;

    /// <param name="dispatcher">Dispatcher on which <see cref="Input"/> is raised.</param>
    public InputHookService(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
        _keyboardProc = KeyboardHook;
        _mouseProc = MouseHook;
    }

    /// <summary>Raised on the dispatcher thread for every forwarded key or mouse button transition, in order.</summary>
    public event Action<InputEvent>? Input;

    /// <summary>Starts the hook thread and returns once both hooks are installed.</summary>
    /// <exception cref="Win32Exception">A hook could not be installed.</exception>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_thread is not null) throw new InvalidOperationException("Already started.");

        Exception? failure = null;
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() => Run(ready, ex => failure = ex))
        {
            IsBackground = true,
            Name = "Reticle input hooks",
        };
        _thread.Start();
        ready.Wait();
        if (failure is not null)
        {
            _thread.Join();
            _thread = null;
            throw failure;
        }
    }

    private void Run(ManualResetEventSlim ready, Action<Exception> fail)
    {
        _threadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE); // create the message queue before Dispose may post WM_QUIT

        IntPtr module = GetModuleHandle(null);
        IntPtr keyboard = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, module, 0);
        IntPtr mouse = keyboard == IntPtr.Zero ? IntPtr.Zero : SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
        if (mouse == IntPtr.Zero)
        {
            fail(new Win32Exception(Marshal.GetLastWin32Error(), "Installing the low-level input hooks failed."));
            if (keyboard != IntPtr.Zero) UnhookWindowsHookEx(keyboard);
            ready.Set();
            return;
        }
        ready.Set();

        try
        {
            // Low-level hooks are called through this thread's message loop.
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        finally
        {
            UnhookWindowsHookEx(mouse);
            UnhookWindowsHookEx(keyboard);
        }
    }

    private IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION)
        {
            try
            {
                bool? down = (int)wParam switch
                {
                    WM_KEYDOWN or WM_SYSKEYDOWN => true,
                    WM_KEYUP or WM_SYSKEYUP => false,
                    _ => null,
                };
                if (down is { } d)
                    Post(new InputEvent(InputKind.Keyboard, Marshal.ReadInt32(lParam), d)); // KBDLLHOOKSTRUCT.vkCode
            }
            catch (Exception ex) { Debug.WriteLine($"Keyboard hook: {ex.Message}"); }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION)
        {
            try
            {
                (int Code, bool Down)? button = (int)wParam switch
                {
                    WM_LBUTTONDOWN => (MouseLeft, true),
                    WM_LBUTTONUP => (MouseLeft, false),
                    WM_RBUTTONDOWN => (MouseRight, true),
                    WM_RBUTTONUP => (MouseRight, false),
                    WM_MBUTTONDOWN => (MouseMiddle, true),
                    WM_MBUTTONUP => (MouseMiddle, false),
                    WM_XBUTTONDOWN or WM_XBUTTONUP => XButton(lParam, (int)wParam == WM_XBUTTONDOWN),
                    _ => null, // move, wheel
                };
                if (button is { } b)
                    Post(new InputEvent(InputKind.Mouse, b.Code, b.Down));
            }
            catch (Exception ex) { Debug.WriteLine($"Mouse hook: {ex.Message}"); }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private static (int, bool)? XButton(IntPtr lParam, bool down)
    {
        int hiword = (Marshal.ReadInt32(lParam, MouseDataOffset) >> 16) & 0xFFFF;
        return hiword switch
        {
            1 => (MouseX1, down),
            2 => (MouseX2, down),
            _ => null,
        };
    }

    private void Post(InputEvent e)
    {
        if (_disposed) return;
        _dispatcher.BeginInvoke(() =>
        {
            if (!_disposed) Input?.Invoke(e);
        });
    }

    /// <summary>Stops the message loop and removes both hooks.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_thread is null) return;
        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        if (!_thread.Join(TimeSpan.FromSeconds(2)))
            Debug.WriteLine("Input hook thread did not stop in time.");
        _thread = null;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX, ptY;
        public uint lPrivate;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SylphyHorn.Services
{
    /// <summary>
    /// Impede que o WebView invisível do WhatsApp intercepte cliques em outra área.
    /// Todas as consultas COM e alterações de janela pertencem a uma única thread.
    /// </summary>
    internal sealed class WhatsAppClickThroughService : IDisposable
    {
        private const int ExtendedStyle = -20;
        private const int Layered = 0x80000;
        private const int Transparent = 0x20;
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private readonly AutoResetEvent _changed = new AutoResetEvent(false);
        private readonly Dictionary<IntPtr, WindowIdentity> _modified = new Dictionary<IntPtr, WindowIdentity>();
        private readonly Thread _worker;
        private readonly Action<Exception> _reportError;
        private readonly string _ownershipProperty = "SylphyHorn.WhatsApp." + Guid.NewGuid().ToString("N");
        private volatile bool _enabled;
        private bool _disposed;

        public WhatsAppClickThroughService(Action<Exception> reportError)
        {
            this._reportError = reportError;
            this._worker = new Thread(this.Run) { IsBackground = true, Name = "WhatsApp desktop compatibility" };
            this._worker.SetApartmentState(ApartmentState.STA);
            this._worker.Start();
        }

        public void SetEnabled(bool enabled)
        {
            if (this._disposed) return;
            this._enabled = enabled;
            this._changed.Set();
        }

        private void Run()
        {
            IDesktopManager manager = null;
            var waits = new WaitHandle[] { this._stop, this._changed };
            var lastError = DateTime.MinValue;
            try
            {
                while (!this._stop.WaitOne(0))
                {
                    try
                    {
                        if (this._enabled)
                        {
                            if (manager == null) manager = (IDesktopManager)new DesktopManager();
                            this.Scan(manager);
                        }
                        else this.RestoreAll();
                    }
                    catch (Exception ex)
                    {
                        // Explorer pode reiniciar. Recriar o objeto COM na próxima passagem.
                        if (manager != null) Marshal.ReleaseComObject(manager);
                        manager = null;
                        this.RestoreAll();
                        if ((DateTime.UtcNow - lastError).TotalMinutes >= 1)
                        {
                            this._reportError(ex);
                            lastError = DateTime.UtcNow;
                        }
                    }
                    // Desativado, aguardar somente configuração ou encerramento.
                    if (WaitHandle.WaitAny(waits, this._enabled || this._modified.Count > 0 ? 500 : Timeout.Infinite) == 0) break;
                }
            }
            finally
            {
                this.RestoreAll();
                if (manager != null) Marshal.ReleaseComObject(manager);
            }
        }

        private void Scan(IDesktopManager manager)
        {
            var processes = ReadProcesses();
            var roots = new Dictionary<uint, IntPtr>();
            var candidates = new List<WindowIdentity>();
            EnumWindows((window, _) =>
            {
                var thread = GetWindowThreadProcessId(window, out var pid);
                if (!processes.TryGetValue(pid, out var process)) return true;
                var className = new StringBuilder(256);
                GetClassName(window, className, className.Capacity);
                if (process.Name.Equals("WhatsApp.Root.exe", StringComparison.OrdinalIgnoreCase)
                    && className.ToString() == "WinUIDesktopWin32WindowClass")
                    roots[pid] = window;

                if (process.Name.Equals("msedgewebview2.exe", StringComparison.OrdinalIgnoreCase)
                    && className.ToString() == "Chrome_WidgetWin_1"
                    && processes.TryGetValue(process.Parent, out var parent)
                    && parent.Name.Equals("WhatsApp.Root.exe", StringComparison.OrdinalIgnoreCase))
                    candidates.Add(new WindowIdentity(window, pid, thread, process.Parent));
                return true;
            }, IntPtr.Zero);

            var keep = new HashSet<IntPtr>();
            foreach (var candidate in candidates)
            {
                var window = candidate.Handle;
                if (!roots.TryGetValue(candidate.Parent, out var root)) continue;
                if (IsIconic(root) || !IsWindowVisible(root)) continue;
                if (manager.IsWindowOnCurrentVirtualDesktop(root, out var onCurrent) != 0 || onCurrent) continue;
                if (!IsWindowVisible(window) || !IsInvisibleLayeredWindow(window)) continue;

                // Em uma janela layered, WS_EX_TRANSPARENT também libera o hit-test.
                // Preservar a visibilidade evita interferir no ciclo de vida do WebView.
                if (!candidate.Matches()) continue;
                if (this._modified.TryGetValue(window, out var previous)
                    && (!previous.Matches() || GetProp(window, this._ownershipProperty) != new IntPtr(1)))
                    this._modified.Remove(window);
                var style = GetWindowLong(window, ExtendedStyle);
                if ((style & Transparent) != 0 && !this._modified.ContainsKey(window)) continue;
                if ((style & Transparent) == 0)
                {
                    // O WebView pode repor seus estilos enquanto continua em outra área.
                    // Nesse caso, reaplicar a correção, mantendo a mesma identidade.
                    // A propriedade desaparece com a janela, protegendo contra reutilização do HWND.
                    if (!SetProp(window, this._ownershipProperty, new IntPtr(1)))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    SetLastError(0);
                    if (SetWindowLong(window, ExtendedStyle, style | Transparent) == 0
                        && Marshal.GetLastWin32Error() != 0)
                    {
                        var error = Marshal.GetLastWin32Error();
                        RemoveProp(window, this._ownershipProperty);
                        throw new Win32Exception(error);
                    }
                    this._modified[window] = candidate;
                }
                keep.Add(window);
            }

            foreach (var window in new List<IntPtr>(this._modified.Keys))
                if (!keep.Contains(window)) this.Restore(window);
        }

        private static bool IsInvisibleLayeredWindow(IntPtr window)
        {
            return (GetWindowLong(window, ExtendedStyle) & Layered) != 0
                && GetLayeredWindowAttributes(window, out _, out var alpha, out var flags)
                && (flags & 2) != 0 && alpha == 0;
        }

        private void Restore(IntPtr window)
        {
            var identity = this._modified[window];
            if (identity.Matches() && GetProp(window, this._ownershipProperty) == new IntPtr(1))
            {
                // Restaurar somente o bit que alteramos. O aplicativo pode mudar os demais.
                var style = GetWindowLong(window, ExtendedStyle);
                if ((style & Transparent) != 0)
                {
                    SetLastError(0);
                    if (SetWindowLong(window, ExtendedStyle, style & ~Transparent) == 0
                        && Marshal.GetLastWin32Error() != 0)
                        return; // Manter para repetir a restauração na próxima passagem.
                }
            }
            RemoveProp(window, this._ownershipProperty);
            this._modified.Remove(window);
        }

        private void RestoreAll()
        {
            foreach (var window in new List<IntPtr>(this._modified.Keys)) this.Restore(window);
        }

        private static Dictionary<uint, ProcessEntry> ReadProcesses()
        {
            var snapshot = CreateToolhelp32Snapshot(2, 0);
            if (snapshot == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var result = new Dictionary<uint, ProcessEntry>();
                var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry)) };
                if (!Process32First(snapshot, ref entry)) throw new Win32Exception(Marshal.GetLastWin32Error());
                do { result[entry.Id] = entry; } while (Process32Next(snapshot, ref entry));
                return result;
            }
            finally { CloseHandle(snapshot); }
        }

        public void Dispose()
        {
            if (this._disposed) return;
            this._disposed = true;
            this._stop.Set();
            // A thread não chama a interface de forma síncrona e conclui a restauração antes de sair.
            this._worker.Join();
            this._stop.Dispose();
            this._changed.Dispose();
        }

        private sealed class WindowIdentity
        {
            public IntPtr Handle { get; }
            public uint Process { get; }
            public uint Thread { get; }
            public uint Parent { get; }
            public WindowIdentity(IntPtr handle, uint process, uint thread, uint parent)
            {
                this.Handle = handle; this.Process = process; this.Thread = thread; this.Parent = parent;
            }
            public bool Matches() => IsWindow(this.Handle)
                && GetWindowThreadProcessId(this.Handle, out var pid) == this.Thread && pid == this.Process;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry
        {
            public uint Size, Usage, Id;
            public UIntPtr Heap;
            public uint Module, Threads, Parent;
            public int Priority;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
        }

        [ComImport, Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")]
        private class DesktopManager { }
        [ComImport, Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDesktopManager
        {
            [PreserveSig] int IsWindowOnCurrentVirtualDesktop(IntPtr window, [MarshalAs(UnmanagedType.Bool)] out bool current);
            [PreserveSig] int GetWindowDesktopId(IntPtr window, out Guid desktop);
            [PreserveSig] int MoveWindowToDesktop(IntPtr window, ref Guid desktop);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetProp(IntPtr window, string name, IntPtr value);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetProp(IntPtr window, string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr RemoveProp(IntPtr window, string name);

        private delegate bool WindowCallback(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowLong(IntPtr window, int index, int value);
        [DllImport("user32.dll")] private static extern bool GetLayeredWindowAttributes(IntPtr window, out uint color, out byte alpha, out uint flags);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint process);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")] private static extern void SetLastError(uint error);
    }
}

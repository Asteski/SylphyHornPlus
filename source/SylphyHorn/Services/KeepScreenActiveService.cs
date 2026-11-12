using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SylphyHorn.Services
{
    /// <summary>
    /// Mantém tela/sistema ativos sem alterar o plano de energia nem desativar o protetor no registro.
    /// A solicitação pertence à thread e o Windows a remove mesmo se o processo terminar inesperadamente.
    /// </summary>
    internal sealed class KeepScreenActiveService : IDisposable
    {
        private const uint Continuous = 0x80000000;
        private const uint SystemRequired = 0x00000001;
        private const uint DisplayRequired = 0x00000002;
        private readonly object _gate = new object();
        private readonly AutoResetEvent _changed = new AutoResetEvent(false);
        private readonly Thread _worker;
        private readonly Action<Exception> _reportError;
        private volatile bool _enabled;
        private volatile bool _disposed;
        private volatile bool _active;
        internal bool IsActive => _active;

        public KeepScreenActiveService(Action<Exception> reportError)
        {
            _reportError = reportError;
            _worker = new Thread(Run) { IsBackground = true, Name = "SylphyHorn keep screen active" };
            _worker.Start();
        }

        public void SetEnabled(bool enabled)
        {
            lock (_gate)
            {
                if (_disposed) return;
                _enabled = enabled;
                _changed.Set();
            }
        }

        private void Run()
        {
            try
            {
                while (!_disposed)
                {
                    try
                    {
                        // Não interfere no bloqueio manual, na tela de senha nem em prompts seguros.
                        var requested = _enabled && IsInteractiveDesktop();
                        if (requested != _active)
                        {
                            if (SetThreadExecutionState(Continuous | (requested ? SystemRequired | DisplayRequired : 0)) == 0)
                                throw new Win32Exception(Marshal.GetLastWin32Error(), "Não foi possível alterar a solicitação de energia.");
                            _active = requested;
                        }
                        if (_active) PreventScreenSaver();
                    }
                    catch (Exception ex)
                    {
                        _enabled = false;
                        SetThreadExecutionState(Continuous);
                        _active = false;
                        _reportError?.Invoke(ex);
                    }
                    _changed.WaitOne(_enabled ? 500 : Timeout.Infinite);
                }
            }
            finally
            {
                SetThreadExecutionState(Continuous);
                _active = false;
            }
        }

        private static void PreventScreenSaver()
        {
            if (!SystemParametersInfo(0x0010, 0, out var saverEnabled, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (saverEnabled == 0) return;
            if (!SystemParametersInfo(0x000E, 0, out var timeoutSeconds, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (timeoutSeconds == 0) return;
            if (!SystemParametersInfo(0x1026, 0, out var blocked, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (blocked != 0)
                throw new InvalidOperationException("O Windows está configurado para não aceitar atividade simulada no temporizador da proteção de tela.");

            var last = new LastInput { Size = (uint)Marshal.SizeOf(typeof(LastInput)) };
            if (!GetLastInputInfo(ref last)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var idle = unchecked((uint)Environment.TickCount - last.Time);
            var interval = Math.Max(500u, Math.Min(30000u, timeoutSeconds * 1000u / 3u));
            if (idle >= interval) ResetScreenSaverIdle();
        }

        internal static void ResetScreenSaverIdle()
        {
            // ES_DISPLAY_REQUIRED não impede o protetor. Movimento relativo ZERO renova a atividade
            // sem deslocar o cursor, clicar, rolar ou digitar. Não escreve configurações globais.
            var input = new Input { Mouse = new MouseInput { Flags = 0x0001, ExtraInfo = new UIntPtr(0x53594841u) } };
            if (SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Input))) != 1)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "O Windows recusou a renovação de atividade da tela.");
        }

        private static bool IsInteractiveDesktop()
        {
            var desktop = OpenInputDesktop(0, false, 0x0001);
            if (desktop == IntPtr.Zero) return false;
            try
            {
                var name = new StringBuilder(256);
                return GetUserObjectInformation(desktop, 2, name, name.Capacity * 2, out _)
                    && string.Equals(name.ToString(), "Default", StringComparison.OrdinalIgnoreCase);
            }
            finally { CloseDesktop(desktop); }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _changed.Set();
            }
            // A mesma thread que solicitou energia libera a solicitação antes de encerrar.
            _worker.Join();
            _changed.Dispose();
        }

        [StructLayout(LayoutKind.Sequential)] private struct LastInput { public uint Size; public uint Time; }
        [StructLayout(LayoutKind.Sequential)] private struct MouseInput
        {
            public int X; public int Y; public uint Data; public uint Flags; public uint Time; public UIntPtr ExtraInfo;
        }
        // MOUSEINPUT é o maior membro da união INPUT tanto em x86 quanto em x64.
        [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public MouseInput Mouse; }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint SetThreadExecutionState(uint flags);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool GetLastInputInfo(ref LastInput input);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SystemParametersInfo(uint action, uint parameter, out uint value, uint flags);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseDesktop(IntPtr desktop);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder value, int size, out int needed);
    }
}

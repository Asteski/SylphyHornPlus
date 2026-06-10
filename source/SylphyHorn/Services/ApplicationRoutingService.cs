using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SylphyHorn.Interop;
using SylphyHorn.Serialization;
using WindowsDesktop;

namespace SylphyHorn.Services
{
	public class ApplicationRoutingService : IDisposable
	{
		private const uint EVENT_OBJECT_SHOW = 0x8002;
		private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
		private const int OBJID_WINDOW = 0;

		public static ApplicationRoutingService Instance { get; } = new ApplicationRoutingService();

		private readonly object _syncRoot = new object();
		private readonly HashSet<IntPtr> _handledWindows = new HashSet<IntPtr>();
		private readonly WinEventDelegate _winEventDelegate;
		private IntPtr _windowShownHook;
		private bool _started;

		private ApplicationRoutingService()
		{
			this._winEventDelegate = this.OnWindowShown;
		}

		public void Start()
		{
			if (this._started) return;

			this._windowShownHook = NativeMethods.SetWinEventHook(
				EVENT_OBJECT_SHOW,
				EVENT_OBJECT_SHOW,
				IntPtr.Zero,
				this._winEventDelegate,
				0,
				0,
				WINEVENT_OUTOFCONTEXT);
			this._started = this._windowShownHook != IntPtr.Zero;
		}

		private void OnWindowShown(
			IntPtr hWinEventHook,
			uint eventType,
			IntPtr hwnd,
			int idObject,
			int idChild,
			uint dwEventThread,
			uint dwmsEventTime)
		{
			if (hwnd == IntPtr.Zero || idObject != OBJID_WINDOW || idChild != 0) return;
			if (!this.TryRegisterWindow(hwnd)) return;

			Task.Run(() => this.RouteWindow(hwnd));
		}

		private bool TryRegisterWindow(IntPtr hwnd)
		{
			lock (this._syncRoot)
			{
				if (this._handledWindows.Contains(hwnd)) return false;

				this._handledWindows.Add(hwnd);
				return true;
			}
		}

		private void RouteWindow(IntPtr hwnd)
		{
			try
			{
				Thread.Sleep(200);

				if (!IsTopLevelVisibleWindow(hwnd)) return;
				if (!TryGetProcessName(hwnd, out var processName)) return;

				var target = this.FindTargetDesktop(processName);
				if (target == null) return;

				VirtualDesktopHelper.MoveToDesktop(hwnd, target);
				if (VirtualDesktop.Current != target)
				{
					target.Switch();
				}
				NativeMethods.SetForegroundWindow(hwnd);
			}
			catch (Exception ex)
			{
				LoggingService.Instance.Register(ex);
			}
		}

		private VirtualDesktop FindTargetDesktop(string processName)
		{
			var desktops = VirtualDesktop.AllDesktops;
			var configuredProcessNames = Settings.General.DesktopProcessNames.Value;
			var count = Settings.General.DesktopProcessNamesCreateMissingDesktop.Value
				? configuredProcessNames.Count
				: Math.Min(desktops.Length, configuredProcessNames.Count);

			for (var index = 0; index < count; ++index)
			{
				var configuredNames = configuredProcessNames[index].Value;
				if (MatchesProcessName(configuredNames, processName))
				{
					if (index >= desktops.Length)
					{
						desktops = this.CreateDesktopsThrough(index);
						if (index >= desktops.Length) return null;
					}

					return desktops[index];
				}
			}

			return null;
		}

		private VirtualDesktop[] CreateDesktopsThrough(int index)
		{
			lock (this._syncRoot)
			{
				var desktops = VirtualDesktop.AllDesktops;
				while (desktops.Length <= index)
				{
					if (VirtualDesktop.Create() == null) break;
					desktops = VirtualDesktop.AllDesktops;
				}

				return desktops;
			}
		}

		private static bool IsTopLevelVisibleWindow(IntPtr hwnd)
		{
			if (!NativeMethods.IsWindowVisible(hwnd)) return false;
			if (NativeMethods.GetShellWindow() == hwnd) return false;

			var root = NativeMethods.GetAncestor(hwnd, GetAncestorFlags.Root);
			return root == hwnd;
		}

		private static bool TryGetProcessName(IntPtr hwnd, out string processName)
		{
			processName = null;

			NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
			if (processId <= 0 || processId == Process.GetCurrentProcess().Id) return false;

			try
			{
				using (var process = Process.GetProcessById(processId))
				{
					processName = NormalizeProcessName(process.ProcessName);
					return !string.IsNullOrEmpty(processName);
				}
			}
			catch
			{
				return false;
			}
		}

		private static bool MatchesProcessName(string configuredNames, string processName)
		{
			if (string.IsNullOrWhiteSpace(configuredNames)) return false;

			return SplitConfiguredNames(configuredNames)
				.Any(name => string.Equals(name, processName, StringComparison.OrdinalIgnoreCase));
		}

		private static IEnumerable<string> SplitConfiguredNames(string configuredNames)
		{
			return configuredNames
				.Split(new[] { ',', ';', '\r', '\n', '|' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(NormalizeProcessName)
				.Where(name => !string.IsNullOrEmpty(name));
		}

		private static string NormalizeProcessName(string processName)
		{
			var name = processName?.Trim().Trim('"');
			if (string.IsNullOrEmpty(name)) return null;

			try
			{
				name = Path.GetFileNameWithoutExtension(name);
			}
			catch (ArgumentException)
			{
				if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
				{
					name = name.Substring(0, name.Length - 4);
				}
			}

			return name;
		}

		public void Dispose()
		{
			if (this._windowShownHook != IntPtr.Zero)
			{
				NativeMethods.UnhookWinEvent(this._windowShownHook);
				this._windowShownHook = IntPtr.Zero;
			}
			this._started = false;
		}
	}
}

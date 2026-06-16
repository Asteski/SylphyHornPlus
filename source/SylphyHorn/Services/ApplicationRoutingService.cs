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
		private readonly HashSet<Guid> _desktopsWithObservedApps = new HashSet<Guid>();
		private readonly HashSet<Guid> _closingDesktopIds = new HashSet<Guid>();
		private readonly WinEventDelegate _winEventDelegate;
		private readonly Timer _emptyDesktopTimer;
		private IntPtr _windowShownHook;
		private bool _started;
		private bool _checkingEmptyDesktops;

		private ApplicationRoutingService()
		{
			this._winEventDelegate = this.OnWindowShown;
			this._emptyDesktopTimer = new Timer(_ => this.CheckForEmptyDesktops(), null, Timeout.Infinite, Timeout.Infinite);
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
			if (this._started)
			{
				this._emptyDesktopTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
			}
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
				if (!TryGetProcessNames(hwnd, out var processNames)) return;

				var target = this.FindTargetDesktop(processNames);
				if (target == null) return;

				VirtualDesktopHelper.MoveToDesktop(hwnd, target);
				if (ShouldCloseDesktopWhenEmpty(target))
				{
					this.MarkDesktopObserved(target);
				}
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

		private VirtualDesktop FindTargetDesktop(IReadOnlyCollection<string> processNames)
		{
			var desktops = VirtualDesktop.AllDesktops;
			var configuredProcessNames = Settings.General.DesktopProcessNames.Value;
			var count = Settings.General.DesktopProcessNamesCreateMissingDesktop.Value
				? configuredProcessNames.Count
				: Math.Min(desktops.Length, configuredProcessNames.Count);

			for (var index = 0; index < count; ++index)
			{
				var configuredNames = configuredProcessNames[index].Value;
				if (MatchesProcessName(configuredNames, processNames))
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

		private void CheckForEmptyDesktops()
		{
			if (!this._started) return;
			if (!this.TryBeginCheckForEmptyDesktops()) return;

			try
			{
				var desktops = VirtualDesktop.AllDesktops;
				var configuredProcessNames = Settings.General.DesktopProcessNames.Value;
				var closeSettings = Settings.General.DesktopProcessNamesCloseWhenEmpty.Value;
				var count = Math.Min(desktops.Length, Math.Min(configuredProcessNames.Count, closeSettings.Count));

				for (var index = 0; index < count; ++index)
				{
					if (!closeSettings[index].Value) continue;

					var desktop = desktops[index];
					var configuredNames = configuredProcessNames[index].Value;
					if (string.IsNullOrWhiteSpace(configuredNames)) continue;

					if (HasMatchingProcess(configuredNames))
					{
						this.MarkDesktopObserved(desktop);
						continue;
					}

					if (this.HasDesktopBeenObserved(desktop))
					{
						this.CloseDesktop(desktop);
						break;
					}
				}
			}
			catch (Exception ex)
			{
				LoggingService.Instance.Register(ex);
			}
			finally
			{
				lock (this._syncRoot)
				{
					this._checkingEmptyDesktops = false;
				}
			}
		}

		private bool TryBeginCheckForEmptyDesktops()
		{
			lock (this._syncRoot)
			{
				if (this._checkingEmptyDesktops) return false;

				this._checkingEmptyDesktops = true;
				return true;
			}
		}

		private static bool HasMatchingProcess(string configuredNames)
		{
			foreach (var process in Process.GetProcesses())
			{
				try
				{
					using (process)
					{
						var names = new List<string>();
						AddProcessName(names, process.ProcessName);
						try
						{
							AddProcessName(names, process.MainModule?.FileName);
						}
						catch
						{
							// ProcessName is usually enough; some processes deny module path access.
						}

						if (MatchesProcessName(configuredNames, names))
						{
							return true;
						}
					}
				}
				catch
				{
					process.Dispose();
				}
			}

			return false;
		}

		private void MarkDesktopObserved(VirtualDesktop desktop)
		{
			if (desktop == null) return;

			lock (this._syncRoot)
			{
				this._desktopsWithObservedApps.Add(desktop.Id);
			}
		}

		private static bool ShouldCloseDesktopWhenEmpty(VirtualDesktop desktop)
		{
			if (desktop == null) return false;

			var desktops = VirtualDesktop.AllDesktops;
			var index = Array.FindIndex(desktops, item => item.Id == desktop.Id);
			var closeSettings = Settings.General.DesktopProcessNamesCloseWhenEmpty.Value;

			return index >= 0
				&& index < closeSettings.Count
				&& closeSettings[index].Value;
		}

		private bool HasDesktopBeenObserved(VirtualDesktop desktop)
		{
			lock (this._syncRoot)
			{
				return this._desktopsWithObservedApps.Contains(desktop.Id)
					&& !this._closingDesktopIds.Contains(desktop.Id);
			}
		}

		private void CloseDesktop(VirtualDesktop desktop)
		{
			lock (this._syncRoot)
			{
				if (!this._closingDesktopIds.Add(desktop.Id)) return;
			}

			try
			{
				var desktops = VirtualDesktop.AllDesktops;
				if (desktops.Length <= 1) return;

				var index = Array.FindIndex(desktops, item => item.Id == desktop.Id);
				if (index < 0) return;

				var fallback = index > 0
					? desktops[index - 1]
					: desktops.FirstOrDefault(item => item.Id != desktop.Id);
				if (fallback == null) return;

				desktop.Remove(fallback);
				lock (this._syncRoot)
				{
					this._desktopsWithObservedApps.Remove(desktop.Id);
				}
			}
			catch (Exception ex)
			{
				LoggingService.Instance.Register(ex);
			}
			finally
			{
				lock (this._syncRoot)
				{
					this._closingDesktopIds.Remove(desktop.Id);
				}
			}
		}

		private VirtualDesktop[] CreateDesktopsThrough(int index)
		{
			lock (this._syncRoot)
			{
				var desktops = VirtualDesktop.AllDesktops;
				var missingCount = index - desktops.Length + 1;
				if (missingCount <= 0) return desktops;

				var createdDesktops = new List<VirtualDesktop>(missingCount);
				for (var i = 0; i < missingCount; ++i)
				{
					var createdDesktop = VirtualDesktop.Create();
					if (createdDesktop == null) break;
					createdDesktops.Add(createdDesktop);
				}

				var refreshedDesktops = VirtualDesktop.AllDesktops;
				if (refreshedDesktops.Length >= desktops.Length + createdDesktops.Count)
				{
					return refreshedDesktops;
				}

				return desktops
					.Concat(createdDesktops.Where(createdDesktop => desktops.All(desktop => desktop.Id != createdDesktop.Id)))
					.ToArray();
			}
		}

		private static bool IsTopLevelVisibleWindow(IntPtr hwnd)
		{
			if (!NativeMethods.IsWindowVisible(hwnd)) return false;
			if (NativeMethods.GetShellWindow() == hwnd) return false;

			var root = NativeMethods.GetAncestor(hwnd, GetAncestorFlags.Root);
			return root == hwnd;
		}

		private static bool TryGetProcessNames(IntPtr hwnd, out IReadOnlyCollection<string> processNames)
		{
			processNames = null;

			NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
			if (processId <= 0 || processId == Process.GetCurrentProcess().Id) return false;

			try
			{
				using (var process = Process.GetProcessById(processId))
				{
					var names = new List<string>();
					AddProcessName(names, process.ProcessName);
					try
					{
						AddProcessName(names, process.MainModule?.FileName);
					}
					catch
					{
						// Some processes deny module path access; ProcessName is still enough for routing.
					}

					processNames = names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
					return processNames.Count > 0;
				}
			}
			catch
			{
				return false;
			}
		}

		private static bool MatchesProcessName(string configuredNames, IReadOnlyCollection<string> processNames)
		{
			if (string.IsNullOrWhiteSpace(configuredNames)) return false;
			if (processNames == null || processNames.Count == 0) return false;

			var compactProcessNames = processNames.Select(CompactProcessName).ToArray();
			return SplitConfiguredNames(configuredNames).Any(name =>
				processNames.Any(processName => string.Equals(name, processName, StringComparison.OrdinalIgnoreCase))
				|| compactProcessNames.Any(processName => string.Equals(CompactProcessName(name), processName, StringComparison.OrdinalIgnoreCase)));
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

		private static void AddProcessName(ICollection<string> processNames, string processName)
		{
			var name = NormalizeProcessName(processName);
			if (!string.IsNullOrEmpty(name))
			{
				processNames.Add(name);
			}
		}

		private static string CompactProcessName(string processName)
			=> new string((processName ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());

		public void Dispose()
		{
			this._emptyDesktopTimer.Change(Timeout.Infinite, Timeout.Infinite);
			this._emptyDesktopTimer.Dispose();
			if (this._windowShownHook != IntPtr.Zero)
			{
				NativeMethods.UnhookWinEvent(this._windowShownHook);
				this._windowShownHook = IntPtr.Zero;
			}
			this._started = false;
		}
	}
}

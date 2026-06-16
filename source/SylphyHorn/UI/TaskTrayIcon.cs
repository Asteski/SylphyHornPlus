using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using MetroRadiance.Interop.Win32;
using MetroRadiance.Platform;
using SylphyHorn.Interop;
using SylphyHorn.Properties;
using SylphyHorn.Serialization;
using SylphyHorn.Services;
using SylphyHorn.Services.Mouse;
using WindowsDesktop;

namespace SylphyHorn.UI
{
	public class TaskTrayIcon : IDisposable
	{
		private const int _trayWheelThrottleMilliseconds = 120;

		private Icon _icon;
		private readonly Icon _darkIcon;
		private readonly Icon _lightIcon;
		private readonly TaskTrayIconItem[] _items;
		private readonly IDisposable _trayMouseWheelSettingSubscription;
		private NotifyIcon _notifyIcon;
		private DynamicInfoTrayIcon _infoIcon;
		private readonly string _showSettingsMenuName = Resources.TaskTray_Menu_Settings;
		private MouseInterceptor _trayMouseInterceptor;
		private int _lastTrayMouseWheelTime;
		private bool _loggedNotifyIconRectFailure;

		public TaskTrayIcon(Icon darkIcon, Icon lightIcon, TaskTrayIconItem[] items)
		{
			this._darkIcon = darkIcon;
			this._lightIcon = lightIcon;

			this._icon = WindowsTheme.SystemTheme.Current == Theme.Light ? this._lightIcon : this._darkIcon;
			this._items = items;

			WindowsTheme.SystemTheme.Changed += this.OnSystemThemeChanged;
			WindowsTheme.Accent.Changed += this.OnAccentChanged;
			WindowsTheme.ColorPrevalence.Changed += this.OnColorPrevalenceChanged;
			VirtualDesktop.CurrentChanged += this.OnCurrentDesktopChanged;
			VirtualDesktop.Destroyed += this.OnDesktopDestroyed;
			this._trayMouseWheelSettingSubscription = Settings.General.TraySwitchDesktopWithMouseWheel
				.Subscribe(_ => this.UpdateTrayMouseWheelHook());
		}

		public void Show()
		{
			if (this._notifyIcon != null) return;

			this._notifyIcon = new NotifyIcon()
			{
				Text = ProductInfo.Title,
				Icon = this._icon,
				Visible = true,
				ContextMenu = new ContextMenu(),
			};

			this.RebuildContextMenu();
			this._notifyIcon.ContextMenu.Popup += this.OnContextMenuPopup;
			this._notifyIcon.MouseClick += this.OnIconClick;
			this.UpdateTrayMouseWheelHook();
		}

		public TaskTrayBaloon CreateBaloon() => new TaskTrayBaloon(this);

		internal void ShowBaloon(TaskTrayBaloon baloon)
		{
			if (this._notifyIcon == null) this.Show();

			this._notifyIcon.ShowBalloonTip(
				(int)baloon.Timespan.TotalMilliseconds,
				baloon.Title,
				baloon.Text,
				ToolTipIcon.None);
		}

		public void Reload(VirtualDesktop desktop = null)
		{
			if (Settings.General.TrayShowDesktop)
			{
				VisualHelper.InvokeOnUIDispatcher(() => this.UpdateWithDesktopInfo(desktop ?? VirtualDesktop.Current));
			}
			else if (this._icon != this._darkIcon && this._icon != this._lightIcon)
			{
				this._infoIcon = null;

				this.ChangeIcon(WindowsTheme.SystemTheme.Current == Theme.Light
					? this._lightIcon
					: this._darkIcon);
			}
		}

		private void UpdateWithDesktopInfo(VirtualDesktop currentDesktop)
		{
			var desktops = VirtualDesktop.AllDesktops;
			var currentDesktopIndex = Array.IndexOf(desktops, currentDesktop) + 1;
			var totalDesktopCount = desktops.Length;

			var text = string.Format(
				Resources.TaskTray_TooltipText_DesktopCount + "\n" + ProductInfo.Title,
				currentDesktopIndex,
				totalDesktopCount);
			this.ChangeText(text);

			if (this._infoIcon == null)
			{
				this._infoIcon = new DynamicInfoTrayIcon(
					WindowsTheme.SystemTheme.Current,
					WindowsTheme.ColorPrevalence.Current);
			}

			this._infoIcon.UpdateFont();
			this.ChangeIcon(this._infoIcon.GetDesktopInfoIcon(currentDesktopIndex, Settings.General.TrayShowOnlyCurrentNumber ? 0 : totalDesktopCount));
		}

		private void OnCurrentDesktopChanged(object sender, VirtualDesktopChangedEventArgs e)
		{
			this.Reload(e.NewDesktop);
		}

		private void OnDesktopDestroyed(object sender, VirtualDesktopDestroyEventArgs e)
		{
			this.Reload();
		}

		private void OnAccentChanged(object sender, System.Windows.Media.Color e)
		{
			var colorPrevalence = WindowsTheme.ColorPrevalence.Current;
			if (Settings.General.TrayShowDesktop && colorPrevalence)
			{
				this._infoIcon.UpdateBrush(WindowsTheme.SystemTheme.Current, colorPrevalence);
				VisualHelper.InvokeOnUIDispatcher(() => this.UpdateWithDesktopInfo(VirtualDesktop.Current));
			}
		}

		private void OnColorPrevalenceChanged(object sender, bool e)
		{
			if (Settings.General.TrayShowDesktop)
			{
				this._infoIcon.UpdateBrush(WindowsTheme.SystemTheme.Current, e);
				VisualHelper.InvokeOnUIDispatcher(() => this.UpdateWithDesktopInfo(VirtualDesktop.Current));
			}
		}

		private void OnSystemThemeChanged(object sender, Theme e)
		{
			if (Settings.General.TrayShowDesktop)
			{
				this._infoIcon.UpdateBrush(e, WindowsTheme.ColorPrevalence.Current);
				VisualHelper.InvokeOnUIDispatcher(() => this.UpdateWithDesktopInfo(VirtualDesktop.Current));
			}
			else
			{
				this.ChangeIcon(e == Theme.Light
					? this._lightIcon
					: this._darkIcon);
			}
		}

		private void OnIconClick(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				if (this._items == null || this._items.Length == 0) return;

				var showSettingsItem = this._items.FirstOrDefault(i => i.Text == this._showSettingsMenuName);
				showSettingsItem?.ClickAction();
			}
		}

		private void UpdateTrayMouseWheelHook()
		{
			if (this._notifyIcon != null && Settings.General.TraySwitchDesktopWithMouseWheel.Value)
			{
				this.StartTrayMouseWheelHook();
				return;
			}

			this.StopTrayMouseWheelHook();
		}

		private void StartTrayMouseWheelHook()
		{
			if (this._trayMouseInterceptor != null) return;

			var mouseInterceptor = new MouseInterceptor();
			mouseInterceptor.WheelDown += this.OnTrayMouseWheel;
			mouseInterceptor.WheelUp += this.OnTrayMouseWheel;
			try
			{
				mouseInterceptor.StartCapturing();
				this._trayMouseInterceptor = mouseInterceptor;
			}
			catch (Exception ex)
			{
				mouseInterceptor.Dispose();
				LoggingService.Instance.Register(ex);
			}
		}

		private void StopTrayMouseWheelHook()
		{
			var mouseInterceptor = this._trayMouseInterceptor;
			if (mouseInterceptor == null) return;

			this._trayMouseInterceptor = null;
			mouseInterceptor.WheelDown -= this.OnTrayMouseWheel;
			mouseInterceptor.WheelUp -= this.OnTrayMouseWheel;
			mouseInterceptor.Dispose();
		}

		private void OnTrayMouseWheel(ref MouseState state)
		{
			try
			{
				if (!Settings.General.TraySwitchDesktopWithMouseWheel.Value) return;
				if (!this.IsPointOverNotifyIcon(state.X, state.Y)) return;

				state.Handled = true;
				if (!this.TryAcceptTrayMouseWheel()) return;

				var delta = state.Stroke == Stroke.WheelUp ? 120 : -120;
				VisualHelper.InvokeOnUIDispatcher(() => VirtualDesktopService.SwitchByMouseWheelDelta(delta));
			}
			catch (Exception ex)
			{
				LoggingService.Instance.Register(ex);
			}
		}

		private bool TryAcceptTrayMouseWheel()
		{
			var now = unchecked((uint)Environment.TickCount);
			var last = unchecked((uint)this._lastTrayMouseWheelTime);
			if (now - last < _trayWheelThrottleMilliseconds) return false;

			this._lastTrayMouseWheelTime = unchecked((int)now);
			return true;
		}

		private bool IsPointOverNotifyIcon(int x, int y)
		{
			return this.TryGetNotifyIconRect(out var rect)
				&& x >= rect.Left
				&& x < rect.Right
				&& y >= rect.Top
				&& y < rect.Bottom;
		}

		private bool TryGetNotifyIconRect(out RECT rect)
		{
			rect = default(RECT);
			try
			{
				if (!this.TryGetNotifyIconIdentifier(out var identifier)) return false;

				return NativeMethods.Shell_NotifyIconGetRect(ref identifier, out rect) == 0;
			}
			catch (Exception ex)
			{
				if (!this._loggedNotifyIconRectFailure)
				{
					this._loggedNotifyIconRectFailure = true;
					LoggingService.Instance.Register(ex);
				}

				return false;
			}
		}

		private bool TryGetNotifyIconIdentifier(out NativeMethods.NotifyIconIdentifier identifier)
		{
			identifier = default(NativeMethods.NotifyIconIdentifier);
			if (this._notifyIcon == null) return false;

			var notifyIconType = typeof(NotifyIcon);
			var windowField = notifyIconType.GetField("window", BindingFlags.NonPublic | BindingFlags.Instance);
			var idField = notifyIconType.GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
			if (windowField == null || idField == null) return false;

			var window = windowField.GetValue(this._notifyIcon) as NativeWindow;
			if (window == null || window.Handle == IntPtr.Zero) return false;

			var id = idField.GetValue(this._notifyIcon);
			if (id == null) return false;

			identifier = new NativeMethods.NotifyIconIdentifier
			{
				Size = Marshal.SizeOf(typeof(NativeMethods.NotifyIconIdentifier)),
				HWnd = window.Handle,
				Id = Convert.ToUInt32(id),
				GuidItem = Guid.Empty,
			};
			return true;
		}

		private void OnContextMenuPopup(object sender, EventArgs e)
		{
			this.RebuildContextMenu();
		}

		private void RebuildContextMenu()
		{
			var contextMenu = this._notifyIcon?.ContextMenu;
			if (contextMenu == null) return;

			contextMenu.MenuItems.Clear();
			foreach (var item in this._items.Where(x => x.CanDisplay()))
			{
				var menuItem = item;
				if (menuItem.IsSeparator)
				{
					contextMenu.MenuItems.Add(new MenuItem("-"));
					continue;
				}

				contextMenu.MenuItems.Add(new MenuItem(menuItem.Text, (sender, args) => menuItem.ClickAction())
				{
					Enabled = menuItem.CanClick(),
				});
			}
		}

		private void ChangeText(string newText)
		{
			this._notifyIcon.Text = newText;
		}

		private void ChangeIcon(Icon newIcon)
		{
			if (this._icon != this._darkIcon && this._icon != this._lightIcon)
			{
				this._icon?.Dispose();
			}

			this._icon = newIcon;
			this._notifyIcon.Icon = newIcon;
		}

		public void Dispose()
		{
			WindowsTheme.SystemTheme.Changed -= this.OnSystemThemeChanged;
			WindowsTheme.Accent.Changed -= this.OnAccentChanged;
			WindowsTheme.ColorPrevalence.Changed -= this.OnColorPrevalenceChanged;
			VirtualDesktop.CurrentChanged -= this.OnCurrentDesktopChanged;
			VirtualDesktop.Destroyed -= this.OnDesktopDestroyed;
			if (this._notifyIcon != null)
			{
				this._notifyIcon.ContextMenu.Popup -= this.OnContextMenuPopup;
				this._notifyIcon.MouseClick -= this.OnIconClick;
			}

			this.StopTrayMouseWheelHook();
			this._trayMouseWheelSettingSubscription?.Dispose();
			this._notifyIcon?.Dispose();
			this._lightIcon?.Dispose();
			this._icon?.Dispose();
		}
	}

	public class TaskTrayIconItem
	{
		private readonly Func<string> _textProvider;

		public string Text => this._textProvider();

		public Action ClickAction { get; }

		public Func<bool> CanDisplay { get; }

		public Func<bool> CanClick { get; }

		public bool IsSeparator { get; }

		public static TaskTrayIconItem Separator()
			=> new TaskTrayIconItem(() => "-", () => { }, () => true, () => false, true);

		public TaskTrayIconItem(string text, Action clickAction) : this(text, clickAction, () => true) { }

		public TaskTrayIconItem(string text, Action clickAction, Func<bool> canDisplay)
			: this(() => text, clickAction, canDisplay) { }

		public TaskTrayIconItem(string text, Action clickAction, Func<bool> canDisplay, Func<bool> canClick)
			: this(() => text, clickAction, canDisplay, canClick) { }

		public TaskTrayIconItem(Func<string> textProvider, Action clickAction) : this(textProvider, clickAction, () => true) { }

		public TaskTrayIconItem(Func<string> textProvider, Action clickAction, Func<bool> canDisplay)
			: this(textProvider, clickAction, canDisplay, () => true) { }

		public TaskTrayIconItem(Func<string> textProvider, Action clickAction, Func<bool> canDisplay, Func<bool> canClick)
			: this(textProvider, clickAction, canDisplay, canClick, false) { }

		private TaskTrayIconItem(Func<string> textProvider, Action clickAction, Func<bool> canDisplay, Func<bool> canClick, bool isSeparator)
		{
			this._textProvider = textProvider;
			this.ClickAction = clickAction;
			this.CanDisplay = canDisplay;
			this.CanClick = canClick;
			this.IsSeparator = isSeparator;
		}
	}

	public class TaskTrayBaloon
	{
		private readonly TaskTrayIcon _icon;

		public string Title { get; set; }

		public string Text { get; set; }

		public TimeSpan Timespan { get; set; }

		internal TaskTrayBaloon(TaskTrayIcon icon)
		{
			this._icon = icon;
		}

		public void Show()
		{
			this._icon.ShowBaloon(this);
		}
	}
}

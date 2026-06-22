using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Livet;
using MetroRadiance.Platform;
using MetroTrilithon.Lifetime;
using MetroRadiance.Interop.Win32;
using SylphyHorn.Interop;
using SylphyHorn.Properties;
using SylphyHorn.Serialization;
using SylphyHorn.UI;
using SylphyHorn.UI.Bindings;
using WindowsDesktop;

namespace SylphyHorn.Services
{
	public class TaskbarDeskbandService : IDisposable
	{
		private const int _gwlStyle = -16;
		private const int _gwlExStyle = -20;
		private const int _wsChild = 0x40000000;
		private const int _wsCaption = 0x00C00000;
		private const int _wsThickFrame = 0x00040000;
		private const int _wsMinimizeBox = 0x00020000;
		private const int _wsMaximizeBox = 0x00010000;
		private const int _wsExLayered = 0x00080000;
		private const int _wsExToolWindow = 0x00000080;
		private const int _horizontalMinWidth = 38;
		private const int _horizontalMaxWidth = 220;
		private const int _horizontalTextPadding = 18;
		private const int _modernLeftReservedWidth = 60;
		private const int _modernEdgeMargin = 4;
		private const int _verticalHeight = 28;
		private const int _lightFontWeight = 300;
		private const int _semiLightFontWeight = 350;
		private const int _regularFontWeight = 400;
		private const int _semiBoldFontWeight = 600;
		private const int _boldFontWeight = 700;
		private const int _rpcServerUnavailableHResult = unchecked((int)0x800706BA);
		private const int _rpcCallFailedHResult = unchecked((int)0x800706BE);
		private const int _rpcDisconnectedHResult = unchecked((int)0x80010108);

		private readonly LivetCompositeDisposable _compositeDisposable = new LivetCompositeDisposable();
		private readonly Timer _layoutTimer;
		private DeskbandForm _form;
		private IntPtr _taskbarHandle;
		private IntPtr _containerHandle;
		private IntPtr _taskListHandle;
		private IntPtr _trayNotifyHandle;
		private RECT _originalTaskListRect;
		private DeskbandMode _activeMode = DeskbandMode.Disabled;
		private bool _hasOriginalTaskListRect;
		private bool _started;
		private bool _disposed;

		public TaskbarDeskbandService()
		{
			MigrateLegacyEnabledSetting();
			MigrateLegacyPositionSetting();
			MigrateLegacyRomanDisplayMode();

			this._layoutTimer = new Timer { Interval = 1000 };
			this._layoutTimer.Tick += (sender, args) => this.OnLayoutTimerTick();

			Settings.General.TaskbarDeskbandMode
				.Subscribe(mode =>
				{
					RememberDeskbandMode(mode);
					if (!this._started) return;
					this.Hide(stopTimer: false);
					if (GetDeskbandMode() == DeskbandMode.Disabled)
					{
						this._layoutTimer.Stop();
						return;
					}

					this._layoutTimer.Start();
					this.Show();
				})
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandPosition
				.Subscribe(_ => this.UpdateLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandPositionOffset
				.Subscribe(_ => this.UpdateLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandVerticalPositionOffset
				.Subscribe(_ => this.UpdateLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandDisplayMode
				.Subscribe(_ => this.UpdateTextAndLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandCustomNumberStyleEnabled
				.Subscribe(_ => this.UpdateTextAndLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandNumberWrapper
				.Subscribe(_ => this.UpdateTextAndLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandNumberWrapperSpaces
				.Subscribe(_ => this.UpdateTextAndLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandRomanNumber
				.Subscribe(_ => this.UpdateTextAndLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandShowTotalDesktopCount
				.Subscribe(_ => this.UpdateTextAndLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandNumberBeforeName
				.Subscribe(_ => this.UpdateTextAndLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandCustomAppearanceEnabled
				.Subscribe(_ => this.UpdateAppearance())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandFontFamily
				.Subscribe(_ => this.UpdateAppearance())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandFontSize
				.Subscribe(_ => this.UpdateAppearance())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandFontColor
				.Subscribe(_ => this.UpdateAppearance())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandFontWeight
				.Subscribe(_ => this.UpdateAppearance())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandFontBold
				.Subscribe(_ => this.UpdateAppearance())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandFontItalic
				.Subscribe(_ => this.UpdateAppearance())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandFontUnderline
				.Subscribe(_ => this.UpdateAppearance())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandFontRenderingMode
				.Subscribe(_ => this.UpdateTextAndLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandTooltipEnabled
				.Subscribe(_ => this.UpdateTextAndLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandTooltipNumberOnly
				.Subscribe(_ => this.UpdateTextAndLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandTooltipListWindows
				.Subscribe(_ => this.UpdateTextAndLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.TaskbarDeskbandTooltipWindowStyle
				.Subscribe(_ => this.UpdateTextAndLayout())
				.AddTo(this._compositeDisposable);
			Settings.General.UseDesktopName
				.Subscribe(_ => this.UpdateTextAndLayout())
				.AddTo(this._compositeDisposable);

			VirtualDesktop.CurrentChanged += this.OnCurrentDesktopChanged;
			VirtualDesktop.Created += this.OnDesktopCreated;
			VirtualDesktop.Destroyed += this.OnDesktopDestroyed;
			VirtualDesktop.Renamed += this.OnDesktopRenamed;
			WindowsTheme.SystemTheme.Changed += this.OnSystemThemeChanged;
			Settings.General.DesktopNames.PropertyChanged += this.OnDesktopNamesChanged;
		}

		public void Start()
		{
			if (this._disposed) return;

			this._started = true;
			if (GetDeskbandMode() != DeskbandMode.Disabled)
			{
				this._layoutTimer.Start();
				this.Show();
			}
		}

		public static bool IsDeskbandModeEnabled
			=> Settings.General.TaskbarDeskbandMode.Value != GeneralSettings.TaskbarDeskbandModeDisabledValue;

		public static void ToggleDeskbandMode()
		{
			if (IsDeskbandModeEnabled)
			{
				RememberDeskbandMode(Settings.General.TaskbarDeskbandMode.Value);
				Settings.General.TaskbarDeskbandMode.Value = GeneralSettings.TaskbarDeskbandModeDisabledValue;
				return;
			}

			Settings.General.TaskbarDeskbandMode.Value = GetLastEnabledDeskbandMode();
		}

		private static void RememberDeskbandMode(uint mode)
		{
			if (IsEnabledDeskbandMode(mode))
			{
				Settings.General.TaskbarDeskbandLastEnabledMode.Value = mode;
			}
		}

		private static uint GetLastEnabledDeskbandMode()
		{
			var mode = Settings.General.TaskbarDeskbandLastEnabledMode.Value;
			return IsEnabledDeskbandMode(mode) ? mode : GetDefaultEnabledDeskbandMode();
		}

		private static uint GetDefaultEnabledDeskbandMode()
			=> ProductInfo.IsWindows11OrLater
				? GeneralSettings.TaskbarDeskbandModeModernTaskbarValue
				: GeneralSettings.TaskbarDeskbandModeLegacyTaskbarValue;

		private static bool IsEnabledDeskbandMode(uint mode)
			=> mode == GeneralSettings.TaskbarDeskbandModeModernTaskbarValue
				|| mode == GeneralSettings.TaskbarDeskbandModeLegacyTaskbarValue;

		public void Dispose()
		{
			if (this._disposed) return;
			this._disposed = true;

			VirtualDesktop.CurrentChanged -= this.OnCurrentDesktopChanged;
			VirtualDesktop.Created -= this.OnDesktopCreated;
			VirtualDesktop.Destroyed -= this.OnDesktopDestroyed;
			VirtualDesktop.Renamed -= this.OnDesktopRenamed;
			WindowsTheme.SystemTheme.Changed -= this.OnSystemThemeChanged;
			Settings.General.DesktopNames.PropertyChanged -= this.OnDesktopNamesChanged;
			this._compositeDisposable.Dispose();
			this.Hide();
			this._layoutTimer.Dispose();
		}

		private void OnLayoutTimerTick()
		{
			try
			{
				if (GetDeskbandMode() == DeskbandMode.Disabled)
				{
					this.Hide();
					return;
				}

				if (this._form == null)
				{
					this.Show();
					return;
				}

				if (Settings.General.TaskbarDeskbandTooltipListWindows.Value)
				{
					this.UpdateText(show: false, render: false);
				}

				this.UpdateLayout();
			}
			catch (COMException ex) when (IsVirtualDesktopTemporarilyUnavailable(ex))
			{
				// Explorer can briefly tear down the virtual desktop COM server while restarting.
			}
		}

		private void Show()
		{
			if (this._form != null) return;
			var mode = GetDeskbandMode();
			if (mode == DeskbandMode.Disabled) return;
			if (!this.TryFindTaskbarWindows(mode)) return;

			this._activeMode = mode;
			this._form = new DeskbandForm(CreateAppearance(WindowsTheme.SystemTheme.Current));

			var handle = this._form.Handle;
			var style = NativeMethods.GetWindowLongPtr(handle, _gwlStyle).ToInt64();
			style &= ~(_wsCaption | _wsThickFrame | _wsMinimizeBox | _wsMaximizeBox);
			style |= _wsChild;
			NativeMethods.SetWindowLongPtr(handle, _gwlStyle, new IntPtr(style));
			var exStyle = NativeMethods.GetWindowLongPtr(handle, _gwlExStyle).ToInt64();
			exStyle |= _wsExLayered | _wsExToolWindow;
			NativeMethods.SetWindowLongPtr(handle, _gwlExStyle, new IntPtr(exStyle));
			NativeMethods.SetParent(handle, mode == DeskbandMode.LegacyTaskbar ? this._containerHandle : this._taskbarHandle);

			this.UpdateText(show: false, render: false);
			this.UpdateLayout(force: true);
			this._layoutTimer.Start();
		}

		private void Hide(bool stopTimer = true)
		{
			if (stopTimer)
			{
				this._layoutTimer.Stop();
			}

			this.RestoreTaskList();

			if (this._form != null)
			{
				this._form.Close();
				this._form.Dispose();
				this._form = null;
			}

			this._taskbarHandle = IntPtr.Zero;
			this._containerHandle = IntPtr.Zero;
			this._taskListHandle = IntPtr.Zero;
			this._trayNotifyHandle = IntPtr.Zero;
			this._activeMode = DeskbandMode.Disabled;
		}

		private void UpdateLayout(bool force = false)
		{
			if (this._form == null) return;
			if (!this.AreTaskbarWindowsValid())
			{
				this.Hide(stopTimer: false);
				this.Show();
				return;
			}

			if (this._activeMode == DeskbandMode.ModernTaskbar)
			{
				this.UpdateModernLayout();
				return;
			}

			this.UpdateLegacyLayout(force);
		}

		private void UpdateLegacyLayout(bool force)
		{
			if (this._activeMode != DeskbandMode.LegacyTaskbar) return;

			if (!NativeMethods.GetWindowRect(this._taskbarHandle, out var taskbarRect)
				|| !NativeMethods.GetWindowRect(this._containerHandle, out var containerRect)
				|| !NativeMethods.GetWindowRect(this._taskListHandle, out var taskListRect))
			{
				return;
			}

			var horizontal = Width(taskbarRect) >= Height(taskbarRect);
			var placeOnLeft = IsDeskbandPlacedOnLeft();
			var horizontalOffset = GetDeskbandHorizontalOffset();
			var verticalOffset = GetDeskbandVerticalOffset();
			if (!this._hasOriginalTaskListRect || force)
			{
				this._originalTaskListRect = GetInitialTaskListRect(taskListRect, containerRect, horizontal, placeOnLeft);
				this._hasOriginalTaskListRect = true;
			}

			var sourceTaskListRect = this._originalTaskListRect;
			if (horizontal)
			{
				var height = Math.Max(1, Height(sourceTaskListRect));
				var minWidth = Scale(_horizontalMinWidth);
				var maxWidth = Math.Min(Scale(_horizontalMaxWidth), Math.Max(minWidth, Width(sourceTaskListRect) - 1));
				var width = this._form.GetDesiredWidth(minWidth, maxWidth);
				var left = sourceTaskListRect.Left - containerRect.Left;
				var top = sourceTaskListRect.Top - containerRect.Top;
				var deskbandTop = top + verticalOffset;
				var taskWidth = Math.Max(1, Width(sourceTaskListRect) - width);
				if (placeOnLeft)
				{
					NativeMethods.MoveWindow(this._form.Handle, Clamp(left + horizontalOffset, 0, Math.Max(0, Width(containerRect) - width)), deskbandTop, width, height, true);
					NativeMethods.MoveWindow(this._taskListHandle, left + width, top, taskWidth, height, true);
				}
				else
				{
					NativeMethods.MoveWindow(this._taskListHandle, left, top, taskWidth, height, true);
					NativeMethods.MoveWindow(this._form.Handle, Clamp(left + taskWidth + horizontalOffset, 0, Math.Max(0, Width(containerRect) - width)), deskbandTop, width, height, true);
				}
				this.ShowDeskbandFormIfRendered();
			}
			else
			{
				var width = Math.Max(1, Width(sourceTaskListRect));
				var height = Scale(_verticalHeight);
				var left = sourceTaskListRect.Left - containerRect.Left;
				var top = sourceTaskListRect.Top - containerRect.Top;
				var taskHeight = Math.Max(1, Height(sourceTaskListRect) - height);
				if (placeOnLeft)
				{
					NativeMethods.MoveWindow(this._form.Handle, Clamp(left + horizontalOffset, 0, Math.Max(0, Width(containerRect) - width)), top + verticalOffset, width, height, true);
					NativeMethods.MoveWindow(this._taskListHandle, left, top + height, width, taskHeight, true);
				}
				else
				{
					NativeMethods.MoveWindow(this._taskListHandle, left, top, width, taskHeight, true);
					NativeMethods.MoveWindow(this._form.Handle, Clamp(left + horizontalOffset, 0, Math.Max(0, Width(containerRect) - width)), top + taskHeight + verticalOffset, width, height, true);
				}
				this.ShowDeskbandFormIfRendered();
			}
		}

		private void UpdateModernLayout()
		{
			if (this._activeMode != DeskbandMode.ModernTaskbar) return;
			if (!NativeMethods.GetWindowRect(this._taskbarHandle, out var taskbarRect)) return;

			var horizontal = Width(taskbarRect) >= Height(taskbarRect);
			var placeOnLeft = IsDeskbandPlacedOnLeft();
			if (horizontal)
			{
				var taskbarWidth = Math.Max(1, Width(taskbarRect));
				var height = Math.Max(1, Height(taskbarRect));
				var minWidth = Scale(_horizontalMinWidth);
				var maxWidth = Math.Min(Scale(_horizontalMaxWidth), Math.Max(minWidth, taskbarWidth / 3));
				var width = this._form.GetDesiredWidth(minWidth, maxWidth);
				var margin = Scale(_modernEdgeMargin);
				var left = placeOnLeft
					? Math.Min(taskbarWidth - width, Scale(_modernLeftReservedWidth) + margin)
					: GetModernRightEdge(taskbarRect) - width - margin;
				left += GetDeskbandHorizontalOffset();
				left = Math.Max(0, Math.Min(taskbarWidth - width, left));

				NativeMethods.MoveWindow(this._form.Handle, left, GetDeskbandVerticalOffset(), width, height, true);
			}
			else
			{
				var width = Math.Max(1, Width(taskbarRect));
				var height = Scale(_verticalHeight);
				var taskbarHeight = Math.Max(1, Height(taskbarRect));
				var margin = Scale(_modernEdgeMargin);
				var top = placeOnLeft
					? margin
					: taskbarHeight - height - margin;
				top += GetDeskbandVerticalOffset();

				NativeMethods.MoveWindow(this._form.Handle, Clamp(GetDeskbandHorizontalOffset(), 0, Math.Max(0, Width(taskbarRect) - width)), top, width, height, true);
			}

			this.ShowDeskbandFormIfRendered();
		}

		private void ShowDeskbandFormIfRendered()
		{
			if (this._form != null && this._form.RenderLayered())
			{
				NativeMethods.ShowWindow(this._form.Handle, ShowWindowCommand.ShowNoActivate);
				if (this._activeMode == DeskbandMode.ModernTaskbar)
				{
					NativeMethods.BringWindowToTop(this._form.Handle);
				}
			}
		}

		private void RestoreTaskList()
		{
			if (!this._hasOriginalTaskListRect || this._taskListHandle == IntPtr.Zero || this._containerHandle == IntPtr.Zero) return;
			if (!NativeMethods.IsWindow(this._taskListHandle) || !NativeMethods.GetWindowRect(this._containerHandle, out var containerRect)) return;

			NativeMethods.MoveWindow(
				this._taskListHandle,
				this._originalTaskListRect.Left - containerRect.Left,
				this._originalTaskListRect.Top - containerRect.Top,
				Width(this._originalTaskListRect),
				Height(this._originalTaskListRect),
				true);
			this._hasOriginalTaskListRect = false;
		}

		private static RECT GetInitialTaskListRect(RECT taskListRect, RECT containerRect, bool horizontal, bool placeOnLeft)
		{
			if (!placeOnLeft) return taskListRect;

			var rect = taskListRect;
			var staleOffsetThreshold = Scale(_horizontalMinWidth);
			if (horizontal)
			{
				var staleOffset = rect.Left - containerRect.Left;
				if (staleOffset >= staleOffsetThreshold)
				{
					rect.Left = containerRect.Left;
				}
			}
			else
			{
				var staleOffset = rect.Top - containerRect.Top;
				if (staleOffset >= staleOffsetThreshold)
				{
					rect.Top = containerRect.Top;
				}
			}

			return rect;
		}

		private bool TryFindTaskbarWindows(DeskbandMode mode)
		{
			this._taskbarHandle = NativeMethods.FindWindow("Shell_TrayWnd", null);
			if (this._taskbarHandle == IntPtr.Zero) return false;

			this._trayNotifyHandle = FindDescendantWindowByClass(this._taskbarHandle, "TrayNotifyWnd");
			if (mode == DeskbandMode.ModernTaskbar)
			{
				this._containerHandle = this._taskbarHandle;
				this._taskListHandle = IntPtr.Zero;
				return true;
			}

			this._containerHandle = NativeMethods.FindWindowEx(this._taskbarHandle, IntPtr.Zero, "ReBarWindow32", null);
			if (this._containerHandle == IntPtr.Zero)
			{
				this._containerHandle = NativeMethods.FindWindowEx(this._taskbarHandle, IntPtr.Zero, "WorkerW", null);
			}
			if (this._containerHandle == IntPtr.Zero) return false;

			this._taskListHandle = NativeMethods.FindWindowEx(this._containerHandle, IntPtr.Zero, "MSTaskSwWClass", null);
			if (this._taskListHandle == IntPtr.Zero)
			{
				this._taskListHandle = NativeMethods.FindWindowEx(this._containerHandle, IntPtr.Zero, "MSTaskListWClass", null);
			}

			return this._taskListHandle != IntPtr.Zero;
		}

		private bool AreTaskbarWindowsValid()
		{
			if (!NativeMethods.IsWindow(this._taskbarHandle) || !NativeMethods.IsWindow(this._containerHandle))
			{
				return false;
			}

			return this._activeMode == DeskbandMode.ModernTaskbar
				|| NativeMethods.IsWindow(this._taskListHandle);
		}

		private static void MigrateLegacyEnabledSetting()
		{
			if (Settings.General.TaskbarDeskbandEnabled.Value
				&& Settings.General.TaskbarDeskbandMode.Value == GeneralSettings.TaskbarDeskbandModeDisabledValue)
			{
				Settings.General.TaskbarDeskbandMode.Value = GeneralSettings.TaskbarDeskbandModeLegacyTaskbarValue;
				Settings.General.TaskbarDeskbandEnabled.Value = false;
			}
		}

		private static void MigrateLegacyPositionSetting()
		{
			if (Settings.General.TaskbarDeskbandPlaceOnLeft.Value
				&& Settings.General.TaskbarDeskbandPosition.Value == GeneralSettings.TaskbarDeskbandPositionRightValue)
			{
				Settings.General.TaskbarDeskbandPosition.Value = GeneralSettings.TaskbarDeskbandPositionLeftValue;
				Settings.General.TaskbarDeskbandPlaceOnLeft.Value = false;
			}
		}

		private static void MigrateLegacyRomanDisplayMode()
		{
			if (Settings.General.TaskbarDeskbandDisplayMode.Value == GeneralSettings.TaskbarDeskbandDisplayModeRomanNumberValue)
			{
				Settings.General.TaskbarDeskbandDisplayMode.Value = GeneralSettings.TaskbarDeskbandDisplayModeNumberOnlyValue;
				Settings.General.TaskbarDeskbandRomanNumber.Value = true;
			}
		}

		private static bool IsDeskbandPlacedOnLeft()
			=> Settings.General.TaskbarDeskbandPosition.Value == GeneralSettings.TaskbarDeskbandPositionLeftValue;

		private static int GetDeskbandHorizontalOffset()
			=> Settings.General.TaskbarDeskbandPositionOffset.Value;

		private static int GetDeskbandVerticalOffset()
			=> Settings.General.TaskbarDeskbandVerticalPositionOffset.Value;

		private static DeskbandMode GetDeskbandMode()
		{
			var value = Settings.General.TaskbarDeskbandMode.Value;
			if (value == GeneralSettings.TaskbarDeskbandModeModernTaskbarValue) return DeskbandMode.ModernTaskbar;
			if (value == GeneralSettings.TaskbarDeskbandModeLegacyTaskbarValue) return DeskbandMode.LegacyTaskbar;
			return DeskbandMode.Disabled;
		}

		private int GetModernRightEdge(RECT taskbarRect)
		{
			if (this._trayNotifyHandle != IntPtr.Zero
				&& NativeMethods.IsWindow(this._trayNotifyHandle)
				&& NativeMethods.GetWindowRect(this._trayNotifyHandle, out var trayRect))
			{
				var rightEdge = trayRect.Left - taskbarRect.Left;
				if (rightEdge > 0) return rightEdge;
			}

			return Width(taskbarRect);
		}

		private void UpdateTextAndLayout()
		{
			this.UpdateText(show: false, render: false);
			this.UpdateLayout();
		}

		private void UpdateText(bool show = true, bool render = true)
		{
			if (this._form == null) return;

			if (!TryGetDeskbandInfo(out var text, out var tooltip)) return;
			if (this._form.SetDesktopInfo(text, tooltip, render) && show)
			{
				NativeMethods.ShowWindow(this._form.Handle, ShowWindowCommand.ShowNoActivate);
			}
		}

		private static bool TryGetDeskbandInfo(out string text, out string tooltip)
		{
			text = string.Empty;
			tooltip = string.Empty;

			try
			{
				var currentDesktop = VirtualDesktop.Current;
				var currentDesktopIndex = Array.IndexOf(VirtualDesktop.AllDesktops, currentDesktop) + 1;
				if (currentDesktopIndex <= 0) return true;

				var desktopName = GetDesktopName(currentDesktopIndex, currentDesktop);
				text = GetDeskbandText(currentDesktopIndex, desktopName);
				tooltip = GetDesktopTooltip(currentDesktopIndex, desktopName, currentDesktop);
				return true;
			}
			catch (COMException ex) when (IsVirtualDesktopTemporarilyUnavailable(ex))
			{
				return false;
			}
		}

		private static bool IsVirtualDesktopTemporarilyUnavailable(COMException ex)
			=> ex.ErrorCode == _rpcServerUnavailableHResult
				|| ex.ErrorCode == _rpcCallFailedHResult
				|| ex.ErrorCode == _rpcDisconnectedHResult;

		private void OnCurrentDesktopChanged(object sender, VirtualDesktopChangedEventArgs e)
		{
			this.UpdateTextAndLayout();
		}

		private void OnDesktopCreated(object sender, VirtualDesktop e)
		{
			this.UpdateTextAndLayout();
		}

		private void OnDesktopDestroyed(object sender, VirtualDesktopDestroyEventArgs e)
		{
			this.UpdateTextAndLayout();
		}

		private void OnDesktopRenamed(object sender, VirtualDesktopRenamedEventArgs e)
		{
			this.UpdateTextAndLayout();
		}

		private void OnSystemThemeChanged(object sender, Theme e)
		{
			this.UpdateAppearance();
		}

		private void OnDesktopNamesChanged(object sender, PropertyChangedEventArgs e)
		{
			this.UpdateTextAndLayout();
		}

		private void UpdateAppearance()
		{
			if (this._form == null) return;

			if (this._form.SetAppearance(CreateAppearance(WindowsTheme.SystemTheme.Current)))
			{
				NativeMethods.ShowWindow(this._form.Handle, ShowWindowCommand.ShowNoActivate);
				this.UpdateLayout();
			}
		}

		private static DeskbandAppearance CreateAppearance(Theme theme)
		{
			if (Settings.General.TaskbarDeskbandCustomAppearanceEnabled)
			{
				var fontWeightValue = GetCustomFontWeightValue();
				var fontWeight = GetCustomFontWeight(fontWeightValue);
				var fontStyle = fontWeight >= _boldFontWeight ? FontStyle.Bold : FontStyle.Regular;
				if (Settings.General.TaskbarDeskbandFontItalic) fontStyle |= FontStyle.Italic;
				if (Settings.General.TaskbarDeskbandFontUnderline) fontStyle |= FontStyle.Underline;

				return new DeskbandAppearance(
					CreateCustomFont(fontStyle, fontWeightValue, fontWeight),
					GetCustomTextColor(theme),
					theme,
					$"custom:{GetCustomFontFamily()}:{GetCustomFontSize()}:{fontStyle}:{fontWeight}");
			}

			return CreateDefaultAppearance(theme);
		}

		private static string GetCustomFontFamily()
		{
			var fontFamily = Settings.General.TaskbarDeskbandFontFamily.Value;
			return string.IsNullOrWhiteSpace(fontFamily)
				? GeneralSettings.TaskbarDeskbandFontFamilyDefaultValue
				: fontFamily;
		}

		private static float GetCustomFontSize()
		{
			var fontSize = Settings.General.TaskbarDeskbandFontSize.Value;
			if (fontSize < 4) return 4;
			if (fontSize > 72) return 72;
			return fontSize;
		}

		private static uint GetCustomFontWeightValue()
		{
			var value = Settings.General.TaskbarDeskbandFontWeight.Value;
			if (value == GeneralSettings.TaskbarDeskbandFontWeightRegularValue
				&& Settings.General.TaskbarDeskbandFontBold.Value)
			{
				return GeneralSettings.TaskbarDeskbandFontWeightBoldValue;
			}

			return value;
		}

		private static int GetCustomFontWeight(uint value)
		{
			if (value == GeneralSettings.TaskbarDeskbandFontWeightLightValue) return _lightFontWeight;
			if (value == GeneralSettings.TaskbarDeskbandFontWeightSemiLightValue) return _semiLightFontWeight;
			if (value == GeneralSettings.TaskbarDeskbandFontWeightSemiBoldValue) return _semiBoldFontWeight;
			if (value == GeneralSettings.TaskbarDeskbandFontWeightBoldValue) return _boldFontWeight;
			return _regularFontWeight;
		}

		private static Font CreateCustomFont(FontStyle fontStyle, uint fontWeightValue, int fontWeight)
		{
			var logFont = CreateLogFont(
				GetCustomFontFamilyForWeight(fontWeightValue),
				GetCustomFontSize(),
				fontStyle,
				fontWeight);
			if (TryCreateFontFromLogFont(logFont, out var logFontFont))
			{
				return logFontFont;
			}

			try
			{
				return new Font(GetCustomFontFamilyForWeight(fontWeightValue), GetCustomFontSize(), fontStyle, GraphicsUnit.Point);
			}
			catch
			{
				return new Font(GeneralSettings.TaskbarDeskbandFontFamilyDefaultValue, GetCustomFontSize(), fontStyle, GraphicsUnit.Point);
			}
		}

		private static string GetCustomFontFamilyForWeight(uint fontWeightValue)
		{
			var fontFamily = GetCustomFontFamily();
			if (!IsSegoeFontFamily(fontFamily)) return fontFamily;

			if (fontWeightValue == GeneralSettings.TaskbarDeskbandFontWeightLightValue) return "Segoe UI Light";
			if (fontWeightValue == GeneralSettings.TaskbarDeskbandFontWeightSemiLightValue) return "Segoe UI Semilight";
			if (fontWeightValue == GeneralSettings.TaskbarDeskbandFontWeightSemiBoldValue) return "Segoe UI Semibold";
			return fontFamily;
		}

		private static bool IsSegoeFontFamily(string fontFamily)
			=> fontFamily != null
				&& fontFamily.StartsWith("Segoe UI", StringComparison.OrdinalIgnoreCase);

		private static NativeMethods.LogFont CreateLogFont(string faceName, float fontSizeInPoints, FontStyle fontStyle, int weight)
		{
			return new NativeMethods.LogFont
			{
				lfHeight = PointsToLogFontHeight(fontSizeInPoints),
				lfWeight = weight,
				lfItalic = (fontStyle & FontStyle.Italic) == FontStyle.Italic ? (byte)1 : (byte)0,
				lfUnderline = (fontStyle & FontStyle.Underline) == FontStyle.Underline ? (byte)1 : (byte)0,
				lfCharSet = 1,
				lfQuality = 5,
				lfFaceName = string.IsNullOrWhiteSpace(faceName) ? GeneralSettings.TaskbarDeskbandFontFamilyDefaultValue : faceName,
			};
		}

		private static DeskbandAppearance CreateDefaultAppearance(Theme theme)
		{
			var font = CreateDefaultDeskbandFont();
			return new DeskbandAppearance(
				font,
				GetDefaultTextColor(theme),
				theme,
				"default:" + font.FontFamily.Name + ":" + font.SizeInPoints + ":" + font.Style);
		}

		private static Font CreateDefaultDeskbandFont()
		{
			var logFont = CreateLogFont(
				GeneralSettings.TaskbarDeskbandFontFamilyDefaultValue,
				GeneralSettings.TaskbarDeskbandFontSizeDefaultValue,
				FontStyle.Regular,
				_regularFontWeight);
			if (TryCreateFontFromLogFont(logFont, out var logFontFont))
			{
				return logFontFont;
			}

			try
			{
				return new Font(
					GeneralSettings.TaskbarDeskbandFontFamilyDefaultValue,
					GeneralSettings.TaskbarDeskbandFontSizeDefaultValue,
					FontStyle.Regular,
					GraphicsUnit.Point);
			}
			catch
			{
				return CreateFallbackDefaultFont();
			}
		}

		private static bool TryGetTaskbarClockLogFont(IntPtr taskbarHandle, out NativeMethods.LogFont logFont)
		{
			logFont = default(NativeMethods.LogFont);
			if (taskbarHandle == IntPtr.Zero)
			{
				taskbarHandle = NativeMethods.FindWindow("Shell_TrayWnd", null);
			}
			if (taskbarHandle == IntPtr.Zero) return false;

			var clockHandle = FindTaskbarClockWindow(taskbarHandle);
			if (clockHandle == IntPtr.Zero) return false;

			var fontHandle = NativeMethods.SendMessage(clockHandle, NativeMethods.WmGetFont, IntPtr.Zero, IntPtr.Zero);
			if (fontHandle == IntPtr.Zero) return false;

			return NativeMethods.GetObject(fontHandle, Marshal.SizeOf(typeof(NativeMethods.LogFont)), ref logFont) != 0;
		}

		private static Font CreateFallbackDefaultFont()
		{
			var logFont = CreateLogFont("Segoe UI", 9.0f, FontStyle.Regular, _regularFontWeight);
			if (TryCreateFontFromLogFont(logFont, out var font))
			{
				return font;
			}

			return new Font("Segoe UI", 9.0f, FontStyle.Bold, GraphicsUnit.Point);
		}

		private static bool TryGetSystemMessageLogFont(out NativeMethods.LogFont logFont)
		{
			logFont = default(NativeMethods.LogFont);
			var metrics = new NativeMethods.NonClientMetrics
			{
				Size = Marshal.SizeOf(typeof(NativeMethods.NonClientMetrics)),
			};

			if (!NativeMethods.SystemParametersInfo(
				NativeMethods.SpiGetNonClientMetrics,
				metrics.Size,
				ref metrics,
				0))
			{
				return false;
			}

			logFont = metrics.MessageFont;
			return !string.IsNullOrWhiteSpace(logFont.lfFaceName);
		}

		private static bool TryCreateFontFromLogFont(NativeMethods.LogFont logFont, out Font font)
		{
			font = null;
			try
			{
				font = Font.FromLogFont(logFont);
				return true;
			}
			catch
			{
				try
				{
					var style = FontStyle.Regular;
					if (logFont.lfWeight >= 600) style |= FontStyle.Bold;
					if (logFont.lfItalic != 0) style |= FontStyle.Italic;
					if (logFont.lfUnderline != 0) style |= FontStyle.Underline;

					font = new Font(
						string.IsNullOrWhiteSpace(logFont.lfFaceName) ? "Segoe UI" : logFont.lfFaceName,
						LogFontHeightToPoints(logFont.lfHeight),
						style,
						GraphicsUnit.Point);
					return true;
				}
				catch
				{
					return false;
				}
			}
		}

		private static float LogFontHeightToPoints(int height)
		{
			using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
			{
				var pixels = Math.Abs(height);
				if (pixels == 0) pixels = 12;
				return (float)(pixels * 72.0 / graphics.DpiY);
			}
		}

		private static int PointsToLogFontHeight(float points)
		{
			using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
			{
				return -(int)Math.Round(points * graphics.DpiY / 72.0);
			}
		}

		private static string GetLogFontSignature(NativeMethods.LogFont logFont)
		{
			return string.Join(
				":",
				logFont.lfFaceName,
				logFont.lfHeight,
				logFont.lfWidth,
				logFont.lfWeight,
				logFont.lfItalic,
				logFont.lfUnderline,
				logFont.lfStrikeOut,
				logFont.lfCharSet,
				logFont.lfQuality,
				logFont.lfPitchAndFamily);
		}

		private static IntPtr FindTaskbarClockWindow(IntPtr taskbarHandle)
		{
			var trayNotify = FindDescendantWindowByClass(taskbarHandle, "TrayNotifyWnd");
			var root = trayNotify != IntPtr.Zero ? trayNotify : taskbarHandle;
			var clock = FindDescendantWindowByClass(root, "TrayClockWClass", "ClockButton");
			return clock != IntPtr.Zero
				? clock
				: FindDescendantWindowByClassNamePart(root, "Clock");
		}

		private static IntPtr FindDescendantWindowByClass(IntPtr parent, params string[] classNames)
		{
			return FindDescendantWindow(parent, className =>
				Array.Exists(classNames, name => string.Equals(className, name, StringComparison.Ordinal)));
		}

		private static IntPtr FindDescendantWindowByClassNamePart(IntPtr parent, string classNamePart)
		{
			return FindDescendantWindow(parent, className =>
				className.IndexOf(classNamePart, StringComparison.OrdinalIgnoreCase) >= 0);
		}

		private static IntPtr FindDescendantWindow(IntPtr parent, Predicate<string> match)
		{
			if (parent == IntPtr.Zero) return IntPtr.Zero;

			var found = IntPtr.Zero;
			EnumWindowsDelegate callback = (handle, lParam) =>
			{
				var className = GetWindowClassName(handle);
				if (match(className))
				{
					found = handle;
					return false;
				}

				var descendant = FindDescendantWindow(handle, match);
				if (descendant != IntPtr.Zero)
				{
					found = descendant;
					return false;
				}

				return true;
			};

			NativeMethods.EnumChildWindows(parent, callback, IntPtr.Zero);
			return found;
		}

		private static string GetWindowClassName(IntPtr handle)
		{
			var className = new StringBuilder(256);
			return NativeMethods.GetClassName(handle, className, className.Capacity) > 0
				? className.ToString()
				: string.Empty;
		}

		private static Color GetCustomTextColor(Theme theme)
		{
			return TryParseColor(Settings.General.TaskbarDeskbandFontColor.Value, out var color)
				? color
				: GetDefaultTextColor(theme);
		}

		private static Color GetDefaultTextColor(Theme theme)
		{
			return theme == Theme.Light
				? Color.FromArgb(32, 32, 32)
				: Color.White;
		}

		private static bool TryParseColor(string value, out Color color)
		{
			color = Color.Empty;
			if (string.IsNullOrWhiteSpace(value)) return false;

			try
			{
				color = ColorTranslator.FromHtml(value);
				return color.A > 0 || value.IndexOf("transparent", StringComparison.OrdinalIgnoreCase) < 0;
			}
			catch
			{
				return false;
			}
		}

		private static string GetDeskbandText(int number, string desktopName)
		{
			var mode = Settings.General.TaskbarDeskbandDisplayMode.Value;
			if (mode == GeneralSettings.TaskbarDeskbandDisplayModeDesktopNumberValue)
			{
				return $"Desktop {FormatDesktopNumber(number)}";
			}

			if (mode == GeneralSettings.TaskbarDeskbandDisplayModeNameOnlyValue)
			{
				return !string.IsNullOrWhiteSpace(desktopName)
					? desktopName
					: $"Desktop {FormatDesktopNumber(number)}";
			}

			if (mode == GeneralSettings.TaskbarDeskbandDisplayModeNameWithNumberValue)
			{
				var formattedNumber = FormatDesktopNumber(number);
				var name = GetDesktopNameWithoutDefaultNumber(number, desktopName);

				return Settings.General.TaskbarDeskbandNumberBeforeName
					? $"{formattedNumber} {name}"
					: $"{name} {formattedNumber}";
			}

			return FormatDesktopNumber(number);
		}

		private static string FormatDesktopNumber(int number)
		{
			var useRomanNumber = Settings.General.TaskbarDeskbandRomanNumber.Value;
			var numberText = FormatDesktopNumberText(number, useRomanNumber);
			GetNumberWrapper(out var open, out var close);
			if (string.IsNullOrEmpty(open) && string.IsNullOrEmpty(close)) return numberText;

			if (Settings.General.TaskbarDeskbandNumberWrapperSpaces.Value)
			{
				return $"{open} {numberText} {close}";
			}

			return open + numberText + close;
		}

		private static string GetDesktopNameWithoutDefaultNumber(int number, string desktopName)
		{
			if (string.IsNullOrWhiteSpace(desktopName)) return "Desktop";

			var trimmedName = desktopName.Trim();
			var defaultName = $"Desktop {number}";
			return string.Equals(trimmedName, defaultName, StringComparison.OrdinalIgnoreCase)
				? "Desktop"
				: desktopName;
		}

		private static void GetNumberWrapper(out string open, out string close)
		{
			switch (Settings.General.TaskbarDeskbandNumberWrapper.Value)
			{
				case var value when value == GeneralSettings.TaskbarDeskbandNumberWrapperNoneValue:
					open = string.Empty;
					close = string.Empty;
					return;

				case var value when value == GeneralSettings.TaskbarDeskbandNumberWrapperRoundValue:
					open = "(";
					close = ")";
					return;

				case var value when value == GeneralSettings.TaskbarDeskbandNumberWrapperCurlyValue:
					open = "{";
					close = "}";
					return;

				case var value when value == GeneralSettings.TaskbarDeskbandNumberWrapperAngleValue:
					open = "<";
					close = ">";
					return;

				case var value when value == GeneralSettings.TaskbarDeskbandNumberWrapperSingleQuoteValue:
					open = "'";
					close = "'";
					return;

				case var value when value == GeneralSettings.TaskbarDeskbandNumberWrapperDoubleQuoteValue:
					open = "\"";
					close = "\"";
					return;

				case var value when value == GeneralSettings.TaskbarDeskbandNumberWrapperPipeValue:
					open = "|";
					close = "|";
					return;

				case var value when value == GeneralSettings.TaskbarDeskbandNumberWrapperSlashValue:
					open = "/";
					close = "/";
					return;

				default:
					open = "[";
					close = "]";
					return;
			}
		}

		private static string FormatDesktopNumberText(int number, bool useRomanNumber)
		{
			var numberText = useRomanNumber ? ToRomanNumber(number) : number.ToString();
			if (!Settings.General.TaskbarDeskbandShowTotalDesktopCount.Value) return numberText;

			var totalDesktopCount = Math.Max(1, VirtualDesktop.Count);
			var totalText = useRomanNumber ? ToRomanNumber(totalDesktopCount) : totalDesktopCount.ToString();
			return $"{numberText}/{totalText}";
		}

		private static Size MeasureLayeredSingleLineText(string text, Font font)
		{
			if (UseDirectWriteFontRendering())
			{
				return MeasureLayeredSingleLineTextDirectWrite(text, font);
			}

			if (UseGdiPlusFontRendering())
			{
				return MeasureLayeredSingleLineTextGdiPlus(text, font);
			}

			return TextRenderer.MeasureText(
				text ?? string.Empty,
				font,
				new Size(int.MaxValue, int.MaxValue),
				TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		}

		private static void DrawLayeredSingleLineText(Graphics graphics, string text, Font font, Color color, Rectangle bounds)
		{
			if (UseDirectWriteFontRendering())
			{
				DrawLayeredSingleLineTextDirectWrite(graphics, text, font, color, bounds);
				return;
			}

			if (UseGdiPlusFontRendering())
			{
				DrawLayeredSingleLineTextGdiPlus(graphics, text, font, color, bounds);
				return;
			}

			DrawLayeredTextMask(
				graphics,
				text,
				font,
				color,
				bounds,
				TextFormatFlags.HorizontalCenter
					| TextFormatFlags.VerticalCenter
					| TextFormatFlags.SingleLine
					| TextFormatFlags.NoPadding
					| TextFormatFlags.NoPrefix
					| TextFormatFlags.EndEllipsis);
		}

		private static Size MeasureLayeredTextBlock(string text, Font font, int maxWidth)
		{
			if (UseDirectWriteFontRendering())
			{
				return MeasureLayeredTextBlockDirectWrite(text, font, maxWidth);
			}

			if (UseGdiPlusFontRendering())
			{
				return MeasureLayeredTextBlockGdiPlus(text, font, maxWidth);
			}

			return TextRenderer.MeasureText(
				text ?? string.Empty,
				font,
				new Size(maxWidth, int.MaxValue),
				TextFormatFlags.NoPadding | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
		}

		private static void DrawLayeredTextBlock(Graphics graphics, string text, Font font, Color color, Rectangle bounds)
		{
			if (UseDirectWriteFontRendering())
			{
				DrawLayeredTextBlockDirectWrite(graphics, text, font, color, bounds);
				return;
			}

			if (UseGdiPlusFontRendering())
			{
				DrawLayeredTextBlockGdiPlus(graphics, text, font, color, bounds);
				return;
			}

			DrawLayeredTextMask(
				graphics,
				text,
				font,
				color,
				bounds,
				TextFormatFlags.NoPadding | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
		}

		private static bool UseGdiPlusFontRendering()
			=> Settings.General.TaskbarDeskbandFontRenderingMode.Value == GeneralSettings.TaskbarDeskbandFontRenderingModeGdiPlusValue;

		private static bool UseDirectWriteFontRendering()
			=> Settings.General.TaskbarDeskbandFontRenderingMode.Value == GeneralSettings.TaskbarDeskbandFontRenderingModeDirectWriteValue;

		private static Size MeasureLayeredSingleLineTextDirectWrite(string text, Font font)
		{
			var formattedText = CreateDirectWriteText(text, font, Color.White);
			return new Size(
				Math.Max(1, (int)Math.Ceiling(formattedText.WidthIncludingTrailingWhitespace)),
				Math.Max(1, (int)Math.Ceiling(formattedText.Height)));
		}

		private static void DrawLayeredSingleLineTextDirectWrite(Graphics graphics, string text, Font font, Color color, Rectangle bounds)
		{
			if (bounds.Width <= 0 || bounds.Height <= 0) return;

			var formattedText = CreateDirectWriteText(text, font, color);
			formattedText.MaxTextWidth = bounds.Width;
			formattedText.MaxTextHeight = bounds.Height;
			formattedText.TextAlignment = System.Windows.TextAlignment.Center;
			formattedText.Trimming = System.Windows.TextTrimming.CharacterEllipsis;

			var y = bounds.Y + Math.Max(0, (bounds.Height - formattedText.Height) / 2.0);
			DrawDirectWriteText(graphics, formattedText, bounds, new System.Windows.Point(0, y - bounds.Y));
		}

		private static Size MeasureLayeredTextBlockDirectWrite(string text, Font font, int maxWidth)
		{
			var formattedText = CreateDirectWriteText(text, font, Color.White);
			formattedText.MaxTextWidth = Math.Max(1, maxWidth);
			return new Size(
				Math.Max(1, (int)Math.Ceiling(formattedText.WidthIncludingTrailingWhitespace)),
				Math.Max(1, (int)Math.Ceiling(formattedText.Height)));
		}

		private static void DrawLayeredTextBlockDirectWrite(Graphics graphics, string text, Font font, Color color, Rectangle bounds)
		{
			if (bounds.Width <= 0 || bounds.Height <= 0) return;

			var formattedText = CreateDirectWriteText(text, font, color);
			formattedText.MaxTextWidth = bounds.Width;
			formattedText.MaxTextHeight = bounds.Height;
			DrawDirectWriteText(graphics, formattedText, bounds, new System.Windows.Point(0, 0));
		}

		private static System.Windows.Media.FormattedText CreateDirectWriteText(string text, Font font, Color color)
		{
			var typeface = new System.Windows.Media.Typeface(
				new System.Windows.Media.FontFamily(font.FontFamily.Name),
				font.Italic ? System.Windows.FontStyles.Italic : System.Windows.FontStyles.Normal,
				font.Bold ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Regular,
				System.Windows.FontStretches.Normal);
			var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
			var formattedText = new System.Windows.Media.FormattedText(
				text ?? string.Empty,
				CultureInfo.CurrentUICulture,
				System.Windows.FlowDirection.LeftToRight,
				typeface,
				font.SizeInPoints * 96.0 / 72.0,
				brush,
				1.0);

			if (font.Underline)
			{
				formattedText.SetTextDecorations(System.Windows.TextDecorations.Underline);
			}

			return formattedText;
		}

		private static void DrawDirectWriteText(Graphics graphics, System.Windows.Media.FormattedText formattedText, Rectangle bounds, System.Windows.Point origin)
		{
			var visual = new System.Windows.Media.DrawingVisual();
			using (var context = visual.RenderOpen())
			{
				context.DrawText(formattedText, origin);
			}

			var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
				bounds.Width,
				bounds.Height,
				96,
				96,
				System.Windows.Media.PixelFormats.Pbgra32);
			bitmap.Render(visual);

			var stride = bounds.Width * 4;
			var pixels = new byte[stride * bounds.Height];
			bitmap.CopyPixels(pixels, stride, 0);

			using (var renderedText = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb))
			{
				var data = renderedText.LockBits(
					new Rectangle(0, 0, bounds.Width, bounds.Height),
					ImageLockMode.WriteOnly,
					PixelFormat.Format32bppPArgb);
				try
				{
					Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
				}
				finally
				{
					renderedText.UnlockBits(data);
				}

				graphics.DrawImageUnscaled(renderedText, bounds.X, bounds.Y);
			}
		}

		private static Size MeasureLayeredSingleLineTextGdiPlus(string text, Font font)
		{
			using (var bitmap = new Bitmap(1, 1))
			using (var graphics = Graphics.FromImage(bitmap))
			using (var format = CreateSingleLineStringFormat())
			{
				graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
				var size = graphics.MeasureString(text ?? string.Empty, font, int.MaxValue, format);
				return new Size((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
			}
		}

		private static void DrawLayeredSingleLineTextGdiPlus(Graphics graphics, string text, Font font, Color color, Rectangle bounds)
		{
			graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
			using (var brush = new SolidBrush(color))
			using (var format = CreateSingleLineStringFormat())
			{
				format.Alignment = StringAlignment.Center;
				format.LineAlignment = StringAlignment.Center;
				graphics.DrawString(text ?? string.Empty, font, brush, bounds, format);
			}
		}

		private static Size MeasureLayeredTextBlockGdiPlus(string text, Font font, int maxWidth)
		{
			using (var bitmap = new Bitmap(1, 1))
			using (var graphics = Graphics.FromImage(bitmap))
			using (var format = CreateTextBlockStringFormat())
			{
				graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
				var size = graphics.MeasureString(text ?? string.Empty, font, maxWidth, format);
				return new Size((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
			}
		}

		private static void DrawLayeredTextBlockGdiPlus(Graphics graphics, string text, Font font, Color color, Rectangle bounds)
		{
			graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
			using (var brush = new SolidBrush(color))
			using (var format = CreateTextBlockStringFormat())
			{
				graphics.DrawString(text ?? string.Empty, font, brush, bounds, format);
			}
		}

		private static StringFormat CreateSingleLineStringFormat()
		{
			var format = new StringFormat(StringFormat.GenericTypographic);
			format.FormatFlags |= StringFormatFlags.NoWrap;
			format.Trimming = StringTrimming.EllipsisCharacter;
			return format;
		}

		private static StringFormat CreateTextBlockStringFormat()
		{
			var format = new StringFormat(StringFormat.GenericTypographic);
			format.Trimming = StringTrimming.Word;
			return format;
		}

		private static void DrawLayeredTextMask(Graphics graphics, string text, Font font, Color color, Rectangle bounds, TextFormatFlags flags)
		{
			if (bounds.Width <= 0 || bounds.Height <= 0) return;

			using (var mask = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb))
			using (var maskGraphics = Graphics.FromImage(mask))
			{
				maskGraphics.Clear(Color.Black);
				TextRenderer.DrawText(
					maskGraphics,
					text ?? string.Empty,
					font,
					new Rectangle(0, 0, bounds.Width, bounds.Height),
					Color.White,
					Color.Black,
					flags);

				ApplyTextColorToMask(mask, color);
				graphics.DrawImageUnscaled(mask, bounds.X, bounds.Y);
			}
		}

		private static void ApplyTextColorToMask(Bitmap mask, Color color)
		{
			var bounds = new Rectangle(0, 0, mask.Width, mask.Height);
			var data = mask.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
			try
			{
				var stride = Math.Abs(data.Stride);
				var buffer = new byte[stride * mask.Height];
				Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

				for (var y = 0; y < mask.Height; y++)
				{
					var row = y * stride;
					for (var x = 0; x < mask.Width; x++)
					{
						var index = row + x * 4;
						var coverage = Math.Max(buffer[index + 2], Math.Max(buffer[index + 1], buffer[index]));
						var alpha = coverage * color.A / 255;

						buffer[index] = color.B;
						buffer[index + 1] = color.G;
						buffer[index + 2] = color.R;
						buffer[index + 3] = (byte)alpha;
					}
				}

				Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
			}
			finally
			{
				mask.UnlockBits(data);
			}
		}

		private static string ToRomanNumber(int value)
		{
			if (value <= 0 || value > 3999) return value.ToString();

			var map = new[]
			{
				new { Value = 1000, Text = "M" },
				new { Value = 900, Text = "CM" },
				new { Value = 500, Text = "D" },
				new { Value = 400, Text = "CD" },
				new { Value = 100, Text = "C" },
				new { Value = 90, Text = "XC" },
				new { Value = 50, Text = "L" },
				new { Value = 40, Text = "XL" },
				new { Value = 10, Text = "X" },
				new { Value = 9, Text = "IX" },
				new { Value = 5, Text = "V" },
				new { Value = 4, Text = "IV" },
				new { Value = 1, Text = "I" },
			};
			var result = new StringBuilder();
			foreach (var item in map)
			{
				while (value >= item.Value)
				{
					result.Append(item.Text);
					value -= item.Value;
				}
			}

			return result.ToString();
		}

		private static string GetDesktopName(int number, VirtualDesktop desktop)
		{
			var name = desktop?.Name;
			if (!string.IsNullOrWhiteSpace(name)) return name;

			var generalSettings = Settings.General;
			var desktopNames = generalSettings.DesktopNames.Value;
			var index = number - 1;
			if (desktopNames.Count >= number
				&& !string.IsNullOrEmpty(desktopNames[index].Value))
			{
				return desktopNames[index].Value;
			}

			return string.Empty;
		}

		private static string GetDesktopTooltip(int number, string desktopName, VirtualDesktop desktop)
		{
			if (!Settings.General.TaskbarDeskbandTooltipEnabled.Value) return string.Empty;

			var builder = new StringBuilder();
			builder.Append("Desktop ").Append(number);
			if (!Settings.General.TaskbarDeskbandTooltipNumberOnly.Value
				&& !string.IsNullOrWhiteSpace(desktopName))
			{
				builder.Append(": ").Append(desktopName);
			}

			if (Settings.General.TaskbarDeskbandShowTotalDesktopCount.Value)
			{
				builder.AppendLine();
				builder.Append(GetDesktopCountTooltipLine());
			}

			if (Settings.General.TaskbarDeskbandTooltipListWindows.Value)
			{
				var windowLines = GetDesktopWindowLines(desktop);
				builder.AppendLine();
				builder.AppendLine();
				if (windowLines.Count == 0)
				{
					builder.Append("Empty");
				}
				else
				{
					builder.Append("Includes:");
					foreach (var line in windowLines)
					{
						builder.AppendLine();
						builder.Append("- ").Append(line);
					}
				}
			}

			return builder.ToString();
		}

		private static string GetDesktopCountTooltipLine()
		{
			var count = Math.Max(1, VirtualDesktop.Count);
			return count == 1
				? "1 available desktop"
				: $"{count} available desktops";
		}

		private static IReadOnlyList<string> GetDesktopWindowLines(VirtualDesktop desktop)
		{
			var lines = new List<string>();
			if (desktop == null) return lines;

			var shellWindow = NativeMethods.GetShellWindow();
			EnumWindowsDelegate callback = (handle, lParam) =>
			{
				if (handle == shellWindow || !NativeMethods.IsWindowVisible(handle)) return true;
				if (NativeMethods.GetAncestor(handle, GetAncestorFlags.Root) != handle) return true;

				try
				{
					if (VirtualDesktop.FromHwnd(handle) != desktop) return true;
				}
				catch
				{
					return true;
				}

				var title = GetWindowTitle(handle);
				var applicationName = GetApplicationName(handle);
				var line = FormatTooltipWindowLine(applicationName, title);
				if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
				return true;
			};

			NativeMethods.EnumWindows(callback, IntPtr.Zero);
			return lines;
		}

		private static string GetWindowTitle(IntPtr handle)
		{
			var length = NativeMethods.GetWindowTextLength(handle);
			if (length <= 0) return string.Empty;

			var title = new StringBuilder(length + 1);
			return NativeMethods.GetWindowText(handle, title, title.Capacity) > 0
				? title.ToString()
				: string.Empty;
		}

		private static string GetApplicationName(IntPtr handle)
		{
			NativeMethods.GetWindowThreadProcessId(handle, out var processId);
			if (processId <= 0) return string.Empty;

			try
			{
				using (var process = Process.GetProcessById(processId))
				{
					return process.ProcessName;
				}
			}
			catch
			{
				return string.Empty;
			}
		}

		private static string FormatTooltipWindowLine(string applicationName, string title)
		{
			var style = Settings.General.TaskbarDeskbandTooltipWindowStyle.Value;
			if (style == GeneralSettings.TaskbarDeskbandTooltipWindowStyleApplicationNameValue)
			{
				return applicationName;
			}

			if (style == GeneralSettings.TaskbarDeskbandTooltipWindowStyleApplicationNameColonTitleValue)
			{
				return CombineApplicationNameAndTitle(applicationName, title, ": ");
			}

			if (style == GeneralSettings.TaskbarDeskbandTooltipWindowStyleApplicationNameDashTitleValue)
			{
				return CombineApplicationNameAndTitle(applicationName, title, " - ");
			}

			return title;
		}

		private static string CombineApplicationNameAndTitle(string applicationName, string title, string separator)
		{
			if (string.IsNullOrWhiteSpace(applicationName)) return title;
			if (string.IsNullOrWhiteSpace(title)) return applicationName;

			return applicationName + separator + title;
		}

		private struct DeskbandAppearance
		{
			public Font TextFont { get; }

			public Color TextColor { get; }

			public Theme Theme { get; }

			public string Signature { get; }

			public DeskbandAppearance(Font textFont, Color textColor, Theme theme, string signature)
			{
				this.TextFont = textFont;
				this.TextColor = textColor;
				this.Theme = theme;
				this.Signature = signature;
			}
		}

		private enum DeskbandMode
		{
			Disabled,
			ModernTaskbar,
			LegacyTaskbar,
		}

		private static int Width(RECT rect) => rect.Right - rect.Left;

		private static int Height(RECT rect) => rect.Bottom - rect.Top;

		private static int Clamp(int value, int min, int max)
			=> Math.Max(min, Math.Min(max, value));

		private static int Scale(int value)
		{
			using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
			{
				return (int)Math.Round(value * graphics.DpiX / 96.0);
			}
		}

		private class DeskbandForm : Form
		{
			private const int _wmContextMenu = 0x007B;
			private const int _wmRButtonUp = 0x0205;

			private Font _textFont;
			private string _textValue = string.Empty;
			private string _tooltipValue = string.Empty;
			private readonly ToolTip _nativeToolTip = new ToolTip { InitialDelay = 300, ReshowDelay = 100, AutoPopDelay = 5000 };
			private readonly Timer _modernTooltipShowTimer = new Timer { Interval = 300 };
			private readonly Timer _modernTooltipHideTimer = new Timer { Interval = 5000 };
			private ModernTooltipForm _modernTooltip;
			private Color _textColor;
			private Theme _theme;
			private string _appearanceSignature;
			private int _lastLoggedUpdateError;

			public DeskbandForm(DeskbandAppearance appearance)
			{
				this.FormBorderStyle = FormBorderStyle.None;
				this.ShowInTaskbar = false;
				this.StartPosition = FormStartPosition.Manual;
				this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
				this.ApplyAppearance(appearance);
				this._modernTooltipShowTimer.Tick += (sender, args) => this.ShowModernTooltip();
				this._modernTooltipHideTimer.Tick += (sender, args) => this.HideModernTooltip();
			}

			protected override void OnPaintBackground(PaintEventArgs e)
			{
			}

			protected override void OnPaint(PaintEventArgs e)
			{
			}

			public bool SetDesktopInfo(string value, string tooltip, bool render = true)
			{
				this._tooltipValue = tooltip ?? string.Empty;
				this.UpdateNativeTooltip();
				if (this._textValue != value)
				{
					this._textValue = value;
				}

				return render && this.RenderLayered();
			}

			protected override void OnMouseEnter(EventArgs e)
			{
				base.OnMouseEnter(e);
				this.StartModernTooltipTimer();
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);
				if (this._modernTooltip?.Visible == true)
				{
					this.PositionModernTooltip();
				}
			}

			protected override void OnMouseLeave(EventArgs e)
			{
				base.OnMouseLeave(e);
				this.HideModernTooltip();
			}

			protected override void OnMouseWheel(MouseEventArgs e)
			{
				base.OnMouseWheel(e);

				if (Settings.General.TaskbarDeskbandSwitchDesktopWithMouseWheel.Value)
				{
					var delta = Settings.General.TaskbarDeskbandSwitchDesktopWithMouseWheelReverse.Value
						? -e.Delta
						: e.Delta;
					VirtualDesktopService.SwitchByMouseWheelDelta(delta);
				}
			}

			protected override void OnMouseDoubleClick(MouseEventArgs e)
			{
				base.OnMouseDoubleClick(e);

				if (e.Button == MouseButtons.Left)
				{
					ExecuteDoubleClickAction();
				}
			}

			protected override void OnMouseClick(MouseEventArgs e)
			{
				base.OnMouseClick(e);

				if (e.Button == MouseButtons.Middle)
				{
					ExecuteMiddleClickAction();
				}
			}

			protected override void WndProc(ref Message m)
			{
				if (m.Msg == _wmRButtonUp || m.Msg == _wmContextMenu)
				{
					this.HideModernTooltip();
					this.ShowDeskbandContextMenu();
					return;
				}

				base.WndProc(ref m);
			}

			private void ShowDeskbandContextMenu()
			{
				var point = this.PointToClient(Cursor.Position);
				(SylphyHorn.Application.Current as SylphyHorn.Application)?.TaskTrayIcon?.ShowContextMenu(this, point);
			}

			private void UpdateNativeTooltip()
			{
				this._nativeToolTip.SetToolTip(this, UseModernTooltipLook() ? string.Empty : this._tooltipValue);
			}

			private void StartModernTooltipTimer()
			{
				this.UpdateNativeTooltip();
				if (!UseModernTooltipLook() || string.IsNullOrWhiteSpace(this._tooltipValue)) return;

				this._modernTooltipShowTimer.Stop();
				this._modernTooltipShowTimer.Start();
			}

			private void ShowModernTooltip()
			{
				this._modernTooltipShowTimer.Stop();
				if (!UseModernTooltipLook() || string.IsNullOrWhiteSpace(this._tooltipValue)) return;

				if (this._modernTooltip == null || this._modernTooltip.IsDisposed)
				{
					this._modernTooltip = new ModernTooltipForm();
				}

				this._modernTooltip.SetTheme(this._theme);
				this._modernTooltip.SetText(this._tooltipValue);
				this.PositionModernTooltip();
				this._modernTooltip.ShowNoActivate();
				this._modernTooltipHideTimer.Stop();
				this._modernTooltipHideTimer.Start();
			}

			private void PositionModernTooltip()
			{
				if (this._modernTooltip == null || this._modernTooltip.IsDisposed) return;

				var cursor = Cursor.Position;
				var screen = Screen.FromPoint(cursor).WorkingArea;
				var x = cursor.X + TaskbarDeskbandService.Scale(12);
				var y = cursor.Y + TaskbarDeskbandService.Scale(18);
				if (x + this._modernTooltip.Width > screen.Right) x = cursor.X - this._modernTooltip.Width - TaskbarDeskbandService.Scale(12);
				if (y + this._modernTooltip.Height > screen.Bottom) y = cursor.Y - this._modernTooltip.Height - TaskbarDeskbandService.Scale(12);

				this._modernTooltip.Location = new Point(Math.Max(screen.Left, x), Math.Max(screen.Top, y));
			}

			private void HideModernTooltip()
			{
				this._modernTooltipShowTimer.Stop();
				this._modernTooltipHideTimer.Stop();
				this._modernTooltip?.Hide();
			}

			private static bool UseModernTooltipLook()
				=> Settings.General.TaskbarDeskbandTooltipLook.Value == GeneralSettings.TaskbarDeskbandTooltipLookWindows11Value;

			public bool SetAppearance(DeskbandAppearance appearance)
			{
				if (this._textFont != null
					&& this._appearanceSignature == appearance.Signature
					&& this._textFont.FontFamily.Name == appearance.TextFont.FontFamily.Name
					&& Math.Abs(this._textFont.SizeInPoints - appearance.TextFont.SizeInPoints) < 0.01f
					&& this._textFont.Style == appearance.TextFont.Style
					&& this._textColor == appearance.TextColor
					&& this._theme == appearance.Theme)
				{
					appearance.TextFont.Dispose();
					return this.RenderLayered();
				}

				this.ApplyAppearance(appearance);
				return this.RenderLayered();
			}

			public int GetDesiredWidth(int minWidth, int maxWidth)
			{
				if (string.IsNullOrEmpty(this._textValue)) return minWidth;

				var textSize = MeasureLayeredSingleLineText(this._textValue, this._textFont);
				var width = textSize.Width + TaskbarDeskbandService.Scale(_horizontalTextPadding);
				return Math.Max(minWidth, Math.Min(maxWidth, width));
			}

			private void ApplyAppearance(DeskbandAppearance appearance)
			{
				var oldFont = this._textFont;
				this._textFont = appearance.TextFont;
				this._appearanceSignature = appearance.Signature;
				oldFont?.Dispose();
				this._textColor = appearance.TextColor;
				this._theme = appearance.Theme;
				this._modernTooltip?.SetTheme(appearance.Theme);
			}

			public bool RenderLayered()
			{
				if (!this.IsHandleCreated) return false;

				var width = Math.Max(1, this.Width);
				var height = Math.Max(1, this.Height);
				using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb))
				using (var graphics = Graphics.FromImage(bitmap))
				{
					graphics.Clear(Color.FromArgb(1, 0, 0, 0));
					graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
					DrawLayeredSingleLineText(
						graphics,
						this._textValue,
						this._textFont,
						this._textColor,
						new Rectangle(0, 0, width, height));

					return this.UpdateLayeredBitmap(bitmap, width, height);
				}
			}

			private bool UpdateLayeredBitmap(Bitmap bitmap, int width, int height)
			{
				var screenDc = NativeMethods.GetDC(IntPtr.Zero);
				var memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
				var bitmapHandle = IntPtr.Zero;
				var previousBitmap = IntPtr.Zero;

				try
				{
					var bitmapInfo = new NativeMethods.BitmapInfo
					{
						Header = new NativeMethods.BitmapInfoHeader
						{
							Size = (uint)Marshal.SizeOf(typeof(NativeMethods.BitmapInfoHeader)),
							Width = width,
							Height = -height,
							Planes = 1,
							BitCount = 32,
							Compression = NativeMethods.BiRgb,
							SizeImage = (uint)(width * height * 4),
						},
					};

					bitmapHandle = NativeMethods.CreateDIBSection(
						screenDc,
						ref bitmapInfo,
						NativeMethods.DibRgbColors,
						out var bitmapBits,
						IntPtr.Zero,
						0);
					if (bitmapHandle == IntPtr.Zero || bitmapBits == IntPtr.Zero) return false;

					CopyBitmapBits(bitmap, bitmapBits, width, height);
					previousBitmap = NativeMethods.SelectObject(memoryDc, bitmapHandle);
					var size = new NativeMethods.LayeredSize(width, height);
					var source = new NativeMethods.LayeredPoint(0, 0);
					var blend = new NativeMethods.BlendFunction
					{
						BlendOp = NativeMethods.AcSrcOver,
						BlendFlags = 0,
						SourceConstantAlpha = 255,
						AlphaFormat = NativeMethods.AcSrcAlpha,
					};

					var updated = this.UpdateLayeredBitmapIndirect(memoryDc, ref size, ref source, ref blend);
					if (!updated)
					{
						this.LogLastWin32UpdateError();
					}

					return updated;
				}
				finally
				{
					if (previousBitmap != IntPtr.Zero) NativeMethods.SelectObject(memoryDc, previousBitmap);
					if (bitmapHandle != IntPtr.Zero) NativeMethods.DeleteObject(bitmapHandle);
					if (memoryDc != IntPtr.Zero) NativeMethods.DeleteDC(memoryDc);
					if (screenDc != IntPtr.Zero) NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
				}
			}

			private bool UpdateLayeredBitmapIndirect(
				IntPtr sourceDc,
				ref NativeMethods.LayeredSize size,
				ref NativeMethods.LayeredPoint source,
				ref NativeMethods.BlendFunction blend)
			{
				var sizePtr = IntPtr.Zero;
				var sourcePtr = IntPtr.Zero;
				var blendPtr = IntPtr.Zero;

				try
				{
					sizePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeMethods.LayeredSize)));
					sourcePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeMethods.LayeredPoint)));
					blendPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeMethods.BlendFunction)));
					Marshal.StructureToPtr(size, sizePtr, false);
					Marshal.StructureToPtr(source, sourcePtr, false);
					Marshal.StructureToPtr(blend, blendPtr, false);

					var updateInfo = new NativeMethods.UpdateLayeredWindowInfo
					{
						Size = Marshal.SizeOf(typeof(NativeMethods.UpdateLayeredWindowInfo)),
						SourceDc = sourceDc,
						SizePointer = sizePtr,
						SourcePositionPointer = sourcePtr,
						BlendPointer = blendPtr,
						Flags = NativeMethods.UlwAlpha,
					};

					return NativeMethods.UpdateLayeredWindowIndirect(this.Handle, ref updateInfo);
				}
				finally
				{
					if (blendPtr != IntPtr.Zero) Marshal.FreeHGlobal(blendPtr);
					if (sourcePtr != IntPtr.Zero) Marshal.FreeHGlobal(sourcePtr);
					if (sizePtr != IntPtr.Zero) Marshal.FreeHGlobal(sizePtr);
				}
			}

			private void LogLastWin32UpdateError()
			{
				var error = Marshal.GetLastWin32Error();
				if (error == 0 || error == this._lastLoggedUpdateError) return;

				this._lastLoggedUpdateError = error;
				LoggingService.Instance.Register(new Win32Exception(error, "Taskbar deskband layered update failed."));
			}

			private static void ExecuteDoubleClickAction()
				=> ExecuteDeskbandAction(Settings.General.TaskbarDeskbandDoubleClickAction.Value, allowReturnToDesktop1: false);

			private static void ExecuteMiddleClickAction()
				=> ExecuteDeskbandAction(Settings.General.TaskbarDeskbandMiddleClickAction.Value, allowReturnToDesktop1: true);

			private static void ExecuteDeskbandAction(uint action, bool allowReturnToDesktop1)
			{
				if (action == GeneralSettings.TaskbarDeskbandDoubleClickActionDisabledValue)
				{
					return;
				}

				if (allowReturnToDesktop1
					&& action == GeneralSettings.TaskbarDeskbandMiddleClickActionReturnToDesktop1Value)
				{
					VirtualDesktopService.GetByIndex(0)?.Switch();
					return;
				}

				if (action == GeneralSettings.TaskbarDeskbandDoubleClickActionSettingsValue)
				{
					OpenSettingsWindow();
					return;
				}

				VirtualDesktopService.ShowTaskView();
			}

			private static void OpenSettingsWindow()
			{
				var dispatcher = System.Windows.Application.Current?.Dispatcher;
				if (dispatcher == null) return;

				dispatcher.BeginInvoke(new Action(() =>
				{
					try
					{
						if (!SylphyHorn.Application.Args.CanSettings) return;

						if (SettingsWindow.Instance != null)
						{
							SettingsWindow.Instance.Activate();
							return;
						}

						var application = System.Windows.Application.Current as SylphyHorn.Application;
						SettingsWindow.Instance = new SettingsWindow
						{
							DataContext = new SettingsWindowViewModel(application?.HookService),
						};

						try
						{
							SettingsWindow.Instance.ShowDialog();
						}
						finally
						{
							SettingsWindow.Instance = null;
						}
					}
					catch (Exception ex)
					{
						LoggingService.Instance.Register(ex);
					}
				}));
			}

			private static void CopyBitmapBits(Bitmap bitmap, IntPtr targetBits, int width, int height)
			{
				var bounds = new Rectangle(0, 0, width, height);
				var data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);

				try
				{
					var targetStride = width * 4;
					var sourceStride = Math.Abs(data.Stride);
					var sourceBuffer = new byte[sourceStride * height];
					var targetBuffer = new byte[targetStride * height];

					Marshal.Copy(data.Scan0, sourceBuffer, 0, sourceBuffer.Length);
					for (var y = 0; y < height; y++)
					{
						var sourceY = data.Stride < 0 ? height - 1 - y : y;
						Buffer.BlockCopy(sourceBuffer, sourceY * sourceStride, targetBuffer, y * targetStride, targetStride);
					}

					Marshal.Copy(targetBuffer, 0, targetBits, targetBuffer.Length);
				}
				finally
				{
					bitmap.UnlockBits(data);
				}
			}

			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					this._nativeToolTip.Dispose();
					this._modernTooltipShowTimer.Dispose();
					this._modernTooltipHideTimer.Dispose();
					this._modernTooltip?.Dispose();
					this._textFont?.Dispose();
				}

				base.Dispose(disposing);
			}
		}

		private class ModernTooltipForm : Form
		{
			private const int _cornerRadius = 4;
			private const int _paddingX = 10;
			private const int _paddingY = 7;
			private readonly Font _font = new Font("Segoe UI", 9.0f, FontStyle.Regular, GraphicsUnit.Point);
			private Color _backgroundColor;
			private Color _foregroundColor;
			private string _text = string.Empty;

			protected override bool ShowWithoutActivation => true;

			protected override CreateParams CreateParams
			{
				get
				{
					var cp = base.CreateParams;
					cp.ExStyle |= _wsExToolWindow;
					cp.ExStyle |= _wsExLayered;
					cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
					return cp;
				}
			}

			public ModernTooltipForm()
			{
				this.FormBorderStyle = FormBorderStyle.None;
				this.ShowInTaskbar = false;
				this.StartPosition = FormStartPosition.Manual;
				this.SetTheme(WindowsTheme.SystemTheme.Current);
			}

			public void SetTheme(Theme theme)
			{
				this._backgroundColor = theme == Theme.Light
					? Color.FromArgb(242, 242, 242)
					: Color.FromArgb(43, 43, 43);
				this._foregroundColor = theme == Theme.Light
					? Color.FromArgb(32, 32, 32)
					: Color.White;

				if (this.Visible) this.RenderLayered();
			}

			public void SetText(string text)
			{
				this._text = text ?? string.Empty;
				var textSize = MeasureLayeredTextBlock(this._text, this._font, 420);
				this.Size = new Size(
					Math.Max(1, textSize.Width + TaskbarDeskbandService.Scale(_paddingX * 2)),
					Math.Max(1, textSize.Height + TaskbarDeskbandService.Scale(_paddingY * 2)));
				if (this.Visible) this.RenderLayered();
			}

			public void ShowNoActivate()
			{
				this.RenderLayered();
				NativeMethods.ShowWindow(this.Handle, ShowWindowCommand.ShowNoActivate);
				this.RenderLayered();
			}

			protected override void OnSizeChanged(EventArgs e)
			{
				base.OnSizeChanged(e);
				if (this.Visible) this.RenderLayered();
			}

			protected override void OnLocationChanged(EventArgs e)
			{
				base.OnLocationChanged(e);
				if (this.Visible) this.RenderLayered();
			}

			public bool RenderLayered()
			{
				if (!this.IsHandleCreated) return false;

				var width = Math.Max(1, this.Width);
				var height = Math.Max(1, this.Height);
				using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb))
				using (var graphics = Graphics.FromImage(bitmap))
				{
					graphics.Clear(Color.Transparent);
					graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
					graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

					using (var brush = new SolidBrush(this._backgroundColor))
					using (var path = CreateRoundedRectanglePath(new Rectangle(0, 0, width, height), TaskbarDeskbandService.Scale(_cornerRadius)))
					{
						graphics.FillPath(brush, path);
					}

					DrawLayeredTextBlock(
						graphics,
						this._text,
						this._font,
						this._foregroundColor,
						new Rectangle(
							TaskbarDeskbandService.Scale(_paddingX),
							TaskbarDeskbandService.Scale(_paddingY),
							width - TaskbarDeskbandService.Scale(_paddingX * 2),
							height - TaskbarDeskbandService.Scale(_paddingY * 2)));

					return this.UpdateLayeredBitmap(bitmap, width, height);
				}
			}

			private bool UpdateLayeredBitmap(Bitmap bitmap, int width, int height)
			{
				var screenDc = NativeMethods.GetDC(IntPtr.Zero);
				var memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
				var bitmapHandle = IntPtr.Zero;
				var previousBitmap = IntPtr.Zero;

				try
				{
					var bitmapInfo = new NativeMethods.BitmapInfo
					{
						Header = new NativeMethods.BitmapInfoHeader
						{
							Size = (uint)Marshal.SizeOf(typeof(NativeMethods.BitmapInfoHeader)),
							Width = width,
							Height = -height,
							Planes = 1,
							BitCount = 32,
							Compression = NativeMethods.BiRgb,
							SizeImage = (uint)(width * height * 4),
						},
					};

					bitmapHandle = NativeMethods.CreateDIBSection(
						screenDc,
						ref bitmapInfo,
						NativeMethods.DibRgbColors,
						out var bitmapBits,
						IntPtr.Zero,
						0);
					if (bitmapHandle == IntPtr.Zero || bitmapBits == IntPtr.Zero) return false;

					CopyBitmapBits(bitmap, bitmapBits, width, height);
					previousBitmap = NativeMethods.SelectObject(memoryDc, bitmapHandle);

					var destination = new NativeMethods.LayeredPoint(this.Left, this.Top);
					var size = new NativeMethods.LayeredSize(width, height);
					var source = new NativeMethods.LayeredPoint(0, 0);
					var blend = new NativeMethods.BlendFunction
					{
						BlendOp = NativeMethods.AcSrcOver,
						BlendFlags = 0,
						SourceConstantAlpha = 255,
						AlphaFormat = NativeMethods.AcSrcAlpha,
					};

					return NativeMethods.UpdateLayeredWindow(
						this.Handle,
						screenDc,
						ref destination,
						ref size,
						memoryDc,
						ref source,
						0,
						ref blend,
						NativeMethods.UlwAlpha);
				}
				finally
				{
					if (previousBitmap != IntPtr.Zero) NativeMethods.SelectObject(memoryDc, previousBitmap);
					if (bitmapHandle != IntPtr.Zero) NativeMethods.DeleteObject(bitmapHandle);
					if (memoryDc != IntPtr.Zero) NativeMethods.DeleteDC(memoryDc);
					if (screenDc != IntPtr.Zero) NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
				}
			}

			private static void CopyBitmapBits(Bitmap bitmap, IntPtr targetBits, int width, int height)
			{
				var bounds = new Rectangle(0, 0, width, height);
				var data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);

				try
				{
					var targetStride = width * 4;
					var sourceStride = Math.Abs(data.Stride);
					var sourceBuffer = new byte[sourceStride * height];
					var targetBuffer = new byte[targetStride * height];

					Marshal.Copy(data.Scan0, sourceBuffer, 0, sourceBuffer.Length);
					for (var y = 0; y < height; y++)
					{
						var sourceY = data.Stride < 0 ? height - 1 - y : y;
						Buffer.BlockCopy(sourceBuffer, sourceY * sourceStride, targetBuffer, y * targetStride, targetStride);
					}

					Marshal.Copy(targetBuffer, 0, targetBits, targetBuffer.Length);
				}
				finally
				{
					bitmap.UnlockBits(data);
				}
			}

			private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectanglePath(Rectangle rectangle, int radius)
			{
				var diameter = Math.Max(1, radius * 2);
				var path = new System.Drawing.Drawing2D.GraphicsPath();
				path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
				path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
				path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
				path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
				path.CloseFigure();
				return path;
			}

			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					this._font.Dispose();
				}

				base.Dispose(disposing);
			}
		}
	}
}

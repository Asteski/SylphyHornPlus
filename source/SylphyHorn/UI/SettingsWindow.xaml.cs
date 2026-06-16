using System;
using System.Windows.Interop;
using SylphyHorn.Interop;
using SylphyHorn.Properties;
using WindowsDesktop;

namespace SylphyHorn.UI
{
	partial class SettingsWindow
	{
		public static SettingsWindow Instance { get; set; }

		public SettingsWindow()
		{
			this.InitializeComponent();
		}

		public void SelectAboutSection()
		{
			this.SettingsTabControl.SelectedIndex = this.SettingsTabControl.Items.Count - 1;
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.ApplyWindows11CornerPreference();
		}

		protected override void OnContentRendered(EventArgs e)
		{
			base.OnContentRendered(e);
			this.Pin();
		}

		private void ApplyWindows11CornerPreference()
		{
			if (!ProductInfo.IsWindows11OrLater) return;

			var handle = new WindowInteropHelper(this).Handle;
			if (handle == IntPtr.Zero) return;

			try
			{
				var preference = (int)NativeMethods.DwmWindowCornerPreference.Round;
				NativeMethods.DwmSetWindowAttribute(
					handle,
					NativeMethods.DwmwaWindowCornerPreference,
					ref preference,
					sizeof(int));
			}
			catch
			{
				// DWM corner preference is cosmetic; settings must still open if Windows rejects it.
			}
		}
	}
}

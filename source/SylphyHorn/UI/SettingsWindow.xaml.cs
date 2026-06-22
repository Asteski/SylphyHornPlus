using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
			ApplyWindows11ControlStyles();
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

		private void NumberTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key != Key.Up && e.Key != Key.Down) return;
			if (!(sender is TextBox textBox)) return;

			var value = 0;
			if (!string.IsNullOrWhiteSpace(textBox.Text))
			{
				int.TryParse(textBox.Text, out value);
			}

			value += e.Key == Key.Up ? 1 : -1;
			textBox.Text = value.ToString();
			textBox.SelectAll();
			textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
			e.Handled = true;
		}

		private void NumberTextBoxLostFocus(object sender, RoutedEventArgs e)
		{
			if (!(sender is TextBox textBox)) return;

			if (!int.TryParse(textBox.Text, out var value))
			{
				value = 0;
			}

			textBox.Text = value.ToString();
			textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
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

		private static void ApplyWindows11ControlStyles()
		{
			if (!ProductInfo.IsWindows11OrLater) return;

			var dictionaries = Application.Current.Resources.MergedDictionaries;
			var source = new Uri("pack://application:,,,/SylphyHorn;component/Styles/Windows11SettingsControls.xaml", UriKind.Absolute);
			foreach (var dictionary in dictionaries)
			{
				if (dictionary.Source == source) return;
			}

			dictionaries.Add(new ResourceDictionary { Source = source });
		}
	}
}

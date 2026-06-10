using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MetroRadiance.Interop;
using MetroRadiance.Platform;
using SylphyHorn.Interop;

namespace SylphyHorn.UI
{
	public class DynamicInfoTrayIcon
	{
		private const string _defaultFontFamilyName = "Segoe UI Variable Text, Segoe UI";
		private const double _defaultHorizontalFontSize = 8.5;
		private const double _defaultVerticalFontSize = 7.5;
		private const double _verticalSpacing = -0.5;
		private const double _triggerFontSizeInEffectivePixels = 14.0;
		private const double _minFontSize = 4.0;
		private const double _defaultSimpleFontSize = 15;

		private static readonly SolidColorBrush _lightForegroundBrush = new SolidColorBrush(ImmersiveColor.GetColorByTypeName(ImmersiveColorNames.SystemTextLightTheme));
		private static readonly SolidColorBrush _darkForegroundBrush = new SolidColorBrush(ImmersiveColor.GetColorByTypeName(ImmersiveColorNames.SystemTextDarkTheme));

		private SolidColorBrush _foregroundBrush;
		private Typeface _defaultFont;
		private Typeface _simpleFont;
		private double _horizontalFontSize;
		private double _verticalFontSize;
		private double _simpleFontSize;
		private Dpi? _dpi;

		public DynamicInfoTrayIcon(Theme theme, bool colorPrevalence, Dpi? dpi = null)
		{
			this._foregroundBrush = GetThemeBrush(theme, colorPrevalence);
			this.UpdateFont();
			this._dpi = dpi;
		}

		public System.Drawing.Icon GetDesktopInfoIcon(int currentDesktop, int totalDesktopCount)
		{
			using (var iconBitmap = this.DrawInfo(currentDesktop, totalDesktopCount))
			{
				return iconBitmap.ToIcon();
			}
		}

		public void UpdateBrush(Theme theme, bool colorPrevalence)
		{
			this._foregroundBrush = GetThemeBrush(theme, colorPrevalence);
		}

		public void UpdateFont()
		{
			var fontFamily = new FontFamily(_defaultFontFamilyName);
			this._defaultFont = new Typeface(fontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
			this._simpleFont = new Typeface(fontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

			this._horizontalFontSize = _defaultHorizontalFontSize;
			this._verticalFontSize = this._horizontalFontSize * _defaultVerticalFontSize / _defaultHorizontalFontSize;
			this._simpleFontSize = this._horizontalFontSize * _defaultSimpleFontSize / _defaultHorizontalFontSize;
		}

		// consolidate two methods below?
		private System.Drawing.Bitmap DrawInfo(int currentDesktop, int totalDesktopCount)
		{
			var iconSize = IconHelper.GetIconSize();
			var dpi = this._dpi ?? GetDpi();
			var scale = dpi.X / 96.0;

			var drawingVisual = new DrawingVisual();
			TextOptions.SetTextRenderingMode(drawingVisual, TextRenderingMode.Grayscale);
			TextOptions.SetTextFormattingMode(drawingVisual, TextFormattingMode.Display);
			using (var context = drawingVisual.RenderOpen())
			{
				var currentOrientation = GetOrientation(totalDesktopCount);
				if (totalDesktopCount <= 0)
				{
					this.DrawSimpleInfo(context, iconSize, scale, currentDesktop);
				}
				else if (currentOrientation == Orientation.Horizontal)
				{
					this.DrawHorizontalInfo(context, iconSize, scale, currentDesktop, totalDesktopCount);
				}
				else
				{
					this.DrawVerticalInfo(context, iconSize, scale, currentDesktop, totalDesktopCount);
				}
			}

			return drawingVisual.ToBitmap(iconSize, dpi);
		}

		private void DrawHorizontalInfo(DrawingContext context, System.Drawing.Size size, double scale, int currentDesktop, int totalDesktopCount)
		{
			var stringToDraw = $"{currentDesktop}/{totalDesktopCount}";
			var formattedText = this.GetFormattedTextFromText(stringToDraw, this._horizontalFontSize, size, scale);
			formattedText.LineHeight = Math.Min(this._horizontalFontSize, size.Height);

			var offsetY = Math.Floor(0.5 * (size.Height - formattedText.Extent));
			context.DrawText(formattedText, new Point(0, offsetY));
		}

		private void DrawVerticalInfo(DrawingContext context, System.Drawing.Size size, double scale, int currentDesktop, int totalDesktopCount, double? currentFontSize = null)
		{
			var fontSize = currentFontSize ?? this._verticalFontSize;
			var scaleable = (fontSize - 1) >= _minFontSize;
			var lineHeight = Math.Min(fontSize, 0.5 * size.Height);

			var firstString = currentDesktop.ToString();
			var firstFormattedText = this.GetFormattedTextFromText(firstString, fontSize, size, scale);
			firstFormattedText.LineHeight = lineHeight;
			if (scaleable && firstFormattedText.MinWidth > size.Width)
			{
				DrawVerticalInfo(context, size, scale, currentDesktop, totalDesktopCount, fontSize - 1);
				return;
			}

			var secondString = totalDesktopCount.ToString();
			var secondFormattedText = this.GetFormattedTextFromText(secondString, fontSize, size, scale);
			secondFormattedText.LineHeight = lineHeight;
			if (scaleable && secondFormattedText.MinWidth > size.Width)
			{
				DrawVerticalInfo(context, size, scale, currentDesktop, totalDesktopCount, fontSize - 1);
				return;
			}

			var offsetY1 = Math.Floor(0.5 * size.Height - _verticalSpacing - firstFormattedText.Extent);
			context.DrawText(firstFormattedText, new Point(0, offsetY1));

			var offsetY2 = Math.Ceiling(0.5 * size.Height + _verticalSpacing);
			context.DrawText(secondFormattedText, new Point(0, offsetY2));
		}

		private void DrawSimpleInfo(DrawingContext context, System.Drawing.Size size, double scale, int currentDesktop)
		{
			var stringToDraw = $"{currentDesktop}";
			var digit = (int)Math.Floor(Math.Log10(currentDesktop));
			var fontSize = this._simpleFontSize * Math.Pow(0.84, digit) * Math.Pow(0.84, digit > 0 ? digit - 1 : 0);
			var formattedText = this.GetFormattedTextFromText(stringToDraw, fontSize, size, scale, _simpleFont);
			formattedText.LineHeight = Math.Min(fontSize, size.Height);

			var offsetY = Math.Floor(0.5 * (size.Height - formattedText.Extent));
			context.DrawText(formattedText, new Point(0, offsetY));
		}

		private FormattedText GetFormattedTextFromText(string text, double fontSize, System.Drawing.Size size, double scale = 1, Typeface font = null)
		{
			var formattedText = new FormattedText(
				text,
				CultureInfo.CurrentUICulture,
				FlowDirection.LeftToRight,
				font ?? _defaultFont,
				fontSize,
				this._foregroundBrush,
				null,
				fontSize * scale >= _triggerFontSizeInEffectivePixels ? TextFormattingMode.Ideal : TextFormattingMode.Display,
				scale);
			formattedText.MaxLineCount = 1;
			formattedText.MaxTextWidth = size.Width;
			formattedText.TextAlignment = TextAlignment.Center;
			formattedText.Trimming = TextTrimming.None;
			return formattedText;
		}

		private static Orientation GetOrientation(int totalDesktopCount)
		{
			return totalDesktopCount >= 10 ? Orientation.Vertical : Orientation.Horizontal;
		}

		private static SolidColorBrush GetThemeBrush(Theme theme, bool colorPrevalence)
		{
			return colorPrevalence
				? _darkForegroundBrush
				: theme == Theme.Light
					? _lightForegroundBrush
					: _darkForegroundBrush;
		}

		private static Dpi GetDpi()
		{
			var dpi = IconHelper.GetSystemDpi();
			var maxDpi = Math.Max(dpi.X, dpi.Y);
			var newDpi = new Dpi(maxDpi, maxDpi);
			return newDpi;
		}
	}
}

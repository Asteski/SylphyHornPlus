using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MetroTrilithon.Serialization;

namespace SylphyHorn.Serialization
{
	public class GeneralSettings : SettingsHost
	{
		private readonly ISerializationProvider _provider;

		public GeneralSettings(ISerializationProvider provider)
		{
			this._provider = provider;
		}

		public SerializableProperty<bool> LoopDesktop => this.Cache(key => new SerializableProperty<bool>(key, this._provider));

		public SerializableProperty<bool> NotificationWhenSwitchedDesktop => this.Cache(key => new SerializableProperty<bool>(key, this._provider, NotificationWhenSwitchedDesktopDefaultValue));

		public SerializableProperty<bool> AlwaysShowDesktopNotification => this.Cache(key => new SerializableProperty<bool>(key, this._provider, AlwaysShowDesktopNotificationDefaultValue));

		public SerializableProperty<bool> SimpleNotification => this.Cache(key => new SerializableProperty<bool>(key, this._provider));

		public SerializableProperty<int> NotificationDuration => this.Cache(key => new SerializableProperty<int>(key, this._provider, NotificationDurationDefaultValue));

		public SerializableProperty<bool> ChangeBackgroundEachDesktop => this.Cache(key => new SerializableProperty<bool>(key, this._provider));

		public SerializableProperty<string> DesktopBackgroundFolderPath => this.Cache(key => new SerializableProperty<string>(key, this._provider));

		public SerializableProperty<bool> OverrideWindowsDefaultKeyCombination => this.Cache(key => new SerializableProperty<bool>(key, this._provider, OverrideWindowsDefaultKeyCombinationDefaultValue));

		public SerializableProperty<bool> SuspendKeyDetection => this.Cache(key => new SerializableProperty<bool>(key, this._provider));

		public SerializableProperty<bool> FirstTime => this.Cache(key => new SerializableProperty<bool>(key, this._provider, true));

		public SerializableProperty<string> Culture => this.Cache(key => new SerializableProperty<string>(key, this._provider));

		public SerializableProperty<uint> Placement => this.Cache(key => new SerializableProperty<uint>(key, this._provider, PlacementDefaultValue));

		public SerializableProperty<uint> Display => this.Cache(key => new SerializableProperty<uint>(key, this._provider, 0));

		public SerializableProperty<uint> NotificationWindowStyle => this.Cache(key => new SerializableProperty<uint>(key, this._provider, NotificationWindowStyleDefaultValue));

		public SerializableProperty<uint> NotificationCornerStyle => this.Cache(key => new SerializableProperty<uint>(key, this._provider, NotificationCornerStyleDefaultValue));

		public SerializableProperty<uint> NotificationHeaderAlignment => this.Cache(key => new SerializableProperty<uint>(key, this._provider, NotificationHeaderAlignmentDefaultValue));

		public SerializableProperty<uint> NotificationBodyAlignment => this.Cache(key => new SerializableProperty<uint>(key, this._provider, NotificationBodyAlignmentDefaultValue));

		public SerializableProperty<string> NotificationFontFamily => this.Cache(key => new SerializableProperty<string>(key, this._provider));

		public SerializableProperty<int> NotificationHeaderFontSize => this.Cache(key => new SerializableProperty<int>(key, this._provider, NotificationHeaderFontSizeDefaultValue));

		public SerializableProperty<int> NotificationBodyFontSize => this.Cache(key => new SerializableProperty<int>(key, this._provider, NotificationBodyFontSizeDefaultValue));

		public SerializableProperty<int> NotificationLineSpacing => this.Cache(key => new SerializableProperty<int>(key, this._provider, NotificationLineSpacingDefaultValue));

		public SerializableProperty<int> NotificationMinWidth => this.Cache(key => new SerializableProperty<int>(key, this._provider, NotificationMinWidthDefaultValue));

		public SerializableProperty<int> SimpleNotificationMinWidth => this.Cache(key => new SerializableProperty<int>(key, this._provider, SimpleNotificationMinWidthDefaultValue));

		public SerializableProperty<int> PinWindowMinWidth => this.Cache(key => new SerializableProperty<int>(key, this._provider, PinWindowMinWidthDefaultValue));

		public SerializableProperty<int> NotificationMinHeight => this.Cache(key => new SerializableProperty<int>(key, this._provider, NotificationMinHeightDefaultValue));

		public SerializableProperty<int> NotificationOffsetX => this.Cache(key => new SerializableProperty<int>(key, this._provider, NotificationOffsetXDefaultValue));

		public SerializableProperty<int> NotificationOffsetY => this.Cache(key => new SerializableProperty<int>(key, this._provider, NotificationOffsetYDefaultValue));

		public SerializableProperty<int> PinWindowOffsetX => this.Cache(key => new SerializableProperty<int>(key, this._provider, PinWindowOffsetXDefaultValue));

		public SerializableProperty<int> PinWindowOffsetY => this.Cache(key => new SerializableProperty<int>(key, this._provider, PinWindowOffsetYDefaultValue));

		public SerializableProperty<bool> TrayShowDesktop => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TrayShowDesktopDefaultValue));

		public SerializableProperty<bool> TrayShowOnlyCurrentNumber => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TrayShowOnlyCurrentNumberDefaultValue));

		public SerializableProperty<bool> TraySwitchDesktopWithMouseWheel => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TraySwitchDesktopWithMouseWheelDefaultValue));

		public SerializableProperty<bool> TraySwitchDesktopWithMouseWheelReverse => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TraySwitchDesktopWithMouseWheelReverseDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandEnabled => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandEnabledDefaultValue));

		public SerializableProperty<uint> TaskbarDeskbandMode => this.Cache(key => new SerializableProperty<uint>(key, this._provider, TaskbarDeskbandModeDefaultValue));

		public SerializableProperty<uint> TaskbarDeskbandLastEnabledMode => this.Cache(key => new SerializableProperty<uint>(key, this._provider, TaskbarDeskbandLastEnabledModeDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandPlaceOnLeft => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandPlaceOnLeftDefaultValue));

		public SerializableProperty<uint> TaskbarDeskbandPosition => this.Cache(key => new SerializableProperty<uint>(key, this._provider, TaskbarDeskbandPositionDefaultValue));

		public SerializableProperty<int> TaskbarDeskbandPositionOffset => this.Cache(key => new SerializableProperty<int>(key, this._provider, TaskbarDeskbandPositionOffsetDefaultValue));

		public SerializableProperty<int> TaskbarDeskbandVerticalPositionOffset => this.Cache(key => new SerializableProperty<int>(key, this._provider, TaskbarDeskbandVerticalPositionOffsetDefaultValue));

		public SerializableProperty<uint> TaskbarDeskbandDisplayMode => this.Cache(key => new SerializableProperty<uint>(key, this._provider, TaskbarDeskbandDisplayModeDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandCustomNumberStyleEnabled => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandCustomNumberStyleEnabledDefaultValue));

		public SerializableProperty<uint> TaskbarDeskbandNumberWrapper => this.Cache(key => new SerializableProperty<uint>(key, this._provider, TaskbarDeskbandNumberWrapperDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandNumberWrapperSpaces => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandNumberWrapperSpacesDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandRomanNumber => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandRomanNumberDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandShowTotalDesktopCount => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandShowTotalDesktopCountDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandNumberBeforeName => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandNumberBeforeNameDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandCustomAppearanceEnabled => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandCustomAppearanceEnabledDefaultValue));

		public SerializableProperty<string> TaskbarDeskbandFontFamily => this.Cache(key => new SerializableProperty<string>(key, this._provider, TaskbarDeskbandFontFamilyDefaultValue));

		public SerializableProperty<int> TaskbarDeskbandFontSize => this.Cache(key => new SerializableProperty<int>(key, this._provider, TaskbarDeskbandFontSizeDefaultValue));

		public SerializableProperty<string> TaskbarDeskbandFontColor => this.Cache(key => new SerializableProperty<string>(key, this._provider, TaskbarDeskbandFontColorDefaultValue));

		public SerializableProperty<uint> TaskbarDeskbandFontWeight => this.Cache(key => new SerializableProperty<uint>(key, this._provider, TaskbarDeskbandFontWeightDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandFontBold => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandFontBoldDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandFontItalic => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandFontItalicDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandFontUnderline => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandFontUnderlineDefaultValue));

		public SerializableProperty<uint> TaskbarDeskbandFontRenderingMode => this.Cache(key => new SerializableProperty<uint>(key, this._provider, TaskbarDeskbandFontRenderingModeDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandTooltipEnabled => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandTooltipEnabledDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandTooltipNumberOnly => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandTooltipNumberOnlyDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandTooltipListWindows => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandTooltipListWindowsDefaultValue));

		public SerializableProperty<uint> TaskbarDeskbandTooltipLook => this.Cache(key => new SerializableProperty<uint>(key, this._provider, TaskbarDeskbandTooltipLookDefaultValue));

		public SerializableProperty<uint> TaskbarDeskbandTooltipWindowStyle => this.Cache(key => new SerializableProperty<uint>(key, this._provider, TaskbarDeskbandTooltipWindowStyleDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandSwitchDesktopWithMouseWheel => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandSwitchDesktopWithMouseWheelDefaultValue));

		public SerializableProperty<bool> TaskbarDeskbandSwitchDesktopWithMouseWheelReverse => this.Cache(key => new SerializableProperty<bool>(key, this._provider, TaskbarDeskbandSwitchDesktopWithMouseWheelReverseDefaultValue));

		public SerializableProperty<uint> TaskbarDeskbandDoubleClickAction => this.Cache(key => new SerializableProperty<uint>(key, this._provider, TaskbarDeskbandDoubleClickActionDefaultValue));

		public SerializableProperty<uint> TaskbarDeskbandMiddleClickAction => this.Cache(key => new SerializableProperty<uint>(key, this._provider, TaskbarDeskbandMiddleClickActionDefaultValue));

		public SerializableProperty<bool> UseDesktopName => this.Cache(key => new SerializableProperty<bool>(key, this._provider));

		public SerializableProperty<bool> OverrideDesktopsOnStartup => this.Cache(key => new SerializableProperty<bool>(key, this._provider, OverrideDesktopsOnStartupDefaultValue));

		public DesktopNamePropertyList DesktopNames => this.Cache(key => new DesktopNamePropertyList(key, this._provider));

		public WallpaperPathPropertyList DesktopBackgroundImagePaths => this.Cache(key => new WallpaperPathPropertyList(key, this._provider));

		public WallpaperPositionsPropertyList DesktopBackgroundPositions => this.Cache(key => new WallpaperPositionsPropertyList(key, this._provider));

		public DesktopProcessNamePropertyList DesktopProcessNames => this.Cache(key => new DesktopProcessNamePropertyList(key, this._provider));

		public DesktopProcessNameClosePropertyList DesktopProcessNamesCloseWhenEmpty => this.Cache(key => new DesktopProcessNameClosePropertyList(key, this._provider));

		public SerializableProperty<bool> DesktopProcessNamesCreateMissingDesktop => this.Cache(key => new SerializableProperty<bool>(key, this._provider, DesktopProcessNamesCreateMissingDesktopDefaultValue));

		#region default values

		public static bool NotificationWhenSwitchedDesktopDefaultValue { get; } = true;

		public static bool AlwaysShowDesktopNotificationDefaultValue { get; } = false;

		public static int NotificationDurationDefaultValue { get; } = 2500 /* milliseconds */;

		public static bool OverrideWindowsDefaultKeyCombinationDefaultValue { get; } = false;

		public static uint PlacementDefaultValue { get; } = 5 /* Center */;

		public static uint NotificationWindowStyleDefaultValue { get; } = 4 /* BlurWindowThemeMode.System */;

		public static uint NotificationCornerStyleDefaultValue { get; } = IsWindows11OrLater ? 2u : 1u /* BlurWindowCornerMode.Rounded / NotRounded */;

		public static uint NotificationHeaderAlignmentDefaultValue { get; } = 0 /* Left */;

		public static uint NotificationBodyAlignmentDefaultValue { get; } = 0 /* Left */;

		public static int NotificationHeaderFontSizeDefaultValue { get; } = 18 /* px */;

		public static int NotificationBodyFontSizeDefaultValue { get; } = 32 /* px */;

		public static int NotificationLineSpacingDefaultValue { get; } = -4;

		public static int NotificationMinWidthDefaultValue { get; } = 500 /* px */;

		public static int SimpleNotificationMinWidthDefaultValue { get; } = 210 /* px */;

		public static int PinWindowMinWidthDefaultValue { get; } = 400 /* px */;

		public static int NotificationMinHeightDefaultValue { get; } = 100 /* px */;

		public static int NotificationOffsetXDefaultValue { get; } = 0 /* px */;

		public static int NotificationOffsetYDefaultValue { get; } = 0 /* px */;

		public static int NotificationOffsetXWithRoundedDefaultValue { get; } = 12 /* px */;

		public static int NotificationOffsetYWithRoundedDefaultValue { get; } = 12 /* px */;

		public static int PinWindowOffsetXDefaultValue { get; } = 0 /* px */;

		public static int PinWindowOffsetYDefaultValue { get; } = 0 /* px */;

		public static string NotificationFontFamilyDefaultValue { get; } = "Segoe UI Light, Yu Gothic UI Light, Meiryo UI";

		public static bool TrayShowDesktopDefaultValue { get; } = false;

		public static bool TrayShowOnlyCurrentNumberDefaultValue { get; } = false;

		public static bool TraySwitchDesktopWithMouseWheelDefaultValue { get; } = false;

		public static bool TraySwitchDesktopWithMouseWheelReverseDefaultValue { get; } = false;

		public static bool TaskbarDeskbandEnabledDefaultValue { get; } = false;

		public static uint TaskbarDeskbandModeDisabledValue { get; } = 0;

		public static uint TaskbarDeskbandModeModernTaskbarValue { get; } = 1;

		public static uint TaskbarDeskbandModeLegacyTaskbarValue { get; } = 2;

		public static uint TaskbarDeskbandModeDefaultValue { get; } = TaskbarDeskbandModeDisabledValue;

		public static uint TaskbarDeskbandLastEnabledModeDefaultValue { get; } = TaskbarDeskbandModeDisabledValue;

		public static bool TaskbarDeskbandPlaceOnLeftDefaultValue { get; } = false;

		public static uint TaskbarDeskbandPositionRightValue { get; } = 0;

		public static uint TaskbarDeskbandPositionLeftValue { get; } = 1;

		public static uint TaskbarDeskbandPositionDefaultValue { get; } = TaskbarDeskbandPositionRightValue;

		public static int TaskbarDeskbandPositionOffsetDefaultValue { get; } = 0;

		public static int TaskbarDeskbandVerticalPositionOffsetDefaultValue { get; } = 0;

		public static uint TaskbarDeskbandDisplayModeNumberOnlyValue { get; } = 0;

		public static uint TaskbarDeskbandDisplayModeDesktopNumberValue { get; } = 1;

		public static uint TaskbarDeskbandDisplayModeNameOnlyValue { get; } = 2;

		public static uint TaskbarDeskbandDisplayModeNameWithNumberValue { get; } = 3;

		public static uint TaskbarDeskbandDisplayModeRomanNumberValue { get; } = 4;

		public static uint TaskbarDeskbandDisplayModeDefaultValue { get; } = TaskbarDeskbandDisplayModeNumberOnlyValue;

		public static bool TaskbarDeskbandCustomNumberStyleEnabledDefaultValue { get; } = false;

		public static uint TaskbarDeskbandNumberWrapperSquareValue { get; } = 0;

		public static uint TaskbarDeskbandNumberWrapperRoundValue { get; } = 1;

		public static uint TaskbarDeskbandNumberWrapperCurlyValue { get; } = 2;

		public static uint TaskbarDeskbandNumberWrapperAngleValue { get; } = 3;

		public static uint TaskbarDeskbandNumberWrapperSingleQuoteValue { get; } = 4;

		public static uint TaskbarDeskbandNumberWrapperDoubleQuoteValue { get; } = 5;

		public static uint TaskbarDeskbandNumberWrapperPipeValue { get; } = 6;

		public static uint TaskbarDeskbandNumberWrapperSlashValue { get; } = 7;

		public static uint TaskbarDeskbandNumberWrapperNoneValue { get; } = 8;

		public static uint TaskbarDeskbandNumberWrapperDefaultValue { get; } = TaskbarDeskbandNumberWrapperNoneValue;

		public static bool TaskbarDeskbandNumberWrapperSpacesDefaultValue { get; } = false;

		public static bool TaskbarDeskbandRomanNumberDefaultValue { get; } = false;

		public static bool TaskbarDeskbandShowTotalDesktopCountDefaultValue { get; } = false;

		public static bool TaskbarDeskbandNumberBeforeNameDefaultValue { get; } = false;

		public static bool TaskbarDeskbandCustomAppearanceEnabledDefaultValue { get; } = false;

		public static string TaskbarDeskbandFontFamilyDefaultValue { get; } = "Segoe UI Variable Text";

		public static int TaskbarDeskbandFontSizeDefaultValue { get; } = 9;

		public static string TaskbarDeskbandFontColorDefaultValue { get; } = "#FFFFFF";

		public static uint TaskbarDeskbandFontWeightRegularValue { get; } = 0;

		public static uint TaskbarDeskbandFontWeightLightValue { get; } = 1;

		public static uint TaskbarDeskbandFontWeightSemiLightValue { get; } = 2;

		public static uint TaskbarDeskbandFontWeightSemiBoldValue { get; } = 3;

		public static uint TaskbarDeskbandFontWeightBoldValue { get; } = 4;

		public static uint TaskbarDeskbandFontWeightDefaultValue { get; } = TaskbarDeskbandFontWeightRegularValue;

		public static bool TaskbarDeskbandFontBoldDefaultValue { get; } = false;

		public static bool TaskbarDeskbandFontItalicDefaultValue { get; } = false;

		public static bool TaskbarDeskbandFontUnderlineDefaultValue { get; } = false;

		public static uint TaskbarDeskbandFontRenderingModeGdiValue { get; } = 0;

		public static uint TaskbarDeskbandFontRenderingModeGdiPlusValue { get; } = 1;

		public static uint TaskbarDeskbandFontRenderingModeDirectWriteValue { get; } = 2;

		public static uint TaskbarDeskbandFontRenderingModeDefaultValue { get; } = TaskbarDeskbandFontRenderingModeGdiPlusValue;

		public static bool TaskbarDeskbandTooltipEnabledDefaultValue { get; } = true;

		public static bool TaskbarDeskbandTooltipNumberOnlyDefaultValue { get; } = false;

		public static bool TaskbarDeskbandTooltipListWindowsDefaultValue { get; } = false;

		public static uint TaskbarDeskbandTooltipLookWindows10Value { get; } = 0;

		public static uint TaskbarDeskbandTooltipLookWindows11Value { get; } = 1;

		public static uint TaskbarDeskbandTooltipLookDefaultValue { get; } = IsWindows11OrLater
			? TaskbarDeskbandTooltipLookWindows11Value
			: TaskbarDeskbandTooltipLookWindows10Value;

		public static uint TaskbarDeskbandTooltipWindowStyleTitleValue { get; } = 0;

		public static uint TaskbarDeskbandTooltipWindowStyleApplicationNameValue { get; } = 1;

		public static uint TaskbarDeskbandTooltipWindowStyleApplicationNameColonTitleValue { get; } = 2;

		public static uint TaskbarDeskbandTooltipWindowStyleApplicationNameDashTitleValue { get; } = 3;

		public static uint TaskbarDeskbandTooltipWindowStyleDefaultValue { get; } = TaskbarDeskbandTooltipWindowStyleTitleValue;

		public static bool TaskbarDeskbandSwitchDesktopWithMouseWheelDefaultValue { get; } = false;

		public static bool TaskbarDeskbandSwitchDesktopWithMouseWheelReverseDefaultValue { get; } = false;

		public static uint TaskbarDeskbandDoubleClickActionTaskViewValue { get; } = 0;

		public static uint TaskbarDeskbandDoubleClickActionSettingsValue { get; } = 1;

		public static uint TaskbarDeskbandDoubleClickActionDisabledValue { get; } = 2;

		public static uint TaskbarDeskbandDoubleClickActionDefaultValue { get; } = TaskbarDeskbandDoubleClickActionTaskViewValue;

		public static uint TaskbarDeskbandMiddleClickActionTaskViewValue { get; } = 0;

		public static uint TaskbarDeskbandMiddleClickActionSettingsValue { get; } = 1;

		public static uint TaskbarDeskbandMiddleClickActionDisabledValue { get; } = 2;

		public static uint TaskbarDeskbandMiddleClickActionReturnToDesktop1Value { get; } = 3;

		public static uint TaskbarDeskbandMiddleClickActionDefaultValue { get; } = TaskbarDeskbandMiddleClickActionDisabledValue;

		public static bool OverrideDesktopsOnStartupDefaultValue { get; } = false;

		public static bool DesktopProcessNamesCreateMissingDesktopDefaultValue { get; } = false;

		#endregion

		private static bool IsWindows11OrLater => Environment.OSVersion.Version.Build >= 22000;
	}
}

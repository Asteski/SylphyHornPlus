using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MetroRadiance.Interop.Win32;

namespace SylphyHorn.Interop
{
	public static class NativeMethods
	{
		public const int AcSrcOver = 0x00;
		public const int AcSrcAlpha = 0x01;
		public const int UlwAlpha = 0x00000002;
		public const uint BiRgb = 0;
		public const uint DibRgbColors = 0;
		public const int SpiGetNonClientMetrics = 0x0029;
		public const int WmGetFont = 0x0031;

		[DllImport("user32.dll")]
		public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool IsWindowVisible(IntPtr hWnd);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool IsWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern IntPtr GetAncestor(IntPtr hWnd, GetAncestorFlags gaFlags);

		[DllImport("user32.dll")]
		public static extern IntPtr GetShellWindow();

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern IntPtr SetWinEventHook(
			uint eventMin,
			uint eventMax,
			IntPtr hmodWinEventProc,
			WinEventDelegate lpfnWinEventProc,
			uint idProcess,
			uint idThread,
			uint dwFlags);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

		[DllImport("user32.dll")]
		public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsDelegate lpEnumFunc, IntPtr lParam);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool EnumWindows(EnumWindowsDelegate lpEnumFunc, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int GetWindowTextLength(IntPtr hWnd);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SystemParametersInfo(int uiAction, int uiParam, ref NonClientMetrics pvParam, int fWinIni);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool BringWindowToTop(IntPtr hWnd);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr GetParent(IntPtr hWnd);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommand nCmdShow);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool RedrawWindow(IntPtr hWnd, ref RECT lprcUpdate, IntPtr hrgnUpdate, RedrawWindowFlags flags);

		[DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
		public static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
		public static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

		[DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
		public static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
		public static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

		public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
		{
			return IntPtr.Size == 8
				? GetWindowLongPtr64(hWnd, nIndex)
				: new IntPtr(GetWindowLong32(hWnd, nIndex));
		}

		public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
		{
			return IntPtr.Size == 8
				? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
				: new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
		}
		
		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetCursorPos(out POINT lpPoint);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr MonitorFromPoint(POINT pt, MonitorDefaultTo dwFlags);

		[DllImport("User32.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
		public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, int crKey, byte bAlpha, LayeredWindowAttributes dwFlags);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool UpdateLayeredWindow(
			IntPtr hWnd,
			IntPtr hdcDst,
			ref LayeredPoint pptDst,
			ref LayeredSize psize,
			IntPtr hdcSrc,
			ref LayeredPoint pptSrc,
			int crKey,
			ref BlendFunction pblend,
			int dwFlags);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool UpdateLayeredWindowIndirect(
			IntPtr hWnd,
			ref UpdateLayeredWindowInfo updateInfo);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr GetDC(IntPtr hWnd);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

		[DllImport("gdi32.dll", SetLastError = true)]
		public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

		[DllImport("gdi32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool DeleteDC(IntPtr hdc);

		[DllImport("gdi32.dll", SetLastError = true)]
		public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

		[DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetObject", SetLastError = true)]
		public static extern int GetObject(IntPtr hgdiobj, int cbBuffer, ref LogFont lpvObject);

		[DllImport("gdi32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool DeleteObject(IntPtr ho);

		[DllImport("gdi32.dll", SetLastError = true)]
		public static extern IntPtr CreateDIBSection(
			IntPtr hdc,
			ref BitmapInfo pbmi,
			uint usage,
			out IntPtr ppvBits,
			IntPtr hSection,
			uint offset);

		[DllImport("dxva2.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref int pdwNumberOfPhysicalMonitors);

		[DllImport("dxva2.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

		[DllImport("dxva2.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, PHYSICAL_MONITOR[] pPhysicalMonitorArray);

		public static class GlobalHook
		{
			[DllImport("user32.dll")]
			public static extern IntPtr SetWindowsHookEx(int idHook, HookDelegate lpfn, IntPtr hMod, uint dwThreadId);

			[DllImport("user32.dll")]
			public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, uint msg, ref MSLLHOOKSTRUCT msllhookstruct);

			[DllImport("user32.dll")]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool UnhookWindowsHookEx(IntPtr hhk);

			public delegate IntPtr HookDelegate(int nCode, uint msg, ref MSLLHOOKSTRUCT msllhookstruct);
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct LayeredPoint
		{
			public int X;
			public int Y;

			public LayeredPoint(int x, int y)
			{
				this.X = x;
				this.Y = y;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct LayeredSize
		{
			public int Cx;
			public int Cy;

			public LayeredSize(int cx, int cy)
			{
				this.Cx = cx;
				this.Cy = cy;
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct BlendFunction
		{
			public byte BlendOp;
			public byte BlendFlags;
			public byte SourceConstantAlpha;
			public byte AlphaFormat;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct UpdateLayeredWindowInfo
		{
			public int Size;
			public IntPtr DestinationDc;
			public IntPtr DestinationPositionPointer;
			public IntPtr SizePointer;
			public IntPtr SourceDc;
			public IntPtr SourcePositionPointer;
			public int ColorKey;
			public IntPtr BlendPointer;
			public int Flags;
			public IntPtr DirtyRectPointer;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct BitmapInfo
		{
			public BitmapInfoHeader Header;
			public uint Colors;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct BitmapInfoHeader
		{
			public uint Size;
			public int Width;
			public int Height;
			public ushort Planes;
			public ushort BitCount;
			public uint Compression;
			public uint SizeImage;
			public int XPelsPerMeter;
			public int YPelsPerMeter;
			public uint ClrUsed;
			public uint ClrImportant;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct LogFont
		{
			public int lfHeight;
			public int lfWidth;
			public int lfEscapement;
			public int lfOrientation;
			public int lfWeight;
			public byte lfItalic;
			public byte lfUnderline;
			public byte lfStrikeOut;
			public byte lfCharSet;
			public byte lfOutPrecision;
			public byte lfClipPrecision;
			public byte lfQuality;
			public byte lfPitchAndFamily;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
			public string lfFaceName;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct NonClientMetrics
		{
			public int Size;
			public int BorderWidth;
			public int ScrollWidth;
			public int ScrollHeight;
			public int CaptionWidth;
			public int CaptionHeight;
			public LogFont CaptionFont;
			public int SmallCaptionWidth;
			public int SmallCaptionHeight;
			public LogFont SmallCaptionFont;
			public int MenuWidth;
			public int MenuHeight;
			public LogFont MenuFont;
			public LogFont StatusFont;
			public LogFont MessageFont;
			public int PaddedBorderWidth;
		}
	}

	public delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

	public delegate bool EnumWindowsDelegate(IntPtr hWnd, IntPtr lParam);

	public delegate void WinEventDelegate(
		IntPtr hWinEventHook,
		uint eventType,
		IntPtr hwnd,
		int idObject,
		int idChild,
		uint dwEventThread,
		uint dwmsEventTime);

	public enum GetAncestorFlags
	{
		Parent = 1,
		Root = 2,
		RootOwner = 3,
	}

	public enum ShowWindowCommand
	{
		Hide = 0,
		ShowNoActivate = 4,
	}

	[Flags]
	public enum RedrawWindowFlags
	{
		Invalidate = 0x0001,
		Erase = 0x0004,
		UpdateNow = 0x0100,
		NoChildren = 0x0040,
	}
}

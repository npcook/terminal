using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace npcook.Ssh
{
	internal static class NativeMethods
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public int left;
			public int top;
			public int right;
			public int bottom;
		}

		[DllImport("user32.dll")]
		public static extern bool GetClientRect(IntPtr hwnd, out RECT rect);

		[DllImport("user32.dll")]
		public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		public struct SHFILEINFO
		{
			public IntPtr hIcon;
			public int iIcon;
			public uint dwAttributes;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string szDisplayName;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
			public string szTypeName;
		};

		[Flags]
		public enum SHGFI : int
		{
			ADDOVERLAYS = 0x00020,
			ATTR_SPECIFIED = 0x20000,
			ATTRIBUTES = 0x00800,
			ICON = 0x00100,
			USEFILEATTRIBUTES = 0x00010,
			SMALLICON = 0x00001
		}

		public static uint FILE_ATTRIBUTE_NORMAL = 0x80;
		public static uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

		[DllImport("shell32.dll", CharSet = CharSet.Auto)]
		public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, SHGFI uFlags);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool DestroyIcon(IntPtr hIcon);

		public enum SIID : uint
		{
			SIID_DOCNOASSOC = 0,
			FOLDER = 3,
		}

		[Flags]
		public enum SHGSI : uint
		{
			ICONLOCATION = 0,
			ICON = 0x000000100,
			SYSICONINDEX = 0x000004000,
			LINKOVERLAY = 0x000008000,
			SELECTED = 0x000010000,
			LARGEICON = 0x000000000,
			SMALLICON = 0x000000001,
			SHELLICONSIZE = 0x000000004
		}

		[StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct SHSTOCKICONINFO
		{
			public UInt32 cbSize;
			public IntPtr hIcon;
			public Int32 iSysIconIndex;
			public Int32 iIcon;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string szPath;
		}

		[DllImport("Shell32.dll", SetLastError = false)]
		public static extern Int32 SHGetStockIconInfo(SIID siid, SHGSI uFlags, ref SHSTOCKICONINFO psii);
	}
}

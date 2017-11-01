using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Threading;
using System.Windows.Interop;
using Microsoft.VisualStudio.Progression;

namespace Microsoft.Samples.FileSystemProvider
{
    /// <summary>
    /// Contains methods to load file system icons
    /// </summary>
    static class FileIcons
    {

        public static string GetSmallIconName(Dispatcher dispatcher, IIconService cache, string fileName)
        {
            return GetIconName(dispatcher, cache, fileName, Flags.SHGFI_SMALLICON);
        }

        public static string GetLargeIconName(Dispatcher dispatcher, IIconService cache, string fileName)
        {
            return GetIconName(dispatcher, cache, fileName, Flags.SHGFI_LARGEICON);
        }

        enum Flags 
        {
            SHGFI_ICON = 0x100,
            SHGFI_LARGEICON = 0x0,   // 'Large icon
            SHGFI_SMALLICON = 0x1    // 'Small icon
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        [DllImport("shell32.dll")]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("User32.dll")]
        private static extern int DestroyIcon(IntPtr hIcon);

        private static string GetIconName(Dispatcher dispatcher, IIconService cache, string fileName, Flags flags)
        {
            string name = null;
            try {
                var shinfo = new SHFILEINFO();
                var rc = SHGetFileInfo(fileName, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), (uint)Flags.SHGFI_ICON | (uint)flags);
                if (rc != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                {
                    name = flags.ToString() + ":" + shinfo.iIcon.ToInt64();
                    if (cache.GetIcon(name) == null) 
                    {                        
                        dispatcher.Invoke(new System.Action(() =>
                        {
                            ImageSource result = Imaging.CreateBitmapSourceFromHIcon(shinfo.hIcon, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                            // This must be on the UI thread.
                            cache.AddIcon(name, Path.GetExtension(fileName), result);
                        }));
                        DestroyIcon(shinfo.hIcon);
                    }
                }
            } catch (ArgumentException) {
            }
            return name;
        }

    }

}

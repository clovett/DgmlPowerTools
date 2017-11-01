using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace Microsoft.Samples.FileSystemProvider
{
    class ReparsePoint
    {
        ReparsePoint()
        {
        }

        #region "DllImports, Constants & Structs"
        private const Int32 INVALID_HANDLE_VALUE = -1;
        private const Int32 OPEN_EXISTING = 3;
        private const Int32 FILE_FLAG_OPEN_REPARSE_POINT = 0x200000;
        private const Int32 FILE_FLAG_BACKUP_SEMANTICS = 0x2000000;
        private const Int32 FSCTL_GET_REPARSE_POINT = 0x900A8;

        /// <summary>
        /// If the path "REPARSE_GUID_DATA_BUFFER.SubstituteName" 
        /// begins with this prefix,
        /// it is not interpreted by the virtual file system.
        /// </summary>
        private const String NonInterpretedPathPrefix = "\\??\\";

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUNT_POINT_GUID_DATA_BUFFER
        {
            public UInt32 ReparseTag;
            public UInt16 ReparseDataLength;
            public UInt16 Reserved;
            public UInt16 SubstituteNameOffset;
            public UInt16 SubstituteNameLength;
            public UInt16 PrintNameOffset;
            public UInt16 PrintNameLength;
            /// <summary>
            /// Contains the SubstituteName and the PrintName.
            /// The SubstituteName is the path of the target directory.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3FF0)]
            public byte[] PathBuffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct REPARSE_GUID_DATA_BUFFER
        {
            public UInt32 ReparseTag;
            public UInt16 ReparseDataLength;
            public UInt16 Reserved;
            public UInt16 SubstituteNameOffset;
            public UInt16 SubstituteNameLength;
            public UInt16 PrintNameOffset;
            public UInt16 PrintNameLength;
            public UInt32 Flags;
            /// <summary>
            /// Contains the SubstituteName and the PrintName.
            /// The SubstituteName is the path of the target directory.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3FF0)]
            public byte[] PathBuffer;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(String lpFileName,
                                                Int32 dwDesiredAccess,
                                                Int32 dwShareMode,
                                                IntPtr lpSecurityAttributes,
                                                Int32 dwCreationDisposition,
                                                Int32 dwFlagsAndAttributes,
                                                IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern Int32 DeviceIoControl(IntPtr hDevice,
                                                     Int32 dwIoControlCode,
                                                     IntPtr lpInBuffer,
                                                     Int32 nInBufferSize,
                                                     IntPtr lpOutBuffer,
                                                     Int32 nOutBufferSize,
                                                     out Int32 lpBytesReturned,
                                                     IntPtr lpOverlapped);

        [StructLayout(LayoutKind.Sequential)]
        private struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternameFileName;
        }


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr FindFirstFile(String lpFileName, ref WIN32_FIND_DATA data);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int FindClose(IntPtr handle);

        const uint IO_REPARSE_TAG_DFS = 0x8000000A;
        const uint IO_REPARSE_TAG_DFSR = 0x80000012;
        const uint IO_REPARSE_TAG_HSM = 0xC0000004;
        const uint IO_REPARSE_TAG_HSM2 = 0x80000006;
        const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003;
        const uint IO_REPARSE_TAG_SIS = 0x80000007;
        const uint IO_REPARSE_TAG_SYMLINK = 0xA000000C;

        const uint SYMLINK_FLAG_RELATIVE = 0x00000001;
 
        [DllImport("kernel32.dll", SetLastError = true)]
        private extern static bool GetVolumeNameForVolumeMountPoint(string lpszVolumeMountPoint, 
            IntPtr lpszVolumeName,
            int cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = false)]
        private extern static int GetLastError();


        #endregion

        /// <summary>
        /// Gets the target directory from a directory link in Windows Vista.
        /// </summary>
        /// <param name="directoryInfo">The directory info of this directory 
        /// link</param>
        /// <returns>the target directory, if it was read, 
        /// otherwise an empty string.</returns>
        public static String GetTargetDir(FileSystemInfo directoryInfo, out bool isMountedFolder)
        {
            String targetDir = "";
            isMountedFolder = false;

            try
            {
                // Is it a directory link?
                if ((directoryInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    uint tag = 0;

                    // check whether it's a reparse point or a mounted folder.
                    WIN32_FIND_DATA data = new WIN32_FIND_DATA();
                    IntPtr find = FindFirstFile(directoryInfo.FullName, ref data);
                    if (find != IntPtr.Zero)
                    {
                        FindClose(find);
                        tag = data.dwReserved0;
                    }

                    isMountedFolder = tag == IO_REPARSE_TAG_MOUNT_POINT;
                    
                    // Open the directory link:
                    IntPtr hFile = CreateFile(directoryInfo.FullName,
                                                0,
                                                0,
                                                IntPtr.Zero,
                                                OPEN_EXISTING,
                                                FILE_FLAG_BACKUP_SEMANTICS |
                                                FILE_FLAG_OPEN_REPARSE_POINT,
                                                IntPtr.Zero);
                    if (hFile.ToInt32() != INVALID_HANDLE_VALUE)
                    {
                        SafeFileHandle hFileHandle = new SafeFileHandle(hFile, true);

                        using (hFileHandle)
                        {
                            // Allocate a buffer for the reparse point data:
                            Int32 outBufferSize = isMountedFolder ? Marshal.SizeOf(typeof(MOUNT_POINT_GUID_DATA_BUFFER)) : Marshal.SizeOf(typeof(REPARSE_GUID_DATA_BUFFER));
                            IntPtr outBuffer = Marshal.AllocHGlobal(outBufferSize);

                            try
                            {
                                // Read the reparse point data:
                                Int32 bytesReturned;
                                Int32 readOK = DeviceIoControl(
                                    hFile,
                                    FSCTL_GET_REPARSE_POINT,
                                    IntPtr.Zero,
                                    0,
                                    outBuffer,
                                    outBufferSize,
                                    out bytesReturned,
                                    IntPtr.Zero);

                                if (readOK != 0)
                                {
                                    bool isRelative = false;

                                    if (isMountedFolder)
                                    {
                                        // Get the target directory from the mounted folder :
                                        MOUNT_POINT_GUID_DATA_BUFFER rgdBuffer = (MOUNT_POINT_GUID_DATA_BUFFER)Marshal.PtrToStructure(outBuffer, typeof(MOUNT_POINT_GUID_DATA_BUFFER));
                                        targetDir = Encoding.Unicode.GetString(rgdBuffer.PathBuffer,
                                                rgdBuffer.SubstituteNameOffset,
                                                rgdBuffer.SubstituteNameLength);
                                    }
                                    else
                                    {
                                        // Get the target directory from the reparse point data:
                                        REPARSE_GUID_DATA_BUFFER rgdBuffer = (REPARSE_GUID_DATA_BUFFER)Marshal.PtrToStructure(outBuffer, typeof(REPARSE_GUID_DATA_BUFFER));
                                        targetDir = Encoding.Unicode.GetString(rgdBuffer.PathBuffer,
                                                rgdBuffer.SubstituteNameOffset,
                                                rgdBuffer.SubstituteNameLength);

                                        if ((rgdBuffer.Flags & SYMLINK_FLAG_RELATIVE) == SYMLINK_FLAG_RELATIVE)
                                        {
                                            isRelative = true;
                                        }
                                    }

                                    if (targetDir.StartsWith(NonInterpretedPathPrefix, StringComparison.OrdinalIgnoreCase))
                                    {
                                        targetDir = targetDir.Substring(NonInterpretedPathPrefix.Length);
                                        if (isRelative)
                                        {
                                            targetDir = new Uri(new Uri(directoryInfo.FullName), targetDir).LocalPath;
                                        }

                                        if (targetDir.EndsWith(Path.DirectorySeparatorChar.ToString()) && !targetDir.EndsWith(":" + Path.DirectorySeparatorChar))
                                        {
                                            targetDir = targetDir.Substring(0, targetDir.Length-1);
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                            }

                            // Free the buffer for the reparse point data:
                            Marshal.FreeHGlobal(outBuffer);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return targetDir;
        }
    }
}

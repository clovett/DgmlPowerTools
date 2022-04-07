using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.GraphProviders
{
    class CoffProvider : IDisposable
    {
        IntPtr contents;
        int length;
        const short IMAGE_DOS_SIGNATURE = 0x5A4D;

        public CoffProvider(byte[] image)
        {
            length = image.Length;
            contents = Marshal.AllocCoTaskMem(image.Length);
            Marshal.Copy(image, 0, contents, image.Length);
        }

        internal string[] GetImports()
        {
            List<string> imports = new List<string>();
            if (length >= Marshal.SizeOf<IMAGE_DOS_HEADER>())
            {
                IMAGE_DOS_HEADER header = Marshal.PtrToStructure<IMAGE_DOS_HEADER>(contents);
                if (IMAGE_DOS_SIGNATURE != header.e_magic)
                {
                    return null;
                }

                IntPtr ntheader = ImageNtHeader(contents);
                if (ntheader != IntPtr.Zero)
                {
                    IMAGE_NT_HEADERS nth = Marshal.PtrToStructure<IMAGE_NT_HEADERS>(ntheader);
                    if (nth.Signature == IMAGE_NT_SIGNATURE)
                    {
                        // ok, this is a nt executable.
                        if (nth.OptionalHeader.Magic == IMAGE_NT_OPTIONAL_HDR64_MAGIC)
                        {
                            // is a 64 bit app.
                            if (nth.OptionalHeader.NumberOfRvaAndSizes == 16)
                            {
                                // IMAGE_DATA_DIRECTORY import_directory = nth.OptionalHeader.DataDirectory1;
                                ulong size = 0;
                                IntPtr found = IntPtr.Zero;
                                IntPtr importDir = ImageDirectoryEntryToDataEx(contents, false, IMAGE_DIRECTORY_ENTRY_IMPORT, out size, out found);
                                int increment = Marshal.SizeOf<IMAGE_IMPORT_DESCRIPTOR>();
                                for (; importDir != IntPtr.Zero; importDir += increment)
                                {
                                    IMAGE_IMPORT_DESCRIPTOR desc = Marshal.PtrToStructure<IMAGE_IMPORT_DESCRIPTOR>(importDir);
                                    if (desc.Characteristics == 0 &&
                                        desc.TimeDateStamp == 0 &&
                                        desc.ForwarderChain == 0 &&
                                        desc.Name == 0 &&
                                        desc.FirstThunk == 0)
                                    {
                                        break;
                                    }

                                    // get it!
                                    string name = GetDllName(ntheader, ref desc);
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        imports.Add(name);
                                    }
                                }
                            }
                        }
                        else
                        {
                            throw new Exception("Only 64 bit apps are supported.");
                        }
                    }
                }
            }
            return imports.ToArray();
        }

        private string GetDllName(IntPtr ntheaders, ref IMAGE_IMPORT_DESCRIPTOR desc)
        {
            IntPtr lastRva;
            IntPtr DllNamePtr = ImageRvaToVa(ntheaders, contents, desc.Name, out lastRva);
            if (DllNamePtr != IntPtr.Zero)
            {
                return Marshal.PtrToStringAnsi(DllNamePtr);
            }
            return null;
        }

        const int IMAGE_DIRECTORY_ENTRY_IMPORT = 1;
        const int IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20b;
        const int IMAGE_NT_SIGNATURE = 0x00004550;

        [StructLayout(LayoutKind.Sequential)]
        struct IMAGE_DOS_HEADER
        {
            public short e_magic;                     // Magic number
            public short e_cblp;                      // Bytes on last page of file
            public short e_cp;                        // Pages in file
            public short e_crlc;                      // Relocations
            public short e_cparhdr;                   // Size of header in paragraphs
            public short e_minalloc;                  // Minimum extra paragraphs needed
            public short e_maxalloc;                  // Maximum extra paragraphs needed
            public short e_ss;                        // Initial (relative) SS value
            public short e_sp;                        // Initial SP value
            public short e_csum;                      // Checksum
            public short e_ip;                        // Initial IP value
            public short e_cs;                        // Initial (relative) CS value
            public short e_lfarlc;                    // File address of relocation table
            public short e_ovno;                      // Overlay number
            public short e_res0; //[4];                    // Reserved words
            public short e_res1;
            public short e_res2;
            public short e_res3;
            public short e_oemid;                     // OEM identifier (for e_oeminfo)
            public short e_oeminfo;                   // OEM information; e_oemid specific
            public short e_res20; // [10];                  // Reserved words
            public short e_res21;
            public short e_res22;
            public short e_res23;
            public short e_res24;
            public short e_res25;
            public short e_res26;
            public short e_res27;
            public short e_res28;
            public short e_res29;
            public int e_lfanew;                    // File address of new exe header
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IMAGE_FILE_HEADER
        {
            public short Machine;
            public short NumberOfSections;
            public int TimeDateStamp;
            public int PointerToSymbolTable;
            public int NumberOfSymbols;
            public short SizeOfOptionalHeader;
            public short Characteristics;
        }

        struct IMAGE_DATA_DIRECTORY
        {
            public int VirtualAddress;
            public int Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IMAGE_OPTIONAL_HEADER
        {
            public short Magic;
            public byte MajorLinkerVersion;
            public byte MinorLinkerVersion;
            public int SizeOfCode;
            public int SizeOfInitializedData;
            public int SizeOfUninitializedData;
            public int AddressOfEntryPoint;
            public int BaseOfCode;
            public long ImageBase;
            public int SectionAlignment;
            public int FileAlignment;
            public short MajorOperatingSystemVersion;
            public short MinorOperatingSystemVersion;
            public short MajorImageVersion;
            public short MinorImageVersion;
            public short MajorSubsystemVersion;
            public short MinorSubsystemVersion;
            public int Win32VersionValue;
            public int SizeOfImage;
            public int SizeOfHeaders;
            public int CheckSum;
            public short Subsystem;
            public short DllCharacteristics;
            public long SizeOfStackReserve;
            public long SizeOfStackCommit;
            public long SizeOfHeapReserve;
            public long SizeOfHeapCommit;
            public int LoaderFlags;
            public int NumberOfRvaAndSizes;
            public IMAGE_DATA_DIRECTORY DataDirectory0; // [IMAGE_NUMBEROF_DIRECTORY_ENTRIES] == 16
            public IMAGE_DATA_DIRECTORY DataDirectory1;
            public IMAGE_DATA_DIRECTORY DataDirectory2;
            public IMAGE_DATA_DIRECTORY DataDirectory3;
            public IMAGE_DATA_DIRECTORY DataDirectory4;
            public IMAGE_DATA_DIRECTORY DataDirectory5;
            public IMAGE_DATA_DIRECTORY DataDirectory6;
            public IMAGE_DATA_DIRECTORY DataDirectory7;
            public IMAGE_DATA_DIRECTORY DataDirectory8;
            public IMAGE_DATA_DIRECTORY DataDirectory9;
            public IMAGE_DATA_DIRECTORY DataDirectory10;
            public IMAGE_DATA_DIRECTORY DataDirectory11;
            public IMAGE_DATA_DIRECTORY DataDirectory12;
            public IMAGE_DATA_DIRECTORY DataDirectory13;
            public IMAGE_DATA_DIRECTORY DataDirectory14;
            public IMAGE_DATA_DIRECTORY DataDirectory15;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IMAGE_NT_HEADERS
        {
            public int Signature;
            public IMAGE_FILE_HEADER FileHeader;
            public IMAGE_OPTIONAL_HEADER OptionalHeader;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IMAGE_SECTION_HEADER
        {
            public byte Name0; // [IMAGE_SIZEOF_SHORT_NAME]; // IMAGE_SIZEOF_SHORT_NAME              8
            public byte Name1;
            public byte Name2;
            public byte Name3;
            public byte Name4;
            public byte Name5;
            public byte Name6;
            public byte Name7;
            // union {
            public uint PhysicalAddress;
            // uint VirtualSize;
            // };
            // Misc;
            public uint VirtualAddress;
            public uint SizeOfRawData;
            public uint PointerToRawData;
            public uint PointerToRelocations;
            public uint PointerToLinenumbers;
            public short NumberOfRelocations;
            public short NumberOfLinenumbers;
            public uint Characteristics;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IMAGE_IMPORT_DESCRIPTOR
        {
            // union {
            public uint Characteristics;            // 0 for terminating null import descriptor
            // uint OriginalFirstThunk;         // RVA to original unbound IAT (PIMAGE_THUNK_DATA)
            // }
            // DUMMYUNIONNAME;
            public uint TimeDateStamp;                  // 0 if not bound,
                                                        // -1 if bound, and real date\time stamp
                                                        //     in IMAGE_DIRECTORY_ENTRY_BOUND_IMPORT (new BIND)
                                                        // O.W. date/time stamp of DLL bound to (Old BIND)

            public uint ForwarderChain;                 // -1 if no forwarders
            public uint Name;
            public uint FirstThunk;                     // RVA to IAT (if bound this IAT has actual addresses)
        };

        protected virtual void Dispose(bool disposing)
        {
            if (contents != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(contents);
                contents = IntPtr.Zero;
            }
        }

        ~CoffProvider()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        [DllImport("dbghelp.dll")]
        static extern IntPtr ImageNtHeader(IntPtr ptr);

        [DllImport("dbghelp.dll")]
        static extern IntPtr ImageDirectoryEntryToDataEx(IntPtr Base, bool MappedAsImage, ushort DirectoryEntry, out ulong Size, out IntPtr foundHeader);

        [DllImport("dbghelp.dll")]
        static extern IntPtr ImageRvaToVa(/*PIMAGE_NT_HEADERS*/ IntPtr NtHeaders, IntPtr Base, uint Rva, out /*PIMAGE_SECTION_HEADER*/ IntPtr LastRvaSection);
    }
}

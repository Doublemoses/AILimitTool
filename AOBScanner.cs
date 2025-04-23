using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AILimitTool
{
    class AOBScanner : IDisposable
    {
        [DllImport("ntdll.dll")]
        static extern int NtReadVirtualMemory(IntPtr ProcessHandle, IntPtr BaseAddress, byte[] Buffer, UInt32 NumberOfBytesToRead, ref UInt32 NumberOfBytesRead);

        public List<int> sectionAddress = new List<int>();
        public List<int> sectionSize = new List<int>();
        public List<byte[]> sectionData = new List<byte[]>();

        bool disposed = false;

        public AOBScanner(IntPtr processHandle, IntPtr baseAddress, int size)
        {
            byte[] buffer = new byte[0x600];
            uint bytesRead = 0;

            NtReadVirtualMemory(processHandle, baseAddress, buffer, 0x600, ref bytesRead);

            using (MemoryStream stream = new MemoryStream(buffer))
            using (PEReader reader = new PEReader(stream))
            {
                var headers = reader.PEHeaders;

                foreach (var section in headers.SectionHeaders)
                {
                    if (section.Name == ".text")
                    {
                        sectionAddress.Add(section.VirtualAddress);
                        sectionSize.Add(section.SizeOfRawData);
                        sectionData.Add(new byte[section.SizeOfRawData]);
                        NtReadVirtualMemory(processHandle, baseAddress + section.VirtualAddress, sectionData[sectionData.Count - 1], (uint)section.SizeOfRawData, ref bytesRead);
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            { return; }

            if (disposing)
            {
                sectionAddress.Clear();
                sectionAddress = null;
                sectionSize.Clear();
                sectionSize = null;
                sectionData.Clear();
                sectionData = null;
            }
            disposed = true;
        }

        public int FindAddress(string patternSource, int section, int startOffset = 0, int offsetResult = 0, int chainOffset = 0)
        {
            if (section >= sectionAddress.Count)
            { return -1; }

            (byte, bool)[] searchPattern = ConvertSearchStringToBytes(patternSource);

            IntPtr currentAddress = IntPtr.Zero;
            int indexResult = -1;

            indexResult = ScanMemory(searchPattern, section, startOffset);

            if (indexResult != -1)
            {
                if (chainOffset != 0 && offsetResult == 0)
                { indexResult = BitConverter.ToInt32(sectionData[section], indexResult + chainOffset); }
                else if (chainOffset != 0 && offsetResult != 0)
                { indexResult += BitConverter.ToInt32(sectionData[section], indexResult + chainOffset) + offsetResult + sectionAddress[section]; }
                else
                { indexResult += sectionAddress[section] + offsetResult; }
            }
            return indexResult;
        }

        public IntPtr FindCodeCave(int length, string pattern = "FF")
        {
            return 0x0;
        }

        private (byte, bool)[] ConvertSearchStringToBytes(string patternSource)
        {
            string[] splitPattern = patternSource.Split(" ");
            (byte, bool)[] searchPattern = new (byte, bool)[splitPattern.Length];

            for (int i = 0; i < splitPattern.Length; i++)
            {
                if (splitPattern[i].Contains("?"))
                { searchPattern[i] = (0x0, true); }
                else
                {
                    try
                    {
                        searchPattern[i] = (Convert.ToByte(splitPattern[i], 16), false);
                    }
                    catch
                    {
                        MessageBox.Show("you absolute dingus you typed the address in wrong");
                    }
                }
            }

            return searchPattern;
        }

        private int ScanMemory((byte patternByte, bool isWildcard)[] searchPattern, int section, int startOffset)
        {


            for (int i = startOffset;
                  i <= (sectionSize[section] - searchPattern.Length);
                  i++)
            {
                if (searchPattern[0].patternByte == sectionData[section][i])
                {
                    bool matching = true;
                    int j = 1;

                    while (matching)
                    {
                        if (j >= searchPattern.Length)
                        {
                            return i;
                        }
                        if (!searchPattern[j].isWildcard && searchPattern[j].patternByte != sectionData[section][i + j])
                        {
                            matching = false;
                        }
                        j++;
                    }
                }
            }
            return -1;
        }
    }
}

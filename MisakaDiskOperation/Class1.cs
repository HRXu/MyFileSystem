using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DiskOperation
{
    public class PartitionBuilder
    {
        public List<PartitonInfo> partitionlist = new List<PartitonInfo>();
        byte[] MBR = new byte[512];
        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        public const short INVALID_HANDLE_VALUE = -1;
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint CREATE_NEW = 1;
        public const uint CREATE_ALWAYS = 2;
        public const uint OPEN_EXISTING = 3;
        public const uint FILE_BEGIN = 0;
        public const uint FILE_CURRENT = 1;
        public const uint FILE_END = 2;

        bool hasInitializedMBR=false;

        public string drivename { get; set; }
        [DllImport("Kernel32.dll")]
        extern static IntPtr CreateFile(string fileName, uint accessFlag, uint shareMode, IntPtr security, uint createFlag, uint attributeFlag, IntPtr tempfile);

        [DllImport("Kernel32.dll")]
        extern static bool WriteFile(IntPtr handle, [In] byte[] buffer, uint bufferLength, ref uint length, IntPtr overLapped);

        [DllImport("Kernel32.dll")]
        extern static bool CloseHandle(IntPtr handle);

        [DllImport("Kernel32.dll", SetLastError = true)]
        extern static uint SetFilePointer(IntPtr handle, uint offset, [In, Out] ref uint lpDistanceToMoveHigh, uint flag);

        /// <summary>
        /// 初始化分区表
        /// </summary>
        /// <param name="IPL"></param>
        /// <returns></returns>
        public byte InitializeMBR(byte [] IPL)
        {
            if (IPL.Length != 512) return 2;//mbr有问题
            if (partitionlist.Count == 0 || partitionlist.Count > 5) return 1;//分区超出限制

            //装入IPL
            for (int k = 0; k < 0x1BE; k++)
            {
                MBR[k] = IPL[k];
            }

            //装入分区信息
            for (int i = 0, j=0x1BE ; i < partitionlist.Count; i++, j+=16)
            {

                MBR[0 + j] = (byte)partitionlist[i].Status;

                //起始CHS
                MBR[1 + j] = (byte) (( partitionlist[i].StartSector / 63) % 255);//磁头号             
                uint c=((partitionlist[i].StartSector / 63 ) /255);//柱面
                uint s=(partitionlist[i].StartSector % 63 + 1);//sector
                UInt16 i1 = (UInt16)((c << 6) + s);
                MBR[2 + j] = (byte)i1;
                MBR[3 + j] = (byte)(i1 >> 8);

                MBR[4 + j] = (byte)partitionlist[i].FileSystem;

                //结束CHS
                MBR[5 + j] = (byte)(((partitionlist[i].StartSector + partitionlist[i].SectorCount - 1) / 63) % 255);//磁头号
                c = (((partitionlist[i].StartSector + partitionlist[i].SectorCount-1)/ 63) / 255);//柱面
                s = ((partitionlist[i].StartSector + partitionlist[i].SectorCount - 1) % 63 + 1);//sector
                i1 = (UInt16)((c << 6) + s);
                MBR[6 + j] = (byte)i1;
                MBR[7 + j] = (byte)(i1 >> 8);

                byte s1 = (byte)partitionlist[i].StartSector;
                byte s2 = (byte)(partitionlist[i].StartSector >> 8);
                byte s3 = (byte)(partitionlist[i].StartSector >> 16);
                byte s4 = (byte) (partitionlist[i].StartSector >> 24);
                MBR[8 + j] = s1;
                MBR[9 + j] = s2;
                MBR[10 + j] = s3;
                MBR[11 + j] = s4;

                s1 = (byte)partitionlist[i].SectorCount;
                s2 = (byte)(partitionlist[i].SectorCount >> 8);
                s3 = (byte)(partitionlist[i].SectorCount >> 16);
                s4 = (byte)(partitionlist[i].SectorCount >> 24);
                MBR[12 + j] = s1;
                MBR[13 + j] = s2;
                MBR[14 + j] = s3;
                MBR[15 + j] = s4;
            }
            MBR[510] = 0x55;
            MBR[511] = 0xAA;
            this.hasInitializedMBR = true;
            return 0;
        }

        /// <summary>
        /// 格式化正文
        /// </summary>
        /// <param name="IPL"></param>
        /// <returns></returns>
        public byte Build(byte[] IPL)
        {
            DiskRW.write(drivename, 0, MBR);
            if (!hasInitializedMBR) return 1;//还没初始化分区表
            for (int i = 1; i < partitionlist.Count; i++)
            {
                DiskRW.write(drivename, partitionlist[i].StartSector, IPL);
            }
            return 0;
        }
    }
    public class DiskRW
    {
        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        public const short INVALID_HANDLE_VALUE = -1;
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint CREATE_NEW = 1;
        public const uint CREATE_ALWAYS = 2;
        public const uint OPEN_EXISTING = 3;
        public const uint FILE_BEGIN = 0;
        public const uint FILE_CURRENT = 1;
        public const uint FILE_END = 2;

        [DllImport("Kernel32.dll")]
        public extern static int FormatMessage(int flag, ref IntPtr source, int msgid, int langid, ref string buf, int size, ref IntPtr args);

        [DllImport("Kernel32.dll")]
        extern static IntPtr CreateFile(string fileName, uint accessFlag, uint shareMode, IntPtr security, uint createFlag, uint attributeFlag, IntPtr tempfile);

        [DllImport("Kernel32.dll")]
        extern static bool ReadFile(IntPtr handle, [Out] byte[] buffer, uint bufferLength, ref uint length, IntPtr overLapped);

        [DllImport("Kernel32.dll")]
        extern static bool WriteFile(IntPtr handle, [In] byte[] buffer, uint bufferLength, ref uint length, IntPtr overLapped);

        [DllImport("Kernel32.dll")]
        extern static bool CloseHandle(IntPtr handle);

        [DllImport("Kernel32.dll",SetLastError= true)]
        extern static uint SetFilePointer(IntPtr handle, uint offset, [In, Out] ref uint lpDistanceToMoveHigh, uint flag);

        /// <summary>
        /// 读某个扇区 已测试
        /// </summary>
        /// <param name="drivename"></param>
        /// <param name="sector"></param>
        /// <returns></returns>
        static public byte[] read(string drivename, UInt32 sector)
        {
            IntPtr DiskHandle = CreateFile(drivename, GENERIC_READ, 0, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            byte[] buffer = new byte[512];
            uint length = 0;

            UInt64 len = sector;//偏移量 (uint)(len >> 32)
            len = len << 9;

            UInt32 lenhigh = (UInt32)(len >> 32);
            var i=SetFilePointer(DiskHandle, (uint)len,ref lenhigh, FILE_BEGIN);
            if (i == 0xffffffff)
            {
                var errCode = Marshal.GetLastWin32Error();
                IntPtr tempptr = IntPtr.Zero;
                string msg = null;
                FormatMessage(0x1300, ref tempptr, errCode, 0, ref msg, 255, ref tempptr);
                return null;
            }
            var res=ReadFile(DiskHandle, buffer, 512, ref length, IntPtr.Zero);
            if (res == false) return null;
            CloseHandle(DiskHandle);
            return buffer;
        }

        /// <summary>
        /// 写  已测试
        /// </summary>
        /// <param name="drivename"></param>
        /// <param name="sector"></param>
        /// <param name="buffer">写入内容 需为扇区整数倍</param>
        /// <returns></returns>
        static public bool write(string drivename, UInt32 sector, byte[] buffer)
        {
            IntPtr DiskHandle = CreateFile(drivename, GENERIC_WRITE, 0, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            uint length = 0;
            UInt64 len = sector ;//偏移量 (uint)(len >> 32)
            len = len << 9;
            //UInt32 lenhigh = 0;
            // var i = SetFilePointer(DiskHandle, 0x80000000,ref lenhigh, FILE_BEGIN);
            UInt32 lenhigh = (UInt32)(len>> 32);
            var i=SetFilePointer(DiskHandle, (uint)len, ref lenhigh, FILE_BEGIN);
            if (i == 0xffffffff)
            {
                var errCode=Marshal.GetLastWin32Error();
                IntPtr tempptr = IntPtr.Zero;
                string msg = null;
                FormatMessage(0x1300, ref tempptr, errCode, 0, ref msg, 255, ref tempptr);
                return false;
            }
            bool res=WriteFile(DiskHandle, buffer, (uint)buffer.Length, ref length, IntPtr.Zero);
            CloseHandle(DiskHandle);

            return res;
        }

        /// <summary>
        /// 读指定个数扇区 已测试
        /// </summary>
        /// <param name="drivename"></param>
        /// <param name="sector"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        static public byte[] ReadA(string drivename, UInt32 sector,UInt32 count)
        {
            IntPtr DiskHandle = CreateFile(drivename, GENERIC_READ, 0, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            byte[] buffer = new byte[512*count];
            uint length = 0;

            UInt64 len = sector;//偏移量 (uint)(len >> 32)
            len = len << 9;
            UInt32 lenhigh = (UInt32)(len >> 32);

            SetFilePointer(DiskHandle, (uint)len, ref lenhigh, FILE_BEGIN);
            ReadFile(DiskHandle, buffer, (uint)buffer.Length, ref length, IntPtr.Zero);
            CloseHandle(DiskHandle);
            return buffer;
        }

        /*static public async Task<byte[]>  ReadAsync(string drivename, UInt32 sector)
        {
            IntPtr DiskHandle = CreateFile(drivename, GENERIC_READ, 0, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            byte[] buffer = new byte[512];
            uint length = 0;

            UInt64 len = sector;//偏移量 (uint)(len >> 32)
            len = len << 9;

            UInt32 lenhigh = (UInt32)(len >> 32);
            var i = SetFilePointer(DiskHandle, (uint)len, ref lenhigh, FILE_BEGIN);
            if (i == 0xffffffff)
            {
                var errCode = Marshal.GetLastWin32Error();
                IntPtr tempptr = IntPtr.Zero;
                string msg = null;
                FormatMessage(0x1300, ref tempptr, errCode, 0, ref msg, 255, ref tempptr);
                return null;
            }


            await Task.Run(()=> 
            {
                ReadFile(DiskHandle, buffer, 512, ref length, IntPtr.Zero);
            });

            CloseHandle(DiskHandle);
            return buffer;
        }*/
    }
    public struct PartitonInfo
    {
        public UInt32 StartSector;
        public UInt32 SectorCount;

        public FileSystems FileSystem;
        public Statusx Status;

        public enum FileSystems :byte {
            Empty = 0x00,
            FAT12 =0x01,
            FAT16=0x04,
            NTFS = 0x07,
            SiliconSafeReserved = 0x0D,
            MicrosoftReserved=0x26,
            EFI=0xEF,
            GPTprotectiveMBR=0xEE
        };
        public enum Statusx: byte
        {
            active=0x80,
            inactive=0x00
        }
    }
}

using DiskOperation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XFileSystemSlim
{
    public class Operation
    {
        public enum ErrorCode : byte
        {
            Success = 0,
            Fail = 1,

            //写入空闲表失败 用于DeleteFile
            WriteFreeTableFail = 3,
            //没有足够空间 用于WriteFile
            LackOfSpace = 4,
            //找不到该文件 用于DeleteInDir
            NoSuchFile = 5,
        }
        /// <summary>
        /// 簇与逻辑扇区转换 已测试
        /// </summary>
        /// <param name="firstSector">分区首扇区，不包括mbr</param>
        /// <param name="cluster">簇号</param>
        /// <param name="fileUnit">分配单元大小(簇大小)</param>
        /// <returns>扇区号</returns>
        public static UInt32 transfer(UInt32 firstSector, UInt32 cluster, UInt32 fileUnit)
        {
            var res = firstSector + cluster * (fileUnit >> 9);
            return res;
        }
        /// <summary>
        /// 逻辑扇区与簇转换 已测试
        /// </summary>
        /// <param name="firstSector">起始扇区</param>
        /// <param name="sector">扇区号</param>
        /// <param name="fileUnit">分配单元大小（字节）</param>
        /// <returns>簇号</returns>
        public static UInt32 TransferInverse(UInt32 firstSector, UInt32 sector, UInt16 fileUnit)
        {
            var gap = sector - firstSector;
            //一个簇有多少个扇区
            var i = fileUnit >> 9;
            return (UInt32)(gap / i);
        }
        /// <summary>
        /// 转换4个Byte为一个32位无符号数 已测试
        /// </summary>
        /// <param name="s1"></param>
        /// <param name="s2"></param>
        /// <param name="s3"></param>
        /// <param name="s4"></param>
        /// <returns></returns>
        private static UInt32 mergeByte(byte s1, byte s2, byte s3, byte s4)
        {
            UInt32 i1 = s1;
            UInt32 i2 = s2;
            UInt32 i3 = s3;
            UInt32 i4 = s4;
            return (UInt32)((s4 << 24) + (s3 << 16) + (s2 << 8) + s1);
        }

        /// <summary>
        /// 设置某簇对应空闲表中某一位
        /// </summary>
        /// <param name="cluster">簇号</param>
        /// <param name="value">值</param>
        /// <param name="table">表</param>
        public static void Set(UInt32 cluster, bool value, byte[] table)
        {
            UInt32 i = cluster / 8;//第几个字节
            UInt32 j = cluster % 8;//第几位
            if (value == true) //置1
            {
                switch (j)
                {
                    case 0:
                        table[i] |= 0x01;
                        break;
                    case 1:
                        table[i] |= 0x02;
                        break;
                    case 2:
                        table[i] |= 0x04;
                        break;
                    case 3:
                        table[i] |= 0x08;
                        break;
                    case 4:
                        table[i] |= 0x10;
                        break;
                    case 5:
                        table[i] |= 0x20;
                        break;
                    case 6:
                        table[i] |= 0x40;
                        break;
                    case 7:
                        table[i] |= 0x80;
                        break;
                }
            }
            else //置0
            {
                switch (j)
                {
                    case 0:
                        table[i] &= 0xFE;
                        break;
                    case 1:
                        table[i] &= 0xFD;
                        break;
                    case 2:
                        table[i] &= 0xFB;
                        break;
                    case 3:
                        table[i] &= 0xF7;
                        break;
                    case 4:
                        table[i] &= 0xEF;
                        break;
                    case 5:
                        table[i] &= 0xDF;
                        break;
                    case 6:
                        table[i] &= 0xBF;
                        break;
                    case 7:
                        table[i] &= 0x7F;
                        break;
                }
            }
        }
        /// <summary>
        /// 判断空闲表中某扇区是否空闲
        /// </summary>
        /// <param name="cluster">簇号</param>
        /// <param name="table">空闲表引用</param>
        /// <returns>true为占用，false为空闲</returns>
        private static bool Test(UInt32 cluster, byte[] table)
        {
            UInt32 i = cluster / 8;//第几个字节
            UInt32 j = cluster % 8;//第几位

            byte res = table[i];
            //按位与即可得到对应位信息
            switch (j)
            {
                case 0:
                    res &= 0x01;
                    break;
                case 1:
                    res &= 0x02;
                    break;
                case 2:
                    res &= 0x04;
                    break;
                case 3:
                    res &= 0x08;
                    break;
                case 4:
                    res &= 0x10;
                    break;
                case 5:
                    res &= 0x20;
                    break;
                case 6:
                    res &= 0x40;
                    break;
                case 7:
                    res &= 0x80;
                    break;
            }
            if (res > 0) return true;
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 获取空闲块
        /// </summary>
        /// <param name="drivename">驱动器号</param>
        /// <param name="count">需要的扇区数</param>
        /// <param name="volume">分区信息结构体</param>
        /// <returns>空闲块列表(扇区)</returns>
        public static List<UInt32>GetFreeBlock(string drivename,UInt32 count,VolumeInfo volume)
        {
            //空闲表固定长度为3个扇区
            byte[] freetable = DiskRW.ReadA(drivename,volume.freetableStart, 3);

            List<UInt32> ls = new List<uint>();

            //i为计数,j为簇索引值
            //最大分区长0x400000
            UInt32 i = 0;
            for (UInt32 j = 0; j< 0x400000 ; j++)
            {
                if (!Test(j, freetable))
                {
                    ls.Add(transfer(volume.start,j,512));
                    i++;
                }
                if (i == count) break;
            }
            //磁盘已满或没有足够空间返回null;
            if (ls.Count < i) return null;
            //写空闲表
            foreach (UInt32 item in ls)
            {
                Set(TransferInverse(volume.start,item,512), true, freetable);
            }
            DiskRW.write(drivename, volume.freetableStart, freetable);
            return ls;
        }

        /// <summary>
        /// 获取文件内容位置，目录型为fileinfo位置，文件型为数据块位置
        /// </summary>
        /// <param name="drivename">驱动器号</param>
        /// <param name="info">文件信息</param>
        /// <returns></returns>
        public static List<UInt32> GetContent(string drivename, FileInfo info)
        {
            var res = new List<UInt32>();
            //文件总长1个扇区
            byte[] content = DiskRW.ReadA(drivename, info.Location, 1);
            for (int i = 48; i < content.Length; i+=4)
            {
                UInt32 s = mergeByte(content[i], content[i + 1], content[i + 2], content[i + 3]);
                if (s == 0) continue;
                res.Add(s);
            }
            return res;
        }
        /// <summary>
        /// 获取文件头信息
        /// </summary>
        /// <param name="drivename"></param>
        /// <param name="sector"></param>
        /// <returns></returns>
        public static FileInfo GetFileInfo(string drivename,UInt32 sector)
        {
            byte[] content = DiskRW.ReadA(drivename, sector, 1);
            var file = new FileInfo();

            //文件名
            byte[] tmp = new byte[32];
            Array.Copy(content, tmp, 32);
            file.Name = Encoding.ASCII.GetString(tmp).Replace("\0", "");

            //后缀名
            tmp = new byte[13];
            Array.Copy(content, 32, tmp, 0, 13);
            file.Extension = Encoding.ASCII.GetString(tmp).Replace("\0", "");

            //类型
            file.Type = content[46];

            file.Length = content[47];
            file.Location = sector;
            return file;
        }

        /// <summary>
        /// 设置文件头信息
        /// </summary>
        /// <param name="drivename"></param>
        /// <param name="fat"></param>
        /// <returns></returns>
        public static ErrorCode SetFileInfo(string drivename, FileInfo fat)
        {
            byte[] buffer = DiskRW.read(drivename, fat.Location);

            //name
            for (int i = 0; i < 48; i++)
            {
                buffer[i] = 0;
            }
            byte[] name = Encoding.ASCII.GetBytes(fat.Name);
            name.CopyTo(buffer, 0);
            //扩展名
            name = Encoding.ASCII.GetBytes(fat.Extension);
            name.CopyTo(buffer, 32);

            //类型文件长
            buffer[46] = fat.Type;
            buffer[47] = fat.Length;

            bool res = DiskRW.write(drivename, fat.Location, buffer);
            if (!res) return ErrorCode.Fail;
            return ErrorCode.Success;
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="drivename"></param>
        /// <param name="info"></param>
        /// <param name="freetableLocation"></param>
        /// <param name="start"></param>
        /// <returns></returns>
        public static ErrorCode DeleteFile(string drivename,FileInfo info,VolumeInfo volume)
        {
            //获取空闲表 2扇区
            byte[] freetable = DiskRW.ReadA(drivename, volume.freetableStart, 2);
            var ls = GetContent(drivename, info);

            //数据块写回空闲表
            foreach (var item in ls)
            {
                Set(TransferInverse(volume.start,item,512), false, freetable);
            }

            //文件块写回空闲表
            Set(TransferInverse( volume.start,info.Location,512), false, freetable);

            DiskRW.write(drivename, volume.freetableStart, freetable);
            return ErrorCode.Success;
        }
        /// <summary>
        /// 删除目录中某项
        /// </summary>
        /// <param name="drivename">驱动器名</param>
        /// <param name="volumeinfo">卷信息结构体</param>
        /// <param name="sector">要删的扇区</param>
        /// <param name="dirinfo">目录</param>
        /// <returns></returns>
        public static ErrorCode DeleteInDir(string drivename, VolumeInfo volume, UInt32 sector, FileInfo currentDir)
        {
            byte[] content = DiskRW.ReadA(drivename, currentDir.Location, 1);

            for (UInt16 i = 48; i < content.Length; i += 4)
            {
                UInt32 s1 = mergeByte(content[i], content[i + 1], content[i + 2], content[i + 3]);
                if (s1 == sector)
                {
                    content[i] = 0;
                    content[i + 1] = 0;
                    content[i + 2] = 0;
                    content[i + 3] = 0;
                    DiskRW.write(drivename, currentDir.Location, content);
                    return ErrorCode.Success;
                }
            }
            return ErrorCode.NoSuchFile;
        }

        /// <summary>
        /// 添加文件头
        /// </summary>
        /// <param name="currentDirInfo"></param>
        /// <param name="drivename"></param>
        /// <param name="volume"></param>
        /// <returns>文件头簇号</returns>
        public static UInt32 AddFile(FileInfo currentDirInfo,string drivename,VolumeInfo volume)
        {
            var currentDir = DiskRW.ReadA(drivename, currentDirInfo.Location, 1);
            var ls = GetFreeBlock(drivename, 1, volume);

            DiskRW.write(drivename, ls[0], new byte[512]);//复写已有内容
            //检索当前目录的fat表
            for (int i = 48; i < currentDir.Length; i += 4)
            {
                if (mergeByte(currentDir[i], currentDir[i + 1], currentDir[i + 2], currentDir[i + 3]) == 0)
                {
                    currentDir[i] = (byte)ls[0];
                    currentDir[i + 1] = (byte)(ls[0] >> 8);
                    currentDir[i + 2] = (byte)(ls[0] >> 16);
                    currentDir[i + 3] = (byte)(ls[0] >> 24);

                    //写回磁盘
                    DiskRW.write(drivename, currentDirInfo.Location, currentDir);
                    return ls[0];
                }
            }
            return 0;
        }
        /// <summary>
        /// 写文件
        /// </summary>
        /// <param name="drivename">驱动器名</param>
        /// <param name="volume">卷名</param>
        /// <param name="fat">文件头</param>
        /// <param name="content">写入的内容</param>
        /// <returns></returns>
        public static ErrorCode WriteFile(string drivename,VolumeInfo volume,ref FileInfo fat,byte[] content)
        {
           UInt32 SectorCount = (((UInt32)content.Length) & 0xFFFFFE00)>>9; //除512商
           UInt32 rest = ((UInt32)content.Length) & 0x000001FF; //除512余数

            if (rest != 0) SectorCount++; //content是否是512整数倍，不是的话要多出一个扇区存剩余的内容

            var blockList = GetFreeBlock(drivename, SectorCount, volume);
            if (blockList == null) return ErrorCode.LackOfSpace;

            fat.Length = (byte)SectorCount;
            AppendBlock(drivename, volume, ref fat, blockList);
            //写入文件内容
            UInt32 j = 0;//文件内容下标
            foreach (var block in blockList)
            {            
                var b = new byte[512];
                Array.Copy(content, j, b, 0, content.Length);
                DiskRW.write(drivename, block, b);
            }

            return ErrorCode.Success;
        }

        /// <summary>
        /// 向某个空文件添加内容块列表
        /// </summary>
        /// <param name="drivename">驱动器</param>
        /// <param name="volumeinfo">卷信息</param>
        /// <param name="file">文件头的引用</param>
        /// <param name="blocks">内容块列表</param>
        /// <returns></returns>
        static ErrorCode AppendBlock(string drivename,VolumeInfo volumeinfo,ref FileInfo file, List<UInt32> blocks)
        {
            byte[] content = DiskRW.read(drivename, file.Location);

            //文件长度
            content[47] = (byte)(blocks.Count);
            //i为file头的索引,j为List的索引
            int j = 0;
            for (UInt32 i = 48; i < content.Length; i+=4)
            {
                content[i] = (byte)(blocks[j]);
                content[i+1] = (byte)(blocks[j] >> 8);
                content[i+2] = (byte)(blocks[j] >> 16);
                content[i+3] = (byte)(blocks[j] >> 24);
                j++;
                if (j == blocks.Count) break;
            }

            //写回磁盘
            DiskRW.write(drivename, file.Location, content);
            return ErrorCode.Success;          
        }
    }
    public class FileInfo
    {
        //文件名
        public string Name { get; set; }

        //扩展名
        public string Extension { get; set; }

        //类型
        public byte Type { get; set; }

        //位置(扇区)
        public UInt32 Location { get; set; }

        //长度(单位:扇区)
        public byte Length { get; set; }
    } 
    public class VolumeInfo
    {
        /// <summary>
        /// 空闲表起始
        /// </summary>
        public UInt32 freetableStart;
        /// <summary>
        /// 分区起始
        /// </summary>
        public UInt32 start;
    }
}

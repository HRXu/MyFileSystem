using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiskOperation;

namespace XFileSystem
{
    /// <summary>
    /// 文件系统底层操作
    /// </summary>
    public class myFileSystem
    {
        public enum ErrorCode :byte
        {
            Success=0,
            Fail=1,
            //簇长错误 用于WriteIn
            ErrorClusterLength=2,
            //写入空闲表失败 用于DeleteFile
            WriteFreeTableFail=3,
            //没有足够空间 用于WriteFile
            LackOfSpace=4,
            //找不到该文件 用于DeleteInDir
            NoSuchFile=5,
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
        public static UInt32 TransferInverse(UInt32 firstSector,UInt32 sector,UInt16 fileUnit)
        {
            var gap = sector - firstSector;
            //一个簇有多少个扇区
            var i = fileUnit >> 9;
            return (UInt32)(gap/i);
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
        /// 写入某个簇内容
        /// </summary>
        /// <param name="drivename">驱动器</param>
        /// <param name="volumeinfo">卷信息结构体</param>
        /// <param name="cluster">要写入的簇号</param>
        /// <param name="content">写入内容</param>
        /// <returns></returns>
        public static ErrorCode WriteIn(string drivename,VolumeInfo volumeinfo,UInt32 cluster,byte[] content)
        {
            if (content.Length != volumeinfo.Cluster) return ErrorCode.ErrorClusterLength;
            var res = DiskRW.write(drivename, transfer(volumeinfo.SectorStart, cluster, volumeinfo.Cluster), content);
            if (res)
            {
                return ErrorCode.Success;
            }
            return ErrorCode.Fail;
        }
        /// <summary>
        /// 读取某簇内容
        /// </summary>
        /// <param name="drivename">驱动器</param>
        /// <param name="volumeinfo">卷信息结构体</param>
        /// <param name="cluster">要读取的簇号</param>
        /// <returns>具体内容</returns>
        public static byte[] ReadOut(string drivename,VolumeInfo volumeinfo, UInt32 cluster)
        {
            var res = DiskRW.ReadA(drivename, transfer(volumeinfo.SectorStart, cluster, volumeinfo.Cluster), (UInt32)(volumeinfo.Cluster>>9));          
            return res;
        }

        /// <summary>
        /// 获取卷信息
        /// </summary>
        /// <param name="drivename">驱动器名</param>
        /// <param name="sectorstart">卷开始扇区</param>
        /// <returns>卷信息结构体</returns>
        static public VolumeInfo GetVolumeInfo(string drivename,UInt32 sectorstart)
        {
            VolumeInfo volumeinfo = new VolumeInfo();
            byte[] content=DiskRW.read(drivename,sectorstart);

            byte[] tmp = new byte[32];
            Array.Copy(content, tmp, 32);
            volumeinfo.VolumeName = System.Text.Encoding.Unicode.GetString(tmp);

            UInt32 s1 = content[32];
            UInt32 s2 = content[33];
            UInt32 s3 = content[34];
            UInt32 s4 = content[35];
            volumeinfo.SectorStart = (s4 << 24) + (s3 << 16) + (s2 << 8) + s1;

            s1 = content[36];
            s2 = content[37];
            s3 = content[38];
            s4 = content[39];
            volumeinfo.SectorCount = (s4 << 24) + (s3 << 16) + (s2 << 8) + s1;

            volumeinfo.Cluster = (UInt16)(((UInt16)content[40]) << 9);

            volumeinfo.VolumeLabel = content[41];

            s1 = content[42];
            s2 = content[43];
            s3 = content[44];
            s4 = content[45];
            volumeinfo.FreeTableStart = (s4 << 24) + (s3 << 16) + (s2 << 8) + s1;

            volumeinfo.EncryptionType = content[48];

            return volumeinfo;
        }
        /// <summary>
        /// 写回卷信息
        /// </summary>
        /// <param name="drivename">驱动器名</param>
        /// <param name="volumeInfo">卷信息结构体</param>
        /// <returns>错误代码</returns>
        static public ErrorCode SetVolumeInfo(string drivename,VolumeInfo volumeInfo)
        {
            var  header = new byte[512];
            byte[] name = Encoding.Unicode.GetBytes("新加卷");
            name.CopyTo(header, 0);

            //起始簇扇区
            header[32] = (byte)volumeInfo.SectorStart;
            header[33] = (byte)(volumeInfo.SectorStart >> 8);
            header[34] = (byte)(volumeInfo.SectorStart >> 16);
            header[35] = (byte)(volumeInfo.SectorStart >> 24);

            //分区长度
            header[36] = (byte)(volumeInfo.SectorCount);
            header[37] = (byte)(volumeInfo.SectorCount >> 8);
            header[38] = (byte)(volumeInfo.SectorCount >> 16);
            header[39] = (byte)(volumeInfo.SectorCount >> 24);

            //分配单元大小 512B 的倍数
            header[40] = (byte)(volumeInfo.Cluster >> 9);

            //卷标 预留为空 ascii编码
            header[41] = volumeInfo.VolumeLabel;

            //空闲闲表头
            header[42] = (byte)(volumeInfo.FreeTableStart);
            header[43] = (byte)(volumeInfo.FreeTableStart >> 8);
            header[44] = (byte)(volumeInfo.FreeTableStart >> 16);
            header[45] = (byte)(volumeInfo.FreeTableStart >> 24);

            //加密方式 0 不加密
            header[48] = volumeInfo.EncryptionType;

            bool res=DiskRW.write(drivename, volumeInfo.SectorStart,header);
            if (!res) return ErrorCode.Fail;
            return ErrorCode.Success;
        }
        /// <summary>
        /// 从某个FAT节点读取文件信息,返回值要转换方可使用
        /// </summary>
        /// <param name="drivename"></param>
        /// <param name="location">扇区</param>
        /// <param name="fileUnit"></param>
        /// <returns>返回目录信息或者文件信息</returns>
        public static FATInfo GetFileInfo(string drivename,UInt32 location,UInt16 fileUnit)
        {
            byte[] content=DiskRW.ReadA(drivename, location, (uint)(fileUnit >> 9));
            if (content[77] == 0) //目录
            {
                var dir = new DirectInfo();
                byte[] tmp = new byte[64];
                Array.Copy(content, tmp, 64);
                dir.Name = Encoding.Unicode.GetString(tmp).Replace("\0","");


                //类型
                dir.Type = content[77];
                //权限号
                dir.AccessCode =content[78];
                //权限标记
                dir.AccessMode = content[79];
                //作者
                tmp = new byte[32];
                Array.Copy(content, 80, tmp, 0, 32);
                dir.Author = Encoding.Unicode.GetString(tmp);
                //最后修改人
                tmp = new byte[32];
                Array.Copy(content, 112, tmp, 0, 32);
                dir.LastEditor = Encoding.Unicode.GetString(tmp);
                //创建时间
                UInt32 s1 = content[144];
                UInt32 s2 = content[145];
                UInt32 s3 = content[146];
                UInt32 s4 = content[147];
                dir.CreateTime = (s4 << 24) + (s3 << 16) + (s2 << 8) + s1;
                //最后修改时间
                s1 = content[148];
                s2 = content[149];
                s3 = content[150];
                s4 = content[151];
                dir.LastEditTime = (s4 << 24) + (s3 << 16) + (s2 << 8) + s1;

                dir.Location = location;

                return dir;
            }
            else if (content[77]!=0) //实体文件
            {
                var file = new FileInfo();
                byte[] tmp = new byte[64];
                Array.Copy(content, tmp, 64);
                file.Name = Encoding.Unicode.GetString(tmp).Replace("\0", "");

                //后缀名
                tmp = new byte[13];
                Array.Copy(content,64,tmp,0,13 );
                file.Extension = Encoding.Unicode.GetString(tmp).Replace("\0", "");

                //类型
                file.Type = content[77];
                //权限号
                file.AccessCode = content[78];
                //权限标记
                file.AccessMode = content[79];
                //作者
                tmp = new byte[32];
                Array.Copy(content, 80, tmp, 0, 32);
                file.Author = Encoding.Unicode.GetString(tmp);
                //最后修改人
                tmp = new byte[32];
                Array.Copy(content, 112, tmp, 0, 32);
                file.LastEditor = Encoding.Unicode.GetString(tmp);
                //创建时间
                UInt32 s1 = content[144];
                UInt32 s2 = content[145];
                UInt32 s3 = content[146];
                UInt32 s4 = content[147];
                file.CreateTime = (s4 << 24) + (s3 << 16) + (s2 << 8) + s1;
                //最后修改时间
                s1 = content[148];
                s2 = content[149];
                s3 = content[150];
                s4 = content[151];
                file.LastEditTime = (s4 << 24) + (s3 << 16) + (s2 << 8) + s1;

                //尾字节
                UInt16 s5 = content[152];
                UInt16 s6 = content[153];
                file.Rest =(UInt16)((s6 << 8) + s5);


                file.Location = location;
                return file;
            }
            return null;
        }
        /// <summary>
        /// 获取实体文件信息
        /// </summary>
        /// <param name="drivename"></param>
        /// <param name="location"></param>
        /// <param name="fileUnit"></param>
        /// <returns></returns>
        public static FileInfo GetFileInfoA(string drivename, UInt32 location, UInt16 fileUnit)
        {
            byte[] content = DiskRW.ReadA(drivename, location, (uint)(fileUnit >> 9));
                var file = new FileInfo();
                byte[] tmp = new byte[64];
                Array.Copy(content, tmp, 64);
                file.Name = Encoding.Unicode.GetString(tmp).Replace("\0", "");

                //后缀名
                tmp = new byte[13];
                Array.Copy(content, 64, tmp, 0, 13);
                file.Extension = Encoding.ASCII.GetString(tmp).Replace("\0", "");

                //类型
                file.Type = content[77];
                //权限号
                file.AccessCode = content[78];
                //权限标记
                file.AccessMode = content[79];
                //作者
                tmp = new byte[32];
                Array.Copy(content, 80, tmp, 0, 32);
                file.Author = Encoding.Unicode.GetString(tmp);
                //最后修改人
                tmp = new byte[32];
                Array.Copy(content, 112, tmp, 0, 32);
                file.LastEditor = Encoding.Unicode.GetString(tmp);
                //创建时间
                file.CreateTime = mergeByte(content[144], content[145], content[146], content[147]);
                //最后修改时间
                file.LastEditTime = mergeByte(content[148], content[149], content[150], content[151]);

                //尾字节
                UInt16 s5 = content[152];
                UInt16 s6 = content[153];
                file.Rest = (UInt16)((s6 << 8) + s5);

                file.Location = location;
                return file;
        }


        /// <summary>
        /// 设置文件信息  不改变文件内容
        /// </summary>
        /// <param name="drivename">驱动器</param>
        /// <param name="location">文件位置扇区</param>
        /// <param name="fat">文件表</param>
        /// <returns></returns>
        public static ErrorCode SetFileInfo(string drivename,DirectInfo fat)
        {

            byte[] buffer = DiskRW.read(drivename,fat.Location);

            //name
            for (int i = 0; i < 63; i++)
            {
                buffer[i] = 0;
            }
            byte[] name = Encoding.Unicode.GetBytes(fat.Name);
            name.CopyTo(buffer, 0);

            //文件号 权限号 权限标记
            buffer[77] = fat.Type;
            buffer[78] = fat.AccessCode;
            buffer[79] = fat.AccessMode;

            //作者
            name = Encoding.Unicode.GetBytes(fat.Author);
            name.CopyTo(buffer, 80);
            //最后修改人
            name = Encoding.Unicode.GetBytes(fat.LastEditor);
            name.CopyTo(buffer, 112);

            //创建日期
            buffer[144] = (byte)(fat.CreateTime);
            buffer[145] = (byte)(fat.CreateTime >> 8);
            buffer[146] = (byte)(fat.CreateTime >> 16);
            buffer[147] = (byte)(fat.CreateTime >> 24);
            //修改日期
            buffer[148] = (byte)(fat.LastEditTime);
            buffer[149] = (byte)(fat.LastEditTime >> 8);
            buffer[150] = (byte)(fat.LastEditTime >> 16);
            buffer[151] = (byte)(fat.LastEditTime >> 24);

            bool res = DiskRW.write(drivename, fat.Location, buffer);
            if (!res) return ErrorCode.Fail;
            return ErrorCode.Success;
        }
        public static ErrorCode SetFileInfo(string drivename, FileInfo fat)
        {
            byte[] buffer = DiskRW.read(drivename, fat.Location);

            //name
            for (int i = 0; i < 63; i++)
            {
                buffer[i] = 0;
            }
            byte[] name = Encoding.Unicode.GetBytes(fat.Name);
            name.CopyTo(buffer, 0);

            name = Encoding.Unicode.GetBytes(fat.Extension);
            name.CopyTo(buffer, 64);

            //文件号 权限号 权限标记
            buffer[77] = fat.Type;
            buffer[78] = fat.AccessCode;
            buffer[79] = fat.AccessMode;

            //作者
            name = Encoding.Unicode.GetBytes(fat.Author);
            name.CopyTo(buffer, 80);
            //最后修改人
            name = Encoding.Unicode.GetBytes(fat.LastEditor);
            name.CopyTo(buffer, 112);

            //创建日期
            buffer[144] = (byte)(fat.CreateTime);
            buffer[145] = (byte)(fat.CreateTime >> 8);
            buffer[146] = (byte)(fat.CreateTime >> 16);
            buffer[147] = (byte)(fat.CreateTime >> 24);
            //修改日期
            buffer[148] = (byte)(fat.LastEditTime);
            buffer[149] = (byte)(fat.LastEditTime >> 8);
            buffer[150] = (byte)(fat.LastEditTime >> 16);
            buffer[151] = (byte)(fat.LastEditTime >> 24);

            //尾字节长
            buffer[152] = (byte)fat.Rest;
            buffer[153] = (byte)(fat.Rest >> 8);

            bool res = DiskRW.write(drivename, fat.Location, buffer);
            if (!res) return ErrorCode.Fail;
            return ErrorCode.Success;
        }

        /// <summary>
        /// 获取文件内容具体位置(簇号)
        /// </summary>
        /// <param name="drivename"></param>
        /// <param name="Location">扇区号</param>
        /// <param name="fileUnit"></param>
        /// <returns>位置表(簇号)</returns>
        public static List<UInt32> GetList(string drivename,UInt32 location,VolumeInfo volumeinfo)
        {
            var res = new List<UInt32>();
            byte[] content = DiskRW.ReadA(drivename, location, (uint)(volumeinfo.Cluster >> 9));

            UInt32 next = mergeByte(content[160], content[161], content[162], content[163]);
            for (UInt16 i = 164; i < content.Length; i += 4)
            {
                UInt32 s1=mergeByte(content[i], content[i + 1], content[i + 2], content[i + 3]);
                if (s1 == 0)
                {
                    continue;
                }
                res.Add(s1);
            }

            //没有下一个索引块
            if (next==0)
            {
                return res;
            }

            //读取索引表
            content = DiskRW.ReadA(drivename, transfer(volumeinfo.SectorStart,next,volumeinfo.Cluster), (uint)(volumeinfo.Cluster >> 9));
            while (true)
            {

                for (UInt16 i = 4; i < content.Length; i += 4)
                {
                    UInt32 s1 = mergeByte(content[i], content[i + 1], content[i + 2], content[i + 3]);
                    if (s1 == 0)
                    {
                        continue;
                    }
                    res.Add(s1);
                }
                next = mergeByte(content[0], content[1], content[2], content[3]);
                //下簇号 为零表示没有下一簇
                if (next==0)
                {
                    return res;
                }              
                content = ReadOut(drivename,volumeinfo,next);
            }
        }   

        /// <summary>
        /// 获取空闲块 位示图
        /// </summary>
        /// <param name="drivename"></param>
        /// <param name="volumeInfo">卷信息的引用</param>
        /// <param name="count">所需空闲块个数</param>
        /// <returns>空闲簇列表</returns>
        public static List<UInt32> GetFreeBlock(string drivename,ref VolumeInfo volumeInfo,UInt32 count)
        {
            UInt32 clustercount = (UInt32)((volumeInfo.SectorCount - 1) / (volumeInfo.Cluster>> 9));//簇总数            
            UInt32 UseBytes = (clustercount >> 3) + 1;//用多少个字节存储
            UInt32 UseCluster = UseBytes / volumeInfo.Cluster + 1;//表大小(簇)
            //读取空闲表内容
            byte[] content = DiskRW.ReadA(drivename, volumeInfo.FreeTableStart, UseCluster*(UInt32)(volumeInfo.Cluster >> 9));
            //返回的空闲块号
            List<UInt32> ls = new List<uint>();

            //i为计数,j为簇索引值
            UInt32 i = 0;
            for (UInt32 j = 0; j < clustercount; j++)
            {
                if (!Test(j, content)) {
                    ls.Add(j);
                    i++;
                }
                if (i == count) break;
            }

            //磁盘已满或没有空间返回null;
            if (ls.Count < i) return null;
            //写空闲表
            foreach (UInt32 item in ls)
            {
                Set(item, true, content);
            }
            //写回磁盘
            DiskRW.write(drivename, volumeInfo.FreeTableStart,content);
            return ls;
        }      
        /// <summary>
        /// 写回空闲表
        /// </summary>
        /// <param name="drivename"></param>
        /// <param name="volumeInfo"></param>
        /// <param name="freetable"></param>
        /// <returns></returns>
        public static ErrorCode SetFreeBlock(string drivename, ref VolumeInfo volumeInfo, byte[] freetable)
        {
            var res=DiskRW.write(drivename, volumeInfo.FreeTableStart, freetable);
            if (res)
            {
                return ErrorCode.Success;
            }
            return ErrorCode.Fail;
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
        /// 判断空闲表中某簇是否空闲
        /// </summary>
        /// <param name="cluster">簇号</param>
        /// <param name="table">空闲表引用</param>
        /// <returns>true为占用，false为空闲</returns>
        private static bool Test(UInt32 cluster,byte[] table)
        {
            UInt32 i = cluster / 8;//第几个字节
            UInt32 j = cluster % 8;//第几位

            byte res = table[i] ;
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
        /// 添加文件fat，文件为空
        /// </summary>
        /// <param name="current">当前目录信息</param>
        /// <param name="drivename">驱动器名</param>
        /// <param name="volumeinfo">当前卷信息</param>
        /// <returns>新文件簇号</returns>
        public static UInt32 AddDir(DirectInfo current, string drivename,VolumeInfo volumeinfo)
        {
            byte[] buffer = new byte[volumeinfo.Cluster];
            var ls=GetFreeBlock(drivename, ref volumeinfo, 1);//获取存放fat的簇

            //向现在目录加项
            var currentDir=DiskRW.ReadA(drivename, current.Location, (UInt16)(volumeinfo.Cluster >> 9));

            //检索当前的fat表
            for (int i = 164; i < volumeinfo.Cluster ; i+=4)
            {
                if(mergeByte(currentDir[i], currentDir[i+1], currentDir[i+2], currentDir[i + 3]) == 0)
                {
                    currentDir[i] = (byte)ls[0];
                    currentDir[i + 1] = (byte)(ls[0] >> 8);
                    currentDir[i + 2] = (byte)(ls[0] >> 16);
                    currentDir[i + 3] = (byte)(ls[0] >> 24);

                    //写回磁盘
                    DiskRW.write(drivename, current.Location, currentDir);
                    return ls[0];
                }
            }

            //fat满并且没有下个簇存放
            if (mergeByte(currentDir[160], currentDir[161], currentDir[162], currentDir[163]) == 0)
            {
                //添加下一索引块
                byte[] buffer2 = new byte[volumeinfo.Cluster];//写入内容缓存
                var l2 = GetFreeBlock(drivename, ref volumeinfo, 1);
                //写入上个的索引表
                currentDir[160] = (byte)l2[0];
                currentDir[161] = (byte)(l2[0] >> 8);
                currentDir[162] = (byte)(l2[0] >> 16);
                currentDir[163] = (byte)(l2[0] >> 24);
                //下个
                buffer2[4] = (byte)ls[0];
                buffer2[5] = (byte)(ls[0] >> 8);
                buffer2[6] = (byte)(ls[0] >> 16);
                buffer2[7] = (byte)(ls[0] >> 24);
                //写入磁盘
                WriteIn(drivename, volumeinfo, l2[0], buffer2);
                DiskRW.write(drivename, current.Location, currentDir);
                return ls[0];
            }

            //寻找下一个索引表
            currentDir = ReadOut(drivename, volumeinfo, mergeByte(currentDir[160], currentDir[161], currentDir[162], currentDir[163]));
            while (true)
            {
                for (int i = 4; i < volumeinfo.Cluster; i += 4)
                {
                    if (mergeByte(currentDir[i], currentDir[i + 1], currentDir[i + 2], currentDir[i + 3]) == 0)
                    {
                        currentDir[i] = (byte)ls[0];
                        currentDir[i + 1] = (byte)(ls[0] >> 8);
                        currentDir[i + 2] = (byte)(ls[0] >> 16);
                        currentDir[i + 3] = (byte)(ls[0] >> 24);
                        //写回磁盘
                        DiskRW.write(drivename, current.Location, currentDir);
                        return ls[0];
                    }
                    continue;
                }
                //索引表满并且没有下个簇存放
                if (mergeByte(currentDir[0], currentDir[1], currentDir[2], currentDir[3]) == 0)
                {
                    //添加下一索引块
                    byte[] buffer2 = new byte[volumeinfo.Cluster];//写入内容缓存
                    var l2 = GetFreeBlock(drivename, ref volumeinfo, 1);
                    //写入上个的索引表
                    currentDir[0] = (byte)l2[0];
                    currentDir[1] = (byte)(l2[0] >> 8);
                    currentDir[2] = (byte)(l2[0] >> 16);
                    currentDir[3] = (byte)(l2[0] >> 24);
                    //下个
                    buffer2[4] = (byte)ls[0];
                    buffer2[5] = (byte)(ls[0] >> 8);
                    buffer2[6] = (byte)(ls[0] >> 16);
                    buffer2[7] = (byte)(ls[0] >> 24);
                    //写入磁盘
                    WriteIn(drivename, volumeinfo, l2[0], buffer2);
                    DiskRW.write(drivename, current.Location, currentDir);
                    return ls[0];
                }
                currentDir = ReadOut(drivename, volumeinfo, mergeByte(currentDir[0], currentDir[1], currentDir[2], currentDir[3]));
            }
        }
        /// <summary>
        /// 获得目录下文件内容
        /// </summary>
        /// <param name="drivename"></param>
        /// <param name="location">位置（扇区）</param>
        /// <param name="fileUnit"></param>
        /// <returns></returns>
        public static List<FATInfo> GetDirContent(string drivename, UInt32 location, VolumeInfo volumeinfo)
        {
            var locations = GetList(drivename, location, volumeinfo);
            List<FATInfo> res = new List<FATInfo>();
            foreach (var loc in locations)
            {
                res.Add(GetFileInfo(drivename,
                    transfer(volumeinfo.SectorStart,loc,volumeinfo.Cluster),
                    volumeinfo.Cluster));
            }
            return res;
        }
        /// <summary>
        /// 删除实体文件
        /// </summary>
        /// <param name="drivename"></param>
        /// <param name="volumeinfo"></param>
        /// <param name="fat"></param>
        /// <returns></returns>
        public static ErrorCode DeleteFile(string drivename,VolumeInfo volumeinfo,FileInfo fat)
        {
            var content = GetList(drivename, fat.Location, volumeinfo);

            UInt32 clustercount = (UInt32)((volumeinfo.SectorCount - 1) / (volumeinfo.Cluster >> 9));//簇总数            
            UInt32 UseBytes = (clustercount >> 3) + 1;//用多少个字节存储
            UInt32 UseCluster = UseBytes / volumeinfo.Cluster + 1;//表大小(簇)
            //读取空闲表内容
            byte[] freetable = DiskRW.ReadA(drivename, volumeinfo.FreeTableStart, UseCluster * (UInt32)(volumeinfo.Cluster >> 9));

            //将位置写回空闲表
            foreach (UInt32 block in content)
            {
                Set(block, false, freetable);
            }
            //fat簇写回空闲表;
            Set(TransferInverse(volumeinfo.SectorStart, fat.Location, volumeinfo.Cluster), false, freetable);
            //保存空闲表
            var res=SetFreeBlock(drivename, ref volumeinfo, freetable);
            if (res == ErrorCode.Fail) return ErrorCode.WriteFreeTableFail;
            return ErrorCode.Success;
        }
        /// <summary>
        /// 删除目录中某项
        /// </summary>
        /// <param name="drivename">驱动器名</param>
        /// <param name="volumeinfo">卷信息结构体</param>
        /// <param name="cluster">要删的扇区</param>
        /// <param name="dirinfo">目录</param>
        /// <returns></returns>
        public static ErrorCode DeleteInDir(string drivename,VolumeInfo volumeinfo,UInt32 sector,DirectInfo dirinfo)
        {
            var cluster = TransferInverse(volumeinfo.SectorStart, sector, volumeinfo.Cluster);
            
            byte[] content = DiskRW.ReadA(drivename, dirinfo.Location, (uint)(volumeinfo.Cluster >> 9));

            UInt32 next = mergeByte(content[160], content[161], content[162], content[163]);
            for (UInt16 i = 164; i < content.Length; i += 4)
            {
                UInt32 s1 = mergeByte(content[i], content[i + 1], content[i + 2], content[i + 3]);
                if (s1 == cluster)
                {
                    content[i] = 0;
                    content[i + 1] = 0;
                    content[i + 2] = 0;
                    content[i + 3] = 0;
                    DiskRW.write(drivename, dirinfo.Location, content);
                    return ErrorCode.Success;
                }
            }

            //没有下一个索引块,找不到该目录
            if (next == 0)
            {
                return ErrorCode.NoSuchFile;
            }

            //读取索引表
            content = DiskRW.ReadA(drivename, transfer(volumeinfo.SectorStart, next, volumeinfo.Cluster), (uint)(volumeinfo.Cluster >> 9));
            while (true)
            {
                for (UInt16 i = 4; i < content.Length; i += 4)
                {
                    UInt32 s1 = mergeByte(content[i], content[i + 1], content[i + 2], content[i + 3]);
                    if (s1 == cluster)
                    {
                        content[i] = 0;
                        content[i + 1] = 0;
                        content[i + 2] = 0;
                        content[i + 3] = 0;
                        DiskRW.write(drivename, next, content);
                        return ErrorCode.Success;
                    }
                }
                next = mergeByte(content[0], content[1], content[2], content[3]);
                //下簇号 为零表示没有下一簇
                if (next == 0)
                {
                    return ErrorCode.NoSuchFile;
                }
                content = ReadOut(drivename, volumeinfo, next);
            }
        }

        /// <summary>
        /// 读取文件内容
        /// </summary>
        /// <param name="drivename"></param>
        /// <param name="volumeinfo"></param>
        /// <param name="fat"></param>
        /// <returns></returns>
        public static List<byte> ReadFile(string drivename,VolumeInfo volumeinfo,FileInfo fat)
        {
            var res = new List<byte>();
            var locations = GetList(drivename, fat.Location, volumeinfo);

            //添加内容
            for (int i = 0; i < (locations.Count-1); i++)
            {
                res.AddRange(ReadOut(drivename, volumeinfo, locations[i]));
            }
            //添加最后一块内容
            var tmp=ReadOut(drivename, volumeinfo, locations[locations.Count - 1]);
            for (int i = 0; i < fat.Rest; i++)
            {
                res.Add(tmp[i]);
            }
            return res;
        }

        /// <summary>
        /// 向文件写入内容
        /// </summary>
        /// <param name="drivename">驱动器</param>
        /// <param name="volumeinfo">卷信息</param>
        /// <param name="fat">文件fat的引用</param>
        /// <param name="content">写入的内容</param>
        /// <returns></returns>
        public static ErrorCode WriteFile(string drivename,VolumeInfo volumeinfo,ref FileInfo fat,byte[] content)
        {
            //BUGS
            //先计算需要多少磁盘块，然后去空闲表申请
            UInt32 clustercount = (UInt32)(content.Length / volumeinfo.Cluster);
            UInt16 nailcount = (UInt16)(content.Length % volumeinfo.Cluster);
            if (nailcount != 0) clustercount++;//没有整除的情况

            //申请空闲块
            var blocks = GetFreeBlock(drivename, ref volumeinfo, clustercount);
            if (blocks == null) return ErrorCode.LackOfSpace;

            //fat中添加相应块
            AppendBlock(drivename,volumeinfo,fat,blocks);

            //设置尾字节长
            fat.Rest = nailcount;


            byte[] b;//缓存
            //文件总长就一块
            if (blocks.Count == 1)
            {
                b = new byte[volumeinfo.Cluster];
                content.CopyTo(b, 0);
                var res=WriteIn(drivename, volumeinfo, blocks[0], b);
                return res;
            }

            //写入 最后一块单独处理
            UInt32 j = 0;
            for (int i = 0; i < blocks.Count-1; i++,j+=volumeinfo.Cluster)
            {
                b = new byte[volumeinfo.Cluster];
                Array.Copy(content, j, b, 0, volumeinfo.Cluster);
                WriteIn(drivename, volumeinfo, blocks[i], b);
            }

            //最后一块的写入
            b = new byte[volumeinfo.Cluster];
            for (int i=0;  j< content.Length; j++,i++)
            {
                b[i] = content[j];
            }
            WriteIn(drivename, volumeinfo, blocks.Last(), b);

            return ErrorCode.Success;
        }
        /// <summary>
        /// 向某个空文件添加内容块列表
        /// </summary>
        /// <param name="drivename">驱动器</param>
        /// <param name="fat">实体文件FAT</param>
        /// <param name="blocks">内容块</param>
        /// <returns></returns>
        private static ErrorCode AppendBlock(string drivename,VolumeInfo volumeinfo,FileInfo fat,List<UInt32> blocks)
        {
            var content = new byte[volumeinfo.Cluster];
            UInt16 i1 = (ushort)(volumeinfo.Cluster - 163);//fat簇内可存放多少簇索引
            ///<summary>
            ///总共两种情况
            ///1.fat表即可放入所有索引
            ///2.fat表和额外的索引表放入
            ///</summary>

            //第一种情况 已测试
            if (blocks.Count<=i1)
            {
                //下一索引表为0
                content[160] = 0;
                content[161] = 0;
                content[162] = 0;
                content[163] = 0;

                //i是list的索引,j是content的索引
                for (int i = 0,j=164; j < volumeinfo.Cluster;j+=4)
                {
                    content[j] = (byte)blocks[i];
                    content[j+1] = (byte)(blocks[i]>>8);
                    content[j+2] = (byte)(blocks[i]>>16);
                    content[j+3] = (byte)(blocks[i]>>24);

                    i++;
                    if (i == blocks.Count) break;//写完
                }
                var res=DiskRW.write(drivename, fat.Location, content);
                if (res == false) return ErrorCode.Fail;
                return ErrorCode.Success; 
            }

            //第二种情况
            int i2= 0;//list索引
            for (int j = 164; j < volumeinfo.Cluster; i2++, j += 4)
            {
                    content[j] = (byte)blocks[i2];
                    content[j+1] = (byte)(blocks[i2] >> 8);
                    content[j+2] = (byte)(blocks[i2] >> 16);
                    content[j+3] = (byte)(blocks[i2] >> 24);
            }
            //获取的空闲块列表
            var blocklist = GetFreeBlock(drivename, ref volumeinfo, 1);
            content[160] = (byte)blocklist[0];
            content[161] = (byte)(blocklist[0]>>8);
            content[162] = (byte)(blocklist[0]>>16);
            content[163] = (byte)(blocklist[0]>>24);

            DiskRW.write(drivename, fat.Location, content);

            //var blocklist = GetFreeBlock(drivename, ref volumeinfo, 1);
            while (true)
            {
                content = new byte[volumeinfo.Cluster];
                for (int j = 4; j < volumeinfo.Cluster; j += 4)
                {
                    content[j] = (byte)blocks[i2];
                    content[j+1] = (byte)(blocks[i2] >> 8);
                    content[j+2] = (byte)(blocks[i2] >> 16);
                    content[j+3] = (byte)(blocks[i2] >> 24);

                    i2++;
                    if (i2 == blocks.Count) break;
                }
                if (i2 == blocks.Count) break;

                var tmp = blocklist[0];//暂存目前块地址
                blocklist = GetFreeBlock(drivename, ref volumeinfo, 1);
                content[0] = (byte)blocklist[0];
                content[1] = (byte)(blocklist[0] >> 8);
                content[2] = (byte)(blocklist[0] >> 16);
                content[3] = (byte)(blocklist[0] >> 24);
                WriteIn(drivename, volumeinfo,tmp, content);

            }

            WriteIn(drivename,volumeinfo,blocklist[0],content);
            return ErrorCode.Success;
        }
    }

    /// <summary>
    /// 保存卷信息
    /// </summary>
    public struct VolumeInfo
    {
        //卷名
        public string VolumeName;

        //起始扇区
        public UInt32 SectorStart;

        //不包括mbr
        public UInt32 SectorCount;

        //簇长
        public UInt16 Cluster;

        //卷标
        public byte VolumeLabel;

        //空闲表扇区
        public UInt32 FreeTableStart;

        //空闲表组织方式 0为成组链 1为位示图
        public byte FreeTableType;

        //加密方式
        public byte EncryptionType;
    }

    /// <summary>
    /// 保存文件基本信息的基类
    /// </summary>
    public class FATInfo
    {
        //位置(扇区)
        public UInt32 Location { get; set; }

        //位置(簇号)
        //public UInt32 Cluster { get; set; }

        //文件名
        public string Name { get; set; }

        //类型
        public byte Type { get; set; }

        //权限号
        public byte AccessCode { get; set; }

        //权限标记
        public byte AccessMode { get; set; }

        //作者
        public string Author { get; set; }

        //最后修改人
        public string LastEditor { get; set; }

        //创建日期
        public UInt32 CreateTime { get; set; }

        //最后修改日期
        public UInt32 LastEditTime { get; set; }

    }

    /// <summary>
    /// 目录信息
    /// </summary>
    public class DirectInfo : FATInfo
    {
        
    }

    /// <summary>
    /// 文件信息
    /// </summary>
    public class FileInfo:FATInfo
    {
        //尾字节长
        public UInt16 Rest { get; set; }

        //文件后缀名
        public string Extension { get; set; }

    }
}

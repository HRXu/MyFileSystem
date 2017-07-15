using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiskOperation;

namespace 分区助手
{
    /// <summary>
    /// 按照分区格式分区
    /// </summary>
    public class FormatDisk
    {
        /// <summary>
        /// 第一个扇区
        /// </summary>
        UInt32 firstSector { get; set; }

        /// <summary>
        /// 长度,单位扇区 包括MBR
        /// </summary>
        UInt32 length { get; set; }

        /// <summary>
        /// 分配单元大小 字节
        /// </summary>
        UInt16 fileUnit { get; set; }

        /// <summary>
        /// 卷头
        /// </summary>
        byte[] header;

        /// <summary>
        /// 根目录fat
        /// </summary>
        byte[] fatHead;

        string drivename { get; set; }

        public FormatDisk(UInt32 startsector, UInt32 sectorcount, UInt16 fileunit, string drn)
        {
            firstSector = startsector+1;//不保留扇区，保留mbr区
            length = sectorcount;
            fileUnit = fileunit;
            drivename = drn;
        }

        /// <summary>
        /// 初始化卷头块 成组链接
        /// </summary>
        private void buildHeader()
        {
            //文件名
            header = new byte[fileUnit];
            byte[] name = Encoding.Unicode.GetBytes("新加卷");
            name.CopyTo(header, 0);

            //起始簇扇区
            header[32] = (byte)firstSector;
            header[33] = (byte)(firstSector >> 8);
            header[34] = (byte)(firstSector >> 16);
            header[35] = (byte)(firstSector >> 24);

            //分区长度
            header[36] = (byte)(length);
            header[37] = (byte)(length >> 8);
            header[38] = (byte)(length >> 16);
            header[39] = (byte)(length >> 24);

            //分配单元大小 512B 的倍数
            header[40] = (byte)(fileUnit >> 9);

            //卷标 预留为空 ascii编码
            header[41] = 0;

            //空闲闲表头
            header[42] = (byte)(transfer(2));
            header[43] = (byte)(transfer(2) >> 8);
            header[44] = (byte)(transfer(2) >> 16);
            header[45] = (byte)(transfer(2) >> 24);

            header[46] = 0;//成组链接

            //加密方式 0 不加密
            header[48] = 0;
        }

        /// <summary>
        /// 初始化卷头块 位示图
        /// </summary>
        private void buildHeaderA()
        {
            //文件名
            header = new byte[fileUnit];
            byte[] name = Encoding.Unicode.GetBytes("新加卷");
            name.CopyTo(header, 0);

            //起始簇扇区
            header[32] = (byte)firstSector;
            header[33] = (byte)(firstSector >> 8);
            header[34] = (byte)(firstSector >> 16);
            header[35] = (byte)(firstSector >> 24);

            //分区长度
            header[36] = (byte)(length);
            header[37] = (byte)(length >> 8);
            header[38] = (byte)(length >> 16);
            header[39] = (byte)(length >> 24);

            //分配单元大小 512B 的倍数
            header[40] = (byte)(fileUnit >> 9);

            //卷标 预留为空 ascii编码
            header[41] = 0;

            //空闲闲表头
            header[42] = (byte)(transfer(2));
            header[43] = (byte)(transfer(2) >> 8);
            header[44] = (byte)(transfer(2) >> 16);
            header[45] = (byte)(transfer(2) >> 24);

            header[46] = 1;//位示图

            //加密方式 0 不加密
            header[48] = 0;
        }

        private void buildFatHeader()
        {
            fatHead = new byte[fileUnit];

            //目录类型
            fatHead[77] = 0;

            //权限号
            fatHead[78] = 255;

            //权限级别 最低
            fatHead[79] = 255;

            //作者修改人为空

            //创建日期空

            //修改日期为空
        }

        /// <summary>
        /// 开始格式化 成组链接
        /// </summary>
        public void build()
        {
            buildHeader();
            buildFatHeader();

            //没有错误处理
            DiskRW.write(drivename, firstSector, header);
            DiskRW.write(drivename, transfer(1), fatHead);

            //组织空闲块
            UInt32 clustercount = (UInt32)((length - 2048) / (fileUnit>>9));//簇总数            
            UInt16 count = (UInt16)(fileUnit/4-1);//每簇空闲表容量

            //cluster为组长块
            for (UInt32 cluster = 2; cluster<clustercount; cluster+=count)
            {
                byte[] buffer = new byte[fileUnit];
                UInt32 i = 1;//记录当前录到哪一块（组长块偏移） == 该组内目前有的块数+1

                //写入组长块
                UInt32 j = 2;//记录写到了几个32位数
                for (; j < (count+1); j++,i++)
                {
                    //不能填充完当前块
                    if ((i + cluster) >= clustercount)
                    {
                        buffer[0] = (byte)(i);
                        buffer[1] = (byte)((i) >> 8);
                        buffer[2] = (byte)((i) >> 16);
                        buffer[3] = (byte)((i) >> 24);
                        DiskRW.write(drivename, transfer(cluster), buffer);
                        return;
                    }
                    buffer[j*4] = (byte)(i+cluster);
                    buffer[j*4+1] = (byte)((i + cluster) >> 8);
                    buffer[j*4+2] = (byte)((i + cluster) >> 16);
                    buffer[j*4+3] = (byte)((i + cluster) >> 24);
                }
                buffer[0] = (byte)(i);
                buffer[1] = (byte)((i) >> 8);
                buffer[2] = (byte)((i) >> 16);
                buffer[3] = (byte)((i) >> 24);

                //下块地址
                buffer[4] = (byte)(cluster + count);
                buffer[5] = (byte)((cluster + count) >> 8);
                buffer[6] = (byte)((cluster + count) >> 16);
                buffer[7] = (byte)((cluster + count) >> 24);

                DiskRW.write(drivename, transfer(cluster), buffer);                
                i = 1;
            }

            return;
        }

        /// <summary>
        /// 开始格式化 位示图
        /// </summary>
        public void buildA()
        {
            buildHeaderA();
            buildFatHeader();

            //没有错误处理
            DiskRW.write(drivename, firstSector, header);
            DiskRW.write(drivename, transfer(1), fatHead);

            //组织空闲块
            UInt32 clustercount = (UInt32)((length - 1) / (fileUnit >> 9));//簇总数            
            UInt32 UseBytes = (clustercount >> 3)+1;//用多少个字节存储
            UInt32 UseCluster = UseBytes / fileUnit + 1;//表大小

            byte[] buffer = new byte[UseCluster*fileUnit];

            Set(0, true, buffer);
            Set(1, true, buffer);
            for (uint i = 2; i < UseCluster; i++)
            {
                Set(i, true, buffer);
            }
            DiskRW.write(drivename, transfer(2), buffer);
            return;
        }

        /// <summary>
        /// 设置某簇对应空闲表中某一位为1
        /// </summary>
        /// <param name="cluster">簇号</param>
        /// <param name="value">值</param>
        /// <param name="table">表</param>
        private void Set(UInt32 cluster,bool value,byte[] table)
        {
            UInt32 i = cluster / 8;//第几个字节
            UInt32 j = cluster % 8;//第几位
            if (value==true) //置1
            {
                switch (j)
                {
                    case 0:
                        table[i]|=0x01;
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
        /// 簇与逻辑扇区映射
        /// </summary>
        /// <param name="unit"></param>
        /// <returns>返回为实际扇区号</returns>
        private UInt32 transfer(UInt32 cluster)
        {
            var res = (UInt32)(firstSector + cluster * (fileUnit>>9));
            return res;
        }
    }
}

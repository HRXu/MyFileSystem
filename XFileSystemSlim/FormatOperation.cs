using DiskOperation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XFileSystemSlim
{
    //格式化为XFSS
    class FormatOperation
    {
        /// <summary>
        /// 第一个扇区
        /// </summary>
        UInt32 firstSector { get; set; }

        /// <summary>
        /// 根目录fat
        /// </summary>
        byte[] fatHead;

        string drivename { get; set; }

        public FormatOperation(UInt32 startsector, string drn)
        {
            firstSector = startsector;//不保留扇区，保留mbr区
            drivename = drn;
        }

        /// <summary>
        /// 建立文件头
        /// </summary>
        private void buildFatHeader()
        {
            fatHead = new byte[512];

            byte[] name = Encoding.Unicode.GetBytes("FIRSTPART");
            name.CopyTo(fatHead, 0);
            //目录类型
            fatHead[46] = 0;
        }

        /// <summary>
        /// 开始格式化 位示图
        /// </summary>
        public void build()
        {
            //写入根目录头
            buildFatHeader();
            DiskRW.write(drivename, transfer(0), fatHead);

            //组织空闲块
            byte[] buffer = new byte[512*2];
            Set(0, true, buffer);
            Set(1, true, buffer);
            Set(2, true, buffer);
            DiskRW.write(drivename, transfer(1), buffer);


            return;
        }

        /// <summary>
        /// 设置某簇对应空闲表中某一位为1
        /// </summary>
        /// <param name="cluster">簇号</param>
        /// <param name="value">值</param>
        /// <param name="table">表</param>
        private void Set(UInt32 cluster, bool value, byte[] table)
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
        /// 簇与逻辑扇区映射
        /// </summary>
        /// <param name="unit"></param>
        /// <returns>返回为实际扇区号</returns>
        UInt32 transfer(UInt32 cluster)
        {
            var res = firstSector + cluster;
            return res;
        }
    }
}
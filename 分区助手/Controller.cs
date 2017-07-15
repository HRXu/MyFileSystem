using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiskOperation;
using System.Collections.ObjectModel;

namespace 分区助手
{
    public class Controller
    {
        PartitionBuilder partitionBulider = new PartitionBuilder();
        public diskinfo selected=new diskinfo();
        public Partition partitionSelected { get; set; }

        public UInt64 restSize;

        public ObservableCollection<Partition> currentPartitionList = new ObservableCollection<Partition>();

        private byte[] iPL=new byte[512];
        public byte[] IPL
        {
            set { iPL = (byte[])value.Clone(); }
        }



        /// <summary>
        /// 添加分区计划
        /// </summary>
        /// <param name="Size"></param>
        /// <returns>错误号</returns>
        public int addPartition(UInt64 Size, PartitonInfo.FileSystems filesystem)
        {
            if (Size > restSize) return 1;//超出限制
            if (partitionBulider.partitionlist.Count == 4) return 2;//分区上限
            if (partitionBulider.partitionlist.Count == 0)
            {
                partitionBulider.partitionlist.Add(new PartitonInfo
                {
                    StartSector = 2048,
                    SectorCount = (uint)(Size << 11),
                    FileSystem = filesystem,
                    Status = PartitonInfo.Statusx.active
                });               
            }
            else
            {
                partitionBulider.partitionlist.Add(new PartitonInfo
                {
                    StartSector = partitionBulider.partitionlist.Last().StartSector+ partitionBulider.partitionlist.Last().SectorCount,
                    SectorCount = (uint)(Size << 11),
                    FileSystem = filesystem,
                    Status = PartitonInfo.Statusx.inactive
                });               
            }
            restSize -= Size;
            return 0;//成功添加
        }

        /// <summary>
        /// 开始分区
        /// </summary>
        /// <param name="drivename"></param>
        /// <returns></returns>
        public int start(string drivename)
        {
            if (iPL == null) return 1;
            partitionBulider.drivename = drivename;        
            
            var res=partitionBulider.InitializeMBR(iPL);
            if (res == 2) return 2;
            if (res == 1) return 3;

            partitionBulider.Build(iPL);
            return 0;
        }

        /// <summary>
        /// 清空
        /// </summary>
        public void clear()
        {
            partitionBulider.partitionlist.Clear();
            restSize = 0;
        }

        //待优化
        /// <summary>
        /// 全盘擦除
        /// </summary>
        public void erase()
        {
            UInt32 mod64 = selected.sectors-(selected.sectors >> 6);
            UInt32 mod16 = selected.sectors - (selected.sectors >> 4);
            UInt32 i = 0;
            for (; i < mod64; i+=64)
            {
                DiskRW.write(selected.diskname,i, new byte[32768]);
            }
            for (; i < mod16; i++)
            {
                DiskRW.write(selected.diskname, i, new byte[4096]);
            }
            for (;  i<selected.sectors ; i++)
            {
                DiskRW.write(selected.diskname, i, new byte[512]);
            }
        }

        public void readPartitionTable()
        {
            currentPartitionList.Clear();
            if (selected.diskname == null || selected.diskname == "") return;
            var mbr=DiskRW.read(selected.diskname, 0);
            parse(mbr);
        }
        private void parse(byte[] MBR)
        {
            for (int i = 0, j = 0x1BE;i<4 ; i++, j += 16)
            {
                var part = new Partition();

                //起始扇区偏移
                UInt32 s1 = MBR[8 + j];
                UInt32 s2 = MBR[9 + j];
                UInt32 s3 = MBR[10 + j];
                UInt32 s4 = MBR[11 + j];
                UInt32 temp = (s4 << 24) + (s3 << 16) + (s2 << 8) + s1;
                part.StartSector = temp.ToString();
                part.SS = temp;

                //长度 (扇区)
                s1 = MBR[12 + j];
                s2 = MBR[13 + j];
                s3 = MBR[14 + j];
                s4 = MBR[15 + j];
                temp = (s4 << 24) + (s3 << 16) + (s2 << 8) + s1;
                part.SC = temp;

                //长度为零说明没有这个分区
                if (temp == 0) continue;

                part.SectorCount = temp.ToString();

                currentPartitionList.Add(part);
            }
            return;
        }
    }

    /// <summary>
    /// 磁盘信息
    /// </summary>
    public class diskinfo
    {
        public string diskname { get; set; }
        public UInt32 sectors { get; set; }
        public string model { get; set; }
        public UInt32 partition { get; set; }
    }

    /// <summary>
    /// 分区信息，注意与MisakaDiskOperation之中的PartitionInfo用处不同
    /// 用与保存从MBR中读取的磁盘信息
    /// </summary>
    public class Partition
    {
        public string StartSector { get; set; }
        //起始扇区偏移
        public UInt32 SS { get; set; }
        public string SectorCount { get; set; }
        //总扇区数
        public UInt32 SC { get; set; }
    }
}

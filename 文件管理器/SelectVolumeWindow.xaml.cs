using DiskOperation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace 文件管理器
{
    /// <summary>
    /// SelectVolumeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SelectVolumeWindow : Window
    {
        ObservableCollection<diskinfo> driveList = new ObservableCollection<diskinfo>();
        ObservableCollection<Partition> currentPartitionList = new ObservableCollection<Partition>();
        public SelectVolumeWindow()
        {
            
            InitializeComponent();
            init();
            this.DriveListBox.ItemsSource = driveList;
            VolumeListBox.ItemsSource = currentPartitionList;
        }
        /// <summary>
        /// 初始化列表
        /// </summary>
        private void init()
        {
            SelectQuery query = new SelectQuery("Select * From Win32_DiskDrive");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            foreach (ManagementBaseObject disk in searcher.Get())
            {
                driveList.Add(new diskinfo
                {
                    diskname = disk["Name"] as string,
                    model = disk["Model"] as string,
                    sectors = (UInt32)((UInt64)disk["Size"] >> 9),
                    partition = (byte)((UInt32)disk["Partitions"])
                });
            }
        }

        /// <summary>
        /// 写入全局变量
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void driveListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var item = e.AddedItems[0] as diskinfo;
            Data.DriveName = item.diskname;
            //读取分区信息
            readPartitionTable();
            e.Handled = true;
        }

        void readPartitionTable()
        {
            currentPartitionList.Clear();
            byte[] MBR = DiskRW.read(Data.DriveName, 0);
            for (int i = 0, j = 0x1BE; i < 4; i++, j += 16)
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
        }

        void VolumeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = e.AddedItems[0] as Partition;
            if (VolumeListBox.SelectedIndex == 0)
            {
                Data.StartSector = item.SS;
            }
            else
            {
                Data.StartSector = item.SS + 1;
            }
        }
    }

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

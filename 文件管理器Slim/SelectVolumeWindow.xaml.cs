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
using 文件管理器Slim.Struct;

namespace 文件管理器Slim
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
        /// 选中某个驱动器
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
        /// <summary>
        /// 读取MBR分区
        /// </summary>
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

                part.SectorCount = temp;

                //长度 (扇区)
                s1 = MBR[12 + j];
                s2 = MBR[13 + j];
                s3 = MBR[14 + j];
                s4 = MBR[15 + j];
                temp = (s4 << 24) + (s3 << 16) + (s2 << 8) + s1;
                part.SectorCount = temp;

                //长度为零说明没有这个分区
                if (temp == 0) continue;
                currentPartitionList.Add(part);
            }
        }

        /// <summary>
        /// 选中某个分区
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void VolumeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = e.AddedItems[0] as Partition;
            Data.Start = item.StartSector;
            Data.Length = item.SectorCount;
        }
    }
}


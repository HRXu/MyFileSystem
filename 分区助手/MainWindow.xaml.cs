using System;
using System.Collections.ObjectModel;
using System.Management;
using System.Windows;
using DiskOperation;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;

namespace 分区助手
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        ObservableCollection<missions> missionlist = new ObservableCollection<missions>();
        ObservableCollection<diskinfo> diskList = new ObservableCollection<diskinfo>();
        Controller controller = new Controller();
        public MainWindow()
        {
            InitializeComponent();
            this.missonListView.ItemsSource = missionlist;
            this.driveListBox.ItemsSource = diskList;
            this.partitionList.ItemsSource = controller.currentPartitionList;
            init();
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
                diskList.Add(new diskinfo
                {
                    diskname = disk["Name"] as string,
                    model = disk["Model"] as string,
                    sectors = (UInt32)((UInt64)disk["Size"] >> 9),
                    partition=(byte)((UInt32)disk["Partitions"])
                });
            }
        }

        /// <summary>
        /// 选中驱动器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void driveListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var item = e.AddedItems[0] as diskinfo;
            this.diskName.Text = item.diskname;
            this.totalSize.Text = $"{ (item.sectors >> 11)}";
            this.partitionCount.Text = $"{item.partition}";
            controller.selected = item;
            controller.restSize = (item.sectors >> 11) - 1;
            missionlist.Clear();
            controller.currentPartitionList.Clear();
            controller.readPartitionTable();
            //读取分区信息
            e.Handled = true;
        }

        /// <summary>
        /// 添加分区计划，先处理好数据再传入controller
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Add(object sender, RoutedEventArgs e)
        {
            PartitonInfo.FileSystems filesystem= PartitonInfo.FileSystems.Empty;
            switch (this.fileSystemPicker.SelectedIndex)
            {
                case 0://NTFS
                    filesystem = PartitonInfo.FileSystems.NTFS;
                    break;
                case 1:
                    filesystem = PartitonInfo.FileSystems.EFI;
                    break;
                case 2:
                    filesystem = PartitonInfo.FileSystems.Empty;
                    break;
                case 3:
                    filesystem = PartitonInfo.FileSystems.MicrosoftReserved;
                    break;
                default:
                    break;
            }

            UInt64 size=0;
            try
            {
                size=Convert.ToUInt64(newSizeTextBox.Text);
            }
            catch (Exception)
            {
                this.output.Text = "分区参数有误";
            }
            if (size == 0) return;


            var res=controller.addPartition(size,filesystem);
            if (res == 1)
            {
                output.Text = "大小超出限制";return;
            }
            else if (res == 2)
            {
                output.Text = "已达分区上限";return;
            }
            else
            {
                output.Text = "添加成功";
            }
            missionlist.Add(new missions { size=newSizeTextBox.Text });
        }

        /// <summary>
        /// 载入IPL模板
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImportIPL(object sender, RoutedEventArgs e)
        {
            var openfiledialog = new OpenFileDialog();
            var res = openfiledialog.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.Cancel)
            {
                return;
            }

            Stream fs = openfiledialog.OpenFile();

            BinaryReader sr = new BinaryReader(fs);
            List<byte> ls = new List<byte>();
            try
            {
                while (true)
                {
                    ls.Add(sr.ReadByte());
                }
            }
            catch (Exception)
            {

            }
            controller.IPL = ls.ToArray();
        }

        /// <summary>
        /// 开始建立分区表
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BuildPartition(object sender, RoutedEventArgs e)
        {
            if (diskName.Text == "")
            {
                output.Text = "请先选择驱动器";
                return;
            }
            switch (controller.start(diskName.Text))
            {
                case 0:
                    output.Text = "分区完成";
                    break;
                case 1:
                    output.Text = "请先选择IPL程序文件";
                    break;
                case 2:
                    output.Text = "IPL大小错误";
                    break;
                case 3:
                    output.Text = "没有分区任务";
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 取消分区计划
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DropMission(object sender, RoutedEventArgs e)
        {
            missionlist.Clear();
            controller.clear();
        }

        /// <summary>
        /// 格式化XFSS
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Format(object sender, RoutedEventArgs e)
        {
            UInt16 unitSize=2048;
            switch (this.clusterSizePicker.SelectedIndex)
            {
                case 0://NTFS
                    unitSize =1024;
                    break;
                case 1:
                    unitSize = 2048;
                    break;
                case 2:
                    unitSize = 4096;
                    break;
                case 3:
                    unitSize = 8192;
                    break;
                default:
                    break;
            }

            if (controller.partitionSelected == null) {output.Text = "请先选择分区";return;}
            FormatDisk d;

            //第一个扇区不需要保留
            if (partitionList.SelectedIndex==0)
            {
                d = new FormatDisk(controller.partitionSelected.SS-1, controller.partitionSelected.SC, unitSize, this.diskName.Text);
            }
            else
            {
                d = new FormatDisk(controller.partitionSelected.SS, controller.partitionSelected.SC, unitSize, this.diskName.Text);
            }
            d.buildA();
        }

        private void partitionList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (controller.currentPartitionList.Count == 0) return;
            controller.partitionSelected = new Partition();
            controller.partitionSelected = e.AddedItems[0] as Partition;
        }

        private void Erase(object sender, RoutedEventArgs e)
        {
            if (diskName.Text == "")
            {
                output.Text = "请先选择驱动器";
                return;
            }
            controller.erase();
        }

        /// <summary>
        /// 格式化为XFSS
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormatXFSS_Click(object sender, RoutedEventArgs e)
        {

        }
    }

    public struct missions
    {
        public string size { get; set; }
    }
}

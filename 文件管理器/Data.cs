using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XFileSystem;

namespace 文件管理器
{
    /// <summary>
    /// 全局变量
    /// </summary>
    public static class Data
    {
        public static VolumeInfo Volume { get; set; }
        public static string DriveName { get; set; }
        public static UInt32 StartSector { get;set;}
        public static UInt32 SectorCount { get; set; }// 分区扇区个数 包括mbr

        /// <summary>
        /// 显示的文件目录列表
        /// </summary>
        public static ObservableCollection<DisplayContent> list = new ObservableCollection<DisplayContent>();

        /// <summary>
        /// 当前文件夹信息
        /// </summary>
        public static DirectInfo current = new DirectInfo();

        /// <summary>
        /// 导航栈
        /// </summary>
        public static Stack<DirectInfo> naviStack = new Stack<DirectInfo>();

        public enum Type: byte
        {
            Director=0,
            File=1,
            Text=2,
            Sound=3,
            Image=4,
        }
    }
}

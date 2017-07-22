using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace 文件管理器Slim.Struct
{
    public class Partition
    {
           //起始扇区偏移
           public UInt32 StartSector { get; set; }
           //总扇区数
           public UInt32 SectorCount { get; set; }
    }
}

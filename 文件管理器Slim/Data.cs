using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace 文件管理器Slim
{
    /// <summary>
    /// 临时存储信息，只供中转使用
    /// </summary>
    static class Data
    {
        //所选驱动器名
        static public string DriveName{ get; set; }
        //起始扇区号
        static public UInt32 Start { get; set; }
        //长度
        static public UInt32 Length { get; set; }
    }
}

﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Hawk.Core.Connectors;
using Hawk.Core.Utils.Plugins;

namespace Hawk.ETL.Plugins.Transformers
{
    [XFrmWork("合并多列","将多个列组合成同一列" )]
    public class MergeTF : TransformerBase
    {
        public MergeTF()
        {
            MergeWith = "";
            Format = "";


        }

        [DisplayName("其他项")]
        [Description("写入多个列名，中间使用空格分割")]
        public string MergeWith { get; set; }

        [Description("形如'http:\\{0}:{1},{2}...'本列的序号为0，之后分别为1,2,3..")]
        public string Format { get; set; }
 
        public override object TransformData(IFreeDocument datas)
        {
            object item = datas[Column];
            if (item == null)
                item = "";
            List<object> strs = new List<object>();
            strs.Add(item);
            var Columns = MergeWith.Split(new string[]{" "},StringSplitOptions.RemoveEmptyEntries);
            strs.AddRange(Columns.Select(Column => datas[Column]));
            return string.Format(Format, strs.ToArray());
        }
    }
}

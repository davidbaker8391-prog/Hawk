﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls.WpfPropertyGrid.Attributes;
using Hawk.Core.Connectors;
using Hawk.Core.Utils;
using Hawk.Core.Utils.Plugins;
using Hawk.ETL.Interfaces;
using Hawk.ETL.Managements;
using Hawk.ETL.Plugins.Transformers;
using Hawk.ETL.Process;

namespace Hawk.ETL.Plugins.Generators
{
    public class ETLBase : IColumnProcess
    {
        protected readonly IProcessManager processManager;
        private string _etlSelector;
        protected bool IsExecute;
        private SmartETLTool mainstream;

        public ETLBase()
        {
            processManager = MainDescription.MainFrm.PluginDictionary["模块管理"] as IProcessManager;
        }

        [DisplayName("子流-选择")]
        [Description("输入ETL的任务名称")]
        public string ETLSelector
        {
            get { return _etlSelector; }
            set
            {
                if (_etlSelector != value)
                {
                }
                _etlSelector = value;
            }
        }

        protected SmartETLTool etl { get; set; }

        public void SetExecute(bool value)
        {
            IsExecute = value;
        }

        public virtual FreeDocument DictSerialize(Scenario scenario = Scenario.Database)
        {
            var dict = this.UnsafeDictSerialize();
            dict.Add("Type", GetType().Name);
            dict.Add("Group", "Transformer");
            return dict;
        }

        public virtual void DictDeserialize(IDictionary<string, object> docu, Scenario scenario = Scenario.Database)
        {
            this.UnsafeDictDeserialize(docu);
        }

        [Category("1.基本选项"), PropertyOrder(1), DisplayName("原列名")]
        public string Column { get; set; }

        [Category("1.基本选项")]
        [DisplayName("模块名")]
        public string Name { get; set; }

        [Category("1.基本选项")]
        [DisplayName("启用")]
        [PropertyOrder(5)]
        public bool Enabled { get; set; }

        [Browsable(false)]
        public string TypeName
        {
            get
            {
                var item = AttributeHelper.GetCustomAttribute(GetType());
                if (item == null)
                    return GetType().ToString();
                return item.Name;
            }
        }

        public void Finish()
        {
        }

        public virtual bool Init(IEnumerable<IFreeDocument> datas)
        {

            if (string.IsNullOrEmpty(ETLSelector))
                return false;
            mainstream =
                processManager.CurrentProcessCollections.OfType<SmartETLTool>()
                    .FirstOrDefault(d => d.CurrentETLTools.Contains(this));
            etl =
                processManager.CurrentProcessCollections.FirstOrDefault(d => d.Name == ETLSelector) as SmartETLTool;
            if (mainstream!=null&&mainstream.Name == this.Name)
                throw new Exception("子流程不能调用自身，否则会引起循环调用");
            if (etl != null)
            {
                return true;
            }

            var task = processManager.CurrentProject.Tasks.FirstOrDefault(d => d.Name == ETLSelector);
            if (task == null)
                return false;

            ControlExtended.UIInvoke(() => { task.Load(false); });

            etl =
                processManager.CurrentProcessCollections.FirstOrDefault(d => d.Name == ETLSelector) as SmartETLTool;

         
            etl.InitProcess(true);
            return etl != null;
        }

        public override string ToString()
        {
            return TypeName + " " + Column;
        }

        protected IEnumerable<IColumnProcess> GetProcesses()
        {
            var processes = new List<IColumnProcess>();


            foreach (var tool in etl.CurrentETLTools)
            {
                
                    processes.Add(tool);
            }
            return processes;
        }
    }

    [XFrmWork("子流-生成", "从其他数据清洗模块中生成序列，用以组合大模块")]
    public class EtlGE : ETLBase, IColumnGenerator
    {
        [Description("当前位置")]
        public int Position { get; set; }

        [DisplayName("生成模式")]
        public MergeType MergeType { get; set; }

        public IEnumerable<FreeDocument> Generate(IFreeDocument document = null)
        {
            var process = GetProcesses();

            foreach (var item in etl.Generate(process, IsExecute).Select(d => d as FreeDocument))
            {
                yield return item;
            }
        }

        public int? GenerateCount()
        {
            return null;
        }
    }


    [XFrmWork("子流-执行", "从其他数据清洗模块中生成序列，用以组合大模块")]
    public class EtlEX : ETLBase, IDataExecutor
    {
        private EnumerableFunc func;

        [DisplayName("添加到任务")]
        [Description("勾选后，本子任务会添加到任务管理器中")]
        public bool AddTask { get; set; }

        [DisplayName("新列名")]
        [Description("从原始数据中传递到子执行流的列，多个列用空格分割")]
        public string NewColumn { get; set; }

        public override bool Init(IEnumerable<IFreeDocument> datas)
        {
            base.Init(datas);
            var process = GetProcesses().ToList();
            func = etl.Aggregate(d => d, process, true);
            return true;
        }

        public IEnumerable<IFreeDocument> Execute(IEnumerable<IFreeDocument> documents)
        {
            foreach (var document in documents)
            {
                IFreeDocument doc = null;
                if (string.IsNullOrEmpty(NewColumn))
                {
                    doc = document.Clone();
                }
                else
                {
                    doc = new FreeDocument();
                    doc.MergeQuery(document, NewColumn + " " + Column);
                }
                if (AddTask)
                {
                    var name = doc[Column];
                    ControlExtended.UIInvoke(() =>
                    {
                        var task = TemporaryTask.AddTempTask("ETL" + name, func(new List<IFreeDocument> {doc}),
                            d => d.ToList());
                        processManager.CurrentProcessTasks.Add(task);
                    });
                }
                else
                {
                    var r = func(new List<IFreeDocument> {doc}).ToList();
                }

                yield return document;
            }
        }
    }

    [XFrmWork("字典转换", "将两列数据，转换为一行数据，拖入的列为key")]
    public class DictTF : TransformerBase
    {
        [DisplayName("值列名")]
        public string ValueColumn { get; set; }

        public override bool Init(IEnumerable<IFreeDocument> docus)
        {
            IsMultiYield = true;
            return base.Init(docus);
        }

        public override IEnumerable<IFreeDocument> TransformManyData(IEnumerable<IFreeDocument> datas)
        {
            if (string.IsNullOrEmpty(Column) || string.IsNullOrEmpty(ValueColumn))
            {
                foreach (var data in datas)
                {
                    yield return data;
                }
                yield break;
            }
            var result = new FreeDocument();
            foreach (var data in datas)
            {
                var key = data[Column]?.ToString();
                var value = data[ValueColumn]?.ToString();

                if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(value))
                {
                    yield return result.Clone();
                }
                else
                {
                    result.SetValue(key, value);
                }
            }
        }
    }

    [XFrmWork("子流-转换", "从其他数据清洗模块中生成序列，用以组合大模块")]
    public class EtlTF : ETLBase, IColumnDataTransformer
    {
        private EnumerableFunc func;
        private IEnumerable<IColumnProcess> process;

        [DisplayName("新列名")]
        [Description("从原始数据中传递到子执行流的列，多个列用空格分割")]
        public string NewColumn { get; set; }

        [Browsable(false)]
        public bool OneOutput => false;

        public override bool Init(IEnumerable<IFreeDocument> datas)
        {
            base.Init(datas);
            process = GetProcesses();
            func = etl.Aggregate(d => d, process, IsExecute);
            return true;
        }

        public object TransformData(IFreeDocument data)
        {
            var result = func(new List<IFreeDocument> {data.Clone()}).FirstOrDefault();
            data.AddRange(result);
            return null;
        }
        [DisplayName("返回多个数据")]
        public bool IsMultiYield { get; set; }

        public IEnumerable<IFreeDocument> TransformManyData(IEnumerable<IFreeDocument> datas)
        {
            foreach (var data in datas)
            {
                var result = etl.Generate(process, IsExecute, new List<IFreeDocument> {data.Clone()});
                foreach (var item in result)
                {
                    yield return item.MergeQuery(data, NewColumn);
                }
            }
        }
    }
}
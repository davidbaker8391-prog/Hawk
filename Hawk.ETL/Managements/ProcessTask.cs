﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Controls.WpfPropertyGrid.Attributes;
using System.Xml.Serialization;
using Hawk.Core.Utils;
using Hawk.Core.Utils.Logs;
using Hawk.Core.Utils.Plugins;
using IronPython.Hosting;

namespace Hawk.ETL.Managements
{
    public class ProcessTask : TaskBase, IDictionarySerializable
    {
        #region Constructors and Destructors

        public ProcessTask()
        {
            ProcessToDo = new FreeDocument();
        }

        #endregion

        #region Properties

        /// <summary>
        ///     要实现的算法和对应的配置
        /// </summary>
        [XmlIgnore]
        [Browsable(false)]
        public FreeDocument ProcessToDo { get; set; }

        #endregion

        public virtual FreeDocument DictSerialize(Scenario scenario = Scenario.Database)
        {
            var docu = ProcessToDo.DictSerialize(scenario);
            docu.SetValue("CreateTime", CreateTime);
            docu.SetValue("Name", Name);
            docu.SetValue("Description", Description);
            docu.SetValue("ScriptPath", ScriptPath);
            return docu;
        }

        public virtual void DictDeserialize(IDictionary<string, object> docu, Scenario scenario = Scenario.Database)
        {
            ProcessToDo.DictDeserialize(docu, scenario);
            Name = docu.Set("Name", Name);
            Description = docu.Set("Description", Description);
            CreateTime = docu.Set("CreateTime", CreateTime);
            ScriptPath = docu.Set("ScriptPath", ScriptPath);
        }

        #region Constants and Fields

        #endregion

        #region Public Methods

        private void EvalScript()
        {
            if (!File.Exists(ScriptPath))
                return;
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();

            try
            {
                var source = engine.CreateScriptSourceFromFile(ScriptPath);
                var compiledCode = source.Compile();
                foreach (var process in ProcessManager.CurrentProcessCollections)
                {
                    scope.SetVariable(process.Name, process);
                }
                dynamic d;

                d = compiledCode.Execute(scope);
            }
            catch (Exception ex)
            {
                XLogSys.Print.Error(ex);
            }
        }

        public virtual void Load()
        {
            if (
                (ProcessManager.CurrentProcessTasks.FirstOrDefault(d => d == this) == null).SafeCheck("不能重复加载该任务") ==
                false)
                return;
            ControlExtended.SafeInvoke(() =>
            {
                var processname = ProcessToDo["Type"].ToString();
                if (string.IsNullOrEmpty(processname))
                    return;
                var process = ProcessManager.GetOneInstance(processname, newOne: true);
                ProcessToDo.DictCopyTo(process as IDictionarySerializable);
                process.Init();
                EvalScript();
            }, LogType.Important, $"加载{Name}任务", true);
        }

        [DisplayName("脚本路径")]
        [PropertyOrder(6)]
        public string ScriptPath { get; set; }

        #endregion
    }
}
﻿using System;
using Hawk.Core.Utils;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AvalonDock.Layout;
using Hawk.Core.Utils;
using Hawk.Core.Utils.Logs;
using Hawk.Core.Utils.MVVM;
using AutoUpdaterDotNET;
using Hawk.Core.Utils.Plugins;
using Hawk.ETL.Controls;
using Hawk.ETL.Interfaces;
using Hawk.ETL.Managements;
using log4net.Config;
using ToastNotifications;
using ToastNotifications.Core;
using ToastNotifications.Lifetime;
using ToastNotifications.Messages;
using ToastNotifications.Position;
using Path = System.IO.Path;

namespace Hawk
{

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary
    public partial class MainWindow : Window, IMainFrm, IDockableManager,INotifyPropertyChanged
    {
        public Dictionary<string, IXPlugin> PluginDictionary { get; set; }
        public event EventHandler<ProgramEventArgs> ProgramEvent;
        public void InvokeProgramEvent(ProgramEventArgs e)
        {
            
        }

        public MainWindow()
        {

#if !DEBUG
            //     try
            {
#endif
            InitializeComponent();
            MainDescription.MainFrm = this;


            this.notifier = new Notifier(cfg =>
            {
                cfg.PositionProvider = new WindowPositionProvider(
                    parentWindow: Application.Current.MainWindow,
                    corner: Corner.TopRight,
                    offsetX: 10,
                    offsetY: 10);

                cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
                    notificationLifetime: TimeSpan.FromSeconds(3),
                    maximumNotificationCount: MaximumNotificationCount.FromCount(5));

                cfg.Dispatcher = Application.Current.Dispatcher;
            });
                ToolTipService.ShowDurationProperty.OverrideMetadata(
                    typeof(DependencyObject), new FrameworkPropertyMetadata(60000));
                Application.Current.Resources["ThemeDictionary"] = new ResourceDictionary();
            //   this.SetCurrentTheme("ShinyBlue");
        ;
            if (ConfigurationManager.AppSettings["PluginLocationRelative"] == "true")
            {
                pluginPosition = MainStartUpLocation
                                 + ConfigurationManager.AppSettings["MainPluginLocation"];
            }
            else
            {
                pluginPosition = ConfigurationManager.AppSettings["MainPluginLocation"];
            }

            XmlConfigurator.Configure(new FileInfo("log4net.config"));
       
            string icon = ConfigurationManager.AppSettings["Icon"];
            try
            {
                Icon = new BitmapImage(new Uri(pluginPosition + icon, UriKind.Absolute));
            }
            catch (Exception ex)
            {
                XLogSys.Print.Error(GlobalHelper.Get("IconNotExist"));

            }
          
            PluginManager = new PluginManager();
#if !DEBUG
            Dispatcher.UnhandledException += (s, e) =>
            {

                if (MessageBox.Show(GlobalHelper.Get("key_0"), GlobalHelper.Get("key_1"), MessageBoxButton.YesNoCancel) ==
                    MessageBoxResult.Yes)
                {
                    dynamic process = PluginDictionary["DataProcessManager"];
                    process.SaveCurrentTasks();
                }

                MessageBox.Show(GlobalHelper.Get("key_2") + e.Exception);
                XLogSys.Print.Fatal(e.Exception);
            };
#endif
            AppHelper.LoadLanguage();
            ViewDictionary = new List<ViewItem>();
            Title = ConfigurationManager.AppSettings["Title"];

            //    this.myDebugSystemUI.MainFrmUI = this;
            PluginManager.MainFrmUI = this;

            //  this.myDebugSystemUI.Init();

            PluginManager.Init(new[] { MainStartUpLocation });


            PluginManager.LoadPlugins();
            PluginManager.LoadView();

            DataContext = this;
            foreach (ICommand action in CommandCollection.Concat(Commands))
            {
                SetCommandKeyBinding(action);
            }
            XLogSys.Print.Info(Title +GlobalHelper.Get("Start"));

  
            AutoUpdater.Start("https://raw.githubusercontent.com/ferventdesert/Hawk/global/Hawk/autoupdate.xml");
            Closing += (s, e) =>
            {
                List<IDataProcess> revisedTasks;
                var processmanager = PluginDictionary["DataProcessManager"] as DataProcessManager;
                revisedTasks = processmanager.GetRevisedTasks().ToList();
                if (!revisedTasks.Any())
                {
                    if (MessageBox.Show(GlobalHelper.Get("Closing"), GlobalHelper.Get("Tips"), MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        PluginManager.Close();
                        PluginManager.SaveConfigFile();
                        Process.GetCurrentProcess().Kill();
                    }
                    else
                    {
                        e.Cancel = true;
                    }
                }
                else
                {

                    var result =
                        MessageBox.Show(GlobalHelper.FormatArgs(
                            "RemaindSave", " ".Join(revisedTasks.Select(d => d.Name).ToArray())), GlobalHelper.Get("Tips"),MessageBoxButton.YesNoCancel);
                    if(result==MessageBoxResult.Yes || result==MessageBoxResult.No)
                    {
                        if (result == MessageBoxResult.Yes)
                        {
                            revisedTasks.Execute(d => processmanager.SaveTask(d, false));

                        }
                        PluginManager.Close();
                        PluginManager.SaveConfigFile();
                        Process.GetCurrentProcess().Kill();
                    }
                    else
                    {
                        e.Cancel = true;
                    }
                }
              
            };
           // var md = ETLHelper.GetAllMarkdownDoc();
           // File.WriteAllText("HawkDoc.md", md);
            //  TestCode();
#if !DEBUG
            }


            // catch (Exception ex)
            {
                //   MessageBox.Show(ex.ToString());
            }
#endif
        }

        private Notifier notifier;
   

        public ObservableCollection<IAction> CommandCollection { get; set; }
        private void SetCommandKeyBinding(ICommand command)
        {
            var com = command as Command;
            if (com != null && com.Key != null)
            {
                InputBindings.Add(new KeyBinding
                {
                    Command = com,
                    Key = com.Key.Value,
                    Modifiers = com.Modifiers
                });
            }
            var action = command as IAction;
            if (action != null)
                foreach (ICommand childAction in action.ChildActions)
                {
                    SetCommandKeyBinding(childAction);
                }
        }


        public ReadOnlyCollection<ICommand> Commands
        {
            get
            {
                return CommandBuilder.GetCommands(
                    this,
                    new[]
                    {
                        new Command(GlobalHelper.Get("DataMgmt"), obj => ActiveThisContent(GlobalHelper.Get("DataMgmt"))) ,
                        new Command(GlobalHelper.Get("ModuleMgmt"), obj => ActiveThisContent(GlobalHelper.Get("ModuleMgmt"))) 
                    });
            }
        }

        #region Public Properties
        protected PluginManager PluginManager;
        private string pluginPosition;

        public string MainStartUpLocation => Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

        public  string MainPluginLocation => pluginPosition;

        #endregion
        public List<ViewItem> ViewDictionary { get; private set; }

        public  void ActiveThisContent(string name)
        {
            ViewItem view = ViewDictionary.FirstOrDefault(d => d.Name == name);

            var item = view?.Container as LayoutAnchorable;
            if (item == null)
                return;
            item.Show();
            item.IsActive = true;
        }

        // Private Methods (2) 

        public  void ActiveThisContent(object rc)
        {
            ViewItem view = ViewDictionary.FirstOrDefault(d => d.View == rc);

            var item = view?.Container as LayoutAnchorable;
            if (item == null)
                return;
            item.IsActive = true;
        }

        public  void AddDockAbleContent(FrmState thisState, object thisControl, params string[] objects)
        {
            AddDockAbleContent(thisState, thisControl, objects[0]);
        }


   

        public void RemoveDockableContent(object model)
        {
            ViewItem view2 = ViewDictionary.FirstOrDefault(d => d.Model == model);

            var item = view2?.Container as LayoutAnchorable;
            if (item == null)
                return;
            item.Close();
            ViewDictionary.Remove(view2);
        }

        private LayoutAnchorable Factory(string name, object content)
        {
            var layout = new LayoutAnchorable { Title = name, Content = content };
            layout.Hiding +=
                (s, e) => OnDockManagerUserChanged(new DockChangedEventArgs(DockChangedType.Remove, content));
            layout.Closing +=
                (s, e) => OnDockManagerUserChanged(new DockChangedEventArgs(DockChangedType.Remove, content));
            return layout;
        }
        public event EventHandler<DockChangedEventArgs> DockManagerUserChanged;

        public void OnDockManagerUserChanged(DockChangedEventArgs e)
        {
            EventHandler<DockChangedEventArgs> handler = this.DockManagerUserChanged;
            handler?.Invoke(this, e);
        }
        public  void AddDockAbleContent(FrmState thisState, object thisControl, string name)
        {
            string name2 = null;
            int count = ViewDictionary.Count(d => d.Name == name);
            if (count != 0)
            {
                name2 = name + count;
            }
            else
            {
                name2 = name;
            }
            var viewitem = new ViewItem(thisControl, name, thisState);
            LayoutAnchorable layout = null;
            ViewDictionary.Add(viewitem);
            try
            {
                switch (thisState)
                {
                    case FrmState.Large:
                        layout = Factory(name, thisControl);
                        documentMain.Children.Add(layout);
                      
                        layout.IsActive = true;
                        break;
                    case FrmState.Buttom:
                        layout = Factory(name, thisControl);

                        var view = thisControl as DebugManagerUI;
                        if (view != null)
                        {
                            RichTextBoxAppender.SetRichTextBox(view.richtextBox, DebugText,notifier);
                        }

                        documentButtom.Children.Add(layout);
                        documentButtom.Children.RemoveElementsNoReturn(d => d.Content == null);
                        layout.IsActive = true;
                        break;  
                    case FrmState.Middle:
                        layout = Factory(name, thisControl);
                        viewitem.Container = layout;
                        dockablePane1.Children.Add(layout);
                        dockablePane1.Children.RemoveElementsNoReturn(d=>d.Content==null);
                        layout.IsActive = true;
                        break;                
                    case FrmState.Mini:
                        layout = Factory(name, thisControl);
                        viewitem.Container = layout;
                        dockablePane2.Children.Add(layout);
                        dockablePane2.Children.RemoveElementsNoReturn(d => d.Content == null);
                        layout.IsActive = true;
                        break;
                    case FrmState.Mini2:
                        layout = Factory(name, thisControl);
                        viewitem.Container = layout;
                        dockablePane3.Children.Add(layout);
                        dockablePane3.Children.RemoveElementsNoReturn(d => d.Content == null);
                        layout.IsActive = true;
                        break;
                    case FrmState.Custom:
                        var window = new Window {Title = name};
                        window.Content = thisControl;
                        window.ShowDialog();
                        break;

                    case FrmState.Float:

                        layout = Factory(name, thisControl);

                        dockablePane1.Children.Add(layout);

                        layout.Float();
                                    
                        break;
                }
                var canNotClose= new string[] {GlobalHelper.Get("ModuleMgmt"),GlobalHelper.Get("SysState"),GlobalHelper.Get("DebugView")};
                if (canNotClose.Contains(name))
                    if (layout != null) layout.CanClose = false;
                viewitem.Container = layout;
            }
            catch (Exception ex)
            {
                XLogSys.Print.ErrorFormat("{0}{1},{2}",GlobalHelper.Get("ControlLoad"),GlobalHelper.Get("Error") , ex.Message);
            }
        }

        public  void SetBusy(bool isBusyValue, string title = null, string message =null,
            int percent = -1)
        {
            if (title == null)
                title=GlobalHelper.Get("key_3");
            if (message == null)
                message = GlobalHelper.Get("LongTask");
            BusyIndicator.IsBusy = isBusyValue;

            BusyIndicator.BusyContent = message;

            ProgressBar.Value = 100;
            ProgressBar.IsIndeterminate = isBusyValue;
        }
        private void DebugText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ActiveThisContent(GlobalHelper.Get("key_4"));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

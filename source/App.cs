// SPDX-License-Identifier: AGPL-3.0-only
// BitGlance, created 2026-09-30. Desktop interaction references PayDance.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

[assembly:AssemblyTitle("BitGlance · 比特一瞥")]
[assembly:AssemblyDescription("BTC ETH SOL USDT perpetual desktop price widget")]
[assembly:AssemblyVersion("1.1.0.0")]
[assembly:AssemblyCopyright("BitGlance contributors; window placement based on PayDance © 2026 Mr.Baoboer")]
namespace BitGlance {
 public sealed class Preferences {
  public int Interval {get;set;}
  public int Opacity {get;set;}
  public string Symbol {get;set;}
  public bool ShowChange {get;set;}
  public bool ShowUnit {get;set;}
  public bool Dark {get;set;}
  public bool Pinned {get;set;}
  public bool Mini {get;set;}
  public SavedPosition MainPosition {get;set;}
  public SavedPosition MiniPosition {get;set;}
  public Preferences(){Interval=15;Opacity=95;Symbol="BTCUSDT";ShowChange=true;ShowUnit=true;}
 }
 public static class Program {
  static Mutex mutex; static EventWaitHandle signal;
  [STAThread] public static int Main(string[] args){
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   if(args.Length==2 && args[0]=="--self-test")return Checks.Run(args[1]);
   if(args.Length==2 && args[0]=="--check-feed")return Checks.Feed(args[1]);
   bool first; mutex=new Mutex(true,"Local\\BitGlance.Desktop.v1",out first);
   signal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\BitGlance.Show.v1");
   if(!first){signal.Set();signal.Dispose();mutex.Dispose();return 0;}
   try {
    var app=new Application();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
    var desktop=new Desktop(app);
    var waiter=new Thread(()=>{while(signal.WaitOne())app.Dispatcher.BeginInvoke(new Action(desktop.ShowFull));});waiter.IsBackground=true;waiter.Start();
    app.Run(); GC.KeepAlive(desktop);return 0;
   } catch(Exception e) {
    MessageBox.Show("应用未能启动："+e.Message,"BitGlance",MessageBoxButton.OK,MessageBoxImage.Error);return 1;
   } finally {mutex.ReleaseMutex();mutex.Dispose();}
  }
 }
 public sealed class Desktop {
  readonly Application app;readonly Window window;readonly PriceChart chart=new PriceChart();readonly MarketClient client=new MarketClient();
  readonly DispatcherTimer timer=new DispatcherTimer();readonly string settingsPath;
  Preferences prefs;Forms.NotifyIcon tray;Quote quote;CancellationTokenSource cts=new CancellationTokenSource();
  DateTime next=DateTime.UtcNow,chartAt=DateTime.MinValue;bool loading,chartLoading,week,offline,exiting;int generation,chartGeneration,failures;string errorText="";
  T Find<T>(string name) where T:FrameworkElement {return (T)window.FindName(name);}
  void Text(string name,string value){Find<TextBlock>(name).Text=value;}
  Brush Brush(string key){return (Brush)window.Resources[key];}
  static readonly CultureInfo inv=CultureInfo.InvariantCulture;
  static readonly Brush green=new SolidColorBrush(Color.FromRgb(31,143,105));
  static readonly Brush red=new SolidColorBrush(Color.FromRgb(209,84,75));
  public Desktop(Application application,string isolatedSettingsPath=null){
   app=application;settingsPath=isolatedSettingsPath??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BitGlance","settings.json");prefs=Load();
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml")) window=(Window)XamlReader.Load(stream);
   app.MainWindow=window;
   Find<ContentControl>("ChartHost").Content=chart;
   Bind("CloseButton",Exit);Bind("HideButton",()=>{CapturePosition();Save();window.Hide();});
   Find<ContentControl>("PriceTarget").MouseDoubleClick+=(s,e)=>{if(e.ChangedButton==MouseButton.Left){SetMode(true);e.Handled=true;}};
   Find<ContentControl>("MiniTarget").MouseDoubleClick+=(s,e)=>{if(e.ChangedButton==MouseButton.Left){ShowFull();e.Handled=true;}};
   Bind("BtcButton",()=>SelectSymbol("BTCUSDT"));Bind("EthButton",()=>SelectSymbol("ETHUSDT"));Bind("SolButton",()=>SelectSymbol("SOLUSDT"));
   Bind("ThemeButton",()=>{prefs.Dark=!prefs.Dark;ApplyTheme();Save();});
   Bind("PinButton",()=>{prefs.Pinned=!prefs.Pinned;ApplyPinned();Save();});
   Bind("RefreshButton",()=>{if(!loading){chartAt=DateTime.MinValue;next=DateTime.UtcNow;Refresh();}});
   Bind("SettingsButton",Settings);Bind("DayButton",()=>SetRange(false));Bind("WeekButton",()=>SetRange(true));
   Find<Grid>("TitleBar").MouseLeftButtonDown+=Drag;
   Find<Grid>("MiniView").MouseLeftButtonDown+=(s,e)=>{if(prefs.Mini&&e.ClickCount==1)Drag(s,e);};
   window.KeyDown+=(s,e)=>{if(e.Key==Key.F5){chartAt=DateTime.MinValue;Refresh();}if(e.Key==Key.Escape && prefs.Mini)ShowFull();if(e.Key==Key.M && Keyboard.Modifiers==ModifierKeys.Control)SetMode(!prefs.Mini);};
   window.Closing+=(s,e)=>{CapturePosition();Save();Cleanup();};
   window.Closed+=(s,e)=>app.Shutdown();
   window.ContextMenu=MakeMenu();CreateTray();ApplyTheme();ApplyMode();ApplyPinned();
   window.Loaded+=(s,e)=>{RestorePosition();Refresh();};window.Show();
   timer.Interval=TimeSpan.FromSeconds(1);timer.Tick+=(s,e)=>{UpdateStatus();if(!loading && DateTime.UtcNow>=next)Refresh();};timer.Start();
   Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
  }
  void DisplayChanged(object s,EventArgs e){window.Dispatcher.BeginInvoke(new Action(RestorePosition));}
  void Bind(string name,Action action){Find<Button>(name).Click+=(s,e)=>action();}
  Preferences Load(){
   try {var p=new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(settingsPath));if(p==null)throw new Exception();p.Interval=new[]{5,15,30,60}.Contains(p.Interval)?p.Interval:15;p.Opacity=WindowPlacement.Opacity(p.Opacity);p.Symbol=MarketClient.IsSupported(p.Symbol)?p.Symbol:"BTCUSDT";return p;}catch{return new Preferences();}
  }
  void Save(){
   try {Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));string tmp=settingsPath+".tmp";File.WriteAllText(tmp,new JavaScriptSerializer().Serialize(prefs));if(File.Exists(settingsPath))File.Replace(tmp,settingsPath,null);else File.Move(tmp,settingsPath);}catch { }
  }
  void Drag(object sender,MouseButtonEventArgs e){
   DependencyObject obj=e.OriginalSource as DependencyObject;
   while(obj!=null){if(obj is Button)return;obj=VisualTreeHelper.GetParent(obj);}
   if(e.LeftButton==MouseButtonState.Pressed){try{window.DragMove();CapturePosition();Save();}catch(InvalidOperationException){ }e.Handled=true;}
  }
  void ApplyPinned(){window.Topmost=prefs.Mini||prefs.Pinned;Find<Button>("PinButton").Foreground=prefs.Pinned?Brush("Accent"):Brush("Muted");}
  void ApplyMode(){
   Find<Grid>("FullView").Visibility=prefs.Mini?Visibility.Collapsed:Visibility.Visible;Find<Grid>("MiniView").Visibility=prefs.Mini?Visibility.Visible:Visibility.Collapsed;
   window.ShowInTaskbar=!prefs.Mini;window.Opacity=1;
   Find<Border>("Shell").Opacity=prefs.Mini?prefs.Opacity/100.0:1;
   Find<TextBlock>("MiniUnit").Visibility=prefs.ShowUnit?Visibility.Visible:Visibility.Collapsed;
   if(!prefs.Mini){window.Width=456;window.Height=618;}else ResizeMini();
   ApplyPinned();
  }
  void ResizeMini(){
   if(!prefs.Mini)return;
   var text=new FormattedText(Find<TextBlock>("MiniPrice").Text,inv,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),24,Brush("Ink"),1);
   window.Width=Math.Max(150,Math.Ceiling(text.WidthIncludingTrailingWhitespace)+(prefs.ShowUnit?37:0)+44);
   window.Height=Find<TextBlock>("MiniStatus").Visibility==Visibility.Visible?80:61;
  }
  void SetMode(bool mini){
   if(prefs.Mini==mini)return;CapturePosition();prefs.Mini=mini;ApplyMode();window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{RestorePosition();Save();}));
  }
  public void ShowFull(){if(exiting)return;window.Show();window.WindowState=WindowState.Normal;SetMode(false);RestorePosition();window.Activate();}
  void ApplyTheme(){
   string[] keys={"Surface","Ink","Muted","Line","Soft"};string[] colors=prefs.Dark?new[]{"#1C2420","#EFF2EC","#96A49A","#354139","#29352D"}:new[]{"#FCFBF8","#242B29","#828980","#E8E9E2","#F2F3EE"};
   for(int i=0;i<keys.Length;i++)window.Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
   chart.Dark=prefs.Dark;chart.InvalidateVisual();ApplyPinned();ApplyRangeButtons();ApplyAssetButtons();UpdateQuote();
  }
  string Asset {get{return prefs.Symbol.Replace("USDT","");}}
  string PriceFormat {get{return Asset=="SOL"?"N3":"N2";}}
  void ApplyAssetButtons(){string[] ids={"BtcButton","EthButton","SolButton"};for(int i=0;i<ids.Length;i++){var b=Find<Button>(ids[i]);bool selected=prefs.Symbol==MarketClient.Symbols[i];b.Background=selected?Brush("Soft"):Brushes.Transparent;b.Foreground=selected?Brush("Accent"):Brush("Muted");}}
  void SelectSymbol(string symbol){
   if(!MarketClient.IsSupported(symbol)||prefs.Symbol==symbol)return;
   prefs.Symbol=symbol;generation++;chartGeneration++;cts.Cancel();cts.Dispose();cts=new CancellationTokenSource();quote=null;offline=false;loading=false;chartLoading=false;failures=0;chartAt=DateTime.MinValue;chart.Data.Clear();chart.InvalidateVisual();
   Text("ChartTitle","价格走势");Text("ChartStart",week?"7 天前":"24 小时前");Text("ChartEnd","现在");Find<Button>("RefreshButton").IsEnabled=true;ApplyAssetButtons();UpdateQuote();Save();next=DateTime.UtcNow;Refresh();
  }
  ContextMenu MakeMenu(){var m=new ContextMenu();Add(m,"显示主窗口",ShowFull);Add(m,"迷你悬浮窗",()=>SetMode(true));var assets=new MenuItem{Header="切换永续合约"};foreach(var symbol in MarketClient.Symbols){string selected=symbol;var item=new MenuItem{Header=symbol.Replace("USDT","")+" / USDT"};item.Click+=(s,e)=>SelectSymbol(selected);assets.Items.Add(item);}m.Items.Add(assets);Add(m,"立即刷新",()=>Refresh());Add(m,"设置",Settings);m.Items.Add(new Separator());Add(m,"退出",Exit);return m;}
  void Add(ContextMenu menu,string text,Action action){var item=new MenuItem{Header=text};item.Click+=(s,e)=>action();menu.Items.Add(item);}
  void CreateTray(){
   tray=new Forms.NotifyIcon();
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))tray.Icon=new System.Drawing.Icon(stream);
   tray.Text="比特一瞥 · BitGlance";tray.Visible=true;tray.DoubleClick+=(s,e)=>window.Dispatcher.BeginInvoke(new Action(ShowFull));
   var menu=new Forms.ContextMenuStrip();menu.Items.Add("显示主窗口",null,(s,e)=>window.Dispatcher.BeginInvoke(new Action(ShowFull)));menu.Items.Add("迷你悬浮窗",null,(s,e)=>window.Dispatcher.BeginInvoke(new Action(()=>{window.Show();SetMode(true);})));menu.Items.Add("立即刷新",null,(s,e)=>window.Dispatcher.BeginInvoke(new Action(()=>Refresh())));menu.Items.Add("退出",null,(s,e)=>window.Dispatcher.BeginInvoke(new Action(Exit)));tray.ContextMenuStrip=menu;
  }
  void ApplyRangeButtons(){Find<Button>("DayButton").Background=week?Brushes.Transparent:Brush("Soft");Find<Button>("WeekButton").Background=week?Brush("Soft"):Brushes.Transparent;Find<Button>("DayButton").Foreground=week?Brush("Muted"):Brush("Ink");Find<Button>("WeekButton").Foreground=week?Brush("Ink"):Brush("Muted");}
  void SetRange(bool value){if(week==value)return;week=value;chartGeneration++;chartLoading=false;chart.Data.Clear();chart.InvalidateVisual();chartAt=DateTime.MinValue;Text("ChartStart",week?"7 天前":"24 小时前");ApplyRangeButtons();RefreshChart();}
  async void Refresh(){
   if(loading||exiting)return;loading=true;int version=generation;var token=cts.Token;Find<Button>("RefreshButton").IsEnabled=false;UpdateStatus();RefreshChart();
   try {
    var value=await client.FetchQuote(prefs.Symbol,token);if(version!=generation||exiting)return;
    quote=value;offline=false;failures=0;errorText="";next=DateTime.UtcNow.AddSeconds(prefs.Interval);UpdateQuote();
   }catch(OperationCanceledException){if(version==generation&&!exiting){Failure("连接超时",0);}}
   catch(FeedException e){if(version==generation&&!exiting)Failure(e.Message,e.RetrySeconds);}
   catch(Exception){if(version==generation&&!exiting)Failure("无法连接行情源",0);}
   finally {if(version==generation&&!exiting){loading=false;Find<Button>("RefreshButton").IsEnabled=true;UpdateStatus();}}
  }
  void Failure(string message,int delay){offline=true;errorText=message;failures++;next=DateTime.UtcNow.AddSeconds(Math.Max(delay,Math.Min(120,prefs.Interval*Math.Pow(2,Math.Min(3,failures-1)))));UpdateQuote();}
  async void RefreshChart(){
   if(chartLoading||DateTime.UtcNow-chartAt<TimeSpan.FromSeconds(60)||exiting)return;
   chartLoading=true;int version=generation,rangeVersion=chartGeneration;bool range=week;var token=cts.Token;
   if(chart.Data.Count==0){Text("ChartMessage","正在读取真实行情…");Find<TextBlock>("ChartMessage").Visibility=Visibility.Visible;}
   try {
    var rows=await client.FetchCandles(prefs.Symbol,range,token);if(version!=generation||rangeVersion!=chartGeneration||exiting)return;
    chart.Data=rows;chartAt=DateTime.UtcNow;chart.InvalidateVisual();Find<TextBlock>("ChartMessage").Visibility=Visibility.Collapsed;
    Text("ChartTitle",week?"价格走势 · 1 小时采样":"价格走势 · 5 分钟采样");Text("ChartStart",MarketClient.Unix(rows[0].Time).ToLocalTime().ToString(week?"MM/dd":"HH:mm"));Text("ChartEnd",MarketClient.Unix(rows[rows.Count-1].Time).ToLocalTime().ToString(week?"MM/dd HH:mm":"HH:mm"));
    System.Windows.Automation.AutomationProperties.SetName(chart,Asset+" / USDT 永续价格走势图，"+rows.Count+"个数据点");
   }catch {if(version==generation&&rangeVersion==chartGeneration&&!exiting){Text("ChartMessage",chart.Data.Count==0?"走势图暂不可用 · 将自动重试":"");Find<TextBlock>("ChartMessage").Visibility=chart.Data.Count==0?Visibility.Visible:Visibility.Collapsed;Text("ChartTitle","历史曲线 · 更新失败");chartAt=DateTime.UtcNow;}}
   finally {if(version==generation&&rangeVersion==chartGeneration)chartLoading=false;}
  }
  void UpdateQuote(){
   chart.PriceFormat=PriceFormat;
   Text("PairLabel",Asset+" / USDT 永续");Text("UnitLabel","USDT");Text("MiniUnit","USDT");
   Text("AssetName",Asset=="BTC"?"Bitcoin":Asset=="ETH"?"Ethereum":"Solana");Text("AssetGlyph",Asset=="BTC"?"₿":Asset=="ETH"?"Ξ":"S");Text("VolumeCaption","24h 成交量 · "+Asset);
   Text("SourceLabel","Bitget · USDT 永续 · 最新成交价");
   Find<ContentControl>("MiniTarget").ToolTip=Asset+" / USDT 永续 · 双击展开；右键设置或切换币种";
   Text("PriceLabel",quote==null?"— —":quote.Price.ToString(PriceFormat,inv));Text("MiniPrice",quote==null?"— —":quote.Price.ToString(PriceFormat,inv));
   bool stats=quote!=null;string change=stats?(quote.Change>=0?"+":"")+quote.Change.ToString("F2",inv)+"%":"等待行情";
   Text("ChangeLabel",change);Text("ChangeCaption","过去 24 小时");
   Find<TextBlock>("ChangeLabel").Foreground=stats?(quote.Change>=0?green:red):Brush("Muted");
   Find<Border>("ChangePill").Background=stats?new SolidColorBrush(prefs.Dark?Color.FromRgb(39,56,46):Color.FromRgb(233,242,234)):Brush("Soft");
   Text("HighLabel",stats?quote.High.ToString(PriceFormat,inv):"—");Text("LowLabel",stats?quote.Low.ToString(PriceFormat,inv):"—");Text("VolumeLabel",stats?quote.Volume.ToString("N0",inv):"—");
   if(tray!=null)tray.Text=quote==null?Asset+" 永续 · 连接中":Asset+" 永续 "+quote.Price.ToString(PriceFormat,inv)+" USDT"+(offline?" · 离线":"");
   UpdateStatus();
  }
  void UpdateStatus(){
   bool stale=quote!=null&&quote.IsStale(DateTime.UtcNow);int seconds=Math.Max(0,(int)Math.Ceiling((next-DateTime.UtcNow).TotalSeconds));
   string last=quote==null?"":quote.ReceivedUtc.ToLocalTime().ToString("HH:mm:ss");
   Text("LiveLabel",offline?"离线":stale?"数据延迟":quote==null?"连接中":prefs.Interval+" 秒刷新");
   Find<System.Windows.Shapes.Ellipse>("StatusDot").Fill=(offline||stale)?red:quote==null?Brush("Accent"):green;
   string status=offline?errorText+" · "+seconds+" 秒后重试":loading?"正在更新…":quote==null?"首次连接中…":"更新于 "+last+" · "+seconds+" 秒后刷新";
   if(offline && quote!=null)status="离线 · 保留 "+last+" 价格 · "+seconds+" 秒后重试";
   if(stale&&!offline)status="行情时间 "+quote.MarketUtc.ToLocalTime().ToString("MM/dd HH:mm")+" · 数据已延迟";
   Text("UpdateLabel",status);Find<TextBlock>("UpdateLabel").ToolTip=errorText;
   string mini=offline?"离线 · 自动重试":stale?"行情已延迟":quote==null?"正在获取价格":(quote.Change>=0?"+":"")+quote.Change.ToString("F2",inv)+"% · 24h";
   Text("MiniStatus",mini);Find<TextBlock>("MiniStatus").Foreground=offline||stale?red:quote!=null?(quote.Change>=0?green:red):Brush("Muted");
   Find<TextBlock>("MiniStatus").Visibility=prefs.ShowChange||offline||stale||quote==null?Visibility.Visible:Visibility.Collapsed;
   ResizeMini();
  }
  void Settings(){
   var dialog=new Window{Title="比特一瞥 · 设置",Width=410,Height=535,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=window,Background=Brush("Surface"),FontFamily=window.FontFamily,Foreground=Brush("Ink"),ShowInTaskbar=false};
   var panel=new StackPanel{Margin=new Thickness(25)};dialog.Content=panel;
   panel.Children.Add(new TextBlock{Text="按你的习惯，看一眼价格。",FontSize=18,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,18)});
   panel.Children.Add(new TextBlock{Text="USDT 永续合约 · 最新成交价",Margin=new Thickness(0,0,0,6)});
   var asset=new ComboBox{Name="AssetChoice",ItemsSource=new[]{"BTC / USDT 永续","ETH / USDT 永续","SOL / USDT 永续"},SelectedIndex=Array.IndexOf(MarketClient.Symbols,prefs.Symbol),Height=29};panel.Children.Add(asset);
   panel.Children.Add(new TextBlock{Text="自动刷新间隔",Margin=new Thickness(0,17,0,6)});
   var interval=new ComboBox{Name="IntervalChoice",ItemsSource=new[]{"5 秒","15 秒","30 秒","60 秒"},SelectedIndex=Array.IndexOf(new[]{5,15,30,60},prefs.Interval),Height=29};panel.Children.Add(interval);
   var label=new TextBlock{Text="迷你窗背景不透明度  "+prefs.Opacity+"%",Margin=new Thickness(0,17,0,8)};panel.Children.Add(label);
   var slider=new Slider{Name="BackgroundOpacity",Minimum=0,Maximum=100,Value=prefs.Opacity,TickFrequency=5,IsSnapToTickEnabled=true};slider.ValueChanged+=(s,e)=>label.Text="迷你窗背景不透明度  "+(int)slider.Value+"%";panel.Children.Add(slider);
   panel.Children.Add(new TextBlock{Text="只改变背景，数字保持完全不透明。",FontSize=11,Foreground=Brush("Muted"),Margin=new Thickness(0,7,0,12)});
   var change=new CheckBox{Name="ShowChangeOption",Content="迷你窗显示 24 小时涨跌幅",IsChecked=prefs.ShowChange,Margin=new Thickness(0,0,0,10),Foreground=Brush("Ink")};panel.Children.Add(change);
   var unit=new CheckBox{Name="ShowUnitOption",Content="迷你窗显示 USDT 单位",IsChecked=prefs.ShowUnit,Margin=new Thickness(0,0,0,10),Foreground=Brush("Ink")};panel.Children.Add(unit);
   panel.Children.Add(new TextBlock{Text="双击价格切换迷你 / 主窗口，拖动数字可移动。\n右键可切换币种；断线提示始终保留。",FontSize=11,Foreground=Brush("Muted"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,13)});
   var buttons=new Grid();buttons.ColumnDefinitions.Add(new ColumnDefinition());buttons.ColumnDefinitions.Add(new ColumnDefinition());
   var about=new Button{Content="关于与开源",Height=32,Margin=new Thickness(0,0,8,0)};about.Click+=(s,e)=>About(dialog);buttons.Children.Add(about);
   var save=new Button{Content="保存设置",Height=32,Background=Brush("Accent"),Foreground=Brushes.White};Grid.SetColumn(save,1);buttons.Children.Add(save);panel.Children.Add(buttons);
   save.Click+=(s,e)=>{
    string selected=MarketClient.Symbols[asset.SelectedIndex];prefs.Interval=new[]{5,15,30,60}[interval.SelectedIndex];prefs.Opacity=(int)slider.Value;prefs.ShowChange=change.IsChecked==true;prefs.ShowUnit=unit.IsChecked==true;
    UpdateQuote();ApplyMode();Save();dialog.Close();if(selected!=prefs.Symbol)SelectSymbol(selected);else{next=DateTime.UtcNow;Refresh();}
   };
   dialog.ShowDialog();
  }
  void About(Window owner){
   MessageBox.Show(owner,"比特一瞥 BitGlance 1.1\n\n参考 PayDance 的桌面交互与窗口位置恢复实现。\n这是修改与移植版本，非 PayDance 官方产品。\n\nBased on PayDance. Copyright (C) 2026 Mr.Baoboer.\nLicensed under the GNU Affero General Public License v3.0 only.\n附加条款：legal/ADDITIONAL_TERMS.md\n\n完整源码与构建脚本位于应用旁的 source 文件夹。\n本应用不含交易功能、不需要账号。\n设置仅保存于本机 LocalAppData/BitGlance。\n\n行情来源：Bitget USDT 永续合约，最新成交价。\n上游：https://github.com/MrBaoboer/PayDance","关于与开源",MessageBoxButton.OK,MessageBoxImage.Information);
  }
  public void Exit(){if(exiting)return;CapturePosition();Save();Cleanup();app.Shutdown();}
  void Cleanup(){if(exiting)return;exiting=true;Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;timer.Stop();cts.Cancel();cts.Dispose();client.Dispose();if(tray!=null){tray.Visible=false;tray.Dispose();}}
  [StructLayout(LayoutKind.Sequential)]struct RECT{public int Left,Top,Right,Bottom;}
  [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out RECT r);
  [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
  void CapturePosition(){
   if(!window.IsLoaded||window.WindowState!=WindowState.Normal)return;RECT r;if(!GetWindowRect(new WindowInteropHelper(window).Handle,out r))return;
   var p=new SavedPosition{X=r.Left,Y=r.Top};if(!WindowPlacement.Usable(p))return;if(prefs.Mini)prefs.MiniPosition=p;else prefs.MainPosition=p;
  }
  void RestorePosition(){
   IntPtr h=new WindowInteropHelper(window).Handle;if(h==IntPtr.Zero)return;RECT r;if(!GetWindowRect(h,out r))return;
   var areas=Forms.Screen.AllScreens.OrderByDescending(s=>s.Primary).Select(s=>s.WorkingArea).ToArray();
   var saved=prefs.Mini?prefs.MiniPosition:prefs.MainPosition;
   if(saved==null){saved=prefs.Mini?new SavedPosition{X=areas[0].Right-(r.Right-r.Left)-30,Y=areas[0].Top+45}:new SavedPosition{X=areas[0].Left+(areas[0].Width-(r.Right-r.Left))/2,Y=areas[0].Top+(areas[0].Height-(r.Bottom-r.Top))/2};}
   var position=WindowPlacement.Resolve(saved,new System.Drawing.Size(r.Right-r.Left,r.Bottom-r.Top),areas);
   SetWindowPos(h,IntPtr.Zero,position.X,position.Y,0,0,0x0015);
  }
 }
}



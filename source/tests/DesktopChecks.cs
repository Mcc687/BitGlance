// SPDX-License-Identifier: AGPL-3.0-only
// In-process WPF tests with isolated settings and real public futures data.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BitGlance;
class DesktopChecks {
 static List<string> log=new List<string>();static int code=0;
 static void Check(bool value,string name){if(!value)throw new Exception(name);log.Add("PASS "+name);}
 static void Click(Window w,string name){((Button)w.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
 static void DoubleClick(Window w,string name){((Control)w.FindName(name)).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Control.MouseDoubleClickEvent});}
 static string Text(Window w,string name){return ((TextBlock)w.FindName(name)).Text;}
 static async Task WaitFor(Func<bool> predicate){for(int i=0;i<80;i++){if(predicate())return;await Task.Delay(250);}throw new Exception("Timed out waiting for UI state");}
 static void Configure(Window w,int opacity,bool change,bool unit,string screenshot=null){
  w.Dispatcher.BeginInvoke(new Action(()=>{
   var dialog=w.OwnedWindows.Cast<Window>().Single();var panel=(StackPanel)dialog.Content;
   panel.Children.OfType<Slider>().Single().Value=opacity;
   panel.Children.OfType<CheckBox>().Single(c=>c.Name=="ShowChangeOption").IsChecked=change;
   panel.Children.OfType<CheckBox>().Single(c=>c.Name=="ShowUnitOption").IsChecked=unit;
   if(screenshot!=null)Render(dialog,screenshot);
   panel.Children.OfType<Grid>().Single().Children.OfType<Button>().Last().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  }));Click(w,"SettingsButton");
 }
 static RenderTargetBitmap Render(Window w,string file){
  w.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth*2),(int)Math.Ceiling(w.ActualHeight*2),192,192,PixelFormats.Pbgra32);bitmap.Render(w);
  if(file!=null){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(file))png.Save(f);}return bitmap;
 }
 static byte Alpha(RenderTargetBitmap bitmap,int x,int y){byte[] px=new byte[4];bitmap.CopyPixels(new Int32Rect(x,y,1,1),px,4,0);return px[3];}
 static int OpaqueDigits(Window w,RenderTargetBitmap bitmap){
  var label=(TextBlock)w.FindName("MiniPrice");var origin=label.TransformToAncestor(w).Transform(new Point(0,0));int count=0;
  for(int y=(int)(origin.Y*2);y<(int)((origin.Y+label.ActualHeight)*2);y++)for(int x=(int)(origin.X*2);x<(int)((origin.X+label.ActualWidth)*2);x++)if(Alpha(bitmap,x,y)==255)count++;
  return count;
 }
 [STAThread]static int Main(string[] args){
  ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
  string folder=args[0];Directory.CreateDirectory(folder);string config=Path.Combine(folder,"isolated-settings.json");
  var settings=new Preferences{MainPosition=new SavedPosition{X=140,Y=100},MiniPosition=new SavedPosition{X=620,Y=180},Opacity=100};
  File.WriteAllText(config,new JavaScriptSerializer().Serialize(settings));
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var desktop=new Desktop(app,config);var w=app.MainWindow;
  app.Dispatcher.BeginInvoke(new Action(async ()=>{
   try{
    Func<bool> loaded=()=>Text(w,"PriceLabel")!="— —"&&((TextBlock)w.FindName("ChartMessage")).Visibility==Visibility.Collapsed;
    await WaitFor(loaded);await Task.Delay(200);
    Check(w.Width==456&&w.ShowInTaskbar,"main window opens and uses taskbar");
    Check(Text(w,"PairLabel")=="BTC / USDT 永续"&&Text(w,"SourceLabel").Contains("最新成交价"),"BTC uses explicitly labelled USDT perpetual last price");
    Check(w.FindName("MiniButton")==null&&w.FindName("ExpandButton")==null,"old mini button and expand arrow are removed");
    Render(w,Path.Combine(folder,"main.png"));
    Click(w,"EthButton");await WaitFor(loaded);Check(Text(w,"PairLabel")=="ETH / USDT 永续"&&Text(w,"VolumeCaption").EndsWith("ETH"),"ETH tab updates quote, chart and volume unit");
    Click(w,"SolButton");await WaitFor(loaded);Check(Text(w,"PairLabel")=="SOL / USDT 永续","SOL tab loads perpetual market");Render(w,Path.Combine(folder,"sol.png"));
    Click(w,"BtcButton");Click(w,"EthButton");Click(w,"SolButton");await WaitFor(loaded);
    var displayed=(Quote)typeof(Desktop).GetField("quote",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(desktop);
    Check(displayed.Symbol=="SOLUSDT"&&Text(w,"PairLabel").StartsWith("SOL"),"rapid switching discards old in-flight contract responses");
    Click(w,"WeekButton");await WaitFor(()=>Text(w,"ChartTitle").Contains("1 小时"));Check(Text(w,"ChartTitle").Contains("1 小时"),"7d futures chart loads");
    Click(w,"DayButton");await WaitFor(()=>Text(w,"ChartTitle").Contains("5 分钟"));
    Click(w,"BtcButton");await WaitFor(loaded);
    ((ContentControl)w.FindName("PriceTarget")).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});
    Check(w.ShowInTaskbar,"single price click does not enter mini mode");
    DoubleClick(w,"PriceTarget");await Task.Delay(250);
    Check(w.Topmost&&!w.ShowInTaskbar&&w.Height==80,"double price click enters compact mini mode");
    Check(Text(w,"MiniPrice")==Text(w,"PriceLabel")&&!Text(w,"MiniStatus").Contains(":"),"mini keeps price and 24h change but removes update time");
    Render(w,Path.Combine(folder,"mini.png"));
    Configure(w,35,true,true);await Task.Delay(250);
    Check(w.Opacity==1&&((Border)w.FindName("Shell")).Opacity==0.35&&((ContentControl)w.FindName("MiniTarget")).Opacity==1,"35 percent fades background only");
    Render(w,Path.Combine(folder,"mini-35.png"));
    Configure(w,0,true,true);await Task.Delay(250);var transparent=Render(w,Path.Combine(folder,"mini-0.png"));
    Check(((Border)w.FindName("Shell")).Opacity==0&&Alpha(transparent,24,transparent.PixelHeight/2)==0,"0 percent produces a fully transparent background pixel");
    Check(OpaqueDigits(w,transparent)>100,"price digits remain fully opaque at zero background");
    Configure(w,0,false,true);await Task.Delay(150);
    Check(((TextBlock)w.FindName("MiniStatus")).Visibility==Visibility.Collapsed&&((TextBlock)w.FindName("MiniUnit")).Visibility==Visibility.Visible,"change visibility can be disabled independently");
    Configure(w,0,true,false);await Task.Delay(150);
    Check(((TextBlock)w.FindName("MiniStatus")).Visibility==Visibility.Visible&&((TextBlock)w.FindName("MiniUnit")).Visibility==Visibility.Collapsed,"USDT unit can be hidden independently");
    Configure(w,0,false,false);await Task.Delay(250);
    Check(w.Height==61&&((TextBlock)w.FindName("MiniStatus")).Visibility==Visibility.Collapsed,"hiding both fields leaves a compact number-only widget");
    Render(w,Path.Combine(folder,"number-only.png"));
    typeof(Desktop).GetMethod("Failure",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(desktop,new object[]{"测试断线",30});
    Check(((TextBlock)w.FindName("MiniStatus")).Visibility==Visibility.Visible&&Text(w,"MiniStatus").Contains("离线"),"offline warning stays visible when 24h change is hidden");
    DoubleClick(w,"MiniTarget");await Task.Delay(200);
    Check(w.Width==456&&w.ShowInTaskbar&&((Border)w.FindName("Shell")).Opacity==1,"double mini price click restores opaque main window");
    Click(w,"RefreshButton");await WaitFor(()=>!Text(w,"LiveLabel").Contains("离线"));
    Click(w,"ThemeButton");await Task.Delay(200);Check(((SolidColorBrush)w.Resources["Surface"]).Color.R==28,"dark theme still works");Render(w,Path.Combine(folder,"dark.png"));Click(w,"ThemeButton");
    Configure(w,0,false,false,Path.Combine(folder,"settings.png"));
    Click(w,"PinButton");Check(w.Topmost,"main pin button works");Click(w,"PinButton");
    Click(w,"HideButton");Check(!w.IsVisible,"hide preserves tray instance");desktop.ShowFull();await Task.Delay(100);Check(w.IsVisible,"tray recovery shows main view");
    DoubleClick(w,"PriceTarget");await Task.Delay(150);DoubleClick(w,"MiniTarget");await Task.Delay(150);
    var saved=new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(config));
    Check(saved.MainPosition!=null&&saved.MiniPosition!=null&&saved.MainPosition.X!=saved.MiniPosition.X,"main and mini positions remain independent");
    Check(saved.Opacity==0&&!saved.ShowUnit&&!saved.ShowChange&&saved.Symbol=="BTCUSDT","zero opacity and new visibility settings persist");
   }catch(Exception e){log.Add("FAIL "+e);code=1;}
   finally{File.WriteAllLines(Path.Combine(folder,"desktop-checks.txt"),log.ToArray());desktop.Exit();}
  }));
  app.Run();return code;
 }
}

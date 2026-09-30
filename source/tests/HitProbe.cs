// SPDX-License-Identifier: AGPL-3.0-only
// Queries the OS hit target; does not synthesize global input or move the cursor.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using BitGlance;
class HitProbe {
 static int exitCode;
 [StructLayout(LayoutKind.Sequential)]struct POINT {public int X,Y;}
 [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(POINT point);
 [STAThread]static int Main(string[] args){
  string folder=args[0];Directory.CreateDirectory(folder);string settings=Path.Combine(folder,"hit-settings.json");
  File.WriteAllText(settings,new JavaScriptSerializer().Serialize(new Preferences{Mini=true,Opacity=0,ShowUnit=false,Symbol="SOLUSDT",MiniPosition=new SavedPosition{X=300,Y=150}}));
  ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var desktop=new Desktop(app,settings);var w=app.MainWindow;
  app.Dispatcher.BeginInvoke(new Action(async ()=>{
   try{
    for(int i=0;i<40&&((TextBlock)w.FindName("MiniPrice")).Text=="— —";i++)await Task.Delay(250);
    var prefs=(Preferences)typeof(Desktop).GetField("prefs",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(desktop);
    var log=new List<string>();string[] labels={"0% with change","0% number only","0% dark number only","35% dark","100% dark"};
    for(int mode=0;mode<labels.Length;mode++){
     prefs.ShowChange=mode==0;prefs.Dark=mode>=2;prefs.Opacity=mode==3?35:mode==4?100:0;
     typeof(Desktop).GetMethod("ApplyTheme",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(desktop,null);
     typeof(Desktop).GetMethod("ApplyMode",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(desktop,null);
     w.UpdateLayout();await Task.Delay(350);
     var target=(FrameworkElement)w.FindName("MiniTarget");IntPtr hwnd=new WindowInteropHelper(w).Handle;int hits=0,total=0;
     for(int y=0;y<8;y++)for(int x=0;x<20;x++){
      var point=target.PointToScreen(new Point((x+0.5)*target.ActualWidth/20,(y+0.5)*target.ActualHeight/8));
      bool hit=WindowFromPoint(new POINT{X=(int)Math.Round(point.X),Y=(int)Math.Round(point.Y)})==hwnd;if(hit)hits++;total++;
     }
     log.Add((hits==total?"PASS ":"FAIL ")+labels[mode]+": native WindowFromPoint "+hits+" / "+total+" points target the widget.");if(hits!=total)exitCode=1;
    }
    log.Add("Version: "+typeof(Desktop).Assembly.GetName().Version);File.WriteAllLines(Path.Combine(folder,"hit-probe.txt"),log.ToArray());
   }catch(Exception e){exitCode=1;File.WriteAllText(Path.Combine(folder,"hit-probe.txt"),"FAIL "+e);}
   finally{desktop.Exit();}
  }));app.Run();return exitCode;
 }
}

// SPDX-License-Identifier: AGPL-3.0-only
// BitGlance, created 2026-09-30.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BitGlance {
 public sealed class PriceChart:FrameworkElement {
  public List<Candle> Data=new List<Candle>();
  public bool Dark;
  public string PriceFormat="N2";
  double hover=-1;
  static readonly Brush orange=new SolidColorBrush(Color.FromRgb(233,129,45));
  public PriceChart(){MouseMove+=(s,e)=>{hover=e.GetPosition(this).X;InvalidateVisual();};MouseLeave+=(s,e)=>{hover=-1;InvalidateVisual();};}
  protected override void OnRender(DrawingContext dc){
   base.OnRender(dc);double w=ActualWidth,h=ActualHeight;
   if(w<20||h<20)return;
   dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,w,h));
   var muted=new SolidColorBrush(Dark?Color.FromRgb(141,151,145):Color.FromRgb(137,145,137));
   var line=new Pen(new SolidColorBrush(Dark?Color.FromRgb(51,60,55):Color.FromRgb(231,233,226)),1);line.DashStyle=DashStyles.Dash;
   double top=12,bottom=h-13;
   for(int i=0;i<3;i++)dc.DrawLine(line,new Point(0,top+(bottom-top)*i/2),new Point(w,top+(bottom-top)*i/2));
   if(Data.Count<2)return;
   double min=(double)Data.Min(c=>c.Close),max=(double)Data.Max(c=>c.Close),range=Math.Max(max-min,max*0.0002);
   long t0=Data[0].Time,t1=Data[Data.Count-1].Time;
   if(t1<=t0)return;
   Func<Candle,Point> xy=c=>new Point(2+(w-4)*(c.Time-t0)/(double)(t1-t0),bottom-((double)c.Close-min)/range*(bottom-top));
   var path=new StreamGeometry();using(var g=path.Open()) {g.BeginFigure(xy(Data[0]),false,false);g.PolyLineTo(Data.Skip(1).Select(xy).ToArray(),true,false);}path.Freeze();
   var fill=new StreamGeometry();using(var g=fill.Open()){g.BeginFigure(new Point(2,h),true,true);g.LineTo(xy(Data[0]),true,false);g.PolyLineTo(Data.Skip(1).Select(xy).ToArray(),true,false);g.LineTo(new Point(w-2,h),true,false);}fill.Freeze();
   var gradient=new LinearGradientBrush(Color.FromArgb(52,233,129,45),Color.FromArgb(0,233,129,45),90);
   dc.DrawGeometry(gradient,null,fill);dc.DrawGeometry(null,new Pen(orange,1.8),path);
   var last=xy(Data[Data.Count-1]);dc.DrawEllipse(orange,null,last,3,3);
   if(hover>=0){
    long timestamp=t0+(long)(Math.Max(0,Math.Min(w,hover))/w*(t1-t0));
    var c=Data.OrderBy(v=>Math.Abs(v.Time-timestamp)).First();var p=xy(c);
    dc.DrawLine(new Pen(muted,0.7),new Point(p.X,top),new Point(p.X,bottom));dc.DrawEllipse(orange,new Pen(Dark?Brushes.Black:Brushes.White,2),p,4,4);
    string text=MarketClient.Unix(c.Time).ToLocalTime().ToString("MM/dd HH:mm")+"   "+c.Close.ToString(PriceFormat,CultureInfo.InvariantCulture);
    var label=new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,Dark?Brushes.White:Brushes.Black,VisualTreeHelper.GetDpi(this).PixelsPerDip);
    double x=Math.Max(0,Math.Min(w-label.Width-16,p.X-label.Width/2));
    dc.DrawRoundedRectangle(Dark?new SolidColorBrush(Color.FromRgb(42,49,45)):Brushes.White,new Pen(muted,0.4),new Rect(x,0,label.Width+16,23),5,5);
    dc.DrawText(label,new Point(x+8,4));
   }
  }
 }
}

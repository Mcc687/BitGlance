// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BitGlance {
 public sealed class PriceChart:FrameworkElement {
  List<Candle> data=new List<Candle>();
  public readonly ChartViewport View=new ChartViewport();
  public event Action ViewChanged;
  public List<Candle> Data {get{return data;}set{var rows=value??new List<Candle>();View.Update(data,rows);data=rows;Changed();}}
  public bool Dark,Candlesticks;
  public int IntervalMinutes=5;
  public string PriceFormat="N2",Asset="BTC";
  double hover=-1,dragX;
  bool dragging;
  static readonly CultureInfo inv=CultureInfo.InvariantCulture;
  static readonly Brush orange=ColorBrush(233,129,45),up=ColorBrush(31,143,105),down=ColorBrush(209,84,75);
  static Brush ColorBrush(byte r,byte g,byte b){var bsh=new SolidColorBrush(Color.FromRgb(r,g,b));bsh.Freeze();return bsh;}
  Brush Muted {get{return Dark?ColorBrush(150,164,154):ColorBrush(130,137,128);}}
  Brush Ink {get{return Dark?ColorBrush(239,242,236):ColorBrush(36,43,41);}}
  Brush Surface {get{return Dark?ColorBrush(28,36,32):ColorBrush(252,251,248);}}
  double PlotWidth {get{return Math.Max(10,ActualWidth-(Candlesticks?68:4));}}
  public PriceChart(){
   ClipToBounds=true;Focusable=true;
   MouseMove+=(s,e)=>{
    var x=e.GetPosition(this).X;
    if(dragging&&Candlesticks&&View.Count>0){double step=PlotWidth/View.Count;int bars=(int)((dragX-x)/step);if(bars!=0){Pan(bars);dragX-=bars*step;}hover=-1;}
    else hover=x;
    InvalidateVisual();
   };
   MouseLeave+=(s,e)=>{hover=-1;InvalidateVisual();};
   MouseLeftButtonDown+=(s,e)=>{if(!Candlesticks||data.Count==0)return;Focus();dragX=e.GetPosition(this).X;dragging=CaptureMouse();Cursor=Cursors.SizeWE;e.Handled=true;};
   MouseLeftButtonUp+=(s,e)=>{if(!dragging)return;dragging=false;ReleaseMouseCapture();Cursor=Cursors.Cross;e.Handled=true;};
   LostMouseCapture+=(s,e)=>{dragging=false;Cursor=Candlesticks?Cursors.Cross:Cursors.Arrow;};
   MouseWheel+=(s,e)=>{if(!Candlesticks)return;Zoom(e.Delta/120,(e.GetPosition(this).X-2)/PlotWidth);e.Handled=true;};
   KeyDown+=(s,e)=>{if(!Candlesticks)return;if(e.Key==Key.Left)Pan(-10);else if(e.Key==Key.Right)Pan(10);else if(e.Key==Key.Home)Latest();else if(e.Key==Key.Add||e.Key==Key.OemPlus)Zoom(1,0.5);else if(e.Key==Key.Subtract||e.Key==Key.OemMinus)Zoom(-1,0.5);else return;e.Handled=true;};
  }
  void Changed(){InvalidateVisual();if(ViewChanged!=null)ViewChanged();}
  public void Reset(){hover=-1;dragging=false;if(IsMouseCaptured)ReleaseMouseCapture();View.Reset();Changed();}
  public void Pan(int bars){View.Pan(bars);Changed();}
  public void Zoom(int steps,double anchor){View.Zoom(steps,anchor);Changed();}
  public void Latest(){View.Latest();Changed();}
  public List<Candle> Visible {get{return Candlesticks?data.Skip(View.First).Take(View.Count).ToList():data;}}
  public bool IsOpen(Candle c){DateTime now=DateTime.UtcNow,start=MarketClient.Unix(c.Time);return start<=now&&now<start.AddMinutes(IntervalMinutes);}
  public string Describe(Candle c){return MarketClient.Unix(c.Time).ToLocalTime().ToString("MM/dd HH:mm")+(IsOpen(c)?" · 未收盘":" · 已收盘")+"   量 "+c.Volume.ToString("N2",inv)+" "+Asset+"\n开 "+c.Open.ToString(PriceFormat,inv)+"    高 "+c.High.ToString(PriceFormat,inv)+"\n低 "+c.Low.ToString(PriceFormat,inv)+"    收 "+c.Close.ToString(PriceFormat,inv)+" USDT";}
  FormattedText Label(string text,double size,Brush color){return new FormattedText(text,inv,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,color,VisualTreeHelper.GetDpi(this).PixelsPerDip);}
  protected override void OnRender(DrawingContext dc){
   base.OnRender(dc);double w=ActualWidth,h=ActualHeight;if(w<30||h<30)return;
   dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,w,h));
   var grid=new Pen(Dark?ColorBrush(53,65,57):ColorBrush(232,233,226),1);grid.DashStyle=DashStyles.Dash;
   var rows=Visible;double left=2,right=left+PlotWidth,top=12,bottom=h-(Candlesticks?39:13);
   if(bottom<=top)return;
   for(int i=0;i<3;i++)dc.DrawLine(grid,new Point(left,top+(bottom-top)*i/2),new Point(right,top+(bottom-top)*i/2));
   if(rows.Count<2)return;
   double lo=(double)rows.Min(c=>Candlesticks?c.Low:c.Close),hi=(double)rows.Max(c=>Candlesticks?c.High:c.Close);
   double pad=Math.Max((hi-lo)*0.08,Math.Max(hi*0.00005,0.000001));lo-=pad;hi+=pad;
   Func<decimal,double> y=price=>bottom-((double)price-lo)/(hi-lo)*(bottom-top);
   Func<int,double> x=i=>Candlesticks?left+(i+0.5)*PlotWidth/rows.Count:left+PlotWidth*(rows[i].Time-rows[0].Time)/(double)(rows[rows.Count-1].Time-rows[0].Time);
   if(Candlesticks){
    double body=Math.Max(1,Math.Min(11,PlotWidth/rows.Count*0.66));decimal maxVolume=rows.Max(c=>c.Volume);
    for(int i=0;i<rows.Count;i++){
     var c=rows[i];var color=c.Close>=c.Open?up:down;double px=x(i),a=y(c.Open),b=y(c.Close);
     dc.DrawLine(new Pen(color,1),new Point(px,y(c.High)),new Point(px,y(c.Low)));
     dc.DrawRectangle(color,null,new Rect(px-body/2,Math.Min(a,b),body,Math.Max(1,Math.Abs(a-b))));
     double volume=maxVolume>0?24*(double)(c.Volume/maxVolume):0;
     dc.PushOpacity(0.38);dc.DrawRectangle(color,null,new Rect(px-body/2,h-3-volume,body,volume));dc.Pop();
    }
    for(int i=0;i<3;i++){
     double value=hi-(hi-lo)*i/2;var label=Label(value.ToString(PriceFormat,inv),9,Muted);label.MaxTextWidth=64;label.Trimming=TextTrimming.CharacterEllipsis;
     dc.DrawText(label,new Point(right+5,top+(bottom-top)*i/2-label.Height/2));
    }
    dc.DrawText(Label("量",9,Muted),new Point(right+5,h-23));
    if(View.Following){
     var last=rows[rows.Count-1];double py=y(last.Close);var color=last.Close>=last.Open?up:down;
     var priceLine=new Pen(color,0.65){DashStyle=DashStyles.Dot};dc.DrawLine(priceLine,new Point(left,py),new Point(right,py));
     var label=Label(last.Close.ToString(PriceFormat,inv),9,color);label.MaxTextWidth=62;label.Trimming=TextTrimming.CharacterEllipsis;
     dc.DrawRoundedRectangle(Surface,new Pen(color,0.6),new Rect(right+2,py-9,64,18),3,3);dc.DrawText(label,new Point(right+5,py-label.Height/2));
    }
   }else{
    var points=rows.Select((c,i)=>new Point(x(i),y(c.Close))).ToArray();
    var path=new StreamGeometry();using(var g=path.Open()){g.BeginFigure(points[0],false,false);g.PolyLineTo(points.Skip(1).ToArray(),true,false);}path.Freeze();
    var fill=new StreamGeometry();using(var g=fill.Open()){g.BeginFigure(new Point(left,h),true,true);g.PolyLineTo(points,true,false);g.LineTo(new Point(right,h),true,false);}fill.Freeze();
    dc.DrawGeometry(new LinearGradientBrush(Color.FromArgb(52,233,129,45),Color.FromArgb(0,233,129,45),90),null,fill);dc.DrawGeometry(null,new Pen(orange,1.8),path);dc.DrawEllipse(orange,null,points[points.Length-1],3,3);
   }
   if(hover>=left&&hover<=right&&!dragging){
    int index;
    if(Candlesticks)index=Math.Min(rows.Count-1,Math.Max(0,(int)((hover-left)/PlotWidth*rows.Count)));
    else {long t=rows[0].Time+(long)((hover-left)/PlotWidth*(rows[rows.Count-1].Time-rows[0].Time));index=rows.Select((c,i)=>new{c,i}).OrderBy(v=>Math.Abs(v.c.Time-t)).First().i;}
    var selected=rows[index];double px=x(index),py=y(selected.Close);dc.DrawLine(new Pen(Muted,0.7),new Point(px,top),new Point(px,h-3));
    dc.DrawEllipse(Candlesticks?(selected.Close>=selected.Open?up:down):orange,new Pen(Surface,1.5),new Point(px,py),3,3);
    string text=Candlesticks?Describe(selected):MarketClient.Unix(selected.Time).ToLocalTime().ToString("MM/dd HH:mm")+"   "+selected.Close.ToString(PriceFormat,inv)+" USDT";
    var label=Label(text,10,Ink);label.MaxTextWidth=w-20;label.Trimming=TextTrimming.CharacterEllipsis;
    double width=Math.Min(w,label.Width+16),tx=Math.Max(0,Math.Min(w-width,px-width/2));
    dc.DrawRoundedRectangle(Surface,new Pen(Muted,0.5),new Rect(tx,0,width,label.Height+10),5,5);dc.DrawText(label,new Point(tx+8,5));
   }
  }
 }
}

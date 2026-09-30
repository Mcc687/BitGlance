// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
namespace BitGlance {
 public static class Checks {
  static readonly List<string> results=new List<string>();
  static void Assert(bool good,string name){if(!good)throw new Exception(name);results.Add("PASS "+name);}
  static void Reject(Action action,string name){bool rejected=false;try{action();}catch(FormatException){rejected=true;}Assert(rejected,name);}
  public static int Run(string path){
   try {
    var now=MarketClient.Unix(1790738700000L);
    const string json="{\"code\":\"00000\",\"data\":[{\"symbol\":\"BTCUSDT\",\"lastPr\":\"83318.50\",\"markPrice\":\"83300\",\"change24h\":\"-0.00163\",\"high24h\":\"84563.99\",\"low24h\":\"82900\",\"baseVolume\":\"12888.6405\",\"ts\":1790738700000}]}";
    Thread.CurrentThread.CurrentCulture=new CultureInfo("de-DE");
    var q=MarketClient.ParseTicker(json,"BTCUSDT",now);
    Assert(q.Price==83318.50m && q.Change==-0.163m && q.Unit=="USDT","decimal parsing is locale-independent and keeps USDT unit");
    Assert(!q.IsStale(now.AddSeconds(119)) && q.IsStale(now.AddSeconds(121)),"old server timestamp is flagged stale");
    Reject(()=>MarketClient.ParseTicker(json.Replace("BTCUSDT","ETHUSDT"),"BTCUSDT",now),"rejects response for a different contract");
    Reject(()=>MarketClient.ParseTicker(json.Replace("83318.50","0"),"BTCUSDT",now),"rejects zero price");
    Reject(()=>MarketClient.ParseTicker(json.Replace("83318.50","NaN"),"BTCUSDT",now),"rejects non-numeric price");
    Reject(()=>MarketClient.ParseTicker(json.Replace("82900","99999"),"BTCUSDT",now),"rejects impossible high/low range");
    Reject(()=>MarketClient.ParseTicker(json,"BTCUSDT",now.AddHours(-1)),"rejects future server timestamps");
    foreach(var symbol in MarketClient.Symbols)Assert(MarketClient.ParseTicker(json.Replace("BTCUSDT",symbol),symbol,now).Symbol==symbol,"parses perpetual contract "+symbol);
    Assert(!MarketClient.IsSupported("BTCUSD")&&!MarketClient.IsSupported("DOGEUSDT"),"unsupported symbols are rejected");
    var legacy=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Preferences>("{\"Provider\":1,\"Interval\":30,\"Opacity\":30}");
    Assert(legacy.Symbol=="BTCUSDT" && legacy.ShowChange && legacy.ShowUnit && legacy.Opacity==30,"v1 settings migrate with visible fields and saved opacity");
    var candles=MarketClient.ParseCandles("[[2000,2,4,1,\"2.5\",10,30],[1000,1,3,1,\"2\",0,0],[2000,2,4,1,\"3\",11,33]]");
    Assert(candles.Count==2 && candles[0].Time==1000 && candles[1].Close==3,"chart sorts timestamps and removes duplicate candles");
    Assert(candles[1].Open==2&&candles[1].High==4&&candles[1].Low==1&&candles[1].Volume==11,"candles keep real OHLC and base-asset volume");
    const string bars="[[1000,2,3,1,2,0,0],[2000,2,3,1,2,10,20]]";
    Reject(()=>MarketClient.ParseCandles(bars.Replace("2,3,1,2","4,3,1,2")),"rejects open above candle high");
    Reject(()=>MarketClient.ParseCandles(bars.Replace("2,3,1,2","2,3,2.5,2")),"rejects low above open or close");
    Reject(()=>MarketClient.ParseCandles(bars.Replace("10,20","-10,20")),"rejects negative candle volume");
    Reject(()=>MarketClient.ParseCandles(bars.Replace("2000","1000")),"requires two distinct candle timestamps");
    Reject(()=>MarketClient.ParseCandles(bars.Replace("2000","999999999999999")),"rejects out of range candle timestamps");
    Assert(MarketClient.ParseCandles(bars)[0].Volume==0,"flat candles with no trades remain valid");
    Assert(!legacy.Candles&&legacy.ChartInterval=="5m","old settings default to line chart and 5m candles");
    Assert(MarketClient.Intervals.Select(MarketClient.IntervalMinutes).SequenceEqual(new[]{1,5,15,60}),"four supported candle periods map to correct durations");
    var history=Enumerable.Range(0,300).Select(i=>new Candle{Time=(i+1)*60000L,Open=2,High=3,Low=1,Close=2,Volume=0}).ToList();
    var view=new ChartViewport();view.Update(new List<Candle>(),history);
    Assert(view.First==240&&view.Count==60&&view.Following,"initial candle view follows latest 60 bars");
    view.Pan(-50);long anchor=history[view.First].Time;
    var rolled=history.Skip(1).Concat(new[]{new Candle{Time=301*60000L}}).ToList();view.Update(history,rolled);
    Assert(rolled[view.First].Time==anchor&&!view.Following,"polling preserves history by timestamp when oldest bar expires");
    view.Latest();view.Update(rolled,history);Assert(view.First==240&&view.Following,"live view remains attached to newest candle on refresh");
    view.Zoom(100,1);Assert(view.Count==20&&view.First==280,"zoom in stops at 20 candles and anchors latest edge");
    view.Zoom(-100,1);Assert(view.Count==160&&view.First==140,"zoom out stops at 160 candles and anchors latest edge");
    view.Pan(-9999);Assert(view.First==0&&!view.Following,"pan clamps to oldest loaded candle");
    view.Pan(9999);Assert(view.First==140&&view.Following,"pan clamps to latest loaded candle");
    view.Update(history,candles);Assert(view.First==0&&view.Count==2,"short histories never create invalid viewport indices");
    view.Update(candles,new List<Candle>());view.Zoom(1,0.5);view.Pan(-1);Assert(view.Count==0&&view.First==0,"empty chart tolerates navigation while loading");
    Reject(()=>MarketClient.ParseCandles("[]"),"rejects missing chart history");
    Assert(!WindowPlacement.Usable(new SavedPosition{X=-32000,Y=-32000}),"minimize sentinel is never saved");
    Assert(WindowPlacement.Usable(new SavedPosition{X=-1600,Y=60}),"negative coordinates on a left monitor are valid");
    var areas=new[]{new Rectangle(0,0,1920,1040),new Rectangle(-1920,0,1920,1080)};
    var p=WindowPlacement.Resolve(new SavedPosition{X=-1800,Y=100},new Size(450,600),areas);
    Assert(p.X==-1800 && p.Y==100,"restores window on secondary monitor");
    p=WindowPlacement.Resolve(new SavedPosition{X=9000,Y=9000},new Size(450,600),areas);
    Assert(p.X>=0 && p.X+450<=1920 && p.Y+600<=1040,"disconnected monitor position returns to visible work area");
    p=WindowPlacement.Resolve(new SavedPosition{X=1919,Y=1039},new Size(450,600),areas);
    Assert(p.X+450<1920 && p.Y+600<1040,"edge position keeps full window reachable");
    Assert(WindowPlacement.Opacity(-1)==0 && WindowPlacement.Opacity(0)==0 && WindowPlacement.Opacity(999)==100,"background opacity supports exactly 0 through 100 percent");
    File.WriteAllLines(path,results.ToArray());return 0;
   }catch(Exception e){results.Add("FAIL "+e);File.WriteAllLines(path,results.ToArray());return 1;}
  }
  public static int Feed(string path){
   try{using(var c=new MarketClient()){
    var lines=new List<string>();
    foreach(string symbol in MarketClient.Symbols){
     var q=c.FetchQuote(symbol,CancellationToken.None).GetAwaiter().GetResult();
     var day=c.FetchCandles(symbol,false,CancellationToken.None).GetAwaiter().GetResult();
     var week=c.FetchCandles(symbol,true,CancellationToken.None).GetAwaiter().GetResult();
     lines.Add("PASS Bitget USDT perpetual "+symbol+": "+q.Price.ToString(CultureInfo.InvariantCulture)+"; market UTC="+q.MarketUtc.ToString("o"));
     lines.Add("PASS "+symbol+" charts: "+day.Count+" 5m candles, "+week.Count+" 1h candles");
     foreach(string period in MarketClient.Intervals){var rows=c.FetchCandles(symbol,period,300,CancellationToken.None).GetAwaiter().GetResult();long step=MarketClient.IntervalMinutes(period)*60000L;if(rows.Count<60||rows.Zip(rows.Skip(1),(a,b)=>b.Time-a.Time).Any(d=>d!=step))throw new Exception("Wrong candle period: "+symbol+" "+period);lines.Add("PASS "+symbol+" "+period+": "+rows.Count+" validated OHLCV candles; spacing "+step+" ms");}
    }
    lines.Add("Fetched UTC: "+DateTime.UtcNow.ToString("o"));File.WriteAllLines(path,lines.ToArray());
   }return 0;}catch(Exception e){File.WriteAllText(path,"FAIL "+e);return 1;}
  }
 }
}


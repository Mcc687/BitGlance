// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
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
    var candles=MarketClient.ParseCandles("[[2000,1,1,1,\"2.5\",1,3],[1000,1,1,1,\"2\",1,3],[2000,1,1,1,\"3\",1,3]]");
    Assert(candles.Count==2 && candles[0].Time==1000 && candles[1].Close==3,"chart sorts timestamps and removes duplicate candles");
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
    }
    lines.Add("Fetched UTC: "+DateTime.UtcNow.ToString("o"));File.WriteAllLines(path,lines.ToArray());
   }return 0;}catch(Exception e){File.WriteAllText(path,"FAIL "+e);return 1;}
  }
 }
}


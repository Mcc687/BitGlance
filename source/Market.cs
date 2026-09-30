// SPDX-License-Identifier: AGPL-3.0-only
// BitGlance, created 2026-09-30. Public market data only.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace BitGlance {
 public sealed class Quote {
  public decimal Price, Change, High, Low, Volume;
  public string Unit, Source, Symbol;
  public DateTime ReceivedUtc, MarketUtc;
  public bool IsStale(DateTime now) { return (now - MarketUtc).TotalSeconds > 120; }
 }
 public sealed class Candle { public long Time; public decimal Open,High,Low,Close,Volume; }
 public sealed class TickerEnvelope { public string code {get;set;} public Dictionary<string,object>[] data {get;set;} }
 public sealed class CandleEnvelope { public string code {get;set;} public object[][] data {get;set;} }
 public sealed class FeedException : Exception {
  public int RetrySeconds;
  public FeedException(string message, int retrySeconds) : base(message) { RetrySeconds=retrySeconds; }
 }
 public sealed class MarketClient : IDisposable {
  readonly HttpClient client;
  public MarketClient() {
   client = new HttpClient(); client.Timeout=TimeSpan.FromSeconds(12);
   client.DefaultRequestHeaders.UserAgent.ParseAdd("BitGlance/1.2");
  }
  async Task<string> Get(string url,CancellationToken token) {
   using(var r=await client.GetAsync(url,token).ConfigureAwait(false)) {
    if((int)r.StatusCode==429 || (int)r.StatusCode==418) {
     int delay=(int)r.StatusCode==418?300:60;
     if(r.Headers.RetryAfter!=null && r.Headers.RetryAfter.Delta.HasValue) delay=Math.Max(delay,(int)r.Headers.RetryAfter.Delta.Value.TotalSeconds);
     throw new FeedException("行情源限流，稍后自动重试",Math.Min(delay,3600));
    }
    r.EnsureSuccessStatusCode(); return await r.Content.ReadAsStringAsync().ConfigureAwait(false);
   }
  }
  public static readonly string[] Symbols={"BTCUSDT","ETHUSDT","SOLUSDT"};
  public static bool IsSupported(string symbol){return Symbols.Contains(symbol);}
  public async Task<Quote> FetchQuote(string symbol,CancellationToken token) {
   if(!IsSupported(symbol))throw new ArgumentException("Unsupported contract");
   return ParseTicker(await Get("https://api.bitget.com/api/v2/mix/market/ticker?symbol="+symbol+"&productType=USDT-FUTURES",token).ConfigureAwait(false),symbol,DateTime.UtcNow);
  }
  public static readonly string[] Intervals={"1m","5m","15m","1H"};
  public static bool IsInterval(string interval){return Intervals.Contains(interval);}
  public static int IntervalMinutes(string interval){if(!IsInterval(interval))throw new ArgumentException("Unsupported interval");return interval=="1H"?60:int.Parse(interval.TrimEnd('m'),CultureInfo.InvariantCulture);}
  public Task<List<Candle>> FetchCandles(string symbol,bool week,CancellationToken token) {
   return FetchCandles(symbol,week?"1H":"5m",week?169:289,token);
  }
  public async Task<List<Candle>> FetchCandles(string symbol,string interval,int limit,CancellationToken token) {
   if(!IsSupported(symbol))throw new ArgumentException("Unsupported contract");
   if(!IsInterval(interval)||limit<2||limit>1000)throw new ArgumentException("Unsupported chart request");
   string query=interval+"&limit="+limit.ToString(CultureInfo.InvariantCulture);
   string json=await Get("https://api.bitget.com/api/v2/mix/market/candles?symbol="+symbol+"&productType=USDT-FUTURES&granularity="+query+"&kLineType=MARKET",token).ConfigureAwait(false);
   var envelope=new JavaScriptSerializer().Deserialize<CandleEnvelope>(json);
   if(envelope==null||envelope.code!="00000"||envelope.data==null)throw new FormatException("历史行情接口返回错误");
   return ParseCandles(new JavaScriptSerializer().Serialize(envelope.data));
  }
  static decimal Number(Dictionary<string,object> d,string key,bool positive) {
   object o; decimal n;
   if(!d.TryGetValue(key,out o)||!decimal.TryParse(Convert.ToString(o,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out n)||(positive && n<=0)) throw new FormatException("无效行情字段: "+key);
   return n;
  }
  public static DateTime Unix(long ms) { return new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddMilliseconds(ms); }
  public static Quote ParseTicker(string json,string expectedSymbol,DateTime now) {
   var envelope=new JavaScriptSerializer().Deserialize<TickerEnvelope>(json);
   if(envelope==null||envelope.code!="00000"||envelope.data==null||envelope.data.Length!=1)throw new FormatException("行情接口返回错误");
   var d=envelope.data[0]; object symbol;
   if(!IsSupported(expectedSymbol)||!d.TryGetValue("symbol",out symbol)||Convert.ToString(symbol)!=expectedSymbol) throw new FormatException("交易对不匹配");
   var q=new Quote {Price=Number(d,"lastPr",true),Change=Number(d,"change24h",false)*100m,High=Number(d,"high24h",true),Low=Number(d,"low24h",true),Volume=Number(d,"baseVolume",false),Unit="USDT",Source="Bitget USDT-FUTURES",Symbol=expectedSymbol,ReceivedUtc=now,MarketUtc=Unix((long)Number(d,"ts",true))};
   if(q.Low>q.High || q.Volume<0 || q.MarketUtc>now.AddMinutes(5)) throw new FormatException("行情数据异常");
   return q;
  }
  public static List<Candle> ParseCandles(string json) {
   var rows=new JavaScriptSerializer().Deserialize<object[][]>(json);
   if(rows==null || rows.Length<2) throw new FormatException("历史行情不足");
   var candles=new List<Candle>();
   foreach(var row in rows) {
    if(row==null||row.Length<7) throw new FormatException("历史行情格式错误");
    long time;
    if(!long.TryParse(Convert.ToString(row[0],CultureInfo.InvariantCulture),NumberStyles.Integer,CultureInfo.InvariantCulture,out time)||time<=0||time>253402300799999L)throw new FormatException("历史行情时间无效");
    var c=new Candle{Time=time,Open=CandleNumber(row[1]),High=CandleNumber(row[2]),Low=CandleNumber(row[3]),Close=CandleNumber(row[4]),Volume=CandleNumber(row[5])};
    if(c.Open<=0||c.Close<=0||c.Low<=0||c.High<c.Low||c.High<Math.Max(c.Open,c.Close)||c.Low>Math.Min(c.Open,c.Close)||c.Volume<0)throw new FormatException("历史行情 OHLC 数据无效");
    candles.Add(c);
   }
   var result=candles.GroupBy(c=>c.Time).Select(g=>g.Last()).OrderBy(c=>c.Time).ToList();
   if(result.Count<2)throw new FormatException("历史行情不足");return result;
  }
  static decimal CandleNumber(object value){decimal n;if(!decimal.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out n))throw new FormatException("历史行情数值无效");return n;}
  public void Dispose(){client.Dispose();}
 }
}

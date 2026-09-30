// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;

namespace BitGlance {
 // Keeps a time anchor when the API rolls its fixed-size history forward.
 public sealed class ChartViewport {
  int total,first,requested=60;
  public int First {get{return first;}}
  public int Count {get{return Math.Min(total,requested);}}
  public bool Following {get;private set;}
  public ChartViewport(){Following=true;}
  int Clamp(int value){return Math.Max(0,Math.Min(Math.Max(0,total-Count),value));}
  public void Update(IList<Candle> oldData,IList<Candle> newData){
   long anchor=oldData.Count>first?oldData[first].Time:0;total=newData.Count;
   if(Following||oldData.Count==0){first=Clamp(total);Following=true;return;}
   int index=0;while(index<total&&newData[index].Time<anchor)index++;first=Clamp(index);
  }
  public void Reset(){requested=60;Latest();}
  public void Latest(){first=Clamp(total);Following=true;}
  public void Pan(int bars){first=Clamp(first+bars);Following=first==Math.Max(0,total-Count);}
  public void Zoom(int steps,double anchor){
   if(total==0||steps==0)return;
   anchor=Math.Max(0,Math.Min(1,anchor));double at=first+anchor*(Count-1);
   requested=(int)Math.Max(20,Math.Min(160,Math.Round(requested*Math.Pow(1.2,-Math.Max(-20,Math.Min(20,steps))))));
   first=Clamp((int)Math.Round(at-anchor*(Count-1)));Following=first==Math.Max(0,total-Count);
  }
 }
}

// SPDX-FileCopyrightText: 2026 Mr.Baoboer
// SPDX-License-Identifier: AGPL-3.0-only
// Additional terms: see ../legal/ADDITIONAL_TERMS.md
// Modified 2026-09-30 for BitGlance: C# port of position validation and
// visible-work-area clamping from PayDance src/lib/window-mode.ts.
// Upstream: 27ad42dc159e99a2a3545c47f5a47371e5e012f3. Not an official PayDance version.
using System;
using System.Drawing;
using System.Linq;

namespace BitGlance {
 public sealed class SavedPosition { public int X {get;set;} public int Y {get;set;} }
 public static class WindowPlacement {
  public static bool Usable(SavedPosition p) { return p!=null && p.X>-30000 && p.Y>-30000 && Math.Abs((long)p.X)<=1000000 && Math.Abs((long)p.Y)<=1000000; }
  public static int Opacity(int n) { return Math.Max(0,Math.Min(100,n)); }
  public static SavedPosition Resolve(SavedPosition p,Size size,Rectangle[] areas) {
   if(areas.Length==0) return Usable(p)?p:new SavedPosition{X=80,Y=80};
   Rectangle area=areas[0]; bool found=false;
   if(Usable(p)) {
    foreach(var a in areas) if(a.Contains(p.X,p.Y)){area=a;found=true;break;}
    if(!found){
     var candidate=areas.Select(a=>new{Area=a,Overlap=Rectangle.Intersect(a,new Rectangle(p.X,p.Y,size.Width,size.Height))}).OrderByDescending(a=>(long)a.Overlap.Width*a.Overlap.Height).First();
     if(candidate.Overlap.Width>0 && candidate.Overlap.Height>0){area=candidate.Area;found=true;}
    }
   }
   int x=found?p.X:area.Left+32,y=found?p.Y:area.Top+32;
   return new SavedPosition { X=Math.Min(Math.Max(x,area.Left+8),Math.Max(area.Left+8,area.Right-size.Width-8)),Y=Math.Min(Math.Max(y,area.Top+8),Math.Max(area.Top+8,area.Bottom-size.Height-8))};
  }
 }
}

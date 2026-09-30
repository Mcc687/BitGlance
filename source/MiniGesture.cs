// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Windows;

namespace BitGlance {
 public enum MiniGestureEnd { None,Click,Drag,DoubleClick }
 // Screen coordinates stay stable while the window follows the pointer.
 // Recognize clicks on release; the second press can still become a drag.
 public sealed class MiniGesture {
  readonly uint doubleTime;readonly double dragX,dragY,clickX,clickY;
  Point lastClick;uint downTime,lastTime;bool pendingClick,secondClick;
  public Point Start {get;private set;}
  public bool Active {get;private set;}
  public bool Dragging {get;private set;}
  public MiniGesture(int milliseconds,double dragWidth,double dragHeight,double clickWidth,double clickHeight){doubleTime=(uint)milliseconds;dragX=dragWidth;dragY=dragHeight;clickX=clickWidth;clickY=clickHeight;}
  public void Down(Point point,uint timestamp){
   if(Active)Cancel();
   secondClick=pendingClick&&unchecked(timestamp-lastTime)<=doubleTime&&Math.Abs(point.X-lastClick.X)<=clickX&&Math.Abs(point.Y-lastClick.Y)<=clickY;
   pendingClick=false;Start=point;downTime=timestamp;Active=true;Dragging=false;
  }
  public bool Move(Point point){
   if(!Active)return false;
   if(Math.Abs(point.X-Start.X)>=dragX||Math.Abs(point.Y-Start.Y)>=dragY){Dragging=true;secondClick=false;}
   return Dragging;
  }
  public MiniGestureEnd Up(Point point){
   if(!Active)return MiniGestureEnd.None;Move(point);Active=false;
   if(Dragging){Dragging=false;pendingClick=false;return MiniGestureEnd.Drag;}
   if(secondClick){secondClick=false;pendingClick=false;return MiniGestureEnd.DoubleClick;}
   pendingClick=true;lastClick=Start;lastTime=downTime;return MiniGestureEnd.Click;
  }
  public void Cancel(){Active=false;Dragging=false;pendingClick=false;secondClick=false;}
 }
}

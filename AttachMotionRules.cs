using System;
namespace RezShadow {
 internal static class AttachMotionRules {
  internal static float Advance(float angle,float degreesPerSecond,float dt) {
   if(Single.IsNaN(dt)||Single.IsInfinity(dt)||dt<=0) return angle;
   // Do not jump around the target following a stalled frame.
   float next=angle+degreesPerSecond*Math.Min(dt,0.1f);
   return (next%360+360)%360;
  }
  internal static bool UsableSample(float dt,float distance) {
   return dt>=0.001f && dt<=0.25f && !Single.IsNaN(distance) && !Single.IsInfinity(distance) && distance<=12;
  }
 }
}

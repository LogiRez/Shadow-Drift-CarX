using System;
namespace RezShadow {
 internal static class ShadowWheelMath {
  internal static float Advance(float angle,float speed,float radius,float direction,float dt) {
   if(float.IsNaN(speed)||float.IsInfinity(speed)||float.IsNaN(radius)||float.IsInfinity(radius)||radius<0.05f
    ||float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0) return angle;
   float step=Math.Max(-120,Math.Min(120,speed))/radius*(180f/(float)Math.PI)*Math.Min(dt,0.1f)*direction;
   return ((angle+step)%360+360)%360;
  }
 }
}

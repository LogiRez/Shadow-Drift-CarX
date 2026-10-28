using System;
namespace RezShadow {
 internal static class ShadowSteeringMath {
  internal static bool Angle(float axleX,float axleZ,out float degrees) {
   degrees=0;
   if(float.IsNaN(axleX)||float.IsInfinity(axleX)||float.IsNaN(axleZ)||float.IsInfinity(axleZ)||axleX*axleX+axleZ*axleZ<0.0001f) return false;
   // Wheel roll does not change its axle. Normalize mirrored wheel models to car-right.
   if(axleX<0) { axleX=-axleX; axleZ=-axleZ; }
   degrees=(float)Math.Max(-85,Math.Min(85,Math.Atan2(-axleZ,axleX)*180/Math.PI)); return true;
  }
 }
}

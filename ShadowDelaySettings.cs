using System;
namespace RezShadow {
 internal static class ShadowDelaySettings {
  internal static float Normalize(float value) {
   if(float.IsNaN(value)||float.IsInfinity(value)) return 0.1f;
   return (float)(Math.Round(Math.Max(0.01,Math.Min(0.3,value))*1000,MidpointRounding.AwayFromZero)/1000);
  }
 }
}

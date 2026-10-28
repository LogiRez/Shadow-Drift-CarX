using System;
namespace RezShadow {
 internal struct ShadowDriveState {
  internal bool valid; internal float throttle,brake,handbrake,rpm; internal int gear;
  internal static float Unit(float value) { return float.IsNaN(value)||float.IsInfinity(value)?0:Math.Max(0,Math.Min(1,value)); }
  internal static ShadowDriveState Blend(ShadowDriveState a,ShadowDriveState b,float t) {
   t=Unit(t);
   return new ShadowDriveState {valid=a.valid&&b.valid, throttle=Unit(a.throttle+(b.throttle-a.throttle)*t),
    brake=Unit(a.brake+(b.brake-a.brake)*t),handbrake=Unit(a.handbrake+(b.handbrake-a.handbrake)*t),
    rpm=a.rpm+(b.rpm-a.rpm)*t,gear=t<1?a.gear:b.gear};
  }
 }
}

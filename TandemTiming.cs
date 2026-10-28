using System;
namespace RezShadow {
 // Pure timing policy: the route clock always runs forward. It never moves a car directly.
 internal sealed class TandemTiming {
  internal const float MaxDelay=0.6f, BackoffRate=0.20f, CatchupRate=0.12f;
  internal float Delay,Rate;
  internal bool Transitioning;
  internal bool Preparing;
  float steeringBaseline,steeringCueTime,prepareUntil,lastPrepare=-10;
  float slip,steer,previousAbs,peak,stableTime,holdUntil,lastTrigger=-10;
  int driftSide;
  bool observed,armed;
  internal void Reset(float delay) {
   Delay=Clamp(delay,0.01f,MaxDelay); Rate=0; observed=armed=Transitioning=false;
   slip=steer=previousAbs=peak=stableTime=holdUntil=0; driftSide=0; lastTrigger=-10;
   Preparing=false; steeringBaseline=steeringCueTime=prepareUntil=0; lastPrepare=-10;
  }
  internal static float Clamp(float value,float min,float max) {
   return float.IsNaN(value)||float.IsInfinity(value)?min:Math.Max(min,Math.Min(max,value));
  }
  internal void Observe(float now,float dt,float speed,float angle,float wheelAngle,bool steeringValid) {
   if(dt<=0 || dt>0.25f || speed<5 || !Finite(angle)) {
    observed=armed=false; stableTime=steeringCueTime=0; Preparing=false; prepareUntil=0; Transitioning=now<holdUntil; return;
   }
   if(!observed) { slip=angle; steer=wheelAngle; previousAbs=Math.Abs(angle); observed=true; return; }
   float weight=1-(float)Math.Exp(-dt/0.09f),oldSteer=steer;
   slip+=(angle-slip)*weight;
   if(steeringValid && Finite(wheelAngle)) steer+=(wheelAngle-steer)*weight;
   float absolute=Math.Abs(slip),unwind=(previousAbs-absolute)/dt;
   int side=slip>=0?1:-1;
   if(absolute>=15 && unwind<8 && now>holdUntil) {
    if(side!=driftSide) { stableTime=0; driftSide=side; peak=absolute; }
    stableTime+=dt; peak=Math.Max(peak,absolute);
    if(stableTime>=0.2f && now-lastTrigger>0.7f && !armed) { armed=true; steeringBaseline=steer; }
   } else if(!armed) stableTime=0;
   bool steerCue=steeringValid && Math.Abs(steer-oldSteer)/dt>30;
   // Steering can start changing before the body visibly unwinds. Treat it as a
   // small, provisional request, never as a confirmed full transition.
   bool earlyCue=armed && steeringValid && Finite(wheelAngle) && absolute>=15
    && unwind>-3 && Math.Abs(steer-oldSteer)/dt>20 && Math.Abs(steer-steeringBaseline)>2;
   steeringCueTime=earlyCue?steeringCueTime+dt:0;
   if(steeringCueTime>=0.05f && now-lastPrepare>0.7f && now>=holdUntil) {
    prepareUntil=now+0.30f; lastPrepare=now; steeringCueTime=0;
   }
   if(armed && peak-absolute>2 && (unwind>15 || (steerCue && unwind>6))) {
    holdUntil=now+0.35f; lastTrigger=now; armed=false; stableTime=0; peak=0;
   }
   // Keep the gap through the neutral phase, with a bounded hold if the lead straightens.
   if(now-lastTrigger<0.6f && absolute<12) holdUntil=Math.Max(holdUntil,now+0.15f);
   previousAbs=absolute; Transitioning=now<holdUntil;
   Preparing=!Transitioning && now<prepareUntil;
  }
  internal void Advance(float dt,float baseDelay,float extraDelay,float clearanceDelay,bool manual) {
   if(!Finite(dt)||dt<=0) return;
   dt=Math.Min(dt,0.1f); // A hitch never causes a large catch-up step.
   baseDelay=Clamp(baseDelay,0.01f,0.3f);
   float target=Math.Max(baseDelay,Clamp(clearanceDelay,0.01f,MaxDelay));
   if(Transitioning || manual) target+=Clamp(extraDelay,0,0.3f);
   else if(Preparing) target+=Math.Min(0.025f,Clamp(extraDelay,0,0.3f));
   target=Clamp(target,0.01f,MaxDelay);
   // Critically damped timing spring, integrated in small steps for frame-rate independence.
   int steps=(int)Math.Ceiling(dt*240); float step=dt/steps;
   for(int i=0;i<steps;i++) {
    float acceleration=Clamp(16*(target-Delay)-8*Rate,-0.4f,0.4f);
    Rate=Clamp(Rate+acceleration*step,-CatchupRate,BackoffRate);
    Delay+=Rate*step;
    if(Delay<0.01f) { Delay=0.01f; Rate=Math.Max(0,Rate); }
    if(Delay>MaxDelay) { Delay=MaxDelay; Rate=Math.Min(0,Rate); }
   }
  }
  static bool Finite(float value) { return !float.IsNaN(value)&&!float.IsInfinity(value); }
 }
 internal static class ShadowArrivalRules {
  internal static bool CanPlace(float distance,float behind,float height) {
   return distance>=6 && distance<=25 && behind>=2 && behind<=distance && height>=0 && height<=1.5f;
  }
 }
 internal struct TandemFootprint {
  internal float x,z,yaw,front,rear,width;
  // SAT for two oriented, asymmetric car rectangles. Margin is total separation requested.
  internal static bool Overlaps(TandemFootprint a,TandemFootprint b,float margin) {
   double ar=a.yaw*Math.PI/180,br=b.yaw*Math.PI/180;
   double ax=Math.Cos(ar),az=-Math.Sin(ar),afX=Math.Sin(ar),afZ=Math.Cos(ar);
   double bx=Math.Cos(br),bz=-Math.Sin(br),bfX=Math.Sin(br),bfZ=Math.Cos(br);
   double dx=b.x+bfX*(b.front-b.rear)/2-a.x-afX*(a.front-a.rear)/2;
   double dz=b.z+bfZ*(b.front-b.rear)/2-a.z-afZ*(a.front-a.rear)/2;
   double aw=a.width/2,al=(a.front+a.rear)/2,bw=b.width/2,bl=(b.front+b.rear)/2;
   return Axis(dx,dz,ax,az,ax,az,afX,afZ,bx,bz,bfX,bfZ,aw,al,bw,bl,margin)
    && Axis(dx,dz,afX,afZ,ax,az,afX,afZ,bx,bz,bfX,bfZ,aw,al,bw,bl,margin)
    && Axis(dx,dz,bx,bz,ax,az,afX,afZ,bx,bz,bfX,bfZ,aw,al,bw,bl,margin)
    && Axis(dx,dz,bfX,bfZ,ax,az,afX,afZ,bx,bz,bfX,bfZ,aw,al,bw,bl,margin);
  }
  static bool Axis(double dx,double dz,double x,double z,double ax,double az,double afx,double afz,
   double bx,double bz,double bfx,double bfz,double aw,double al,double bw,double bl,float margin) {
   double extent=aw*Math.Abs(x*ax+z*az)+al*Math.Abs(x*afx+z*afz)
    +bw*Math.Abs(x*bx+z*bz)+bl*Math.Abs(x*bfx+z*bfz)+margin;
   return Math.Abs(dx*x+dz*z)<extent;
  }
 }
}


using System;
using UnityEngine;
namespace RezShadow {
 public sealed partial class Exporter {
  readonly TandemTiming tandemTiming=new TandemTiming();
  bool tandemChoice=true,tandemActive,tandemApproximate,tandemLimited;
  float tandemExtra=0.015f,tandemMargin=0.15f,tandemTickAt=-1,tandemClock=-1;
  float tandemClearance;
  TandemFootprint tandemOwnSize,tandemLeadSize;
  Vector3 tandemCachedPosition;
  Quaternion tandemCachedRotation;
  string tandemStatus="Tandem mode will ease back for transitions and close the gap gradually.";
  void ResetTandemTiming() {
   tandemTiming.Reset(shadowDelay); tandemTickAt=tandemClock=-1;
   tandemCachedPosition=shadowHoldPosition; tandemCachedRotation=shadowHoldRotation;
   tandemLimited=false; tandemClearance=shadowDelay;
  }
  TandemFootprint ReadTandemSize(Component car) {
   var result=new TandemFootprint {front=2.5f,rear=2.5f,width=2};
   try {
    float front=Convert.ToSingle(R(car,"lengthFront")),rear=Convert.ToSingle(R(car,"lengthRear")),width=Convert.ToSingle(R(car,"width"));
    if(!FiniteAttach(front)||!FiniteAttach(rear)||!FiniteAttach(width)||front<0.5f||rear<0.5f||width<1||front>5||rear>5||width>4)
     throw new InvalidOperationException("Car dimensions unavailable");
    result.front=front; result.rear=rear; result.width=width;
   } catch { tandemApproximate=true; }
   return result;
  }
  static TandemFootprint FootprintAt(TandemFootprint size,Vector3 position,Quaternion rotation) {
   size.x=position.x; size.z=position.z; size.yaw=rotation.eulerAngles.y; return size;
  }
  bool SampleTandem(float at,out Vector3 position,out Quaternion rotation) {
   ShadowPose a,b; float t;
   bool found=shadowTrail.Sample(at,out a,out b,out t);
   position=found?Vector3.Lerp(a.position,b.position,t):Vector3.zero;
   rotation=found?Quaternion.Slerp(a.rotation,b.rotation,t):Quaternion.identity; return found;
  }
  float TandemClearanceDelay(float now) {
   Vector3 lead; Quaternion leadRotation;
   if(!SampleTandem(shadowTrail.LatestTime,out lead,out leadRotation)) return shadowDelay;
   TandemFootprint leader=FootprintAt(tandemLeadSize,lead,leadRotation);
   // Check a modest yaw envelope during a cue, not a predicted driving path.
   TandemFootprint left=leader,right=leader;
   // Use actual body clearance; the former yaw envelope opened an unnecessarily large gap.
   float available=Mathf.Min(TandemTiming.MaxDelay,shadowTrail.Span);
   for(float delay=shadowDelay;delay<=available+0.0001f;delay+=0.01f) {
    Vector3 position; Quaternion rotation;
    if(!SampleTandem(now-delay,out position,out rotation)) continue;
    TandemFootprint follower=FootprintAt(tandemOwnSize,position,rotation);
    if(Mathf.Abs(position.y-lead.y)>3 || (!TandemFootprint.Overlaps(follower,leader,tandemMargin)
      && !TandemFootprint.Overlaps(follower,left,tandemMargin) && !TandemFootprint.Overlaps(follower,right,tandemMargin))) {
     tandemLimited=false; return delay;
    }
   }
   tandemLimited=true; return TandemTiming.MaxDelay;
  }
  float TandemSampleTime(float now) {
   float dt=tandemTickAt<0?0:now-tandemTickAt; tandemTickAt=now;
   Vector3 latest,earlier; Quaternion rotation,oldRotation;
   float latestAt=shadowTrail.LatestTime;
   if(SampleTandem(latestAt,out latest,out rotation) && SampleTandem(latestAt-0.10f,out earlier,out oldRotation)) {
    Vector3 motion=latest-earlier; motion.y=0;
    Vector3 forward=rotation*Vector3.forward; forward.y=0;
    float slip=motion.sqrMagnitude>0.0025f?Vector3.SignedAngle(motion,forward,Vector3.up):0;
    ShadowPose a,b; float t; shadowTrail.Sample(latestAt,out a,out b,out t);
    tandemTiming.Observe(now,dt,motion.magnitude/0.10f,slip,(b.steerLeft+b.steerRight)*0.5f,b.hasSteering);
   }
   tandemClearance=TandemClearanceDelay(now);
   bool manual=Input.GetKey(KeyCode.RightShift);
   // Entry blending already handles joining the route; do not wind up the timing spring while waiting.
   tandemTiming.Advance(dt,shadowDelay,tandemExtra,tandemClearance,manual);
   float at=Mathf.Min(now-tandemTiming.Delay,shadowTrail.LatestTime);
   if(tandemClock>=0) at=Mathf.Max(at,tandemClock);
   tandemClock=at;
   string phase=manual?"Manual back-off":tandemTiming.Preparing?"Early steering cue - easing off door":tandemTiming.Transitioning?"Giving transition room":tandemTiming.Rate<-0.003f?"Closing gradually":tandemTiming.Rate>0.003f?"Opening gap":"Holding gap";
   tandemStatus=phase+" | actual delay "+(now-at).ToString("0.000")+" s | route pace "+((1-tandemTiming.Rate)*100).ToString("0")+"%";
   if(tandemLimited) tandemStatus+=" | No clear position in the 0.60 s window; increase separation or detach.";
   else if(tandemClearance>tandemTiming.Delay+0.02f) tandemStatus+=" | Still easing toward clearance.";
   if(tandemApproximate) tandemStatus+=" | Estimated car dimensions.";
   return at;
  }
  void DrawTandemControls() {
   tandemChoice=GUILayout.Toggle(tandemChoice," Adaptive spacing (restart to apply)");
   if(!tandemChoice && !tandemActive) return;
   tandemExtra=TandemSlider("Extra delay",tandemExtra,0f,0.10f," s");
   tandemMargin=TandemSlider("Body margin",tandemMargin,0f,0.75f," m");
   GUILayout.Label("Hold Right Shift to back off",smallStyle);
   GUILayout.Label(tandemStatus,smallStyle);
  }
  float TandemSlider(string label,float value,float min,float max,string unit) {
   GUILayout.BeginHorizontal(); GUILayout.Label(label,GUILayout.Width(115));
   float result=GUILayout.HorizontalSlider(value,min,max,opacityTrack,opacityThumb,GUILayout.Width(280));
   GUILayout.Label(result.ToString("0.00")+unit,smallStyle,GUILayout.Width(75)); GUILayout.EndHorizontal(); return result;
  }
 }
}




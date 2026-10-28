using UnityEngine;
namespace RezShadow {
 public sealed partial class Exporter {
  struct ShadowPose { internal Vector3 position; internal Quaternion rotation; internal float steerLeft,steerRight,networkSteer; internal bool hasSteering,hasNetworkSteer; internal ShadowDriveState drive; }
  readonly TimedTrail<ShadowPose> shadowTrail=new TimedTrail<ShadowPose>(1024);
  const int PlayShadow=103;
  const int JoinLeader=104;
  bool joinLeaderActive,joinLeaderArmed;
  object joinInputManager;
  System.Reflection.MethodInfo joinInputValue;
  object[] joinInputActions;
  float joinNeutralSince=-1;
  bool BindJoinInput() {
   try {
    var type=FindType("GameInput.InputManager"); var action=FindType("GameInput.ActionType");
    joinInputManager=type.GetProperty("instance",Flags).GetValue(null,null);
    joinInputValue=type.GetMethod("GetActionValue",Flags,null,new[]{action},null);
    joinInputActions=new object[]{System.Enum.Parse(action,"Turn"),System.Enum.Parse(action,"Accelerate"),System.Enum.Parse(action,"Brake"),System.Enum.Parse(action,"HandBrake")};
    if(joinInputManager==null || joinInputValue==null) throw new System.Exception("Driving input unavailable");
    joinNeutralSince=-1; return true;
   } catch(System.Exception e) { shadowInfo="Join unavailable: "+e.Message; return false; }
  }
  void UpdateJoinLeaderHandoff() {
   if(!joinLeaderActive || !attachSnapshot || !shadowReady || shadowArrivalPending) return;
   try {
    float input=0;
    foreach(object action in joinInputActions) input=Mathf.Max(input,Mathf.Abs(System.Convert.ToSingle(joinInputValue.Invoke(joinInputManager,new[]{action}))));
    if(!joinLeaderArmed) {
     if(input>0.15f) joinNeutralSince=-1;
     else if(joinNeutralSince<0) joinNeutralSince=Time.unscaledTime;
     else if(Time.unscaledTime-joinNeutralSince>=0.25f) joinLeaderArmed=true;
     return;
    }
    if(input<0.20f) return;
    ReleaseShadowMotion("Driving control returned at leader speed.");
   } catch(System.Exception e) {
    joinLeaderActive=false;
    attachInfo="Auto handoff unavailable. Use F9 to return. "+e.Message;
   }
  }
  void ReleaseShadowMotion(string reason) {
    Component native=attachedNative; Rigidbody body=attachedBody;
    Vector3 velocity=body.velocity,angular=body.angularVelocity;
    object interp=shadowDriveInterpolator; var setter=shadowDriveVectorSetter;
    object velocityKey=shadowVelocityKey,angularKey=shadowAngularKey;
    ShadowDriveState drive=shadowDriveValue; bool hasDrive=shadowDriveValid;
    DetachCar(reason,true);
    if(native!=null && hasDrive) {
     native.GetType().GetProperty("gear",Flags).SetValue(native,drive.gear,null);
     native.GetType().GetProperty("rpm",Flags).SetValue(native,drive.rpm,null);
    }
    if(interp!=null && setter!=null) {
     setter.Invoke(interp,new object[]{velocityKey,velocity});
     setter.Invoke(interp,new object[]{angularKey,angular});
    }
    if(body!=null && !body.isKinematic) { body.velocity=velocity; body.angularVelocity=angular; }

  }
  bool shadowEnabled,shadowReady,shadowBlending;
  bool shadowArrivalPending,shadowArrivalClosing;
  float shadowArrivalTick;
  float shadowDelay=0.1f,shadowDelayChoice=0.1f,shadowSampleAt=-1,shadowBlendAt,shadowBlendDuration;
  Vector3 shadowLastTargetPosition,shadowHoldPosition,shadowBlendPosition;
  Quaternion shadowHoldRotation,shadowBlendRotation;
  string shadowInfo="Choose a target, then click Play delayed shadow.";
  void ClearShadowRoute() {
   shadowArrivalPending=shadowArrivalClosing=false;
   RestoreNetworkSteering();
   RestoreShadowDrive();
   shadowSteerValid=false; shadowSteerCar=null; shadowTargetLeft=shadowTargetRight=null;
   RestoreShadowWheels(); shadowWheelFailed=false;
   shadowEnabled=shadowReady=shadowBlending=false; shadowSampleAt=-1; shadowTrail.Clear(); tandemActive=false; ResetTandemTiming();
   shadowInfo="Choose a target, then click Play delayed shadow.";
  }
  void HoldShadowPose() {
   shadowSteerValid=false; shadowNetworkValid=false;
   if(attachedOwnCar==null) return;
   shadowHoldPosition=attachedOwnCar.transform.position; shadowHoldRotation=attachedOwnCar.transform.rotation;
   shadowReady=shadowBlending=false; ResetAttachMotion();
   shadowDriveValid=false; ResetTandemTiming();

  }
  void StartDelayedShadow() {
   RestoreShadowWheels(); shadowWheelFailed=false;
   attachPlayback.Stop(); attachSpin=false; attachSpinPhase=0; attachBlendTime=1;
   shadowDelay=ShadowDelaySettings.Normalize(shadowDelayChoice); shadowEnabled=true;
   tandemActive=tandemChoice; tandemApproximate=false;
   if(tandemActive) { tandemOwnSize=ReadTandemSize(attachedOwnCar); tandemLeadSize=ReadTandemSize(attachedTargetCar); }
   HoldShadowPose(); shadowInfo="Buffering the target's route...";
   shadowArrivalPending=true; shadowArrivalClosing=false;
  }
  bool TryShadowArrival(float now,out float delay) {
   delay=0;
   if(shadowTrail.Span<0.6f) return false;
   Vector3 lead=attachedTargetCar.transform.position;
   Vector3 forward=attachedTargetCar.transform.forward;
   TandemFootprint own=ReadTandemSize(attachedOwnCar);
   for(float candidate=0.6f;candidate>=0.299f;candidate-=0.02f) {
    Vector3 position; Quaternion rotation;
    if(!SampleTandem(now-candidate,out position,out rotation)) continue;
    Vector3 offset=position-lead;
    if(!ShadowArrivalRules.CanPlace(offset.magnitude,-Vector3.Dot(offset,forward),Mathf.Abs(offset.y))) continue;
    bool occupied=false;
    var footprint=FootprintAt(own,position,rotation);
    foreach(object player in players) {
     Component car=R(player,"userCar") as Component;
     if(car==null || !car.gameObject.activeInHierarchy || Mathf.Abs(car.transform.position.y-position.y)>3) continue;
     if((car.transform.position-position).sqrMagnitude>225) continue;
     if(TandemFootprint.Overlaps(footprint,FootprintAt(ReadTandemSize(car),car.transform.position,car.transform.rotation),0.5f)) { occupied=true; break; }
    }
    // Check the lead even if the periodically refreshed roster has not caught up.
    if(TandemFootprint.Overlaps(footprint,FootprintAt(ReadTandemSize(attachedTargetCar),lead,attachedTargetCar.transform.rotation),0.5f)) occupied=true;
    if(occupied) continue;
    delay=candidate; return true;
   }
   return false;
  }
  void CaptureShadowRoute(Transform target) {
   float now=Time.unscaledTime;
   if(shadowSampleAt>=0 && now>=shadowSampleAt && now-shadowSampleAt<1f/120) return;
   ShadowPose pose=new ShadowPose {position=target.position,rotation=target.rotation};
   pose.hasSteering=CaptureShadowSteering(out pose.steerLeft,out pose.steerRight);
   pose.drive=CaptureShadowDrive();
   try { pose.networkSteer=System.Convert.ToSingle(Call(R(attachedTargetCar,"carX"),"GetSteer")); pose.hasNetworkSteer=FiniteAttach(pose.networkSteer); } catch { pose.hasNetworkSteer=false; }
   if(!FiniteAttach(pose.position.x)||!FiniteAttach(pose.position.y)||!FiniteAttach(pose.position.z)
    ||!FiniteAttach(pose.rotation.x)||!FiniteAttach(pose.rotation.y)||!FiniteAttach(pose.rotation.z)||!FiniteAttach(pose.rotation.w))
    throw new System.InvalidOperationException("Target route contains an invalid pose.");
   float distance=shadowTrail.Count==0?0:Vector3.Distance(pose.position,shadowLastTargetPosition);
   if(tandemActive && shadowSampleAt>=0 && now-shadowSampleAt>0.25f) {
    shadowTrail.Clear(); distance=0; HoldShadowPose();
   }
   bool reset=shadowTrail.Add(now,pose,distance);
   shadowSampleAt=now; shadowLastTargetPosition=pose.position;
   if(reset && shadowEnabled) { HoldShadowPose(); shadowInfo="Route restarted after a position jump or recording gap."; }
  }
  void DelayedShadowPose(out Vector3 position,out Quaternion rotation) {
   ShadowPose a,b; float t,now=Time.unscaledTime;
   bool arriving=false;
   if(shadowArrivalPending) {
    float arrivalDelay;
    if(!TryShadowArrival(now,out arrivalDelay)) {
     position=shadowHoldPosition; rotation=shadowHoldRotation;
     shadowInfo="Waiting for a clear recorded position behind the lead. F9 cancels."; return;
    }
    tandemTiming.Reset(arrivalDelay); tandemClock=now-arrivalDelay; tandemTickAt=now;
    shadowArrivalTick=now; shadowArrivalPending=false; shadowArrivalClosing=!tandemActive;
    shadowReady=true; shadowBlending=false; arriving=true;
   }
   if(tandemActive && shadowTrail.Span<TandemTiming.MaxDelay) {
    position=shadowHoldPosition; rotation=shadowHoldRotation;
    shadowInfo="Buffering tandem route: "+shadowTrail.Span.ToString("0.00")+" / 0.60 seconds"; return;
   }
   // At low frame rates the newest recorded frame can be older than a 0.1-second delay.
   // Hold that newest pose rather than repeatedly restarting the entry blend; do not predict ahead.
   float sampleAt;
   if(tandemActive) sampleAt=TandemSampleTime(now);
   else if(shadowArrivalClosing || arriving) {
    tandemTiming.Advance(now-shadowArrivalTick,shadowDelay,0,shadowDelay,false); shadowArrivalTick=now;
    sampleAt=now-tandemTiming.Delay;
    if(Mathf.Abs(tandemTiming.Delay-shadowDelay)<0.0001f) shadowArrivalClosing=false;
   } else sampleAt=now-shadowDelay;
   if(shadowTrail.Count>=2 && now-shadowTrail.LatestTime<=0.5f) sampleAt=Mathf.Min(sampleAt,shadowTrail.LatestTime);
   if(!shadowTrail.Sample(sampleAt,out a,out b,out t)) {
    if(shadowReady) HoldShadowPose();
    position=shadowHoldPosition; rotation=shadowHoldRotation;
    shadowInfo="Buffering route: "+Mathf.Min(shadowTrail.Span,shadowDelay).ToString("0.000")+" / "+shadowDelay.ToString("0.000")+" seconds";
    return;
   }
   position=Vector3.Lerp(a.position,b.position,t); rotation=Quaternion.Slerp(a.rotation,b.rotation,t);
   if(arriving) {
    // A direct placement is not a travelled segment: never serialize the map-wide jump as velocity.
    Vector3 prior; Quaternion priorRotation;
    attachLinearVelocity=Vector3.zero; attachAngularVelocity=Vector3.zero;
    if(SampleTandem(sampleAt-0.02f,out prior,out priorRotation)) {
     attachLinearVelocity=Vector3.ClampMagnitude((position-prior)/0.02f,120);
     float angle; Vector3 axis; (rotation*Quaternion.Inverse(priorRotation)).ToAngleAxis(out angle,out axis);
     if(angle>180) angle-=360;
     if(FiniteAttach(axis.x)&&FiniteAttach(axis.y)&&FiniteAttach(axis.z)) attachAngularVelocity=Vector3.ClampMagnitude(axis*(angle*Mathf.Deg2Rad/0.02f),60);
    }
    attachMotionSample=true; attachSamplePosition=position; attachSampleRotation=rotation; attachSampleTime=now;
   }
   shadowDriveValue=ShadowDriveState.Blend(a.drive,b.drive,t); shadowDriveValid=shadowDriveValue.valid;
   shadowNetworkValid=a.hasNetworkSteer && b.hasNetworkSteer;
   shadowNetworkValue=Mathf.Lerp(a.networkSteer,b.networkSteer,t);
   shadowSteerValid=a.hasSteering && b.hasSteering;
   if(shadowSteerValid) {
    shadowSteerLeft=Mathf.LerpAngle(a.steerLeft,b.steerLeft,t);
    shadowSteerRight=Mathf.LerpAngle(a.steerRight,b.steerRight,t);
   }
   if(!shadowReady) {
    shadowReady=shadowBlending=true; shadowBlendAt=now;
    shadowBlendPosition=shadowHoldPosition; shadowBlendRotation=shadowHoldRotation;
    shadowBlendDuration=Mathf.Clamp(Vector3.Distance(position,shadowBlendPosition)/25,0.75f,5);
    ResetAttachMotion();
   }
   if(shadowBlending) {
    float blend=Mathf.Clamp01((now-shadowBlendAt)/shadowBlendDuration); blend=blend*blend*(3-2*blend);
    position=Vector3.Lerp(shadowBlendPosition,position,blend); rotation=Quaternion.Slerp(shadowBlendRotation,rotation,blend);
    if(now-shadowBlendAt>=shadowBlendDuration) shadowBlending=false;
   }
   shadowInfo=(shadowBlending?"Blending into route":"Following recorded route")+" - "+(now-sampleAt).ToString("0.000")+" seconds behind";
  }
 }}
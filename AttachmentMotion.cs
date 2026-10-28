using UnityEngine;
namespace RezShadow {
 public sealed partial class Exporter {
  bool attachOrbitReverse,attachSpin,attachMotionSample;
  readonly MovementPlayback attachPlayback=new MovementPlayback();
  bool attachOrbit { get { return attachPlayback.Running; } }
  bool attachOrbitPaused { get { return attachPlayback.Paused; } }
  float attachOrbitSpeed { get { return attachPlayback.Speed; } set { attachPlayback.Speed=value; } }
  float attachOrbitPhase { get { return attachPlayback.Phase; } }
  int attachPath { get { return attachPlayback.Path; } }
  float attachOrbitRadius=4,attachOrbitHeight=2,attachSpinSpeed=25,attachSpinPhase;
  int attachFacing; // target heading, face target, tangent
  int attachMovementRequest=-1;
  const int StopPath=100,PausePath=101,ReversePath=102;
  float attachPathLift=2,attachBlendTime=1;
  Vector3 attachBlendOffset;
  Quaternion attachBlendRotation;
  Vector3 attachSamplePosition,attachLinearVelocity,attachAngularVelocity;
  Quaternion attachSampleRotation;
  float attachSampleTime;
  Vector2 attachControlsScroll;
  void UpdateAttachmentAnimation() {
   ProcessAttachMovementRequest();
   if(!attachSnapshot) return;
   attachBlendTime=Mathf.Min(1,attachBlendTime+Mathf.Min(Time.unscaledDeltaTime,0.1f)/0.75f);
   attachPlayback.Tick(Time.unscaledDeltaTime,attachOrbitReverse);
   if(attachSpin) attachSpinPhase=AttachMotionRules.Advance(attachSpinPhase,attachSpinSpeed,Time.unscaledDeltaTime);
  }
  void ProcessAttachMovementRequest() {
   int request=attachMovementRequest; attachMovementRequest=-1;
   if(request<0) return;
   if(request==PlayShadow || request==JoinLeader || (request>=0 && request<MovementPaths.Names.Length)) {
    if(!EnsureSelectedAttachment()) return;
   }
   if(!attachSnapshot) return;
   if(request==JoinLeader) { StartDelayedShadow(); joinLeaderActive=true; joinLeaderArmed=false; visible=false; return; }
   joinLeaderActive=false;
   if(request==PlayShadow) { StartDelayedShadow(); return; }
   if(request==PausePath) { attachPlayback.Paused=!attachPlayback.Paused; ResetAttachMotion(); return; }
   if(request==ReversePath) { attachOrbitReverse=!attachOrbitReverse; ResetAttachMotion(); return; }
   BeginAttachPathTransition();
   shadowEnabled=shadowReady=shadowBlending=false;
   if(request==StopPath) attachPlayback.Stop();
   else attachPlayback.Play(request);
  }
  string AttachPlaybackStatus() {
   if(!attachSnapshot) return attachSelected!=null ? "Ready: choose a position, delayed shadow or sequence. Your car is still under your control." : "Movement: select a player first.";
   if(shadowEnabled) return "Delayed shadow: "+shadowInfo;
   if(!attachOrbit) return "Movement stopped. Click a sequence to play.";
   string state=attachOrbitPaused?"Paused":attachOrbitSpeed<=0?"Speed is zero":"Playing";
   return state+": "+MovementPaths.Names[attachPath]+"  |  Loop "+(attachOrbitPhase/360*100).ToString("0")+"%";
  }
  void AttachedPose(Transform anchor,out Vector3 position,out Quaternion rotation) {
   if(shadowEnabled) { DelayedShadowPose(out position,out rotation); return; }
   Quaternion heading=Quaternion.Euler(0,anchor.eulerAngles.y,0);
   if(attachOrbit) {
    float x,y,z,dx,dz;
    MovementPaths.Sample(attachPath,attachOrbitPhase,attachOrbitRadius,attachOrbitHeight,attachPathLift,out x,out y,out z,out dx,out dz);
    Vector3 radial=heading*new Vector3(x,0,z),tangent=heading*new Vector3(dx,0,dz)*(attachOrbitReverse?-1:1);
    position=anchor.position+radial+Vector3.up*y;
    if(attachFacing==1 && radial.sqrMagnitude>0.001f) rotation=Quaternion.LookRotation(-radial,Vector3.up);
    else if(attachFacing==2 && tangent.sqrMagnitude>0.001f) rotation=Quaternion.LookRotation(tangent,Vector3.up);
    else rotation=heading;
   } else {
    position=attachPreset==5?anchor.position+heading*new Vector3(attachOffset.x,0,attachOffset.z)+Vector3.up*attachOffset.y:anchor.position+anchor.rotation*attachOffset;
    rotation=anchor.rotation;
   }
   rotation=rotation*Quaternion.Euler(attachAngles)*Quaternion.Euler(0,attachSpinPhase,0);
   if(attachBlendTime<1) {
    float t=attachBlendTime*attachBlendTime*(3-2*attachBlendTime);
    position=Vector3.Lerp(anchor.position+heading*attachBlendOffset,position,t);
    rotation=Quaternion.Slerp(heading*attachBlendRotation,rotation,t);
   }
  }
  void BeginAttachPathTransition() {
   if(!attachSnapshot || attachedOwnCar==null || attachedTargetCar==null) return;
   Transform anchor=attachedTargetCar.transform; Quaternion inverse=Quaternion.Inverse(Quaternion.Euler(0,anchor.eulerAngles.y,0));
   attachBlendOffset=inverse*(attachedOwnCar.transform.position-anchor.position);
   attachBlendRotation=inverse*attachedOwnCar.transform.rotation; attachBlendTime=0; ResetAttachMotion();
  }
  void ResetAttachMotion() { attachMotionSample=false; attachLinearVelocity=attachAngularVelocity=Vector3.zero; }
  void ApplyAttachedPose(Vector3 position,Quaternion rotation,bool sampleMotion) {
   float now=Time.unscaledTime,dt=now-attachSampleTime;
   if(sampleMotion && !attachMotionSample) {
    attachedBody.velocity=attachedBody.angularVelocity=Vector3.zero;
    attachSamplePosition=position; attachSampleRotation=rotation; attachSampleTime=now; attachMotionSample=true;
   } else if(sampleMotion && dt>=0.001f) {
    if(AttachMotionRules.UsableSample(dt,(position-attachSamplePosition).magnitude)) {
     attachLinearVelocity=Vector3.ClampMagnitude((position-attachSamplePosition)/dt,120);
     Quaternion delta=rotation*Quaternion.Inverse(attachSampleRotation); float angle; Vector3 axis;
     delta.ToAngleAxis(out angle,out axis); if(angle>180) angle-=360;
     attachAngularVelocity=FiniteAttach(axis.x)&&FiniteAttach(axis.y)&&FiniteAttach(axis.z)?Vector3.ClampMagnitude(axis*(angle*Mathf.Deg2Rad/dt),60):Vector3.zero;
    } else attachLinearVelocity=attachAngularVelocity=Vector3.zero;
    attachSamplePosition=position; attachSampleRotation=rotation; attachSampleTime=now;
   }
   attachedBody.position=position; attachedBody.rotation=rotation;
   attachedBody.velocity=attachLinearVelocity; attachedBody.angularVelocity=attachAngularVelocity;
   attachedOwnCar.transform.SetPositionAndRotation(position,rotation);
  }
  void DrawOrbitControls() {

   GUILayout.Label("Movement sequences",nameStyle);
   GUILayout.Label("Click a sequence to start or restart it.",smallStyle);
   bool wasEnabled=GUI.enabled; GUI.enabled=wasEnabled && attachSelected!=null;
   GUILayout.BeginHorizontal();
   for(int i=0;i<MovementPaths.Names.Length;i++)
    if(ActionButton((attachSnapshot && attachOrbit && attachPath==i?"* ":"")+MovementPaths.Names[i],GUILayout.Width(153))) {
     attachMovementRequest=i;
    }
   GUILayout.EndHorizontal();
   GUILayout.BeginHorizontal();
   if(ActionButton(attachOrbit?"Stop sequence":"Play sequence",GUILayout.Width(170))) attachMovementRequest=attachOrbit?StopPath:attachPath;
   GUI.enabled=wasEnabled && attachSnapshot && attachOrbit;
   if(ActionButton(attachOrbitPaused?"Resume path":"Pause path",GUILayout.Width(150))) attachMovementRequest=PausePath;
   GUI.enabled=wasEnabled && attachSnapshot;
   if(ActionButton(attachOrbitReverse?"Direction: reverse":"Direction: forward",GUILayout.Width(180))) attachMovementRequest=ReversePath;
   if(ActionButton("Restart loop",GUILayout.Width(150))) attachMovementRequest=attachPath;
   GUILayout.EndHorizontal();
   GUI.enabled=wasEnabled && !shadowEnabled;
   attachOrbitRadius=AttachSlider("Path size",attachOrbitRadius,1.5f,12);
   attachOrbitHeight=AttachSlider("Base height",attachOrbitHeight,-12,8);
   if(attachPath>=2) attachPathLift=AttachSlider("Vertical travel",attachPathLift,0,6);
   attachOrbitSpeed=AttachSlider("Loop deg/sec",attachOrbitSpeed,0,60);
   GUILayout.Label(attachOrbitSpeed>0?"One loop: "+(360/attachOrbitSpeed).ToString("0.0")+" seconds":"Path speed is zero.",smallStyle);
   GUILayout.BeginHorizontal(); GUILayout.Label("Facing",smallStyle,GUILayout.Width(115));
   string[] options={"Target heading","Face target","Along path"};
   for(int i=0;i<3;i++) if(ActionButton((attachFacing==i?"* ":"")+options[i],GUILayout.Width(155))) { attachFacing=i; ResetAttachMotion(); }
   GUILayout.EndHorizontal();
   if(ActionButton(attachSpin?"Auto spin: on":"Auto spin: off",GUILayout.Width(180))) attachSpin=!attachSpin;
   attachSpinSpeed=AttachSlider("Spin deg/sec",attachSpinSpeed,-1440,1440);
   GUILayout.BeginHorizontal(); GUILayout.Label("Spin speed",smallStyle,GUILayout.Width(115));
   int[] spinPresets={90,360,720,1440};
   foreach(int speed in spinPresets) if(ActionButton(speed+" deg/s",GUILayout.Width(115))) attachSpinSpeed=speed*(attachSpinSpeed<0?-1:1);
   if(ActionButton("Reverse spin",GUILayout.Width(140))) attachSpinSpeed=-attachSpinSpeed;
   GUILayout.EndHorizontal();
   GUILayout.Label("Spin: "+(Mathf.Abs(attachSpinSpeed)/360).ToString("0.00")+" turns/sec. Speed buttons keep the selected direction.",smallStyle);
   if(ActionButton("Reset rotation",GUILayout.Width(180))) { attachAngles=Vector3.zero; attachSpinPhase=0; attachSpin=false; ResetAttachMotion(); }
   GUILayout.Label("Paths loop around the target's heading. Figure eight crosses the center; use enough height. Pause holds the path position; auto spin is separate. Location presets turn movement off.",smallStyle);
   GUI.enabled=wasEnabled;
  }
 }
}




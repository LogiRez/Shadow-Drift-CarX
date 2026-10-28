using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace RezShadow {
 public sealed partial class Exporter {
  bool attachTab; object attachRequested,attachedPlayer,attachedGame; int attachedLobbyId=-1;
  object attachSelected; int attachPresetRequested=-1;
  Component attachedOwnCar,attachedTargetCar,attachedNative;
  Rigidbody attachedBody;
  bool attachSnapshot,attachOldActive,attachOldKinematic,attachOldCollisions,attachOldGravity; float attachOldDrag,attachOldAngularDrag;
  RigidbodyInterpolation attachOldInterpolation;
  Vector3 attachStartPosition,attachOffset=new Vector3(0,2.2f,0),attachAngles;
  Quaternion attachStartRotation; int attachScene;
  string attachName="",attachInfo="Choose Attach to car on a player in the Players tab.";
  bool detachRequested; int attachPreset; float attachCheckAt;
  static readonly string[] AttachNames={"Roof","Hood","Behind","Left","Right","Underneath"};
  void UpdateCarAttachment() {
   if(Input.GetKeyDown(KeyCode.F9)) { detachRequested=true; }
   if(detachRequested) { detachRequested=false; DetachCar("Detached. Returned to your starting position."); }
   if(attachRequested!=null) {
    object target=attachRequested; attachRequested=null;
    attachSelected=target;
    attachInfo="Selected "+PlayerName(target)+". Choose a position, delayed shadow or movement sequence to attach.";
   }
   if(attachPresetRequested>=0) {
    int preset=attachPresetRequested; attachPresetRequested=-1;
    if(EnsureSelectedAttachment()) SetAttachPreset(preset);
   }
   UpdateAttachmentAnimation();
   UpdateJoinLeaderHandoff();
  }
  bool EnsureSelectedAttachment() {
   if(attachSelected==null) { attachInfo="Choose a player in the Players tab first."; return false; }
   try {
    if(!attachSnapshot || !System.Object.ReferenceEquals(attachSelected,attachedPlayer)) BeginCarAttachment(attachSelected);
    return attachSnapshot;
   } catch(Exception e) { DetachCar("Attach failed: "+(e.InnerException??e).Message); return false; }
  }
  bool AttachmentIsCurrent() {
   if(!attachSnapshot || attachedOwnCar==null || attachedTargetCar==null || attachedBody==null || attachedNative==null) return false;
   if(!attachedOwnCar.gameObject.activeInHierarchy || !attachedTargetCar.gameObject.activeInHierarchy) return false;
   if(attachedOwnCar.gameObject.scene.handle!=attachScene) return false;
   object game=gameProperty.GetValue(null,null);
   if(!System.Object.ReferenceEquals(game,attachedGame)) return false;
   object local=R(game,"LocalPlayer");
   if(!System.Object.ReferenceEquals(R(local,"userCar"),attachedOwnCar) || !System.Object.ReferenceEquals(R(attachedPlayer,"userCar"),attachedTargetCar)) return false;
   if(AttachmentLobby.ReadId(R(game,"CurrentRoom"),R)!=attachedLobbyId) return false;
   if(Time.unscaledTime>=attachCheckAt) {
    attachCheckAt=Time.unscaledTime+0.5f; bool found=false;
    IEnumerable roster=playersField.GetValue(game) as IEnumerable;
    if(roster!=null) foreach(object player in roster) if(System.Object.ReferenceEquals(player,attachedPlayer)) { found=true; break; }
    if(!found) return false;
   }
   return true;
  }
  void BeginCarAttachment(object target) {


   DetachCar(null);
   object game=gameProperty.GetValue(null,null),local=R(game,"LocalPlayer");
   if(game==null || local==null || System.Object.ReferenceEquals(local,target)) throw new InvalidOperationException("Join a lobby and select another player.");
   bool present=false; IEnumerable roster=playersField.GetValue(game) as IEnumerable;
   if(roster!=null) foreach(object player in roster) if(System.Object.ReferenceEquals(player,target)) { present=true; break; }
   if(!present) throw new InvalidOperationException("That player left the lobby.");
   int lobbyId=AttachmentLobby.ReadId(R(game,"CurrentRoom"),R);
   Component own=R(local,"userCar") as Component,other=R(target,"userCar") as Component;
   if(own==null || other==null || !own.gameObject.activeInHierarchy || !other.gameObject.activeInHierarchy) throw new InvalidOperationException("Wait for both cars to spawn.");
   if(!System.Object.Equals(R(own,"isNetworkCar"),false) || !System.Object.Equals(R(other,"isNetworkCar"),true)) throw new InvalidOperationException("Could not verify local and remote cars.");
   if(System.Object.Equals(R(own,"m_inGarage"),true)) throw new InvalidOperationException("Leave the garage before attaching.");
   Rigidbody body=R(own,"getRigidbody") as Rigidbody; Component native=R(own,"carX") as Component;
   if(body==null || native==null || !System.Object.Equals(R(native,"isCreated"),true)) throw new InvalidOperationException("Car physics is not ready.");
   attachedOwnCar=own; attachedTargetCar=other; attachedNative=native; attachedBody=body;
   attachedPlayer=target; attachedGame=game; attachedLobbyId=lobbyId; attachScene=own.gameObject.scene.handle;
   attachStartPosition=body.position; attachStartRotation=body.rotation;
   attachOldGravity=body.useGravity; attachOldDrag=body.drag; attachOldAngularDrag=body.angularDrag;
   attachOldActive=(bool)Call(native,"GetCarActive"); attachOldKinematic=body.isKinematic; attachOldCollisions=body.detectCollisions; attachOldInterpolation=body.interpolation;
   AcquireMotionLease();attachSnapshot=true; attachName=PlayerName(target); attachCheckAt=0;
   Call(native,"SetCarActive",false);
   if(!body.isKinematic) { body.velocity=Vector3.zero; body.angularVelocity=Vector3.zero; }
   body.detectCollisions=false; body.isKinematic=false; body.useGravity=false; body.drag=0; body.angularDrag=0; body.interpolation=RigidbodyInterpolation.None;
   ResetAttachMotion();
   attachSelected=target;
   if(attachSnapshot) attachInfo="Following "+attachName+". F9 detaches and returns you to your starting position.";
  }
  void DetachCar(string reason,bool keepMotion=false) {
   ReleaseMotionLease();
   joinLeaderActive=false; joinLeaderArmed=false;
   Vector3 releaseVelocity=attachedBody!=null?attachedBody.velocity:Vector3.zero;
   Vector3 releaseAngular=attachedBody!=null?attachedBody.angularVelocity:Vector3.zero;
   attachSelected=null; attachPresetRequested=-1;
   ClearShadowRoute();
   attachMovementRequest=-1; attachPlayback.Stop();
   // Restore each saved setting independently so a failed step cannot skip the rest.
   var errors=new List<string>();
   if(attachSnapshot) {
    attachSnapshot=false;
    if(attachedBody!=null) {
     try {
      if(!keepMotion && attachedOwnCar!=null && attachedOwnCar.gameObject.scene.handle==attachScene) {
       attachedBody.position=attachStartPosition; attachedBody.rotation=attachStartRotation;
       attachedOwnCar.transform.SetPositionAndRotation(attachStartPosition,attachStartRotation);
      }
     } catch(Exception e) { errors.Add("position: "+e.Message); }
     try { attachedBody.isKinematic=attachOldKinematic; if(!attachOldKinematic) { attachedBody.velocity=keepMotion?releaseVelocity:Vector3.zero; attachedBody.angularVelocity=keepMotion?releaseAngular:Vector3.zero; } } catch(Exception e) { errors.Add("motion: "+e.Message); }
     try { attachedBody.useGravity=attachOldGravity; attachedBody.drag=attachOldDrag; attachedBody.angularDrag=attachOldAngularDrag; } catch(Exception e) { errors.Add("gravity/drag: "+e.Message); }
     try { attachedBody.detectCollisions=attachOldCollisions; attachedBody.interpolation=attachOldInterpolation; } catch(Exception e) { errors.Add("collision: "+e.Message); }
    }
    if(attachedNative!=null) try { Call(attachedNative,"SetCarActive",attachOldActive); } catch(Exception e) { errors.Add("simulation: "+e.Message); }
   }
   if(keepMotion && attachedBody!=null && !attachOldKinematic) { attachedBody.velocity=releaseVelocity; attachedBody.angularVelocity=releaseAngular; }
   ResetAttachMotion();
   attachedBody=null; attachedOwnCar=attachedTargetCar=attachedNative=null;
   attachedPlayer=attachedGame=null; attachedLobbyId=-1;
   if(reason!=null) attachInfo=reason;
   if(errors.Count>0) { attachInfo="Detach needs attention. Respawn your car. "+String.Join("; ",errors.ToArray()); Debug.LogWarning(attachInfo); }
  }
  void FollowAttachedCar(bool sampleMotion=false) {
   if(!attachSnapshot) return;
   try {
    if(!AttachmentIsCurrent() || System.Object.Equals(R(attachedOwnCar,"m_inGarage"),true)) { DetachCar("Detached because a player left, changed car or changed room."); return; }

    // Adaptive timing advances once, after the target's rendered pose is captured.
    // Earlier callbacks reuse that result instead of advancing three separate clocks.
    if(shadowEnabled && tandemActive && !sampleMotion) {

     ApplyAttachedPose(tandemCachedPosition,tandemCachedRotation,false);
     ApplyNetworkSteering(); ApplyShadowDrive(); return;
    }
    Transform anchor=attachedTargetCar.transform;
    if(sampleMotion) CaptureShadowRoute(anchor);
    Vector3 position; Quaternion rotation; AttachedPose(anchor,out position,out rotation);

    if(!FiniteAttach(position.x)||!FiniteAttach(position.y)||!FiniteAttach(position.z)) throw new InvalidOperationException("Invalid target position.");
    ApplyAttachedPose(position,rotation,sampleMotion);
    if(shadowEnabled && tandemActive) { tandemCachedPosition=position; tandemCachedRotation=rotation; }
    ApplyNetworkSteering();
    ApplyShadowDrive();
   } catch(Exception e) { DetachCar("Attachment stopped: "+(e.InnerException??e).Message); }
  }
  void FixedUpdate() { UpdateToolInput();FollowAttachedCar(); }
  void LateUpdate() { FollowAttachedCar(true); }
  static bool FiniteAttach(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }
  void SetAttachPreset(int preset) {
   shadowEnabled=shadowReady=shadowBlending=false;
   attachBlendTime=1;
   attachMovementRequest=-1; attachPlayback.Stop();
   attachPreset=Mathf.Clamp(preset,0,AttachNames.Length-1); attachAngles=Vector3.zero; attachSpin=false; attachSpinPhase=0; ResetAttachMotion();
   float rear=2.5f,front=2.5f,width=2;
   try { if(attachedTargetCar!=null) { rear=Convert.ToSingle(R(attachedTargetCar,"lengthRear")); front=Convert.ToSingle(R(attachedTargetCar,"lengthFront")); width=Convert.ToSingle(R(attachedTargetCar,"width")); } } catch { }
   if(!FiniteAttach(rear)) rear=2.5f; if(!FiniteAttach(front)) front=2.5f; if(!FiniteAttach(width)) width=2;
   switch(attachPreset) {
    case 0: attachOffset=new Vector3(0,2.2f,0); break;
    case 1: attachOffset=new Vector3(0,1.3f,Mathf.Clamp(front,1,4)); break;
    case 2: attachOffset=new Vector3(0,0,-Mathf.Clamp(rear+3,4,8)); break;
    case 3: attachOffset=new Vector3(-Mathf.Clamp(width+1,3,6),0,0); break;
    case 4: attachOffset=new Vector3(Mathf.Clamp(width+1,3,6),0,0); break;
    default: attachOffset=new Vector3(0,-4,0); break;
   }
  }
  float AttachSlider(string label,float value,float min,float max) {
   GUILayout.BeginHorizontal(); GUILayout.Label(label,GUILayout.Width(115));
   float result=GUILayout.HorizontalSlider(value,min,max,opacityTrack,opacityThumb,GUILayout.Width(420));
   GUILayout.Label(result.ToString("0.0"),smallStyle,GUILayout.Width(65)); GUILayout.EndHorizontal(); return result;
  }
  void DrawCarAttachment() {
   GUILayout.BeginVertical(cardStyle);
   GUILayout.Label("Car attachment",nameStyle);
   GUILayout.Label(attachSelected!=null?"Selected: "+PlayerName(attachSelected):"Choose a player using Attach to car in the Players tab.",smallStyle);
   if(attachSnapshot) GUILayout.Label("Currently following: "+attachName,smallStyle);
   attachControlsScroll=GUILayout.BeginScrollView(attachControlsScroll,GUILayout.Height(Mathf.Clamp(window.height*.28f,160,260)));
   GUILayout.Space(8); GUILayout.BeginHorizontal();
   bool choiceEnabled=GUI.enabled; GUI.enabled=choiceEnabled && attachSelected!=null;
   for(int i=0;i<AttachNames.Length;i++) if(ActionButton((attachSnapshot && System.Object.ReferenceEquals(attachSelected,attachedPlayer) && !shadowEnabled && !attachOrbit && attachPreset==i?"* ":"")+AttachNames[i],GUILayout.Width(132))) attachPresetRequested=i;
   GUI.enabled=choiceEnabled;
   GUILayout.EndHorizontal(); GUILayout.Space(6);
   bool poseControlsEnabled=GUI.enabled; GUI.enabled=poseControlsEnabled && !shadowEnabled;
   attachOffset.x=AttachSlider("Left / right",attachOffset.x,-10,10);
   attachOffset.y=AttachSlider("Height / depth",attachOffset.y,-12,8);
   attachOffset.z=AttachSlider("Back / forward",attachOffset.z,-12,12);
   attachAngles.y=AttachSlider("Turn",attachAngles.y,-180,180);
   attachAngles.x=AttachSlider("Tilt",attachAngles.x,-180,180);
   attachAngles.z=AttachSlider("Roll",attachAngles.z,-180,180);
   GUI.enabled=poseControlsEnabled;
   GUILayout.Space(8); DrawOrbitControls(); GUILayout.EndScrollView();
   GUILayout.Space(8); bool enabled=GUI.enabled; GUI.enabled=enabled && attachSnapshot;
   if(ActionButton("Detach / F9 - return to start",GUILayout.Width(300))) detachRequested=true;
   GUI.enabled=enabled;
   GUILayout.Label(AttachPlaybackStatus(),smallStyle);
   GUILayout.Label(attachInfo,smallStyle);
   GUILayout.EndVertical();
  }
 }
}













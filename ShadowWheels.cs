using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace RezShadow {
 public sealed partial class Exporter {
  sealed class ShadowWheelVisual {
   internal Transform transform;
   internal Quaternion original,last;
   internal float radius,direction,angle,initialSteer;
   internal bool painted,front,left;
  }
  readonly List<ShadowWheelVisual> shadowWheels=new List<ShadowWheelVisual>();
  bool shadowWheelSpin=true,shadowWheelFailed;
  int shadowWheelFrame=-1,shadowWheelVisualHash;
  string shadowWheelStatus="Wheel spin and delayed steering are visual only.";
  void RestoreShadowWheels() {
   foreach(ShadowWheelVisual w in shadowWheels) {
    if(w.transform!=null && w.painted && Mathf.Abs(Quaternion.Dot(w.transform.localRotation,w.last))>0.99999f)
     w.transform.localRotation=w.original;
   }
   shadowWheels.Clear(); shadowWheelFrame=-1;
  }
  void BindShadowWheels() {
   IEnumerable indices=R(attachedOwnCar,"activeWheels") as IEnumerable;
   if(indices==null) throw new InvalidOperationException("Wheel list unavailable.");
   foreach(object index in indices) {
    Type type=attachedNative.GetType();
    var getWheel=type.GetMethod("GetWheel",Flags,null,new Type[]{index.GetType()},null);
    var getTransform=type.GetMethod("GetWheelTransform",Flags,null,new Type[]{index.GetType()},null);
    if(getWheel==null || getTransform==null) throw new MissingMethodException("Wheel rendering API unavailable.");
    object wheel=getWheel.Invoke(attachedNative,new object[]{index});
    Transform visual=getTransform.Invoke(attachedNative,new object[]{index}) as Transform;
    if(visual==null || !object.Equals(R(wheel,"isValid"),true)) continue;
    float radius=Convert.ToSingle(R(wheel,"radius"));
    if(!FiniteAttach(radius)||radius<0.05f||radius>3) continue;
    if(visual==attachedOwnCar.transform || !visual.IsChildOf(attachedOwnCar.transform)) continue;
    bool duplicate=false; foreach(ShadowWheelVisual known in shadowWheels) if(known.transform==visual) duplicate=true;
    if(duplicate) continue;
    float sign=Vector3.Dot(visual.right,attachedOwnCar.transform.right)<0?-1:1;
    string name=index.ToString(); bool front=name=="FrontLeft" || name=="FrontRight";
    float initialSteer=0; if(front) ReadVisualSteer(visual,attachedOwnCar.transform,out initialSteer);
    shadowWheels.Add(new ShadowWheelVisual {transform=visual,original=visual.localRotation,radius=radius,direction=sign,front=front,left=name=="FrontLeft",initialSteer=initialSteer});
   }
   if(shadowWheels.Count==0) throw new InvalidOperationException("No supported wheel visuals found.");
   shadowWheelStatus="Animating "+shadowWheels.Count+" wheels: travel spin / recorded front steering (visual only).";
  }
  void RenderShadowWheels() {
   try {
    if(!attachSnapshot || !shadowEnabled || !shadowReady || (!shadowWheelSpin && !shadowWheelSteer) || attachedOwnCar==null || attachedNative==null) {
     RestoreShadowWheels(); return;
    }
    if(shadowWheelFailed) return;
    int hash=Convert.ToInt32(R(attachedOwnCar,"LastLoadVisualHash"));
    if(shadowWheels.Count>0 && hash!=shadowWheelVisualHash) RestoreShadowWheels();
    if(shadowWheels.Count==0) { BindShadowWheels(); shadowWheelVisualHash=hash; }
    bool advance=shadowWheelFrame!=Time.frameCount; shadowWheelFrame=Time.frameCount;
    float speed=Vector3.Dot(attachLinearVelocity,attachedOwnCar.transform.forward);
    foreach(ShadowWheelVisual w in shadowWheels) {
     if(w.transform==null) continue;
     if(advance && shadowWheelSpin) w.angle=ShadowWheelMath.Advance(w.angle,speed,w.radius,w.direction,Time.unscaledDeltaTime);
     Quaternion steering=Quaternion.identity;
     if(w.front && shadowWheelSteer && shadowSteerValid) {
      Vector3 up=w.transform.parent==null?attachedOwnCar.transform.up:w.transform.parent.InverseTransformDirection(attachedOwnCar.transform.up);
      float target=w.left?shadowSteerLeft:shadowSteerRight;
      steering=Quaternion.AngleAxis(target-w.initialSteer,up);
     }
     w.last=steering*w.original*Quaternion.AngleAxis(w.angle,Vector3.right);
     w.transform.localRotation=w.last; w.painted=true;
    }
   } catch(Exception e) {
    RestoreShadowWheels(); shadowWheelFailed=true;
    shadowWheelStatus="Wheel animation unavailable: "+(e.InnerException??e).Message;
   }
  }
 }
}

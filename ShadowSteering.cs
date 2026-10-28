using System;
using UnityEngine;
namespace RezShadow {
 public sealed partial class Exporter {
  bool shadowWheelSteer=true,shadowSteerValid;
  float shadowSteerLeft,shadowSteerRight;
  Transform shadowTargetLeft,shadowTargetRight;
  Component shadowSteerCar; int shadowSteerHash;
  bool shadowNetworkValid,shadowNetworkSaved;
  float shadowNetworkValue,shadowNetworkOriginal;
  Component shadowNetworkNative;
  System.Reflection.MethodInfo shadowNetworkSetter;
  string shadowNetworkStatus="Steering will use CarX's normal car synchronization.";
  void RestoreNetworkSteering() {
   try { if(shadowNetworkSaved && shadowNetworkNative!=null && shadowNetworkSetter!=null)
    shadowNetworkSetter.Invoke(shadowNetworkNative,new object[]{shadowNetworkOriginal,false});
   } catch(Exception e) { Debug.LogWarning("Restore steering: "+e.Message); }
   shadowNetworkSaved=false; shadowNetworkNative=null; shadowNetworkSetter=null; shadowNetworkValid=false;
  }
  void ApplyNetworkSteering() {
   if(!shadowEnabled || !shadowWheelSteer || !shadowNetworkValid || attachedNative==null) {
    if(shadowNetworkSaved) RestoreNetworkSteering();
    return;
   }
   try {
    if(!shadowNetworkSaved) {
     shadowNetworkSetter=attachedNative.GetType().GetMethod("SetSteer",Flags,null,new Type[]{typeof(float),typeof(bool)},null);
     if(shadowNetworkSetter==null) throw new MissingMethodException("SetSteer(float,bool)");
     shadowNetworkOriginal=Convert.ToSingle(Call(attachedNative,"GetSteer"));
     shadowNetworkNative=attachedNative; shadowNetworkSaved=true;
    }
    shadowNetworkSetter.Invoke(shadowNetworkNative,new object[]{shadowNetworkValue,false});
    shadowNetworkStatus="Recorded steering applied to native car; remote visibility needs confirmation.";
   } catch(Exception e) {
    RestoreNetworkSteering(); shadowNetworkStatus="Native steering unavailable: "+(e.InnerException??e).Message;
   }
  }
  static bool ReadVisualSteer(Transform wheel,Transform car,out float angle) {
   angle=0; if(wheel==null || car==null) return false;
   Vector3 axle=car.InverseTransformDirection(wheel.right);
   return ShadowSteeringMath.Angle(axle.x,axle.z,out angle);
  }
  bool CaptureShadowSteering(out float left,out float right) {
   left=right=0;
   try {
    if(attachedTargetCar==null) return false;
    int hash=Convert.ToInt32(R(attachedTargetCar,"LastLoadVisualHash"));
    if(shadowSteerCar!=attachedTargetCar || shadowTargetLeft==null || shadowTargetRight==null || hash!=shadowSteerHash) {
     shadowSteerCar=attachedTargetCar; shadowSteerHash=hash;
     shadowTargetLeft=shadowTargetRight=null;
     object native=R(attachedTargetCar,"carX"); Type index=FindType("CarX.WheelIndex");
     if(native==null || index==null) return false;
     var getter=native.GetType().GetMethod("GetWheelTransform",Flags,null,new Type[]{index},null);
     if(getter==null) return false;
     shadowTargetLeft=getter.Invoke(native,new object[]{Enum.Parse(index,"FrontLeft")}) as Transform;
     shadowTargetRight=getter.Invoke(native,new object[]{Enum.Parse(index,"FrontRight")}) as Transform;
    }
    return ReadVisualSteer(shadowTargetLeft,attachedTargetCar.transform,out left)
     && ReadVisualSteer(shadowTargetRight,attachedTargetCar.transform,out right);
   } catch { return false; }
  }
 }
}


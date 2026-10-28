using System;
using System.Reflection;
using UnityEngine;
namespace RezShadow {
 public sealed partial class Exporter {
  bool shadowDriveEnabled=true,shadowDriveValid,shadowDriveSaved,shadowDriveFailed;
  ShadowDriveState shadowDriveValue;
  Component shadowDriveNative; object shadowDriveInterpolator;
  PropertyInfo[] shadowDriveProperties; object[] shadowDriveOriginal;
  MethodInfo shadowDriveVectorSetter; object shadowVelocityKey,shadowAngularKey;
  int shadowDriveMaxGear; float shadowDriveMaxRpm;
  string shadowDriveStatus="Drivetrain synchronization awaits a recorded route.";
  static readonly string[] ShadowDriveNames={"accelerate","brake","handbrake","gear","rpm","interpolation"};
  ShadowDriveState CaptureShadowDrive() {
   try {
    object native=R(attachedTargetCar,"carX");
    var state=new ShadowDriveState {throttle=Convert.ToSingle(R(native,"accelerate")),brake=Convert.ToSingle(R(native,"brake")),
     handbrake=Convert.ToSingle(R(native,"handbrake")),gear=Convert.ToInt32(R(native,"gear")),rpm=Convert.ToSingle(R(native,"rpm"))};
    state.valid=FiniteAttach(state.throttle)&&FiniteAttach(state.brake)&&FiniteAttach(state.handbrake)&&FiniteAttach(state.rpm)&&state.rpm>=0;
    return state;
   } catch { return new ShadowDriveState(); }
  }
  void BindShadowDrive() {
   Type type=attachedNative.GetType();
   var properties=new PropertyInfo[ShadowDriveNames.Length]; var values=new object[properties.Length];
   for(int i=0;i<properties.Length;i++) {
    properties[i]=type.GetProperty(ShadowDriveNames[i],Flags);
    if(properties[i]==null || !properties[i].CanWrite) throw new MissingMemberException(ShadowDriveNames[i]);
    values[i]=properties[i].GetValue(attachedNative,null);
   }
   object interp=R(attachedNative,"m_interpolator"); if(interp==null) throw new MissingMemberException("Car interpolator");
   MethodInfo setter=interp.GetType().GetMethod("SetVectorProp",Flags);
   if(setter==null) throw new MissingMethodException("SetVectorProp");
   Type vectorEnum=interp.GetType().GetNestedType("VectorProp",BindingFlags.Public|BindingFlags.NonPublic);
   Type keyType=setter.GetParameters()[0].ParameterType;
   object velocity=Enum.ToObject(keyType,Convert.ToInt32(Enum.Parse(vectorEnum,"Velocity")));
   object angular=Enum.ToObject(keyType,Convert.ToInt32(Enum.Parse(vectorEnum,"AngularVelocity")));
   shadowDriveMaxGear=Mathf.Clamp(Convert.ToInt32(R(attachedNative,"numGears")),1,12);
   shadowDriveMaxRpm=Convert.ToSingle(R(attachedNative,"engineRevLimiter"));
   if(!FiniteAttach(shadowDriveMaxRpm)||shadowDriveMaxRpm<1000) shadowDriveMaxRpm=20000;
   shadowDriveProperties=properties; shadowDriveOriginal=values; shadowDriveInterpolator=interp;
   shadowDriveVectorSetter=setter; shadowVelocityKey=velocity; shadowAngularKey=angular;
   shadowDriveNative=attachedNative; shadowDriveSaved=true;
  }
  void RestoreShadowDrive() {
   if(shadowDriveSaved && shadowDriveNative!=null) {
    // Reset temporary velocity caches before restoring interpolation and controls.
    try {
     shadowDriveVectorSetter.Invoke(shadowDriveInterpolator,new object[]{shadowVelocityKey,Vector3.zero});
     shadowDriveVectorSetter.Invoke(shadowDriveInterpolator,new object[]{shadowAngularKey,Vector3.zero});
    } catch(Exception e) { Debug.LogWarning("Restore speed cache: "+e.Message); }
    for(int i=0;i<shadowDriveProperties.Length;i++) try { shadowDriveProperties[i].SetValue(shadowDriveNative,shadowDriveOriginal[i],null); }
     catch(Exception e) { Debug.LogWarning("Restore "+ShadowDriveNames[i]+": "+e.Message); }
   }
   shadowDriveSaved=false; shadowDriveNative=null; shadowDriveInterpolator=null;
  }
  void ApplyShadowDrive() {
   if(!shadowEnabled || !shadowReady || !shadowDriveEnabled || !shadowDriveValid) {
    RestoreShadowDrive(); shadowDriveFailed=false; return;
   }
   if(shadowDriveFailed) return;
   try {
    if(!shadowDriveSaved) BindShadowDrive();
    shadowDriveProperties[5].SetValue(shadowDriveNative,true,null);
    shadowDriveVectorSetter.Invoke(shadowDriveInterpolator,new object[]{shadowVelocityKey,attachLinearVelocity});
    shadowDriveVectorSetter.Invoke(shadowDriveInterpolator,new object[]{shadowAngularKey,attachAngularVelocity});
    shadowDriveProperties[0].SetValue(shadowDriveNative,ShadowDriveState.Unit(shadowDriveValue.throttle),null);
    shadowDriveProperties[1].SetValue(shadowDriveNative,ShadowDriveState.Unit(shadowDriveValue.brake),null);
    shadowDriveProperties[2].SetValue(shadowDriveNative,ShadowDriveState.Unit(shadowDriveValue.handbrake),null);
    shadowDriveProperties[3].SetValue(shadowDriveNative,Mathf.Clamp(shadowDriveValue.gear,-1,shadowDriveMaxGear),null);
    shadowDriveProperties[4].SetValue(shadowDriveNative,Mathf.Clamp(shadowDriveValue.rpm,0,shadowDriveMaxRpm),null);
    float observed=Convert.ToSingle(R(shadowDriveNative,"speedKMH"));
    shadowDriveStatus="Native speed "+observed.ToString("0.0")+" km/h | RPM "+Convert.ToSingle(R(shadowDriveNative,"rpm")).ToString("0")+" | gear "+R(shadowDriveNative,"gear");
   } catch(Exception e) {
    RestoreShadowDrive(); shadowDriveFailed=true;
    shadowDriveStatus="Drivetrain unavailable: "+(e.InnerException??e).Message;
   }
  }
 }
}


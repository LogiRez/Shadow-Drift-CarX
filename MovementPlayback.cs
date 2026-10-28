using System;
namespace RezShadow {
 internal sealed class MovementPlayback {
  internal bool Running,Paused;
  internal int Path;
  internal float Phase,Speed=20;
  internal void Play(int path) {
   if(path<0 || path>=MovementPaths.Names.Length) throw new ArgumentOutOfRangeException("path");
   Path=path; Phase=0; Paused=false; Running=true;
   if(float.IsNaN(Speed)||float.IsInfinity(Speed)||Speed<=0) Speed=20;
  }
  internal void Stop() { Running=false; Paused=false; Phase=0; }
  internal void Tick(float dt,bool reverse) {
   if(Running && !Paused) Phase=AttachMotionRules.Advance(Phase,Speed*(reverse?-1:1),dt);
  }
 }
}

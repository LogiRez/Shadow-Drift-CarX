using System;
namespace RezShadow {
 // Bounded chronological ring. Sampling never extrapolates beyond recorded history.
 internal sealed class TimedTrail<T> {
  readonly float[] times;
  readonly T[] poses;
  int start,count;
  internal TimedTrail(int capacity) {
   if(capacity<2) throw new ArgumentOutOfRangeException("capacity");
   times=new float[capacity]; poses=new T[capacity];
  }
  internal int Count { get { return count; } }
  internal float LatestTime { get { return count==0?0:times[Index(count-1)]; } }
  internal float Span { get { return count<2?0:times[Index(count-1)]-times[start]; } }
  int Index(int offset) { return (start+offset)%times.Length; }
  internal void Clear() { Array.Clear(poses,0,poses.Length); start=count=0; }
  internal bool Add(float now,T pose,float distance) {
   if(float.IsNaN(now)||float.IsInfinity(now)||float.IsNaN(distance)||float.IsInfinity(distance)||distance<0)
    throw new ArgumentException("Invalid route sample.");
   bool reset=false;
   if(count>0) {
    float dt=now-times[Index(count-1)];
    if(dt<0 || dt>0.5f || distance>Math.Max(12f,dt*120f)) { Clear(); reset=true; }
    else if(dt==0) return false;
   }
   if(count==times.Length) { start=Index(1); count--; }
   int slot=Index(count); times[slot]=now; poses[slot]=pose; count++;
   return reset;
  }
  internal bool Sample(float at,out T a,out T b,out float fraction) {
   a=b=default(T); fraction=0;
   if(count<2 || float.IsNaN(at)||float.IsInfinity(at) || at<times[start] || at>times[Index(count-1)]) return false;
   int lo=0,hi=count-1;
   while(hi-lo>1) { int mid=(lo+hi)/2; if(times[Index(mid)]<=at) lo=mid; else hi=mid; }
   int left=Index(lo),right=Index(hi);
   a=poses[left]; b=poses[right]; fraction=(at-times[left])/(times[right]-times[left]); return true;
  }
 }
}

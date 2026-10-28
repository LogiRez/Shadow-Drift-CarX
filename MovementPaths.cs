using System;
namespace RezShadow {
 internal static class MovementPaths {
  internal static readonly string[] Names={"Circle","Figure eight","Wave orbit","Spiral loop","Side sweep"};
  // Positions and derivatives are in the target's level heading frame. All paths close smoothly.
  internal static void Sample(int path,float degrees,float radius,float height,float lift,out float x,out float y,out float z,out float dx,out float dz) {
   double p=degrees*Math.PI/180, s=Math.Sin(p),c=Math.Cos(p),s2=Math.Sin(2*p),c2=Math.Cos(2*p);
   x=(float)(radius*s); y=height; z=(float)(radius*c); dx=(float)(radius*c); dz=(float)(-radius*s);
   switch(path) {
    case 1: z=(float)(radius*0.5*s2); dz=(float)(radius*c2); break;
    case 2: y+=(float)(lift*s2); break;
    case 3:
     double r=radius*(0.65+0.35*c), dr=-radius*0.35*s;
     x=(float)(r*s); z=(float)(r*c); y+=(float)(lift*s);
     dx=(float)(dr*s+r*c); dz=(float)(dr*c-r*s); break;
    case 4: z=-radius; dz=0; y+=(float)(lift*(1-c)*0.5); break;
   }
  }
 }
}

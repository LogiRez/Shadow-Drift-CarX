using System;
namespace RezShadow {
 internal static class AttachmentLobby {
  internal static bool TryReadId(object room,Func<object,string,object> read,out int id) {
   id=-1;
   try {
    if(room==null || !Object.Equals(read(room,"IsJoined"),true)) return false;
    object value=read(room,"Id");
    if(!(value is int) || (int)value<0) return false;
    id=(int)value; return true;
   } catch { return false; }
  }
  internal static int ReadId(object room,Func<object,string,object> read) {
   if(room==null || !Object.Equals(read(room,"IsJoined"),true)) throw new InvalidOperationException("Join a multiplayer lobby before attaching.");
   object id=read(room,"Id");
   if(!(id is int) || (int)id<0) throw new InvalidOperationException("Could not identify the current multiplayer lobby.");
   return (int)id;
  }
 }
}

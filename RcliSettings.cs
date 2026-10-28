using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;

// Shared by the launcher and the injected tool. Preferences never modify either binary.
internal static class RcliSettings {
 const int Magic=0x52434C49,Version=1;
 static readonly object Gate=new object();
 internal static string Root {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Shadow Drift");}}
 static string Store {get{return Path.Combine(Root,"settings.dat");}}
 static Dictionary<string,string> Read() {
  var values=new Dictionary<string,string>(StringComparer.Ordinal);
  if(!File.Exists(Store)) return values;
  using(var stream=new FileStream(Store,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
  using(var reader=new BinaryReader(stream,Encoding.UTF8)) {
   if(reader.ReadInt32()!=Magic || reader.ReadInt32()!=Version) throw new InvalidDataException("Unsupported RCLI settings file.");
   int count=reader.ReadInt32();if(count<0 || count>256) throw new InvalidDataException("Invalid RCLI settings count.");
   for(int i=0;i<count;i++) values[reader.ReadString()]=reader.ReadString();
  }
  return values;
 }
 internal static string Get(string key,string fallback) {lock(Gate){string value;return Read().TryGetValue(key,out value)?value:fallback;}}
 internal static string Legacy(string key,string name,string fallback) {
  lock(Gate) {
   string existing;if(Read().TryGetValue(key,out existing)) return existing;
   string path=Path.Combine(Path.GetDirectoryName(typeof(RcliSettings).Assembly.Location),name);
   string value=File.Exists(path)?File.ReadAllText(path):fallback;
   Set(key,value);
   // Keep an original backup outside the deployment folder after successful import.
   if(File.Exists(path)) try {
    string archive=Path.Combine(Root,"legacy-backups");Directory.CreateDirectory(archive);
    string backup=Path.Combine(archive,name+"."+DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"."+Guid.NewGuid().ToString("N").Substring(0,6)+".bak");
    File.Move(path,backup);
   } catch { }
   return value;
  }
 }
 internal static void Set(string key,string value) {
  lock(Gate) using(var mutex=new Mutex(false,"Local\\RezShadow.Preferences.v1")) {
   bool taken=false;
   try {
    try {taken=mutex.WaitOne(1500);} catch(AbandonedMutexException) {taken=true;}
    if(!taken) throw new IOException("RCLI settings are busy. Retry shortly.");
    var values=Read();values[key]=value??"";
    Directory.CreateDirectory(Root);
    string temp=Store+"."+Guid.NewGuid().ToString("N")+".tmp";
    try {
     using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
     using(var writer=new BinaryWriter(stream,Encoding.UTF8)) {
      writer.Write(Magic);writer.Write(Version);writer.Write(values.Count);
      foreach(var pair in values) {writer.Write(pair.Key);writer.Write(pair.Value);}
     }
     if(File.Exists(Store)) File.Replace(temp,Store,null);else File.Move(temp,Store);
    } finally {if(File.Exists(temp)) File.Delete(temp);}
   } finally {if(taken) mutex.ReleaseMutex();}
  }
 }
 internal static string RuntimeFile(string name) {
  string folder=Path.GetDirectoryName(typeof(RcliSettings).Assembly.Location).ToLowerInvariant();
  uint hash=2166136261;foreach(char c in folder) {hash^=c;hash*=16777619;}
  string root=Path.Combine(Path.Combine(Root,"runtime"),hash.ToString("x8"));Directory.CreateDirectory(root);
  return Path.Combine(root,name);
 }
 internal static string LogFile(string name) {string root=Path.Combine(Root,"logs");Directory.CreateDirectory(root);return Path.Combine(root,name);}
}

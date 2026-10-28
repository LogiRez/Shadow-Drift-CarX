using System;
using System.IO;
using System.Reflection;
namespace RezShadow {
 internal static class BundledHarmony {
  static readonly string[] names={"Mono.Cecil","MonoMod.Utils","MonoMod.RuntimeDetour","0Harmony"};
  static bool registered;
  static readonly object gate=new object();
  static Assembly Loaded(string name) {
   foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) if(assembly.GetName().Name==name) return assembly;
   return null;
  }
  static Assembly Load(string name) {
   lock(gate) {
    Assembly existing=Loaded(name); if(existing!=null) return existing;
    using(Stream stream=typeof(BundledHarmony).Assembly.GetManifestResourceStream("RCLI.Harmony."+name+".dll")) {
     if(stream==null) throw new FileNotFoundException("Missing bundled dependency: "+name);
     using(MemoryStream bytes=new MemoryStream()) { stream.CopyTo(bytes); return Assembly.Load(bytes.ToArray()); }
    }
   }
  }
  static Assembly Resolve(object sender,ResolveEventArgs args) {
   string name=new AssemblyName(args.Name).Name;
   foreach(string allowed in names) if(name==allowed) return Load(name);
   return null;
  }
  internal static void EnsureLoaded() {
   lock(gate) {
    if(Loaded("0Harmony")!=null) return;
    if(!registered) { AppDomain.CurrentDomain.AssemblyResolve+=Resolve; registered=true; }
    foreach(string name in names) Load(name);
   }
  }
 }
}

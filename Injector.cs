using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

sealed class Remote : IDisposable
{
    internal IntPtr Handle;
    readonly List<IntPtr> blocks = new List<IntPtr>();
    bool timedOut;
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool IsWow64Process(IntPtr process, out bool wow);
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, UIntPtr size, uint kind, uint protect);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool VirtualFreeEx(IntPtr process, IntPtr address, UIntPtr size, uint kind);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool VirtualProtectEx(IntPtr process, IntPtr address, UIntPtr size, uint protect, out uint old);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] bytes, UIntPtr size, out UIntPtr written);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] bytes, UIntPtr size, out UIntPtr read);
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr CreateRemoteThread(IntPtr process, IntPtr attrs, UIntPtr stack, IntPtr start, IntPtr param, uint flags, out uint id);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
    [DllImport("kernel32.dll", SetLastError=true)] static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetExitCodeThread(IntPtr handle, out uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    static void Check(bool ok) { if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    internal Remote(int pid) {
        Handle = OpenProcess(0x43a, false, pid);
        if (Handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        bool wow;
        try { Check(IsWow64Process(Handle, out wow)); if (wow || IntPtr.Size != 8) throw new Exception("This launcher requires 64-bit CarX."); }
        catch { CloseHandle(Handle); Handle = IntPtr.Zero; throw; }
    }
    internal long Alloc(int size) { var p = VirtualAllocEx(Handle, IntPtr.Zero, (UIntPtr)size, 0x3000, 4); if (p == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error()); blocks.Add(p); return p.ToInt64(); }
    internal void Write(long p, byte[] b) { UIntPtr n; Check(WriteProcessMemory(Handle, new IntPtr(p), b, (UIntPtr)b.Length, out n)); if (n.ToUInt64() != (ulong)b.Length) throw new Exception("Incomplete memory write."); }
    internal byte[] Read(long p, int size) { var b = new byte[size]; UIntPtr n; Check(ReadProcessMemory(Handle, new IntPtr(p), b, (UIntPtr)size, out n)); if(n.ToUInt64() != (ulong)size) throw new Exception("Incomplete memory read."); return b; }
    internal long I64(long p) { return BitConverter.ToInt64(Read(p,8),0); }
    internal uint U32(long p) { return BitConverter.ToUInt32(Read(p,4),0); }
    internal ushort U16(long p) { return BitConverter.ToUInt16(Read(p,2),0); }
    internal string String(long p) { var b = new List<byte>(); for(int i=0;i<2048;i++) { byte c=Read(p+i,1)[0]; if(c==0) return Encoding.UTF8.GetString(b.ToArray()); b.Add(c); } throw new Exception("Unterminated remote string."); }
    internal long Text(string s) { byte[] b=Encoding.UTF8.GetBytes(s+"\0"); long p=Alloc(b.Length); Write(p,b); return p; }
    internal long Code(byte[] b) { long p=Alloc(b.Length); Write(p,b); uint old; Check(VirtualProtectEx(Handle,new IntPtr(p),(UIntPtr)b.Length,0x20,out old)); Check(FlushInstructionCache(Handle,new IntPtr(p),(UIntPtr)b.Length)); return p; }
    internal void Run(byte[] bytes) {
        long p=Code(bytes); uint id; IntPtr t=CreateRemoteThread(Handle,IntPtr.Zero,UIntPtr.Zero,new IntPtr(p),IntPtr.Zero,0,out id);
        if(t==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try { uint wait=WaitForSingleObject(t,15000); if(wait!=0) { timedOut=true; throw new Exception("CarX did not finish loading within 15 seconds. Return to the game; do not retry until restarting it."); } uint exit; Check(GetExitCodeThread(t,out exit)); if(exit!=0) throw new Exception("Loader thread failed (0x"+exit.ToString("X8")+"). Restart CarX before retrying."); }
        finally { CloseHandle(t); }
    }
    internal Dictionary<string,long> Exports(long module) {
        long pe=module+U32(module+0x3c);
        if(U32(pe)!=0x4550 || U16(pe+24)!=0x20b) throw new Exception("Unsupported runtime image.");
        uint exportRva=U32(pe+24+112), exportSize=U32(pe+24+116); long exp=module+exportRva;
        uint count=U32(exp+24); if(count>100000) throw new Exception("Invalid export table.");
        long functions=module+U32(exp+28), names=module+U32(exp+32), ordinals=module+U32(exp+36);
        var result=new Dictionary<string,long>();
        for(int i=0;i<count;i++) { string name=String(module+U32(names+i*4)); if(!name.StartsWith("mono_")) continue; uint rva=U32(functions+U16(ordinals+i*2)*4); if(rva>=exportRva && rva<exportRva+exportSize) continue; result[name]=module+rva; }
        return result;
    }
    public void Dispose() { if(!timedOut) foreach(var p in blocks) VirtualFreeEx(Handle,p,UIntPtr.Zero,0x8000); if(Handle!=IntPtr.Zero) CloseHandle(Handle); }
}

sealed class X64
{
    readonly List<byte> b=new List<byte>();
    readonly List<int> failureJumps=new List<int>();
    internal void Bytes(params byte[] v) { b.AddRange(v); }
    internal void Q(long v) { b.AddRange(BitConverter.GetBytes(v)); }
    internal void Begin() { Bytes(0x48,0x83,0xec,0x28); }
    internal void Arg(int reg,long value) { if(reg==0) Bytes(0x48,0xb9); else if(reg==1) Bytes(0x48,0xba); else if(reg==2) Bytes(0x49,0xb8); else Bytes(0x49,0xb9); Q(value); }
    internal void LoadArg(int reg,long address) { Arg(reg,address); if(reg==0) Bytes(0x48,0x8b,0x09); else if(reg==1) Bytes(0x48,0x8b,0x12); else if(reg==2) Bytes(0x4d,0x8b,0x00); else Bytes(0x4d,0x8b,0x09); }
    internal void Call(long p) { Bytes(0x48,0xb8); Q(p); Bytes(0xff,0xd0); }
    internal void Store(long p) { Bytes(0x49,0xbb); Q(p); Bytes(0x49,0x89,0x03); }
    internal void Set(long p,long value) { Bytes(0x48,0xb8); Q(value); Store(p); }
    internal void Require() { Bytes(0x48,0x85,0xc0,0x0f,0x84); failureJumps.Add(b.Count); Bytes(0,0,0,0); }
    internal void FailureLabel() { foreach(int pos in failureJumps) { byte[] offset=BitConverter.GetBytes(b.Count-pos-4); for(int j=0;j<4;j++) b[pos+j]=offset[j]; } failureJumps.Clear(); }
    internal void End() { Bytes(0x31,0xc0,0x48,0x83,0xc4,0x28,0xc3); }
    internal byte[] ToArray() { return b.ToArray(); }
}

static class Injector
{
    internal static void SelfTest() {
        using(var r=new Remote(Process.GetCurrentProcess().Id)) {
            long data=r.Alloc(8); var c=new X64(); c.Begin(); c.Set(data,0x1234567887654321); c.End(); r.Run(c.ToArray());
            if(r.I64(data)!=0x1234567887654321) throw new Exception("x64 execution test failed.");
            var jump=new X64(); jump.Begin(); jump.Bytes(0x31,0xc0); jump.Require(); jump.Set(data,0); jump.FailureLabel(); jump.End(); r.Run(jump.ToArray());
            if(r.I64(data)!=0x1234567887654321) throw new Exception("Failure branch test failed.");
            long sum=r.Code(new byte[]{0x48,0x89,0xc8,0x48,0x01,0xd0,0x4c,0x01,0xc0,0x4c,0x01,0xc8,0xc3});
            long argument=r.Alloc(8); r.Write(argument,BitConverter.GetBytes((long)7));
            var call=new X64(); call.Begin(); call.LoadArg(0,argument); call.Arg(1,11); call.Arg(2,13); call.Arg(3,17); call.Call(sum); call.Store(data); call.End(); r.Run(call.ToArray());
            if(r.I64(data)!=48) throw new Exception("Native four-argument call test failed.");
            long domains=r.Alloc(8+32*8);
            long callback=r.Code(new byte[]{0x48,0x8b,0x02,0x48,0x83,0xf8,0x20,0x73,0x0b,0x48,0x89,0x4c,0xc2,0x08,0x48,0xff,0xc0,0x48,0x89,0x02,0xc3});
            var enumeration=new X64(); enumeration.Begin();
            for(int i=0;i<33;i++) { enumeration.Arg(0,100+i); enumeration.Arg(1,domains); enumeration.Call(callback); }
            enumeration.End(); r.Run(enumeration.ToArray());
            if(r.I64(domains)!=32 || r.I64(domains+8)!=100 || r.I64(domains+32*8)!=131) throw new Exception("Bounded domain callback test failed.");
        }
        File.WriteAllText(RcliSettings.LogFile("self-test.txt"),"PASS: 64-bit execution, remote read/write, failure branch, four-argument native calls, bounded domain enumeration, cleanup. These checks do not validate the GUI inside CarX.");
    }
    internal static string Inject(bool unload = false, int expectedPid = 0) {
        string dll=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Shadow-Drift.Mod.dll");
        if(!File.Exists(dll)) throw new Exception("Keep Shadow-Drift.Mod.dll beside Shadow Drift.exe.");
        var processes=Process.GetProcessesByName("Drift Racing Online");
        if(processes.Length!=1) throw new Exception(processes.Length==0 ? "Start CarX first and wait for its main menu." : "More than one CarX process is running.");
        if(expectedPid != 0 && processes[0].Id != expectedPid) { foreach(var p in processes) p.Dispose(); throw new Exception("The CarX session changed. Refresh the launcher before retrying."); }
        using(var process=processes[0]) using(var r=new Remote(process.Id)) {
            ProcessModule mono=null;
            foreach(ProcessModule module in process.Modules) {
                if(module.ModuleName.Equals("mono-2.0-bdwgc.dll",StringComparison.OrdinalIgnoreCase)) mono=module;
            }
            if(mono==null) throw new Exception("The game's Mono runtime is not loaded. This build needs the Mono version of CarX.");
            var exports=r.Exports(mono.BaseAddress.ToInt64());
            Func<string,long> api=name=> { long p; if(!exports.TryGetValue(name,out p)) throw new Exception("Missing runtime API: "+name); return p; };
            long domains=r.Alloc(8+32*8);
            // Domain callback: append RCX to a bounded array supplied in RDX.
            long callback=r.Code(new byte[]{0x48,0x8b,0x02,0x48,0x83,0xf8,0x20,0x73,0x0b,0x48,0x89,0x4c,0xc2,0x08,0x48,0xff,0xc0,0x48,0x89,0x02,0xc3});
            var enumerate=new X64(); enumerate.Begin(); enumerate.Arg(0,callback); enumerate.Arg(1,domains); enumerate.Call(api("mono_domain_foreach")); enumerate.End(); r.Run(enumerate.ToArray());
            long count=r.I64(domains), chosen=0; var names=new List<string>();
            if(count<1 || count>32) throw new Exception("No valid Mono application domains found.");
            long nameResult=r.Alloc(8);
            for(int i=0;i<count;i++) {
                long domain=r.I64(domains+8+i*8); var getName=new X64(); getName.Begin(); getName.Arg(0,domain); getName.Call(api("mono_domain_get_friendly_name")); getName.Store(nameResult); getName.End(); r.Run(getName.ToArray());
                string name=r.String(r.I64(nameResult)); names.Add(name);
                if(name=="Unity Child Domain" || count==1) chosen=domain;
            }
            if(chosen==0) throw new Exception("Could not identify Unity's domain: "+String.Join(", ",names.ToArray()));
            long state=r.Alloc(64), path=r.Text(dll), ns=r.Text("RezShadow"), type=r.Text("Entry"), method=r.Text(unload ? "Unload" : "Load");
            var c=new X64(); c.Begin(); c.Set(state,1);
            c.Arg(0,chosen); c.Call(api("mono_thread_attach")); c.Store(state+8); c.Require();
            c.Set(state,2); c.Arg(0,chosen); c.Arg(1,path); c.Call(api("mono_domain_assembly_open")); c.Require();
            c.Bytes(0x48,0x89,0xc1); c.Call(api("mono_assembly_get_image")); c.Require();
            c.Bytes(0x48,0x89,0xc1); c.Arg(1,ns); c.Arg(2,type); c.Call(api("mono_class_from_name")); c.Require();
            c.Bytes(0x48,0x89,0xc1); c.Arg(1,method); c.Arg(2,0); c.Call(api("mono_class_get_method_from_name")); c.Require();
            c.Bytes(0x48,0x89,0xc1); c.Arg(1,0); c.Arg(2,0); c.Arg(3,state+16); c.Call(api("mono_runtime_invoke"));
            c.Set(state,3); c.FailureLabel();
            c.LoadArg(0,state+8); c.Bytes(0x48,0x85,0xc9,0x74,0x0c); c.Call(api("mono_thread_detach")); c.End();
            string marker=RcliSettings.RuntimeFile((unload ? "unloaded-" : "loaded-")+process.Id+".txt");
            if(File.Exists(marker)) File.Delete(marker);
            string oppositeMarker=RcliSettings.RuntimeFile((unload ? "loaded-" : "unloaded-")+process.Id+".txt");
            if(File.Exists(oppositeMarker)) File.Delete(oppositeMarker);
            r.Run(c.ToArray());
            if(r.I64(state)!=3) throw new Exception("Mono could not invoke the tool (stage "+r.I64(state)+"). If an older version is loaded, restart CarX first.");
            if(r.I64(state+16)!=0) throw new Exception("The managed entry point threw an exception. See CarX's Player.log.");
            for(int i=0;i<30;i++) { if(File.Exists(marker)) return unload ? "Shadow Drift deactivated." : "Shadow Drift ready. Return to CarX and use your configured menu key."; System.Threading.Thread.Sleep(100); }
            if(unload) return "Unload requested. Return to CarX to finish deactivating the tool.";
            return "Shadow Drift loaded; waiting for CarX to render. Return to the game, then use your configured menu key.\n\nIf it does not appear, check load-error.txt and CarX's Player.log.";
        }
    }
}





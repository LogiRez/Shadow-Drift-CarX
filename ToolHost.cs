using System;using System.IO;using System.Collections;using System.Collections.Generic;using System.Reflection;using UnityEngine;
namespace RezShadow {
 public sealed partial class Exporter:MonoBehaviour {
  const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
  const string ToolName="Shadow Drift",DefaultMenuKey="F8";const string ToolId="RezShadow";
  PropertyInfo gameProperty;FieldInfo playersField;readonly List<object> players=new List<object>();
  bool visible=true,menuInputCaptured,menuSettingsOpen,cursorSaved;float nextPoll,nextKeyRead;
  Rect window=new Rect(70,70,1040,800);Vector2 contentScroll,playerScroll;GUIStyle cardStyle,nameStyle,smallStyle,titleStyle,buttonStyle,opacityTrack,opacityThumb;GUISkin skin;readonly List<Texture2D> textures=new List<Texture2D>();
  string hostStatus="Ready";KeyCode menuKey;string menuKeyName=DefaultMenuKey;
  static readonly string[] MenuKeys={"F1","F2","F3","F4","F5","F6","F7","F8","F11","F12","Insert","Home","PageUp","PageDown"};
  static Exporter uiOwner;object inputHarmony;readonly List<MethodBase> inputPatches=new List<MethodBase>();MethodInfo lockInput,resetInput,getInputLock;bool nativeLockOwned;
  static Type FindType(string name){foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){Type t=a.GetType(name,false);if(t!=null)return t;}return null;}
  static readonly Dictionary<string,MemberInfo> members=new Dictionary<string,MemberInfo>();
  static object R(object o,string n){if(o==null)return null;string key=o.GetType().AssemblyQualifiedName+"/"+n;MemberInfo member;if(!members.TryGetValue(key,out member)){for(Type t=o.GetType();t!=null;t=t.BaseType){member=(MemberInfo)t.GetProperty(n,Flags|BindingFlags.DeclaredOnly)??t.GetField(n,Flags|BindingFlags.DeclaredOnly);if(member!=null)break;}if(member!=null)members[key]=member;}var p=member as PropertyInfo;if(p!=null)return p.GetValue(o,null);var f=member as FieldInfo;return f==null?null:f.GetValue(f.IsStatic?null:o);}
  static int Number(object o,string n){object v=R(o,n);if(v==null)throw new InvalidOperationException("Missing "+n);return Convert.ToInt32(v);}
  static object Call(object o,string n,params object[] args){if(o==null)throw new InvalidOperationException(n+" target unavailable");foreach(var m in o.GetType().GetMethods(Flags))if(m.Name==n&&!m.ContainsGenericParameters&&m.GetParameters().Length==args.Length)return m.Invoke(o,args);throw new MissingMethodException(n);}
  static void Set(object o,string n,object value){for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,Flags|BindingFlags.DeclaredOnly);if(f!=null){f.SetValue(o,value);return;}var p=t.GetProperty(n,Flags|BindingFlags.DeclaredOnly);if(p!=null){p.SetValue(o,value,null);return;}}throw new MissingMemberException(n);}
  string PlayerName(object p){string name=Convert.ToString(R(p,"FilteredNickName"));return System.Text.RegularExpressions.Regex.Replace(name??"Player","<[^>]*>|[\\r\\n\\t]","");}
  Dictionary<string,object[]> MenuRegistry(){lock(AppDomain.CurrentDomain){var d=AppDomain.CurrentDomain.GetData("rez.movement.menu-keys") as Dictionary<string,object[]>;if(d==null){d=new Dictionary<string,object[]>();AppDomain.CurrentDomain.SetData("rez.movement.menu-keys",d);}return d;}}
  bool SetMenuKey(string value,bool save){if(Array.IndexOf(MenuKeys,value)<0)return false;var map=MenuRegistry();lock(map){foreach(var pair in map){if(pair.Key==ToolId)continue;Behaviour owner=pair.Value[0] as Behaviour;if(owner!=null&&owner.isActiveAndEnabled&&Convert.ToString(pair.Value[1])==value){hostStatus=value+" is already used by "+pair.Key+". Choose another menu key.";return false;}}menuKey=(KeyCode)Enum.Parse(typeof(KeyCode),value);menuKeyName=value;map[ToolId]=new object[]{this,value};}if(save)try{RcliSettings.Set("menu.hotkey",value);}catch(Exception e){hostStatus="Menu key save: "+e.Message;}return true;}
  void ReadMenuKey(){string value=DefaultMenuKey;try{value=RcliSettings.Get("menu.hotkey",DefaultMenuKey);}catch(Exception e){hostStatus=e.Message;}if(value==menuKeyName&&MenuRegistry().ContainsKey(ToolId))return;if(SetMenuKey(value,false))return;if(!MenuRegistry().ContainsKey(ToolId)){foreach(string key in MenuKeys)if(SetMenuKey(key,true))break;}else try{RcliSettings.Set("menu.hotkey",menuKeyName);}catch{}}
  void AcquireMotionLease(){lock(AppDomain.CurrentDomain){Behaviour active=AppDomain.CurrentDomain.GetData("rez.movement.active") as Behaviour;if(active!=null&&active.isActiveAndEnabled&&!object.ReferenceEquals(active,this))throw new InvalidOperationException("Stop the other standalone movement tool before starting this mode.");AppDomain.CurrentDomain.SetData("rez.movement.active",this);}}
  void ReleaseMotionLease(){lock(AppDomain.CurrentDomain){if(object.ReferenceEquals(AppDomain.CurrentDomain.GetData("rez.movement.active"),this))AppDomain.CurrentDomain.SetData("rez.movement.active",null);}}
  public static bool ToolGameInputPrefix(){return uiOwner==null||!uiOwner.isActiveAndEnabled||!uiOwner.menuInputCaptured;}
  void InstallInput(){BundledHarmony.EnsureLoaded();inputHarmony=Activator.CreateInstance(FindType("HarmonyLib.Harmony"),new object[]{ToolId+".input"});var hook=typeof(Exporter).GetMethod("ToolGameInputPrefix");foreach(string name in new[]{"UnityEngine.EventSystems.EventSystem","NavigationInputModule"}){Type t=FindType(name);if(t==null)continue;foreach(string n in name=="NavigationInputModule"?new[]{"UpdateInput","UpdateMouse"}:new[]{"Update"}){var original=t.GetMethod(n,Flags,null,Type.EmptyTypes,null);if(original==null)continue;var descriptor=Activator.CreateInstance(FindType("HarmonyLib.HarmonyMethod"),new object[]{hook});Set(descriptor,"priority",800);bool done=false;foreach(var m in inputHarmony.GetType().GetMethods())if(m.Name=="Patch"){var p=m.GetParameters();int slot=Array.FindIndex(p,x=>x.Name=="prefix");if(slot<0)continue;object[] args=new object[p.Length];args[0]=original;args[slot]=descriptor;m.Invoke(inputHarmony,args);done=true;break;}if(done)inputPatches.Add(original);}}
   Type manager=FindType("GameInput.InputManager");if(manager!=null)foreach(var m in manager.GetMethods(Flags)){if(!m.IsStatic)continue;var p=m.GetParameters();if(p.Length==1&&p[0].ParameterType.IsAssignableFrom(GetType())){if(m.Name=="LockInput")lockInput=m;if(m.Name=="ResetLockInput")resetInput=m;}if(m.Name=="GetLockedInputObject"&&p.Length==0)getInputLock=m;}}
  void UpdateToolInput(){bool capture=visible||(menuInputCaptured&&(Input.GetMouseButton(0)||Input.GetMouseButton(1)||Input.GetMouseButton(2)));menuInputCaptured=capture;if(capture){if(!cursorSaved){lock(AppDomain.CurrentDomain){var owners=AppDomain.CurrentDomain.GetData("rez.movement.cursor-owners") as HashSet<Behaviour>;if(owners==null){owners=new HashSet<Behaviour>();AppDomain.CurrentDomain.SetData("rez.movement.cursor-owners",owners);}if(owners.Count==0)AppDomain.CurrentDomain.SetData("rez.movement.cursor-baseline",new object[]{Cursor.visible,Cursor.lockState});owners.Add(this);cursorSaved=true;}}Cursor.visible=true;Cursor.lockState=CursorLockMode.None;if(!nativeLockOwned&&getInputLock!=null&&lockInput!=null&&getInputLock.Invoke(null,null)==null){lockInput.Invoke(null,new object[]{this});nativeLockOwned=true;}}else ReleaseToolInput();}
  void ReleaseToolInput(){if(nativeLockOwned){try{if(resetInput!=null&&getInputLock!=null&&object.ReferenceEquals(getInputLock.Invoke(null,null),this))resetInput.Invoke(null,new object[]{this});}catch(Exception e){Debug.LogWarning(e);}nativeLockOwned=false;}menuInputCaptured=false;if(cursorSaved){lock(AppDomain.CurrentDomain){var owners=AppDomain.CurrentDomain.GetData("rez.movement.cursor-owners") as HashSet<Behaviour>;if(owners!=null){owners.Remove(this);if(owners.Count==0){var baseline=AppDomain.CurrentDomain.GetData("rez.movement.cursor-baseline") as object[];if(baseline!=null){Cursor.visible=(bool)baseline[0];Cursor.lockState=(CursorLockMode)baseline[1];}}}cursorSaved=false;}}}
  void Awake(){uiOwner=this;menuKey=(KeyCode)Enum.Parse(typeof(KeyCode),DefaultMenuKey);ReadMenuKey();Type controller=FindType("SyncMultiplayer.NetworkController"),game=FindType("SyncMultiplayer.NetworkGame");gameProperty=controller==null?null:controller.GetProperty("InstanceGame",Flags);playersField=game==null?null:game.GetField("Players",Flags);window.width=Mathf.Min(1040,Screen.width-40);window.height=Mathf.Min(800,Screen.height-40);window.x=(Screen.width-window.width)/2;window.y=(Screen.height-window.height)/2;try{InstallInput();}catch(Exception e){hostStatus="Input setup: "+e.GetBaseException().Message;}
#if SHADOW
   Application.onBeforeRender+=RenderShadowWheels;
#endif
  }
  void Update(){if(Time.unscaledTime>=nextKeyRead){nextKeyRead=Time.unscaledTime+1;ReadMenuKey();}if(Input.GetKeyDown(menuKey))visible=!visible;UpdateToolInput();
#if ROCKET
   if(Input.GetKeyDown(KeyCode.F10))StopRocketMovement("Stopped with F10");UpdateRocketMovement();
#else
   if(Time.unscaledTime>=nextPoll){nextPoll=Time.unscaledTime+1;RefreshPlayers();}UpdateCarAttachment();FollowAttachedCar();
#endif
  }
#if ROCKET
  void FixedUpdate(){UpdateToolInput();FixedRocketMovement();}
#else
  void RefreshPlayers(){try{players.Clear();object game=gameProperty==null?null:gameProperty.GetValue(null,null);var list=game==null||playersField==null?null:playersField.GetValue(game) as IEnumerable;if(list!=null)foreach(object p in list)if(p!=null&&!object.Equals(R(p,"IsLocal"),true))players.Add(p);}catch(Exception e){hostStatus="Players: "+e.Message;}}
#endif
  void OnDisable(){
#if ROCKET
   StopRocketMovement();ReleaseFlightRespawnGuard();
#else
   DetachCar("Tool unloaded");Application.onBeforeRender-=RenderShadowWheels;
#endif
   ReleaseMotionLease();ReleaseToolInput();if(uiOwner==this)uiOwner=null;var registry=MenuRegistry();lock(registry){object[] v;if(registry.TryGetValue(ToolId,out v)&&object.ReferenceEquals(v[0],this))registry.Remove(ToolId);}if(inputHarmony!=null){var undo=inputHarmony.GetType().GetMethod("Unpatch",new[]{typeof(MethodBase),typeof(MethodInfo)});foreach(var m in inputPatches)try{if(undo!=null)undo.Invoke(inputHarmony,new object[]{m,typeof(Exporter).GetMethod("ToolGameInputPrefix")});}catch(Exception e){Debug.LogWarning(e);}}inputPatches.Clear();}
  void OnDestroy(){foreach(var t in textures)if(t!=null)UnityEngine.Object.Destroy(t);if(skin!=null)UnityEngine.Object.Destroy(skin);}
  Texture2D Solid(Color c){var t=new Texture2D(1,1);t.SetPixel(0,0,c);t.Apply();textures.Add(t);return t;}
  void PrepareSkin(){if(skin!=null)return;skin=UnityEngine.Object.Instantiate(GUI.skin);Color ivory=new Color(.94f,.92f,.95f),muted=new Color(.75f,.74f,.82f);Texture2D surface=Solid(new Color(.08f,.07f,.11f,.98f)),card=Solid(new Color(.13f,.10f,.16f,.97f)),red=Solid(new Color(.58f,.12f,.26f)),hover=Solid(new Color(.72f,.20f,.34f)),field=Solid(new Color(.055f,.05f,.075f));skin.window.normal.background=surface;skin.window.onNormal.background=surface;skin.window.padding=new RectOffset(0,0,0,0);skin.window.normal.textColor=Color.clear;skin.label.normal.textColor=ivory;skin.label.fontSize=15;skin.button.fixedHeight=38;skin.button.margin=new RectOffset(3,8,5,6);skin.button.normal.background=red;skin.button.hover.background=hover;skin.button.active.background=hover;skin.button.normal.textColor=skin.button.hover.textColor=Color.white;skin.textField.normal.background=field;skin.textField.normal.textColor=ivory;skin.textField.fixedHeight=36;skin.toggle.fontSize=15;skin.toggle.wordWrap=true;skin.toggle.normal.textColor=skin.toggle.onNormal.textColor=ivory;skin.toggle.margin=new RectOffset(3,3,8,8);cardStyle=new GUIStyle(skin.box){padding=new RectOffset(16,16,12,12),margin=new RectOffset(0,8,8,12)};cardStyle.normal.background=card;nameStyle=new GUIStyle(skin.label){fontSize=20,fontStyle=FontStyle.Bold,wordWrap=true};smallStyle=new GUIStyle(skin.label){fontSize=14,wordWrap=true};smallStyle.normal.textColor=muted;titleStyle=new GUIStyle(nameStyle){fontSize=24};buttonStyle=skin.button;opacityTrack=skin.horizontalSlider;opacityThumb=skin.horizontalSliderThumb;
  }
  bool ActionButton(string label,params GUILayoutOption[] options){return GUILayout.Button(label,options);}
  void OnGUI(){if(!visible)return;PrepareSkin();GUISkin prior=GUI.skin;bool enabled=GUI.enabled;try{GUI.skin=skin;GUI.enabled=true;window=GUI.Window(0x534844,window,DrawToolWindow,ToolName);}finally{GUI.skin=prior;GUI.enabled=enabled;}}
  void DrawToolWindow(int id){GUI.DrawTexture(new Rect(0,0,window.width,3),textures[2]);GUI.Label(new Rect(24,18,window.width-160,36),ToolName,titleStyle);GUI.Label(new Rect(window.width-100,25,80,24),"by rez",smallStyle);GUILayout.BeginArea(new Rect(24,76,window.width-48,window.height-122));contentScroll=GUILayout.BeginScrollView(contentScroll);GUILayout.BeginVertical();
   if(ActionButton("Menu key: "+menuKeyName+(menuSettingsOpen?"  ▲":"  ▼"),GUILayout.Width(260)))menuSettingsOpen=!menuSettingsOpen;if(menuSettingsOpen){GUILayout.Label("Choose a key. Changes are saved; keys already used by the other tool are rejected.",smallStyle);int old=Array.IndexOf(MenuKeys,menuKeyName);int choice=GUILayout.SelectionGrid(old,MenuKeys,4);if(choice!=old&&choice>=0)SetMenuKey(MenuKeys[choice],true);}GUILayout.Label(hostStatus,smallStyle);
#if ROCKET
   DrawRocketMovement();
#else
   DrawShadowTool();
#endif
   GUILayout.BeginHorizontal();if(ActionButton("Hide menu",GUILayout.Width(170)))visible=false;if(ActionButton("Unload tool",GUILayout.Width(170)))Entry.Unload();GUILayout.EndHorizontal();GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.EndArea();GUI.Label(new Rect(24,window.height-30,window.width-48,22),menuKeyName+"  MENU     ·     F9  STOP / RETURN",smallStyle);GUI.DragWindow(new Rect(0,0,window.width,66));}
 }
}



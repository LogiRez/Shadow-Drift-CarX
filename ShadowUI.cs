using UnityEngine;
namespace RezShadow { public sealed partial class Exporter {
 string playerSearch="";
 void DrawShadowTool(){
 GUILayout.Label("Choose your leader",nameStyle);playerSearch=GUILayout.TextField(playerSearch);
 playerScroll=GUILayout.BeginScrollView(playerScroll,GUILayout.Height(140));
 foreach(object p in players) {string n=PlayerName(p);if(n.IndexOf(playerSearch,System.StringComparison.OrdinalIgnoreCase)<0)continue;if(ActionButton((object.ReferenceEquals(p,attachSelected)?"✓  ":"")+n))attachRequested=p;}
 if(players.Count==0)GUILayout.Label("Join a multiplayer lobby and wait for players to load.",smallStyle);
 GUILayout.EndScrollView();GUILayout.Label(attachSelected==null?"No leader selected":"Leader: "+PlayerName(attachSelected),smallStyle);
 DrawDelayedShadow();GUILayout.Label(attachInfo,smallStyle);
 if(attachSnapshot&&ActionButton("Stop and return to start (F9)"))detachRequested=true;
 }
}}

namespace RezShadow { public sealed partial class Exporter {
  void DrawDelayedShadow() {
   GUILayout.Label("Delayed shadow",nameStyle);
   GUILayout.BeginHorizontal(); GUILayout.Label("Delay (sec)",GUILayout.Width(115));
   shadowDelayChoice=ShadowDelaySettings.Normalize(GUILayout.HorizontalSlider(shadowDelayChoice,0.01f,0.3f,opacityTrack,opacityThumb,GUILayout.Width(280)));
   GUILayout.Label(shadowDelayChoice.ToString("0.000"),smallStyle,GUILayout.Width(65)); GUILayout.EndHorizontal();
   GUILayout.BeginHorizontal();
   if(ActionButton("- 0.001",GUILayout.Width(105))) shadowDelayChoice=ShadowDelaySettings.Normalize(shadowDelayChoice-0.001f);
   if(ActionButton("+ 0.001",GUILayout.Width(105))) shadowDelayChoice=ShadowDelaySettings.Normalize(shadowDelayChoice+0.001f);
   foreach(float delay in new float[]{0.025f,0.05f,0.1f})
    if(ActionButton(delay.ToString("0.000"),GUILayout.Width(75))) shadowDelayChoice=delay;
   GUILayout.EndHorizontal();
   DrawTandemControls();
   shadowWheelSpin=GUILayout.Toggle(shadowWheelSpin," Animate wheel spin");
   shadowWheelSteer=GUILayout.Toggle(shadowWheelSteer," Match delayed front-wheel steering");
   shadowDriveEnabled=GUILayout.Toggle(shadowDriveEnabled," Synchronize shadow drivetrain");
   GUILayout.Label(shadowDriveStatus,smallStyle);
   if(shadowNetworkStatus.StartsWith("Native steering unavailable")) GUILayout.Label(shadowNetworkStatus,smallStyle);
   if(shadowWheelStatus.StartsWith("Wheel animation unavailable")) GUILayout.Label(shadowWheelStatus,smallStyle);
   bool enabled=GUI.enabled; GUI.enabled=enabled && attachSelected!=null;
   GUILayout.BeginHorizontal();
   if(ActionButton(shadowEnabled?"Apply delay / restart":"Play delayed shadow",GUILayout.Width(190))) attachMovementRequest=PlayShadow;
   if(ActionButton("Join leader / drive away",GUILayout.Width(210)) && BindJoinInput()) attachMovementRequest=JoinLeader;
   GUI.enabled=enabled && attachSnapshot && shadowEnabled;
   if(ActionButton("Stop shadow",GUILayout.Width(110))) attachMovementRequest=StopPath;
   GUI.enabled=enabled; GUILayout.EndHorizontal();
   if(shadowEnabled) GUILayout.Label(shadowInfo,smallStyle);
   if(joinLeaderActive) GUILayout.Label(joinLeaderArmed?"Join active: steer, accelerate or brake to take over.":"Join active: release driving controls to arm takeover.",smallStyle);
   GUILayout.Space(10);
  }
}}

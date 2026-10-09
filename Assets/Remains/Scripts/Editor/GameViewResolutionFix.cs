using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace Remains.Editor {
// Game view "Low Resolution Aspect Ratios" renders at half the Retina resolution and looks pixelated in Play mode.
public static class GameViewResolutionFix {
 const BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 [MenuItem("Remains/Game View/Full Resolution")]
 public static string Apply(){
  var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var report="";
  foreach(var w in Resources.FindObjectsOfTypeAll(type)){
   var low=type.GetProperty("lowResolutionForAspectRatios",Any);
   var before=low?.GetValue(w);if(low!=null&&low.CanWrite)low.SetValue(w,false);
   var vsync=type.GetProperty("vSyncEnabled",Any);
   report+=$"lowRes {before} -> {low?.GetValue(w)}; vsync {vsync?.GetValue(w)}; ";
   ((EditorWindow)w).Repaint();
  }
  return report==""?"no game view":report;
 }
}
}

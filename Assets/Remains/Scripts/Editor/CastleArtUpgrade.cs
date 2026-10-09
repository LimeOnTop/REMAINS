using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Remains.Editor {
public static partial class CastleArtUpgrade {
 const string ScenePath="Assets/Remains/Scenes/Remains_RoundMap.unity";
 const string Out="output/castle-art-v3/";
 const string Dir="Assets/Remains/CastleArtV3/";
 public static void Inspect(){
  EditorSceneManager.OpenScene(ScenePath);
  Directory.CreateDirectory(Out);
  var root=GameObject.Find("Map_LargeCastle_Blockout");
  var text=string.Join("\n",root.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==root.transform || (t.parent!=null&&t.parent.name=="Castle_150x130m")).Select(t=>$"{t.name}: {t.position} rotation {t.eulerAngles}"));
  File.WriteAllText(Out+"scene-before.txt",text);
  var camera=root.transform.Find("OverviewCamera").GetComponent<Camera>();
  BattlefieldV2Builder.Capture(camera,Out+"map-before.png");
  File.WriteAllText(Out+"inspect.done","ok");
 }
}
}

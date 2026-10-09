using System.IO;
using UnityEditor;
namespace Remains.Editor {
 [InitializeOnLoad]
 public static class SiegeV4Tasks {
  const string Request="output/siege-v4/editor-task.txt";static double next;
  static SiegeV4Tasks(){EditorApplication.update+=Tick;File.WriteAllText("output/siege-v4/editor-version.txt","MESH_SAVE_V5");}
  static void Dump(){var lines=new System.Collections.Generic.List<string>();foreach(var t in UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(UnityEngine.FindObjectsSortMode.None)){if(t.name=="timber-leaf"||t.name=="Left_Hinge"||t.name=="Right_Hinge"||t.name=="timber-bridge"){lines.Add(t.name+" pos "+t.position+" rot "+t.eulerAngles);foreach(var r in t.GetComponentsInChildren<UnityEngine.Renderer>()){lines.Add("  "+r.name+" active "+r.enabled+" bounds "+r.bounds);if(lines.Count>80)break;}}}File.WriteAllLines("output/siege-v4/bounds.txt",lines);}
  static void Tick(){if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;next=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request))return;var task=File.ReadAllText(Request).Trim();File.Delete(Request);try{if(task=="stop"){EditorApplication.isPlaying=false;}else if(task=="save"){UnityEditor.AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());CastleArtUpgrade.ExportLandscape();}else if(task=="preview")SiegeV4Checks.Preview();else if(task=="check")SiegeV4Checks.Run();else if(task=="visuals")SiegeV4Builder.RepairVisuals();else if(task=="polish")SiegeV4Builder.Polish();else if(task=="chains")SiegeV4Builder.RepairChains();else if(task=="repair")SiegeV4Builder.RepairApproach();else if(task=="dump")Dump();else if(task=="build")SiegeV4Builder.Build();File.WriteAllText("output/siege-v4/task-result.txt",task+" OK");}catch(System.Exception e){File.WriteAllText("output/siege-v4/task-result.txt",task+" FAILED\n"+e);UnityEngine.Debug.LogException(e);}}
 }
}

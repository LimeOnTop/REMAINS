using UnityEditor;
using UnityEngine;
namespace Remains.Editor {
// Style V8 pass on the existing scene materials: no objects are moved or rebuilt, only shaders and palette change.
// Grass/ground/foliage/rock palettes follow the stylized (painterly, desaturated) references collected for the map.
public static class StyleV8 {
 const string Mats="Assets/Remains/MapRework/Materials/";
 static Material Load(string path){var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m)Debug.LogWarning("StyleV8: missing "+path);return m;}
 static Color C(double r,double g,double b)=>new Color((float)r,(float)g,(float)b,1);
 static void Foliage(string name,Color c,float gradient=.45f,float wind=.012f){var m=Load(Mats+name+".mat");if(!m)return;m.shader=Shader.Find("Remains/FoliageV8");m.SetColor("_BaseColor",c);m.SetFloat("_Gradient",gradient);m.SetFloat("_Wind",wind);EditorUtility.SetDirty(m);}
 static void Rock(string name,Color c,double moss){var m=Load(Mats+name+".mat");if(!m)return;m.shader=Shader.Find("Remains/RockV8");m.SetColor("_BaseColor",c);m.SetFloat("_Moss",(float)moss);m.SetColor("_MossColor",C(.36,.45,.19));EditorUtility.SetDirty(m);}
 static void Ground(string path){var m=Load(path);if(!m)return;m.SetFloat("_StyleV8",1);EditorUtility.SetDirty(m);}
 static void Grass(string path){var m=Load(path);if(!m)return;m.SetFloat("_WindStrength",.32f);m.SetColor("_RootColor",C(.17,.27,.11));m.SetColor("_TipColor",C(.50,.65,.29));m.SetColor("_DryColor",C(.70,.68,.38));m.SetColor("_LushColor",C(.40,.58,.24));EditorUtility.SetDirty(m);}
 [MenuItem("Remains/Map/Apply Style V8")]
 public static void Apply(){
  Foliage("Nature_Leaf_Dark",C(.17,.30,.14));Foliage("Nature_Leaf_Mid",C(.30,.45,.18));Foliage("Nature_Leaf_Light",C(.52,.63,.27));Foliage("Nature_Leaf_Autumn",C(.56,.60,.25));
  Foliage("Nature_Pine_Dark",C(.08,.20,.15),.55f,.008f);Foliage("Nature_Pine_Mid",C(.14,.30,.19),.55f,.008f);Foliage("Nature_Pine_Light",C(.27,.42,.21),.55f,.008f);
  Rock("Nature_Rock_Shadow",C(.37,.36,.33),.55f);Rock("Nature_Rock_Mid",C(.49,.47,.42),.7f);Rock("Nature_Rock_Light",C(.61,.58,.51),.8f);Rock("Nature_Rock_Moss",C(.35,.44,.19),1);
  Ground("Assets/Remains/RedesignV7/Coastal_Ground.mat");Ground(Mats+"Ground_V4.mat");Ground("Assets/Remains/MapPainting/OriginalPathOverlay.mat");
  Grass(Mats+"Grass_V4.mat");Grass("Assets/Remains/MapPainting/PaintedGrass.mat");
  AssetDatabase.SaveAssets();
 }
}
}

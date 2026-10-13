// Stylized foliage (style V8): soft "volume" normals from the mesh, wrapped two-tone light with a soft terminator,
// canopy gradient from shaded base to sunlit crown, back-lit translucency and a gentle wind sway that grows with height.
// Meshes are normalised (object y 0..~1), so object height drives the gradient and the sway.
Shader "Remains/FoliageV8" {
 Properties { _BaseColor("Colour",Color)=(.3,.45,.18,1) _ShadowTint("Shadow tint",Color)=(.42,.55,.62,1) _Gradient("Base to crown gradient",Range(0,1))=.45
  _Translucency("Back-lit glow",Range(0,1))=.35 _Wind("Wind sway (object units)",Float)=.012 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 struct A { float4 p:POSITION; float3 n:NORMAL; };
 struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; float3 n:TEXCOORD1; float h:TEXCOORD2; };
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor,_ShadowTint; float _Gradient,_Translucency,_Wind;
 CBUFFER_END
 V vert(A i){V o;float3 op=i.p.xyz;float h=saturate(op.y);float3 pivot=TransformObjectToWorld(float3(0,0,0));float t=_Time.y;
  float ph=dot(pivot.xz,float2(.11,.07));
  op.xz+=float2(sin(t*1.1+ph),cos(t*.83+ph*1.3))*_Wind*h*h+sin(t*3.3+op.y*9+ph)*_Wind*.25*h;
  float3 w=TransformObjectToWorld(op);o.p=TransformWorldToHClip(w);o.world=w;o.n=TransformObjectToWorldNormal(i.n);o.h=h;return o;}
 ENDHLSL
 Pass { Tags { "LightMode"="UniversalForward" }
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 half4 frag(V i):SV_Target{
  float3 n=normalize(i.n);Light l=GetMainLight(TransformWorldToShadowCoord(i.world));
  float wrap=dot(n,l.direction)*.5+.5;float lit=smoothstep(.42,.62,wrap)*l.shadowAttenuation;
  float3 col=_BaseColor.rgb*lerp(1-_Gradient*.45,1+_Gradient*.3,smoothstep(.25,1,i.h));
  float3 shadow=col*_ShadowTint.rgb*.78+SampleSH(n)*col*.45;
  float3 sun=col*lerp(half3(1,1,1),l.color,.4)*(.95+.15*wrap);
  float3 v=normalize(GetWorldSpaceViewDir(i.world));
  float back=pow(saturate(dot(-v,l.direction)),3)*_Translucency*(1-saturate(dot(n,v)))*l.shadowAttenuation;
  return half4(lerp(shadow,sun,lit)+col*back*float3(1,.95,.55),1);}
 ENDHLSL
 }
 // Same swaying vertex as the colour pass: URP depth priming tests the forward pass against this depth.
 Pass { Name "DepthOnly" Tags { "LightMode"="DepthOnly" } ZWrite On ColorMask R
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment depthFrag
 half depthFrag(V i):SV_Target{return 0;}
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}

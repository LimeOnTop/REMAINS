// Flat-shaded low-poly ground. Vertex colour is the base tint; uv0.x = path weight, uv0.y = sand weight.
// World-space procedural detail: grass patches with clover/flower speckles, packed-earth pebbles on paths, grain on sand.
Shader "Remains/GroundV4" {
 Properties { _BaseColor("Tint",Color)=(1,1,1,1) _Detail("Detail strength",Range(0,1))=1 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
 Pass { Tags { "LightMode"="UniversalForward" }
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 struct A { float4 p:POSITION; float3 n:NORMAL; float4 c:COLOR; float2 uv:TEXCOORD0; };
 struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; float3 n:TEXCOORD1; float4 c:COLOR; float2 uv:TEXCOORD2; };
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor; float _Detail;
 CBUFFER_END
 float hash(float2 p){ return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
 float vnoise(float2 p){ float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
  return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+float2(1,1)),f.x),f.y); }
 // Distance to nearest jittered cell point and that cell's random id.
 float2 cells(float2 p){ float2 i=floor(p);float best=9,id=0;
  for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){float2 c=i+float2(x,y);float2 q=c+float2(hash(c),hash(c+17.3))*.8+.1;float d=length(p-q);if(d<best){best=d;id=hash(c+5.1);}}
  return float2(best,id); }
 V vert(A i){V o;float3 w=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(w);o.world=w;o.n=TransformObjectToWorldNormal(i.n);o.c=i.c*_BaseColor;o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target{
  // Flat facet normal from screen derivatives keeps the low-poly look even on shared vertices.
  float3 n=normalize(cross(ddy(i.world),ddx(i.world)));if(dot(n,i.n)<0)n=-n;
  Light l=GetMainLight(TransformWorldToShadowCoord(i.world));
  float nd=saturate(dot(n,l.direction));
  float2 xz=i.world.xz;float3 col=i.c.rgb;
  float path=saturate(i.uv.x),sand=saturate(i.uv.y),grass=saturate(1-path-sand);
  // Grass: two scales of soft patches plus sparse clover / flower dots.
  float g=1+(vnoise(xz*.35)-.5)*.16+(vnoise(xz*1.7)-.5)*.10;
  float2 fc=cells(xz*2.2);float flower=step(fc.x,.07)*step(.93,fc.y);
  float3 fcol=fc.y>.975?float3(.95,.93,.80):(fc.y>.955?float3(.95,.80,.25):float3(.62,.52,.85));
  float3 gcol=lerp(col*g,fcol,flower*.85);
  float clover=step(fc.x,.16)*step(fc.y,.25);gcol*=1-clover*.12;
  // Path: packed earth with embedded pebbles and slightly darker wheel ruts.
  float2 pc=cells(xz*3.1);float pebble=1-smoothstep(.18,.26,pc.x);
  float3 pcol=col*(1+(vnoise(xz*2.5)-.5)*.18);pcol=lerp(pcol,col*(1.18+pc.y*.2),pebble*.7);pcol*=1-.10*(1-smoothstep(.0,.14,pc.x-.28));
  // Sand: fine grain and faint ripples.
  float3 scol=col*(1+(hash(floor(xz*14))-.5)*.08+sin(xz.x*1.3+xz.y*.7+vnoise(xz*.3)*4)*.03);
  float3 c=lerp(col,gcol*grass+pcol*path+scol*sand,_Detail);
  float3 lit=c*(.46+.54*nd*l.shadowAttenuation)*lerp(half3(1,1,1),l.color,.35);
  // Underwater caustics: two drifting cell layers; light gathers where their distance fields meet.
  float under=saturate((6.9-i.world.y)*1.5)*step(.5,_Detail);
  if(under>0){float t=_Time.y;float ca=cells(xz*.42+float2(sin(t*.31),cos(t*.27))*1.3).x;float cb=cells(xz*.58+float2(-t*.11,t*.09)).x;
   float caus=saturate(1-abs(ca-cb)*7);caus=caus*caus*caus;lit+=l.color*caus*under*.22*l.shadowAttenuation;lit*=lerp(1,.88,under);}
  return half4(lit,1);}
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}

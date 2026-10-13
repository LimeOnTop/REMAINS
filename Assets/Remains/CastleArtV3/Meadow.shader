// Stylized meadow (style V8): every blade fades from a dark olive root to a sun-warmed tip, large soft patches
// shift between lush and dry tints, travelling wind gusts bend and brighten the field, blades shrink where paths are painted.
// uv0.y = height along the blade (0 root, 1 tip). Vertex colour adds per-blade brightness variation.
Shader "Remains/MeadowV3" {
 Properties { _BaseColor("Tint",Color)=(1,1,1,1) _DetailStrength("Earth variation",Range(0,1))=0 _WindStrength("Grass wind",Float)=0 _Freshness("Fresh green",Range(0,1))=0 _PaintedGrass("Painted grass layer",Float)=0
  _RootColor("Root",Color)=(.16,.26,.11,1) _TipColor("Tip",Color)=(.60,.69,.31,1) _DryColor("Dry patch tip",Color)=(.74,.70,.38,1) _LushColor("Lush patch tip",Color)=(.40,.58,.24,1)
  _Translucency("Back-lit tips",Range(0,1))=.45 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
 Pass { Tags { "LightMode"="UniversalForward" } Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Assets/Remains/MapRework/MapPaint.hlsl"
 struct A { float4 p:POSITION; float3 n:NORMAL; float4 c:COLOR; float2 uv:TEXCOORD0; };
 struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; float3 n:TEXCOORD1; float4 c:COLOR; float2 hg:TEXCOORD2; };
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor; float _DetailStrength; float _WindStrength; float _Freshness; float _PaintedGrass;
 float4 _RootColor,_TipColor,_DryColor,_LushColor; float _Translucency;
 CBUFFER_END
 float hash(float2 p){ return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
 float vnoise(float2 p){ float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
  return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+float2(1,1)),f.x),f.y); }
 V vert(A i){V o;float3 w=TransformObjectToWorld(i.p.xyz);float h=saturate(i.uv.y);float t=_Time.y;
  // Gust fronts roll across the field along one direction; a faster flutter keeps single blades alive.
  float2 dir=normalize(float2(1,.35));
  float gust=sin(dot(w.xz,dir)*.16-t*1.6+vnoise(w.xz*.04)*5)*.5+.5;gust*=gust;
  float flutter=sin(w.x*1.7+w.z*1.3+t*3.1)*.3;
  float2 bend=dir*(gust*1.1+flutter+.25)*_WindStrength*h*h;
  w.xz+=bend;w.y-=length(bend)*.35*h;
  // Trampled border: blades lie lower where the paint texture marks a path.
  float4 pp=RemainsPaintLod(w.xz);w.y-=h*saturate((pp.g+pp.b)*pp.a*1.5)*.32;
  o.p=TransformWorldToHClip(w);o.world=w;o.n=TransformObjectToWorldNormal(i.n);o.c=i.c*_BaseColor;o.hg=float2(h,gust);return o;}
 half4 frag(V i):SV_Target{float4 paint=RemainsPaint(i.world.xz);if(_PaintedGrass>.5)clip(paint.r*paint.a-.16);else {float dither=frac(sin(dot(floor(i.world.xz*10),float2(12.9898,78.233)))*43758.5453);clip(1-paint.a-dither*.98);}
  float h=i.hg.x;float2 xz=i.world.xz;
  float patch=vnoise(xz*.035)*.65+vnoise(xz*.11)*.35;
  float3 tip=lerp(_LushColor.rgb,_TipColor.rgb,smoothstep(.3,.5,patch));tip=lerp(tip,_DryColor.rgb,smoothstep(.68,.85,patch));
  float3 col=lerp(_RootColor.rgb,tip,pow(max(h,0),1.15));
  // Per-blade variation taken from the authored vertex colour (its brightness relative to the expected gradient).
  float lum=dot(i.c.rgb,float3(.3,.59,.11));col*=clamp(lum/lerp(.26,.53,h),.86,1.14);
  col*=1+i.hg.y*.12*h;
  Light l=GetMainLight(TransformWorldToShadowCoord(i.world));
  // Field-like lighting: normals bent toward up so the meadow shades as one soft surface.
  float3 n=normalize(lerp(normalize(i.n),float3(0,1,0),.7));float nd=saturate(dot(n,l.direction));
  float3 v=normalize(GetWorldSpaceViewDir(i.world));float back=pow(saturate(dot(-v,l.direction)),4)*h*_Translucency;
  float3 lit=col*(.5+.5*nd*l.shadowAttenuation)*lerp(half3(1,1,1),l.color,.35)+col*back*l.shadowAttenuation*float3(1,.95,.6);
  return half4(lit,1);}
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}

// Flat-shaded low-poly ground. Vertex colour is the base tint; uv0.x = path weight, uv0.y = sand weight.
// World-space procedural detail: grass patches with clover/flower speckles, packed-earth pebbles on paths, grain on sand.
Shader "Remains/GroundV4" {
 Properties { _BaseColor("Tint",Color)=(1,1,1,1) _Detail("Detail strength",Range(0,1))=1 _Freshness("Fresh green",Range(0,1))=0 _PaintOverlay("Existing path overlay",Float)=0
  _StyleV8("Style V8 palette and worn paths",Float)=0 _GrassA("Ground under grass A",Color)=(.27,.38,.15,1) _GrassB("Ground under grass B",Color)=(.38,.46,.19,1)
  _DirtDark("Path dirt dark",Color)=(.40,.31,.20,1) _DirtLight("Path dirt light",Color)=(.62,.51,.35,1) _PebbleColor("Path pebbles",Color)=(.70,.66,.58,1) }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
 Pass { Tags { "LightMode"="UniversalForward" }
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Assets/Remains/MapRework/MapPaint.hlsl"
 struct A { float4 p:POSITION; float3 n:NORMAL; float4 c:COLOR; float2 uv:TEXCOORD0; };
 struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; float3 n:TEXCOORD1; float4 c:COLOR; float2 uv:TEXCOORD2; };
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor; float _Detail;float _Freshness;float _PaintOverlay;
 float _StyleV8;float4 _GrassA,_GrassB,_DirtDark,_DirtLight,_PebbleColor;
 CBUFFER_END
 float hash(float2 p){ return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
 float vnoise(float2 p){ float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
  return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+float2(1,1)),f.x),f.y); }
 // Distance to nearest jittered cell point and that cell's random id.
 float2 cells(float2 p){ float2 i=floor(p);float best=9,id=0;
  for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){float2 c=i+float2(x,y);float2 q=c+float2(hash(c),hash(c+17.3))*.8+.1;float d=length(p-q);if(d<best){best=d;id=hash(c+5.1);}}
  return float2(best,id); }
 // Nearest jittered cell point: xy = offset from it, z = distance, w = cell id.
 float4 cellv(float2 p){ float2 i=floor(p);float4 best=float4(0,0,9,0);
  for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){float2 c=i+float2(x,y);float2 q=c+float2(hash(c),hash(c+17.3))*.8+.1;float d=length(p-q);if(d<best.z)best=float4(p-q,d,hash(c+5.1));}
  return best; }
 V vert(A i){V o;float3 w=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(w);o.world=w;o.n=TransformObjectToWorldNormal(i.n);o.c=i.c*_BaseColor;o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target{
  // Flat facet normal from screen derivatives keeps the low-poly look even on shared vertices.
  float3 n=normalize(cross(ddy(i.world),ddx(i.world)));if(dot(n,i.n)<0)n=-n;
  Light l=GetMainLight(TransformWorldToShadowCoord(i.world));
  float nd=saturate(dot(n,l.direction));
  float2 xz=i.world.xz;float3 col=i.c.rgb;
  float path=saturate(i.uv.x),sand=saturate(i.uv.y),grass=saturate(1-path-sand);
  col=lerp(col,col*float3(.78,1.18,.95),_Freshness*grass);
  float4 paint=RemainsPaint(xz);
  if(_PaintOverlay>.5)clip(.05-paint.a);
  float3 paintColor=paint.r*float3(.29,.48,.11)+paint.g*float3(.57,.405,.205)+paint.b*float3(.33,.235,.135);
  col=lerp(col,paintColor,paint.a);
  path=lerp(path,paint.g+paint.b,paint.a);sand*=1-paint.a;grass=saturate(1-path-sand);
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
  if(_StyleV8>.5){
   // V8: grass ground matches the blade roots; paths are worn dirt with a ragged, grass-flecked edge and scattered pebbles.
   float n1=vnoise(xz*.45)*.6+vnoise(xz*1.8)*.4;
   gcol=lerp(_GrassA.rgb,_GrassB.rgb,smoothstep(.25,.75,vnoise(xz*.06)*.7+n1*.3))*(.92+.16*vnoise(xz*3.1));
   gcol=lerp(gcol,fcol,flower*.85);gcol*=1-clover*.1;
   float pe=saturate((path-.5)*2.4+.5+(n1-.5)*.8);
   float3 dirt=lerp(_DirtDark.rgb,_DirtLight.rgb,saturate(vnoise(xz*.18)*.55+vnoise(xz*.9)*.25+path*.35-.12));
   dirt*=.93+.1*vnoise(xz*5.3)+.05*vnoise(xz*13.7);
   float2 sp=xz*2.1+(float2(vnoise(xz*3.3),vnoise(xz*3.3+7.7))-.5)*.5;float4 cv=cellv(sp);float has=step(.86,cv.w);float rad=.11+.12*frac(cv.w*37.1);
   float ang=atan2(cv.y,cv.x);float rr=rad*(.8+.2*sin(ang*3+cv.w*40)+.1*sin(ang*5+cv.w*17));
   float stone=has*(1-smoothstep(rr-.025,rr,cv.z));
   float ring=has*smoothstep(rr-.025,rr,cv.z)*(1-smoothstep(rr,rr+.07,cv.z));
   float2 ld=normalize(l.direction.xz+1e-4);float shade=.62+.55*saturate(dot(cv.xy/rr,ld)*.5+.5);
   dirt*=lerp(.8,1,pe);dirt*=1-.3*ring;dirt=lerp(dirt,lerp(dirt,_PebbleColor.rgb,.7)*(.85+.25*frac(cv.w*13.7))*shade,stone*smoothstep(.3,.8,pe));
   dirt*=1-.07*smoothstep(.45,.7,vnoise(float2(xz.x*.7,xz.y*.7)+vnoise(xz*.2)*3));
   // Cart ruts painted into the blue channel of the paint layer.
   float rut=paint.a*paint.b/max(paint.g+paint.b,.02);dirt*=1-.26*rut*(.7+.3*vnoise(xz*1.7));
   // Ragged transition: grass clumps (two noise scales) spill into the dirt, the dirt rim is damper and darker.
   float bits=step(vnoise(xz*2.1)*.6+vnoise(xz*7.3)*.4,(1-pe)*1.05);
   dirt*=lerp(.84,1,smoothstep(.12,.55,pe));
   pcol=lerp(dirt,gcol*.86,bits*(1-pe));
   // Trampled, slightly dry grass in a halo just outside the trail.
   float halo=smoothstep(.02,.4,path)*(1-pe);gcol=lerp(gcol,gcol*float3(1.1,.97,.74),halo*.65);
   path=pe*(1-sand);grass=saturate(1-path-sand);
  }
  float3 c=lerp(col,gcol*grass+pcol*path+scol*sand,max(_Detail,_StyleV8));
  // V8: steep faces (cliff skirts, cape sides) read as layered rock instead of vertical grass.
  if(_StyleV8>.5){float steep=1-smoothstep(.38,.62,n.y);float band=frac(i.world.y*.55+vnoise(xz*.2)*.8);
   float3 rk=lerp(float3(.17,.16,.14),float3(.30,.29,.26),smoothstep(.2,.8,band))*(.9+.2*vnoise(xz*1.3+i.world.y));c=lerp(c,rk,steep*(1-sand*.6));}
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

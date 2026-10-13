#ifndef REMAINS_MAP_PAINT_INCLUDED
#define REMAINS_MAP_PAINT_INCLUDED
TEXTURE2D(_RemainsMapPaint); SAMPLER(sampler_RemainsMapPaint);
float4 _RemainsMapPaintRect;
float _RemainsPaintEnabled;
float4 RemainsPaint(float2 worldXZ) {
 if(_RemainsPaintEnabled<.5)return 0;
 float2 uv=(worldXZ-_RemainsMapPaintRect.xy)/_RemainsMapPaintRect.zw;
 if(any(uv<0)||any(uv>1))return 0;
 return SAMPLE_TEXTURE2D(_RemainsMapPaint,sampler_RemainsMapPaint,uv);
}
// Vertex-stage variant (explicit LOD), used by grass to flatten blades at path edges.
float4 RemainsPaintLod(float2 worldXZ) {
 if(_RemainsPaintEnabled<.5)return 0;
 float2 uv=(worldXZ-_RemainsMapPaintRect.xy)/_RemainsMapPaintRect.zw;
 if(any(uv<0)||any(uv>1))return 0;
 return SAMPLE_TEXTURE2D_LOD(_RemainsMapPaint,sampler_RemainsMapPaint,uv,0);
}
#endif

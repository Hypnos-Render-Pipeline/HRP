#pragma raytracing test

$SurfaceDescriptionInputs.TimeParameters:			float4 _Time;

#undef SAMPLE_TEXTURE2D
#define SAMPLE_TEXTURE2D(textureName, samplerName, coord2)		textureName.SampleLevel(samplerName, coord2, 0) 
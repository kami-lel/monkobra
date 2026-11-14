Shader "Monkobra/FallingSnakeSurface" {
    Properties {
        [MainTexture] _BaseMap("Original texture", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _SkinColor("Skin", Color) = (0.12,0.55,0.025,1)
        _BellyColor("Belly", Color) = (0.9,0.75,0.2,1)
        _SpotColor("Body markings", Color) = (0.34,0.70,0.06,1)
        _SmoothBody("Smooth body pattern", Range(0,1)) = 1
        _HeadWatch("Raised watch pose", Range(0,1)) = 0
        _BellyThreshold("Belly stripe narrowness", Range(0,0.95)) = .85
        _YellowRetention("Original yellow retained", Range(0,1)) = .45
        _HeadBellyPhase("Head belly alignment", Float) = 0
        _CollarLength("Head palette transition length", Float) = .75
        _BodyLength("Body length", Float) = 5.8
        _NeckTextureBlendU("Original neck texture blend distance", Float) = .65
        _TreeAxis("Tree axis", Vector) = (0,0,0,0)
        _HeadBellyDirection("Head underside direction", Vector) = (0,-1,0,0)
    }
    SubShader {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
        Pass {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _SkinColor, _BellyColor, _SpotColor;
                half _SmoothBody, _HeadWatch, _BellyThreshold, _YellowRetention;
                float _BodyLength, _NeckTextureBlendU, _HeadBellyPhase, _CollarLength;
                float4 _TreeAxis, _HeadBellyDirection;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float2 neckUV:TEXCOORD1;float3 bellyDirectionOS:TEXCOORD2;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half body:TEXCOORD3;half fog:TEXCOORD4;half3 bellyDirectionWS:TEXCOORD5;half3 collar:TEXCOORD6;};
            Varyings Vert(Attributes input) {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=TRANSFORM_TEX(input.uv,_BaseMap);
                output.bellyDirectionWS=TransformObjectToWorldDir(input.bellyDirectionOS,false);
                output.body=input.color.r;
                output.collar=half3(input.color.g,input.color.b*2-1,input.color.a*2-1);
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half3 CorrectSourceSkin(half3 original,half3 normal,float3 bellyDirection) {
                half yellow=smoothstep(.65h,.8h,original.r/max(original.g,.001h))
                    *(1-smoothstep(.45h,.65h,original.b/max(original.g,.001h)));
                yellow*=smoothstep(.08h,.18h,max(original.r,original.g));
                half underside=smoothstep(_BellyThreshold,.99h,dot(normal,bellyDirection));
                return lerp(original,_SkinColor.rgb,yellow*(1-underside*_YellowRetention));
            }
            half4 Frag(Varyings input):SV_Target {
                half3 normal=normalize(input.normalWS);
                float3 viewDirection=normalize(_WorldSpaceCameraPos-input.positionWS);
                float3 surfaceFacing=normalize(cross(ddx(input.positionWS),ddy(input.positionWS)));
                if(dot(surfaceFacing,viewDirection)<0)surfaceFacing=-surfaceFacing;
                normal=normalize(lerp(surfaceFacing,normal,smoothstep(-.2,.15,dot(normal,viewDirection))));
                float3 inward=normalize(float3(_TreeAxis.x-input.positionWS.x,0,_TreeAxis.z-input.positionWS.z));
                half3 bellyDirection=normalize(lerp(_HeadBellyDirection.xyz,input.bellyDirectionWS,input.body));
                half3 original=CorrectSourceSkin(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb,normal,bellyDirection);
                // Both sides of the cut share the same palette and angular stripe at the join.
                float collarAngle=atan2(input.collar.z,input.collar.y)/6.2831853;
                float2 patternUV=lerp(float2(collarAngle,0),input.uv,input.body);
                float palettePhase=lerp(1,1-smoothstep(0,max(.001,_CollarLength),input.uv.y*_BodyLength),input.body);
                patternUV.x+=_HeadBellyPhase*palettePhase;
                float row=floor(patternUV.y*6);
                float2 marking=float2(frac(patternUV.x*2+frac(row*.5)*.5)-.5,frac(patternUV.y*6)-.5)/float2(.3,.34);
                half spot=1-smoothstep(.65,1,dot(marking,marking));
                half3 green=lerp(_SkinColor.rgb,_SpotColor.rgb,spot*.8);
                half belly=smoothstep(_BellyThreshold,.99h,dot(normal,bellyDirection));

                half3 body=lerp(green,_BellyColor.rgb,belly);
                half procedural=max(input.body,input.collar.x);
                half3 albedo=lerp(original,body,procedural)*_BaseColor.rgb;
                Light light=GetMainLight();
                half3 lighting=SampleSH(normal)+light.color*saturate(dot(normal,light.direction));
                return half4(MixFog(albedo*lighting,input.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}

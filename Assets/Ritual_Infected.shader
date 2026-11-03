Shader "UI/Ritual_Balatro_Pro"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        
        _Mode ("Mode_0_Metal_1_Stone", Float) = 0.0
        _Contrast ("Contrast_Intensity", Range(0, 3)) = 1.5
        _MatCap ("MatCap_Sphere", 2D) = "gray" {}
        _FoilShininess ("Reflection_Intensity", Range(0, 5)) = 2.0
        _MetallicBrightness ("Brightness_Boost", Range(0, 2)) = 1.0
        _NoiseTex ("Surface_Grain_Noise", 2D) = "white" {}
        _Roughness ("Stone_Roughness", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off Lighting Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float2 screenUV : TEXCOORD1; fixed4 color : COLOR; };

            sampler2D _MainTex; sampler2D _MatCap; sampler2D _NoiseTex;
            float _Mode; float _Contrast; float _FoilShininess; float _MetallicBrightness; float _Roughness; fixed4 _Color;

            v2f vert(appdata_t v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                // Tính tọa độ màn hình để hiệu ứng bạc "chạy" khi di chuyển bài
                float4 screenPos = ComputeScreenPos(o.vertex);
                o.screenUV = screenPos.xy / screenPos.w;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                fixed4 tex = tex2D(_MainTex, i.uv);
                // Khử màu ảnh gốc (Grayscale)
                float gray = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                
                float noise = tex2D(_NoiseTex, i.uv).r;
                float3 finalRGB;

                if (_Mode < 0.5) { 
                    // CHẾ ĐỘ KIM LOẠI (METAL/FOIL)
                    float3 refl = tex2D(_MatCap, i.screenUV).rgb;
                    float3 baseMetal = float3(gray, gray, gray) * i.color.rgb;
                    // Phép cộng ánh sáng phản chiếu để tạo độ loáng bóng sắc cạnh
                    finalRGB = baseMetal + (refl * _FoilShininess * _MetallicBrightness);
                } else { 
                    // CHẾ ĐỘ ĐÁ (STONE)
                    float3 stoneBase = lerp(tex.rgb, float3(noise, noise, noise), _Roughness);
                    finalRGB = float3(gray, gray, gray) * stoneBase * i.color.rgb;
                }

                // Tăng độ tương phản để tạo cảm giác "khối" kim loại/đá
                finalRGB = ((finalRGB - 0.5) * _Contrast) + 0.5;

                return fixed4(finalRGB, tex.a * i.color.a);
            }
            ENDCG
        }
    }
}
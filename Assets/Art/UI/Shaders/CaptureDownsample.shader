// 휴대전화 화면 캡처처럼 글자가 많은 큰 이미지를 작게 표시할 때 쓰는 UI 셰이더.
// 화면 픽셀 하나가 덮는 원본 텍셀 영역(ddx/ddy)을 여러 지점에서 샘플해 평균한다(밉맵 없이도 계단·끊김 없이 축소).
// 평균은 감마 공간에서 해 흰 바탕의 가는 한글 획이 옅어지지 않게 한다. 확대(1배 이상)에서는 보통 bilinear와 같다.
// RectMask2D·Mask(스텐실)는 기본 UI 셰이더와 같이 지원한다.
Shader "Intersection/UI/Capture Downsample"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MaxTaps ("Max taps per axis", Range(1, 8)) = 6

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _MaxTaps;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float3 ToGamma(float3 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return LinearToGammaSpace(c);
            #endif
            }

            float3 FromGamma(float3 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return GammaToLinearSpace(c);
            #endif
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                // 화면 픽셀 하나가 덮는 원본 텍셀 수 (가로·세로)
                float2 dx = ddx(uv) * _MainTex_TexelSize.zw;
                float2 dy = ddy(uv) * _MainTex_TexelSize.zw;
                float fw = max(abs(dx.x), abs(dy.x));
                float fh = max(abs(dx.y), abs(dy.y));
                int nx = (int)clamp(ceil(fw), 1, _MaxTaps);
                int ny = (int)clamp(ceil(fh), 1, _MaxTaps);
                float2 span = float2(max(fw, 1), max(fh, 1)) * _MainTex_TexelSize.xy;
                float2 stepUV = span / float2(nx, ny);
                float2 start = uv - 0.5 * span + 0.5 * stepUV;

                float3 rgb = 0;
                float alpha = 0;
                [loop] for (int y = 0; y < ny; y++)
                {
                    [loop] for (int x = 0; x < nx; x++)
                    {
                        float4 c = tex2Dlod(_MainTex, float4(start + stepUV * float2(x, y), 0, 0));
                        rgb += ToGamma(c.rgb);
                        alpha += c.a;
                    }
                }
                float n = nx * ny;
                half4 color = half4(FromGamma(rgb / n), alpha / n);
                color = (color + _TextureSampleAdd) * i.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
        ENDCG
        }
    }
}

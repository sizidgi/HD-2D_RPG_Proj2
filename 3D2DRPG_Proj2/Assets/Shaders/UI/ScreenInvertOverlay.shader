Shader "RyotaSuzuki/UI/ScreenInvertOverlay"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Lighting Off
        Cull Off
        ZWrite Off
        ZTest Always
        Blend OneMinusDstColor OneMinusDstColor

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag() : SV_Target
            {
                return fixed4(_Intensity, _Intensity, _Intensity, _Intensity);
            }
            ENDCG
        }
    }
}

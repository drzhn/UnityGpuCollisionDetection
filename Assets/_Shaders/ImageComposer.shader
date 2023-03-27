Shader "Hidden/ImageComposer"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ObjectTexture ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            sampler2D _ObjectTexture;

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 colObject = tex2D(_ObjectTexture, i.uv);

                fixed3 ret = lerp(col.rgb, colObject.rgb, colObject.a);
                
                // col.rgb = 1 - col.rgb;
                return fixed4(ret,1);
            }
            ENDCG
        }
    }
}
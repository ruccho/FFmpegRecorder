Shader "Unlit/Sketch0"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
				float4 viewport : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
			float4 _MainTex_TexelSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
				o.viewport = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                //fixed4 col = tex2D(_MainTex, i.uv);

				float2 sc = i.viewport * _ScreenParams.xy;

				float2 sc_r = sc + float2(sin(_Time.y * 20 - sc.y * 0.023), 0) * 10;
				float2 sc_g = sc + float2(sin(_Time.y * -13 + sc.y * 0.013), 0) * 10;
				float2 sc_b = sc + float2(sin(_Time.y * 5 + sc.y * 0.019), 0) * 10;

				float2 sc_center = _ScreenParams.xy * 0.5;

				float rad = min(_ScreenParams.x, _ScreenParams.y) * 0.8 * 0.5;

				float c_r = step(distance(sc_r, sc_center), rad);
				float c_g = step(distance(sc_g, sc_center), rad);
				float c_b = step(distance(sc_b, sc_center), rad);

				fixed4 c = fixed4(c_r, c_g, c_b, 1);

				fixed4 bg = fixed4(i.viewport.xy, 0, 1);


				return lerp(bg, c, step(0.01, c_r+c_g+c_b));

                // apply fog
                //UNITY_APPLY_FOG(i.fogCoord, col);
                //return col;
            }
            ENDCG
        }
    }
}

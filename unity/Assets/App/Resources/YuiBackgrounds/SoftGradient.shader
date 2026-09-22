Shader "Yui/Background/SoftGradient"
{
    Properties
    {
        _BottomColor ("Bottom", Vector) = (.115,.125,.165,1)
        _TopColor ("Top", Vector) = (.075,.084,.115,1)
        _GlowColor ("Glow", Vector) = (.18,.165,.21,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            half4 _BottomColor, _TopColor, _GlowColor;
            struct v2f { float4 position : SV_POSITION; float4 screen : TEXCOORD0; };
            v2f vert(float4 vertex : POSITION)
            {
                v2f o; o.position=UnityObjectToClipPos(vertex); o.screen=ComputeScreenPos(o.position); return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv=i.screen.xy/i.screen.w;
                // One static background pass. No texture, blur, time, lights or render texture.
                half3 color=lerp(_BottomColor.rgb,_TopColor.rgb,smoothstep(0,1,uv.y));
                float2 light=(uv-float2(.48,.60))*float2(1.30,1.05);
                half halo=saturate(1-dot(light,light)*2.35); halo=halo*halo*halo;
                color+=_GlowColor.rgb*halo;
                half floorGlow=1-smoothstep(.02,.23,abs(uv.y-.12));
                color+=half3(.035,.04,.052)*floorGlow*(1-abs(uv.x-.5));
                #ifndef UNITY_COLORSPACE_GAMMA
                    color=GammaToLinearSpace(color);
                #endif
                return fixed4(color,1);
            }
            ENDCG
        }
    }
    Fallback Off
}

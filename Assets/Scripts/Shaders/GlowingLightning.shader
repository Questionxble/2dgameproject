Shader "Custom/GlowingLightning" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Lightning Color", Color) = (0.3, 0.7, 1.0, 1.0)
        _EmissionColor ("Emission Color", Color) = (0.5, 1.5, 3.0, 1.0)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 3.0
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
        _Width ("Line Width", Range(0.1, 2.0)) = 1.0
    }
    
    SubShader {
        Tags { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }
        
        Blend SrcAlpha One // Additive blending for bright glow
        ZWrite Off
        Cull Off
        
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            
            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct v2f {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _EmissionColor;
            float _GlowIntensity;
            float _PulseSpeed;
            float _Width;
            
            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target {
                // Sample the texture (usually white for LineRenderer)
                fixed4 tex = tex2D(_MainTex, i.uv);
                
                // Create pulsing effect using time
                float pulse = sin(_Time.y * _PulseSpeed) * 0.2 + 0.8; // Pulse between 0.6 and 1.0
                
                // Calculate distance from center for glow falloff
                float distFromCenter = abs(i.uv.y - 0.5) * 2.0; // 0 at center, 1 at edges
                
                // Enhanced glow falloff with softer edges
                float coreGlow = 1.0 - smoothstep(0.0, 0.3, distFromCenter); // Sharp core
                float outerGlow = 1.0 - smoothstep(0.3, _Width, distFromCenter); // Soft outer glow
                float combinedGlow = coreGlow + outerGlow * 0.5; // Combine both layers
                
                // Base lightning color with vertex color support
                fixed4 baseColor = _Color * i.color * tex;
                
                // Emission with pulsing and enhanced glow
                float emissionStrength = _GlowIntensity * pulse * combinedGlow;
                fixed4 emission = _EmissionColor * emissionStrength;
                
                // Combine colors with enhanced brightness
                fixed4 finalColor = baseColor * coreGlow + emission;
                
                // Enhanced alpha with better falloff for outer glow
                finalColor.a = combinedGlow * tex.a * baseColor.a;
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, finalColor);
                
                return finalColor;
            }
            ENDCG
        }
    }
    
    // Fallback for older hardware
    FallBack "Sprites/Default"
}
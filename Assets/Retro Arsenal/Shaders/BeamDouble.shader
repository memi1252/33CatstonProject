// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Archanor VFX/Retro Arsenal/BeamDouble"
{
	Properties
	{
		_TextureSample("Texture Sample", 2D) = "white" {}
		_TextureSample2("Texture Sample #2", 2D) = "white" {}
		_Tint("Tint", Color) = (0,0,0,0)
		_ExtraGlow("Extra Glow", Range( 0 , 15)) = 0
		_ScrollSpeed("Scroll Speed", Vector) = (-5,0,0,0)
		_ScrollSpeed2("Scroll Speed #2", Vector) = (-25,0,0,0)
	}

	SubShader
	{
		Tags 
		{ 
			"RenderType" = "Transparent"  
			"Queue" = "Transparent+0" 
			"RenderPipeline" = "UniversalPipeline"
			"IgnoreProjector" = "True" 
		}
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		Cull Off

		Pass
		{
			Name "ForwardLit"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct Attributes
			{
				float4 positionOS   : POSITION;
				float4 color        : COLOR;
				float2 uv           : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionHCS  : SV_POSITION;
				float4 color        : COLOR;
				float2 uv           : TEXCOORD0;
			};

			TEXTURE2D(_TextureSample);
			SAMPLER(sampler_TextureSample);
			float4 _TextureSample_ST;

			TEXTURE2D(_TextureSample2);
			SAMPLER(sampler_TextureSample2);
			float4 _TextureSample2_ST;

			CBUFFER_START(UnityPerMaterial)
				float4 _Tint;
				float _ExtraGlow;
				float2 _ScrollSpeed;
				float2 _ScrollSpeed2;
			CBUFFER_END

			Varyings vert(Attributes input)
			{
				Varyings output;
				output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
				output.color = input.color;
				output.uv = input.uv;
				return output;
			}

			half4 frag(Varyings input) : SV_Target
			{
				float time = _Time.y;
				
				float2 uv_1 = input.uv * _TextureSample_ST.xy + _TextureSample_ST.zw;
				float2 panner_1 = uv_1 + time * _ScrollSpeed;
				half4 tex1 = SAMPLE_TEXTURE2D(_TextureSample, sampler_TextureSample, panner_1);

				float2 uv_2 = input.uv * _TextureSample2_ST.xy + _TextureSample2_ST.zw;
				float2 panner_2 = uv_2 + (time * 1.25) * _ScrollSpeed2;
				half4 tex2 = SAMPLE_TEXTURE2D(_TextureSample2, sampler_TextureSample2, panner_2);

				half3 emission = (input.color.rgb * _ExtraGlow) * _Tint.rgb * tex1.rgb * tex2.rgb;
				half alpha = saturate(input.color.a * tex1.a * tex2.a);

				return half4(emission, alpha);
			}
			ENDHLSL
		}
	}
	Fallback "Sprites/Default"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.RangedFloatNode;16;-1580.367,531.3124;Inherit;False;Constant;_Float2;Float 2;13;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;23;-1425.019,1061.05;Inherit;False;Constant;_Float3;Float 2;13;0;Create;True;0;0;0;False;0;False;1.25;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;15;-1399.762,529.4488;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;12;-1399.062,322.1223;Inherit;False;Property;_ScrollSpeed;Scroll Speed;4;0;Create;True;0;0;0;False;0;False;-5,0;-1.5,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleTimeNode;20;-1244.414,1059.186;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;21;-1243.714,851.8599;Inherit;False;Property;_ScrollSpeed2;Scroll Speed #2;5;0;Create;True;0;0;0;False;0;False;-25,0;-1.5,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;31;-1373.348,703.4696;Inherit;False;0;18;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;14;-1370.062,170.1225;Inherit;False;0;9;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PannerNode;13;-1093.348,298.4073;Inherit;True;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;19;-938,828.1448;Inherit;True;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.VertexColorNode;3;-900.6292,-292.9637;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;4;-930.9111,-105.7614;Inherit;False;Property;_ExtraGlow;Extra Glow;3;0;Create;True;0;0;0;False;0;False;0;3;0;15;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;18;-697.1329,596.6971;Inherit;True;Property;_TextureSample2;Texture Sample #2;1;0;Create;True;0;0;0;False;0;False;-1;None;5f1578c4b39c9b149886e98b247865ea;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;9;-726.5852,326.027;Inherit;True;Property;_TextureSample;Texture Sample;0;0;Create;True;0;0;0;False;0;False;-1;None;5f1578c4b39c9b149886e98b247865ea;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;7;-626.4273,-174.2883;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;6;-739.84,71.7299;Inherit;False;Property;_Tint;Tint;2;0;Create;True;0;0;0;False;0;False;0,0,0,0;1,1,1,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;35;-302.6595,613.1166;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;8;-441.4772,47.30261;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;11;-140.2759,301.007;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;10;-198.9482,49.04736;Inherit;False;3;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;17;-1556.863,194.402;Inherit;False;Constant;_Tiling;Tiling;12;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;30;-1560.149,727.7491;Inherit;False;Constant;_Tiling2;Tiling #2;12;0;Create;True;0;0;0;False;0;False;2;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;32;-292.1506,493.9886;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;34;46.2068,247.7435;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;216.0826,44.12647;Float;False;True;-1;2;AmplifyShaderEditor.MaterialInspector;0;0;Unlit;Archanor VFX/Retro Arsenal/BeamDouble;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Off;0;False;;0;False;;False;0;False;;0;False;;False;0;Transparent;0.5;True;True;0;False;Transparent;;Transparent;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;2;5;False;;10;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;15;0;16;0
WireConnection;20;0;23;0
WireConnection;31;0;30;0
WireConnection;14;0;17;0
WireConnection;13;0;14;0
WireConnection;13;2;12;0
WireConnection;13;1;15;0
WireConnection;19;0;31;0
WireConnection;19;2;21;0
WireConnection;19;1;20;0
WireConnection;18;1;19;0
WireConnection;9;1;13;0
WireConnection;7;0;3;0
WireConnection;7;1;4;0
WireConnection;35;0;9;4
WireConnection;35;1;18;4
WireConnection;8;0;7;0
WireConnection;8;1;6;0
WireConnection;11;0;3;4
WireConnection;11;1;35;0
WireConnection;10;0;8;0
WireConnection;10;1;9;0
WireConnection;10;2;18;0
WireConnection;32;0;9;4
WireConnection;32;1;18;4
WireConnection;34;0;11;0
WireConnection;0;2;10;0
WireConnection;0;9;34;0
ASEEND*/
//CHKSM=45E5C739793E04581ED54B0CF3502E2111629E69
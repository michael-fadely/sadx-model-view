using SharpDX;
using SharpDX.Mathematics.Interop;

namespace sadx_model_view
{
	public struct ShaderMaterial
	{
		public static int SizeInBytes => 2 * Vector4.SizeInBytes + sizeof(float) + 5 * sizeof(int);
		public RawColor4 Diffuse;
		public RawColor4 Specular;
		public float Exponent;
		public bool UseLight;
		public bool UseAlpha;
		public bool UseEnv;
		public bool UseTexture;
		public bool UseSpecular;

		public bool Equals(ShaderMaterial other)
		{
			return Diffuse.Equals(other.Diffuse)
			       && Specular.Equals(other.Specular)
			       && Exponent.Equals(other.Exponent)
			       && UseLight == other.UseLight
			       && UseAlpha == other.UseAlpha
			       && UseEnv == other.UseEnv
			       && UseTexture == other.UseTexture
			       && UseSpecular == other.UseSpecular;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
				return false;
			return obj is ShaderMaterial && Equals((ShaderMaterial)obj);
		}

		public static bool operator==(ShaderMaterial lhs, ShaderMaterial rhs)
		{
			return lhs.Equals(rhs);
		}

		public static bool operator !=(ShaderMaterial lhs, ShaderMaterial rhs)
		{
			return !(lhs == rhs);
		}

		public override int GetHashCode() => 1;
	}
}
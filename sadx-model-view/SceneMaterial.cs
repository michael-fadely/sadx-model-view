using sadx_model_view.Extensions;
using sadx_model_view.Extensions.SharpDX;
using SharpDX;

namespace sadx_model_view
{
	public struct SceneMaterial : ICBuffer
	{
		public Color4 Diffuse;
		public Color4 Specular;
		public float  Exponent;
		public bool   UseLight;
		public bool   UseAlpha;
		public bool   UseEnv;
		public bool   UseTexture;
		public bool   UseSpecular;

		public bool Equals(SceneMaterial other)
		{
			return Diffuse == other.Diffuse
			       && Specular == other.Specular
			       && Exponent.NearEqual(other.Exponent)
			       && UseLight == other.UseLight
			       && UseAlpha == other.UseAlpha
			       && UseEnv == other.UseEnv
			       && UseTexture == other.UseTexture
			       && UseSpecular == other.UseSpecular;
		}

		public override bool Equals(object? obj)
		{
			if (obj is null)
			{
				return false;
			}

			return obj is SceneMaterial material && Equals(material);
		}

		public static bool operator ==(SceneMaterial lhs, SceneMaterial rhs)
		{
			return lhs.Equals(rhs);
		}

		public static bool operator !=(SceneMaterial lhs, SceneMaterial rhs)
		{
			return !(lhs == rhs);
		}

		public override int GetHashCode()
		{
			return 1;
		}

		/// <inheritdoc />
		public void Write(CBufferWriter writer)
		{
			writer.Add(Diffuse);
			writer.Add(Specular);
			writer.Add(Exponent);
			writer.Add(UseLight);
			writer.Add(UseAlpha);
			writer.Add(UseEnv);
			writer.Add(UseTexture);
			writer.Add(UseSpecular);
		}
	}
}
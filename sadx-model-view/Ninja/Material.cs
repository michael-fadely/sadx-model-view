﻿using System;
using System.IO;

using sadx_model_view.Extensions;

namespace sadx_model_view.Ninja
{
	// TODO: blend modes
	/// <summary>
	/// Flags used for materials.
	/// </summary>
	[Flags]
	public enum NJD_FLAG : uint
	{
		Pick           = 0x80,
		UseAnisotropic = 0x1000,
		ClampV         = 0x8000,
		ClampU         = 0x10000,
		FlipV          = 0x20000,
		FlipU          = 0x40000,
		IgnoreSpecular = 0x80000,
		UseAlpha       = 0x100000,
		UseTexture     = 0x200000,
		UseEnv         = 0x400000,
		DoubleSide     = 0x800000,
		UseFlat        = 0x1000000,
		IgnoreLight    = 0x2000000
	}

	/// <summary>
	/// A material for a model containing lighting parameters and other attributes.
	/// </summary>
	public class NJS_MATERIAL
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => 0x14;

		/// <summary>
		/// Constructs <see cref="NJS_MATERIAL"/> from a file.<para/>
		/// See also:
		/// <seealso cref="NJS_COLOR"/>
		/// </summary>
		/// <param name="stream">A stream containing the data.</param>
		public NJS_MATERIAL(Stream stream)
		{
			byte[] buffer = new byte[SizeInBytes];
			stream.ReadExact(buffer);

			diffuse    = new NJS_COLOR(BitConverter.ToInt32(buffer, 0x00));
			specular   = new NJS_COLOR(BitConverter.ToInt32(buffer, 0x04));
			exponent   = BitConverter.ToSingle(buffer, 0x08);
			attr_texId = BitConverter.ToUInt32(buffer, 0x0C);
			attrflags  = (NJD_FLAG)BitConverter.ToUInt32(buffer, 0x10);
		}

		/// <summary>
		/// Copy constructor.
		/// </summary>
		/// <param name="material">Material to copy from.</param>
		public NJS_MATERIAL(NJS_MATERIAL material)
		{
			diffuse    = material.diffuse;
			specular   = material.specular;
			exponent   = material.exponent;
			attr_texId = material.attr_texId;
			attrflags  = material.attrflags;
		}

		/// <summary>
		/// Default constructor.
		/// Initializes a material with white diffuse color, no specular color
		/// and the <see cref="NJD_FLAG.IgnoreLight"/>
		/// and <see cref="NJD_FLAG.IgnoreSpecular"/> flags.
		/// </summary>
		public NJS_MATERIAL()
		{
			diffuse    = new NJS_COLOR(255, 255, 255, 255);
			specular   = new NJS_COLOR(0);
			exponent   = 1.0f;
			attr_texId = 0;
			attrflags  = NJD_FLAG.IgnoreLight | NJD_FLAG.IgnoreSpecular;
		}

		public NJS_COLOR diffuse;
		public NJS_COLOR specular;
		public float     exponent;
		public uint      attr_texId; /* attribute and texture ID in texlist        */
		public NJD_FLAG  attrflags;  /* attribute flags                            */

		public uint TextureIndex
		{
			get => attr_texId & 0xFFFF;
			set => attr_texId = (uint)((attr_texId & ~0xFFFF) | (value & 0xFFFF));
		}

		public uint DestinationBlend
		{
			get => ((uint)attrflags >> 26) & 7;
			set => attrflags = (NJD_FLAG)(((uint)attrflags & ~0x1C000000) | (value << 26));
		}

		public uint SourceBlend
		{
			get => ((uint)attrflags >> 29) & 7;
			set => attrflags = (NJD_FLAG)(((uint)attrflags & ~0xE0000000) | (value << 29));
		}
	}
}

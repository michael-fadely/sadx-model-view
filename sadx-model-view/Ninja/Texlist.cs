﻿using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace sadx_model_view.Ninja
{
	class NJS_TEXPALETTE
	{
		public int palette; // TODO: void*
		public ushort mode;
		public short bank;
		public short offset;
		public short count;

		public NJS_TEXPALETTE()
		{
			palette = 0;
			mode    = 0;
			bank    = 0;
			offset  = 0;
			count   = 0;
		}
	}

	class NJS_TEXSURFACE : IDisposable
	{
		public uint Type;
		public uint BitDepth;
		public uint PixelFormat;
		public uint nWidth;
		public uint nHeight;
		public uint TextureSize;
		public uint fSurfaceFlags;
		public Texture pSurface;
		public int pVirtual; // TODO: Uint32*
		public int pPhysical; // TODO: Uint32*

		public NJS_TEXSURFACE()
		{
			Type          = 0;
			BitDepth      = 0;
			PixelFormat   = 0;
			nWidth        = 0;
			nHeight       = 0;
			TextureSize   = 0;
			fSurfaceFlags = 0;
			pSurface      = null;
			pVirtual      = 0;
			pPhysical     = 0;
		}

		~NJS_TEXSURFACE()
		{
			Dispose();
		}

		public void Dispose()
		{
			pSurface?.Dispose();
			pSurface = null;
		}
	}

	class NJS_TEXINFO : IDisposable
	{
		public int texaddr; // TODO: void*
		public NJS_TEXSURFACE texsurface;

		public NJS_TEXINFO()
		{
			texaddr = 0;
			texsurface = new NJS_TEXSURFACE();
		}

		~NJS_TEXINFO()
		{
			Dispose();
		}

		public void Dispose()
		{
			texsurface?.Dispose();
			texsurface = null;
		}
	}

	class NJS_TEXMEMLIST : IDisposable
	{
		public uint globalIndex;
		public NJS_TEXPALETTE bank;
		public uint tspparambuffer;
		public uint texparambuffer;
		public uint texaddr;
		public NJS_TEXINFO texinfo;
		public ushort count;
		public ushort dummy;

		public NJS_TEXMEMLIST()
		{
			globalIndex    = 0;
			bank           = null;
			tspparambuffer = 0;
			texparambuffer = 0;
			texaddr        = 0;
			texinfo        = null;
			count          = 0;
			dummy          = 0;
		}

		~NJS_TEXMEMLIST()
		{
			Dispose();
		}

		public void Dispose()
		{
			texinfo?.Dispose();
			texinfo = null;
		}
	}

	[Flags]
	enum NJD_TEXATTR : uint
	{
		CACHE            = unchecked((uint)(1 << 31)),
		TYPE_MEMORY      = 1 << 30,
		BOTH             = 1 << 29,
		TYPE_FRAMEBUFFER = 1 << 28,
		PALGLOBALINDEX   = 1 << 24,
		GLOBALINDEX      = 1 << 23,
		AUTOMIPMAP       = 1 << 22,
		AUTODITHER       = 1 << 21,
		TEXCONTINUE      = 1 << 16 
	}

	class NJS_TEXNAME
	{
		public static int SizeInBytes => 0xC;

		// If attr has the flag NJD_TEXATTR_TYPE_MEMORY, use texinfo.
		// Otherwise, use filename.
		// In the original structure, filename is a void* that points
		// to one or the other depending on that flag.
		public string filename = string.Empty;
		public NJS_TEXINFO texinfo = null;

		public NJD_TEXATTR attr;
		public NJS_TEXMEMLIST texaddr = null;

		public NJS_TEXNAME(Stream stream)
		{
			var buffer = new byte[SizeInBytes];
			stream.Read(buffer, 0, buffer.Length);
			var position = stream.Position;

			var data_ptr = BitConverter.ToUInt32(buffer, 0);
			attr = (NJD_TEXATTR)BitConverter.ToUInt32(buffer, 0x04);
			var tex_ptr = BitConverter.ToUInt32(buffer, 0x08);

			if (tex_ptr > 0)
			{
				throw new Exception("texaddr was not null! NJS_TEXMEMLIST should always be dynamically allocated!");
			}

			if ((uint)attr != 0)
			{
				throw new Exception("attr was not 0! Flags are for runtime only!");
			}

			if (data_ptr > 0)
			{
				stream.Position = data_ptr;
				var str = new byte[255];
				filename = Encoding.UTF8.GetString(str, 0, stream.ReadString(ref str));
			}

			stream.Position = position;
		}
	}

	class NJS_TEXLIST
	{
		public static int SizeInBytes => 0x8;

		public readonly List<NJS_TEXNAME> textures = new List<NJS_TEXNAME>();
		public uint nbTexture => (uint)textures.Count;

		public NJS_TEXLIST(Stream stream)
		{
			var buffer = new byte[SizeInBytes];
			stream.Read(buffer, 0, buffer.Length);
			var position = stream.Position;

			var textures_ptr = BitConverter.ToUInt32(buffer, 0);
			var count = BitConverter.ToUInt32(buffer, 4);

			if (count > 0 && textures_ptr > 0)
			{
				stream.Position = textures_ptr;
				for (var i = 0; i < count; i++)
				{
					textures.Add(new NJS_TEXNAME(stream));
				}
			}

			stream.Position = position;
		}
	}
}
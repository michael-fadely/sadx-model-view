using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using sadx_model_view.Extensions;

using SharpDX.Direct3D11;

namespace sadx_model_view.Ninja
{
	public class NJS_TEXPALETTE
	{
		public int    palette; // TODO: void*
		public ushort mode;
		public short  bank;
		public short  offset;
		public short  count;

		public NJS_TEXPALETTE()
		{
			palette = 0;
			mode    = 0;
			bank    = 0;
			offset  = 0;
			count   = 0;
		}
	}

	public class NJS_TEXSURFACE : IDisposable
	{
		public uint       Type;
		public uint       BitDepth;
		public uint       PixelFormat;
		public uint       nWidth;
		public uint       nHeight;
		public uint       TextureSize;
		public uint       fSurfaceFlags;
		public Texture2D? pSurface;
		public int        pVirtual;  // TODO: Uint32*
		public int        pPhysical; // TODO: Uint32*

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

		public void Dispose()
		{
			DisposableExtensions.DisposeAndNullify(ref pSurface);
		}
	}

	public class NJS_TEXINFO : IDisposable
	{
		public int            texaddr; // TODO: void*
		public NJS_TEXSURFACE texsurface;

		public NJS_TEXINFO()
		{
			texaddr    = 0;
			texsurface = new NJS_TEXSURFACE();
		}

		public void Dispose()
		{
			texsurface.Dispose();
		}
	}

	public class NJS_TEXMEMLIST : IDisposable
	{
		public uint            globalIndex;
		public NJS_TEXPALETTE? bank;
		public uint            tspparambuffer;
		public uint            texparambuffer;
		public uint            texaddr;
		public NJS_TEXINFO?    texinfo;
		public ushort          count;
		public ushort          dummy;

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

		public void Dispose()
		{
			DisposableExtensions.DisposeAndNullify(ref texinfo);
		}
	}

	[Flags]
	public enum NJD_TEXATTR : uint
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

	public class NJS_TEXNAME
	{
		public static int SizeInBytes => 0xC;

		// If attr has the flag NJD_TEXATTR_TYPE_MEMORY, use texinfo.
		// Otherwise, use filename.
		// In the original structure, filename is a void* that points
		// to one or the other depending on that flag.
		public string       filename = string.Empty;
		public NJS_TEXINFO? texinfo  = null;

		public NJD_TEXATTR     attr;
		public NJS_TEXMEMLIST? texaddr = null;

		public NJS_TEXNAME(Stream stream)
		{
			byte[] buffer = new byte[SizeInBytes];
			stream.ReadExact(buffer);
			long position = stream.Position;

			uint dataOffset = BitConverter.ToUInt32(buffer, 0);
			attr = (NJD_TEXATTR)BitConverter.ToUInt32(buffer, 0x04);
			uint texPtr = BitConverter.ToUInt32(buffer, 0x08);

			if (texPtr > 0)
			{
				throw new Exception("texaddr was not null! NJS_TEXMEMLIST should always be dynamically allocated!");
			}

			if (attr != 0)
			{
				throw new Exception("attr was not 0! Flags are for runtime only!");
			}

			if (dataOffset > 0)
			{
				stream.Position = dataOffset;
				byte[] str = new byte[255];
				filename = Encoding.UTF8.GetString(str, 0, stream.ReadString(str));
			}

			stream.Position = position;
		}
	}

	public class NJS_TEXLIST
	{
		public static int SizeInBytes => 0x8;

		public readonly IReadOnlyList<NJS_TEXNAME> textures;

		public uint nbTexture => (uint)textures.Count;

		public NJS_TEXLIST(Stream stream)
		{
			byte[] buffer = new byte[SizeInBytes];
			stream.ReadExact(buffer);
			long position = stream.Position;

			uint texturesOffset = BitConverter.ToUInt32(buffer, 0);
			uint count = BitConverter.ToUInt32(buffer, 4);

			var texturesTemp = new List<NJS_TEXNAME>(capacity: checked((int)count));

			if (count > 0 && texturesOffset > 0)
			{
				stream.Position = texturesOffset;

				for (int i = 0; i < count; i++)
				{
					texturesTemp.Add(new NJS_TEXNAME(stream));
				}
			}

			textures = texturesTemp;

			stream.Position = position;
		}
	}
}

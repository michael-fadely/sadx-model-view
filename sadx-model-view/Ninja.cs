using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Ninja
{
	using Angle      = Int32;
	using Float      = Single;  /*  4 byte real number          */
	using NJS_POINT3 = Vector3;
	using NJS_VECTOR = Vector3;
	using Sint16     = Int16;   /*  signed 2 byte integer       */
	using Sint32     = Int32;   /*  signed 4 byte integer       */
	using Uint16     = UInt16;  /*  unsigned 2 byte integer     */
	using Uint32     = UInt32;  /*  unsigned 4 byte integer     */
	using Uint8      = Byte;    /*  unsigned 1 byte integer     */

	static class Ninja
	{
		public static void FromStream(this NJS_VECTOR vector, Stream stream)
		{
			var buffer = new byte[sizeof(float)];
			stream.Read(buffer, 0, buffer.Length);
			vector.X = BitConverter.ToSingle(buffer, 0);

			stream.Read(buffer, 0, buffer.Length);
			vector.Y = BitConverter.ToSingle(buffer, 0);

			stream.Read(buffer, 0, buffer.Length);
			vector.Z = BitConverter.ToSingle(buffer, 0);
		}

		public static void FromStream(this NJS_VECTOR vector, ref byte[] buffer, int offset = 0)
		{
			vector.X = BitConverter.ToSingle(buffer, offset + 0);
			vector.Y = BitConverter.ToSingle(buffer, offset + 4);
			vector.Z = BitConverter.ToSingle(buffer, offset + 8);
		}
	}

	struct Rotation3
	{
		public static int SizeInBytes => sizeof(Angle) * 3;
		public Angle X, Y, Z;
	}
	struct NJS_TEX
	{
		public static int SizeInBytes => sizeof(Sint16) * 2;
		public NJS_TEX(ref byte[] buffer, int offset = 0)
		{
			u = BitConverter.ToInt16(buffer, offset);
			v = BitConverter.ToInt16(buffer, offset + sizeof(Sint16));
		}

		public Sint16 u, v;
	}

	struct NJS_BGRA
	{
		public static int SizeInBytes => sizeof(uint);
		// ReSharper disable once InconsistentNaming
		public Uint8 b, g, r, a;
	}

	[StructLayout(LayoutKind.Explicit)]
	struct NJS_COLOR // union
	{
		public static int SizeInBytes => sizeof(Sint32);
		public NJS_COLOR(Sint32 color)
		{
			argb = new NJS_BGRA
			{
				b = 255,
				g = 255,
				r = 255,
				a = 255
			};

			this.color = color;
		}

		public NJS_COLOR(NJS_BGRA argb)
		{
			color = -1;
			this.argb = argb;
		}

		[FieldOffset(0)]
		public Sint32 color;

		[FieldOffset(0)]
		public NJS_BGRA argb;
	}

	struct NJS_MATERIAL
	{
		public static int SizeInBytes => 0x14;

		public NJS_MATERIAL(Stream file)
		{
			var buffer = new byte[SizeInBytes];
			file.Read(buffer, 0, buffer.Length);

			diffuse    = new NJS_COLOR(BitConverter.ToInt32(buffer, 0x00));
			specular   = new NJS_COLOR(BitConverter.ToInt32(buffer, 0x04));
			exponent   = BitConverter.ToSingle(buffer, 0x08);
			attr_texId = BitConverter.ToUInt32(buffer, 0x0C);
			attrflags  = BitConverter.ToUInt32(buffer, 0x10);
		}

		public NJS_COLOR diffuse;
		public NJS_COLOR specular;
		public Float exponent;
		public Uint32 attr_texId;  /* attribute and texture ID in texlist        */
		public Uint32 attrflags;   /* attribute flags                            */
	}

	enum NJD_MESHSET : Uint16
	{
		_3       = 0x0000,
		_4       = 0x4000,
		_N       = 0x8000,
		_TRIMESH = 0xc000,
		_MASK    = 0xc000
	}

	struct NJS_MESHSET
	{
		public NJD_MESHSET Type
		{
			get { return (NJD_MESHSET)(type_matId & (UInt16)NJD_MESHSET._MASK); }
			set
			{
				type_matId &= (Uint16)~NJD_MESHSET._MASK;
				type_matId |= (Uint16)value;
			}
		}

		public Uint16 MaterialId
		{
			get
			{
				return (Uint16)(type_matId & (Uint16)~NJD_MESHSET._MASK);
			}
			set
			{
				if (value >= 16384)
					throw new ArgumentOutOfRangeException("Number must be < 16384");

				type_matId &= (Uint16)NJD_MESHSET._MASK;
				type_matId |= value;
			}
		}

		public static int SizeInBytes => 0x18;

		public NJS_MESHSET(Stream file)
		{
			var buffer = new byte[SizeInBytes];
			file.Read(buffer, 0, buffer.Length);

			type_matId = BitConverter.ToUInt16(buffer, 0x00);
			nbMesh     = BitConverter.ToUInt16(buffer, 0x02);

			var meshes_ptr    = BitConverter.ToUInt32(buffer, 0x04);
			var attrs_ptr     = BitConverter.ToUInt32(buffer, 0x08);
			var normals_ptr   = BitConverter.ToUInt32(buffer, 0x0C);
			var vertcolor_ptr = BitConverter.ToUInt32(buffer, 0x10);
			var vertuv_ptr    = BitConverter.ToUInt32(buffer, 0x14);

			meshes    = new List<short>();
			attrs     = new List<uint>();
			normals   = new List<NJS_VECTOR>();
			vertcolor = new List<NJS_COLOR>();
			vertuv    = new List<NJS_TEX>();

			var position = file.Position;

			if (meshes_ptr != 0)
			{
				file.Position = meshes_ptr;
				var meshesBuffer = new byte[sizeof(ushort) * nbMesh];
				file.Read(meshesBuffer, 0, meshesBuffer.Length);

				for (var i = 0; i < nbMesh; i++)
					meshes.Add(BitConverter.ToInt16(meshesBuffer, sizeof(ushort) * i));
			}

			if (attrs_ptr != 0)
			{
				file.Position = attrs_ptr;
				var attrsBuffer = new byte[sizeof(uint) * nbMesh];
				file.Read(attrsBuffer, 0, attrsBuffer.Length);

				for (var i = 0; i < nbMesh; i++)
					attrs.Add(BitConverter.ToUInt32(attrsBuffer, sizeof(uint) * i));
			}

			if (normals_ptr != 0)
			{
				file.Position = normals_ptr;
				var normalsBuffer = new byte[Vector3.SizeInBytes * nbMesh];
				file.Read(normalsBuffer, 0, normalsBuffer.Length);

				for (var i = 0; i < nbMesh; i++)
				{
					NJS_VECTOR vector = new NJS_VECTOR();
					vector.FromStream(ref normalsBuffer, i * Vector3.SizeInBytes);
					normals.Add(vector);
				}
			}

			if (vertcolor_ptr != 0)
			{
				file.Position = vertcolor_ptr;
				var vertcolorBuffer = new byte[sizeof(int) * nbMesh];
				file.Read(vertcolorBuffer, 0, vertcolorBuffer.Length);

				for (var i = 0; i < nbMesh; i++)
				{
					vertcolor.Add(new NJS_COLOR(BitConverter.ToInt32(vertcolorBuffer, sizeof(int) * i)));
				}
			}

			if (vertuv_ptr != 0)
			{
				file.Position = vertuv_ptr;
				var vertuvBuffer = new byte[NJS_TEX.SizeInBytes * nbMesh];
				file.Read(vertuvBuffer, 0, vertuvBuffer.Length);

				for (var i = 0; i < nbMesh; i++)
					vertuv.Add(new NJS_TEX(ref vertuvBuffer, NJS_TEX.SizeInBytes * i));
			}

			file.Position = position;
		}

		public Uint16 type_matId;          /* meshset type and attr index
											14-15 : meshset type bits
											0-13 : material id(0-4095)     */
		public Uint16 nbMesh;              /* mesh count                   */
		public List<Sint16> meshes;        /* mesh array                   */
		public List<Uint32> attrs;         /* attribure                    */
		public List<NJS_VECTOR> normals;   /* mesh normal list             */
		public List<NJS_COLOR> vertcolor;  /* polygon vertex color list    */
		public List<NJS_TEX> vertuv;       /* polygon vertex uv list       */
	}

	class NJS_MODEL
	{
		public static int SizeInBytes => 0x28;
		public NJS_MODEL(Stream file)
		{
			var buffer = new byte[SizeInBytes];
			file.Read(buffer, 0, buffer.Length);

			nbPoint   = BitConverter.ToInt32(buffer, 0x08);
			nbMeshset = BitConverter.ToUInt16(buffer, 0x14);
			nbMat     = BitConverter.ToUInt16(buffer, 0x16);
			center.X  = BitConverter.ToSingle(buffer, 0x18);
			center.Y  = BitConverter.ToSingle(buffer, 0x18 + 4);
			center.Z  = BitConverter.ToSingle(buffer, 0x18 + 8);
			r         = BitConverter.ToSingle(buffer, 0x24);
			points    = new List<NJS_POINT3>();
			normals   = new List<NJS_VECTOR>();
			meshsets  = new List<NJS_MESHSET>();
			mats      = new List<NJS_MATERIAL>();

			var position = file.Position;

			if (nbPoint > 0)
			{
				var points_ptr = BitConverter.ToUInt32(buffer, 0x00);

				if (points_ptr > 0)
				{
					file.Position = points_ptr;

					for (var i = 0; i < nbPoint; i++)
					{
						NJS_POINT3 v = new NJS_VECTOR();
						v.FromStream(file);
						points.Add(v);
					}
				}

				var normals_ptr = BitConverter.ToInt32(buffer, 0x04);

				if (normals_ptr > 0)
				{
					file.Position = normals_ptr;

					for (var i = 0; i < nbPoint; i++)
					{
						NJS_POINT3 v = new NJS_VECTOR();
						v.FromStream(file);
						normals.Add(v);
					}
				}
			}

			var meshsets_ptr = BitConverter.ToUInt32(buffer, 0x0C);
			if (nbMeshset > 0 && meshsets_ptr > 0)
			{
				file.Position = meshsets_ptr;
				for (var i = 0; i < nbMeshset; i++)
					meshsets.Add(new NJS_MESHSET(file));
			}

			var mats_ptr = BitConverter.ToUInt32(buffer, 0x10);
			if (nbMat > 0 && mats_ptr > 0)
			{
				file.Position = mats_ptr;
				for (var i = 0; i < nbMeshset; i++)
					mats.Add(new NJS_MATERIAL(file));
			}

			file.Position = position;
		}

		public List<NJS_POINT3> points;    /* vertex list                  */
		public List<NJS_VECTOR> normals;   /* vertex normal list           */
		public Sint32 nbPoint;             /* vertex count                 */
		public List<NJS_MESHSET> meshsets; /* meshset list                 */
		public List<NJS_MATERIAL> mats;    /* material list                */
		public Uint16 nbMeshset;           /* meshset count                */
		public Uint16 nbMat;               /* material count               */
		public NJS_POINT3 center;          /* model center                 */
		public Float r;                    /* radius                       */
	}

	class NJS_OBJECT
	{
		public static int SizeInBytes => 0x34;
		public NJS_OBJECT(Stream file)
		{
			var buffer = new byte[SizeInBytes];
			file.Read(buffer, 0, buffer.Length);

			evalflags = BitConverter.ToUInt32(buffer, 0);

			pos.X = BitConverter.ToSingle(buffer, 8);
			pos.Y = BitConverter.ToSingle(buffer, 8 + 4);
			pos.Z = BitConverter.ToSingle(buffer, 8 + 8);

			ang.X = BitConverter.ToInt32(buffer, 0x14);
			ang.Y = BitConverter.ToInt32(buffer, 0x14 + 4);
			ang.Z = BitConverter.ToInt32(buffer, 0x14 + 8);

			scl.X = BitConverter.ToSingle(buffer, 0x20);
			scl.Y = BitConverter.ToSingle(buffer, 0x20 + 4);
			scl.Z = BitConverter.ToSingle(buffer, 0x20 + 8);

			var model_ptr   = BitConverter.ToUInt32(buffer, 0x04);
			var child_ptr   = BitConverter.ToUInt32(buffer, 0x2C);
			var sibling_ptr = BitConverter.ToUInt32(buffer, 0x30);

			var position = file.Position;

			if (model_ptr != 0)
			{
				file.Position = model_ptr;
				model = new NJS_MODEL(file);
			}

			if (child_ptr != 0)
			{
				file.Position = child_ptr;
				child = new NJS_OBJECT(file);
			}

			if (sibling_ptr != 0)
			{
				file.Position = sibling_ptr;
				sibling = new NJS_OBJECT(file);
			}

			file.Position = position;
		}

		public Uint32 evalflags;     /* evalation flags              */
		public NJS_MODEL model;      /* model data pointer           */
		public NJS_VECTOR pos;       /* translation                  */
		public Rotation3 ang;        /* rotation                     */
		public NJS_VECTOR scl;       /* scaling                      */
		public NJS_OBJECT child;     /* child object                 */
		public NJS_OBJECT sibling;   /* sibling object               */
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D9;
using SharpDX.Mathematics.Interop;

namespace sadx_model_view
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

	public static class Ninja
	{
		/// <summary>
		/// <para>Constructs a new <see cref="NJS_VECTOR"/> with data provided by a stream.</para>
		/// </summary>
		/// <param name="stream">Stream containing data.</param>
		/// <returns>The new vector.</returns>
		public static NJS_VECTOR VectorFromStream(Stream stream)
		{
			NJS_VECTOR vector;

			var buffer = new byte[sizeof(float)];
			stream.Read(buffer, 0, buffer.Length);
			vector.X = BitConverter.ToSingle(buffer, 0);

			stream.Read(buffer, 0, buffer.Length);
			vector.Y = BitConverter.ToSingle(buffer, 0);

			stream.Read(buffer, 0, buffer.Length);
			vector.Z = BitConverter.ToSingle(buffer, 0);

			return vector;
		}

		/// <summary>
		/// <para>Constructs a new <see cref="NJS_VECTOR"/> with data provided by a buffer.</para>
		/// </summary>
		/// <param name="buffer">Buffer containing data.</param>
		/// <param name="offset">Offset in buffer to read from.</param>
		/// <returns>The new vector.</returns>
		public static NJS_VECTOR VectorFromStream(ref byte[] buffer, int offset = 0)
		{
			return new NJS_VECTOR(BitConverter.ToSingle(buffer, offset + 0), BitConverter.ToSingle(buffer, offset + 4), BitConverter.ToSingle(buffer, offset + 8));
		}

		public static float DegreeToRadian(float n)
		{
			return (float)(n * Math.PI / 180.0f);
		}
		public static Angle DegreeToAngle(float n)
		{
			return (Angle)(n * 65536.0 / 360.0);
		}

		public static Angle RadToAngle(float n)
		{
			return (Angle)(n * 65536.0 / (2 * Math.PI));
		}
		public static float RadToDegree(float n)
		{
			return (float)(n * 180.0 / Math.PI);
		}

		public static float AngleToDegree(Angle n)
		{
			return (float)(n * 360.0 / 65536.0);
		}
		public static float AngleToRadian(Angle n)
		{
			return (float)(n * (2 * Math.PI) / 65536.0);
		}

		public static NJS_VECTOR AngleToRadian(ref Rotation3 n)
		{
			return new NJS_VECTOR(AngleToRadian(n.X), AngleToRadian(n.Y), AngleToRadian(n.Z));
		}
		public static NJS_VECTOR AngleToDegree(ref Rotation3 n)
		{
			return new NJS_VECTOR(AngleToDegree(n.X), AngleToDegree(n.Y), AngleToDegree(n.Z));
		}
	}

	/// <summary>
	/// A structure defining rotation on 3 axes in BAMS.
	/// </summary>
	public struct Rotation3
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => sizeof(Angle) * 3;

		public Angle X, Y, Z;

		public Rotation3(Angle x, Angle y, Angle z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public static Rotation3 operator +(Rotation3 lhs, Rotation3 rhs)
		{
			return new Rotation3(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);
		}
		public static Rotation3 operator -(Rotation3 lhs, Rotation3 rhs)
		{
			return new Rotation3(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);
		}
	}

	/// <summary>
	/// <para>Defines UV coordinates for <see cref="NJS_MESHSET"/>.</para>
	/// See also:
	/// <seealso cref="NJS_MESHSET"/>
	/// </summary>
	public struct NJS_TEX
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => sizeof(Sint16) * 2;

		public NJS_TEX(ref byte[] buffer, int offset = 0)
		{
			u = BitConverter.ToInt16(buffer, offset);
			v = BitConverter.ToInt16(buffer, offset + sizeof(Sint16));
		}

		public Sint16 u, v;
	}

	/// <summary>
	/// A color represented by 4 bytes.
	/// </summary>
	public struct NJS_BGRA
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => sizeof(uint);

		public Uint8 b, g, r, a;
	}

	/// <summary>
	/// <para>A union defining a color represented by 4 bytes.
	/// It can be manipulated with direct access to the color integer, or on an individual byte level.</para>
	/// See also:
	/// <seealso cref="NJS_BGRA"/>
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	public struct NJS_COLOR // union
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => sizeof(Sint32);

		/// <summary>
		/// Constructs <see cref="NJS_COLOR"/> from a 32-bit integer.
		/// </summary>
		/// <param name="color">An integer containing an ARGB color.</param>
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

		/// <summary>
		/// Constructs <see cref="NJS_COLOR"/> from <see cref="NJS_BGRA"/>.<para/>
		/// See also:
		/// <seealso cref="NJS_BGRA"/>
		/// </summary>
		/// <param name="argb"></param>
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

	/// <summary>
	/// Flags used for materials.
	/// </summary>
	enum NJD_FLAG : Uint32
	{
		PICK            = 0x80,
		USE_ANISOTROPIC = 0x1000,
		CLAMP_V         = 0x8000,
		CLAMP_U         = 0x10000,
		FLIP_V          = 0x20000,
		FLIP_U          = 0x40000,
		IGNORE_SPECULAR = 0x80000,
		USE_ALPHA       = 0x100000,
		USE_TEXTURE     = 0x200000,
		USE_ENV         = 0x400000,
		DOUBLE_SIDE     = 0x800000,
		USE_FLAT        = 0x1000000,
		IGNORE_LIGHT    = 0x2000000,
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
		/// <param name="file">A file stream containing the data.</param>
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

	/// <summary>
	/// Used to identify different types of <see cref="NJS_MESHSET"/>.
	/// </summary>
	public enum NJD_MESHSET : Uint16
	{
		/// <summary>
		/// <para>List of triangular polygons.</para>
		///<para> Indicates that each polygon is defined by 3 vertices.
		/// The number of polygons is defined by <seealso cref="NJS_MESHSET.nbMesh"/>,
		/// and the number of vertices is <seealso cref="NJS_MESHSET.nbMesh"/> * 3.</para>
		/// </summary>
		Tri = 0x0000,
		/// <summary>
		/// <para>List of quadrilateral polygons.</para>
		/// <para>Indicates that each polygon is defined by 4 vertices.
		/// The number of polygons is defined by <seealso cref="NJS_MESHSET.nbMesh"/>,
		/// and the number of vertices is <seealso cref="NJS_MESHSET.nbMesh"/> * 4.</para>
		/// </summary>
		Quad = 0x4000,
		/// <summary>
		/// <para>List of N-sided polygons, where N is 5 or more.</para>
		/// <para>Each polygon is preceded by the number of vertices defining it. Subsequently,
		/// the number of vertices must be calculated by iterating over <see cref="NJS_MESHSET.meshes"/>.
		/// The number of polygons is defined by <seealso cref="NJS_MESHSET.nbMesh"/>.</para>
		/// </summary>
		NSided = 0x8000,
		/// <summary>
		/// <para>List of contiguous polygons (triangle strip).</para>
		/// <para>Each polygon is preceded by the number of vertices defining it. Subsequently,
		/// the number of vertices must be calculated by iterating over <see cref="NJS_MESHSET.meshes"/>.
		/// The number of polygons is defined by <seealso cref="NJS_MESHSET.nbMesh"/>.</para>
		/// </summary>
		Strip = 0xc000
	}

	struct Vertex
	{
		public static VertexFormat Format = VertexFormat.Position | VertexFormat.Normal | VertexFormat.Diffuse | VertexFormat.Texture1;
		public static int SizeInBytes => 36;
		public RawVector3 position;
		public RawVector3 normal;
		public RawColorBGRA diffuse;
		public RawVector2 uv;
	}

	/// <summary>
	/// <para>Defines a list of polygons and their type for <see cref="NJS_MODEL"/>.</para>
	/// See also:
	/// <seealso cref="NJD_MESHSET"/>
	/// <seealso cref="NJS_MODEL"/>
	/// </summary>
	public class NJS_MESHSET : IDisposable
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => 0x18;

		/// <summary>
		/// The mesh type for this meshset.<para/>
		/// See <seealso cref="NJD_MESHSET"/> for more information
		/// </summary>
		public NJD_MESHSET Type
		{
			get { return (NJD_MESHSET)(type_matId & (UInt16)NJD_MESHSET.Strip); }
			set
			{
				type_matId &= (Uint16)~NJD_MESHSET.Strip;
				type_matId |= (Uint16)value;

				switch (Type)
				{
					case NJD_MESHSET.Tri:
					case NJD_MESHSET.Quad:
						PrimitiveType = PrimitiveType.TriangleList;
						break;

					case NJD_MESHSET.NSided:
						PrimitiveType = PrimitiveType.TriangleFan;
						break;

					case NJD_MESHSET.Strip:
						PrimitiveType = PrimitiveType.TriangleStrip;
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		public PrimitiveType PrimitiveType { get; private set; }

		public int PrimitiveCount { get; private set; }

		/// <summary>
		/// The material ID for this meshset.
		/// </summary>
		public Uint16 MaterialId
		{
			get
			{
				return (Uint16)(type_matId & (Uint16)~NJD_MESHSET.Strip);
			}
			set
			{
				if (value > 16383)
					throw new ArgumentOutOfRangeException(nameof(value), "Number must be less than 16384");

				type_matId &= (Uint16)NJD_MESHSET.Strip;
				type_matId |= value;
			}
		}

		/// <summary>
		/// <para>The actual number of vertices referenced by this meshset.</para>
		/// <para>
		/// The number of vertcies varies from <see cref="nbMesh"/> to different degrees
		/// depending on the type of polygon managed by this meshset. (see <seealso cref="Type"/>, <seealso cref="NJD_MESHSET"/>)
		/// </para>
		/// </summary>
		public int VertexCount { get; }

		/// <summary>
		/// The vertex buffer used to render this meshset.
		/// </summary>
		public VertexBuffer VertexBuffer;

		public Uint16 type_matId;          /* meshset type and attr index
											14-15 : meshset type bits
											0-13 : material id(0-4095)     */
		public Uint16 nbMesh;              /* mesh count                   */
		public List<Sint16> meshes;        /* mesh array                   */
		public List<Uint32> attrs;         /* attribure                    */
		public List<NJS_VECTOR> normals;   /* mesh normal list             */
		public List<NJS_COLOR> vertcolor;  /* polygon vertex color list    */
		public List<NJS_TEX> vertuv;       /* polygon vertex uv list       */

		public NJS_MESHSET(Stream file)
		{
			// TODO: Use indexed everything

			var buffer = new byte[SizeInBytes];
			file.Read(buffer, 0, buffer.Length);

			VertexBuffer = null;

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

			VertexCount = 0;
			PrimitiveCount = 0;

			if (meshes_ptr != 0)
			{
				file.Position = meshes_ptr;
				var meshesBuffer = new byte[2];

				Type = Type;

				switch (Type)
				{
					case NJD_MESHSET.Tri:
						VertexCount = nbMesh * 3;
						PrimitiveCount = VertexCount;

						for (int i = 0; i < VertexCount; i++)
						{
							file.Read(meshesBuffer, 0, sizeof(short));
							meshes.Add(BitConverter.ToInt16(meshesBuffer, 0));
						}
						break;

					case NJD_MESHSET.Quad:
						VertexCount = nbMesh * 4;
						PrimitiveCount = nbMesh * 6 / 3;

						for (int i = 0; i < VertexCount; i++)
						{
							file.Read(meshesBuffer, 0, sizeof(short));
							meshes.Add(BitConverter.ToInt16(meshesBuffer, 0));
						}
						break;

					case NJD_MESHSET.NSided:
					case NJD_MESHSET.Strip:
						for (int i = 0; i < nbMesh; i++)
						{
							file.Read(meshesBuffer, 0, sizeof(short));
							var n = BitConverter.ToInt16(meshesBuffer, 0);
							meshes.Add(n);

							// n is being maked because there are cases where
							// the number has garbage data after the first byte.
							n &= 0x3FFF;

							for (int j = 0; j < n; j++)
							{
								file.Read(meshesBuffer, 0, sizeof(short));
								meshes.Add(BitConverter.ToInt16(meshesBuffer, 0));
							}

							VertexCount += n;
						}

						CalculateStripPrimitiveCount();

						if (VertexCount != meshes.Count - nbMesh)
						{
							throw new Exception("Recorded vertex count is incorrect.");
						}
						break;

					default:
						throw new ArgumentOutOfRangeException();
				}
			}

			if (attrs_ptr != 0)
			{
				file.Position = attrs_ptr;
				var attrsBuffer = new byte[sizeof(uint) * VertexCount];
				file.Read(attrsBuffer, 0, attrsBuffer.Length);

				for (var i = 0; i < VertexCount; i++)
					attrs.Add(BitConverter.ToUInt32(attrsBuffer, sizeof(uint) * i));
			}

			if (normals_ptr != 0)
			{
				file.Position = normals_ptr;
				var normalsBuffer = new byte[NJS_VECTOR.SizeInBytes * VertexCount];
				file.Read(normalsBuffer, 0, normalsBuffer.Length);

				for (var i = 0; i < VertexCount; i++)
				{
					NJS_VECTOR vector = Ninja.VectorFromStream(ref normalsBuffer, i * NJS_VECTOR.SizeInBytes);
					normals.Add(vector);
				}
			}

			if (vertcolor_ptr != 0)
			{
				file.Position = vertcolor_ptr;
				var vertcolorBuffer = new byte[sizeof(int) * VertexCount];
				file.Read(vertcolorBuffer, 0, vertcolorBuffer.Length);

				for (var i = 0; i < VertexCount; i++)
				{
					vertcolor.Add(new NJS_COLOR(BitConverter.ToInt32(vertcolorBuffer, sizeof(int) * i)));
				}
			}

			if (vertuv_ptr != 0)
			{
				file.Position = vertuv_ptr;
				var vertuvBuffer = new byte[NJS_TEX.SizeInBytes * VertexCount];
				file.Read(vertuvBuffer, 0, vertuvBuffer.Length);

				for (var i = 0; i < VertexCount; i++)
					vertuv.Add(new NJS_TEX(ref vertuvBuffer, NJS_TEX.SizeInBytes * i));
			}

			file.Position = position;
		}

		private void CalculateStripPrimitiveCount()
		{
			PrimitiveCount = 0;
			var count = nbMesh;
			int i = 0;

			do
			{
				--count;
				var n = meshes[i] & 0x3FFF;
				PrimitiveCount += n + 2;
				i += (n + 1);
			} while (count > 0);

			PrimitiveCount -= 2;
		}

		public void Dispose()
		{
			VertexBuffer.Dispose();
			VertexBuffer = null;
		}
	}

	public class NJS_MODEL : IDisposable
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => 0x28;

		/// <summary>
		/// Constructs <see cref="NJS_MODEL"/>, its points, normals, materials, and meshsets from a file.<para/>
		/// See also:
		/// <seealso cref="NJS_MESHSET"/>
		/// <seealso cref="NJS_MATERIAL"/>
		/// </summary>
		/// <param name="file">A file stream containing the data.</param>
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
						NJS_POINT3 v = Ninja.VectorFromStream(file);
						points.Add(v);
					}
				}

				var normals_ptr = BitConverter.ToInt32(buffer, 0x04);

				if (normals_ptr > 0)
				{
					file.Position = normals_ptr;

					for (var i = 0; i < nbPoint; i++)
					{
						NJS_POINT3 v = Ninja.VectorFromStream(file);
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

		public void CommitVertexBuffer(Device device)
		{
			foreach (NJS_MESHSET set in meshsets)
			{
				// TODO: use vertex declaration and render with a shader
				List<Vertex> vertices = new List<Vertex>();

				int size;

				switch (set.Type)
				{
					case NJD_MESHSET.Tri:
						size = Vertex.SizeInBytes * set.PrimitiveCount;

						for (var i = 0; i < set.VertexCount; i++)
						{
							var point_i = set.meshes[i];

							RawColorBGRA color;

							if (set.vertcolor.Count != 0)
							{
								NJS_BGRA vcolor = set.vertcolor[i].argb;
								color = new RawColorBGRA(vcolor.b, vcolor.g, vcolor.r, vcolor.a);
							}
							else
							{
								color = Color.White;
							}

							Vector2 uv = set.vertuv.Count != 0
								? new Vector2(set.vertuv[i].u / 255.0f, set.vertuv[i].v / 255.0f)
								: new Vector2(0.0f, 0.0f);

							Vertex vertex = new Vertex
							{
								position = points[point_i],
								normal   = normals.Count != 0 ? normals[point_i] : NJS_VECTOR.Up,
								diffuse  = color,
								uv       = uv
							};

							vertices.Add(vertex);
						}
						break;

					case NJD_MESHSET.Quad:
						size = Vertex.SizeInBytes * (set.PrimitiveCount * 3);

						var indeces = new List<KeyValuePair<int, Sint16>>();

						for (var i = 0; i < set.VertexCount; i += 4)
						{
							indeces.Add(new KeyValuePair<int, Sint16>(i + 0, set.meshes[i + 0]));
							indeces.Add(new KeyValuePair<int, Sint16>(i + 1, set.meshes[i + 1]));
							indeces.Add(new KeyValuePair<int, Sint16>(i + 2, set.meshes[i + 2]));
							indeces.Add(new KeyValuePair<int, Sint16>(i + 2, set.meshes[i + 2]));
							indeces.Add(new KeyValuePair<int, Sint16>(i + 1, set.meshes[i + 1]));
							indeces.Add(new KeyValuePair<int, Sint16>(i + 3, set.meshes[i + 3]));
						}

						foreach (var pair in indeces)
						{
							RawColorBGRA color;

							if (set.vertcolor.Count != 0)
							{
								NJS_BGRA vcolor = set.vertcolor[pair.Key].argb;
								color = new RawColorBGRA(vcolor.b, vcolor.g, vcolor.r, vcolor.a);
							}
							else
							{
								color = Color.White;
							}

							Vector2 uv = set.vertuv.Count != 0
								? new Vector2(set.vertuv[pair.Key].u / 255.0f, set.vertuv[pair.Key].v / 255.0f)
								: new Vector2(0.0f, 0.0f);

							Vertex vertex = new Vertex
							{
								position = points[pair.Value],
								normal = normals.Count != 0 ? normals[pair.Value] : NJS_VECTOR.Up,
								diffuse = color,
								uv = uv
							};

							vertices.Add(vertex);
						}

						break;

					case NJD_MESHSET.Strip:
					case NJD_MESHSET.NSided:
						size = Vertex.SizeInBytes * (set.PrimitiveCount + 2);
						int n = 0;
						int vcount = 0;

						for (var i = 0; i < set.nbMesh; i++)
						{
							var count = set.meshes[n] & 0x3FFF;

							for (int j = 0; j < count; j++)
							{
								var point_i = set.meshes[++n];

								RawColorBGRA color;

								if (set.vertcolor.Count != 0)
								{
									NJS_BGRA vcolor = set.vertcolor[vcount].argb;
									color = new RawColorBGRA(vcolor.b, vcolor.g, vcolor.r, vcolor.a);
								}
								else
								{
									color = Color.White;
								}

								Vector2 uv = set.vertuv.Count != 0
									? new Vector2(set.vertuv[vcount].u / 255.0f, set.vertuv[vcount].v / 255.0f)
									: new Vector2(0.0f, 0.0f);

								if (point_i < 0)
									throw new ArgumentOutOfRangeException();

								Vertex vertex = new Vertex
								{
									position = points[point_i],
									normal   = normals.Count != 0 ? normals[point_i] : NJS_VECTOR.Up,
									diffuse  = color,
									uv       = uv
								};

								vertices.Add(vertex);

								// add it twice
								// why you ask? reasons
								if (j == 0 || j == count - 1)
								{
									vertices.Add(vertex);
								}

								++vcount;
							}

							++n;
						}

						break;

					default:
						throw new ArgumentOutOfRangeException();
				}

				if (vertices.Count == 0)
				{
					return;
				}

				set.VertexBuffer = new VertexBuffer(device, size, Usage.None, Vertex.Format, Pool.Managed);
				using (var stream = set.VertexBuffer.Lock(0, size, LockFlags.None))
				{
					foreach (var v in vertices)
					{
						stream.Write(v.position.X);
						stream.Write(v.position.Y);
						stream.Write(v.position.Z);

						stream.Write(v.normal.X);
						stream.Write(v.normal.Y);
						stream.Write(v.normal.Z);

						stream.Write(v.diffuse.B);
						stream.Write(v.diffuse.G);
						stream.Write(v.diffuse.R);
						stream.Write(v.diffuse.A);

						stream.Write(v.uv.X);
						stream.Write(v.uv.Y);
					}

					if (stream.RemainingLength != 0)
						throw new Exception("Failed to fill vertex buffer.");
				}

				set.VertexBuffer.Unlock();
			}
		}

		private void SetSADXMaterial(Device device, NJS_MATERIAL material)
		{
			if (material == null)
				return;

			var flags = (NJD_FLAG)material.attrflags;

			if (flags.HasFlag(NJD_FLAG.USE_TEXTURE))
			{
				if (flags.HasFlag(NJD_FLAG.PICK))
				{
					// TODO: not even implemented in SADX
				}

				if (flags.HasFlag(NJD_FLAG.USE_ANISOTROPIC))
				{
					// TODO: not even implemented in SADX
				}

				if (flags.HasFlag(NJD_FLAG.CLAMP_V))
				{
					device.SetSamplerState(0, SamplerState.AddressV, TextureAddress.Clamp);
				}

				if (flags.HasFlag(NJD_FLAG.CLAMP_U))
				{
					device.SetSamplerState(0, SamplerState.AddressU, TextureAddress.Clamp);
				}

				if (flags.HasFlag(NJD_FLAG.FLIP_V))
				{
					device.SetSamplerState(0, SamplerState.AddressV, TextureAddress.Mirror);
				}

				if (flags.HasFlag(NJD_FLAG.FLIP_U))
				{
					device.SetSamplerState(0, SamplerState.AddressU, TextureAddress.Mirror);
				}

				if (flags.HasFlag(NJD_FLAG.USE_ENV))
				{
					// TODO
				}

				if (flags.HasFlag(NJD_FLAG.USE_ALPHA))
				{
					// TODO
				}
			}

			device.SetRenderState(RenderState.SpecularEnable, !flags.HasFlag(NJD_FLAG.IGNORE_SPECULAR));
			device.SetRenderState(RenderState.CullMode, flags.HasFlag(NJD_FLAG.DOUBLE_SIDE) ? Cull.None : Cull.Counterclockwise);

			if (flags.HasFlag(NJD_FLAG.USE_FLAT))
			{
				// TODO: not even implemented in SADX
			}

			device.EnableLight(0, !flags.HasFlag(NJD_FLAG.IGNORE_LIGHT));

			Material m = new Material
			{
				Specular = new Color4(material.specular.color),
				Diffuse  = new Color4(material.diffuse.color),
				Power    = material.exponent
			};

			// default SADX behavior is to use diffuse for both ambient and diffuse.
			m.Ambient = m.Diffuse;

			device.Material = m;
		}

		public void Draw(Device device)
		{
			using (var block = new StateBlock(device, StateBlockType.All))
			{
				// Set the correct vertex format for model rendering.
				device.VertexFormat = Vertex.Format;

				foreach (var set in meshsets)
				{
					// Begin a state block so changes made by the material
					// can be reverted.
					block.Capture();

					var i = set.MaterialId;
					var material = mats[i];

					// Set up rendering parameters based on this material.
					SetSADXMaterial(device, material);

					// Set the stream source to the current meshset's vertex buffer.
					device.SetStreamSource(0, set.VertexBuffer, 0, Vertex.SizeInBytes);

					// Draw the model.
					device.DrawPrimitives(set.PrimitiveType, 0, set.PrimitiveCount);

					// Restore the previous render state.
					block.Apply();
				}
			}
		}

		public void Dispose()
		{
			foreach (var set in meshsets)
			{
				set.Dispose();
			}

			meshsets.Clear();
		}
	}

	[Flags]
	enum NJD_EVAL : Uint32
	{
		/// <summary>
		/// Ignore translation.
		/// </summary>
		UNIT_POS = 1 << 0,
		/// <summary>
		/// Ignore rotation.
		/// </summary>
		UNIT_ANG = 1 << 1,
		/// <summary>
		/// Ignore scale.
		/// </summary>
		UNIT_SCL = 1 << 2,
		/// <summary>
		/// Don't draw this model.
		/// </summary>
		HIDE = 1 << 3,
		/// <summary>
		/// Terminate tracing children. (Don't draw children)
		/// </summary>
		BREAK = 1 << 4,
		/// <summary>
		/// Use ZXY rotation.
		/// </summary>
		ZXY_ANG = 1 << 5,
		/// <summary>
		/// Skip animation.
		/// </summary>
		SKIP = 1 << 6,
		/// <summary>
		/// Unknown.
		/// </summary>
		SHAPE_SKIP = 1 << 7,
		/// <summary>
		/// Unknown.
		/// </summary>
		CLIP = 1 << 8,
		/// <summary>
		/// Unknown.
		/// </summary>
		MODIFIER = 1 << 9
	}

	/// <summary>
	/// <para>An object in the world which has a model, position, angle, and scale.</para>
	/// See also:
	/// <seealso cref="NJS_MODEL"/>
	/// </summary>
	public class NJS_OBJECT : IDisposable
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => 0x34;

		/// <summary>
		/// Constructs <see cref="NJS_OBJECT"/>, its children, and all of its available members from a file.
		/// </summary>
		/// <param name="file">A file stream containing the data.</param>
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

		public bool IgnoreTranslation
		{
			get { return ((NJD_EVAL)evalflags).HasFlag(NJD_EVAL.UNIT_POS); }
			set
			{
				if (value)
				{
					evalflags |= (Uint32)NJD_EVAL.UNIT_POS;
				}
				else
				{
					evalflags &= (Uint32)~NJD_EVAL.UNIT_POS;
				}
			}
		}
		public bool IgnoreRotation
		{
			get { return ((NJD_EVAL)evalflags).HasFlag(NJD_EVAL.UNIT_ANG); }
			set
			{
				if (value)
				{
					evalflags |= (Uint32)NJD_EVAL.UNIT_ANG;
				}
				else
				{
					evalflags &= (Uint32)~NJD_EVAL.UNIT_ANG;
				}
			}
		}
		public bool IgnoreScale
		{
			get { return ((NJD_EVAL)evalflags).HasFlag(NJD_EVAL.UNIT_SCL); }
			set
			{
				if (value)
				{
					evalflags |= (Uint32)NJD_EVAL.UNIT_SCL;
				}
				else
				{
					evalflags &= (Uint32)~NJD_EVAL.UNIT_SCL;
				}
			}
		}
		public bool SkipDraw
		{
			get { return ((NJD_EVAL)evalflags).HasFlag(NJD_EVAL.HIDE); }
			set
			{
				if (value)
				{
					evalflags |= (Uint32)NJD_EVAL.HIDE;
				}
				else
				{
					evalflags &= (Uint32)~NJD_EVAL.HIDE;
				}
			}
		}
		public bool SkipChildren
		{
			get { return ((NJD_EVAL)evalflags).HasFlag(NJD_EVAL.BREAK); }
			set
			{
				if (value)
				{
					evalflags |= (Uint32)NJD_EVAL.BREAK;
				}
				else
				{
					evalflags &= (Uint32)~NJD_EVAL.BREAK;
				}
			}
		}
		public bool UseZXYRotation
		{
			get { return ((NJD_EVAL)evalflags).HasFlag(NJD_EVAL.ZXY_ANG); }
			set
			{
				if (value)
				{
					evalflags |= (Uint32)NJD_EVAL.ZXY_ANG;
				}
				else
				{
					evalflags &= (Uint32)~NJD_EVAL.ZXY_ANG;
				}
			}

		}

		public void CommitVertexBuffer(Device device)
		{
			model?.CommitVertexBuffer(device);
			child?.CommitVertexBuffer(device);
			sibling?.CommitVertexBuffer(device);
		}

		public void Draw(Device device)
		{
			MatrixStack.Push();

			if (!IgnoreTranslation)
			{
				MatrixStack.Translate(ref pos);
			}

			if (!IgnoreRotation)
			{
				MatrixStack.Rotate(ref ang, UseZXYRotation);
			}

			if (!IgnoreScale)
			{
				MatrixStack.Scale(ref scl);
			}

			MatrixStack.SetTransform(device);

			if (!SkipDraw)
			{
				model?.Draw(device);
			}

			if (!SkipChildren)
			{
				child?.Draw(device);
			}

			MatrixStack.Pop();
			sibling?.Draw(device);
		}

		public void Dispose()
		{
			model?.Dispose();
		}
	}
}

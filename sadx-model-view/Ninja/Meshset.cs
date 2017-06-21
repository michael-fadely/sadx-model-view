using System;
using System.Collections.Generic;
using System.IO;
using SharpDX;
using SharpDX.Mathematics.Interop;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace sadx_model_view.Ninja
{
	/// <summary>
	/// Used to identify different types of <see cref="NJS_MESHSET"/>.
	/// </summary>
	public enum NJD_MESHSET : ushort
	{
		/// <summary>
		/// <para>List of triangular polygons.</para>
		/// <para> Indicates that each polygon is defined by 3 vertices.
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

	/// <summary>
	/// Custom vertex used for rendering <see cref="NJS_MESHSET"/>.
	/// </summary>
	public struct Vertex
	{
		public const VertexFormat Format = VertexFormat.Position | VertexFormat.Normal | VertexFormat.Diffuse | VertexFormat.Texture1;
		public static int SizeInBytes => 36;
		public RawVector3 position;
		public RawVector3 normal;
		public RawColorBGRA? diffuse;
		public RawVector2? uv;
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
		public static int SizeInBytes => sizeof(short) * 2;

		public NJS_TEX(ref byte[] buffer, int offset = 0)
		{
			u = BitConverter.ToInt16(buffer, offset);
			v = BitConverter.ToInt16(buffer, offset + sizeof(short));
		}

		public short u, v;
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
		/// The mesh type for this meshset defined in the bits 14-15 of <seealso cref="NJS_MESHSET.type_matId"/>.<para/>
		/// See <seealso cref="NJD_MESHSET"/> for more information.
		/// </summary>
		public NJD_MESHSET Type
		{
			get => (NJD_MESHSET)(type_matId & (ushort)NJD_MESHSET.Strip);
			set
			{
				type_matId &= (ushort)~NJD_MESHSET.Strip;
				type_matId |= (ushort)value;
			}
		}

		public int PrimitiveCount { get; private set; }

		/// <summary>
		/// The material ID for this meshset defined in bits 0-13 of <seealso cref="NJS_MESHSET.type_matId"/>.
		/// </summary>
		public ushort MaterialId
		{
			get => (ushort)(type_matId & (ushort)~NJD_MESHSET.Strip);
			set
			{
				if (value > 16383)
				{
					throw new ArgumentOutOfRangeException(nameof(value), "Number must be less than 16384");
				}

				type_matId &= (ushort)NJD_MESHSET.Strip;
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

		public Buffer IndexBuffer;
		public int IndexCount;
		public int IndexPrimitiveCount;

		public ushort type_matId;          /* meshset type and attr index
											14-15 : meshset type bits
											0-13 : material id(0-4095)     */
		public ushort nbMesh;              /* mesh count                   */
		public readonly List<short> meshes;         /* mesh array                   */
		// TODO: unused (and unimplemented)
		// ReSharper disable once CollectionNeverQueried.Global
		public readonly List<uint> attrs;           /* attribure                    */
		public readonly List<Vector3> normals;      /* mesh normal list             */
		public readonly List<NJS_COLOR> vertcolor;  /* polygon vertex color list    */
		public readonly List<NJS_TEX> vertuv;       /* polygon vertex uv list       */

		/// <summary>
		/// (Extension) Center of this meshset.
		/// </summary>
		public Vector3 Center = Vector3.Zero;
		/// <summary>
		/// (Extension) Radius of this meshset. This is not the square root.
		/// </summary>
		public float Radius = 0.0f;

		/// <summary>
		/// Constructs a <see cref="NJS_MESHSET"/> from a file.
		/// </summary>
		/// <param name="stream">A stream containing the data.</param>
		public NJS_MESHSET(Stream stream)
		{
			var buffer = new byte[SizeInBytes];
			stream.Read(buffer, 0, buffer.Length);

			IndexBuffer = null;

			type_matId = BitConverter.ToUInt16(buffer, 0x00);
			nbMesh     = BitConverter.ToUInt16(buffer, 0x02);

			var meshes_ptr    = BitConverter.ToUInt32(buffer, 0x04);
			var attrs_ptr     = BitConverter.ToUInt32(buffer, 0x08);
			var normals_ptr   = BitConverter.ToUInt32(buffer, 0x0C);
			var vertcolor_ptr = BitConverter.ToUInt32(buffer, 0x10);
			var vertuv_ptr    = BitConverter.ToUInt32(buffer, 0x14);

			meshes    = new List<short>();
			attrs     = new List<uint>();
			normals   = new List<Vector3>();
			vertcolor = new List<NJS_COLOR>();
			vertuv    = new List<NJS_TEX>();

			long position = stream.Position;

			VertexCount = 0;
			PrimitiveCount = 0;

			if (meshes_ptr != 0)
			{
				stream.Position = meshes_ptr;
				var meshesBuffer = new byte[2];

				Type = Type;

				switch (Type)
				{
					case NJD_MESHSET.Tri:
						VertexCount = nbMesh * 3;
						PrimitiveCount = VertexCount;

						for (int i = 0; i < VertexCount; i++)
						{
							stream.Read(meshesBuffer, 0, sizeof(short));
							meshes.Add(BitConverter.ToInt16(meshesBuffer, 0));
						}
						break;

					case NJD_MESHSET.Quad:
						VertexCount = nbMesh * 4;
						// This formula is used because these quads will later
						// be converted to tris.
						PrimitiveCount = nbMesh * 6 / 3;

						for (int i = 0; i < VertexCount; i++)
						{
							stream.Read(meshesBuffer, 0, sizeof(short));
							meshes.Add(BitConverter.ToInt16(meshesBuffer, 0));
						}
						break;

					case NJD_MESHSET.NSided:
					case NJD_MESHSET.Strip:
						for (int i = 0; i < nbMesh; i++)
						{
							stream.Read(meshesBuffer, 0, sizeof(short));
							short n = BitConverter.ToInt16(meshesBuffer, 0);
							meshes.Add(n);

							// n is being masked because the most significant bit indicates
							// whether or not the polygon to follow is reversed.
							n &= 0x3FFF;

							for (int j = 0; j < n; j++)
							{
								stream.Read(meshesBuffer, 0, sizeof(short));
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
				stream.Position = attrs_ptr;
				var attrsBuffer = new byte[sizeof(uint) * VertexCount];
				stream.Read(attrsBuffer, 0, attrsBuffer.Length);

				for (int i = 0; i < VertexCount; i++)
				{
					attrs.Add(BitConverter.ToUInt32(attrsBuffer, sizeof(uint) * i));
				}
			}

			if (normals_ptr != 0)
			{
				stream.Position = normals_ptr;
				var normalsBuffer = new byte[Vector3.SizeInBytes * VertexCount];
				stream.Read(normalsBuffer, 0, normalsBuffer.Length);

				for (int i = 0; i < VertexCount; i++)
				{
					Vector3 vector = Util.VectorFromStream(ref normalsBuffer, i * Vector3.SizeInBytes);
					normals.Add(vector);
				}
			}

			if (vertcolor_ptr != 0)
			{
				stream.Position = vertcolor_ptr;
				var vertcolorBuffer = new byte[sizeof(int) * VertexCount];
				stream.Read(vertcolorBuffer, 0, vertcolorBuffer.Length);

				for (int i = 0; i < VertexCount; i++)
				{
					vertcolor.Add(new NJS_COLOR(BitConverter.ToInt32(vertcolorBuffer, sizeof(int) * i)));
				}
			}

			if (vertuv_ptr != 0)
			{
				stream.Position = vertuv_ptr;
				var vertuvBuffer = new byte[NJS_TEX.SizeInBytes * VertexCount];
				stream.Read(vertuvBuffer, 0, vertuvBuffer.Length);

				for (int i = 0; i < VertexCount; i++)
				{
					vertuv.Add(new NJS_TEX(ref vertuvBuffer, NJS_TEX.SizeInBytes * i));
				}
			}

			stream.Position = position;
		}

		/// <summary>
		/// Copy constructor.
		/// </summary>
		/// <param name="meshset">Meshset to copy from.</param>
		public NJS_MESHSET(NJS_MESHSET meshset)
		{
			// TODO: copy index buffer?
			type_matId = meshset.type_matId;
			nbMesh     = meshset.nbMesh;
			meshes     = new List<short>(meshset.meshes);
			attrs      = new List<uint>(meshset.attrs);
			normals    = new List<Vector3>(meshset.normals);
			vertcolor  = new List<NJS_COLOR>(meshset.vertcolor);
			vertuv     = new List<NJS_TEX>(meshset.vertuv);
		}

		public NJS_MESHSET()
		{
			IndexBuffer         = null;
			IndexCount          = 0;
			IndexPrimitiveCount = 0;
			type_matId          = 0;
			nbMesh              = 0;
			meshes              = new List<short>();
			attrs               = new List<uint>();
			normals             = new List<Vector3>();
			vertcolor           = new List<NJS_COLOR>();
			vertuv              = new List<NJS_TEX>();
		}

		~NJS_MESHSET()
		{
			Dispose();
		}

		public void Dispose()
		{
			IndexBuffer?.Dispose();
			IndexBuffer = null;
		}

		private void CalculateStripPrimitiveCount()
		{
			PrimitiveCount = 0;
			ushort count = nbMesh;
			int i = 0;

			do
			{
				int n = meshes[i] & 0x3FFF;
				PrimitiveCount += n + 2;
				i += (n + 1);
			} while (--count > 0);

			PrimitiveCount -= 2;
		}
	}
}

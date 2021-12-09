using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using sadx_model_view.Interfaces;
using SharpDX;
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
		/// <para>Indicates that each polygon is defined by 3 vertices.
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
	/// <para>Defines a list of polygons and their type for <see cref="NJS_MODEL"/>.</para>
	/// See also:
	/// <seealso cref="NJD_MESHSET"/>
	/// <seealso cref="NJS_MODEL"/>
	/// </summary>
	public class NJS_MESHSET : IDisposable, IInvalidatable
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
		/// The number of vertices varies from <see cref="nbMesh"/> to different degrees
		/// depending on the type of polygon managed by this meshset.
		/// (see <seealso cref="Type"/>, <seealso cref="NJD_MESHSET"/>)
		/// </para>
		/// </summary>
		public int VertexCount { get; }

		public Buffer? IndexBuffer;
		public int     IndexCount;

		public ushort type_matId;			/* meshset type and attr index
											   14-15 : meshset type bits
											   0-13 : material id(0-4095)     */
		public ushort nbMesh;				/* mesh count                   */
		public readonly List<short> meshes; /* mesh array                   */
		// TODO: unused (and unimplemented)
		// ReSharper disable once CollectionNeverQueried.Global
		public readonly List<uint>      attrs;     /* attribure                    */
		public readonly List<Vector3>   normals;   /* mesh normal list             */
		public readonly List<NJS_COLOR> vertcolor; /* polygon vertex color list    */
		public readonly List<NJS_TEX>   vertuv;    /* polygon vertex uv list       */

		private Vector3[] _points = Array.Empty<Vector3>();

		/// <summary>
		/// Constructs a <see cref="NJS_MESHSET"/> from a file.
		/// </summary>
		/// <param name="stream">A stream containing the data.</param>
		public NJS_MESHSET(Stream stream)
		{
			byte[] buffer = new byte[SizeInBytes];
			stream.Read(buffer, 0, buffer.Length);

			IndexBuffer = null;

			type_matId = BitConverter.ToUInt16(buffer, 0x00);
			nbMesh     = BitConverter.ToUInt16(buffer, 0x02);

			uint meshesOffset    = BitConverter.ToUInt32(buffer, 0x04);
			uint attrsOffset     = BitConverter.ToUInt32(buffer, 0x08);
			uint normalsOffset   = BitConverter.ToUInt32(buffer, 0x0C);
			uint vertcolorOffset = BitConverter.ToUInt32(buffer, 0x10);
			uint vertuvOffset    = BitConverter.ToUInt32(buffer, 0x14);

			meshes    = new List<short>();
			attrs     = new List<uint>();
			normals   = new List<Vector3>();
			vertcolor = new List<NJS_COLOR>();
			vertuv    = new List<NJS_TEX>();

			long position = stream.Position;

			VertexCount = 0;

			if (meshesOffset != 0)
			{
				stream.Position = meshesOffset;
				byte[] meshesBuffer = new byte[2];

				Type = Type;

				switch (Type)
				{
					case NJD_MESHSET.Tri:
						VertexCount = nbMesh * 3;

						for (int i = 0; i < VertexCount; i++)
						{
							stream.Read(meshesBuffer, 0, sizeof(short));
							meshes.Add(BitConverter.ToInt16(meshesBuffer, 0));
						}

						break;

					case NJD_MESHSET.Quad:
						VertexCount = nbMesh * 4;

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

						if (VertexCount != meshes.Count - nbMesh)
						{
							throw new Exception("Recorded vertex count is incorrect.");
						}

						break;

					default:
						throw new ArgumentOutOfRangeException();
				}
			}

			if (attrsOffset != 0)
			{
				stream.Position = attrsOffset;
				byte[] attrsBuffer = new byte[sizeof(uint) * VertexCount];
				stream.Read(attrsBuffer, 0, attrsBuffer.Length);

				for (int i = 0; i < VertexCount; i++)
				{
					attrs.Add(BitConverter.ToUInt32(attrsBuffer, sizeof(uint) * i));
				}
			}

			// HACK: This is wrong.
			if (normalsOffset != 0)
			{
				stream.Position = normalsOffset;
				byte[] normalsBuffer = new byte[Vector3.SizeInBytes * VertexCount];
				stream.Read(normalsBuffer, 0, normalsBuffer.Length);

				for (int i = 0; i < VertexCount; i++)
				{
					Vector3 vector = Util.VectorFromStream(in normalsBuffer, i * Vector3.SizeInBytes);
					normals.Add(vector);
				}
			}

			if (vertcolorOffset != 0)
			{
				stream.Position = vertcolorOffset;
				byte[] vertcolorBuffer = new byte[sizeof(int) * VertexCount];
				stream.Read(vertcolorBuffer, 0, vertcolorBuffer.Length);

				for (int i = 0; i < VertexCount; i++)
				{
					vertcolor.Add(new NJS_COLOR(BitConverter.ToInt32(vertcolorBuffer, sizeof(int) * i)));
				}
			}

			if (vertuvOffset != 0)
			{
				stream.Position = vertuvOffset;
				byte[] vertuvBuffer = new byte[NJS_TEX.SizeInBytes * VertexCount];
				stream.Read(vertuvBuffer, 0, vertuvBuffer.Length);

				for (int i = 0; i < VertexCount; i++)
				{
					vertuv.Add(new NJS_TEX(ref vertuvBuffer, NJS_TEX.SizeInBytes * i));
				}
			}

			stream.Position = position;

			meshes.TrimExcess();
			attrs.TrimExcess();
			normals.TrimExcess();
			vertcolor.TrimExcess();
			vertuv.TrimExcess();
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
			IndexBuffer = null;
			IndexCount  = 0;
			type_matId  = 0;
			nbMesh      = 0;
			meshes      = new List<short>();
			attrs       = new List<uint>();
			normals     = new List<Vector3>();
			vertcolor   = new List<NJS_COLOR>();
			vertuv      = new List<NJS_TEX>();
		}

		/// <summary>
		/// Updates the specified vertex with UV coordinates and/or colors.
		/// Creates a new vertex if a vertex is used more than once with different colors or UVs.
		/// </summary>
		/// <param name="vertices">List of vertices to update.</param>
		/// <param name="localIndex">Index of the current UV coordinates and/or color to use.</param>
		/// <param name="vertexIndex">Index of the vertex in <paramref name="vertices"/>.</param>
		/// <returns><paramref name="localIndex"/> if the vertex was updated, or a new index if a new vertex was added.</returns>
		private short UpdateVertex(IList<Vertex> vertices, int localIndex, int vertexIndex)
		{
			bool added  = false;
			int result = vertexIndex;

			Vertex vertex = vertices[vertexIndex];

			if (vertcolor.Count != 0)
			{
				if (vertex.Diffuse.HasValue)
				{
					result = (short)vertices.Count;
					vertices.Add(vertex);
					added = true;
				}

				NJS_BGRA vcolor = vertcolor[localIndex].argb;
				vertex.Diffuse = new ColorBGRA(vcolor.b, vcolor.g, vcolor.r, vcolor.a);
			}

			if (vertuv.Count != 0)
			{
				var uv = new Vector2(vertuv[localIndex].u / 255.0f, vertuv[localIndex].v / 255.0f);

				if (!added && vertex.UV.HasValue)
				{
					result = (short)vertices.Count;
					vertices.Add(vertex);
				}

				vertex.UV = uv;
			}

			vertices[result] = vertex;
			return (short)result;
		}

		public readonly List<Triangle> Triangles = new List<Triangle>();

		public void CommitIndexBuffer(Renderer device, List<Vertex> vertices)
		{
			Triangles.Clear();
			var indices = new List<short>();

			switch (Type)
			{
				case NJD_MESHSET.Tri:
					for (int i = VertexCount - 1; i >= 0; i--)
					{
						short n = meshes[i];
						UpdateVertex(vertices, i, n);
						indices.Add(n);
					}

					break;

				case NJD_MESHSET.Quad:
					for (int i = 0; i < VertexCount; i += 4)
					{
						short v0 = UpdateVertex(vertices, i + 0, meshes[i + 0]);
						short v1 = UpdateVertex(vertices, i + 1, meshes[i + 1]);
						short v2 = UpdateVertex(vertices, i + 2, meshes[i + 2]);
						short v3 = UpdateVertex(vertices, i + 3, meshes[i + 3]);

						indices.Add(v3);
						indices.Add(v1);
						indices.Add(v2);
						indices.Add(v2);
						indices.Add(v1);
						indices.Add(v0);
					}

					break;

				case NJD_MESHSET.NSided:
				case NJD_MESHSET.Strip:
				{
					int index = 0;
					for (int i = 0; i < nbMesh; i++)
					{
						short n    = meshes[index++];
						bool  flip = (n & 0x8000) == 0;
						n &= 0x3FFF;

						var tempIndices = new List<short>();

						for (int j = 0; j < n; j++)
						{
							// i - (k + 1), where i = index and k = mesh number
							tempIndices.Add(UpdateVertex(vertices, index - (i + 1), meshes[index++]));
						}

						for (int k = 0; k < tempIndices.Count - 2; k++)
						{
							short v0 = tempIndices[k + 0];
							short v1 = tempIndices[k + 1];
							short v2 = tempIndices[k + 2];

							flip = !flip;
							if (!flip)
							{
								indices.Add(v2);
								indices.Add(v1);
								indices.Add(v0);
							}
							else
							{
								indices.Add(v2);
								indices.Add(v0);
								indices.Add(v1);
							}
						}
					}

					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}

			_points = indices.Distinct().Select(i => (Vector3)vertices[i].Position).ToArray();

			var tri = new Triangle();
			int triN = 0;

			foreach (short i in indices)
			{
				switch (triN++)
				{
					case 0:
						tri.A = vertices[i].Position;
						break;

					case 1:
						tri.B = vertices[i].Position;
						break;

					case 2:
						tri.C = vertices[i].Position;
						Triangles.Add(tri);
						triN = 0;
						break;

					default:
						throw new ArgumentOutOfRangeException();
				}
			}

			IndexCount = indices.Count;

			IndexBuffer?.Dispose();
			IndexBuffer = device.CreateIndexBuffer(indices, IndexCount * sizeof(short));
		}

		private Vector3[] TransformedPoints(ref Matrix m)
		{
			if (m.IsIdentity || _points.Length < 1)
			{
				return _points;
			}

			var transformed = new Vector3[_points.Length];

			for (int i = 0; i < transformed.Length; i++)
			{
				Vector3.Transform(ref _points[i], ref m, out transformed[i]);
			}

			return transformed;
		}

		public BoundingBox GetWorldSpaceBoundingBox()
		{
			Matrix m = MatrixStack.Peek();
			BoundingBox worldBox  = BoundingBox.FromPoints(TransformedPoints(ref m));
			return worldBox;
		}

		public void Dispose()
		{
			IndexBuffer?.Dispose();
		}

		public bool IsInvalid
		{
			get => true;
			set
			{
		
			}
		}
	}
}
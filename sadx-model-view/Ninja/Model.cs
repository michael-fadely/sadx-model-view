using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using sadx_model_view.Forms;
using SharpDX;
using Buffer = SharpDX.Direct3D11.Buffer;
using Matrix = SharpDX.Matrix;

namespace sadx_model_view.Ninja
{
	public class NJS_MODEL : IDisposable
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => 0x28;


		public DisplayState DisplayState { get; set; }

		/// <summary>
		/// Constructs <see cref="NJS_MODEL"/>, its points, normals, materials, and meshsets from a file.<para/>
		/// See also:
		/// <seealso cref="NJS_MESHSET"/>
		/// <seealso cref="NJS_MATERIAL"/>
		/// </summary>
		/// <param name="stream">A stream containing the data.</param>
		public NJS_MODEL(Stream stream)
		{
			var buffer = new byte[SizeInBytes];
			stream.Read(buffer, 0, buffer.Length);

			nbPoint   = BitConverter.ToInt32(buffer, 0x08);
			nbMeshset = BitConverter.ToUInt16(buffer, 0x14);
			nbMat     = BitConverter.ToUInt16(buffer, 0x16);
			center.X  = BitConverter.ToSingle(buffer, 0x18);
			center.Y  = BitConverter.ToSingle(buffer, 0x18 + 4);
			center.Z  = BitConverter.ToSingle(buffer, 0x18 + 8);
			r         = BitConverter.ToSingle(buffer, 0x24);
			points    = new List<Vector3>();
			normals   = new List<Vector3>();
			meshsets  = new List<NJS_MESHSET>();
			mats      = new List<NJS_MATERIAL>();

			var position = stream.Position;

			if (nbPoint > 0)
			{
				var points_ptr = BitConverter.ToUInt32(buffer, 0x00);

				if (points_ptr > 0)
				{
					stream.Position = points_ptr;

					for (int i = 0; i < nbPoint; i++)
					{
						Vector3 v = Util.VectorFromStream(stream);
						points.Add(v);
					}
				}

				int normals_ptr = BitConverter.ToInt32(buffer, 0x04);

				if (normals_ptr > 0)
				{
					stream.Position = normals_ptr;

					for (int i = 0; i < nbPoint; i++)
					{
						Vector3 v = Util.VectorFromStream(stream);
						normals.Add(v);
					}
				}
			}

			uint meshsets_ptr = BitConverter.ToUInt32(buffer, 0x0C);
			if (nbMeshset > 0 && meshsets_ptr > 0)
			{
				stream.Position = meshsets_ptr;
				for (int i = 0; i < nbMeshset; i++)
				{
					meshsets.Add(new NJS_MESHSET(stream));
				}
			}

			uint mats_ptr = BitConverter.ToUInt32(buffer, 0x10);
			if (nbMat > 0 && mats_ptr > 0)
			{
				stream.Position = mats_ptr;
				for (int i = 0; i < nbMat; i++)
				{
					mats.Add(new NJS_MATERIAL(stream));
				}
			}

			stream.Position = position;
		}

		/// <summary>
		/// Copy constructor.
		/// </summary>
		/// <param name="model">The model to copy from.</param>
		public NJS_MODEL(NJS_MODEL model)
		{
			points = new List<Vector3>(model.points);
			normals = new List<Vector3>(model.normals);
			nbPoint = points.Count;

			// TODO: copy vertex buffer?

			meshsets = new List<NJS_MESHSET>();
			foreach (NJS_MESHSET meshset in model.meshsets)
			{
				meshsets.Add(new NJS_MESHSET(meshset));
			}

			nbMeshset = (ushort)meshsets.Count;

			mats = new List<NJS_MATERIAL>();
			foreach (NJS_MATERIAL mat in model.mats)
			{
				mats.Add(new NJS_MATERIAL(mat));
			}

			nbMat = (ushort)mats.Count;

			center = model.center;
			r = model.r;
		}

		public NJS_MODEL()
		{
			points       = new List<Vector3>();
			normals      = new List<Vector3>();
			nbPoint      = 0;
			meshsets     = new List<NJS_MESHSET>();
			mats         = new List<NJS_MATERIAL>();
			nbMeshset    = 0;
			nbMat        = 0;
			center       = Vector3.Zero;
			r            = 0.0f;
			VertexBuffer = null;
		}

		public void Dispose()
		{
			foreach (NJS_MESHSET set in meshsets)
			{
				set.Dispose();
			}

			meshsets.Clear();
			VertexBuffer?.Dispose();
			DisplayState?.Dispose();
		}

		public readonly List<Vector3> points;    // vertex list
		public readonly List<Vector3> normals;   // vertex normal list
		public readonly int nbPoint;             // vertex count
		public List<NJS_MESHSET> meshsets;       // meshset list
		public readonly List<NJS_MATERIAL> mats; // material list
		public ushort nbMeshset;                 // meshset count
		public readonly ushort nbMat;            // material count
		public Vector3 center;                   // model center
		public float r;                          // radius

		public Buffer VertexBuffer;

		public void CommitVertexBuffer(Renderer device)
		{
			if (normals.Count != 0 && points.Count != normals.Count)
			{
				throw new Exception("Vertex count deviates from normal count.");
			}

			List<Vertex> vertices = points.Select((point, i) => new Vertex
			{
				position = point,
				normal   = normals.Count > 0 ? normals[i] : Vector3.Up,
				diffuse  = null,
				uv       = null
			}).ToList();

			foreach (NJS_MESHSET set in meshsets)
			{
				var indices = new List<short>();

				switch (set.Type)
				{
					case NJD_MESHSET.Tri:
						for (int i = set.VertexCount - 1; i >= 0; i--)
						{
							short n = set.meshes[i];
							UpdateVertex(set, vertices, i, n);
							indices.Add(n);
						}
						break;

					case NJD_MESHSET.Quad:
						for (int i = 0; i < set.VertexCount; i += 4)
						{
							short v0 = UpdateVertex(set, vertices, i + 0, set.meshes[i + 0]);
							short v1 = UpdateVertex(set, vertices, i + 1, set.meshes[i + 1]);
							short v2 = UpdateVertex(set, vertices, i + 2, set.meshes[i + 2]);
							short v3 = UpdateVertex(set, vertices, i + 3, set.meshes[i + 3]);

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
							for (int i = 0; i < set.nbMesh; i++)
							{
								short n = set.meshes[index++];
								bool flip = (n & 0x8000) == 0;
								n &= 0x3FFF;

								var _indices = new List<short>();

								for (int j = 0; j < n; j++)
								{
									// i - (k + 1), where i = index and k = mesh number
									_indices.Add(UpdateVertex(set, vertices, index - (i + 1), set.meshes[index++]));
								}

								for (int k = 0; k < _indices.Count - 2; k++)
								{
									short v0 = _indices[k + 0];
									short v1 = _indices[k + 1];
									short v2 = _indices[k + 2];

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

				// This is used for transparent sorting.
				List<Vector3> refPoints = indices.Distinct().Select(i => (Vector3)vertices[i].position).ToList();

				if (refPoints.Count > 0)
				{
					set.Center = refPoints.Aggregate((a, b) => a + b) / refPoints.Count;
					set.Radius = refPoints.Select(point => point - set.Center).Select(distance => distance.Length()).Concat(new[] { 0.0f }).Max();
				}

				set.IndexPrimitiveCount = indices.Count / 3;
				set.IndexCount = indices.Count;
				set.IndexBuffer = device.CreateIndexBuffer(indices, set.IndexCount * sizeof(short));
			}

			VertexBuffer = device.CreateVertexBuffer(vertices);
		}

		/// <summary>
		/// Updates the specified vertex with UV cooridnates and/or colors.
		/// Creates a new vertex if a vertex is used more than once with different colors or UVs.
		/// </summary>
		/// <param name="set">Meshset containing metadata.</param>
		/// <param name="vertices">List of vertices to update.</param>
		/// <param name="localIndex">Index of the current UV cooridnates and/or color to use.</param>
		/// <param name="vertexIndex">Index of the vertex in <paramref name="vertices"/>.</param>
		/// <returns><paramref name="localIndex"/> if the vertex was updated, or a new index if a new vertex was added.</returns>
		private static short UpdateVertex(NJS_MESHSET set, IList<Vertex> vertices, int localIndex, int vertexIndex)
		{
			bool added = false;
			int result = vertexIndex;

			Vertex vertex = vertices[vertexIndex];

			if (set.vertcolor.Count != 0)
			{
				if (vertex.diffuse.HasValue)
				{
					result = (short)vertices.Count;
					vertices.Add(vertex);
					added = true;
				}

				NJS_BGRA vcolor = set.vertcolor[localIndex].argb;
				vertex.diffuse = new ColorBGRA(vcolor.b, vcolor.g, vcolor.r, vcolor.a);
			}

			if (set.vertuv.Count != 0)
			{
				var uv = new Vector2(set.vertuv[localIndex].u / 255.0f, set.vertuv[localIndex].v / 255.0f);

				if (!added && vertex.uv.HasValue)
				{
					result = (short)vertices.Count;
					vertices.Add(vertex);
				}

				vertex.uv = uv;
			}

			vertices[result] = vertex;
			return (short)result;
		}

		// TODO: Generate a view frustum and do culling that way.
		public bool IsVisible(Camera camera)
		{
			NJS_SCREEN screen = MainForm.Screen;
			float radius = r * 1.2f;

			MatrixStack.Push();

			Matrix inverse = camera.InverseView;
			MatrixStack.Multiply(ref inverse);

			MatrixStack.CalcPoint(ref center, out Vector3 v);

			MatrixStack.Pop();

			if (radius - v.Z < camera.MaxDrawDistance)
			{
				return false;
			}

			float v2 = -v.Z - radius;
			if (v2 > camera.MinDrawDistance)
			{
				return false;
			}

			float v6 = -1.0f / v2;
			float v3 = screen.dist;
			if ((v.X + radius) * v3 * v6 + screen.cx < 0.0 || screen.w < (v.X - radius) * v3 * v6 + screen.cx)
			{
				return false;
			}

			float v4 = -(0.85000002f * screen.dist);
			return (v.Y - radius) * v4 * v6 + screen.cy >= 0.0
			       && screen.h >= (v.Y + radius) * v4 * v6 + screen.cy;
		}

		public ShaderMaterial GetSADXMaterial(Renderer device, NJS_MATERIAL material)
		{
			if (DisplayState == null)
			{
				DisplayState = device.CreateSADXDisplayState(material);
			}

			NJD_FLAG flags = FlowControl.Apply(material.attrflags);

			if ((flags & NJD_FLAG.UseTexture) == 0)
			{
				device.SetTexture(0, -1);
			}
			else
			{
				int n = (int)material.attr_texId;
				device.SetTexture(0, n);
			}

			var m = new ShaderMaterial
			{
				Diffuse     = new Color4(material.diffuse.color),
				Specular    = new Color4(material.specular.color),
				Exponent    = material.exponent,
				UseAlpha    = (flags & NJD_FLAG.UseAlpha) != 0,
				UseEnv      = (flags & NJD_FLAG.UseEnv) != 0,
				UseTexture  = (flags & NJD_FLAG.UseTexture) != 0,
				UseSpecular = (flags & NJD_FLAG.IgnoreSpecular) == 0,
				UseLight    = (flags & NJD_FLAG.IgnoreLight) == 0
			};

			return m;
		}
	}
}

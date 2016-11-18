using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpDX;
using SharpDX.Direct3D9;

namespace sadx_model_view.Ninja
{
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

					for (var i = 0; i < nbPoint; i++)
					{
						var v = Util.VectorFromStream(stream);
						points.Add(v);
					}
				}

				var normals_ptr = BitConverter.ToInt32(buffer, 0x04);

				if (normals_ptr > 0)
				{
					stream.Position = normals_ptr;

					for (var i = 0; i < nbPoint; i++)
					{
						var v = Util.VectorFromStream(stream);
						normals.Add(v);
					}
				}
			}

			var meshsets_ptr = BitConverter.ToUInt32(buffer, 0x0C);
			if (nbMeshset > 0 && meshsets_ptr > 0)
			{
				stream.Position = meshsets_ptr;
				for (var i = 0; i < nbMeshset; i++)
					meshsets.Add(new NJS_MESHSET(stream));
			}

			var mats_ptr = BitConverter.ToUInt32(buffer, 0x10);
			if (nbMat > 0 && mats_ptr > 0)
			{
				stream.Position = mats_ptr;
				for (var i = 0; i < nbMat; i++)
					mats.Add(new NJS_MATERIAL(stream));
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

			meshsets = new List<NJS_MESHSET>();
			foreach (var meshset in model.meshsets)
			{
				meshsets.Add(new NJS_MESHSET(meshset));
			}

			nbMeshset = (ushort)meshsets.Count;

			mats = new List<NJS_MATERIAL>();
			foreach (var mat in model.mats)
			{
				mats.Add(new NJS_MATERIAL(mat));
			}

			nbMat = (ushort)mats.Count;

			center = model.center;
			r = model.r;
		}

		public NJS_MODEL()
		{
			points             = new List<Vector3>();
			normals            = new List<Vector3>();
			nbPoint            = 0;
			meshsets           = new List<NJS_MESHSET>();
			mats               = new List<NJS_MATERIAL>();
			nbMeshset          = 0;
			nbMat              = 0;
			center             = Vector3.Zero;
			r                  = 0.0f;
			vertexBuffer       = null;
			vertexBufferLength = 0;
		}

		~NJS_MODEL()
		{
			Dispose();
		}

		public void Dispose()
		{
			foreach (var set in meshsets)
			{
				set.Dispose();
			}

			meshsets.Clear();
		}

		public readonly List<Vector3> points;       // vertex list
		public readonly List<Vector3> normals;      // vertex normal list
		public readonly int nbPoint;                // vertex count
		public List<NJS_MESHSET> meshsets;          // meshset list
		public readonly List<NJS_MATERIAL> mats;    // material list
		public ushort nbMeshset;                    // meshset count
		public readonly ushort nbMat;               // material count
		public Vector3 center;                      // model center
		public float r;                             // radius

		private VertexBuffer vertexBuffer;
		private int vertexBufferLength;

		public void CommitVertexBuffer(Device device)
		{
			if (normals.Count != 0 && points.Count != normals.Count)
				throw new Exception("Vertex count deviates from normal count.");

			List<Vertex> vertices = points.Select((point, i) => new Vertex
			{
				position = point,
				normal   = normals.Count > 0 ? normals[i] : Vector3.Up,
				diffuse  = new ColorBGRA(0, 0, 0, 0),
				uv       = Vector2.Zero
			}).ToList();

			foreach (NJS_MESHSET set in meshsets)
			{
				var indices = new List<short>();

				switch (set.Type)
				{
					case NJD_MESHSET.Tri:
						for (int i = set.VertexCount - 1; i > 0; i--)
						{
							var n = set.meshes[i];
							UpdateVertex(set, vertices, i, n);
							indices.Add(n);
						}
						break;

					case NJD_MESHSET.Quad:
						for (int i = 0; i < set.VertexCount; i += 4)
						{
							var v0 = UpdateVertex(set, vertices, i + 0, set.meshes[i + 0]);
							var v1 = UpdateVertex(set, vertices, i + 1, set.meshes[i + 1]);
							var v2 = UpdateVertex(set, vertices, i + 2, set.meshes[i + 2]);
							var v3 = UpdateVertex(set, vertices, i + 3, set.meshes[i + 3]);

							indices.Add(v3);
							indices.Add(v1);
							indices.Add(v2);
							indices.Add(v2);
							indices.Add(v1);
							indices.Add(v0);
						}

						break;

					case NJD_MESHSET.Strip:
					case NJD_MESHSET.NSided:
						{
							int index = 0;
							int v = 0;
							for (int i = 0; i < set.nbMesh; i++)
							{
								var n = set.meshes[index++];
								var flip = (n & 0x8000) == 0;
								n &= 0x3FFF;

								var _indices = new List<short>();

								for (int j = 0; j < n; j++)
								{
									_indices.Add(UpdateVertex(set, vertices, v++, set.meshes[index + j]));
								}

								for (int k = 0; k < _indices.Count - 2; k++)
								{
									var v0 = _indices[k + 0];
									var v1 = _indices[k + 1];
									var v2 = _indices[k + 2];

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

								index += n;
							}

							break;
						}

					default:
						throw new ArgumentOutOfRangeException();
				}

				set.IndexPrimitiveCount = indices.Count / 3;
				set.IndexCount = indices.Count;
				var indexSize = set.IndexCount * sizeof(short);

				set.IndexBuffer = new IndexBuffer(device, indexSize, Usage.None, Pool.Managed, true);

				using (var stream = set.IndexBuffer.Lock(0, set.IndexBuffer.Description.Size, LockFlags.None))
				{
					foreach (var i in indices)
					{
						stream.Write(i);
					}

					if (stream.RemainingLength != 0)
					{
						throw new Exception("Failed to fill index buffer.");
					}
				}

				set.IndexBuffer.Unlock();
			}

			CreateVertexBuffer(device, vertices);
		}

		private void CreateVertexBuffer(Device device, IReadOnlyCollection<Vertex> vertices)
		{
			vertexBufferLength = vertices.Count;
			var vertexSize = vertices.Count * Vertex.SizeInBytes;
			vertexBuffer = new VertexBuffer(device, vertexSize, Usage.None, Vertex.Format, Pool.Managed);

			using (var stream = vertexBuffer.Lock(0, vertexSize, LockFlags.None))
			{
				foreach (var v in vertices)
				{
					stream.Write(v.position.X);
					stream.Write(v.position.Y);
					stream.Write(v.position.Z);

					stream.Write(v.normal.X);
					stream.Write(v.normal.Y);
					stream.Write(v.normal.Z);

					stream.Write(v.diffuse.R);
					stream.Write(v.diffuse.G);
					stream.Write(v.diffuse.B);
					stream.Write(v.diffuse.A);

					stream.Write(v.uv.X);
					stream.Write(v.uv.Y);
				}

				if (stream.RemainingLength != 0)
				{
					throw new Exception("Failed to fill vertex buffer.");
				}
			}

			vertexBuffer.Unlock();
		}

		private static short UpdateVertex(NJS_MESHSET set, IList<Vertex> vertices, int localIndex, int globalIndex)
		{
			bool modified = false;
			bool added    = false;
			var result    = globalIndex;

			ColorBGRA color;
			if (set.vertcolor.Count != 0)
			{
				var vcolor = set.vertcolor[localIndex].argb;
				color = new ColorBGRA(vcolor.b, vcolor.g, vcolor.r, vcolor.a);
			}
			else
			{
				color = Color.White;
			}

			var vertex = vertices[globalIndex];

			if (vertex.diffuse != new ColorBGRA(0, 0, 0, 0) && vertex.diffuse != color)
			{
				result = (short)vertices.Count;
				vertices.Add(vertex);
				added = true;
			}

			if (added || vertex.diffuse != color)
			{
				vertex.diffuse = color;
				modified = true;
			}

			if (set.vertuv.Count != 0)
			{
				var uv = new Vector2(set.vertuv[localIndex].u / 255.0f, set.vertuv[localIndex].v / 255.0f);

				if (!added && vertex.uv != Vector2.Zero && vertex.uv != uv)
				{
					result = (short)vertices.Count;
					vertices.Add(vertex);
					added = true;
				}

				if (added || vertex.uv != uv)
				{
					vertex.uv = uv;
					modified = true;
				}
			}

			if (!modified)
			{
				return (short)globalIndex;
			}

			vertices[result] = vertex;
			return (short)result;
		}

		/// <summary>
		/// This is the texture transformation matrix that SADX uses to anything with an environment map.
		/// </summary>
		private static readonly Matrix environmentMapTransform = new Matrix(-0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.5f, 0.5f, 0.0f, 1.0f);

		private static void SetSADXMaterial(Device device, NJS_MATERIAL material)
		{
			if (material == null)
				return;

			var flags = material.attrflags;

			if (!flags.HasFlag(NJD_FLAG.UseTexture))
			{
				device.SetTexture(0, null);
			}
			else
			{
				var n = (int)material.attr_texId;
				if (n < MainForm.TexturePool.Count)
				{
					device.SetTexture(0, MainForm.TexturePool[n]);
				}

				if (flags.HasFlag(NJD_FLAG.Pick))
				{
					// TODO: not even implemented in SADX
				}

				if (flags.HasFlag(NJD_FLAG.UseAnisotropic))
				{
					// TODO: not even implemented in SADX
				}

				device.SetSamplerState(0, SamplerState.AddressV, flags.HasFlag(NJD_FLAG.ClampV) ? TextureAddress.Clamp : TextureAddress.Wrap);
				device.SetSamplerState(0, SamplerState.AddressU, flags.HasFlag(NJD_FLAG.ClampU) ? TextureAddress.Clamp : TextureAddress.Wrap);
				device.SetSamplerState(0, SamplerState.AddressV, flags.HasFlag(NJD_FLAG.FlipV) ? TextureAddress.Mirror : TextureAddress.Wrap);
				device.SetSamplerState(0, SamplerState.AddressU, flags.HasFlag(NJD_FLAG.FlipU) ? TextureAddress.Mirror : TextureAddress.Wrap);

				if (flags.HasFlag(NJD_FLAG.UseEnv))
				{
					device.SetTextureStageState(0, TextureStage.TextureTransformFlags, TextureArgument.Texture);
					device.SetTransform(TransformState.Texture0, environmentMapTransform);
					device.SetTextureStageState(0, TextureStage.TexCoordIndex, (int)TextureCoordIndex.CameraSpaceNormal);
				}
				else
				{
					device.SetTextureStageState(0, TextureStage.TextureTransformFlags, TextureArgument.Diffuse);
					device.SetTransform(TransformState.Texture0, Matrix.Identity);
					device.SetTextureStageState(0, TextureStage.TexCoordIndex, 0);
				}

				if (flags.HasFlag(NJD_FLAG.UseAlpha))
				{
					device.SetRenderState(RenderState.AlphaBlendEnable, true);
					device.SetRenderState(RenderState.AlphaTestEnable, true);
					device.SetRenderState(RenderState.AlphaRef, 16);
					device.SetRenderState(RenderState.DiffuseMaterialSource, ColorSource.Material);

					device.SetTextureStageState(0, TextureStage.AlphaOperation, TextureOperation.Modulate);
				}
				else
				{
					device.SetRenderState(RenderState.AlphaBlendEnable, false);
					device.SetRenderState(RenderState.AlphaRef, 0);
					device.SetRenderState(RenderState.DiffuseMaterialSource, ColorSource.Color1);

					device.SetTextureStageState(0, TextureStage.AlphaOperation, TextureOperation.SelectArg2);
				}
			}

			device.SetRenderState(RenderState.SpecularEnable, !flags.HasFlag(NJD_FLAG.IgnoreSpecular));
			device.SetRenderState(RenderState.CullMode, flags.HasFlag(NJD_FLAG.DoubleSide) ? Cull.None : MainForm.CullMode);

			if (flags.HasFlag(NJD_FLAG.UseFlat))
			{
				// TODO: not even implemented in SADX
			}

			device.EnableLight(0, !flags.HasFlag(NJD_FLAG.IgnoreLight));

			var m = new Material
			{
				Specular = new Color4(material.specular.color),
				Diffuse  = new Color4(material.diffuse.color),
				Power    = material.exponent
			};

			// default SADX behavior is to use diffuse for both ambient and diffuse.
			m.Ambient = m.Diffuse;
			m.Specular.A = 0.0f;

			device.Material = m;
		}

		public void Draw(Device device)
		{
			// Set the correct vertex format for model rendering.
			device.VertexFormat = Vertex.Format;

			foreach (var set in meshsets)
			{
				if (mats.Count > 0)
				{
					var i = set.MaterialId;

					if (i < mats.Count)
					{
						var material = mats[i];

						// Set up rendering parameters based on this material.
						SetSADXMaterial(device, material);
					}
				}

				// Set the stream source to the current meshset's vertex buffer.
				device.SetStreamSource(0, vertexBuffer, 0, Vertex.SizeInBytes);
				device.Indices = set.IndexBuffer;

				// Draw the model.
				device.DrawIndexedPrimitive(PrimitiveType.TriangleList,
					0, 0, vertexBufferLength, 0, set.IndexPrimitiveCount);
			}
		}
	}
}

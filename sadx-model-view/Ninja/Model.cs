using System;
using System.Collections.Generic;
using System.IO;
using SharpDX;
using SharpDX.Direct3D9;
using SharpDX.Mathematics.Interop;

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
			points    = new List<Vector3>();
			normals   = new List<Vector3>();
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
						Vector3 v = Util.VectorFromStream(file);
						points.Add(v);
					}
				}

				var normals_ptr = BitConverter.ToInt32(buffer, 0x04);

				if (normals_ptr > 0)
				{
					file.Position = normals_ptr;

					for (var i = 0; i < nbPoint; i++)
					{
						Vector3 v = Util.VectorFromStream(file);
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

		public List<Vector3> points;       /* vertex list                  */
		public List<Vector3> normals;      /* vertex normal list           */
		public int nbPoint;                /* vertex count                 */
		public List<NJS_MESHSET> meshsets; /* meshset list                 */
		public List<NJS_MATERIAL> mats;    /* material list                */
		public ushort nbMeshset;           /* meshset count                */
		public ushort nbMat;               /* material count               */
		public Vector3 center;             /* model center                 */
		public float r;                    /* radius                       */

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
								normal   = normals.Count != 0 ? normals[point_i] : Vector3.Up,
								diffuse  = color,
								uv       = uv
							};

							vertices.Add(vertex);
						}
						break;

					case NJD_MESHSET.Quad:
						size = Vertex.SizeInBytes * (set.PrimitiveCount * 3);

						var indeces = new List<KeyValuePair<int, short>>();

						for (var i = 0; i < set.VertexCount; i += 4)
						{
							indeces.Add(new KeyValuePair<int, short>(i + 0, set.meshes[i + 0]));
							indeces.Add(new KeyValuePair<int, short>(i + 1, set.meshes[i + 1]));
							indeces.Add(new KeyValuePair<int, short>(i + 2, set.meshes[i + 2]));
							indeces.Add(new KeyValuePair<int, short>(i + 2, set.meshes[i + 2]));
							indeces.Add(new KeyValuePair<int, short>(i + 1, set.meshes[i + 1]));
							indeces.Add(new KeyValuePair<int, short>(i + 3, set.meshes[i + 3]));
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
								normal   = normals.Count != 0 ? normals[pair.Value] : Vector3.Up,
								diffuse  = color,
								uv       = uv
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
									normal   = normals.Count != 0 ? normals[point_i] : Vector3.Up,
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

			var flags = material.attrflags;

			if (flags.HasFlag(NJD_FLAG.UseTexture))
			{
				if (flags.HasFlag(NJD_FLAG.Pick))
				{
					// TODO: not even implemented in SADX
				}

				if (flags.HasFlag(NJD_FLAG.UseAnisotropic))
				{
					// TODO: not even implemented in SADX
				}

				if (flags.HasFlag(NJD_FLAG.ClampV))
				{
					device.SetSamplerState(0, SamplerState.AddressV, TextureAddress.Clamp);
				}

				if (flags.HasFlag(NJD_FLAG.ClampU))
				{
					device.SetSamplerState(0, SamplerState.AddressU, TextureAddress.Clamp);
				}

				if (flags.HasFlag(NJD_FLAG.FlipV))
				{
					device.SetSamplerState(0, SamplerState.AddressV, TextureAddress.Mirror);
				}

				if (flags.HasFlag(NJD_FLAG.FlipU))
				{
					device.SetSamplerState(0, SamplerState.AddressU, TextureAddress.Mirror);
				}

				if (flags.HasFlag(NJD_FLAG.UseEnv))
				{
					// TODO
				}

				if (flags.HasFlag(NJD_FLAG.UseAlpha))
				{
					// TODO
				}
			}

			device.SetRenderState(RenderState.SpecularEnable, !flags.HasFlag(NJD_FLAG.IgnoreSpecular));
			device.SetRenderState(RenderState.CullMode, flags.HasFlag(NJD_FLAG.DoubleSide) ? Cull.None : Cull.Counterclockwise);

			if (flags.HasFlag(NJD_FLAG.UseFlat))
			{
				// TODO: not even implemented in SADX
			}

			device.EnableLight(0, !flags.HasFlag(NJD_FLAG.IgnoreLight));

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
}

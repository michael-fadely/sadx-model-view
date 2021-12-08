using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using sadx_model_view.Interfaces;
using SharpDX;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace sadx_model_view.Ninja
{
	public class NJS_MODEL : IDisposable, IInvalidatable
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
			byte[] buffer = new byte[SizeInBytes];
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

			long position = stream.Position;

			if (nbPoint > 0)
			{
				uint pointsOffset = BitConverter.ToUInt32(buffer, 0x00);

				if (pointsOffset > 0)
				{
					stream.Position = pointsOffset;

					for (int i = 0; i < nbPoint; i++)
					{
						Vector3 v = Util.VectorFromStream(stream);
						points.Add(v);
					}
				}

				int normalsOffset = BitConverter.ToInt32(buffer, 0x04);

				if (normalsOffset > 0)
				{
					stream.Position = normalsOffset;

					for (int i = 0; i < nbPoint; i++)
					{
						Vector3 v = Util.VectorFromStream(stream);
						normals.Add(v);
					}
				}
			}

			uint meshsetsOffset = BitConverter.ToUInt32(buffer, 0x0C);
			if (nbMeshset > 0 && meshsetsOffset > 0)
			{
				stream.Position = meshsetsOffset;
				for (int i = 0; i < nbMeshset; i++)
				{
					meshsets.Add(new NJS_MESHSET(stream));
				}
			}

			uint matsOffset = BitConverter.ToUInt32(buffer, 0x10);
			if (nbMat > 0 && matsOffset > 0)
			{
				stream.Position = matsOffset;
				for (int i = 0; i < nbMat; i++)
				{
					mats.Add(new NJS_MATERIAL(stream));
				}
			}

			stream.Position = position;

			IsInvalid = true;
		}

		/// <summary>
		/// Copy constructor.
		/// </summary>
		/// <param name="model">The model to copy from.</param>
		public NJS_MODEL(NJS_MODEL model)
		{
			points  = new List<Vector3>(model.points);
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

			IsInvalid = true;
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

			IsInvalid = true;
		}

		public void Dispose()
		{
			foreach (NJS_MESHSET set in meshsets)
			{
				set.Dispose();
			}

			meshsets.Clear();
			VertexBuffer?.Dispose();
		}

		public readonly List<Vector3>      points;    // vertex list
		public readonly List<Vector3>      normals;   // vertex normal list
		public readonly int                nbPoint;   // vertex count
		public          List<NJS_MESHSET>  meshsets;  // meshset list
		public readonly List<NJS_MATERIAL> mats;      // material list
		public          ushort             nbMeshset; // meshset count
		public readonly ushort             nbMat;     // material count
		public          Vector3            center;    // model center
		public          float              r;         // radius

		public Buffer VertexBuffer;

		public void CommitVertexBuffer(Renderer device)
		{
			if (normals.Count != 0 && points.Count != normals.Count)
			{
				throw new Exception("Vertex count deviates from normal count.");
			}

			List<Vertex> vertices = points.Select((point, i) => new Vertex
			{
				Position = point,
				Normal   = normals.Count > 0 ? normals[i] : Vector3.Up,
				Diffuse  = null,
				UV       = null
			}).ToList();

			foreach (NJS_MESHSET set in meshsets)
			{
				set.CommitIndexBuffer(device, vertices);
			}

			VertexBuffer?.Dispose();
			VertexBuffer = device.CreateVertexBuffer(vertices);
		}

		public IEnumerable<Vector3> GetPoints(IEnumerable<short> indices)
		{
			foreach (short i in indices)
			{
				if (i < points.Count)
				{
					yield return points[i];
				}
			}
		}

		public static SceneMaterial GetSADXMaterial(Renderer device, NJS_MATERIAL material)
		{
			NJD_FLAG flags = device.FlowControl.Apply(material.attrflags);

			if ((flags & NJD_FLAG.UseTexture) == 0)
			{
				device.SetTexture(0, -1);
			}
			else
			{
				int n = (int)material.TextureIndex;
				device.SetTexture(0, n);
			}

			NJS_BGRA diffuse  = material.diffuse.argb;
			NJS_BGRA specular = material.specular.argb;

			var m = new SceneMaterial
			{
				Diffuse     = new Color4(diffuse.r / 255.0f,  diffuse.g / 255.0f,  diffuse.b / 255.0f,  diffuse.a / 255.0f),
				Specular    = new Color4(specular.r / 255.0f, specular.g / 255.0f, specular.b / 255.0f, specular.a / 255.0f),
				Exponent    = material.exponent,
				UseAlpha    = (flags & NJD_FLAG.UseAlpha) != 0,
				UseEnv      = (flags & NJD_FLAG.UseEnv) != 0,
				UseTexture  = (flags & NJD_FLAG.UseTexture) != 0,
				UseSpecular = (flags & NJD_FLAG.IgnoreSpecular) == 0,
				UseLight    = (flags & NJD_FLAG.IgnoreLight) == 0
			};

			return m;
		}

		bool isInvalid;

		public bool IsInvalid
		{
			get => isInvalid;
			set
			{
				foreach (NJS_MESHSET set in meshsets)
				{
					set.IsInvalid = value;
				}

				isInvalid = value;
			}
		}

		public void GetTriangles(List<Triangle> list)
		{
			Matrix m = MatrixStack.Peek();

			foreach (NJS_MESHSET set in meshsets)
			{
				foreach (Triangle triangle in set.Triangles)
				{
					Triangle transformedTriangle = triangle;
					Vector3.Transform(ref transformedTriangle.A, ref m, out transformedTriangle.A);
					Vector3.Transform(ref transformedTriangle.B, ref m, out transformedTriangle.B);
					Vector3.Transform(ref transformedTriangle.C, ref m, out transformedTriangle.C);
					list.Add(transformedTriangle);
				}
			}
		}
	}
}
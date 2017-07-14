﻿using System;
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
				set.CommitIndexBuffer(device, vertices);
			}

			VertexBuffer = device.CreateVertexBuffer(vertices);
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
			NJD_FLAG flags = device.FlowControl.Apply(material.attrflags);

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

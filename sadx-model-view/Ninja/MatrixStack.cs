﻿using System;
using System.Collections.Generic;
using SharpDX;

namespace sadx_model_view.Ninja
{
	public static class MatrixStack
	{
		static readonly Stack<Matrix> stack = new Stack<Matrix>();
		public static bool Empty => stack.Count == 0;

		const int M00 = 0x0;
		const int M01 = 0x1;
		const int M02 = 0x2;
		const int M03 = 0x3;
		const int M10 = 0x4;
		const int M11 = 0x5;
		const int M12 = 0x6;
		const int M13 = 0x7;
		const int M20 = 0x8;
		const int M21 = 0x9;
		const int M22 = 0xA;
		const int M23 = 0xB;
		const int M30 = 0xC;
		const int M31 = 0xD;
		const int M32 = 0xE;
		const int M33 = 0xF;


		public static void Push(in Matrix m)
		{
			stack.Push(m);
		}

		public static void Push()
		{
			if (Empty)
			{
				PushIdentity();
			}
			else
			{
				Matrix m = stack.Peek();
				Push(in m);
			}
		}

		public static void PushIdentity()
		{
			Matrix m = Matrix.Identity;
			Push(in m);
		}

		public static void Pop(int n)
		{
			if (stack.Count < 1)
			{
				return;
			}

			if (n < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(n), "Value must be greater than zero");
			}

			do
			{
				stack.Pop();
			} while (--n > 0 && stack.Count > 0);
		}

		public static Matrix Pop()
		{
			return stack.Pop();
		}

		public static void Clear()
		{
			stack.Clear();
		}

		public static Matrix Peek()
		{
			return stack.Peek();
		}

		public static void Translate(in Vector3 v)
		{
			Matrix m = Pop();

			float x = v.X;
			float y = v.Y;
			float z = v.Z;

			m[M30] = z * m[M20] + y * m[M10] + x * m[M00] + m[M30];
			m[M31] = z * m[M21] + y * m[M11] + x * m[M01] + m[M31];
			m[M32] = z * m[M22] + y * m[M12] + x * m[M02] + m[M32];
			m[M33] = z * m[M23] + y * m[M13] + x * m[M03] + m[M33];

			Push(in m);
		}

		public static void Rotate(in Vector3 v, bool useZXY = false)
		{
			Matrix m = Pop();

			if (useZXY)
			{
				if (v.Y != 0)
				{
					var   sin = (float)Math.Sin(v.Y);
					var   cos = (float)Math.Cos(v.Y);
					float m00 = m[M00];
					float m01 = m[M01];
					float m02 = m[M02];
					float m03 = m[M03];

					m[M00] = m00 * cos - sin * m[M20];
					m[M01] = m01 * cos - sin * m[M21];
					m[M02] = m02 * cos - sin * m[M22];
					m[M03] = m03 * cos - sin * m[M23];
					m[M20] = cos * m[M20] + m00 * sin;
					m[M21] = cos * m[M21] + m01 * sin;
					m[M22] = cos * m[M22] + m02 * sin;
					m[M23] = cos * m[M23] + m03 * sin;
				}

				if (v.X != 0)
				{
					var   sin = (float)Math.Sin(v.X);
					var   cos = (float)Math.Cos(v.X);
					float m10 = m[M10];
					float m11 = m[M11];
					float m12 = m[M12];
					float m13 = m[M13];

					m[M10] = sin * m[M20] + m10 * cos;
					m[M11] = sin * m[M21] + m11 * cos;
					m[M12] = sin * m[M22] + m12 * cos;
					m[M13] = sin * m[M23] + m13 * cos;
					m[M20] = cos * m[M20] - m10 * sin;
					m[M21] = cos * m[M21] - m11 * sin;
					m[M22] = cos * m[M22] - m12 * sin;
					m[M23] = cos * m[M23] - m13 * sin;
				}

				if (v.Z != 0)
				{
					var   sin = (float)Math.Sin(v.Z);
					var   cos = (float)Math.Cos(v.Z);
					float m00 = m[M00];
					float m01 = m[M01];
					float m02 = m[M02];
					float m03 = m[M03];

					m[M00] = sin * m[M10] + m00 * cos;
					m[M01] = sin * m[M11] + m01 * cos;
					m[M02] = sin * m[M12] + m02 * cos;
					m[M03] = m03 * cos + sin * m[M13];
					m[M10] = cos * m[M10] - m00 * sin;
					m[M11] = cos * m[M11] - m01 * sin;
					m[M12] = cos * m[M12] - m02 * sin;
					m[M13] = cos * m[M13] - m03 * sin;
				}
			}
			else
			{
				if (v.Z != 0)
				{
					var   sin = (float)Math.Sin(v.Z);
					var   cos = (float)Math.Cos(v.Z);
					float m00 = m[M00];
					float m01 = m[M01];
					float m02 = m[M02];
					float m03 = m[M03];

					m[M00] = sin * m[M10] + m00 * cos;
					m[M01] = sin * m[M11] + m01 * cos;
					m[M02] = sin * m[M12] + m02 * cos;
					m[M03] = m03 * cos + sin * m[M13];
					m[M10] = cos * m[M10] - m00 * sin;
					m[M11] = cos * m[M11] - m01 * sin;
					m[M12] = cos * m[M12] - m02 * sin;
					m[M13] = cos * m[M13] - m03 * sin;
				}

				if (v.Y != 0)
				{
					var   sin = (float)Math.Sin(v.Y);
					var   cos = (float)Math.Cos(v.Y);
					float m00 = m[M00];
					float m01 = m[M01];
					float m02 = m[M02];
					float m03 = m[M03];

					m[M00] = m00 * cos - sin * m[M20];
					m[M01] = m01 * cos - sin * m[M21];
					m[M02] = m02 * cos - sin * m[M22];
					m[M03] = m03 * cos - sin * m[M23];
					m[M20] = cos * m[M20] + m00 * sin;
					m[M21] = cos * m[M21] + m01 * sin;
					m[M22] = cos * m[M22] + m02 * sin;
					m[M23] = cos * m[M23] + m03 * sin;
				}

				if (v.X != 0)
				{
					var   sin = (float)Math.Sin(v.X);
					var   cos = (float)Math.Cos(v.X);
					float m10 = m[M10];
					float m11 = m[M11];
					float m12 = m[M12];
					float m13 = m[M13];

					m[M10] = sin * m[M20] + m10 * cos;
					m[M11] = sin * m[M21] + m11 * cos;
					m[M12] = sin * m[M22] + m12 * cos;
					m[M13] = sin * m[M23] + m13 * cos;
					m[M20] = cos * m[M20] - m10 * sin;
					m[M21] = cos * m[M21] - m11 * sin;
					m[M22] = cos * m[M22] - m12 * sin;
					m[M23] = cos * m[M23] - m13 * sin;
				}
			}

			Push(in m);
		}

		public static void Rotate(in Rotation3 r, bool useZXY = false)
		{
			if (r.X == 0 && r.Y == 0 && r.Z == 0)
			{
				return;
			}

			Vector3 v = Util.AngleToRadian(in r);
			Rotate(in v, useZXY);
		}

		public static void Scale(in Vector3 v)
		{
			Matrix m = Pop();

			m[M00] = v.X * m[M00];
			m[M01] = v.X * m[M01];
			m[M02] = v.X * m[M02];
			m[M03] = v.X * m[M03];
			m[M10] = v.Y * m[M10];
			m[M11] = v.Y * m[M11];
			m[M12] = v.Y * m[M12];
			m[M13] = v.Y * m[M13];
			m[M20] = v.Z * m[M20];
			m[M21] = v.Z * m[M21];
			m[M22] = v.Z * m[M22];
			m[M23] = v.Z * m[M23];

			Push(in m);
		}

		public static void CalcPoint(in Vector3 vs, out Vector3 vd)
		{
			Matrix m = Peek();
			float  x = vs.X;
			float  y = vs.Y;
			float  z = vs.Z;

			vd.X = z * m[M20] + y * m[M10] + x * m[M00] + m[M30];
			vd.Y = z * m[M21] + y * m[M11] + x * m[M01] + m[M31];
			vd.Z = z * m[M22] + y * m[M12] + x * m[M02] + m[M32];
		}

		public static void CalcVector(in Vector3 vs, out Vector3 vd)
		{
			Matrix m = Peek();
			float  x = vs.X;
			float  y = vs.Y;
			float  z = vs.Z;

			vd.X = z * m[M20] + y * m[M10] + x * m[M00];
			vd.Y = z * m[M21] + y * m[M11] + x * m[M01];
			vd.Z = z * m[M22] + y * m[M12] + x * m[M02];
		}

		public static void Multiply(in Matrix m)
		{
			Matrix top = Pop() * m;
			Push(in top);
		}
	}
}
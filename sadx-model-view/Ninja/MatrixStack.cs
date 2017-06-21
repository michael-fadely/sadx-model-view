﻿using System;
using System.Collections.Generic;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;

namespace sadx_model_view.Ninja
{
	public static class MatrixStack
	{
		private static readonly Stack<Matrix> stack = new Stack<Matrix>();
		public static bool Empty => stack.Count == 0;

		private const int M00 = 0x0;
		private const int M01 = 0x1;
		private const int M02 = 0x2;
		private const int M03 = 0x3;
		private const int M10 = 0x4;
		private const int M11 = 0x5;
		private const int M12 = 0x6;
		private const int M13 = 0x7;
		private const int M20 = 0x8;
		private const int M21 = 0x9;
		private const int M22 = 0xA;
		private const int M23 = 0xB;
		private const int M30 = 0xC;
		private const int M31 = 0xD;
		private const int M32 = 0xE;
		private const int M33 = 0xF;


		public static void Push(ref Matrix m)
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
				var m = stack.Peek();
				Push(ref m);
			}
		}

		public static void PushIdentity()
		{
			var m = Matrix.Identity;
			Push(ref m);
		}

		public static void Pop(int n)
		{
			if (stack.Count < 1)
				return;

			if (n < 1)
				throw new ArgumentOutOfRangeException(nameof(n), "Value must be greater than zero");

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

		public static void Translate(ref Vector3 v)
		{
			var m = Pop();

			var x = v.X;
			var y = v.Y;
			var z = v.Z;

			m[M30] = z * m[M20] + y * m[M10] + x * m[M00] + m[M30];
			m[M31] = z * m[M21] + y * m[M11] + x * m[M01] + m[M31];
			m[M32] = z * m[M22] + y * m[M12] + x * m[M02] + m[M32];
			m[M33] = z * m[M23] + y * m[M13] + x * m[M03] + m[M33];

			Push(ref m);
		}

		public static void Rotate(ref Vector3 v, bool useZXY = false)
		{
			var m = Pop();

			if (useZXY)
			{
				if (v.Y != 0)
				{
					var sin = (float)Math.Sin(v.Y);
					var cos = (float)Math.Cos(v.Y);
					var m00 = m[M00];
					var m01 = m[M01];
					var m02 = m[M02];
					var m03 = m[M03];

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
					var sin = (float)Math.Sin(v.X);
					var cos = (float)Math.Cos(v.X);
					var m10 = m[M10];
					var m11 = m[M11];
					var m12 = m[M12];
					var m13 = m[M13];

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
					var sin = (float)Math.Sin(v.Z);
					var cos = (float)Math.Cos(v.Z);
					var m00 = m[M00];
					var m01 = m[M01];
					var m02 = m[M02];
					var m03 = m[M03];

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
					var sin = (float)Math.Sin(v.Z);
					var cos = (float)Math.Cos(v.Z);
					var m00 = m[M00];
					var m01 = m[M01];
					var m02 = m[M02];
					var m03 = m[M03];

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
					var sin = (float)Math.Sin(v.Y);
					var cos = (float)Math.Cos(v.Y);
					var m00 = m[M00];
					var m01 = m[M01];
					var m02 = m[M02];
					var m03 = m[M03];

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
					var sin = (float)Math.Sin(v.X);
					var cos = (float)Math.Cos(v.X);
					var m10 = m[M10];
					var m11 = m[M11];
					var m12 = m[M12];
					var m13 = m[M13];

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

			Push(ref m);
		}

		public static void Rotate(ref Rotation3 r, bool useZXY = false)
		{
			if (r.X == 0 && r.Y == 0 && r.Z == 0)
				return;

			var v = Util.AngleToRadian(ref r);
			Rotate(ref v, useZXY);
		}

		public static void Scale(ref Vector3 v)
		{
			var m = Pop();

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

			Push(ref m);
		}

		public static void SetTransform(Device device, TransformState state = TransformState.World)
		{
			RawMatrix m = Peek();
			device.SetTransform(state, ref m);
		}

		public static void CalcPoint(ref Vector3 vs, out Vector3 vd)
		{
			var m = Peek();
			var x = vs.X;
			var y = vs.Y;
			var z = vs.Z;

			vd.X = z * m[M20] + y * m[M10] + x * m[M00] + m[M30];
			vd.Y = z * m[M21] + y * m[M11] + x * m[M01] + m[M31];
			vd.Z = z * m[M22] + y * m[M12] + x * m[M02] + m[M32];
		}

		public static void CalcVector(ref Vector3 vs, out Vector3 vd)
		{
			var m = Peek();
			var x = vs.X;
			var y = vs.Y;
			var z = vs.Z;

			vd.X = z * m[M20] + y * m[M10] + x * m[M00];
			vd.Y = z * m[M21] + y * m[M11] + x * m[M01];
			vd.Z = z * m[M22] + y * m[M12] + x * m[M02];
		}

		public static void Multiply(ref Matrix m)
		{
			Matrix top = Pop() * m;
			Push(ref top);
		}
	}
}

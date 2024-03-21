﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GRF.Image;
using GRFEditor.OpenGL.MapComponents;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Utilities;

namespace GRFEditor.OpenGL {
	public static class GLHelper {
		public static Dictionary<string, int> IndexedTextures = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		public static Dictionary<string, GrfImage> IndexedImages = new Dictionary<string, GrfImage>(StringComparer.OrdinalIgnoreCase);

		public static int LoadTexture(string file, string uniqueIdentifier) {
			try {
				if (IndexedTextures.ContainsKey(uniqueIdentifier)) {
					return IndexedTextures[uniqueIdentifier];
				}

				return LoadTexture(new GrfImage(file), uniqueIdentifier);
			}
			catch {
				return -1;
			}
		}

		public static int LoadTexture(GrfImage image, string uniqueIdentifier) {
			try {
				if (IndexedTextures.ContainsKey(uniqueIdentifier)) {
					return IndexedTextures[uniqueIdentifier];
				}

				IndexedImages[uniqueIdentifier] = image.Copy();
				image.Convert(GrfImageType.Bgra32);

				for (int i = 0; i < image.Pixels.Length; i += 4) {
					if (image.Pixels[i + 0] < 10 &&
						image.Pixels[i + 1] < 10 &&
						image.Pixels[i + 2] < 10) {
						image.Pixels[i + 0] = 0;
						image.Pixels[i + 1] = 0;
						image.Pixels[i + 2] = 0;
						image.Pixels[i + 3] = 0;
					}
					else if (image.Pixels[i + 0] >= 252 &&
							 image.Pixels[i + 1] < 10 &&
							 image.Pixels[i + 2] >= 252) {
						image.Pixels[i + 0] = 0;
						image.Pixels[i + 1] = 0;
						image.Pixels[i + 2] = 0;
						image.Pixels[i + 3] = 0;
					}
				}

				int tex;
				GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);

				GL.GenTextures(1, out tex);
				GL.BindTexture(TextureTarget.Texture2D, tex);

				GCHandle pinnedArray = GCHandle.Alloc(image.Pixels, GCHandleType.Pinned);
				IntPtr pointer = pinnedArray.AddrOfPinnedObject();

				GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0,
					PixelFormat.Bgra, PixelType.UnsignedByte, pointer);

				pinnedArray.Free();

				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

				//GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
				//GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
				GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

				IndexedTextures[uniqueIdentifier] = tex;
				return tex;
			}
			catch {
				return -1;
			}
		}

		public static BlendingFactor GetOpenGlBlendFromDirectXSrc(int dxBlend) {
			switch (dxBlend) {
				case 0:	// ??
					return BlendingFactor.Zero;
				case 1:	// D3DBLEND_ZERO
					return BlendingFactor.Zero;
				case 2:	// D3DBLEND_ONE
					return BlendingFactor.One;
				case 3:	// D3DBLEND_SRCCOLOR
					return BlendingFactor.SrcColor;
				case 4:	// D3DBLEND_INVSRCCOLOR
					return BlendingFactor.OneMinusSrcColor;
				case 5:	// D3DBLEND_SRCALPHA
					return BlendingFactor.SrcAlpha;
				case 6:	// D3DBLEND_INVSRCALPHA
					return BlendingFactor.OneMinusSrcAlpha;
				case 7:	// D3DBLEND_DESTALPHA
					return BlendingFactor.DstAlpha;
				case 8:	// D3DBLEND_INVDESTALPHA
					return BlendingFactor.OneMinusDstAlpha;
				case 9:	// D3DBLEND_DESTCOLOR
					return BlendingFactor.DstColor;
				case 10: // D3DBLEND_INVDESTCOLOR
					return BlendingFactor.OneMinusDstColor;
				case 11: // D3DBLEND_SRCALPHASAT
					return BlendingFactor.SrcAlphaSaturate;
				case 12: // D3DBLEND_BOTHSRCALPHA
					return BlendingFactor.Src1Alpha;
				case 13: // D3DBLEND_BOTHINVSRCALPHA
					return (BlendingFactor)35067;
			}

			return BlendingFactor.SrcAlpha;
		}

		public static BlendingFactor GetOpenGlBlendFromDirectXDest(int dxBlend) {
			switch (dxBlend) {
				case 0:	// ??
					return BlendingFactor.Zero;
				case 1:	// D3DBLEND_ZERO
					return BlendingFactor.Zero;
				case 2:	// D3DBLEND_ONE
					return BlendingFactor.One;
				case 3:	// D3DBLEND_SRCCOLOR
					return BlendingFactor.SrcColor;
				case 4:	// D3DBLEND_INVSRCCOLOR
					return BlendingFactor.OneMinusSrcColor;
				case 5:	// D3DBLEND_SRCALPHA
					return BlendingFactor.SrcAlpha;
				case 6:	// D3DBLEND_INVSRCALPHA
					return BlendingFactor.OneMinusSrcAlpha;
				case 7:	// D3DBLEND_DESTALPHA
					return BlendingFactor.One;
				//return BlendingFactor.DstAlpha;
				case 8:	// D3DBLEND_INVDESTALPHA
					return BlendingFactor.OneMinusDstAlpha;
				case 9:	// D3DBLEND_DESTCOLOR
					return BlendingFactor.DstColor;
				case 10: // D3DBLEND_INVDESTCOLOR
					return BlendingFactor.OneMinusDstColor;
				case 11: // D3DBLEND_SRCALPHASAT
					return BlendingFactor.SrcAlphaSaturate;
				case 12: // D3DBLEND_BOTHSRCALPHA
					return BlendingFactor.Src1Alpha;
				case 13: // D3DBLEND_BOTHINVSRCALPHA
					return (BlendingFactor)35067;
			}

			return BlendingFactor.SrcAlpha;
		}

		public static double ToDegree(double angle) {
			return angle * (180f / Math.PI);
		}

		public static float ToDegree(float angle) {
			return (float)(angle * (180f / Math.PI));
		}

		public static double ToRad(double angle) {
			return angle * (Math.PI / 180f);
		}

		public static float ToRad(float angle) {
			return (float)(angle * (Math.PI / 180f));
		}

		public static Matrix4 Translate(Matrix4 matrix, Vector3 pos) {
			matrix.Row3 = matrix.Row0 * pos[0] + matrix.Row1 * pos[1] + matrix.Row2 * pos[2] + matrix.Row3;
			return matrix;
		}

		public static Matrix4 Scale(Matrix4 matrix, Vector3 v) {
			matrix.Row0 = matrix.Row0 * v[0];
			matrix.Row1 = matrix.Row1 * v[1];
			matrix.Row2 = matrix.Row2 * v[2];
			return matrix;
		}

		public static Matrix3 Rotate(Matrix3 m, double angle, Vector3 v) {
			float a = (float)angle;
			float c = (float)Math.Cos(a);
			float s = (float)Math.Sin(a);

			Vector3 axis = Vector3.Normalize(v);
			Vector3 temp = (1 - c) * axis;

			Matrix4 Rotate = Matrix4.Identity;
			Rotate[0, 0] = c + temp[0] * axis[0];
			Rotate[0, 1] = temp[0] * axis[1] + s * axis[2];
			Rotate[0, 2] = temp[0] * axis[2] - s * axis[1];

			Rotate[1, 0] = temp[1] * axis[0] - s * axis[2];
			Rotate[1, 1] = c + temp[1] * axis[1];
			Rotate[1, 2] = temp[1] * axis[2] + s * axis[0];

			Rotate[2, 0] = temp[2] * axis[0] + s * axis[1];
			Rotate[2, 1] = temp[2] * axis[1] - s * axis[0];
			Rotate[2, 2] = c + temp[2] * axis[2];

			Matrix3 Result = Matrix3.Identity;
			Result.Row0 = m.Row0 * Rotate[0, 0] + m.Row1 * Rotate[0, 1] + m.Row2 * Rotate[0, 2];
			Result.Row1 = m.Row0 * Rotate[1, 0] + m.Row1 * Rotate[1, 1] + m.Row2 * Rotate[1, 2];
			Result.Row2 = m.Row0 * Rotate[2, 0] + m.Row1 * Rotate[2, 1] + m.Row2 * Rotate[2, 2];
			return Result;
		}

		public static Matrix4 Rotate(Matrix4 m, double angle, Vector3 v) {
			float a = (float)angle;
			float c = (float)Math.Cos(a);
			float s = (float)Math.Sin(a);

			Vector3 axis = Vector3.Normalize(v);
			Vector3 temp = (1 - c) * axis;

			Matrix4 Rotate = Matrix4.Identity;
			Rotate[0, 0] = c + temp[0] * axis[0];
			Rotate[0, 1] = temp[0] * axis[1] + s * axis[2];
			Rotate[0, 2] = temp[0] * axis[2] - s * axis[1];

			Rotate[1, 0] = temp[1] * axis[0] - s * axis[2];
			Rotate[1, 1] = c + temp[1] * axis[1];
			Rotate[1, 2] = temp[1] * axis[2] + s * axis[0];

			Rotate[2, 0] = temp[2] * axis[0] + s * axis[1];
			Rotate[2, 1] = temp[2] * axis[1] - s * axis[0];
			Rotate[2, 2] = c + temp[2] * axis[2];

			Matrix4 Result = Matrix4.Identity;
			Result.Row0 = m.Row0 * Rotate[0, 0] + m.Row1 * Rotate[0, 1] + m.Row2 * Rotate[0, 2];
			Result.Row1 = m.Row0 * Rotate[1, 0] + m.Row1 * Rotate[1, 1] + m.Row2 * Rotate[1, 2];
			Result.Row2 = m.Row0 * Rotate[2, 0] + m.Row1 * Rotate[2, 1] + m.Row2 * Rotate[2, 2];
			Result.Row3 = m.Row3;
			return Result;
		}

		public static double Clamp(float min, float max, double value) {
			if (value < min)
				return min;

			if (value > max)
				return max;

			return value;
		}

		public static float Fract(float f) {
			return f - (int)f;
		}

		public static Vector3 MultiplyWithTranslate(Matrix4 m, Vector3 v) {
			return new Vector3(
				m[0, 0] * v.X + m[1, 0] * v.Y + m[2, 0] * v.Z + m[3, 0],
				m[0, 1] * v.X + m[1, 1] * v.Y + m[2, 1] * v.Z + m[3, 1],
				m[0, 2] * v.X + m[1, 2] * v.Y + m[2, 2] * v.Z + m[3, 2]);
		}

		public static Vector3 MultiplyWithoutTranslate(Matrix4 m, Vector3 v) {
			return new Vector3(
				m[0, 0] * v.X + m[1, 0] * v.Y + m[2, 0] * v.Z,
				m[0, 1] * v.X + m[1, 1] * v.Y + m[2, 1] * v.Z,
				m[0, 2] * v.X + m[1, 2] * v.Y + m[2, 2] * v.Z);
		}
	}
}

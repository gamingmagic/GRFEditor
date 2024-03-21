﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using ErrorManager;
using GRF.ContainerFormat;
using GRF.IO;
using GRF.System;
using Utilities;

namespace GRF.Core.GrfCompression {
	/// <summary>
	/// Curiosity's compression!
	/// </summary>
	public class LzmaCompression : CustomCompression {
		/// <summary>
		/// Initializes a new instance of the <see cref="LzmaCompression" /> class.
		/// </summary>
		public LzmaCompression() {
			_init();
		}

		protected void _init() {
			string dllName = IntPtr.Size == 4 ? "lzma.dll" : "comp_x64.dll";
			string outputPath = Path.Combine(GrfPath.GetDirectoryName(Settings.TempPath), dllName);
			Assembly currentAssembly = Assembly.GetAssembly(typeof (Compression));
			string[] names = currentAssembly.GetManifestResourceNames();
			string ResourceName = "Files." + dllName;
			byte[] cps = null;

			if (names.Any(p => p.EndsWith(ResourceName))) {
				Stream file = currentAssembly.GetManifestResourceStream(names.First(p => p.EndsWith(ResourceName)));
				if (file != null) {
					cps = new byte[file.Length];
					file.Read(cps, 0, (int) file.Length);
				}
			}

			if (cps == null)
				throw GrfExceptions.__CompressionDllFailed.Create(ResourceName);

			try {
				File.WriteAllBytes(outputPath, cps);
			}
			catch {
			}

			_hModule = NativeMethods.LoadLibrary(outputPath);

			if (_hModule == IntPtr.Zero) {
				if (IntPtr.Size == 4) {
					ErrorHandler.HandleException(GrfExceptions.__CompressionDllFailed2.Display(ResourceName, "Microsoft Visual Studio C++ 2005 (x86) | downloading the x64 version will not be compatible"));
				}
				else {
					ErrorHandler.HandleException(GrfExceptions.__CompressionDllFailed2.Display(ResourceName, "Microsoft Visual Studio C++ 2022 (x64) | downloading the x86 version will not be compatible\r\n\r\nLink: https://aka.ms/vs/17/release/vc_redist.x64.exe"));
				}
				Success = false;
				return;
			}

			IntPtr intPtr = NativeMethods.GetProcAddress(_hModule, "uncompress");
			_decompress = (DecompressMethod)Marshal.GetDelegateForFunctionPointer(intPtr, typeof(DecompressMethod));

			_compressionLevel = 9;
			intPtr = NativeMethods.GetProcAddress(_hModule, "lzma_compress");

			if (intPtr == IntPtr.Zero)
				intPtr = NativeMethods.GetProcAddress(_hModule, "compress2");

			if (intPtr == IntPtr.Zero)
				intPtr = NativeMethods.GetProcAddress(_hModule, "compress");

			_compress = (CompressMethod)Marshal.GetDelegateForFunctionPointer(intPtr, typeof(CompressMethod));

			Success = true;
		}

		public override string ToString() {
			return GrfStrings.DisplayLzmaDll;
		}
	}
}
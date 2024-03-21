﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Encryption;
using GRF.ContainerFormat;
using GRF.IO;
using GRF.System;
using Utilities;
using Utilities.Extension;
using Utilities.Services;

namespace GRF.Core {
	public class FileTable : ContainerTable<FileEntry> {
		public const int StructSize = 8;

		protected GrfHeader _header;

		public FileTable(GrfHeader header) {
			_header = header;
			TableSize = 0;
			TableSizeCompressed = 0;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FileTable" /> class.
		/// Probably the most important method in the loading process of the GRF.
		/// It looks like a mess and the algorithm may be very confusing, but this is
		/// partially due to code optimization.
		/// </summary>
		/// <param name="header">The header.</param>
		/// <param name="reader">The GRF file stream.</param>
		public FileTable(GrfHeader header, ByteReaderStream reader) {
			try {
				_header = header;

				if (header.IsCompatibleWith(2, 0))
					_load200(reader);
				else if (header.IsCompatibleWith(1, 0))
					_load100(reader);
				else
					throw GrfExceptions.__UnsupportedFileFormat.Create(header.HexVersionFormat);
			}
			catch (Exception err) {
				header.SetError("File table instantiation has failed : \r\n" + err.Message);
			}
		}

		/// <summary>
		/// Gets the table size compressed.
		/// </summary>
		public int TableSizeCompressed { get; internal set; }

		/// <summary>
		/// Gets the size of the table.
		/// </summary>
		public int TableSize { get; internal set; }

		/// <summary>
		/// Loads the file table using the GRF version 0x100 format.
		/// </summary>
		/// <param name="grfStream">The GRF stream.</param>
		private void _load100(ByteReaderStream grfStream) {
			FileEntry fileEntry;

			int fileListSize = grfStream.Length - grfStream.Position;
			byte[] fileListData = grfStream.Bytes(fileListSize);

			EntryType entryType;

			int directoryIndexCount = 0;
			int filelistEntries = _header.RealFilesCount;
			int offset2;

			for (int entry = 0, offset = 0; entry < filelistEntries; entry++) {
				offset2 = offset + BitConverter.ToInt32(fileListData, offset) + 4;
				entryType = (EntryType) fileListData[offset2 + 12];

				if (entryType == EntryType.Directory)
					directoryIndexCount++;

				offset = offset2 + 17;
			}

			_header.RealFilesCount = filelistEntries - directoryIndexCount;

			int compressedLenAligned;
			int realLen;
			string name;
			byte[] fileName;
			int cycle;
			uint pos;
			int compressedLen;
			int length;
			int index;
			string ext;
			string tempName;
			int filesCount = 0;

			for (int entry = 0, offset = 0; entry < filelistEntries; entry++) {
				offset2 = offset + BitConverter.ToInt32(fileListData, offset) + 4;
				entryType = (EntryType) fileListData[offset2 + 12];

				fileEntry = new FileEntry();

				length = fileListData[offset] - 6;

				fileName = new byte[length];
				Buffer.BlockCopy(fileListData, offset + 6, fileName, 0, length);

				name = DesDecryption.DecodeFileName(fileName);

				// Check and fix the filename
				// If we have a ? char, then the encoding chosen was bad
				// We put it as an actual GRF error because it's an invalid character
				index = name.IndexOf("\0", 0, StringComparison.Ordinal);

				if (index < 0) {
					_header.SetError(GrfStrings.FailedNullString, name);
				}
				else {
					name = name.Substring(0, index);

					if (name.IndexOf("?", 0, StringComparison.Ordinal) > -1)
						_header.SetError(GrfStrings.FailedEncodingString, name);
				}

				if (entryType == EntryType.Directory) {
					if (name.LastIndexOf('.') == -1) {
						offset = offset2 + 17;
						continue;
					}

					entryType |= EntryType.RawDataFile;
				}

				compressedLenAligned = BitConverter.ToInt32(fileListData, offset2 + 4) - 37579;
				realLen = BitConverter.ToInt32(fileListData, offset2 + 8);
				pos = BitConverter.ToUInt32(fileListData, offset2 + 13);

				cycle = 0;
				compressedLen = 0;

				if (name.Length > 4) {
					ext = name.Substring(name.Length - 4).ToLower();
					compressedLen = BitConverter.ToInt32(fileListData, offset2) - BitConverter.ToInt32(fileListData, offset2 + 8) - 715;

					switch (ext) {
						case ".gnd":
						case ".gat":
						case ".act":
						case ".str":
							break;
						default:
							cycle = 1;
							for (int i = 10; compressedLen >= i; i *= 10)
								cycle++;
							break;
					}
				}

				// We have to set the fileEntry manually because of the encryption and many fields
				// are 'custom'
				tempName = EncodingService.DisplayEncoding.GetString(EncodingService.Ansi.GetBytes(name));
				fileEntry.Cycle = cycle;
				fileEntry.SizeCompressed = fileEntry.NewSizeCompressed = compressedLen;
				fileEntry.SizeCompressedAlignment = fileEntry.TemporarySizeCompressedAlignment = compressedLenAligned;
				fileEntry.SizeDecompressed = fileEntry.NewSizeDecompressed = realLen;
				fileEntry.FileExactOffset = fileEntry.TemporaryOffset = pos + GrfHeader.StructSize;
				fileEntry.Flags = entryType;
				fileEntry.Header = _header;
				fileEntry.SetStream(grfStream);

				if (tempName.IndexOf("\\\\", 0, StringComparison.Ordinal) > -1) {
					fileEntry.Modification = Modification.FileNameRenamed;
					fileEntry.RelativePath = tempName.Replace("\\\\", "\\");
				}
				else {
					fileEntry.RelativePath = tempName;
				}

				if (_indexedEntries.ContainsKey(fileEntry.RelativePath)) {
					FileEntry conflict = _indexedEntries[fileEntry.RelativePath];

					if ((conflict.Modification & Modification.FileNameRenamed) == Modification.FileNameRenamed) {
						_indexedEntries.Remove(conflict.RelativePath);
						_indexedEntries[fileEntry.RelativePath] = fileEntry;
					}
				}
				else {
					_indexedEntries[fileEntry.RelativePath] = fileEntry;
				}

				offset = offset2 + 17;
				filesCount++;
			}

			_header.RealFilesCount = filesCount;
			if (_indexedEntries.ContainsKey(GrfStrings.EncryptionFilename)) {
				_indexedEntries[GrfStrings.EncryptionFilename].Modification |= Modification.Removed;
			}
		}

		/// <summary>
		/// Loads the file table using the GRF version 0x200 format.
		/// </summary>
		/// <param name="grfStream">The GRF stream.</param>
		private void _load200(ByteReaderStream grfStream) {
			FileEntry fileEntry;

			TableSizeCompressed = grfStream.Int32();
			TableSize = grfStream.Int32();

			if (TableSizeCompressed == 0 || TableSize == 0)
				return;

			byte[] compressedData = grfStream.Bytes(TableSizeCompressed);
			byte[] data = Compression.Decompress(compressedData, TableSize);

			int bufferPosition = 0;
			int streamLength = data.Length;

			while (bufferPosition < streamLength) {
				fileEntry = new FileEntry(data, ref bufferPosition, _header, grfStream);

				if (ExtensionMethods.ContainsDoubleSlash(fileEntry.RelativePath)) {
					fileEntry.Modification |= Modification.FileNameRenamed;
					fileEntry.RelativePath = fileEntry.RelativePath.Replace("\\\\", "\\");
				}

				// Ignore any other type of entries (such as directories)
				if (fileEntry.Flags == EntryType.Directory) {
					if (fileEntry.RelativePath.LastIndexOf('.') != -1) {
						fileEntry.Flags |= EntryType.RawDataFile;
					}
				}

				if ((fileEntry.Flags & (EntryType.File | EntryType.RawDataFile)) > 0) {
					if (_indexedEntries.ContainsKey(fileEntry.RelativePath)) {
						FileEntry conflict = _indexedEntries[fileEntry.RelativePath];

						if ((conflict.Modification & Modification.FileNameRenamed) == Modification.FileNameRenamed) {
							_indexedEntries[fileEntry.RelativePath] = fileEntry;
						}
					}
					else {
						_indexedEntries.SetQuick(fileEntry.RelativePath, fileEntry);
					}
				}
			}

			if (_indexedEntries.ContainsKey(GrfStrings.EncryptionFilename)) {
				_indexedEntries[GrfStrings.EncryptionFilename].Modification |= Modification.Removed;
			}

			_indexedEntries.HasBeenModified = true;
		}

		/// <summary>
		/// Writes the metadata (the fileTable).
		/// </summary>
		/// <param name="header"> </param>
		/// <param name="grfStream">The GRF stream.</param>
		/// <returns>The new size of the file table (compressed)</returns>
		public int WriteMetadata(GrfHeader header, Stream grfStream) {
			if (header.IsCompatibleWith(2, 0)) {
				using (MemoryStream stream = new MemoryStream()) {
					foreach (FileEntry entry in Entries) {
						entry.WriteMetadata(header, stream);
					}

					stream.Seek(0, SeekOrigin.Begin);

					TableSize = (int) stream.Length;
					byte[] dataCompressed = Compression.CompressDotNet(stream);

					TableSizeCompressed = dataCompressed.Length;

					if (header.EncryptFileTable) {
						if (Ee322.ad0bbddf4b9c6de7b6f99a036deb2be2(dataCompressed)) {
							Ee322.ebfd0ac060c6a005e565726f05d6aac8(header.EncryptionKey, dataCompressed, TableSize + 8);

							if (Ee322.ad0bbddf4b9c6de7b6f99a036deb2be2(dataCompressed))
								Ee322.f8881b1c7355d58161c07ae1c35cfb13(header.EncryptionKey, dataCompressed, TableSize + 8);
						}
					}

					grfStream.Write(BitConverter.GetBytes(TableSizeCompressed), 0, 4);
					grfStream.Write(BitConverter.GetBytes(TableSize), 0, 4);
					grfStream.Write(dataCompressed, 0, dataCompressed.Length);
				}

				return TableSizeCompressed + 8; // 8 is for the 2 int (table size and table compressed size)
			}

			if (header.IsCompatibleWith(1, 0)) {
				using (MemoryStream stream = new MemoryStream()) {
					foreach (FileEntry entry in Entries.OrderBy(p => p.TemporaryOffset)) {
						entry.WriteMetadata(header, stream);
					}

					stream.Seek(0, SeekOrigin.Begin);

					TableSize = (int) stream.Length;
					TableSizeCompressed = TableSize;

					byte[] data = new byte[TableSize];
					stream.Read(data, 0, data.Length);
					grfStream.Write(data, 0, TableSize);
				}

				return TableSize;
			}

			throw GrfExceptions.__UnsupportedFileVersion.Create();
		}

		/// <summary>
		/// Writes the actual compressed data. Optimized.
		/// </summary>
		/// <param name="grf">The GRF.</param>
		/// <param name="originalStream">The original stream.</param>
		/// <param name="grfStream">The GRF stream.</param>
		/// <param name="grfAdd">The GRF add.</param>
		internal void WriteData(Container grf, Stream originalStream, Stream grfStream, Container grfAdd = null) {
			GrfWriter.WriteData(grf, originalStream, grfStream, grfAdd);
		}

		/// <summary>
		/// Repacks the content of the GRF.
		/// </summary>
		/// <param name="grf">The GRF.</param>
		/// <param name="originalStream">The original stream.</param>
		/// <param name="grfStream">The GRF stream.</param>
		internal void WriteDataRepack(Container grf, Stream originalStream, Stream grfStream) {
			GrfWriter.WriteDataRepack(grf, originalStream, grfStream);
		}

		/// <summary>
		/// Writes the content of the GRF and defragments its content. This is the original saving method.
		/// </summary>
		/// <param name="grf">The GRF.</param>
		/// <param name="originalStream">The original stream.</param>
		/// <param name="grfAdd">The GRF add.</param>
		/// <returns>The file table end offset.</returns>
		internal long WriteDataQuick(Container grf, Stream originalStream, Container grfAdd = null) {
			return GrfWriter.WriteDataQuick(grf, originalStream, grfAdd);
		}

		/// <summary>
		/// Looks for identical entries and redirect the indexes to the same content.
		/// </summary>
		/// <param name="grf">The GRF.</param>
		/// <param name="originalStream">The original stream.</param>
		/// <param name="grfStream">The GRF stream.</param>
		/// <returns>The file table end offset.</returns>
		internal long WriteDataCompact(Container grf, Stream originalStream, Stream grfStream) {
			return GrfWriter.WriteCompact(grf, originalStream, grfStream);
		}

		public virtual List<FileEntry> FindEntriesFromFileName(string fileName) {
			fileName = "\\" + fileName.Trim('\\');

			List<FileEntry> entries = new List<FileEntry>();

			foreach (var entry in Entries) {
				if (entry.RelativePath.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
					entries.Add(entry);
			}

			return entries;
		}

		internal override FileEntry Add(string grfPath, string sourceFileName, bool overwrite) {
			FileEntry entry = new FileEntry();

			entry.RelativePath = EncodingService.FromAnyToDisplayEncoding(grfPath);
			entry.Flags = EntryType.File;
			entry.FileExactOffset = 0;
			entry.TemporaryOffset = 0;
			entry.SourceFilePath = sourceFileName;
			entry.Header = _header;
			entry.Modification |= Modification.Added;

			_addLockedFile(sourceFileName);

			if (_indexedEntries.ContainsKey(entry.RelativePath)) {
				if (overwrite) {
					FileEntry conflictEntry = _indexedEntries[entry.RelativePath];
					_indexedEntries[entry.RelativePath] = entry;
					return conflictEntry;
				}
			}
			else {
				_indexedEntries[entry.RelativePath] = entry;
			}

			return null;
		}

		internal override FileEntry Replace(string grfPath, string filePath, string fileName) {
			FileEntry entry = new FileEntry();

			entry.RelativePath = Path.Combine(grfPath, EncodingService.CorrectFileName(fileName) ?? Path.GetFileName(EncodingService.CorrectFileName(filePath)));
			entry.Flags = EntryType.File;
			entry.FileExactOffset = 0;
			entry.TemporaryOffset = 0;
			entry.SourceFilePath = filePath;
			entry.Header = _header;
			entry.Modification |= Modification.Added;

			_addLockedFile(filePath);

			if (_indexedEntries.ContainsKey(entry.RelativePath)) {
				FileEntry conflictEntry = _indexedEntries[entry.RelativePath];
				_indexedEntries[entry.RelativePath] = entry;
				return conflictEntry;
			}

			_indexedEntries[entry.RelativePath] = entry;
			return null;
		}

		internal override FileEntry Replace(string grfPath, FileEntry dataEntry, string fileName) {
			FileEntry entry = new FileEntry();

			entry.RelativePath = Path.Combine(grfPath, EncodingService.CorrectFileName(fileName));
			entry.Flags = EntryType.File;
			entry.FileExactOffset = 0;
			entry.TemporaryOffset = 0;
			entry.SourceFilePath = GrfStrings.DataStreamId;
			entry.Header = _header;
			entry.Modification |= Modification.Added;
			entry.RawDataSource = dataEntry;

			if (_indexedEntries.ContainsKey(entry.RelativePath)) {
				var conflictEntry = _indexedEntries[entry.RelativePath];
				_indexedEntries[entry.RelativePath] = entry;
				return conflictEntry;
			}

			_indexedEntries[entry.RelativePath] = entry;
			return null;
		}

		internal override FileEntry Replace(string grfPath, GrfMemoryStreamHolder data, string fileName) {
			FileEntry entry = new FileEntry();

			entry.RelativePath = Path.Combine(grfPath, EncodingService.CorrectFileName(fileName));
			entry.Flags = EntryType.File;
			entry.FileExactOffset = 0;
			entry.TemporaryOffset = 0;
			entry.SourceFilePath = GrfStrings.DataStreamId;
			entry.Header = _header;
			entry.Modification |= Modification.Added;
			entry.RawDataSource = data;

			if (_indexedEntries.ContainsKey(entry.RelativePath)) {
				var conflictEntry = _indexedEntries[entry.RelativePath];
				_indexedEntries[entry.RelativePath] = entry;
				return conflictEntry;
			}

			_indexedEntries[entry.RelativePath] = entry;
			return null;
		}

		internal override FileEntry Replace(string grfPath, Stream data, string fileName) {
			FileEntry entry = new FileEntry();

			entry.RelativePath = Path.Combine(grfPath, EncodingService.CorrectFileName(fileName));
			entry.Flags = EntryType.File;
			entry.FileExactOffset = 0;
			entry.TemporaryOffset = 0;
			entry.SourceFilePath = GrfStrings.DataStreamId;
			entry.Header = _header;
			entry.Modification |= Modification.Added;
			entry.RawDataSource = data;

			if (_indexedEntries.ContainsKey(entry.RelativePath)) {
				var conflictEntry = _indexedEntries[entry.RelativePath];
				_indexedEntries[entry.RelativePath] = entry;
				return conflictEntry;
			}

			_indexedEntries[entry.RelativePath] = entry;
			return null;
		}

		internal override FileEntry AddFileToRemove(string grfPath) {
			FileEntry entry = new FileEntry();

			entry.RelativePath = EncodingService.FromAnyToDisplayEncoding(grfPath);
			entry.Flags = EntryType.File | EntryType.RemoveFile;
			entry.Header = _header;

			if (_indexedEntries.ContainsKey(entry.RelativePath)) {
				var conflictEntry = _indexedEntries[entry.RelativePath];
				_indexedEntries[entry.RelativePath] = entry;
				return conflictEntry;
			}

			_indexedEntries[entry.RelativePath] = entry;
			return null;
		}

		internal override void Delete() {
			base.Delete();

			TableSizeCompressed = -1;
			TableSize = -1;
		}

		public long GetWastedSpace() {
			long size = 0;

			List<FileEntry> entriesList = Entries.OrderBy(p => p.FileExactOffset).ToList();

			for (int i = 0; i < entriesList.Count - 1; i++) {
				size += entriesList[i + 1].FileExactOffset - entriesList[i].FileExactOffset - entriesList[i].SizeCompressedAlignment;
			}

			return size;
		}

		public void Clear() {
			_indexedEntries.Clear();
		}
	}
}
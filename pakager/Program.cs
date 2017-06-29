using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace pakager {
	unsafe class Program {
		static void SetTitle(string FileName) {
			Console.Title = "pakager: " + FileName;
		}

		static void Main(string[] args) {
			string[] PakFiles;

			if (args.Length != 1) {
				Console.WriteLine("Must supply .pak file or directory containing .pak files");
				return;
			}

			if (File.Exists(args[0]))
				PakFiles = new string[] { args[0] };
			else if (Directory.Exists(args[0]))
				PakFiles = Directory.EnumerateFiles(args[0], "*.pak", SearchOption.AllDirectories).ToArray();
			else {
				Console.WriteLine("Invalid .pak file or directory: `{0}´", args[0]);
				return;
			}

			if (PakFiles.Length == 0) {
				Console.WriteLine("No .pak files found");
				return;
			}

			foreach (var PakFile in PakFiles)
				Test(PakFile);

			Console.WriteLine("\nDone!");
			Console.ReadLine();
		}

		static void Write(byte B) {
			Console.WriteLine("0x{0:X2}", B);
		}

		static void WritePosition(Stream S) {
			Console.WriteLine("- 0x{0:X4}; {0}", (int)S.Position);
		}

		static void Test(string FilePath) {
			string Name = Path.GetFileName(FilePath);
			string NameNoExt = Path.GetFileNameWithoutExtension(FilePath);

			//if (Name != "main_menu.pak")
			//if (Name != "cache.pak")
			if (Name != "ruins_of_sarnath.pak")
				return;
			SetTitle(Name);

			Console.WriteLine(">> {0}", Name);


			FileStream FS = File.OpenRead(FilePath);
			BinaryReader BR = new BinaryReader(FS);
			DeflateStream Deflate = new DeflateStream(FS, CompressionMode.Decompress);


			while (BR.ReadByte() == 0)
				;
			FS.Position--;

			WritePosition(FS);
			//Console.WriteLine("- 0x{0:X4}\t{0}", FS.Length - FS.Position);
			//Console.WriteLine("- 0x{0:X4}\t{0}", FS.Position - FS.Length);

			using (MemoryStream Data = Uncompress(Deflate)) {
				if (Data.Length > 0) {
					File.WriteAllBytes(NameNoExt + "_out.bin", Data.ToArray());

					using (BinaryReader DataReader = new BinaryReader(Data)) {
						int Magic = DataReader.ReadInt32();

						if (Magic == BitConverter.ToInt32(Encoding.ASCII.GetBytes("1SER"), 0)) { // Cache file?
																								 //Console.WriteLine("Cache .pak");

							Console.WriteLine(DataReader.ReadStringNullTerm());
							Data.Seek(0x4E, SeekOrigin.Begin);

							while (true) {
								string Str = DataReader.ReadStringLengthPrefixed();
								if (Str.Length == 0)
									break;

								//Console.WriteLine(Str);
								//File.AppendAllText(NameNoExt + "_out.txt", Str + "\n");
							}
						} else if (Magic == 0x57e0e057) {
							DataReader.ReadBytes(76);

							for (int i = 0; i < 3; i++) {
								SceneEntry Entry = DataReader.ReadStruct<SceneEntry>();
								Console.WriteLine("{0}; {1}; {2}; {3}; {4}; {5}; {6}; {7}; {8}; {9}; {10}; {11}; {12}", Entry.Name, Entry.Unknown0, Entry.Unknown1,
									Entry.Unknown2, Entry.Unknown3, Entry.Unknown4, Entry.Unknown5, Entry.Unknown6, Entry.Unknown7, Entry.Unknown8, Entry.Unknown9,
									Entry.Unknown10, Entry.Unknown11);
							}

						} else
							Console.WriteLine("Unknown .pak type");
					}
				}
			}

			WritePosition(FS);


			/*FS.Seek(0, SeekOrigin.Begin);
			while (true)
				if (BR.ReadByte() == 0x50 && BR.ReadByte() == 0x4b && BR.ReadByte() == 0x05 && BR.ReadByte() == 0x06)
					break;
			DirectoryEnd DirEnd = BR.ReadStruct<DirectoryEnd>();
			WritePosition(FS);*/

			Deflate.Dispose();
			BR.Dispose();
			FS.Dispose();
		}

		static MemoryStream Uncompress(DeflateStream Stream) {
			MemoryStream MS = new MemoryStream();

			byte[] Data = new byte[4096];

			//try {
			while (true) {
				int Len = Stream.Read(Data, 0, Data.Length);

				if (Len > 0)
					MS.Write(Data, 0, Data.Length);
				else
					break;
			}
			/*} catch (Exception) {
			}*/

			MS.Seek(0, SeekOrigin.Begin);
			return MS;
		}

		static MemoryStream Uncompress(DeflateStream Stream, int Len) {
			MemoryStream MS = new MemoryStream();

			byte[] Data = new byte[Len];
			Len = Stream.Read(Data, 0, Data.Length);

			MS.Write(Data, 0, Len);
			MS.Seek(0, SeekOrigin.Begin);
			return MS;
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	unsafe struct DirectoryEnd {
		//Int32 Header;
		public Int32 A;
		public Int32 B;
		public Int32 C;
		public Int32 D;
		public Int32 E;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	unsafe struct SceneEntry {
		long Name1;
		long Name2;

		public uint Unknown0;
		public uint Unknown1;
		public uint Unknown2;
		public uint Unknown3;

		public uint Unknown4;
		public uint Unknown5;
		public uint Unknown6;
		public uint Unknown7;

		public uint Unknown8;
		public uint Unknown9;
		public uint Unknown10;
		public uint Unknown11;

		public string Name {
			get {
				byte[] NameArray = new byte[16];
				Array.Copy(BitConverter.GetBytes(Name1), NameArray, 8);
				Array.Copy(BitConverter.GetBytes(Name2), 0, NameArray, 8, 8);

				fixed (byte* NameArrayPtr = NameArray)
					return new string((sbyte*)NameArrayPtr, 0, 16, Encoding.ASCII);
			}
		}
	}

	unsafe static class Extensions {
		public static string ReadStringNullTerm(this BinaryReader BR) {
			StringBuilder SB = new StringBuilder();
			byte B;

			while ((B = BR.ReadByte()) != 0)
				SB.Append((char)B);

			return SB.ToString();
		}

		public static string ReadStringLengthPrefixed(this BinaryReader BR) {
			int Len = BR.ReadInt32();

			StringBuilder SB = new StringBuilder(Len);
			for (int i = 0; i < Len; i++)
				SB.Append(BR.ReadChar());

			return SB.ToString();
		}

		public static T ReadStruct<T>(this BinaryReader BR) where T : struct {
			byte[] Bytes = BR.ReadBytes(Marshal.SizeOf(typeof(T)));
			fixed (byte* BytesPtr = Bytes)
				return (T)Marshal.PtrToStructure(new IntPtr(BytesPtr), typeof(T));
		}
	}
}
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using TiltbrushToolkit.Exceptions;

namespace TiltbrushToolkit
{
    internal static class UnpackTilt
    {
        private static readonly string[] FILE_ORDER = new string[5]
        {
            "header.bin",
            "thumbnail.png",
            "metadata.json",
            "main.json",
            "data.sketch"
        };
        public static void ConvertZipToDirectory(string filePath)
        {
            byte[] zip = null;
            ushort headerSize = 16;
            using (FileStream fs = File.Open(filePath, FileMode.Open))
            using (BinaryReader br = new BinaryReader(fs))
            {
                var sentinel =  new string(br.ReadChars(4));
                var readHeaderSize = br.ReadUInt16();
                var version = br.ReadUInt16();
                var empty1 = br.ReadUInt32();
                var empty2 = br.ReadUInt32();
                //read and check header
                if(sentinel != "tilT")
                {
                    throw new BadHeaderException($"Sentinel looks weird: {sentinel}");
                }
                if(readHeaderSize > headerSize)
                {
                    throw new BadHeaderException($"Strange header size: {readHeaderSize}");
                }
                if(version != 1)
                {
                    throw new BadHeaderException($"Bogus version: {version}");
                }
               zip = br.ReadBytes((int)br.BaseStream.Length - 16);
            }

            string tempZipFilePath = filePath + ".zip";
            File.WriteAllBytes(tempZipFilePath, zip);

            string outName = filePath + ".part";
            if(Directory.Exists(outName))
            {
                throw new Exception($"Please remove {outName} before conversion");
            }
            Directory.CreateDirectory(outName);

            using (ZipArchive archive = ZipFile.OpenRead(tempZipFilePath))
            {
                foreach (var entry in archive.Entries)
                {
                    entry.ExtractToFile(outName + "/" + entry.Name);
                }
            }

            string tempPath = filePath + ".prev";
            File.Move(filePath, tempPath);
            Directory.Move(outName, filePath);
            File.Delete(tempPath);
            File.Delete(tempZipFilePath);
        }
        public static void ConvertDirectoryToZip(string directoryPath)
        {
            try
            {
                //Todo check for and remove trailing slash
                string destinationPath = directoryPath + ".part";
                if (File.Exists(destinationPath))
                {
                    throw new ArgumentException($"Please remove {destinationPath} before conversion");
                }
                string json = File.ReadAllText(directoryPath + "/metadata.json");
                Metadata metaData = Metadata.FromJson(json);
                byte[] headerBytes = null;
                using (ZipArchive archive = ZipFile.Open(destinationPath, ZipArchiveMode.Create))
                {
                    foreach (string file in FILE_ORDER)
                    {
                        string filePath = directoryPath + "/" + file;
                        if (file == "header.bin")
                        {
                            if (!File.Exists(filePath))
                            {
                                //no header so use the default
                                //header is missing, so use a default value
                                string headerStart = "tilT";
                                ushort version = 1;
                                uint empty1 = 0;
                                uint empty2 = 0;
                                ushort headerSize = 16;
                                byte[] headerStartBytes = Encoding.UTF8.GetBytes(headerStart);
                                byte[] versionByte = BitConverter.GetBytes(version);
                                byte[] empty1Byte = BitConverter.GetBytes(empty1);
                                byte[] empty2Byte = BitConverter.GetBytes(empty2);
                                byte[] headerSizeBytes = BitConverter.GetBytes(headerSize);
                                headerBytes = new byte[headerSize];
                                int i = 0;
                                for (int j = 0; j < headerStartBytes.Length; j++)
                                {
                                    headerBytes[i] = headerStartBytes[j];
                                    i++;
                                }
                                for (int j = 0; j < headerSizeBytes.Length; j++)
                                {
                                    headerBytes[i] = headerSizeBytes[j];
                                    i++;
                                }
                                for (int j = 0; j < versionByte.Length; j++)
                                {
                                    headerBytes[i] = versionByte[j];
                                    i++;
                                }
                                for (int j = 0; j < empty1Byte.Length; j++)
                                {
                                    headerBytes[i] = empty1Byte[j];
                                    i++;
                                }
                                for (int j = 0; j < empty2Byte.Length; j++)
                                {
                                    headerBytes[i] = empty2Byte[j];
                                    i++;
                                }
                            }
                            else
                            {
                                headerBytes = File.ReadAllBytes(filePath);
                            }
                        }
                        else
                        {
                            if (File.Exists(filePath))
                            {
                                archive.CreateEntryFromFile(filePath, file, CompressionLevel.NoCompression);
                            }
                        }
                    }
                }

                string tempFileName = directoryPath + ".prev";
                Directory.Move(directoryPath, tempFileName);

                using (FileStream fs = new FileStream(directoryPath, FileMode.OpenOrCreate))
                using (BinaryWriter bw = new BinaryWriter(fs, Encoding.UTF8))
                {
                    bw.Write(headerBytes);
                    bw.Write(File.ReadAllBytes(destinationPath));
                }
                Directory.Delete(tempFileName, true);
                File.Delete(destinationPath);
            }
            catch(Exception ex)
            {
                throw new FileConversionException($"Error encountered while converting to Zip: {ex.Message}");
            }
        }
    }
}

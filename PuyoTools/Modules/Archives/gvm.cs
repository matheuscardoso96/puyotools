﻿using System;
using System.IO;
using Extensions;

namespace PuyoTools
{
    public class GVM : ArchiveModule
    {
        /*
         * GVM files are archives that contain GVR files only.
        */

        // Main Method
        public GVM()
        {
            Name       = "GVM";
            Extension  = ".gvm";
            CanPack    = true;
            CanExtract = true;
            Translate  = true;

            Filter       = new string[] { Name + " Archive", "*.gvm" };
            PaddingByte  = 0x00;
            PackSettings = new ArchivePackSettings.GVM();
        }

        // Get the offsets, lengths, and filenames of all the files
        public override ArchiveFileList GetFileList(Stream data)
        {
            try
            {
                // Get the number of files
                ushort files = data.ReadUShort(0x0);

                // Create the array of files now
                ArchiveFileList fileList = new ArchiveFileList(files);

                // Now we can get the file offsets, lengths, and filenames
                for (int i = 0; i < files; i++)
                {
                    // Get the filename
                    string filename = data.ReadString(0xA + (i * 0x24), 28);

                    fileList.Entries[i] = new ArchiveFileList.Entry(
                        data.ReadUInt(0x2 + (i * 0x24)), // Offset
                        data.ReadUInt(0x6 + (i * 0x24)), // Length
                        (filename == String.Empty ? String.Empty : filename + (filename.IsAllUpperCase() ? ".GVR" : ".gvr")) // Filename
                    );
                }

                return fileList;
            }
            catch
            {
                // Something went wrong, so return nothing
                return null;
            }
        }


        /* To simplify the process greatly, we are going to convert
         * the GVM to a new format */
        public override MemoryStream TranslateData(Stream stream)
        {
            try
            {
                // Get the number of files, and format type in the stream
                ushort files = stream.ReadUShort(0xA).SwapEndian();
                byte formatType = stream.ReadByte(0x9);

                // Now let's see what information is contained inside the metadata
                bool containsFilename    = (formatType & (1 << 3)) > 0;
                bool containsPixelFormat = (formatType & (1 << 2)) > 0;
                bool containsDimensions  = (formatType & (1 << 1)) > 0;
                bool containsGlobalIndex = (formatType & (1 << 0)) > 0;

                // Let's figure out the metadata size
                int size_filename = 0, size_pixelFormat = 0, size_dimensions = 0, size_globalIndex = 0;
                if (containsFilename)    size_filename = 28;
                if (containsPixelFormat) size_pixelFormat = 2;
                if (containsDimensions)  size_dimensions = 2;
                if (containsGlobalIndex) size_globalIndex = 4;
                int metaDataSize = 2 + size_filename + size_pixelFormat + size_dimensions + size_globalIndex;

                // Now create the header
                MemoryStream data = new MemoryStream();
                data.Write(files);

                // Ok, try to find out data
                uint sourceOffset = stream.ReadUInt(0x4) + 0x8;

                // Write each file in the header
                uint offset = 0x2 + ((uint)files * 0x24);
                for (int i = 0; i < files; i++)
                {
                    // Ok, get the size of the GVR file
                    uint length = stream.ReadUInt(sourceOffset + 0x4) + 8;

                    // Make sure this is a valid file length
                    if (sourceOffset + length > stream.Length)
                        length -= 16; // For some reason some GVR files are like this.
                    if (sourceOffset + length > stream.Length)
                        throw new Exception();

                    // Write the offset, file length, and filename
                    data.Write(offset);      // Offset
                    data.Write(length + 16); // Length

                    if (containsFilename)
                        data.Write(stream.ReadString(0xE + (i * metaDataSize), 28), 28); // Filename
                    else
                        data.Position += 28;

                    // Add the GBIX header
                    data.Position = offset;
                    data.Write(TextureHeader.GBIX);
                    data.Write((int)0x8);

                    // Copy the global index
                    if (containsGlobalIndex)
                        data.Write(stream.ReadUInt(0xE + size_filename + size_pixelFormat + size_dimensions + (i * metaDataSize)));
                    else
                        data.Position += 4;

                    // Write out the 0x0 in the header
                    data.Write(new byte[] { 0x0, 0x0, 0x0, 0x0 });

                    // Now copy the file
                    data.Write(stream, sourceOffset, length);
                    data.Position = 0x26 + (i * 0x24);

                    sourceOffset += length.RoundUp(16);

                    // Increment the offset
                    offset += length + 16;
                }

                return data;
            }
            catch
            {
                // Something went wrong, so send as blank stream
                return new MemoryStream();
            }
        }

        // Format File
        public override Stream FormatFileToAdd(Stream data)
        {
            // Check to see if this is a GVR
            Textures images = new Textures(data, null);
            if (images.Format == TextureFormat.GVR)
            {
                // Does the file start with GVRT?
                if (data.ReadString(0x0, 4) == TextureHeader.GVRT)
                    return data;

                // Otherwise strip off the first 16 bytes
                else
                    return data.Copy(0x10, (int)data.Length - 0x10);
            }

            // Can't add this file!
            return null;
        }

        public override MemoryStream CreateHeader(string[] files, string[] archiveFilenames, int blockSize, bool[] settings, out uint[] offsetList)
        {
            try
            {
                // Let's get out settings now
                //blockSize = 24;
                blockSize = 16;
                bool addFilename    = settings[0];
                bool addPixelFormat = settings[1];
                bool addDimensions  = settings[2];
                bool addGlobalIndex = settings[3];

                // Let's figure out the metadata size, so we can create the header properly
                int metaDataSize = 2;
                if (addFilename)    metaDataSize += 28;
                if (addPixelFormat) metaDataSize += 2;
                if (addDimensions)  metaDataSize += 2;
                if (addGlobalIndex) metaDataSize += 4;

                // Create the header now
                offsetList          = new uint[files.Length];
                MemoryStream header = new MemoryStream(Number.RoundUp(0xC + (files.Length * metaDataSize), blockSize));
                header.Write(ArchiveHeader.GVM, 4);
                header.Write(header.Capacity + 8);
                
                // Set up format type
                byte formatType = 0x0;
                if (addFilename)    formatType |= (1 << 3);
                if (addPixelFormat) formatType |= (1 << 2);
                if (addDimensions)  formatType |= (1 << 1);
                if (addGlobalIndex) formatType |= (1 << 0);
                header.WriteByte(0x0);
                header.WriteByte(formatType);

                // Write number of files
                header.Write(((ushort)files.Length).SwapEndian());

                uint offset = (uint)header.Capacity + 8 + 8;

                // Start writing the information in the header
                for (int i = 0; i < files.Length; i++)
                {
                    using (FileStream data = new FileStream(files[i], FileMode.Open, FileAccess.Read))
                    {
                        // Make sure this is a GVR
                        Textures images = new Textures(data, files[i]);
                        if (images.Format != TextureFormat.GVR)
                            throw new IncorrectTextureFormat();

                        // Get the header offset
                        int headerOffset = (data.ReadString(0x0, 4) == TextureHeader.GVRT ? 0x0 : 0x10);

                        offsetList[i] = offset;
                        header.Write(((ushort)i).SwapEndian());

                        if (addFilename)
                            header.Write(Path.GetFileNameWithoutExtension(archiveFilenames[i]), 27, 28);
                        if (addPixelFormat)
                            header.Write(data, headerOffset + 0xA, 2);
                        if (addDimensions)
                        {
                            // Get the width and height
                            int width  = (int)Math.Min(Math.Log(data.ReadUShort(headerOffset + 0xC).SwapEndian(), 2) - 2, 9);
                            int height = (int)Math.Min(Math.Log(data.ReadUShort(headerOffset + 0xE).SwapEndian(), 2) - 2, 9);
                            header.WriteByte(0x0);
                            //header.WriteByte((byte)((width << 4) | height));
                            header.WriteByte((byte)((height << 4) | width));
                        }
                        if (addGlobalIndex)
                        {
                            if (headerOffset == 0x0)
                                header.Write(new byte[] {0x0, 0x0, 0x0, 0x0});
                            else
                                header.Write(data, 0x8, 4);
                        }

                        //offset += Number.RoundUp((uint)(data.Length - headerOffset), blockSize);
                    }
                }

                return header;
            }
            catch (IncorrectTextureFormat)
            {
                offsetList = null;
                return null;
            }
            catch
            {
                offsetList = null;
                return null;
            }
        }

        // Checks to see if the input stream is a GVM archive
        public override bool Check(Stream input, string filename)
        {
            try
            {
                return (input.ReadString(0x0, 4) == ArchiveHeader.GVM);
            }
            catch
            {
                return false;
            }
        }
    }
}
﻿using System;
using System.IO;
using System.Linq;
using System.Text;

namespace PuyoTools.Modules.Archive
{
    public class NarcArchive : ArchiveBase
    {
        private static readonly byte[] magicCode = { (byte)'N', (byte)'A', (byte)'R', (byte)'C', 0xFE, 0xFF, 0x00, 0x01 };

        public override ArchiveReader Open(Stream source)
        {
            return new NarcArchiveReader(source);
        }

        public override ArchiveWriter Create(Stream destination)
        {
            return new NarcArchiveWriter(destination);
        }

        /// <summary>
        /// Returns if this codec can read the data in <paramref name="source"/>.
        /// </summary>
        /// <param name="source">The data to read.</param>
        /// <returns>True if the data can be read, false otherwise.</returns>
        public static bool Identify(Stream source)
        {
            var startPosition = source.Position;
            var remainingLength = source.Length - startPosition;

            using (var reader = new BinaryReader(source, Encoding.UTF8, true))
            {
                return remainingLength > 12
                    && reader.At(startPosition, x => x.ReadBytes(magicCode.Length)).SequenceEqual(magicCode)
                    && reader.At(startPosition + 8, x => x.ReadInt32()) == remainingLength;
            }
        }
    }

    #region Archive Reader
    public class NarcArchiveReader : ArchiveReader
    {
        public NarcArchiveReader(Stream source) : base(source)
        {
            // Read the archive header
            source.Position += 12;
            ushort fatbOffset = PTStream.ReadUInt16(source); // Should always be 0x10

            // Read the FATB chunk
            source.Position = startOffset + fatbOffset + 4;
            uint fntbOffset = fatbOffset + PTStream.ReadUInt32(source);
            

            // Get the number of entries in the archive
            int numEntries = PTStream.ReadInt32(source);
            entries = new ArchiveEntryCollection(this, numEntries);

            // Let's check to see if this is a Puyo Tetris PS3 NARC by checking the offset of the first file.
            // It seems that offsets are stored differently than a normal NARC.
            bool isPs3Narc = (PTStream.ReadUInt32(source) == 0);

            // This is a Puyo Tetris PS3 NARC.
            if (isPs3Narc)
            {
                // Read the FNTB chunk
                source.Position = startOffset + fntbOffset + 4;
                uint fimgOffset = fntbOffset + PTStream.ReadUInt32(source);
                bool hasFilenames = (fimgOffset - fntbOffset != 16);
                uint filenameOffset = fntbOffset + 8 + PTStream.ReadUInt32(source);

                // Read in all the entries
                source.Position = startOffset + fatbOffset + 12;
                for (int i = 0; i < numEntries; i++)
                {
                    // Read the entry offset and length
                    int entryOffset = PTStream.ReadInt32(source);
                    int entryLength = PTStream.ReadInt32(source) - entryOffset;

                    // Read the filename (if it has one)
                    string entryFname = String.Empty;
                    if (hasFilenames)
                    {
                        long oldPosition = source.Position;
                        source.Position = startOffset + filenameOffset;

                        byte fnameLength = PTStream.ReadByte(source);

                        // Puyo Tools can't handle directory names. Just skip over them for now.
                        if ((fnameLength & 0x80) != 0)
                        {
                            // Go only up to fimgOffset to prevent an infinite loop
                            // (though that should never happen for a properly formatted NARC).
                            while ((fnameLength & 0x80) != 0 && filenameOffset < fimgOffset)
                            {
                                fnameLength &= 0x7F;
                                filenameOffset += (uint)(fnameLength + 4);
                                source.Position += fnameLength + 3;
                                fnameLength = PTStream.ReadByte(source);
                            }
                        }

                        entryFname = PTStream.ReadCString(source, fnameLength);
                        filenameOffset += (uint)(fnameLength + 1);

                        source.Position = oldPosition;
                    }

                    // Add this entry to the collection
                    entries.Add(startOffset + fimgOffset + 8 + entryOffset, entryLength, entryFname);
                }
            }
            
            // This is a NDS NARC.
            else
            {
                // Read the FNTB chunk
                source.Position = startOffset + fntbOffset + 4;
                bool hasFilenames = (PTStream.ReadUInt32(source) == 8);
                uint filenameOffset = fntbOffset + 8 + PTStream.ReadUInt32(source);

                // Read in all the entries
                source.Position = startOffset + fatbOffset + 12;
                for (int i = 0; i < numEntries; i++)
                {
                    // Read the entry offset and length
                    int entryOffset = PTStream.ReadInt32(source);
                    int entryLength = PTStream.ReadInt32(source) - entryOffset;

                    // Read the filename (if it has one)
                    string entryFname = String.Empty;
                    if (hasFilenames)
                    {
                        long oldPosition = source.Position;
                        source.Position = startOffset + filenameOffset;

                        byte fnameLength = PTStream.ReadByte(source);
                        entryFname = PTStream.ReadCString(source, fnameLength);
                        filenameOffset += (uint)(fnameLength + 1);

                        source.Position = oldPosition;
                    }

                    // Add this entry to the collection
                    entries.Add(startOffset + entryOffset, entryLength, entryFname);
                }
            }

            // Set the position of the stream to the end of the file
            source.Seek(0, SeekOrigin.End);
        }
    }
    #endregion

    #region Archive Writer
    public class NarcArchiveWriter : ArchiveWriter
    {
        public NarcArchiveWriter(Stream destination) : base(destination) { }

        public override void Flush()
        {
            throw new NotImplementedException();
        }
    }
    #endregion
}
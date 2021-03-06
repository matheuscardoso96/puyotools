﻿using PuyoTools.Formats.Archives.WriterSettings;
using PuyoTools.GUI;
using PuyoTools.Modules;
using PuyoTools.Modules.Archive;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PuyoTools.Formats.Archives
{
    /// <inheritdoc/>
    internal class AfsFormat : IArchiveFormat
    {
        private AfsFormat() { }

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        internal static AfsFormat Instance { get; } = new AfsFormat();

        public string Name => "AFS";

        public string FileExtension => ".afs";

        public ArchiveBase GetCodec() => new AfsArchive();

        public ModuleSettingsControl GetModuleSettingsControl() => new AfsWriterSettings();

        public bool Identify(Stream source, string filename) => AfsArchive.Identify(source);
    }
}

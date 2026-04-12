using System;
using System.Collections.Generic;
using System.Text;

namespace OpenWebUiUploader
{
    internal sealed class OpenWebUiRunner : IDisposable
    {
        // ---------------- Fields ----------------

        private readonly Uri serverUrl;
        private readonly FileInfo fileToUpload;
        private readonly string knowledge;
        private readonly FileInfo databasePath;
        private readonly DirectoryInfo conversionDirectory;
        private readonly bool deleteConvertedFiles;
        private readonly bool dryRun;

        // ---------------- Lifetime ----------------

        public OpenWebUiRunner( UploaderConfig config )
        {
            if( config.TryValidate( out string? message ) == false )
            {
                throw new MissingRequiredArgumentException( message ?? "" );
            }
            else
            {
                this.serverUrl = config.ServerUrl;
                this.databasePath = config.DatabasePath;
                this.fileToUpload = config.FileToUpload;
                this.knowledge = config.Knowledge;
                this.databasePath = config.DatabasePath;
                this.conversionDirectory = config.ConversionDirectory;
                this.deleteConvertedFiles = config.DeleteConvertedFiles;
                this.dryRun = config.DryRun;
            }
        }

        public void Dispose()
        {
        }

        // ---------------- Methods ----------------

        public void Run()
        {
        }
    }
}

//
// OpenWebUiUploader - A way to upload files as knowledges to Open WebUI.
// Copyright (C) 2026 Seth Hendrick
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.
//

using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.FileSystemGlobbing;
using Serilog;

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

        private readonly ILogger log;

        private readonly HttpClient httpClient;

        // ---------------- Lifetime ----------------

        public OpenWebUiRunner( UploaderConfig config, ILogger log )
        {
            if( config.TryValidate( out string? message ) == false )
            {
                throw new MissingRequiredArgumentException( message ?? "" );
            }

            this.serverUrl = config.ServerUrl;
            this.fileToUpload = config.FileToUpload;
            this.knowledge = config.Knowledge;
            this.databasePath = config.DatabasePath;
            this.conversionDirectory = config.ConversionDirectory;
            this.deleteConvertedFiles = config.DeleteConvertedFiles;
            this.dryRun = config.DryRun;

            this.log = log;
            this.httpClient = new HttpClient
            {
                BaseAddress = this.serverUrl,
            };
            this.httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue( 
                    nameof( OpenWebUiRunner ),
                    this.GetType().Assembly.GetName()?.Version?.ToString() ?? "0.0.0"
                )
            );
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }

        // ---------------- Methods ----------------

        public void Run()
        {
            try
            {
                this.RunInternal();
            }
            finally
            {
                if( this.deleteConvertedFiles )
                {
                    this.log.Debug( "Deleting converted files." );
                    if( this.conversionDirectory.Exists )
                    {
                        if( this.dryRun == false )
                        {
                            try
                            {
                                this.conversionDirectory.Delete( true );
                            }
                            catch( Exception e )
                            {
                                this.log.Error( "Failed to delete converted files: " + e.Message );
                            }
                        }
                    }
                    else
                    {
                        this.log.Verbose( "Converted files directory does not exist.  Doing nothing." );
                    }
                }
            }
        }

        private void RunInternal()
        {
            this.PrintConfig();

            IEnumerable<string> files = this.GetFiles();
            if( files.Any() == false )
            {
                throw new FileNotFoundException( "Found no file(s) at specified location." );
            }

            DirectoryInfo? databaseDirectory = this.databasePath.Directory;
            if( databaseDirectory is null )
            {
                throw new InvalidOperationException( "Database path's directory somehow is null" );
            }
            this.log.Debug( $"Database Directory: {databasePath.FullName}" );

            var exceptions = new List<Exception>();

            if( databaseDirectory.Exists == false )
            {
                this.log.Verbose( "Database directory does not exist.  Creating." );
                if( dryRun == false )
                {
                    databaseDirectory.Create();
                }
            }

            FileInfo? dbPath = GetDatabaseFilePath();

            using var database = new Database( dbPath );
            foreach( string file in files )
            {
                this.log.Information( $"Processing: {file}" );
                string relativePath = Path.GetRelativePath( databaseDirectory.FullName, file );
                this.log.Verbose( $"File path key: {relativePath}" );

                try
                {
                    FileHash? databaseFileHash = database.Files.FindById( relativePath );

                    if( File.Exists( file ) )
                    {
                        string diskFileHash = GetSha256( file );

                        if( databaseFileHash is null )
                        {
                            this.log.Verbose( "File exists on disk, but does not exist in database.  This will be uploaded." );
                            if( this.dryRun == false )
                            {
                                this.Upload( database, file, relativePath, diskFileHash );
                            }
                        }
                        else if( diskFileHash.Equals( databaseFileHash.Hash, StringComparison.OrdinalIgnoreCase ) )
                        {
                            this.log.Verbose( "File on disk's hash matcheshash in database.  Do nothing." );
                        }
                        else
                        {
                            this.log.Verbose( "File on disk's hash does not match hash in database.  Must re-upload." );
                            if( this.dryRun == false )
                            {
                                this.Upload( database, file, relativePath, diskFileHash );
                            }
                        }
                    }
                    else
                    {
                        if( databaseFileHash is null )
                        {
                            this.log.Warning( "File does not exist on disk anymore, and does not exist in database.  Do nothing." );
                        }
                        else
                        {
                            this.log.Verbose( "File does not exists on disk but is in the database.  Removing." );
                            if( this.dryRun == false )
                            {
                                this.Remove( database, file, relativePath );
                            }
                        }
                    }
                }
                catch( Exception e )
                {
                    exceptions.Add( e );
                }
            }

            if( exceptions.Any() )
            {
                throw new AggregateException( "Failed to upload at least one file." );
            }
        }

        private static string GetSha256( string filePath )
        {
            using var stream = File.OpenRead( filePath );
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash( stream );
            return Convert.ToHexString( hash ).ToLowerInvariant();
        }

        private FileInfo? GetDatabaseFilePath()
        {
            FileInfo? dbPath;
            if( this.databasePath.Exists )
            {
                this.log.Verbose( "Database exists, but dry run disabled.  Opening database file." );
                dbPath = this.databasePath;
            }
            else if( this.dryRun )
            {
                this.log.Verbose( "Database does not exist, but dry run is enabled.  Making an in-memory database." );
                dbPath = null;
            }
            else
            {
                this.log.Verbose( "Database does not exist, but dry run disabled.  Creating database file.." );
                dbPath = this.databasePath;
            }

            return dbPath;
        }

        private IEnumerable<string> GetFiles()
        {
            DirectoryInfo? directory = this.fileToUpload.Directory;
            if( directory is null )
            {
                throw new InvalidOperationException( "Input file does not appear to live in a directory." );
            }
            else if( directory.Exists == false )
            {
                throw new DirectoryNotFoundException( $"Could not find directory that contains file: {directory.FullName}" );
            }

            var globber = new Matcher();
            globber.AddInclude( this.fileToUpload.Name );

            return globber.GetResultsInFullPath( directory.FullName );
        }

        private void PrintConfig()
        {
            this.log.Debug( $"Server URL: {this.serverUrl}" );
            this.log.Debug( $"File(s) to upload: {this.fileToUpload.FullName}" );
            this.log.Debug( $"Knowledge to upload to: {this.knowledge}" );
            this.log.Debug( $"Database path: {this.databasePath.FullName}" );
            this.log.Debug( $"Conversion File Path: {this.conversionDirectory.FullName}" );
            this.log.Debug( $"Delete Converted Files: {this.deleteConvertedFiles}" );
            this.log.Debug( $"Dry Run: {this.dryRun}" );
        }

        private void Upload( Database database, string filePath, string relativePath, string diskFileHash )
        {
            var hash = new FileHash { FilePath = relativePath, Hash = diskFileHash };

            if( database.Files.Update( hash ) == false )
            {
                this.log.Error( $"Failed to update file hash in the database for: {filePath}" );
            }
        }

        private void Remove( Database database, string filePath, string relativePath )
        {
            if( database.Files.Delete( relativePath ) == false )
            {
                this.log.Error( $"Failed to remove file not on disk from database for: {filePath}" );
            }
        }
    }
}

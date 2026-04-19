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

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using DotNet.Globbing;
using ElBruno.MarkItDotNet;
using OpenWebUiUploader.Models;
using Serilog;

namespace OpenWebUiUploader
{
    internal sealed class OpenWebUiRunner : IDisposable
    {
        // ---------------- Fields ----------------

        private readonly Uri serverUrl;
        private readonly FileInfo[] filesToUpload;
        private readonly string knowledge;
        private readonly FileInfo databasePath;
        private readonly DirectoryInfo conversionDirectory;
        private readonly bool deleteConvertedFiles;
        private readonly bool dryRun;
        private readonly string apiKey;

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
            this.filesToUpload = config.FileToUpload;
            this.knowledge = config.Knowledge;
            this.databasePath = config.DatabasePath;
            this.conversionDirectory = config.ConversionDirectory;
            this.deleteConvertedFiles = config.DeleteConvertedFiles;
            this.dryRun = config.DryRun;
            this.apiKey = config.GetApiKey();

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
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Bearer", this.apiKey );
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
                this.PrintConfig();

                foreach( FileInfo file in this.filesToUpload )
                {
                    this.log.Information( $"Starting to process: {file.FullName}" );
                    this.RunInternal( file );
                }
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

        private void RunInternal( FileInfo file )
        {
            IEnumerable<string> files = this.GetFiles( file );
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
            this.DetectRemovedFiles( database, databaseDirectory );

            this.log.Information( "Uploading new or changed files." );

            if( this.conversionDirectory.Exists == false )
            {
                this.log.Verbose( "Conversion working directory not found.  Creating." );
                if( dryRun == false )
                {
                    this.conversionDirectory.Create();
                }
            }

            int currentFile = 0;
            int totalFiles = files.Count();
            foreach( string filePath in files )
            {
                ++currentFile;

                this.log.Debug( $"Processing file ({currentFile}/{totalFiles}): {filePath}" );
                string relativePath = Path.GetRelativePath( databaseDirectory.FullName, filePath );
                this.log.Verbose( $"File path key: {relativePath}" );

                try
                {
                    FileHash? databaseFileHash = database.Files.FindById( relativePath );

                    if( File.Exists( filePath ) )
                    {
                        string diskFileHash = GetSha256( filePath );

                        if( databaseFileHash is null )
                        {
                            this.log.Verbose( "File exists on disk, but does not exist in database.  This will be uploaded." );
                            if( this.dryRun == false )
                            {
                                this.Upload( database, filePath, relativePath, diskFileHash );
                            }
                        }
                        else if( diskFileHash.Equals( databaseFileHash.Hash, StringComparison.OrdinalIgnoreCase ) )
                        {
                            this.log.Verbose( "File on disk's hash matches hash in database.  Do nothing." );
                        }
                        else
                        {
                            this.log.Verbose( "File on disk's hash does not match hash in database.  Must re-upload." );
                            if( this.dryRun == false )
                            {
                                this.Remove( database, filePath, relativePath, databaseFileHash.ServerId );
                                this.Upload( database, filePath, relativePath, diskFileHash );
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
                            this.log.Verbose( "File does not exists on disk but is in the database.  Removing from database, but staying on server." );
                            if( this.dryRun == false )
                            {
                                if( database.Files.Delete( relativePath ) == false )
                                {
                                    this.log.Error( $"Failed to remove file not on disk from database for: {filePath}" );
                                }
                            }
                        }
                    }
                }
                catch( Exception e )
                {
                    this.log.Error( $"{filePath} failed: {e.GetType()} - {e.Message}" );
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

        private IEnumerable<string> GetFiles( FileInfo file )
        {
            DirectoryInfo? directory = file.Directory;
            if( directory is null )
            {
                throw new InvalidOperationException( "Input file does not appear to live in a directory." );
            }
            else if( directory.Exists == false )
            {
                throw new DirectoryNotFoundException( $"Could not find directory that contains file: {directory.FullName}" );
            }

            var files = new List<string>();

            Glob glob = Glob.Parse( file.FullName );

            foreach( FileInfo foundFile in directory.EnumerateFiles( "*", SearchOption.AllDirectories ) )
            {
                if( glob.IsMatch( foundFile.FullName ) )
                {
                    files.Add( foundFile.FullName );
                }
            }

            return files;
        }

        private void PrintConfig()
        {
            this.log.Debug( $"Server URL: {this.serverUrl}" );
            this.log.Debug( $"File(s) to upload: [{string.Join( ',', this.filesToUpload.Select( f => f.FullName ) )}]" );
            this.log.Debug( $"Knowledge to upload to: {this.knowledge}" );
            this.log.Debug( $"Database path: {this.databasePath.FullName}" );
            this.log.Debug( $"Conversion File Path: {this.conversionDirectory.FullName}" );
            this.log.Debug( $"Delete Converted Files: {this.deleteConvertedFiles}" );
            this.log.Debug( $"Dry Run: {this.dryRun}" );
        }

        private void DetectRemovedFiles( Database database, DirectoryInfo dbDirectory )
        {
            this.log.Information( "Removing files from OpenWebUI that are no longer on disk." );
            foreach( FileHash fileHash in database.Files.FindAll() )
            {
                var filePath = new FileInfo( Path.Combine( dbDirectory.FullName, fileHash.FilePath ) );
                if( filePath.Exists == false )
                {
                    this.log.Debug( $"File exists in database, but no longer on disk.  Removing: {filePath}" );
                    this.Remove( database, filePath.FullName, fileHash.FilePath, fileHash.ServerId );
                }
            }
        }

        private void Upload( Database database, string filePath, string relativePath, string diskFileHash )
        {
            FileInfo markdownFile = this.ConvertFile( filePath );

            string fileId = this.UploadFile( markdownFile );
            var hash = new FileHash
            {
                FilePath = relativePath,
                Hash = diskFileHash,
                ServerId = fileId
            };

            this.log.Debug( $"'{filePath}' uploaded, awaiting processing." );

            // Wait a little bit before checking the status.
            Thread.Sleep( new TimeSpan( 0, 0, 15 ) );

            this.WaitForProcessing( markdownFile.FullName, fileId );

            this.log.Debug( $"'{filePath}' processed, adding to knowledge." );
            this.AddToKnowledge( fileId );

            this.log.Debug( $"'{filePath}'adding to knowledge. Updaing database." );
            if( database.Files.Upsert( hash ) == false )
            {
                this.log.Error( $"Failed to update file hash in the database for: {filePath}" );
            }
        }

        private FileInfo ConvertFile( string filePath )
        {
            bool uniqueFile = false;
            int? index = null;
            FileInfo? targetFile;
            do
            {
                targetFile = new FileInfo(
                    Path.Combine(
                        this.conversionDirectory.FullName,
                        Path.GetFileNameWithoutExtension( filePath ) + $"{index}.md"
                    )
                );

                uniqueFile = ( targetFile.Exists == false );

                if( uniqueFile == false )
                {
                    if( index is null )
                    {
                        index = 0;
                    }
                    ++index;
                }
            }
            while( uniqueFile == false );

            this.log.Debug( $"Converting '{filePath}' to '{targetFile.FullName}'." );
            var converter = new MarkdownConverter();
            string markdown = converter.ConvertToMarkdown( filePath );
            File.WriteAllText( targetFile.FullName, markdown );

            return targetFile;
        }

        private string UploadFile( FileInfo filePath )
        {
            using var request = new HttpRequestMessage( HttpMethod.Post, "/api/v1/files/" );
            request.Headers.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/json" ) );
            using var formData = new MultipartFormDataContent();

            using var fileStream = filePath.Open( FileMode.Open, FileAccess.Read );
            using var fileStreamContent = new StreamContent( fileStream );
            formData.Add( fileStreamContent, "file", filePath.FullName );

            request.Content = formData;

            HttpResponseMessage fileUploadResponse = this.httpClient.Send( request );
            if( fileUploadResponse.IsSuccessStatusCode == false )
            {
                string errorResponse = fileUploadResponse.Content.ReadAsStringAsync().Result;
                throw new HttpException(
                    $"Failed to upload file.{Environment.NewLine}{fileUploadResponse.StatusCode} - {errorResponse}"
                );
            }

            AddFileResponse? response = fileUploadResponse.Content.ReadFromJsonAsync<AddFileResponse>().Result;
            if( response is null )
            {
                throw new HttpException( $"Could not convert upload file response to {nameof( AddFileResponse )}." );
            }
            else if( string.IsNullOrWhiteSpace( response.FileId ) )
            {
                throw new HttpException( $"Could not get file ID after uploading: {filePath}." );
            }
            else
            {
                return response.FileId;
            }
        }

        private void WaitForProcessing( string filePath, string fileId )
        {
            bool success = false;
            int attempts = 0;
            while( success == false )
            {
                using var request = new HttpRequestMessage( HttpMethod.Get, $"/api/v1/files/{fileId}/process/status" );

                HttpResponseMessage fileProcessStatusResponse = this.httpClient.Send( request );
                if( fileProcessStatusResponse.IsSuccessStatusCode == false )
                {
                    string errorResponse = fileProcessStatusResponse.Content.ReadAsStringAsync().Result;
                    ++attempts;

                    if( attempts > 10 )
                    {
                        throw new HttpException("Failed to check status of file after 10 attempts.  Stopping retries." );
                    }

                    this.log.Warning(
                        $"Failed to check status of file. Attempt {attempts}.{Environment.NewLine}{fileProcessStatusResponse.StatusCode} - {errorResponse}"
                    );
                }

                FileProcessingStatusResponse? response = fileProcessStatusResponse.Content.ReadFromJsonAsync<FileProcessingStatusResponse>().Result;
                if( response is null )
                {
                    throw new HttpException( $"Could not convert upload file response to {nameof( FileProcessingStatusResponse )}." );
                }
                else if( string.IsNullOrWhiteSpace( response.Status ) )
                {
                    throw new HttpException( $"Could not get status of file processing after uploading: {filePath}." );
                }
                else if( response.Status.Equals( "failed", StringComparison.OrdinalIgnoreCase ) )
                {
                    throw new HttpException( $"Failed to process uploaded file: {response.Error}" );
                }
                else if( response.Status.Equals( "completed", StringComparison.OrdinalIgnoreCase ) )
                {
                    this.log.Debug( $"File '{filePath}' done processing." );
                    success = true;
                }

                if( success == false )
                {
                    attempts = 0;
                    const int secondsToWait = 30;
                    this.log.Verbose( $"File '{filePath}' not done processing yet.  Checking in a {secondsToWait} seconds." );
                    Thread.Sleep( new TimeSpan( 0, 0, 0, secondsToWait ) );
                }
            }
        }

        private void AddToKnowledge( string fileId )
        {
            using var request = new HttpRequestMessage( HttpMethod.Post, $"/api/v1/knowledge/{this.knowledge}/file/add" );
            
            var requestContent = new AddFileToKnowledgeRequest { FileId = fileId };

            using var content = JsonContent.Create( requestContent );
            request.Content = content;

            HttpResponseMessage fileProcessStatusResponse = this.httpClient.Send( request );
            if( fileProcessStatusResponse.IsSuccessStatusCode == false )
            {
                string errorResponse = fileProcessStatusResponse.Content.ReadAsStringAsync().Result;
                if(
                    ( fileProcessStatusResponse.StatusCode == HttpStatusCode.BadRequest ) &&
                    "Duplicate content detected".Contains( errorResponse, StringComparison.OrdinalIgnoreCase )
                )
                {
                    // If the content already exists, do nothing.  It probably got added but never got added
                    // to the database.  Return success so it gets added to the database.
                    this.log.Verbose( "Duplicate content detected.  Already in knowledge, adding to database." );
                }
                else
                {
                    throw new HttpException(
                        $"Failed to add file to knowledge.{Environment.NewLine}{fileProcessStatusResponse.StatusCode} - {errorResponse}"
                    );
                }
            }
        }

        private void Remove( Database database, string filePath, string relativePath, string fileId )
        {
            this.log.Debug( $"Deleting {filePath} from knowledge." );
            this.DeleteFromKnowledge( fileId );

            this.log.Debug( $"Deleting {filePath} from server." );
            this.DeleteFromServer( fileId );

            if( database.Files.Delete( relativePath ) == false )
            {
                this.log.Error( $"Failed to remove file not on disk from database for: {filePath}" );
            }
        }

        private void DeleteFromKnowledge( string fileId )
        {
            using var request = new HttpRequestMessage( HttpMethod.Post, $"/api/v1/knowledge/{this.knowledge}/file/remove" );

            var requestContent = new AddFileToKnowledgeRequest { FileId = fileId };

            using var content = JsonContent.Create( requestContent );
            request.Content = content;

            HttpResponseMessage fileProcessStatusResponse = this.httpClient.Send( request );
            if( fileProcessStatusResponse.IsSuccessStatusCode == false )
            {
                string errorResponse = fileProcessStatusResponse.Content.ReadAsStringAsync().Result;
                throw new HttpException(
                    $"Failed to add file to knowledge.{Environment.NewLine}{fileProcessStatusResponse.StatusCode} - {errorResponse}"
                );
            }
        }

        private void DeleteFromServer( string fileId )
        {
            using var request = new HttpRequestMessage( HttpMethod.Delete, $"/api/v1/files/{fileId}" );

            HttpResponseMessage fileProcessStatusResponse = this.httpClient.Send( request );
            if( fileProcessStatusResponse.IsSuccessStatusCode == false )
            {
                string errorResponse = fileProcessStatusResponse.Content.ReadAsStringAsync().Result;
                throw new HttpException(
                    $"Failed to add file to knowledge.{Environment.NewLine}{fileProcessStatusResponse.StatusCode} - {errorResponse}"
                );
            }
        }
    }
}

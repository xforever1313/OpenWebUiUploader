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

using System.CommandLine;
using System.CommandLine.Parsing;
using Serilog;
using Serilog.Events;

namespace OpenWebUiUploader
{
    internal sealed class MainCommand
    {
        // ---------------- Fields ----------------

        private readonly TextWriter consoleOut;

        private readonly Command rootCommand;

        private readonly Option<Uri> serverUrlOption;
        private readonly Option<FileInfo> filePathOption;
        private readonly Option<string> knowledgeOption;
        private readonly Option<FileInfo> databasePath;
        private readonly Option<DirectoryInfo> conversionDirectoryOption;
        private readonly Option<bool> deleteConvertedFilesOption;
        private readonly Option<string> apiKeyEnvVarNameOption;
        private readonly Option<LogEventLevel> verbosityOption;
        private readonly Option<bool> dryRunOption;
        private readonly Option<bool> printLicenseOption;
        private readonly Option<bool> printReadmeOption;
        private readonly Option<bool> printCreditsOption;

        // ---------------- Lifetime ----------------

        public MainCommand( TextWriter consoleOut )
        {
            this.consoleOut = consoleOut;

            this.rootCommand = new RootCommand( "Uploads file(s) to a knowledge in Open WebUI." );

            this.serverUrlOption = new Option<Uri>( "--server_url" )
            {
                Description = "The URL to the Open WebUI Instance.",
                CustomParser = ( ArgumentResult result ) =>
                {
                    var token = result.Tokens.SingleOrDefault()?.Value;
                    if( string.IsNullOrWhiteSpace( token ) )
                    {
                        return null;
                    }

                    if( Uri.TryCreate( token, UriKind.Absolute, out Uri? uri ) )
                    {
                        return uri;
                    }

                    result.AddError( $"Invalid URL: {token}" );
                    return null;
                },
                Required = true
            };
            this.rootCommand.Add( this.serverUrlOption );

            this.filePathOption = new Option<FileInfo>( "--file" )
            {
                Description = "The file(s) to upload to the knowledge in Open WebUI.  " +
                              "Globs are allowed to upload multiple files.  " +
                              "Directories are not allowed.",
                Required = true
            };
            this.rootCommand.Add( this.filePathOption );

            this.knowledgeOption = new Option<string>( "--knowledge_id" )
            {
                Description = "The knowledge ID (usually a UUID) to upload to Open WebUI.",
                Required = true
            };
            this.rootCommand.Add( this.knowledgeOption );

            this.databasePath = new Option<FileInfo>( "--database_path" )
            {
                Description = "Path to the database that keeps track of file hashes.",
                Required = true
            };
            this.rootCommand.Add( this.databasePath );

            this.conversionDirectoryOption = new Option<DirectoryInfo>( "--conversion_directory" )
            {
                 Description = "Where converted markdown files go.",
                 Required = true
            };
            this.rootCommand.Add( this.conversionDirectoryOption );

            this.deleteConvertedFilesOption = new Option<bool>( "--delete_converted_files" )
            {
                DefaultValueFactory = ( ArgumentResult argResult ) => true,
                Description = "Should the deleted markdown files be removed after uploading?",
                Required = false
            };
            this.rootCommand.Add( this.deleteConvertedFilesOption );

            this.apiKeyEnvVarNameOption = new Option<string>( "--api_env_var_name" )
            {
                DefaultValueFactory = ( ArgumentResult argResult ) => UploaderConfig.DefaultApiKeyEnvVarName,
                Description = "The name of the environment variable that contains the API key; NOT the API key itself.",
                Required = false
            };
            this.rootCommand.Add( this.apiKeyEnvVarNameOption );

            this.verbosityOption = new Option<LogEventLevel>( "--verbosity" )
            {
                DefaultValueFactory = ( ArgumentResult argResult ) => LogEventLevel.Information,
                Description = "The verbosity to set to when logging.",
                Required = false
            };
            this.rootCommand.Add( this.verbosityOption );

            this.dryRunOption = new Option<bool>( "--dry_run" )
            {
                DefaultValueFactory = ( ArgumentResult argResult ) => false,
                Description = "Set to true to not change anything, just print what is happening.",
                Required = false
            };
            this.rootCommand.Add( this.dryRunOption );

            this.printLicenseOption = new Option<bool>( "--print_license" )
            {
                DefaultValueFactory = ( ArgumentResult argResult ) => false,
                Description = "Prints this program's license to stdout, and exits.  This takes priority over all other print options.",
                Required = false
            };
            this.rootCommand.Add( this.printLicenseOption );

            this.printReadmeOption = new Option<bool>( "--print_readme" )
            {
                DefaultValueFactory = ( ArgumentResult argResult ) => false,
                Description = "Prints the readme file to stdout, and exits. This takes priority over the --print_credits option.",
                Required = false
            };
            this.rootCommand.Add( this.printReadmeOption );

            this.printCreditsOption = new Option<bool>( "--print_credits" )
            {
                DefaultValueFactory = ( ArgumentResult argResult ) => false,
                Description = "Prints the third-party licenses to stdout, and exits.",
                Required = false
            };
            this.rootCommand.Add( this.printCreditsOption );

            this.rootCommand.SetAction( this.Handler );
        }

        // ---------------- Properties ----------------

        // ---------------- Methods ----------------

        public int Invoke( string[] args )
        {
            ParseResult result = this.rootCommand.Parse( args );
            if( result.Errors.Any() )
            {
                throw new ArgumentParseException(
                    "Failed to parse arguments." + Environment.NewLine + string.Join( Environment.NewLine + "- ", result.Errors.ToArray() )
                );
            }

            return result.Invoke();
        }

        private void Handler( ParseResult result )
        {
            if( result.GetValue( this.printReadmeOption ) )
            {
                Console.WriteLine( this.ReadStringResource( $"{nameof( OpenWebUiUploader )}.Resources.Readme.md" ) );
            }
            else if( result.GetValue( this.printLicenseOption ) )
            {
                Console.WriteLine( this.ReadStringResource( $"{nameof( OpenWebUiUploader )}.Resources.License.md" ) );
            }
            else if( result.GetValue( this.printCreditsOption ) )
            {
                Console.WriteLine( this.ReadStringResource( $"{nameof( OpenWebUiUploader )}.Resources.Credits.md" ) );
            }
            else
            {
                var config = new UploaderConfig(
                    result.GetValue( this.serverUrlOption ),
                    result.GetValue( this.filePathOption ),
                    result.GetValue( this.knowledgeOption ),
                    result.GetValue( this.databasePath ),
                    result.GetValue( this.conversionDirectoryOption ),
                    result.GetValue( this.deleteConvertedFilesOption ),
                    result.GetValue( this.apiKeyEnvVarNameOption ),
                    result.GetValue( this.dryRunOption )
                );

                LogEventLevel logLevel = result.GetValue( this.verbosityOption );

                using var logger = new LoggerConfiguration()
                    .MinimumLevel.Is( logLevel )
                    .WriteTo.Console()
                    .CreateLogger();

                using var runner = new OpenWebUiRunner( config, logger );
                runner.Run();
            }
        }

        private string ReadStringResource( string resourceName )
        {
            using( Stream? stream = this.GetType().Assembly.GetManifestResourceStream( resourceName ) )
            {
                if( stream is null )
                {
                    throw new InvalidOperationException( $"Could not open stream for {resourceName}" );
                }

                using( StreamReader reader = new StreamReader( stream ) )
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}

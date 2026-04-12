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

using System.Diagnostics.CodeAnalysis;

namespace OpenWebUiUploader
{
    /// <summary>
    /// Configuration to upload files to an Open WebUI instance.
    /// </summary>
    /// <param name="ServerUrl">
    /// The URL to the Open WebUI instance.
    /// </param>
    /// <param name="FileToUpload">
    /// The file to upload.  Globs are okay.
    /// </param>
    /// <param name="Knowledge">
    /// The knowledge to upload files to.
    /// </param>
    /// <param name="DatabasePath">
    /// The database file to keep track of hashes.  If a file hash does not change,
    /// it will not be re-uploaded.
    /// </param>
    /// <param name="ConversionDirectory">
    /// Where to output converted files.
    /// </param>
    /// <param name="DeleteConvertedFiles">
    /// Should the converted files be deleted or not?
    /// </param>
    /// <param name="DryRun">
    /// Set to true to not do anything, but rather print everything out.
    /// </param>
    public record class UploaderConfig(
        Uri? ServerUrl,
        FileInfo? FileToUpload,
        string? Knowledge,
        FileInfo? DatabasePath,
        DirectoryInfo? ConversionDirectory,
        bool DeleteConvertedFiles,
        bool DryRun
    )
    {
        // ---------------- Methods ----------------

        [MemberNotNullWhen(
            returnValue: true,
            nameof( ServerUrl ),
            nameof( FileToUpload ),
            nameof( Knowledge ),
            nameof( DatabasePath ),
            nameof( ConversionDirectory ) 
        )]
        public bool TryValidate( out string? errorMessage )
        {
            var errors = new List<string>();

            if( this.ServerUrl is null )
            {
                errors.Add( $"{nameof( this.ServerUrl )} is not specified, but must be." );
            }

            if( this.FileToUpload is null )
            {
                errors.Add( $"{nameof( this.DatabasePath )} is not specified, but must be." );
            }

            if( string.IsNullOrWhiteSpace( this.Knowledge ) )
            {
                errors.Add( $"{nameof( this.Knowledge )} is not specified, but must be." );
            }

            if( this.DatabasePath is null )
            {
                errors.Add( $"{nameof( this.DatabasePath )} is not specified, but it must be." );
            }

            if( ( this.ConversionDirectory is null ) )
            {
                errors.Add( $"{nameof( this.ConversionDirectory )} is not specified, but it must be." );
            }

            if(
                ( this.ServerUrl is null ) ||
                ( this.FileToUpload is null ) ||
                string.IsNullOrWhiteSpace( this.Knowledge ) ||
                ( this.DatabasePath is null ) ||
                ( this.ConversionDirectory is null )
            )
            {
                errorMessage = "Required arguments are missing or null." + Environment.NewLine + string.Join( $"-{Environment.NewLine} ", errors.ToArray() );
                return false;
            }
            else
            {
                errorMessage = null;
                return true;
            }
        }
    }
}

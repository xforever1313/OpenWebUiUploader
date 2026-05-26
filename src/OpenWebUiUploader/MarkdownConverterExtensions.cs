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

using System.Reflection;
using ElBruno.MarkItDotNet;

namespace OpenWebUiUploader
{
    public static class MarkdownConverterExtensions
    {
        public static void SetMaximumFileSize( this MarkdownConverter converter, long maxFileSize )
        {
            MarkdownService service = converter.GetMarkdownService();

            const string varName = "_options";

            FieldInfo? field = typeof( MarkdownService ).GetField(
                varName,
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            if( field is null )
            {
                throw new InvalidOperationException( $"Could not find {varName} field in {nameof( MarkdownService )}." );
            }

            MarkItDotNetOptions? options = field.GetValue( service ) as MarkItDotNetOptions;
            if( options is null )
            {
                throw new InvalidOperationException( $"Could not convert to {nameof( MarkItDotNetOptions )}." );
            }

            options.MaxFileSizeBytes = 0;
        }

        private static MarkdownService GetMarkdownService( this MarkdownConverter converter )
        {
            const string varName = "_service";

            // This is private and there's no way to set it, so we need to use reflection.
            FieldInfo? field = typeof( MarkdownConverter ).GetField(
                varName,
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            if( field is null )
            {
                throw new InvalidOperationException( $"Could not find {varName} field in {nameof( MarkdownConverter )}." );
            }

            MarkdownService? service = field.GetValue( converter ) as MarkdownService;
            if( service is null )
            {
                throw new InvalidOperationException( $"Could not convert to {nameof( MarkdownService )}." );
            }

            return service;
        }
    }
}
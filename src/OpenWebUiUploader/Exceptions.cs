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

namespace OpenWebUiUploader
{
    public sealed class ArgumentParseException : Exception
    {
        // ---------------- Lifetime ----------------

        public ArgumentParseException( string message ) :
            base( message )
        {
        }
    }

    public sealed class MissingRequiredArgumentException : Exception
    {
        // ---------------- Lifetime ----------------

        public MissingRequiredArgumentException( string message ) :
            base( message )
        {
        }
    }

    public sealed class DatabaseException : Exception
    {
        // ---------------- Lifetime ----------------

        public DatabaseException( string message ) :
            base( message )
        {
        }
    }

    public sealed class HttpException : Exception
    {
        // ---------------- Lifetime ----------------

        public HttpException( string message ) :
            base( message )
        {
        }
    }
}

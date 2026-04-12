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

using OpenWebUiUploader;

internal class Program
{
    private static int Main( string[] args )
    {
        try
        {
            var mainCommand = new MainCommand( Console.Out );
            return mainCommand.Invoke( args );
        }
        catch( ArgumentParseException e )
        {
            Console.WriteLine( e.Message );
            return 1;
        }
        catch( MissingRequiredArgumentException e )
        {
            Console.WriteLine( e.Message );
            return 2;
        }
        catch( DirectoryNotFoundException e )
        {
            Console.WriteLine( e.Message );
            return 3;
        }
        catch( FileNotFoundException e )
        {
            Console.WriteLine( e.Message );
            return 4;
        }
        catch( Exception e )
        {
            Console.WriteLine( "FATAL: Unhandled Exception:" );
            Console.WriteLine( e.ToString() );
            return -1;
        }
    }
}
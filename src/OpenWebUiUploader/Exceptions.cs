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
}

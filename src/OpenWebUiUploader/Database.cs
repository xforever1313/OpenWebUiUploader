using LiteDB;

namespace OpenWebUiUploader
{
    internal sealed class Database : IDisposable
    {
        // ---------------- Fields ----------------

        internal const string InMemoryDatabaseName = ":memory:";

        private readonly LiteDatabase dbConnection;

        // ---------------- Lifetime ----------------

        public Database( FileInfo? filePath )
        {
            var connectionString = new ConnectionString
            {
                AutoRebuild = false,
                Connection = ConnectionType.Direct,
                Filename = filePath?.FullName ?? InMemoryDatabaseName
            };

            this.dbConnection = new LiteDatabase( connectionString );

            this.Files = this.dbConnection.GetCollection<FileHash>();
        }

        public void Dispose()
        {
            this.dbConnection.Dispose();
        }

        // ---------------- Properties ----------------

        public ILiteCollection<FileHash> Files { get; }

        // ---------------- Methods ----------------

        public void BeginTransaction()
        {
            bool success = this.dbConnection.BeginTrans();
            if( success == false )
            {
                throw new DatabaseException( "Failed to begin transaction" );
            }
        }

        public void Commit()
        {
            bool success = this.dbConnection.Commit();
            if( success == false )
            {
                throw new DatabaseException( "Failed to commit transaction" );
            }
        }

        public void Rollback()
        {
            bool success = this.dbConnection.Rollback();
            if( success == false )
            {
                throw new DatabaseException( "Failed to rollback transaction" );
            }
        }
    }
}

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

using System.ComponentModel.DataAnnotations;

namespace OpenWebUiUploader
{
    public sealed record class FileHash
    {
        /// <summary>
        /// The path to the file that should be uploaded.
        /// The path is relative to the directory the database lives in.
        /// </summary>
        [Key]
        public required string FilePath { get; init; }

        /// <summary>
        /// The sha256 hash of the file.  If the hash mismatches,
        /// the file will be re-uploaded.
        /// </summary>
        public required string Hash { get; init; }

        /// <summary>
        /// The file ID on the Open WebUI server.
        /// </summary>
        public required string ServerId { get; set; }
    }
}

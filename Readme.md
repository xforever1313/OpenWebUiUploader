# Open WebUI Uploader

This is a tool that allows one to upload files into a [knowledge](https://docs.openwebui.com/features/workspace/knowledge/).

How it works is it reads each file that is passed in and takes the sha256 hash of it.  If the file does not exist in a local database or the hash does not match what is in the database, the file will be uploaded to the specified knowledge.  If the file already exists in the knowledge, it will be deleted and re-uploaded if the hash differs.  No action is taken if the hash changes.

The database, once created, should not be moved on the filesystem.  The file path of the items in the database are relative to the directory the database is in.  This also serves as the primary key.

By using [ElBruno.MarkItDotNet](https://github.com/elbruno/ElBruno.MarkItDotNet), the files will first be converted to markdown so it can more easily be consumed by Open WebUI.  Please see the readme on its GitHub to see what it can convert.

## Installing

This is a dotnet tool.  To install, first download the [Dotnet 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).  Then, on the command line run ```dotnet tool install -g OpenWebUiInstaller```

## Usage

Run ```openwebui_upload --help``` to see the options, but here's a brief overview:

* ```--server_url``` - The URL to the Open WebUI Instance.
* ```--file``` - The file path of file(s) to upload.  Globs are allowed (e.g. ./*.pdf).
* ```--knowledge_id``` - The UUID of the knowledge.  Open the knowledge in a web browser, and this is the value in the URL after ```/knowledge/```, but before any ```?``` characters.
* ```--database_path``` - The path to the database on the local PC to store the file hashes.
* ```--conversion_directory``` - The directory where converted markdown files are kept before being uploaded.  To keep the converted files, specify ```--delete_converted_files=true```.  If the directory exists before starting the program, it will not run so files inside the directory are not accidentally deleted.
* ```--api_env_var_name``` - The name of the environment variable that contains your Open WebUI's API key.  The API key can not be passed in directly as a command line argument, as any user can see command line arguments; exposing your API key to anyone on the PC.

## Limitations

* The database can not be moved on the filesystem once created.
* The knowledge must exist before running.

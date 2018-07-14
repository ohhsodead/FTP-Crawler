# Web Crawler

Simple CLI FTP file crawler written in c-sharp

## Usage

To use, simply run/compile the application and select the option to proceed. __Not recommended for productional use.__

Here is the `config.json` you'll need to configure, place it in your crawler's startup directory (it'll be created if not existent anyway).

```json
{
  "ServersFilename": "C:/ftp-servers.txt",
  "OutputFilename": "C:/ftp-files",
  "Overwrite": "False",
  "RequestTimeout": 600000,
}
```

* `ServersFilename` is the URI pointing to the list of servers you wish to crawl, preferably plain text and split by seperate lines. Note: this can be a local or web file (string)
* `OutputFilename` is the URI pointing to the output file it'll write the results to, without extension (string)
* `Overwrite` indicates whether it will rewrite the entire output or skip existing/duplicates (boolean)
* `RequestTimeout` sets the timeout, in milliseconds, for the request to the web servers (integer)

## Output

The crawler writes to the specified output path containing a class object per line that represents the file. An example of this is shown below:

```csharp
{
  "Name": "some_file_1",
  "Size": "15432",
  "LastModified": "2015-04-23T20:44:41+01:00", 
  "URL": "ftp://ftp.server.com/public/some_files_1.jpg"
}
```

* `Name` is the full name of the file, with extension.
* `Size` is the total size of the file in bytes.
* `LastModified` is the date and time the file was last  modified. 
* `URL` is the direct ftp file Url/Uri.

## Contributing

All contributions are welcome just send a pull request. It is recommended to use Visual Studio 2017 when making code changes in this project. You can download the community version for free [here](https://www.visualstudio.com/downloads/).

## License

This project is licensed under the GNU General Public License v3.0 License - see the [LICENSE.md](LICENSE.md) file for details.

## Acknowledgments

* EasyConsole
* Newtonsoft.Json
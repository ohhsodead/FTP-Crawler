# Web Crawler

Simple CLI FTP file crawler written in c-sharp

## Usage

To use, simply run/compile the application and select the option you desire. 

Here is the `config.json` you'll need to configure, place it in your crawler's startup directory (it'll be created if not existent anyway). __Not recommended for productional use.__

```json
{
  "ServersFilename": "C:/ftp-servers.txt", // Full file path for servers
  "OutputFilename": "C:/ftp-files", // Don't include extension
  "Overwrite": "False", // True/False
  "RequestTimeout": 600000, // Timeout in milliseconds 
  "Extensions": "*" // Extensions supported, `*` defines all types
}
```

I'll somewhat explain what these do...

* `ServersFilename` is the URI pointing to the list of servers you wish to crawl, preferably plain text and split by seperate lines. Note: this can be a local or web file (string)
* `OutputFilename` is the URI pointing to the output file it'll write the results to (string)
* `Overwrite` indicates whether it will rewrite the entire output or skip existing/duplicates (boolean)
* `RequestTimeout` sets the timeout, in milliseconds, for the request to the web servers (integer)
* `Extensions` are the types to be crawled. `*` allows for all/any extensions supported, otherwise specified using a `|` between them, for example `MP4|MP3|PDF`	

## Output

The craler writes to the specified output path containing a class object per line that represents the file. An example of this is shown below:

```json
{
  "Type": "JPG", // Extension/type, preferably capitalized
  "Name": "some_file_1", // Full name, no extension
  "Size": "15432", // Total size in bytes
  "DateUploaded": "2015-04-23T20:44:41+01:00", // Date/time uploaded in full format
  "Host": "ftp.server.com", // Server that it's hosted on
  "URL": "ftp://ftp.server.com/public/some_files_1.jpg" // Direct full URL/URI path
}
```

## Contributing

All contributions are welcome just send a pull request. It is recommended to use Visual Studio 2017 when making code changes in this project. You can download the community version for free [here](https://www.visualstudio.com/downloads/).

## License

This project is licensed under the GNU General Public License v3.0 License - see the [LICENSE.md](LICENSE.md) file for details.

## Acknowledgments

* EasyConsole
* Newtonsoft.Json
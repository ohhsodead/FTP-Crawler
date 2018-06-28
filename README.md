# Web Crawler

Basic concept of a web crawler, solely written for experimental purposes

## Usage

It's just as simple as a configuration file, edit the values to your desire and you're all set. Hopefully expanding the features will come soon, but at the moment I'm working on alternative methods to get the listing.

Here is the `crawler-config.json` you'll need, place it in your Web Crawler directory (startup path) (it will create one if not found anyway):

```json
{
  "ServerList": "FILE-PATH-TO-READ",
  "PathToWrite": "PATH-TO-WRITE",
  "OneFile": "True",
  "OneFileName": "FILE-NAME",
  "RewriteList": "False",
  "RequestTimeout": 300000,
  "SubDirectories": "True",
  "FileTypes": "*"
}
```

I'll somewhat explain what these do...

* `ServerList` is the file URI of your web servers you wish the progam to crawl, on seperate lines and preferably in a text file . Note: this can be a local file or a web file too. (string)
* `PathToWrite` is the directory it'll use to output the results. (string)
* `OneFile` indicates whether it will output all results to one file, or seperate lists for each web server. (True/False)
* `OneFileName` sets the file name to be used for output. (string)
* `RewriteList` indicates whether to re-write the `OneFile` output (delete the old one), or to get existing files in file and not add them if found. (True/False)
* `RequestTimeout` sets the timeout (i milliseconds) for the request to the web servers. (integer)
* `SubDirectories` indicates whether it'll crawl sub directories. (True/False)
* `FileTypes` sets the file types to be added. `*` will allow for all/any extensions, otherwise you need to specify them by using a `|` between them, for example `MP4|MP3|PDF`	

## Output

The output writes to a list of class objects per line that contains the web file. An example of a file is shown below:

```json
{
  "Type": "MP4",
  "Name": "File name (no extension)",
  "Size": "Total size in bytes",
  "DateUploaded": "DateTime uploaded, example: 2015-04-23T20:44:41+01:00",
  "Host": "Server where file's located",
  "URL": "The direct URL/URI to the file"
}
```

## Contributing

All contributions are welcome just send a pull request. It is recommended to use Visual Studio 2017 when making code changes in this project. You can download the community version for free [here](https://www.visualstudio.com/downloads/).

## License

This project is licensed under the GNU General Public License v3.0 License - see the [LICENSE.md](LICENSE.md) file for details.

## Acknowledgments

* EasyConsole
* Newtonsoft.Json
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal class DownloadItem
{
    public Uri Uri { get; set; }
    public string DownloadPath { get; set; }
    public string Name { get; set; }
    public Action OnComplete { get; set; }

    public DownloadItem(string name, string url, string path)
    {
        Name = name;
        Uri = new Uri(url);
        DownloadPath = path;
    }
}

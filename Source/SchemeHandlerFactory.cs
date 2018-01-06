using CefSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoCef
{
  public class SchemeHandlerFactory : ISchemeHandlerFactory
  {
    public const string SchemeName = "custom";

    readonly IDictionary<string, string> ResourceDictionary = new Dictionary<string, string>();

    public IResourceHandler Create(IBrowser browser, IFrame frame, string schemeName, IRequest request)
    {
      //Notes:
      // - The 'host' portion is entirely ignored by this scheme handler.
      // - If you register a ISchemeHandlerFactory for http/https schemes you should also specify a domain name
      // - Avoid doing lots of processing in this method as it will affect performance.
      // - Use the Default ResourceHandler implementation

      var uri = new Uri(request.Url);
      var fileName = uri.AbsolutePath;

      string resource;
      if (ResourceDictionary.TryGetValue(fileName, out resource) && !string.IsNullOrEmpty(resource))
      {
        var fileExtension = Path.GetExtension(fileName);
        return ResourceHandler.FromString(resource, includePreamble: true, mimeType: ResourceHandler.GetMimeType(fileExtension));
      }

      //Load a file directly from disk, then cache it
      if (File.Exists($"html{fileName}"))
      {
        var text = File.ReadAllText($"html{fileName}");
        var fileExtension = Path.GetExtension(fileName);
        ResourceDictionary.Add(fileName, text);
        return ResourceHandler.FromString(text, includePreamble: true, mimeType: ResourceHandler.GetMimeType(fileExtension));
      }

      return null;
    }
  }
}

using CefSharp;
using CefSharp.Internals;
using CefSharp.OffScreen;
using CefSharp.SchemeHandler;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoCef
{
  public class MonoCefBrowser : ChromiumWebBrowser
  {
    public MonoCefBrowser(GraphicsDevice gd, string address, BrowserSettings browserSettings, RequestContext requestContext)
      : base(address, browserSettings, requestContext, false)
    {
      this.Graphics = gd;
    }

    readonly GraphicsDevice Graphics;
    public event EventHandler<NewFrameEventArgs> NewFrame;

    Stopwatch TotalTime = new Stopwatch();
    int RenderCount;

    public override void InvokeRenderAsync(BitmapInfo bitmapInfo)
    {
      if (this.Bitmap != null)
      {
        Texture2D texture;
        lock (bitmapInfo.BitmapLock)
        {
          //var stride = bitmapInfo.Width * bitmapInfo.BytesPerPixel;
          //bitmap = new Bitmap(bitmapInfo.Width, bitmapInfo.Height, stride, PixelFormat.Format32bppPArgb, bitmapInfo.BackBufferHandle);
          TotalTime.Start();
          texture = GetTexture(this.Bitmap);
          TotalTime.Stop();
          RenderCount++;
          Console.WriteLine($"{TotalTime.ElapsedMilliseconds / (double)RenderCount}");
        }
        NewFrame?.Invoke(this, new NewFrameEventArgs(texture));
      }
      //bitmap.Dispose();
      base.InvokeRenderAsync(bitmapInfo);
    }


    private Texture2D GetTexture(Bitmap bmp)
    {
      int[] imgData = new int[bmp.Width * bmp.Height];
      Texture2D texture = new Texture2D(Graphics, bmp.Width, bmp.Height);

      unsafe
      {
        // lock bitmap
        BitmapData origdata = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, bmp.PixelFormat);

        uint* byteData = (uint*)origdata.Scan0;

        // Switch bgra -> rgba
        for (int i = 0; i < imgData.Length; i++)
        {
          byteData[i] = (byteData[i] & 0x000000ff) << 16 | (byteData[i] & 0x0000FF00) | (byteData[i] & 0x00FF0000) >> 16 | (byteData[i] & 0xFF000000);
        }

        // copy data
        System.Runtime.InteropServices.Marshal.Copy(origdata.Scan0, imgData, 0, bmp.Width * bmp.Height);

        byteData = null;

        // unlock bitmap
        bmp.UnlockBits(origdata);
      }

      texture.SetData(imgData);

      return texture;
    }
  }

  public class NewFrameEventArgs : EventArgs
  {
    public readonly Texture2D Frame;
    public NewFrameEventArgs(Texture2D frame)
    {
      this.Frame = frame;
    }
  }

  public class OffscreenBrowserRenderer : IDisposable
  {
    public const string BaseUrl = "custom://cefsharp";
    public const string DefaultUrl = BaseUrl + "/index.html";
    private const bool DebuggingSubProcess = false;
    private const string CachePath = "cache";

    public OffscreenBrowserRenderer()
    {
      Init(multiThreadedMessageLoop: true, browserProcessHandler: new BrowserProcessHandler());
    }

    public event Action<object> DataChanged;

    public void Dispose()
    {
      Cef.Shutdown();
    }

    static void Init(bool multiThreadedMessageLoop, IBrowserProcessHandler browserProcessHandler)
    {
      // Set Google API keys, used for Geolocation requests sans GPS.  See http://www.chromium.org/developers/how-tos/api-keys
      // Environment.SetEnvironmentVariable("GOOGLE_API_KEY", "");
      // Environment.SetEnvironmentVariable("GOOGLE_DEFAULT_CLIENT_ID", "");
      // Environment.SetEnvironmentVariable("GOOGLE_DEFAULT_CLIENT_SECRET", "");

      // Widevine CDM registration - pass in directory where Widevine CDM binaries and manifest.json are located.
      // For more information on support for DRM content with Widevine see: https://github.com/cefsharp/CefSharp/issues/1934
      //Cef.RegisterWidevineCdm(@".\WidevineCdm");

      //Chromium Command Line args
      //http://peter.sh/experiments/chromium-command-line-switches/
      //NOTE: Not all relevant in relation to `CefSharp`, use for reference purposes only.

      var settings = new CefSettings();
      settings.RemoteDebuggingPort = 8088;
      settings.CefCommandLineArgs.Add("transparent-painting-enabled", "1");
      //The location where cache data will be stored on disk. If empty an in-memory cache will be used for some features and a temporary disk cache for others.
      //HTML5 databases such as localStorage will only persist across sessions if a cache path is specified. 
      settings.CachePath = CachePath;
      //settings.UserAgent = "CefSharp Browser" + Cef.CefSharpVersion; // Example User Agent
      //settings.CefCommandLineArgs.Add("renderer-process-limit", "1");
      //settings.CefCommandLineArgs.Add("renderer-startup-dialog", "1");
      //settings.CefCommandLineArgs.Add("enable-media-stream", "1"); //Enable WebRTC
      //settings.CefCommandLineArgs.Add("no-proxy-server", "1"); //Don't use a proxy server, always make direct connections. Overrides any other proxy server flags that are passed.
      //settings.CefCommandLineArgs.Add("debug-plugin-loading", "1"); //Dumps extra logging about plugin loading to the log file.
      //settings.CefCommandLineArgs.Add("disable-plugins-discovery", "1"); //Disable discovering third-party plugins. Effectively loading only ones shipped with the browser plus third-party ones as specified by --extra-plugin-dir and --load-plugin switches
      //settings.CefCommandLineArgs.Add("enable-system-flash", "1"); //Automatically discovered and load a system-wide installation of Pepper Flash.
      //settings.CefCommandLineArgs.Add("allow-running-insecure-content", "1"); //By default, an https page cannot run JavaScript, CSS or plugins from http URLs. This provides an override to get the old insecure behavior. Only available in 47 and above.

      //settings.CefCommandLineArgs.Add("enable-logging", "1"); //Enable Logging for the Renderer process (will open with a cmd prompt and output debug messages - use in conjunction with setting LogSeverity = LogSeverity.Verbose;)
      //settings.LogSeverity = LogSeverity.Verbose; // Needed for enable-logging to output messages

      //settings.CefCommandLineArgs.Add("disable-extensions", "1"); //Extension support can be disabled
      //settings.CefCommandLineArgs.Add("disable-pdf-extension", "1"); //The PDF extension specifically can be disabled

      //Load the pepper flash player that comes with Google Chrome - may be possible to load these values from the registry and query the dll for it's version info (Step 2 not strictly required it seems)
      //settings.CefCommandLineArgs.Add("ppapi-flash-path", @"C:\Program Files (x86)\Google\Chrome\Application\47.0.2526.106\PepperFlash\pepflashplayer.dll"); //Load a specific pepper flash version (Step 1 of 2)
      //settings.CefCommandLineArgs.Add("ppapi-flash-version", "20.0.0.228"); //Load a specific pepper flash version (Step 2 of 2)

      //NOTE: For OSR best performance you should run with GPU disabled:
      // `--disable-gpu --disable-gpu-compositing --enable-begin-frame-scheduling`
      // (you'll loose WebGL support but gain increased FPS and reduced CPU usage).
      // http://magpcss.org/ceforum/viewtopic.php?f=6&t=13271#p27075
      //https://bitbucket.org/chromiumembedded/cef/commits/e3c1d8632eb43c1c2793d71639f3f5695696a5e8

      //NOTE: The following function will set all three params
      //settings.SetOffScreenRenderingBestPerformanceArgs();
      //settings.CefCommandLineArgs.Add("disable-gpu", "1");
      //settings.CefCommandLineArgs.Add("disable-gpu-compositing", "1");
      //settings.CefCommandLineArgs.Add("enable-begin-frame-scheduling", "1");

      //settings.CefCommandLineArgs.Add("disable-gpu-vsync", "1"); //Disable Vsync

      //Disables the DirectWrite font rendering system on windows.
      //Possibly useful when experiencing blury fonts.
      //settings.CefCommandLineArgs.Add("disable-direct-write", "1");

      settings.MultiThreadedMessageLoop = multiThreadedMessageLoop;
      settings.ExternalMessagePump = !multiThreadedMessageLoop;

      // Off Screen rendering (WPF/Offscreen)
      settings.WindowlessRenderingEnabled = true;

      //Disable Direct Composition to test https://github.com/cefsharp/CefSharp/issues/1634
      //settings.CefCommandLineArgs.Add("disable-direct-composition", "1");

      // DevTools doesn't seem to be working when this is enabled
      // http://magpcss.org/ceforum/viewtopic.php?f=6&t=14095
      //settings.CefCommandLineArgs.Add("enable-begin-frame-scheduling", "1");

      var proxy = ProxyConfig.GetProxyInformation();
      switch (proxy.AccessType)
      {
        case InternetOpenType.Direct:
          {
            //Don't use a proxy server, always make direct connections.
            settings.CefCommandLineArgs.Add("no-proxy-server", "1");
            break;
          }
        case InternetOpenType.Proxy:
          {
            settings.CefCommandLineArgs.Add("proxy-server", proxy.ProxyAddress);
            break;
          }
        case InternetOpenType.PreConfig:
          {
            settings.CefCommandLineArgs.Add("proxy-auto-detect", "1");
            break;
          }
      }

      //settings.LogSeverity = LogSeverity.Verbose;

      if (DebuggingSubProcess)
      {
        var architecture = Environment.Is64BitProcess ? "x64" : "x86";
        settings.BrowserSubprocessPath = "..\\..\\..\\..\\CefSharp.BrowserSubprocess\\bin\\" + architecture + "\\Debug\\CefSharp.BrowserSubprocess.exe";
      }

      settings.RegisterScheme(new CefCustomScheme
      {
        SchemeName = SchemeHandlerFactory.SchemeName,
        SchemeHandlerFactory = new SchemeHandlerFactory(),
        IsSecure = true //treated with the same security rules as those applied to "https" URLs
      });

      settings.FocusedNodeChangedEnabled = true;

      if (!Cef.Initialize(settings, performDependencyCheck: !DebuggingSubProcess, browserProcessHandler: browserProcessHandler))
      {
        throw new Exception("Unable to Initialize Cef");
      }

      Cef.AddCrossOriginWhitelistEntry(BaseUrl, "https", "cefsharp.com", false);

      //Experimental option where bound async methods are queued on TaskScheduler.Default.
      //CefSharpSettings.ConcurrentTaskExecution = true;
    }

    RequestContext RequestContext;
    public MonoCefBrowser Browser;
    public Texture2D CurrentFrame;

    public void Resize(Size size)
    {
      Browser.Size = size;
    }

    public void SetMarshalledData(object data)
    {
      this.Browser.ExecuteScriptAsync("pushData", Newtonsoft.Json.JsonConvert.SerializeObject(data));
    }
    public async Task<T> GetMarshalledData<T>()
    {
      var response = await this.Browser.EvaluateScriptAsync("pullData()");
      try
      {
        return JsonConvert.DeserializeObject<T>(response.Result.ToString());
      }
      catch
      {
        return default(T);
      }
    }

    int ChangeCount = 0;
    public async Task<bool> CheckChanged()
    {
      var response = await this.Browser.EvaluateScriptAsync("changeCount()");
      var newChangeCount = (int)response.Result;
      var different = newChangeCount != ChangeCount;
      ChangeCount = newChangeCount;
      return different;
    }

    public void PullLatestDataIfChanged<T>()
    {
      CheckChanged().ContinueWith(task =>
      {
        if (task.Result)
        {
          GetMarshalledData<T>().ContinueWith(task2 =>
          {
            if (task2.Result != null)
            {
              this.DataChanged?.Invoke(task2.Result);
            }
          });
        }
      });
    }

    public async Task MainAsync(GraphicsDevice gd, IntPtr windowHandle, string url, object data, Size size, double zoomLevel = 1.0)
    {
      if (Browser != null)
      {
        Browser.NewFrame -= Browser_NewFrame;
        Browser.Dispose();
      }

      var browserSettings = new BrowserSettings();
      //Reduce rendering speed to one frame per second so it's easier to take screen shots
      browserSettings.WindowlessFrameRate = 30;
      var requestContextSettings = new RequestContextSettings { CachePath = CachePath };

      // RequestContext can be shared between browser instances and allows for custom settings
      // e.g. CachePath
      RequestContext = new RequestContext(requestContextSettings);
      Browser = new MonoCefBrowser(gd, url, browserSettings, RequestContext);
      Browser.CreateBrowser(windowHandle);
      Browser.NewFrame += Browser_NewFrame;
      Browser.Size = size;
      if (zoomLevel > 1)
      {
        Browser.FrameLoadStart += (s, argsi) =>
        {
          var b = (ChromiumWebBrowser)s;
          if (argsi.Frame.IsMain)
          {
            b.SetZoomLevel(zoomLevel);
          }
        };
      }
      await LoadPageAsync(Browser);

      //Check preferences on the CEF UI Thread
      await Cef.UIThreadTaskFactory.StartNew(delegate
      {
        var preferences = RequestContext.GetAllPreferences(true);

        //Check do not track status
        var doNotTrack = (bool)preferences["enable_do_not_track"];

        Debug.WriteLine("DoNotTrack:" + doNotTrack);
      });

      var onUi = Cef.CurrentlyOnThread(CefThreadIds.TID_UI);

      // For Google.com pre-pupulate the search text box
      //await Browser.EvaluateScriptAsync("document.getElementById('lst-ib').value = 'CefSharp Was Here!'");

      // Wait for the screenshot to be taken,
      // if one exists ignore it, wait for a new one to make sure we have the most up to date
      //await Browser.ScreenshotAsync(true).ContinueWith(DisplayBitmap);

      await LoadPageAsync(Browser, url);

      //Gets a wrapper around the underlying CefBrowser instance
      var cefBrowser = Browser.GetBrowser();
      // Gets a warpper around the CefBrowserHost instance
      // You can perform a lot of low level browser operations using this interface
      var cefHost = cefBrowser.GetHost();

      SetMarshalledData(data);

      //You can call Invalidate to redraw/refresh the image
      cefHost.Invalidate(PaintElementType.View);
    }

    private void Browser_NewFrame(object sender, NewFrameEventArgs e)
    {
      this.CurrentFrame = e.Frame;
    }

    public Task LoadPageAsync(IWebBrowser browser, string address = null)
    {
      //If using .Net 4.6 then use TaskCreationOptions.RunContinuationsAsynchronously
      //and switch to tcs.TrySetResult below - no need for the custom extension method
      var tcs = new TaskCompletionSource<bool>();

      EventHandler<LoadingStateChangedEventArgs> handler = null;
      handler = (sender, args) =>
      {
        //Wait for while page to finish loading not just the first frame
        if (!args.IsLoading)
        {
          browser.LoadingStateChanged -= handler;
          //This is required when using a standard TaskCompletionSource
          //Extension method found in the CefSharp.Internals namespace
          tcs.TrySetResultAsync(true);
        }
        browser.GetBrowser().GetHost().Invalidate(PaintElementType.View);
      };

      browser.LoadingStateChanged += handler;

      if (!string.IsNullOrEmpty(address))
      {
        browser.Load(address);
      }
      return tcs.Task;
    }

    public void HandleMouseMove(int x, int y)
    {
      if (Browser != null)
      {
        Browser.GetBrowser().GetHost().SendMouseMoveEvent(x, y, false, CefEventFlags.None);
      }
    }
    public void HandleMouseDown(int x, int y, MouseButtonType type)
    {
      if (Browser != null)
      {
        Browser.GetBrowser().GetHost().SendMouseClickEvent(x, y, type, false, 1, CefEventFlags.None);
      }
    }
    public void HandleMouseUp(int x, int y, MouseButtonType type)
    {
      if (Browser != null)
      {
        Browser.GetBrowser().GetHost().SendMouseClickEvent(x, y, type, true, 1, CefEventFlags.None);
      }
    }
    public void HandleKeyEvent(KeyEvent k)
    {
      if (Browser != null)
      {
        Browser.GetBrowser().GetHost().SendKeyEvent(k);
      }
    }
  }
}

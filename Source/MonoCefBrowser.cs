using CefSharp;
using CefSharp.Internals;
using CefSharp.OffScreen;
using CefSharp.SchemeHandler;
using CefSharp.Structs;
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
      this.Paint += MonoCefBrowser_Paint;
    }

    private void MonoCefBrowser_Paint(object sender, OnPaintEventArgs e)
    {
      if (e.DirtyRect.Width == 0 || e.DirtyRect.Height == 0) { return; }
      TotalTime.Start();
      var bmp = this.ScreenshotOrNull(PopupBlending.Main);
      Texture2D texture = null;
      if (bmp != null)
      {
        texture = GetTexture(bmp, e.DirtyRect);
        RenderCount++;
        //Console.WriteLine($"{TotalTime.ElapsedMilliseconds / (double)RenderCount}");
      }
      TotalTime.Stop();
      if (texture != null)
      {
        this.NewFrame?.Invoke(this, new NewFrameEventArgs(texture));
      }
    }

    readonly GraphicsDevice Graphics;
    public event EventHandler<NewFrameEventArgs> NewFrame;

    Stopwatch TotalTime = new Stopwatch();
    int RenderCount;
    byte[] ImageData;
    int ImageDataWidth;
    int ImageDataHeight;
    Texture2D Texture;

    private Texture2D GetTexture(Bitmap bmp, Rect dirtyRect)
    {
      if (ImageData == null || bmp.Width != ImageDataWidth || bmp.Height != ImageDataHeight)
      {
        this.ImageData = new byte[bmp.Width * bmp.Height * 4];
        this.ImageDataWidth = bmp.Width;
        this.ImageDataHeight = bmp.Height;
        this.Texture = new Texture2D(Graphics, bmp.Width, bmp.Height);
      }

      unsafe
      {
        BitmapData origdata = bmp.LockBits(new Rectangle(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int absStride = Math.Abs(origdata.Stride);

        for (int i = 0; i < dirtyRect.Height; i++)
        {
          IntPtr pointer = new IntPtr(origdata.Scan0.ToInt32() + (origdata.Stride * i));
          var y = i + dirtyRect.Y;
          var x = dirtyRect.X;
          System.Runtime.InteropServices.Marshal.Copy(pointer, ImageData, x*4 + bmp.Width * 4 * y, absStride);
        }
        bmp.UnlockBits(origdata);
      }

      this.Texture.SetData(ImageData);

      return this.Texture;
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
      CefSharpSettings.ShutdownOnExit = true;
      CefSharpSettings.FocusedNodeChangedEnabled = true;

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

      //NOTE: For OSR best performance you should run with GPU disabled:
      // `--disable-gpu --disable-gpu-compositing --enable-begin-frame-scheduling`
      // (you'll loose WebGL support but gain increased FPS and reduced CPU usage).
      // http://magpcss.org/ceforum/viewtopic.php?f=6&t=13271#p27075
      //https://bitbucket.org/chromiumembedded/cef/commits/e3c1d8632eb43c1c2793d71639f3f5695696a5e8

      //NOTE: The following function will set all three params
      settings.SetOffScreenRenderingBestPerformanceArgs();
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

    public void Resize(System.Drawing.Size size)
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

    public async Task MainAsync(GraphicsDevice gd, IntPtr windowHandle, string url, object data, System.Drawing.Size size, double zoomLevel = 1.0)
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
      Browser.CreateBrowser(new WindowInfo() {
        WindowHandle = windowHandle,
        Width = size.Width,
        Height = size.Height,
        WindowlessRenderingEnabled = true
      }, browserSettings);
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

      await LoadPageAsync(Browser, url);

      //Gets a wrapper around the underlying CefBrowser instance
      var cefBrowser = Browser.GetBrowser();
      // Gets a warpper around the CefBrowserHost instance
      // You can perform a lot of low level browser operations using this interface
      var cefHost = cefBrowser.GetHost();
      cefHost.SendFocusEvent(true);

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
        Browser.GetBrowser().GetHost().Invalidate(PaintElementType.View);
      }
    }
    public void HandleMouseUp(int x, int y, MouseButtonType type)
    {
      if (Browser != null)
      {
        Browser.GetBrowser().GetHost().SendMouseClickEvent(x, y, type, true, 1, CefEventFlags.None);
        Browser.GetBrowser().GetHost().Invalidate(PaintElementType.View);
      }
    }
    public void HandleKeyEvent(KeyEvent k)
    {
      if (Browser != null)
      {
        Browser.GetBrowser().GetHost().SendKeyEvent(k);
        Browser.GetBrowser().GetHost().Invalidate(PaintElementType.View);
      }
    }
  }
}

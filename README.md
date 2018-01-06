# MonoCef

MonoCef is an example application that combines [MonoGame](http://www.monogame.net/) and [CefSharp](https://github.com/cefsharp/CefSharp)
to allow a MonoGame application to use HTML/CSS as its front-end UI. This is an
alternative to using one of the many [MonoGame UI Frameworks](http://community.monogame.net/t/what-are-you-guys-using-for-ui-looking-for-a-simple-ui-for-monogame/8313/7)
in existence. This isn't a library that you can pull directly into your project - instead, it's just an example that you can copy from.

This particular example uses the [Angular](https://angular.io/) front-end web framework, but that is an implementation detail I chose to go with, and is not
at all required in order to make use of this technique.

## Setup

1. Install Visual Studio 2015.
2. Install [node.js](https://nodejs.org/en/) version 6.11.0 or later.
3. In the `MonoCefUI` directory, from a console window, PowerShell, or [Cygwin](https://www.cygwin.com/) bash shell:
     a. Run `npm install`
     b. Run `npm run build`
4. Compile and run the `Source/MonoCef.sln` solution in Visual Studio.

## Approach

This approach uses `CefSharp.Offscreen` to create an offscreen Chromium Embedded instance.
The offscreen browser loads an `index.html` file from disk; every time the offscreen browser's bitmap changes,
the pixels are copied onto a MonoGame `Texture2D`, which is drawn during each render loop.

## Debugging

* You can debug the hosted browser using the Chrome Remote Debugger. Open a browser window to [http://localhost:8088/](http://localhost:8088/).
* If you're making frequent changes to the UI, run `npm run watch` instead of `npm run build`. This will watch your filesystem and recompile as needed.
* ...or, run `npm start` and point a web browser to (http://localhost:4200/)[http://localhost:4200/] if you want to view the UI directly in your browser instead of hosted in the MonoGame app.

## Improvements

* Performance
  * Figure out how to get dirty rect support to work, to avoid copying the entire screen every time there is a UI change.
  * Figure out how to [copy directly to video RAM](https://bitbucket.org/chromiumembedded/cef/issues/1006) from the offscreen browser (I'm not sure if this is even possible right now)
* Input
  * The `KeyboardHandler` code needs to be beefed up; in particular, it doesn't handle all keys (e.g. numpad) correctly, and it doesn't do modifier keys either.
* Data Marshalling
  * I couldn't get [RegisterAsyncJsObject](http://cefsharp.github.io/api/57.0.0/html/M_CefSharp_Wpf_ChromiumWebBrowser_RegisterAsyncJsObject.htm) to work, so
    I'm using [ExecuteScriptAsync](http://cefsharp.github.io/api/57.0.0/html/M_CefSharp_WebBrowserExtensions_ExecuteScriptAsync_1.htm)
    and [EvaluateScriptAsync](http://cefsharp.github.io/api/57.0.0/html/M_CefSharp_WebBrowserExtensions_EvaluateScriptAsync_2.htm)
    instead. This is probably less performant and a bit messier, but it works. If someone wants to try to make `RegisterAsyncJsObject` work and
    has success with that, I'd love to hear about it.

## Contact

Chris Frolik, cfrolik@gmail.com.
Feel free to send questions or suggestions.

## License

MIT
using CefSharp;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoCef
{
  class KeyboardHandler
  {
    private KeyboardState ks, ks_old = Keyboard.GetState();
    private List<KeyEvent> Queue = new List<KeyEvent>();
    public void Update()
    {
      ks = Keyboard.GetState();

      foreach (Keys key in Enum.GetValues(typeof(Keys)))
      {
        if (!ks.IsKeyDown(key) && ks_old.IsKeyDown(key))
        {
          Queue.Add(new KeyEvent() { Type = KeyEventType.KeyUp, NativeKeyCode = (int)key, WindowsKeyCode = (int)key });
          Queue.Add(new KeyEvent() { Type = KeyEventType.Char, NativeKeyCode = (int)key, WindowsKeyCode = (int)key });
        }
        else if (ks.IsKeyDown(key) && !ks_old.IsKeyDown(key))
          Queue.Add(new KeyEvent() { Type = KeyEventType.KeyDown, NativeKeyCode = (int)key, WindowsKeyCode = (int)key });
      }
      ks_old = ks;
    }

    public IEnumerable<KeyEvent> Query()
    {
      var ls = Queue;
      Queue = new List<KeyEvent>();
      return ls;
    }
  }
}

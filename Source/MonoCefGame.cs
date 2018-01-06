using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoCef
{
  /// <summary>
  /// This is the main type for your game.
  /// </summary>
  public class MonoCefGame : Game
  {
    readonly GraphicsDeviceManager graphics;
    readonly OffscreenBrowserRenderer BrowserRenderer;
    SpriteBatch SpriteBatch;

    const int WindowWidth = 1600;
    const int WindowHeight = 900;

    ShapeSettings Settings = new ShapeSettings();
    

    public MonoCefGame()
    {
      graphics = new GraphicsDeviceManager(this);
      graphics.PreferredBackBufferWidth = WindowWidth;
      graphics.PreferredBackBufferHeight = WindowHeight;
      Content.RootDirectory = "Content";
      BrowserRenderer = new OffscreenBrowserRenderer();
      BrowserRenderer.DataChanged += BrowserRenderer_DataChanged;
    }

    private void BrowserRenderer_DataChanged(object obj)
    {
      Settings = (ShapeSettings)obj;
      AddOrRemoveShapes();
    }

    /// <summary>
    /// Allows the game to perform any initialization it needs to before starting to run.
    /// This is where it can query for any required services and load any non-graphic
    /// related content.  Calling base.Initialize will enumerate through any components
    /// and initialize them as well.
    /// </summary>
    protected override void Initialize()
    {
      AsyncHelpers.RunSync(() => BrowserRenderer.MainAsync(graphics.GraphicsDevice, this.Window.Handle, 
        OffscreenBrowserRenderer.DefaultUrl,
        Settings,
        new System.Drawing.Size(graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight)));
      IsMouseVisible = true;
      base.Initialize();
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        BrowserRenderer.Dispose();
      }
      base.Dispose(disposing);
    }

    Vector2 RandomLocation()
    {
      return new Vector2(Rand.Next(graphics.PreferredBackBufferWidth), Rand.Next(graphics.PreferredBackBufferHeight));
    }
    Vector2 RandomVelocity()
    {
      return new Vector2(Rand.Next(10)+1, Rand.Next(10)+1);
    }

    Texture2D BlueSquare;
    Texture2D GreenCircle;
    Texture2D RedTriangle;
    readonly Random Rand = new Random();
    readonly List<Shape> Shapes = new List<Shape>();

    /// <summary>
    /// LoadContent will be called once per game and is the place to load
    /// all of your content.
    /// </summary>
    protected override void LoadContent()
    {
      BlueSquare = Content.Load<Texture2D>("BlueSquare");
      GreenCircle = Content.Load<Texture2D>("GreenCircle");
      RedTriangle = Content.Load<Texture2D>("RedTriangle");
      SpriteBatch = new SpriteBatch(GraphicsDevice);
      AddOrRemoveShapes();
    }

    void AddOrRemoveShapes()
    {
      AddOrRemoveShapes(BlueSquare, Settings.squares);
      AddOrRemoveShapes(GreenCircle, Settings.circles);
      AddOrRemoveShapes(RedTriangle, Settings.triangles);
    }

    void AddOrRemoveShapes(Texture2D type, int count)
    {
      if (count < 0) count = 0;
      while (Shapes.Count(s => s.Texture == type) > count)
      {
        var index = Shapes.FindLastIndex(s => s.Texture == type);
        Shapes.RemoveAt(index);
      }
      while (Shapes.Count(s => s.Texture == type) < count)
      {
        Shapes.Add(new Shape(type, RandomLocation(), RandomVelocity()));
      }
    }

    /// <summary>
    /// UnloadContent will be called once per game and is the place to unload
    /// game-specific content.
    /// </summary>
    protected override void UnloadContent()
    {
      Content.Unload();
      Content.Dispose();
    }

    MouseState LastMouseState;
    KeyboardHandler KeyHandler = new KeyboardHandler();

    /// <summary>
    /// Allows the game to run logic such as updating the world,
    /// checking for collisions, gathering input, and playing audio.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    protected override void Update(GameTime gameTime)
    {
      if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
        Exit();

      foreach (var shape in Shapes)
      {
        shape.Move(new Vector2(graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight));
      }

      var mouse = Mouse.GetState();
      if (LastMouseState == null)
        LastMouseState = mouse;
      BrowserRenderer.HandleMouseMove(mouse.Position.X, mouse.Position.Y);
      if (mouse.LeftButton != LastMouseState.LeftButton)
      {
        if (mouse.LeftButton == ButtonState.Pressed)
          BrowserRenderer.HandleMouseDown(mouse.Position.X, mouse.Position.Y, CefSharp.MouseButtonType.Left);
        else
          BrowserRenderer.HandleMouseUp(mouse.Position.X, mouse.Position.Y, CefSharp.MouseButtonType.Left);
      }

      LastMouseState = mouse;
      KeyHandler.Update();
      var keys = KeyHandler.Query();
      foreach (var key in keys)
      {
        BrowserRenderer.HandleKeyEvent(key);
      }


      BrowserRenderer.PullLatestDataIfChanged<ShapeSettings>();

      base.Update(gameTime);
    }

    /// <summary>
    /// This is called when the game should draw itself.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    protected override void Draw(GameTime gameTime)
    {
      GraphicsDevice.Clear(Color.Black);

      SpriteBatch.Begin();
      foreach (var shape in Shapes)
      {
        SpriteBatch.Draw(shape.Texture, new Vector2(shape.Location.X - shape.Texture.Width/2, shape.Location.Y - shape.Texture.Height/2), Color.White);
      }

      if (BrowserRenderer.CurrentFrame != null)
      {
        SpriteBatch.Draw(BrowserRenderer.CurrentFrame, new Vector2(0, 0), Color.White);// new Color(Color.White, 0.5f));
      }

      SpriteBatch.End();
      base.Draw(gameTime);
    }
  }

  class Shape
  {
    public Texture2D Texture;
    public Vector2 Location;
    public Vector2 Velocity;

    public Shape(Texture2D texture, Vector2 location, Vector2 velocity)
    {
      this.Texture = texture;
      this.Location = location;
      this.Velocity = velocity;
    }

    public void Move(Vector2 limit)
    {
      Location = Location + Velocity;
      if (Location.X < 0 || Location.X > limit.X)
      {
        Velocity = new Vector2(-Velocity.X, Velocity.Y);
      }
      if (Location.Y < 0 || Location.Y > limit.Y)
      {
        Velocity = new Vector2(Velocity.X, -Velocity.Y);
      }
    }
  }

  public class ShapeSettings
  {
    public int triangles = 3;
    public int squares = 3;
    public int circles = 3;
  }
}

using CamerasSample.Core.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CamerasSample.Core
{
    /// <summary>
    /// The main class for the game, responsible for managing game components, settings, 
    /// and platform-specific configurations.
    /// </summary>
    public class CamerasSampleGame : Game
    {
        // Resources for drawing.
        private GraphicsDeviceManager graphicsDeviceManager;

        /// <summary>
        /// Indicates if the game is running on a mobile platform.
        /// </summary>
        public readonly static bool IsMobile = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

        /// <summary>
        /// Indicates if the game is running on a desktop platform.
        /// </summary>
        public readonly static bool IsDesktop = OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() || OperatingSystem.IsWindows();

        #region Camera Fields
        enum CameraMode { Fixed, Tracking, FirstPerson, ThirdPerson, TopDownFixed, TopDownCentred };

        private SpriteBatch _spriteBatch;

        private SpriteFont spriteFont;

        // Set the 3D player model to draw.
        private Model myModel;

        // Set the 3D ground model so that we get a sense of movement
        private Model groundModel;

        // Set the velocity of the model, applied each frame to the model's position.
        private Vector3 modelVelocity = Vector3.Zero;

        // Set the position of the model in world space, and set the rotation.
        private Vector3 modelPosition = new Vector3(0.0f, 350.0f, 0.0f);
        private float modelRotation = 0.0f;

        // Set the position of the camera in world space, for our view matrix.
        private Vector3 cameraFixedPosition = new Vector3(0.0f, 1550.0f, 5000.0f);

        // 1st Person camera position relative to player model
        private Vector3 cameraFirstPersonPosition = new Vector3(0.0f, 50.0f, 500.0f);

        // 3rd Person camera position relative to player model
        private Vector3 cameraThirdPersonPosition = new Vector3(0.0f, 1550.0f, 5000.0f);

        // Top Down camera position relative to player model
        private Vector3 cameraTopDownPosition = new Vector3(0.0f, 25000.0f, 1.0f);

        // The aspect ratio determines how to scale 3d to 2d projection.
        private float aspectRatio;

        private SoundEffect engineSoundEffect;
        private SoundEffectInstance engineSound;

        private SoundEffect hyperspaceSoundEffect;

        // Matrices required to correctly display our scene
        private Matrix modelWorldPosition;
        private Matrix currentCameraView;
        private Matrix currentCameraProjection;

        //Distance from the camera of the near and far clipping planes
        private float nearClip = 10.0f;
        private float farClip = 100000.0f;

        // Field of view of the camera in radians (pi/4 is 45 degrees).
        private float fieldOfView = MathHelper.ToRadians(45.0f);

        private CameraMode currentCameraMode = CameraMode.Fixed;

        // Camera Physics
        private float cameraStiffness = 1800.0f;
        private float cameraDamping = 600.0f;
        private float cameraMass = 50.0f;
        private Vector3 cameraVelocity = Vector3.Zero;
        private Vector3 currentCameraPosition = Vector3.Zero;

        // Drag Co-Efficient
        float Drag = 0.97f;

        private bool cameraSpringEnabled = true;
        #endregion Camera Fields

        /// <summary>
        /// Initializes a new instance of the game. Configures platform-specific settings, 
        /// initializes services like settings and leaderboard managers, and sets up the 
        /// screen manager for screen transitions.
        /// </summary>
        public CamerasSampleGame()
        {
            graphicsDeviceManager = new GraphicsDeviceManager(this);

            // Share GraphicsDeviceManager as a service.
            Services.AddService(typeof(GraphicsDeviceManager), graphicsDeviceManager);

            Content.RootDirectory = "Content";

            // Configure screen orientations.
            graphicsDeviceManager.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
        }

        /// <summary>
        /// Initializes the game, including setting up localization and adding the 
        /// initial screens to the ScreenManager.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();

            // Load supported languages and set the default language.
            List<CultureInfo> cultures = LocalizationManager.GetSupportedCultures();
            var languages = new List<CultureInfo>();
            for (int i = 0; i < cultures.Count; i++)
            {
                languages.Add(cultures[i]);
            }

            // TODO You should load this from a settings file or similar,
            // based on what the user or operating system selected.
            var selectedLanguage = LocalizationManager.DEFAULT_CULTURE_CODE;
            LocalizationManager.SetCulture(selectedLanguage);

            aspectRatio = (float)GraphicsDevice.Viewport.Width / GraphicsDevice.Viewport.Height;
            currentCameraProjection = Matrix.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, nearClip, farClip);
        }

        /// <summary>
        /// Loads game content, such as textures and particle systems.
        /// </summary>
        protected override void LoadContent()
        {
            base.LoadContent();

            _spriteBatch = new SpriteBatch(GraphicsDevice);

            groundModel = Content.Load<Model>("Models/Ground");

            myModel = Content.Load<Model>("Models/p1_wedge");

            spriteFont = Content.Load<SpriteFont>("Fonts/Tahoma");

            engineSoundEffect = Content.Load<SoundEffect>("Audio/Engine_2");
            engineSound = engineSoundEffect.CreateInstance();
            hyperspaceSoundEffect = Content.Load<SoundEffect>("Audio/hyperspace_activate");
        }

        /// <summary>
        /// Updates the game's logic, called once per frame.
        /// </summary>
        /// <param name="gameTime">
        /// Provides a snapshot of timing values used for game updates.
        /// </param>
        protected override void Update(GameTime gameTime)
        {
            InputManager.Update();

            // Exit the game if the Back button (GamePad) or Escape key (Keyboard) is pressed.
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
                || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // Get some input.
            UpdateInput(gameTime);

            // Add velocity to the current position.
            modelPosition += modelVelocity;

            // Bleed off velocity over time.
            modelVelocity *= Drag;

            // Update the ships world position based on input
            modelWorldPosition = Matrix.CreateRotationY(modelRotation) * Matrix.CreateTranslation(modelPosition);

            switch (currentCameraMode)
            {
                case CameraMode.Fixed:
                    UpdateFixedCamera();
                    break;

                case CameraMode.Tracking:
                    UpdateTrackingCamera();
                    break;

                case CameraMode.FirstPerson:
                    UpdateFirstPersonCamera();
                    break;

                case CameraMode.ThirdPerson:
                    UpdateThirdPersonCamera((float)gameTime.ElapsedGameTime.TotalSeconds);
                    break;

                case CameraMode.TopDownFixed:
                    UpdateTopDownFixedCamera();
                    break;

                case CameraMode.TopDownCentred:
                    UpdateTopDownCenteredCamera();
                    break;
            }
            base.Update(gameTime);
        }


        protected void UpdateInput(GameTime aGameTime)
        {
            if (InputManager.IsKeyDown(Keys.Left))
            {
                // Rotate Left
                modelRotation += (float)(aGameTime.ElapsedGameTime.TotalMilliseconds * MathHelper.ToRadians(0.1f));
            }
            else if (InputManager.IsKeyDown(Keys.Right))
            {
                // Rotate Right
                modelRotation -= (float)(aGameTime.ElapsedGameTime.TotalMilliseconds * MathHelper.ToRadians(0.1f));
            }
            else
            {
                // Rotate the model using the left thumbstick, and scale it down.
                modelRotation -= InputManager.LeftThumbStick.X * 0.10f;
            }

            // Create some velocity if the right trigger is down.
            Vector3 modelVelocityAdd = Vector3.Zero;

            // Find out what direction we should be thrusting, using rotation.
            modelVelocityAdd.X = -(float)Math.Sin(modelRotation);
            modelVelocityAdd.Z = -(float)Math.Cos(modelRotation);

            // Now scale our direction by how hard the trigger is down.
            if (InputManager.IsKeyDown(Keys.Up))
            {
                modelVelocityAdd /= (float)(aGameTime.ElapsedGameTime.TotalMilliseconds * MathHelper.ToRadians(0.1f));
            }
            else
            {
                modelVelocityAdd *= InputManager.RightTriggerValue * 10;
            }

            // Finally, add this vector to our velocity.
            modelVelocity += modelVelocityAdd;

            GamePad.SetVibration(PlayerIndex.One, InputManager.RightTriggerValue,
                InputManager.RightTriggerValue);

            // Set some audio based on whether we're pressing a trigger.
            if ((InputManager.RightTriggerValue > 0) || InputManager.IsKeyDown(Keys.Up))
            {
                if (engineSound.State == SoundState.Stopped)
                {
                    engineSound = engineSoundEffect.CreateInstance();
                    engineSound.IsLooped = true;
                    engineSound.Play();
                }
                else if (engineSound.State == SoundState.Paused)
                {
                    engineSound.Resume();
                }
            }
            else
            {
                if (engineSound.State == SoundState.Playing)
                {
                    engineSound.Pause();
                }
            }

            // In case you get lost, press A to warp back to the center.
            if (InputManager.IsButtonPressed(Buttons.A) || InputManager.IsKeyPressed(Keys.Space))
            {
                modelPosition = new Vector3(0.0f, 350.0f, 0.0f);
                modelVelocity = Vector3.Zero;
                modelRotation = 0.0f;

                // Make a sound when we warp.
                hyperspaceSoundEffect.Play();
            }

            // Toggle the state of the camera.
            if (InputManager.IsButtonPressed(Buttons.LeftShoulder) || InputManager.IsKeyPressed(Keys.Tab))
            {
                currentCameraMode++;
                currentCameraMode = (CameraMode)((int)currentCameraMode % 6);
            }

            // Pressing the A button or key toggles the spring behavior on and off
            if (InputManager.IsButtonPressed(Buttons.A) || InputManager.IsKeyPressed(Keys.A))
            {
                cameraSpringEnabled = !cameraSpringEnabled;
            }
        }

        /// <summary>
        /// Draws the game's graphics, called once per frame.
        /// </summary>
        /// <param name="gameTime">
        /// Provides a snapshot of timing values used for rendering.
        /// </param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            // Ground drawn from the center of the scene
            DrawModel(groundModel, Matrix.Identity, currentCameraView, currentCameraProjection);

            // Ship model drawn from its current position / rotation
            if (currentCameraMode != CameraMode.FirstPerson) // Comment out this line if you want to see the inside of the ship :)
                DrawModel(myModel, modelWorldPosition, currentCameraView, currentCameraProjection);

            // Draw Help Text and other HUD stuff
            DrawHUD();

            base.Draw(gameTime);
        }

        void DrawModel(Model aModel, Matrix aWorld, Matrix aView, Matrix aProjection)
        {
            //Copy any parent transforms
            Matrix[] transforms = new Matrix[aModel.Bones.Count];
            aModel.CopyAbsoluteBoneTransformsTo(transforms);

            //Draw the model, a model can have multiple meshes, so loop
            foreach (ModelMesh mesh in aModel.Meshes)
            {
                //This is where the mesh orientation is set, as well as our camera and projection
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.EnableDefaultLighting();
                    effect.PreferPerPixelLighting = true;
                    effect.World = transforms[mesh.ParentBone.Index] * aWorld;
                    effect.View = aView;
                    effect.Projection = aProjection;
                }

                //Draw the mesh, will use the effects set above.
                mesh.Draw();
            }
        }

        void DrawHUD()
        {
            _spriteBatch.Begin();

            string Helptext = "Toggle Camera Modes ( " + currentCameraMode.ToString() + " ) = Tab or LeftShoulder Button\n" +
                        "Steer = Left & Right Arrow keys or Left Thumbstick\n" +
                        "Accelerate = Up Arrow key or Right Trigger\n" +
                        "Reset = A Button or Spacebar";


            // Draw the string twice to create a drop shadow, first colored black
            // and offset one pixel to the bottom right, then again in white at the
            // intended position. This makes text easier to read over the background.
            _spriteBatch.DrawString(spriteFont, Helptext, new Vector2(20, 20), Color.Black);
            _spriteBatch.DrawString(spriteFont, Helptext, new Vector2(19, 19), Color.White);

            if (currentCameraMode == CameraMode.FirstPerson)
            {
                string HudText = "Velocity :" + modelVelocity.ToString() + "\n" +
                       "Position :" + modelPosition.ToString() + "\n" +
                       "Rotation :" + modelRotation.ToString() + "\n";

                _spriteBatch.DrawString(spriteFont, HudText, new Vector2(20, 400), Color.White);
            }

            _spriteBatch.End();
        }

        void UpdateCameraView(Vector3 aCameraPosition, Vector3 aCameraTarget)
        {
            currentCameraView = Matrix.CreateLookAt(aCameraPosition, aCameraTarget, Vector3.Up);
        }

        void UpdateFixedCamera()
        {
            // Set up our world matrix, view matrix and projection matrix.
            UpdateCameraView(cameraFixedPosition, Vector3.Zero);
        }

        void UpdateTrackingCamera()
        {
            // Create a vector pointing the direction the camera is facing.  
            Vector3 transformedReference = Vector3.Transform(modelPosition, Matrix.Identity);

            // Calculate the position the camera is looking at.
            Vector3 cameraLookat = transformedReference + modelPosition;

            // Set up our world matrix, view matrix and projection matrix.
            UpdateCameraView(cameraFixedPosition, cameraLookat);
        }

        void UpdateFirstPersonCamera()
        {

            Matrix rotationMatrix = Matrix.CreateRotationY(modelRotation);

            // Create a vector pointing the direction the camera is facing.
            Vector3 transformedReference = Vector3.Transform(cameraFirstPersonPosition, rotationMatrix);

            // Calculate the position the camera is looking from.
            currentCameraPosition = transformedReference + modelPosition;

            // Set up our world matrix, view matrix and projection matrix.
            UpdateCameraView(currentCameraPosition, modelPosition);
        }

        void UpdateThirdPersonCamera(float aElapsed)
        {
            Matrix rotationMatrix = Matrix.CreateRotationY(modelRotation);

            // Create a vector pointing the direction the camera is facing.
            Vector3 transformedReference = Vector3.Transform(cameraThirdPersonPosition, rotationMatrix);

            if (cameraSpringEnabled)
            {
                // Calculate the position where we would like the camera to be looking from.
                Vector3 desiredPosition = transformedReference + modelPosition;

                // Calculate spring force            
                Vector3 stretch = currentCameraPosition - desiredPosition;
                Vector3 force = -cameraStiffness * stretch - cameraDamping * cameraVelocity;

                // Apply acceleration 
                Vector3 acceleration = force / cameraMass;
                cameraVelocity += acceleration * aElapsed;

                // Apply velocity
                currentCameraPosition += cameraVelocity * aElapsed;
            }
            else
            {
                // Calculate the position the camera is looking from.
                currentCameraPosition = transformedReference + modelPosition;
            }

            // Set up our world matrix, view matrix and projection matrix.
            UpdateCameraView(currentCameraPosition, modelPosition);
        }

        void UpdateTopDownFixedCamera()
        {
            // Set up our world matrix, view matrix and projection matrix.
            UpdateCameraView(cameraTopDownPosition, Vector3.Zero);
        }

        void UpdateTopDownCenteredCamera()
        {
            Matrix rotationMatrix = Matrix.CreateRotationY(modelRotation);

            // Create a vector pointing the direction the camera is facing.
            Vector3 transformedReference = Vector3.Transform(cameraTopDownPosition, rotationMatrix);

            // Calculate the position the camera is looking from.
            currentCameraPosition = transformedReference + modelPosition;

            // Set up our world matrix, view matrix and projection matrix.
            UpdateCameraView(currentCameraPosition, modelPosition);
        }
    }
}
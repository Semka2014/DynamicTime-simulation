﻿using SDL2;
using System.Drawing;
using System.Numerics;
using System.Reflection;

namespace SOE
{
    internal class Program
    {
        private static readonly PriorityQueue<Bit, float> Bits = new();
        private static float _currentTime = 0;
        private static float _currentStep = 0;
        private static readonly Random Rnd = new();

        // SDL Objects
        private static IntPtr _window;
        private static IntPtr _renderer;

        static void Main(string[] args)
        {
            InitializeSDL();
            InitializeBits(100);
            ProcessInteractions();
            RunSimulation();
        }

        private static void InitializeSDL()
        {
            SDL.SDL_Init(SDL.SDL_INIT_VIDEO);
            _window = SDL.SDL_CreateWindow("Simulation", SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, 800, 600, SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
            _renderer = SDL.SDL_CreateRenderer(_window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
        }

        private static void InitializeBits(int count)
        {
            for (int i = 0; i < count; i++)
            {
                int mass = Rnd.Next(1, 15);
                Bit bit = new(mass, mass * 2, new Point(Rnd.Next(0, 1000), Rnd.Next(0, 500)));

                float velocityScale = (float)Rnd.NextDouble();
                float velocityAngle = (float)Rnd.Next((int)Math.Round(2 * Math.PI * 10000)) / 10000;
                bit.AddForce(new Vector(velocityScale, velocityAngle));

                if (i == 5)
                {
                    bit.AddForce(new Vector(60, velocityAngle));
                }

                Bits.Enqueue(bit, 0);
            }
        }

        private static float CalculateForce(Bit bit1, Bit bit2, float distanceSquared, float combinedRadius)
        {
            int direction = distanceSquared > combinedRadius * combinedRadius ? -1 : 1;
            return (bit1.Mass * bit2.Mass * 5) / distanceSquared * direction;
        }

        private static void RunSimulation()
        {
            float lastIterationTime = 0;
            bool running = true;

            while (running)
            {
                while (SDL.SDL_PollEvent(out SDL.SDL_Event e) != 0)
                {
                    if (e.type == SDL.SDL_EventType.SDL_QUIT)
                    {
                        running = false;
                    }
                }

                Bits.TryDequeue(out Bit currentBit, out _currentTime);

                ProcessInteractions(currentBit);
                currentBit.Activate(_currentTime - currentBit.LastActivationTime);

                float deltaTime = 1 / currentBit.Velocity.Scale;
                Bits.Enqueue(currentBit, deltaTime + _currentTime);

                bool render = _currentStep % 500 == 0;
                Graph.AddValue(_currentTime - lastIterationTime);
                if (render) RenderScene();

                lastIterationTime = _currentTime;
                _currentStep++;
            }
        }

        private static void RenderScene()
        {
            SDL.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 255);
            SDL.SDL_RenderClear(_renderer);

            foreach (var bitItem in Bits.UnorderedItems)
            {
                Bit bit = bitItem.Element;
                DrawBit(_renderer, bit);
            }
            Graph.Draw(_renderer);

            SDL.SDL_RenderPresent(_renderer);
        }

        private static void ProcessInteractions()
        {
            foreach (var bitItem1 in Bits.UnorderedItems)
            {
                Bit bit1 = bitItem1.Element;

                ProcessInteractions(bit1);

                bit1.Activate(1);
            }
        }

        private static void ProcessInteractions(Bit currentBit)
        {
            foreach (var bitItem in Bits.UnorderedItems)
            {
                Bit otherBit = bitItem.Element;

                if (currentBit == otherBit) continue;

                float dx = currentBit.Position.x - otherBit.Position.x;
                float dy = currentBit.Position.y - otherBit.Position.y;
                float distanceSquared = dx * dx + dy * dy;

                if (distanceSquared == 0) continue;

                float combinedRadius = currentBit.Radius + otherBit.Radius;
                float forceMagnitude = CalculateForce(currentBit, otherBit, distanceSquared, combinedRadius);

                float angle = (float)Math.Atan2(dy, dx);
                currentBit.AddForce(new Vector(forceMagnitude, angle));
            }
        }

        static void DrawBit(IntPtr renderer, Bit bit)
        {
            int centerX = (int)bit.Position.x + 100; // Центр круга по X
            int centerY = (int)bit.Position.y + 100; // Центр круга по Y
            int radius = (int)bit.Radius;           // Радиус круга

            // Цвет круга (например, красный)
            SDL.SDL_SetRenderDrawColor(renderer, bit.colorR, bit.colorG, bit.colorB, 255);

            // Рисуем круг
            for (int w = 0; w < radius * 2; w++)
            {
                for (int h = 0; h < radius * 2; h++)
                {
                    int dx = radius - w; // смещение по оси X
                    int dy = radius - h; // смещение по оси Y
                    if ((dx * dx + dy * dy) <= (radius * radius)) // проверка внутри круга
                    {
                        SDL.SDL_RenderDrawPoint(renderer, centerX + dx, centerY + dy);
                    }
                }
            }

            // Рисуем направление скорости (зелёная линия)
            int velocityEndX = centerX + (int)(bit.Velocity.Scale * 5 * Math.Cos(bit.Velocity.Angle));
            int velocityEndY = centerY + (int)(bit.Velocity.Scale * 5 * Math.Sin(bit.Velocity.Angle));
            SDL.SDL_SetRenderDrawColor(renderer, 0, 255, 0, 255); // Зелёный
            SDL.SDL_RenderDrawLine(renderer, centerX, centerY, velocityEndX, velocityEndY);

            // Рисуем направление силы (синяя линия)
            int forceEndX = centerX + (int)(bit.LastForce.Scale * 5 * Math.Cos(bit.LastForce.Angle));
            int forceEndY = centerY + (int)(bit.LastForce.Scale * 5 * Math.Sin(bit.LastForce.Angle));
            SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 255, 255); // Синий
            SDL.SDL_RenderDrawLine(renderer, centerX, centerY, forceEndX, forceEndY);
        }

        struct Vector
        {
            public float Scale, Angle;

            // Конструктор
            public Vector(float scale, float angle)
            {
                this.Scale = scale;
                this.Angle = angle;
            }

            // Перегрузка оператора сложения
            public static Vector operator +(Vector v1, Vector v2)
            {
                float angle1Rad = v1.Angle;
                float angle2Rad = v2.Angle;

                float x1 = v1.Scale * (float)Math.Cos(angle1Rad);
                float y1 = v1.Scale * (float)Math.Sin(angle1Rad);
                float x2 = v2.Scale * (float)Math.Cos(angle2Rad);
                float y2 = v2.Scale * (float)Math.Sin(angle2Rad);

                float xResult = x1 + x2;
                float yResult = y1 + y2;

                float resultantScale = (float)Math.Sqrt(xResult * xResult + yResult * yResult);
                float resultantAngle = (float)Math.Atan2(yResult, xResult);

                return new Vector(resultantScale, resultantAngle);
            }

            public static Vector operator /(Vector v, float scalar)
            {
                if (scalar == 0)
                    throw new DivideByZeroException("Деление на ноль невозможно.");

                return new Vector(v.Scale / scalar, v.Angle);
            }

            public static Vector operator *(Vector v, float scalar)
            {
                return new Vector(v.Scale * scalar, v.Angle);
            }

            public override string ToString()
            {
                return $"Vector(scale: {Scale}, angle: {Angle} rad)";
            }
        }

        struct Point
        {
            public float x, y;
            public Point(float x, float y)
            {
                this.x = x;
                this.y = y;
            }
        }

        class Bit
        {
            public float Mass;
            public float Radius;
            public Point Position;
            public float LastActivationTime = 0;
            public byte colorR, colorG, colorB;

            public Vector Force, LastForce, Velocity;

            public Bit(float mass, float radius, Point position)
            {
                Mass = mass;
                Radius = radius;
                Position = position;
                colorR = (byte)Rnd.Next(255);
                colorG = (byte)Rnd.Next(255);
                colorB = (byte)Rnd.Next(255);
            }

            public void Activate(float deltaTime)
            {
                Position.x += Velocity.Scale * deltaTime * (float)Math.Cos(Velocity.Angle);
                Position.y += Velocity.Scale * deltaTime * (float)Math.Sin(Velocity.Angle);

                Velocity += Force * deltaTime / Mass;
                LastForce = Force;
                Force = new Vector(0, 0);
                LastActivationTime = _currentTime;
            }

            public void AddForce(Vector force)
            {
                Force += force;
            }
        }

        static class Graph
        {
            private static readonly List<float> values = new List<float>();
            private const int MaxValues = 1000; // Максимальное количество значений
            private const int GraphHeight = 50; // Высота графика
            private const int OffsetX = 1000; // Смещение по X (правый верхний угол)
            private const int OffsetY = 40;  // Смещение по Y

            public static void AddValue(float value)
            {
                // Добавляем новое значение в список
                values.Add(value);

                // Удаляем старые значения, если их больше MaxValues
                if (values.Count > MaxValues)
                {
                    values.RemoveAt(0);
                }
            }

            public static void Draw(IntPtr renderer)
            {
                if (values.Count < 2) return;

                // Определяем максимальное значение для нормализации
                float maxValue = values.Max();
                if (maxValue == 0) maxValue = 1; // Избегаем деления на ноль

                // Устанавливаем цвет графика (например, белый)
                SDL.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);

                // Рисуем график
                for (int i = 1; i < values.Count; i++)
                {
                    // Нормализуем значения для отображения на графике
                    float normalizedPrev = values[i - 1] / maxValue * GraphHeight;
                    float normalizedCurr = values[i] / maxValue * GraphHeight;

                    // Рассчитываем позиции точек
                    int x1 = OffsetX - (i - 1);
                    int y1 = OffsetY + GraphHeight - (int)normalizedPrev;
                    int x2 = OffsetX - i;
                    int y2 = OffsetY + GraphHeight - (int)normalizedCurr;

                    // Рисуем линию между двумя точками
                    SDL.SDL_RenderDrawLine(renderer, x1, y1, x2, y2);
                }
            }
        }
    }
}
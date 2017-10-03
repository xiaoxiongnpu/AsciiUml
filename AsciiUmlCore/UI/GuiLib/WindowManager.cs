﻿using System;
using System.Collections.Generic;
using AsciiUml.UI;
using System.Linq;
using AsciiUml.Geo;

namespace AsciiUml
{
    public class WindowManager
    {
        private readonly string title;
        private GuiComponent focus;
        public GuiComponent Focus
        {
            get => focus;
            set
            {
                if(value != null && !value.IsVisible)
                    throw new ArgumentException("Cannot focus invisible item");
                focus = value;
            }
        }

        private readonly List<GuiComponent> rootComponents = new List<GuiComponent>();
        private Canvass previousPaint { get; set; }

        public WindowManager(string title)
        {
            this.title = title;
            previousPaint = new Canvass();
        }

        private static void EnableCatchingShiftArrowPresses()
        {
            Console.TreatControlCAsInput = true;
        }

        public void AddComponent(GuiComponent c)
        {
            rootComponents.Add(c);
        }

        public void Remove(GuiComponent c)
        {
            rootComponents.RemoveAll(x => x == c);
            if (Focus == c)
                Focus = null;
        }

        public void Start()
        {
            EnableCatchingShiftArrowPresses();
            Console.SetWindowSize(90, 50);
            Console.Title = title;
            Console.Clear();

            Repaint();

            while (true)
            {
                try
                {
                    var key = Console.ReadKey(true);

                    if (IsCtrlCPressed(key))
                    {
                        return;
                    }

                    HandleKeyPress(key);

                    Repaint();
                }
                catch (Exception e)
                {
                    foreach (var component in rootComponents)
                    {
                        component.OnException(e);
                    }
                    throw;
                }
            }
        }

        public void Repaint()
        {
            var catode = MergeComponentsToOneView();

            Print(previousPaint, catode);

            previousPaint = catode;
        }

        private void HandleKeyPress(ConsoleKeyInfo key)
        {
            if (Focus != null && Focus.HandleKey(key))
                return;

            foreach (var component in rootComponents.ToArray().Where(x => x != Focus))
            {
                if (component.HandleKey(key))
                {
                    return;
                }
            }
        }

        private Canvass MergeComponentsToOneView()
        {
            var catode = new Canvass();

            // wrong but interesting strategy of delaying the painting of the focused element and its parents
            //var focusChain = FillFocusChain(Focus, new List<GuiComponent>());
            //focusChain.Reverse(); // drawing order
            //var nonFocusChain = RootComponents.Where(x => focusChain.All(f => f != x));
            //foreach (var component in nonFocusChain.Concat(focusChain))
            //{
            //    var canvas = component.Paint();
            //    Merge(catode, canvas, component.Position);
            //}

            var toPaint = new List<GuiComponent>();
            foreach (var rootComponent in rootComponents)
            {
                FillPaintChain(rootComponent, toPaint);
            }

            foreach (var component in toPaint)
            {
                var canvas = component.Paint();
                Merge(catode, canvas, component.Position);
            }

            // repaint focused to ensure it is in front
            if (Focus != null)
            {
                toPaint = FillPaintChain(Focus, new List<GuiComponent>());
                foreach (var component in toPaint)
                {
                    var canvas = component.Paint();
                    Merge(catode, canvas, component.Position);
                }
            }

            return catode;
        }

        //List<GuiComponent> FillFocusChain(GuiComponent c, List<GuiComponent> focusChain)
        //{
        //    if(c==null || c.parent == null)
        //        return focusChain;
        //    focusChain.Add(c);
        //    return FillFocusChain(c.parent, focusChain);
        //}

        List<GuiComponent> FillPaintChain(GuiComponent c, List<GuiComponent> paintchain)
        {
            if (c.IsVisible)
            {
                paintchain.Add(c);
                foreach (var child in c.Children)
                {
                    FillPaintChain(child, paintchain);
                }
            }
            return paintchain;
        }

        private static void Print(Canvass previousPaint, Canvass catode)
        {
            Console.SetCursorPosition(0, 0);

            for (int y = 0; y < previousPaint.Catode.Length - 1; y++)
            {
                for (int x = 0; x < previousPaint.Catode[y].Length - 1; x++)
                {
                    PrintPixel(previousPaint, catode, y, x);
                }
            }
        }

        private static void Merge(Canvass finalResult, Canvass newComponent, Coord canvasDelta)
        {
            for (int y = 0; y+canvasDelta.Y < State.MaxY; y++)
            {
                for (int x = 0; x+canvasDelta.X < State.MaxX; x++)
                {
                    var newpixel = newComponent.Catode[y][x];
                    if (newpixel != null)
                    {
                        if (!Pixel.Compare(finalResult.Catode[y+canvasDelta.Y][x+canvasDelta.X], newpixel))
                        {
                            finalResult.Catode[y + canvasDelta.Y][x + canvasDelta.X] = newpixel;
                        }
                    }
                }
            }
        }

        private static void PrintPixel(Canvass previousPaint, Canvass canvass, int y, int x)
        {
            var newpixel = canvass.Catode[y][x];
            var oldPixel = previousPaint.Catode[y][x];
            if (!Pixel.Compare(newpixel, oldPixel))
            {
                Console.SetCursorPosition(x, y);

                if (newpixel == null)
                {
                    Console.BackgroundColor = ConsoleColor.Black;
                    Console.Write(' ');
                }
                else
                {
                    Console.BackgroundColor = newpixel.BackGroundColor;
                    Console.ForegroundColor = newpixel.ForegroundColor;
                    Console.Write(newpixel.Char);
                }
            }
        }

        private static bool IsCtrlCPressed(ConsoleKeyInfo key)
        {
            return (key.Modifiers & ConsoleModifiers.Control) != 0 && key.KeyChar == '\u0003';
        }
    }
}
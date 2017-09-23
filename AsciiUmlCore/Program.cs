﻿using System;
using System.Collections.Generic;
using System.Linq;
using AsciiUml.Commands;
using AsciiUml.Geo;
using AsciiUml.UI;
using LanguageExt;
using Newtonsoft.Json;
using static AsciiUml.Extensions;

namespace AsciiUml
{
	class Program {
		static readonly List<ICommand> Noop = new List<ICommand>();

		static void Main(string[] args) {
			EnableCatchingShiftArrowPresses();

			Console.SetWindowSize(90, 50);
			var state = new State();
			state.TheCurser = new Cursor(new Coord(0, 0));

			//TempModelForPlayingAround(state.Model);

			state = ReadKeyboardEvalLoop(state);
			return;
		}

		private static State ReadKeyboardEvalLoop(State state)
		{
			var commandLog = new List<List<ICommand>>();

			try
			{
				while (true)
				{
					state.Canvas = PrintToScreen(state);

					var key = Console.ReadKey(true);

					if (IsCtrlCPressed(key))
					{
						return state;
					}

					var commands = ControlKeys(state, key, commandLog)
						.IfEmpty(() => ShiftKeys(state, key))
						.IfEmpty(() => HandleKeys(state, key, commandLog))
						.ToList();

					commandLog.Add(commands);

					foreach (var command in commands)
					{
						state = command.Execute(state);
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine("something unexpected happened " + e.Message + " :: " + e.StackTrace);

				PrintCommandLog(commandLog);
				throw;
			}
		}

		private static void PrintCommandLog(List<List<ICommand>> commandLog)
		{
			//var configuration = ConfigurationHelper.GetStandardConfiguration();
			//var stateprinter =
			//	new StatePrinting.Stateprinter(
			//		configuration.SetOutputFormatter(new JsonStyle(configuration)));
			//Console.WriteLine("To recreate the error do the following " + stateprinter.PrintObject(commandLog));
			//Console.WriteLine("To recreate the error do the following " + JsonConvert);
			var ser = new JsonSerializer() {
				TypeNameHandling = TypeNameHandling.Auto,
			};
			
			ser.Serialize(Console.Out, commandLog);
			Console.WriteLine("press a key");
			Console.ReadKey(true);
		}

		private static bool IsCtrlCPressed(ConsoleKeyInfo key)
		{
			return (key.Modifiers & ConsoleModifiers.Control) != 0 && key.KeyChar == '\u0003';
		}

		private static List<ICommand> HandleKeys(State state, ConsoleKeyInfo key, List<List<ICommand>> commandLog) {
			var model = state.Model;
			var selected = state.SelectedIndexInModel;
			switch (key.Key) {
				case ConsoleKey.UpArrow:
					if(state.TheCurser.Pos.Y > 0)
						return Lst(new MoveCursor(Vector.DeltaNorth), new MoveSelectedPaintable(Vector.DeltaNorth));
					break;
				case ConsoleKey.DownArrow:
					if(state.TheCurser.Pos.Y < State.MaxY-2)
						return Lst(new MoveCursor(Vector.DeltaSouth), new MoveSelectedPaintable(Vector.DeltaSouth));
					break;
				case ConsoleKey.LeftArrow:
					if (state.TheCurser.Pos.X > 0)
						return Lst(new MoveCursor(Vector.DeltaWest), new MoveSelectedPaintable(Vector.DeltaWest));
					break;
				case ConsoleKey.RightArrow:
					if(state.TheCurser.Pos.X < State.MaxX-2)
						return Lst(new MoveCursor(Vector.DeltaEast), new MoveSelectedPaintable(Vector.DeltaEast));
					break;

				case ConsoleKey.Spacebar:
					if (selected.HasValue)
						return Lst(new ClearSelection());

					var obj = GetIdOfCursorPosOrAskUserForId(state)
						.Match(x => Lst(new SelectObject(x.Item1, x.Item2)),
							() => Lst(new ClearSelection()));
					return obj;

				case ConsoleKey.S:
					return PrintIdsAndLetUserSelectObject(state)
						.Match(x => Lst(new SelectObject(x, true)), () => Noop);

				case ConsoleKey.X:
				case ConsoleKey.Delete:
					return Lst(new DeleteSelectedElement());

				case ConsoleKey.H:
					return Lst(new ShowHelpScreen());

				case ConsoleKey.B:
					return ConsoleInputColors(() => 
						CommandParser.TryReadLineWithCancel("Create box. Title: ")
						.Match(x => Lst(new CreateBox(state.TheCurser.Pos, x)), () => Noop));
					
				case ConsoleKey.C:
					Console.WriteLine("Connect from object: ");

					var cmds = Noop;
					PrintIdsAndLetUserSelectObject(state)
						.IfSome(from =>
								{
									Console.WriteLine("");
									Console.WriteLine("to object: ");
									PrintIdsAndLetUserSelectObject(state)
									.IfSome(to => { cmds.Add(new CreateLine(from, to)); });
								});
					return cmds;

				case ConsoleKey.T:
					return ConsoleInputColors(() => 
						CommandParser.TryReadLineWithCancel("Create a label. Text: ")
						.Match(x => Lst(new CreateLabel(state.TheCurser.Pos, x)), () => Noop));

				case ConsoleKey.R:
					return selected.Match(x =>
						{
							if (model[x] is Label)
								return Lst(new RotateSelectedElement(x));
							Screen.PrintErrorAndWaitKey("Only labels can be rotated");
							return Noop;
						},
						() =>
						{
							Screen.PrintErrorAndWaitKey("Nothing is selected");
							return Noop;
						});

				case ConsoleKey.Enter:
					var cmd = CommandParser.TryReadLineWithCancel("Enter command: ");
					return cmd.Match(x => {
						switch (x) {
							case "dump":
								PrintCommandLog(commandLog);
								break;
							case "database":
								return Lst(new CreateDatabase(state.TheCurser.Pos));
						}
						return Noop;
					}, () => Noop);

			}
			return Noop;
		}

		public static List<ICommand> SelectTemporarily(State state, Func<State, List<ICommand>> code)
		{
			return state.SelectedId.Match(
				_ => code(state),
				() => state.Canvas.GetOccupants(state.TheCurser.Pos).Match(
					x=>Lst(new SelectObject(x, false)).Append(code(state)).Append(Lst(new ClearSelection())).ToList(),
					() => Noop));
		}

		private static List<ICommand> ControlKeys(State state, ConsoleKeyInfo key, List<List<ICommand>> commandLog) {
			if ((key.Modifiers & ConsoleModifiers.Control) == 0)
				return Noop;

			switch (key.Key) {
					case ConsoleKey.UpArrow:
					case ConsoleKey.DownArrow:
					case ConsoleKey.LeftArrow:
					case ConsoleKey.RightArrow:
						return ControlCursor(state, key);

				case ConsoleKey.S:
					PrintCommandLog(commandLog);
					return Noop;

				default:
					return Noop;
			}
		}

		private static List<ICommand> ControlCursor(State state, ConsoleKeyInfo key) {
			return SelectTemporarily(state, x => {
				switch (key.Key) {
					case ConsoleKey.UpArrow:
						return Lst(new ResizeSelectedBox(Vector.DeltaNorth));
					case ConsoleKey.DownArrow:
						return Lst(new ResizeSelectedBox(Vector.DeltaSouth));
					case ConsoleKey.LeftArrow:
						return Lst(new ResizeSelectedBox(Vector.DeltaWest));
					case ConsoleKey.RightArrow:
						return Lst(new ResizeSelectedBox(Vector.DeltaEast));
					default:
						return Noop;
				}
			});
		}

		private static List<ICommand> ShiftKeys(State state, ConsoleKeyInfo key) {
			if ((key.Modifiers & ConsoleModifiers.Shift) == 0)
				return Noop;

			return SelectTemporarily(state, x => {
				switch (key.Key)
				{
					case ConsoleKey.UpArrow:
						return Lst(new MoveCursor(Vector.DeltaNorth), new MoveSelectedPaintable(Vector.DeltaNorth));
					case ConsoleKey.DownArrow:
						return Lst(new MoveCursor(Vector.DeltaNorth), new MoveSelectedPaintable(Vector.DeltaSouth));
					case ConsoleKey.LeftArrow:
						return Lst(new MoveCursor(Vector.DeltaNorth), new MoveSelectedPaintable(Vector.DeltaWest));
					case ConsoleKey.RightArrow:
						return Lst(new MoveCursor(Vector.DeltaNorth), new MoveSelectedPaintable(Vector.DeltaEast));
				}
				return Noop;
			});
		}

		private static void EnableCatchingShiftArrowPresses() {
			Console.TreatControlCAsInput = true;
		}

		private static Canvass PrintToScreen(State state) {
			Console.Clear();
			PaintTopMenu(state);

			var canvass = PaintServiceCore.Paint(state);

			Console.WriteLine(canvass.ToString());
			PrintCursor(state.TheCurser, canvass);

			return canvass;
		}

		private static void PaintTopMenu(State state) {
			Screen.SetConsoleGetInputColors();
			Console.WriteLine(
				$"AsciiUml v0.1 Selected: {state.SelectedId?.ToString() ?? "None"}. ({state.TheCurser}) Press \'h\' for help");
			Screen.SetConsoleStandardColor();
		}

		private static void PrintCursor(Cursor curser, Canvass canvass) {
			var top = Console.CursorTop;
			var left = Console.CursorLeft;
			Screen.SetConsoleSelectColor();
			var x = curser.X;
			var y = curser.Y;
			const int TopMenuSize = 1;
			Console.SetCursorPosition(x, y + TopMenuSize);
			Console.Write(canvass.GetCell(curser.Pos));
			Screen.SetConsoleStandardColor();
			Console.SetCursorPosition(left, top);
		}

		private static void TempModelForPlayingAround(List<IPaintable<object>> model) {
			model.Add(new Box(new Coord(0, 0), "Foo\nMiddleware\nMW1"));
			//model.Add(new Box() {Y = 14, Text = "goo\nand\nbazooka"});
			model.Add(new Box(new Coord(19, 27), "foo\nServer\nbazooka"));
			model.Add(new Box(new Coord(13, 20), "goo\nWeb\nServer"));
			model.Add(new Line() {FromId = 0, ToId = 1});
			model.Add(new Label(new Coord(5, 5), "Server\nClient\nAAA"));
		}

		private static T ConsoleInputColors<T>(Func<T> code) {
			Screen.SetConsoleGetInputColors();
			var res = code();
			Screen.SetConsoleStandardColor();
			return res;
		}

		private static Option<Tuple<int, bool>> GetIdOfCursorPosOrAskUserForId(State state) {
			return state.Canvas.GetOccupants(state.TheCurser.Pos).ToOption()
				.Match(x => Tuple.Create(x, false), 
					() => PrintIdsAndLetUserSelectObject(state).Select(x => Tuple.Create(x, true)));
		}

		private static Option<int> PrintIdsAndLetUserSelectObject(State state) {
			var cursorTop = Console.CursorTop;
			Screen.SetConsoleGetInputColors();
			PrintIdsOfModel(state.Model);
			Console.SetCursorPosition(0, cursorTop);

			var res = GetISelectableElement(state.Model);

			Screen.SetConsoleStandardColor();

			return res;
		}

		private static Option<int> GetISelectableElement(List<IPaintable<object>> model) {
			return CommandParser.ReadInt(model.MinMax(x => x.Id), "Select object: ")
				.Bind(x => {
					if (model.SingleOrDefault(b => b.Id == x) is ISelectable)
						return x;
					Screen.PrintErrorAndWaitKey("Not a selectable object");
					return GetISelectableElement(model);
				});
		}

		private static void PrintIdsOfModel(List<IPaintable<object>> model) {
			foreach (var selectable in model.OfType<ISelectable>()) {
				Console.SetCursorPosition(selectable.Pos.X, selectable.Pos.Y + 1);
				Console.Write(selectable.Id);
			}
		}
	}
}
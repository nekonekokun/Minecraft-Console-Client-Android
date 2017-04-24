using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

using MinecraftClient;
using Android.Views.InputMethods;

namespace Android
{
    [Activity(Label = "MinecraftClient", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        int count = 1;
        public static bool basicIO = true;
        public static MainActivity instance = null;
        public ScrollView scrollview = null;
        public TextView textview = null;
        public EditText edittext = null;
        public Button enter_button = null, reset_button = null;
        public static readonly object reset_lock = new object();
        private List<string> text_buf = new List<string>();
        private string last_input = "";
        //private int max_len_show_buf = 10
        static private bool waiting_input = false;

        private static IAutoComplete autocomplete_engine;
        private static LinkedList<string> previous = new LinkedList<string>();
        private static readonly object io_lock = new object();
        private static bool reading = false;
        private static string buffer = "";
        private static string buffer2 = "";
        private static bool resetting = false;
        private Program program = null;
        /// <summary>
        /// Reset the IO mechanism and clear all buffers
        /// </summary>

        public static void Reset()
        {
            lock (io_lock)
            {
                if (reading)
                {
                    ClearLineAndBuffer();
                    reading = false;
                    Console.Write("\b \b");
                }
            }
        }

        /// <summary>
        /// Read a password from the standard input
        /// </summary>

        public static string ReadPassword()
        {
            waiting_input = !resetting;
            instance.RunOnUiThread(delegate
            {
                instance.edittext.InputType = Text.InputTypes.ClassText | 
                Text.InputTypes.TextVariationPassword;
            });
            while (waiting_input)
            {
                Thread.Sleep(300);
            }
            instance.RunOnUiThread(delegate
            {
                instance.edittext.InputType = Text.InputTypes.ClassText | 
                Text.InputTypes.TextVariationVisiblePassword;
            });
            return instance.last_input;
        }

        /// <summary>
        /// Read a line from the standard input
        /// </summary>

        public static string ReadLine()
        {
            instance.RunOnUiThread(delegate
            {
                instance.edittext.InputType = Text.InputTypes.ClassText | Text.InputTypes.TextVariationNormal;
            });
            waiting_input = !resetting;
            while (waiting_input)
            {
                Thread.Sleep(300);
            }
            return instance.last_input;
        }

        /// <summary>
        /// Replace sec. by color tags.
        /// </summary>
        public static string ColorToTag(string str)
        {
            Console.WriteLine("ColorToTag: " + str);
            string rtn = "";
            string[] subs = str.Split(new char[] { '§' });
            if (subs.Length <= 1)
                return str;
            else
            {
                for (int i = 1; i < subs.Length; i++)
                {
                    if (subs[i].Length > 0)
                    {
                        string color_name = "white";
                        switch (subs[i][0])
                        {
                            case '0': color_name = "gray"; break; //Should be Black but Black is non-readable on a black background
                            case '1': color_name = "darkblue"; break;
                            case '2': color_name = "darkgreen"; break;
                            case '3': color_name = "darkcyan"; break;
                            case '4': Console.ForegroundColor = ConsoleColor.DarkRed; break;
                            case '5': Console.ForegroundColor = ConsoleColor.DarkMagenta; break;
                            case '6': Console.ForegroundColor = ConsoleColor.DarkYellow; break;
                            case '7': Console.ForegroundColor = ConsoleColor.Gray; break;
                            case '8': Console.ForegroundColor = ConsoleColor.DarkGray; break;
                            case '9': Console.ForegroundColor = ConsoleColor.Blue; break;
                            case 'a': Console.ForegroundColor = ConsoleColor.Green; break;
                            case 'b': Console.ForegroundColor = ConsoleColor.Cyan; break;
                            case 'c': Console.ForegroundColor = ConsoleColor.Red; break;
                            case 'd': Console.ForegroundColor = ConsoleColor.Magenta; break;
                            case 'e': Console.ForegroundColor = ConsoleColor.Yellow; break;
                            case 'f': Console.ForegroundColor = ConsoleColor.White; break;
                            case 'r': Console.ForegroundColor = ConsoleColor.Gray; break;
                        }
                        rtn += "<font color=\"" + color_name + "\">" + subs[i].Substring(1) + "</font>";
                    }
                }
                return rtn;
            }
        }

        /// <summary>
        /// Write a string to the standard output, without newline character
        /// </summary>
        public static void Write(string text)
        {
            {
                lock (io_lock)
                {
                    Console.Write(text);
                    text = text.Replace("<", "&lt");
                    text = text.Replace(">", "&gt");
                    instance.text_buf.Add(text);
                    string lines = "";
                    foreach (string str in instance.text_buf)
                    {
                        //coloring
                        string color_str = ColorToTag(str);// + "\n";
                        lines += color_str + "<br>\n";

                        //lines += str;// + "\n";
                    }
                    //lines.Replace(System.Environment.NewLine, "<br>\n"); //html \n

                    instance.RunOnUiThread(delegate
                    {
                        Text.ISpanned chars = Text.Html.FromHtml(lines);
                        //Java.Lang.ICharSequence chars = Text.Html.FromHtml(lines);
                        instance.textview.SetText(chars, TextView.BufferType.Spannable);
                        instance.scrollview.SmoothScrollTo(0, instance.scrollview.Height);
                    });
                }
            }
        }

        /// <summary>
        /// Write a string to the standard output with a trailing newline
        /// </summary>

        public static void WriteLine(string line)
        {
            Write(line + '\n');
        }

        /// <summary>
        /// Write a single character to the standard output
        /// </summary>

        public static void Write(char c)
        {
            Write("" + c);
        }

        /// <summary>
        /// Write a Minecraft-Formatted string to the standard output, using §c color codes
        /// </summary>
        /// <param name="str">String to write</param>
        /// <param name="acceptnewlines">If false, space are printed instead of newlines</param>

        public static void WriteLineFormatted(string str, bool acceptnewlines = true)
        {
            MainActivity.Write(str + '\n');
            /*
            if (basicIO) { WriteLine(str); return; }
            if (!String.IsNullOrEmpty(str))
            {
                if (Settings.chatTimeStamps)
                {
                    int hour = DateTime.Now.Hour, minute = DateTime.Now.Minute, second = DateTime.Now.Second;
                    MainActivity.Write(String.Format("{0}:{1}:{2} ", hour.ToString("00"), minute.ToString("00"), second.ToString("00")));
                }
                if (!acceptnewlines) { str = str.Replace('\n', ' '); }
                if (MainActivity.basicIO) { MainActivity.WriteLine(str); return; }
                string[] subs = str.Split(new char[] { '§' });
                if (subs[0].Length > 0) { MainActivity.Write(subs[0]); }
                for (int i = 1; i < subs.Length; i++)
                {
                    if (subs[i].Length > 0)
                    {
                        switch (subs[i][0])
                        {
                            case '0': Console.ForegroundColor = ConsoleColor.Gray; break; //Should be Black but Black is non-readable on a black background
                            case '1': Console.ForegroundColor = ConsoleColor.DarkBlue; break;
                            case '2': Console.ForegroundColor = ConsoleColor.DarkGreen; break;
                            case '3': Console.ForegroundColor = ConsoleColor.DarkCyan; break;
                            case '4': Console.ForegroundColor = ConsoleColor.DarkRed; break;
                            case '5': Console.ForegroundColor = ConsoleColor.DarkMagenta; break;
                            case '6': Console.ForegroundColor = ConsoleColor.DarkYellow; break;
                            case '7': Console.ForegroundColor = ConsoleColor.Gray; break;
                            case '8': Console.ForegroundColor = ConsoleColor.DarkGray; break;
                            case '9': Console.ForegroundColor = ConsoleColor.Blue; break;
                            case 'a': Console.ForegroundColor = ConsoleColor.Green; break;
                            case 'b': Console.ForegroundColor = ConsoleColor.Cyan; break;
                            case 'c': Console.ForegroundColor = ConsoleColor.Red; break;
                            case 'd': Console.ForegroundColor = ConsoleColor.Magenta; break;
                            case 'e': Console.ForegroundColor = ConsoleColor.Yellow; break;
                            case 'f': Console.ForegroundColor = ConsoleColor.White; break;
                            case 'r': Console.ForegroundColor = ConsoleColor.Gray; break;
                        }

                        if (subs[i].Length > 1)
                        {
                            MainActivity.Write(subs[i].Substring(1, subs[i].Length - 1));
                        }
                    }
                }
                Console.ForegroundColor = ConsoleColor.Gray;
                MainActivity.Write('\n');
            }*/
        }

        /// <summary>
        /// Write a Minecraft Console Client Log line
        /// </summary>
        /// <param name="text">Text of the log line</param>

        public static void WriteLogLine(string text)
        {
            WriteLineFormatted("§8[MCC] " + text);
        }

        #region Subfunctions
        private static void ClearLineAndBuffer()
        {
            while (buffer2.Length > 0) { GoRight(); }
            while (buffer.Length > 0) { RemoveOneChar(); }
        }
        private static void RemoveOneChar()
        {
            if (buffer.Length > 0)
            {
                try
                {
                    if (Console.CursorLeft == 0)
                    {
                        Console.CursorLeft = Console.BufferWidth - 1;
                        if (Console.CursorTop > 0)
                            Console.CursorTop--;
                        Console.Write(' ');
                        Console.CursorLeft = Console.BufferWidth - 1;
                        if (Console.CursorTop > 0)
                            Console.CursorTop--;
                    }
                    else Console.Write("\b \b");
                }
                catch (ArgumentOutOfRangeException) { /* Console was resized!? */ }
                buffer = buffer.Substring(0, buffer.Length - 1);

                if (buffer2.Length > 0)
                {
                    Console.Write(buffer2 + " \b");
                    for (int i = 0; i < buffer2.Length; i++) { GoBack(); }
                }
            }
        }
        private static void GoBack()
        {
            try
            {
                if (Console.CursorLeft == 0)
                {
                    Console.CursorLeft = Console.BufferWidth - 1;
                    if (Console.CursorTop > 0)
                        Console.CursorTop--;
                }
                else Console.Write('\b');
            }
            catch (ArgumentOutOfRangeException) { /* Console was resized!? */ }
        }
        private static void GoLeft()
        {
            if (buffer.Length > 0)
            {
                buffer2 = "" + buffer[buffer.Length - 1] + buffer2;
                buffer = buffer.Substring(0, buffer.Length - 1);
                Console.Write('\b');
            }
        }
        private static void GoRight()
        {
            if (buffer2.Length > 0)
            {
                buffer = buffer + buffer2[0];
                Console.Write(buffer2[0]);
                buffer2 = buffer2.Substring(1);
            }
        }
        private static void AddChar(char c)
        {
            Console.Write(c);
            buffer += c;
            Console.Write(buffer2);
            for (int i = 0; i < buffer2.Length; i++) { GoBack(); }
        }
        #endregion

        #region Clipboard management
        private static string ReadClipboard()
        {
            string clipdata = "";
            Thread staThread = new Thread(new ThreadStart(
                delegate
                {
                    try
                    {
                        ClipboardManager cm = (ClipboardManager)MainActivity.instance.GetSystemService(Context.ClipboardService);

                        ClipData cd = cm.PrimaryClip;
                        if (cd != null)
                        {
                            ClipData.Item item = cd.GetItemAt(0);
                            clipdata = item.Text;
                        }
                    }
                    catch { }
                }
            ));
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();
            return clipdata;
        }
        #endregion

        #region AutoComplete API
        /// <summary>
        /// Set an auto-completion engine for TAB autocompletion
        /// </summary>
        /// <param name="engine">Engine implementing the IAutoComplete interface</param>

        public static void SetAutoCompleteEngine(IAutoComplete engine)
        {
            autocomplete_engine = engine;
        }

       

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
#pragma warning disable CS0436 // 型がインポートされた型と競合しています
            SetContentView(Resource.Layout.Main);
            FindViewById<ScrollView>(Resource.Id.scrollView2);
            FindViewById<ScrollView>(Resource.Id.scrollView2);
            scrollview = FindViewById<ScrollView>(Resource.Id.scrollView2);

            edittext = FindViewById<EditText>(Resource.Id.editText2);
            edittext.EditorAction += delegate (object obj, TextView.EditorActionEventArgs e)
            {
                if (e.ActionId == ImeAction.Go || e.ActionId == ImeAction.Done || e.ActionId == ImeAction.Next)
                {
                    enter_button.PerformClick();
                }
            };

            textview = FindViewById<TextView>(Resource.Id.textview2);
            textview.SetBackgroundColor(Android.Graphics.Color.Black);
            enter_button = FindViewById<Button>(Resource.Id.enter2);
            enter_button.Click += delegate
            {
                last_input = edittext.Text;
                waiting_input = false;
                edittext.Text = "";
            };

            reset_button = FindViewById<Button>(Resource.Id.reset2);
            reset_button.Click += delegate
            {
                resetting = true;
                Program.Disconnect();
                lock (io_lock)
                {
                    last_input = "";
                    instance.text_buf.Clear();
                }
                Program.canceled = true;
                waiting_input = false;
                program.Cancel(true);
                Thread.Sleep(700);
                //Program.Restart();

                lock (reset_lock)
                {
                    Settings.ServerIP = "";
                    Settings.Password = "";
                    Settings.Login = "";

                    program = new Program();
                    Program.canceled = false;
                }
                resetting = false;
                lock (io_lock)
                {
                    last_input = "";
                    instance.text_buf.Clear();
                }
                program.Execute();
            };

            //singleton instance
            instance = this;

            program = new Program();
            program.Execute();
        }

        #endregion
    }

    /// <summary>
    /// Interface for TAB autocompletion
    /// Allows to use any object which has an AutoComplete() method using the IAutocomplete interface
    /// </summary>

    public interface IAutoComplete
    {
        IEnumerable<string> AutoComplete(string BehindCursor);
    }
}


﻿using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using static MiniTerm.NativeApi;

namespace MiniTerm
{
    /// <summary>
    /// The UI of the terminal. It's just a normal console window, but we're managing the input/output.
    /// In a "real" project this could be some other UI.
    /// </summary>
    static class Terminal
    {
        private const string CtrlC_Command = "\x3";
        public static bool canInput { set; get; } = true;
        public static void EnableVirtualTerminalSequenceProcessing()
        {
            var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
            if (!GetConsoleMode(iStdOut, out uint outConsoleMode))
            {
                throw new InvalidOperationException("Could not get console mode");
            }

            outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
            if (!SetConsoleMode(iStdOut, outConsoleMode))
            {
                throw new InvalidOperationException("Could not enable virtual terminal processing");
            }
        }

        public static void CopyInputToPipe(SafeFileHandle inputWriteSide)
        {
            using (var writer = new StreamWriter(new FileStream(inputWriteSide, FileAccess.Write)))
            {
                ForwardCtrlC(writer);
                writer.AutoFlush = true;
                /*writer.WriteLine(@"cd \");*/

                while (true)
                {   
                    string input = Console.ReadLine();
                    if (input == "exit")
                    {
                        break;
                    }

                    writer.WriteLine(input);
                    
                }
            }
        }
        private static void ForwardCtrlC(StreamWriter writer)
        {
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                writer.Write(CtrlC_Command);
            };
        }

        public static void CopyPipeToOutput(SafeFileHandle outputReadSide)
        {
            var consoleOut = GetStdHandle(STD_OUTPUT_HANDLE);
            const int bufferSize = 512;
            byte[] buffer = new byte[bufferSize];

            bool read;
            uint lpNumberOfBytesRead;
            do
            {
                /*Debug.WriteLine("send control C");*/
                read = ReadFile(outputReadSide, buffer, bufferSize, out lpNumberOfBytesRead, IntPtr.Zero);
                canInput = false;
                int nb = ((int)lpNumberOfBytesRead);
                WriteFile(consoleOut, buffer, lpNumberOfBytesRead, out uint lpNumberOfBytesWritten, IntPtr.Zero);
                if (buffer[nb - 1] == '>')
                    canInput = true;

            } while (read && lpNumberOfBytesRead > 0);

            consoleOut.Dispose();
            // the above could be replaced by something like the following, where terminalOutput is from Console.OpenStandardOutput()
            // but the original C++ version said:
            //  > Note: Write to the Console using WriteFile(hConsole...), not printf()/puts() to
            //  > prevent partially-read VT sequences from corrupting output
            //using (var pseudoConsoleOutput = new FileStream(outputReadSide, FileAccess.Read))
            //{
            //    pseudoConsoleOutput.CopyTo(terminalOutput);
            //}
        }
    }
}

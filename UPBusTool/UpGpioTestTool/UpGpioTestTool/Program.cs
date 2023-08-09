﻿// Copyright (c) AAEON. All rights reserved.
//
using System;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using Windows.Devices.Gpio;

namespace UpGpioTestTool
{
    class Program
    {
        static string Usage =
        "UpGpioTestTool: Command line GPIO testing utility\n" +
        "commands:\n" +
        "\n" +
        "  list         List the available pins on the default GPIO controller.\n" +
        "  pin %s       select pin to control\n" +
        "  status       status of select pin(0 is output/1 is input)\n" +
        "  input        set select pin as input\n" +
        "  output       set select pin as output\n" +
        "  high         set select pin to high\n" +
        "  low          set select pin to low\n" +
        "  read         read select pin value(0 is low/1 is high)\n" +
        "  pollout      set select pin polling output hi/lo in 1ms interval\n" +
        "  irq          display interrupt event from select pin\n" +
        "  help         show commands\n" +
        "  Example:     %s> <commands>\n" +
        "  %s>pin 7     \n" +
        "  7>output     \n" +
        "  7>high     \n" +
       "\n";

        //interrupt testing variable 
        public struct GpioState
        {
            public int tt { get; set; } //tick time
            public GpioPinValue pv { get; set; }
            public GpioPinEdge edge { get; set; }
        }
        static GpioState gs = new GpioState();
        static List<GpioState> lgs = new List<GpioState>();
        static int rising, falling;

        static void Main(string[] args)
        {
            try
            {
                UpBridge.Up upb = new UpBridge.Up();

                Console.WriteLine(upb.BoardGetManufacture() + "\n"
                            + "Board Name:  " + upb.BoardGetName() + "\n"
                            + "BIOS Ver:    " + upb.BoardGetBIOSVersion() + "\n"
                            + "Firmware Ver:" + upb.BoardGetFirmwareVersion().ToString("X") + "\n");
            }
            catch (InvalidOperationException ie)
            {
                Console.WriteLine(ie.Message);
            }
            Console.WriteLine("Up UWP console GPIO test:");

            if (GpioController.GetDefault().PinCount > 0)
            {
                GpioPin gpioPin=null;
                int selpin = -1;
                while (true)
                {
                    string input;
                    if (selpin == -1)
                    {
                        Console.WriteLine("please select pin to control(pin %s)");
                    }
                    Console.Write(selpin.ToString() + ">");
                    input = Console.ReadLine();
                    string[] inArgs = input.Split(' ');
                    if (inArgs[0] == "pin")
                    {
                        if (inArgs.Length == 2)
                        {
                            selpin = int.Parse(inArgs[1]);
                            try
                            {
                                gpioPin = GpioController.GetDefault().OpenPin(selpin);
                            }catch(InvalidOperationException ie)
                            {
                                Console.WriteLine(ie.Message);
                                selpin = -1;
                            }
                        }
                        continue;
                    }

                    switch (input)
                    {
                        case "pollout":
                            Console.WriteLine("polling output in pin {0}...", selpin);
                            gpioPin.SetDriveMode(GpioPinDriveMode.Output);
                            gpioPin.Write(GpioPinValue.High);
                            int s = Environment.TickCount;
                            for (int i=0;i<100000;i++)
                            {
                                if(i%2==0)
                                    gpioPin.Write(GpioPinValue.Low);
                                else
                                    gpioPin.Write(GpioPinValue.High);
                                sleep(1);
                            }
                            Console.WriteLine("polling output...100000 times completed!!, total time:{0} s", (Environment.TickCount-s)/1000);
                            break;
                        case "irq":
                            Timer t;
                            Console.WriteLine("irq testing...");
                            gpioPin.SetDriveMode(GpioPinDriveMode.Input);
                            gpioPin.ValueChanged += UpGpioValueChanged;
                            t = new Timer(new TimerCallback(UpGpioIrqProc));
                            t.Change(1000, 1000); //1sec interval to count 
                            Console.ReadLine();
                            gpioPin.ValueChanged -= UpGpioValueChanged;
                            t.Dispose();
                            break;
                        case "status":
                            Console.WriteLine(gpioPin.GetDriveMode().ToString());
                            break;
                        case "input":
                            gpioPin.SetDriveMode(GpioPinDriveMode.Input);
                            Console.WriteLine(gpioPin.GetDriveMode().ToString());
                            break;
                        case "output":
                            gpioPin.SetDriveMode(GpioPinDriveMode.Output);
                            Console.WriteLine(gpioPin.GetDriveMode().ToString());
                            break;
                        case "high":
                            gpioPin.Write(GpioPinValue.High);
                            Console.WriteLine(gpioPin.Read().ToString());
                            break;
                        case "low":
                            gpioPin.Write(GpioPinValue.Low);
                            Console.WriteLine(gpioPin.Read().ToString());
                            break;
                        case "read":
                            Console.WriteLine(gpioPin.Read().ToString());
                            break;
                        case "list":
                            Console.WriteLine("Available Pins:"+GpioController.GetDefault().PinCount.ToString() + " (start from 0)");
                            break;
                        case "help":
                        default:
                            Console.WriteLine(Usage);
                            break;

                    }
                }
            }
            else
            {
                Console.WriteLine("No available GPIO pins!");
                Console.WriteLine("Press any key to exit...");
                Console.ReadLine();
            }
        }

        static Stopwatch sw = Stopwatch.StartNew();
        private static void sleep(int ms)
        {
            sw.Start();
            while (true)
            {
                if (sw.Elapsed.TotalMilliseconds >= ms)
                    break;
                Thread.Sleep(0);
            }
            sw.Stop();
            sw.Reset();
        }
        private static void UpGpioIrqProc(object state)
        {
            int i, j;
            for(i = 0;i < lgs.Count; i++)
            {
                if (lgs[i].edge == GpioPinEdge.FallingEdge)
                    falling++;
                if (lgs[i].edge == GpioPinEdge.RisingEdge)
                    rising++;
            }
            for (j = 0; j < i; j++)
                lgs.RemoveAt(0);

            Console.Write("\rRising: {0}, falling: {1}", rising, falling);
        }

        private static void UpGpioValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            gs.edge = args.Edge;
            gs.tt = Environment.TickCount; //ms
            lgs.Add(gs);
        }
    }
}

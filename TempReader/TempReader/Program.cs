using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using System.Collections;


namespace TempReader
{
    public class Program
    {
        public static void Main()
        {
            // write your code here

            /*
             LCD|Netduino
               1|Vcc 3V3
               2|GND
               3|ChipSelect (Pin 10)
               4|Reset(Pin 7)
               5|Data/Command (Pin 8)
               6|MOSI (Pin 11) - input
               7|SCLK (Pin 13) - clock
               8|Backlight (Pin 9 PWM)
            */

            Nokia_5110 Lcd = new Nokia_5110(true, Pins.GPIO_PIN_D10, Pins.GPIO_PIN_D9, Pins.GPIO_PIN_D7, Pins.GPIO_PIN_D8);
            Lcd.BacklightBrightness = 100;

            OutputPort oneWirePort = new OutputPort(Pins.GPIO_PIN_D0, false);
            SecretLabs.NETMF.Hardware.AnalogInput input = new SecretLabs.NETMF.Hardware.AnalogInput(Pins.GPIO_PIN_A0);
            input.SetRange(0, 100);

            int counter = 0;
            int brightnessLevel = 0;

            while (true)
            {
                try
                {
                    OneWire wire = new OneWire(oneWirePort);
                    ArrayList devices = wire.FindAllDevices();
                    DallasOneWireDevices collection = new DallasOneWireDevices();

                    foreach (byte[] device in devices)
                        collection.AddDevice(device);

                    foreach (OneWireTemperatureProbe probe in collection)
                    {
                        DeviceDS18B20 ds18b20 = probe as DeviceDS18B20;
                        if (ds18b20 != null)
                            ds18b20.SetResolution(10, wire);
                    }

                    collection.SendCommandToAll(OneWireDs18b20Reference.CmdConvert, 100, wire);

                    foreach (OneWireTemperatureProbe probe in collection)
                    {
                        brightnessLevel = (int)input.Read();
                        probe.ReadScratchPad(wire);
                        Lcd.Clear();
                        Lcd.DrawString(0, 0, "Temp: " + probe.LastTemperature.ToString(), true);
                        Lcd.DrawString(0, 2, "Brightness: " + brightnessLevel, true);
                        Lcd.DrawString(0, 5, "Count: " + counter++, true);
                        Lcd.BacklightBrightness = (uint)brightnessLevel;
                        Lcd.Refresh();
                    }
                }
                catch (Exception e)
                {

                }
            }
        }
    }
}

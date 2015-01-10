using System;
using Microsoft.SPOT;
using System.Threading;

/*
 * Copyright 2013 Marcus Jansson (http://www.darkgrapix.com)
 *
 * Licensed under the Microsoft Public License (MS-PL);
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://opensource.org/licenses/MS-PL
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace TempReader
{
    public class OneWireRomCommand
    {
        /// <summary>
        /// Searches for attached devices.
        /// </summary>
        public const byte SearchROM = 0xF0;
        /// <summary>
        /// Only usable when there is one device attached to the bus. Will return the 64-bit rom device id
        /// </summary>
        public const byte ReadROM = 0x33;
        /// <summary>
        /// Instructs bus to adress a specific device. Must be followed by 64-bit device id.
        /// </summary>
        public const byte MatchROM = 0x55;
        /// <summary>
        /// Used when you wish to adress all attached devices at the same time
        /// </summary>
        /// <remarks>
        /// Usable for example when you wish to send a convert command to all attached temperature probes.
        /// </remarks>
        public const byte SkipROM = 0xCC;
    }

    /// <summary>
    /// Reference commands and variables for DS18B20 (and to some extent DS18S20)
    /// </summary>
    public class OneWireDs18b20Reference
    {
        public const byte CmdConvert = 0x44;
        public const byte CmdWriteScratchPad = 0x4e;
        public const byte CmdReadScratchPad = 0xbe;
        public const byte CmdCopyScratchPad = 0x48;
        public const byte CmdRecall = 0xb8;
        public const byte CmdReadPowerSupply = 0xb4;

        public const byte Resolution9Bit = 0x1f;
        public const byte Resolution10Bit = 0x3f;
        public const byte Resolution11Bit = 0x5f;
        public const byte Resolution12Bit = 0x7f;
    }

    /// <summary>
    /// The family of 1-Wire Device
    /// </summary>
    public enum OneWireFamily : byte
    {
        DS18S20 = 0x10,
        DS18B20 = 0x28
    }

    
    /// <summary>
    /// Container for detected devices on the 1-Wire bus
    /// </summary>
    public class DallasOneWireDevices : System.Collections.ArrayList
    {
        public void AddDevice(byte[] deviceId)
        {
            if (deviceId[0] == (byte)OneWireFamily.DS18B20)
            {
                this.Add(new DeviceDS18B20 { DeviceId = deviceId });
            }
            else if (deviceId[0] == (byte)OneWireFamily.DS18S20)
            {
                this.Add(new DeviceDS18S20 { DeviceId = deviceId });
            }
            else
            {
                throw new ArgumentException("Unsupported device found - extend library to include support!");
            }
        }

        /// <summary>
        /// Sends the command to all devices using skiprom.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="waitInMilliseconds">The wait in milliseconds.</param>
        /// <param name="oneWire">The one wire.</param>
        public void SendCommandToAll(byte command, int waitInMilliseconds, Microsoft.SPOT.Hardware.OneWire oneWire)
        {
            oneWire.TouchReset();
            oneWire.WriteByte(OneWireRomCommand.SkipROM);
            oneWire.WriteByte(command);
            Thread.Sleep(waitInMilliseconds);
        }
    }

    /// <summary>
    /// The base class for all 1-wire devices.
    /// </summary>
    public abstract class OneWireDevice
    {
        public OneWireDevice() { }
        private byte[] _deviceId;

        public byte[] DeviceId
        {
            get { return _deviceId; }
            set { _deviceId = value; }
        }

        /// <summary>
        /// Initilizes the bus to target this device.
        /// </summary>
        /// <param name="oneWire">The one wire.</param>
        protected void MatchDevice(Microsoft.SPOT.Hardware.OneWire oneWire)
        {
            oneWire.TouchReset();
            oneWire.WriteByte(OneWireRomCommand.MatchROM);

            foreach (byte theByte in DeviceId)
            {
                oneWire.WriteByte(theByte);
            }
        }

    }

    /// <summary>
    /// The base class for temperature probes on a 1-wire bus
    /// </summary>
    public abstract class OneWireTemperatureProbe : OneWireDevice
    {
        /// <summary>
        /// Reads the scratch pad and converts the temperature from the probe to C
        /// Value is also stored in LastTemperature
        /// </summary>
        /// <param name="oneWire">The one wire.</param>
        /// <returns></returns>
        public abstract double ReadScratchPad(Microsoft.SPOT.Hardware.OneWire oneWire);

        /// <summary>
        /// Issues a temperature convert to the device.
        /// If you are not running in parasite mode it's recommended to a skip rom and send convert to all instead, speeds things up!
        /// </summary>
        /// <param name="oneWire">The one wire.</param>
        public abstract void ConvertTemperature(Microsoft.SPOT.Hardware.OneWire oneWire);

        /// <summary>
        /// Gets or sets the last temperature read from the scratchpad
        /// </summary>
        /// <value>
        /// The last temperature.
        /// </value>
        public double LastTemperature { get; set; }

        /// <summary>
        /// Gets or sets the raw data from the scratch pad.
        /// </summary>
        /// <value>
        /// The scratch pad.
        /// </value>
        public byte[] ScratchPad { get; protected set; }

        protected short CreateTemperature(byte high, byte low)
        {
            // This code correctly parses negative temperatures as well.

            short temp2 = (short)(high << 8 | low);

            if (!((high & 0x80) == 0))
            {
                temp2 = (short)((high << 8) + low);
                //temp2 = (short)-temp2;
            }
            return temp2;
        }

    }

    /// <summary>
    /// Provides functionality for device of type DS18B20, which is a Temperature Probe
    /// http://datasheets.maximintegrated.com/en/ds/DS18B20.pdf
    /// </summary>
    public class DeviceDS18B20 : OneWireTemperatureProbe
    {
        public byte ConfigurationRegister { get; set; }

        /// <summary>
        /// Gets the resolution the probe current operates in.
        /// </summary>
        public int Resolution
        {
            get;
            private set;
        }

        private void ParseResolution()
        {
            if (ConfigurationRegister == OneWireDs18b20Reference.Resolution9Bit)
                Resolution = 9;
            else if (ConfigurationRegister == OneWireDs18b20Reference.Resolution10Bit)
                Resolution = 10;
            else if (ConfigurationRegister == OneWireDs18b20Reference.Resolution11Bit)
                Resolution = 11;
            else if (ConfigurationRegister == OneWireDs18b20Reference.Resolution12Bit)
                Resolution = 12;
            else
                Resolution = -1; // Unknown
        }

        /// <summary>
        /// Sets the resolution (between 9 and 12 bit).
        /// A new convert must be triggered to update temperature.
        /// </summary>
        /// <param name="resolution">The resolution.</param>
        public void SetResolution(int resolution, Microsoft.SPOT.Hardware.OneWire oneWire)
        {
            if (resolution < 9 || resolution > 12)
                throw new ArgumentException("Valid resolution span is 9 to 12 bits");

            if (ScratchPad == null)
                ReadScratchPad(oneWire);    // Read first to get user data fields ...

            MatchDevice(oneWire);

            oneWire.WriteByte(OneWireDs18b20Reference.CmdWriteScratchPad);
            oneWire.WriteByte(ScratchPad[2]);
            oneWire.WriteByte(ScratchPad[3]);
            if (resolution == 9)
                oneWire.WriteByte(OneWireDs18b20Reference.Resolution9Bit);
            else if (resolution == 10)
                oneWire.WriteByte(OneWireDs18b20Reference.Resolution10Bit);
            else if (resolution == 11)
                oneWire.WriteByte(OneWireDs18b20Reference.Resolution11Bit);
            else if (resolution == 12)
                oneWire.WriteByte(OneWireDs18b20Reference.Resolution12Bit);
        }

        public override double ReadScratchPad(Microsoft.SPOT.Hardware.OneWire oneWire)
        {
            MatchDevice(oneWire);

            oneWire.WriteByte(OneWireDs18b20Reference.CmdReadScratchPad);
            byte[] scratch = new byte[9];
            for (ushort i = 0; i < 9; i++)
            {
                scratch[i] = (byte)oneWire.ReadByte();
            }

            ConfigurationRegister = scratch[4];
            ParseResolution();
            short temp2 = CreateTemperature(scratch[1], scratch[0]); 

            double tempInC = (double)temp2 / 16f;

            LastTemperature = tempInC;
            ScratchPad = scratch;

            return tempInC;
        }

        public override string ToString()
        {
            return "DS18B20 Probe, ID# " + DeviceId.GetHex() + ", Temperature: " + LastTemperature.ToString() + "C" + ", Resolution: " + Resolution.ToString() + ", Configuration: " + ConfigurationRegister.GetHex();
        }

        /// <summary>
        /// Issues a temperature convert to the device.
        /// If you are not running in parasite mode it's recommended to a skip rom and send convert to all instead, speeds things up!
        /// </summary>
        /// <param name="oneWire">The one wire.</param>
        public override void ConvertTemperature(Microsoft.SPOT.Hardware.OneWire oneWire)
        {
            MatchDevice(oneWire);
            oneWire.WriteByte(OneWireDs18b20Reference.CmdConvert);
            Thread.Sleep(750);  // Just to be on the safe side when using 12bit resolution.
        }
    }

    /// <summary>
    /// Provides functionality for device of type DS18S20, which is a Temperature Probe
    /// http://datasheets.maximintegrated.com/en/ds/DS18S20.pdf
    /// </summary>
    public class DeviceDS18S20 : OneWireTemperatureProbe
    {


        public override double ReadScratchPad(Microsoft.SPOT.Hardware.OneWire oneWire)
        {
            MatchDevice(oneWire);

            oneWire.WriteByte(OneWireDs18b20Reference.CmdReadScratchPad);
            byte[] scratch = new byte[9];
            for (ushort i = 0; i < 9; i++)
            {
                scratch[i] = (byte)oneWire.ReadByte();
            }

            short temp2 = CreateTemperature(scratch[1], scratch[0]); 
            //if (!((scratch[1] & 0x80) == 0))
            //{
            //    temp2 = (short)((scratch[1] << 8) + scratch[0]);
            //    temp2 = (short)-temp2;
            //}

            double tempInC = (double)temp2 / 2f;

            LastTemperature = tempInC;
            ScratchPad = scratch;

            return tempInC;
        }

        public override string ToString()
        {
            return "DS18S20 Probe, ID# " + DeviceId.GetHex() + ", Temperature: " + LastTemperature.ToString() + "C";
        }

        /// <summary>
        /// Issues a temperature convert to the device.
        /// If you are not running in parasite mode it's recommended to a skip rom and send convert to all instead, speeds things up!
        /// </summary>
        /// <param name="oneWire">The one wire.</param>
        public override void ConvertTemperature(Microsoft.SPOT.Hardware.OneWire oneWire)
        {
            MatchDevice(oneWire);
            oneWire.WriteByte(OneWireDs18b20Reference.CmdConvert);
            Thread.Sleep(750);  // Just to be on the safe side when using 12bit resolution.
        }
    }


    public static class Extensions
    {
        public static bool GetBit(this byte b, int bitNumber)
        {
            return (b & (1 << bitNumber)) != 0;
        }

        public static long ToInt64(byte[] value, int startIndex)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            if (startIndex < 0 || startIndex > (value.Length - 8))
            {
                throw new ArgumentOutOfRangeException("startIndex", "Argument is out of range");
            }
            int high = (((value[startIndex + 0] << 0x18) | (value[startIndex + 1] << 0x10)) | (value[startIndex + 2] << 8)) | value[startIndex + 3];
            int low = (((value[startIndex + 4] << 0x18) | (value[startIndex + 5] << 0x10)) | (value[startIndex + 6] << 8)) | value[startIndex + 7];
            return (((long)((ulong)low)) | (((long)high) << 0x20));

        }

        const string hexChars = "0123456789abcdef";

        public static string GetHex(this byte[] b)
        {
            string hex = string.Empty;
            foreach (byte x in b)
            {
                hex += hexChars[x >> 4];
                hex += hexChars[x & 0x0f];
            }
            return hex;
        }

        public static string GetHex(this byte b)
        {
            string hex = string.Empty;
            hex += hexChars[b >> 4];
            hex += hexChars[b & 0x0f];
            return hex;
        }
    }
}

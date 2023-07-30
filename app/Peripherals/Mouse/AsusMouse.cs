﻿using GHelper.AnimeMatrix.Communication;
using GHelper.AnimeMatrix.Communication.Platform;
using System.Runtime.CompilerServices;
using System.Text;

namespace GHelper.Peripherals.Mouse
{
    public enum PowerOffSetting
    {
        Minutes1 = 0,
        Minutes2 = 1,
        Minutes3 = 2,
        Minutes5 = 3,
        Minutes10 = 4,
        Never = 0xFF
    }

    public enum PollingRate
    {
        PR125Hz = 0,
        PR250Hz = 1,
        PR500Hz = 2,
        PR1000Hz = 3,
        PR2000Hz = 4,
        PR4000Hz = 5,
        PR8000Hz = 6,
        PR16000Hz = 7 //for whenever that gets supported lol
    }

    public enum LiftOffDistance
    {
        Low = 0,
        High = 1
    }
    public enum AnimationDirection
    {
        Clockwise = 0x0,
        CounterClockwise = 0x1
    }

    public enum AnimationSpeed
    {
        Slow = 0x9,
        Medium = 0x7,
        Fast = 0x5
    }
    public enum LightingMode
    {
        Off = 0xF0,
        Static = 0x0,
        Breathing = 0x1,
        ColorCycle = 0x2,
        Rainbow = 0x3,
        React = 0x4,
        Comet = 0x5,
        BatteryState = 0x6
    }

    public enum LightingZone
    {
        Logo = 0x00,
        Scrollwheel = 0x01,
        Underglow = 0x02,
        All = 0x03,
        Dock = 0x04,
    }

    public class LightingSetting
    {
        public LightingSetting()
        {
            //Some Sane defaults
            LightingMode = LightingMode.Static;
            AnimationSpeed = AnimationSpeed.Medium;
            AnimationDirection = AnimationDirection.Clockwise;
            RandomColor = false;
            Brightness = 25;
            RGBColor = Color.Red;
        }

        public LightingMode LightingMode { get; set; }
        public int Brightness { get; set; }
        public Color RGBColor { get; set; }
        public bool RandomColor { get; set; }
        public AnimationSpeed AnimationSpeed { get; set; }

        public AnimationDirection AnimationDirection { get; set; }

        public override bool Equals(object? obj)
        {
            return obj is LightingSetting setting &&
                   LightingMode == setting.LightingMode &&
                   Brightness == setting.Brightness &&
                   RGBColor.Equals(setting.RGBColor) &&
                   RandomColor == setting.RandomColor &&
                   AnimationSpeed == setting.AnimationSpeed &&
                   AnimationDirection == setting.AnimationDirection;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(LightingMode, Brightness, RGBColor, RandomColor, AnimationSpeed, AnimationDirection);
        }

        public override string? ToString()
        {
            return "LightingMode: " + LightingMode + ", Color (" + RGBColor.R + ", " + RGBColor.G + ", " + RGBColor.B
                + "), Brightness: " + Brightness + "%, LightingSpeed: " + AnimationSpeed + ", RandomColor:" + RandomColor + ", AnimationDirection:" + AnimationDirection;
        }


    }

    public class AsusMouseDPI
    {
        public AsusMouseDPI()
        {
            Color = Color.Red;
            DPI = 800;
        }
        public Color Color { get; set; }
        public uint DPI { get; set; }
        public override string? ToString()
        {
            return "DPI: " + DPI + ", Color (" + Color.R + ", " + Color.G + ", " + Color.B + ")";
        }
    }

    public abstract class AsusMouse : Device, IPeripheral
    {
        private static string[] POLLING_RATES = { "125 Hz", "250 Hz", "500 Hz", "1000 Hz", "2000 Hz", "4000 Hz", "8000 Hz", "16000 Hz" };
        internal const bool PACKET_LOGGER_ALWAYS_ON = true;
        internal const int ASUS_MOUSE_PACKET_SIZE = 65;

        public event EventHandler? Disconnect;
        public event EventHandler? BatteryUpdated;
        public event EventHandler? MouseReadyChanged;

        private readonly string path;

        public bool IsDeviceReady { get; protected set; }

        private void SetDeviceReady(bool ready)
        {
            bool notify = false;
            if (IsDeviceReady != ready)
            {
                notify = true;
            }
            IsDeviceReady = ready;


            if (MouseReadyChanged is not null && notify)
            {
                MouseReadyChanged(this, EventArgs.Empty);
            }
        }
        public bool Wireless { get; protected set; }
        public int Battery { get; protected set; }
        public bool Charging { get; protected set; }
        public LightingSetting[] LightingSetting { get; protected set; }
        public int LowBatteryWarning { get; protected set; }
        public PowerOffSetting PowerOffSetting { get; protected set; }
        public LiftOffDistance LiftOffDistance { get; protected set; }
        public int DpiProfile { get; protected set; }
        public AsusMouseDPI[] DpiSettings { get; protected set; }
        public int Profile { get; protected set; }
        public PollingRate PollingRate { get; protected set; }
        public bool AngleSnapping { get; protected set; }
        public short AngleAdjustmentDegrees { get; protected set; }

        public AsusMouse(ushort vendorId, ushort productId, string path, bool wireless) : base(vendorId, productId)
        {
            this.path = path;
            this.Wireless = wireless;
            DpiSettings = new AsusMouseDPI[1];
            if (SupportedLightingZones().Length == 0)
            {
                LightingSetting = new LightingSetting[1];
            }
            else
            {
                LightingSetting = new LightingSetting[SupportedLightingZones().Length];
            }

        }

        public override bool Equals(object? obj)
        {
            if (obj is not AsusMouse item)
            {
                return false;
            }

            return this.VendorID().Equals(item.VendorID())
                && this.ProductID().Equals(item.ProductID())
                && this.path.Equals(item.path);
        }

        public override int GetHashCode()
        {
            int hash = 23;
            hash = hash * 31 + VendorID();
            hash = hash * 31 + ProductID();
            hash = hash * 31 + path.GetHashCode();
            return hash;
        }

        public void Connect()
        {
            SetProvider();
            HidSharp.DeviceList.Local.Changed += Device_Changed;
        }

        public override void Dispose()
        {
            Logger.WriteLine(GetDisplayName() + ": Disposing");
            HidSharp.DeviceList.Local.Changed -= Device_Changed;
            base.Dispose();
        }

        private void Device_Changed(object? sender, HidSharp.DeviceListChangedEventArgs e)
        {
            //Use this to validate whether the device is still connected.
            //If not, this will also initiate the disconnect and cleanup sequence.
            CheckConnection();
        }

        //Override this for non battery devices to check whether the connection is still there
        //This function should automatically disconnect the device in GHelper if the device is no longer there or the pipe is broken.
        public virtual void CheckConnection()
        {
            ReadBattery();
        }

        public bool IsDeviceConnected()
        {
            try
            {
                HidSharp.DeviceList.Local.GetHidDevices(VendorID(), ProductID())
                    .First(x => x.DevicePath.Contains(path));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public virtual int USBTimeout()
        {
            return 300;
        }

        public override void SetProvider()
        {
            _usbProvider = new WindowsUsbProvider(_vendorId, _productId, path, USBTimeout());
        }

        protected virtual void OnDisconnect()
        {
            Logger.WriteLine(GetDisplayName() + ": OnDisconnect()");
            if (Disconnect is not null)
            {
                Disconnect(this, EventArgs.Empty);
            }
        }

        private static bool IsPacketLoggerEnabled()
        {
#if DEBUG
            return true;
#else

            return AppConfig.Get("usb_packet_logger") == 1 || PACKET_LOGGER_ALWAYS_ON;
#endif
        }

        protected virtual bool IsMouseError(byte[] packet)
        {
            return packet[1] == 0xFF && packet[2] == 0xAA;
        }

        protected virtual long MeasuredIO(Action<byte[]> ioFunc, byte[] param)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            ioFunc(param);

            watch.Stop();
            return watch.ElapsedMilliseconds;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        protected virtual byte[]? WriteForResponse(byte[] packet)
        {
            byte[] response = new byte[ASUS_MOUSE_PACKET_SIZE];

            try
            {
                if (IsPacketLoggerEnabled())
                    Logger.WriteLine(GetDisplayName() + ": Sending packet: " + ByteArrayToString(packet));

                long time = MeasuredIO(Write, packet);
                Logger.WriteLine(GetDisplayName() + ": Write took " + time + "ms");

                time = MeasuredIO(Read, response);
                Logger.WriteLine(GetDisplayName() + ": Read took " + time + "ms");

                if (IsPacketLoggerEnabled())
                    Logger.WriteLine(GetDisplayName() + ": Read packet: " + ByteArrayToString(response));

                if (IsMouseError(response))
                {
                    Logger.WriteLine(GetDisplayName() + ": Mouse returned error (FF AA). Packet probably not supported by mouse firmware.");
                    //Error. Mouse could not understand or process the sent packet
                    return response;
                }

                //Not the response we were looking for, continue reading
                while (response[0] != packet[0] || response[1] != packet[1] || response[2] != packet[2])
                {
                    Logger.WriteLine(GetDisplayName() + ": Read wrong packet left in buffer: " + ByteArrayToString(response) + ". Retrying...");
                    //Read again
                    time = MeasuredIO(Read, response);
                    Logger.WriteLine(GetDisplayName() + ": Read took " + time + "ms");
                }

            }
            catch (IOException e)
            {
                Logger.WriteLine(GetDisplayName() + ": Failed to read packet " + e.Message);
                OnDisconnect();
                return null;
            }
            catch (TimeoutException e)
            {
                Logger.WriteLine(GetDisplayName() + ": Timeout reading packet " + e.Message);
                return null;
            }
            catch (ObjectDisposedException)
            {
                Logger.WriteLine(GetDisplayName() + ": Channel closed ");
                OnDisconnect();
                return null;
            }


            return response;
        }
        public abstract string GetDisplayName();

        public PeripheralType DeviceType()
        {
            return PeripheralType.Mouse;
        }

        public virtual void SynchronizeDevice()
        {
            DpiSettings = new AsusMouseDPI[DPIProfileCount()];
            ReadBattery();
            if (HasBattery() && Battery <= 0 && Charging == false)
            {
                //Likely only the dongle connected and the mouse is either sleeping or turned off.
                //The mouse will not respond with proper data, but empty responses at this point
                SetDeviceReady(false);
                return;
            }
            SetDeviceReady(true);

            ReadProfile();
            ReadDPI();
            ReadPollingRate();
            ReadLiftOffDistance();
            ReadLightingSetting();
        }

        // ------------------------------------------------------------------------------
        // Battery
        // ------------------------------------------------------------------------------

        public virtual bool HasBattery()
        {
            return true;
        }

        public virtual bool HasAutoPowerOff()
        {
            return false;
        }

        public virtual bool HasLowBatteryWarning()
        {
            return false;
        }

        protected virtual byte[] GetBatteryReportPacket()
        {
            return new byte[] { 0x00, 0x12, 0x07 };
        }

        protected virtual int ParseBattery(byte[] packet)
        {
            if (packet[1] == 0x12 && packet[2] == 0x07)
            {
                return packet[5];
            }

            return -1;
        }
        protected virtual bool ParseChargingState(byte[] packet)
        {
            if (packet[1] == 0x12 && packet[2] == 0x07)
            {
                return packet[10] > 0;
            }

            return false;
        }

        protected virtual PowerOffSetting ParsePowerOffSetting(byte[] packet)
        {
            if (packet[1] == 0x12 && packet[2] == 0x07)
            {
                return (PowerOffSetting)packet[6];
            }

            return PowerOffSetting.Never;
        }
        protected virtual int ParseLowBatteryWarning(byte[] packet)
        {
            if (packet[1] == 0x12 && packet[2] == 0x07)
            {
                return packet[7];
            }

            return 0;
        }
        protected virtual byte[] GetUpdateEnergySettingsPacket(int lowBatteryWarning, PowerOffSetting powerOff)
        {
            return new byte[] { 0x00, 0x51, 0x37, 0x00, 0x00, (byte)powerOff, 0x00, (byte)lowBatteryWarning };
        }

        public void SetEnergySettings(int lowBatteryWarning, PowerOffSetting powerOff)
        {
            if (!HasAutoPowerOff() && !HasLowBatteryWarning())
            {
                return;
            }

            WriteForResponse(GetUpdateEnergySettingsPacket(lowBatteryWarning, powerOff));
            FlushSettings();

            Logger.WriteLine(GetDisplayName() + ": Got Auto Power Off: " + powerOff + " - Low Battery Warnning at: " + lowBatteryWarning + "%");
            this.PowerOffSetting = powerOff;
            this.LowBatteryWarning = lowBatteryWarning;
        }

        public void ReadBattery()
        {
            if (!HasBattery() && !HasAutoPowerOff())
            {
                return;
            }

            byte[]? response = WriteForResponse(GetBatteryReportPacket());
            if (response is null) return;

            if (HasBattery())
            {
                Battery = ParseBattery(response);
                Charging = ParseChargingState(response);

                //If the device goes to standby it will not report battery state anymore.
                SetDeviceReady(Battery > 0);

                if (!IsDeviceReady)
                {
                    Logger.WriteLine(GetDisplayName() + ": Device gone");
                    return;
                }

                Logger.WriteLine(GetDisplayName() + ": Got Battery Percentage " + Battery + "% - Charging:" + Charging);

                if (BatteryUpdated is not null)
                {
                    BatteryUpdated(this, EventArgs.Empty);
                }
            }

            if (HasAutoPowerOff())
            {
                PowerOffSetting = ParsePowerOffSetting(response);
            }

            if (HasLowBatteryWarning())
            {
                LowBatteryWarning = ParseLowBatteryWarning(response);
            }

            if (HasLowBatteryWarning() || HasAutoPowerOff())
            {
                string pos = HasAutoPowerOff() ? PowerOffSetting.ToString() : "Not Supported";
                string lbw = HasLowBatteryWarning() ? LowBatteryWarning.ToString() : "Not Supported";
                Logger.WriteLine(GetDisplayName() + ": Got Auto Power Off: " + pos + " - Low Battery Warnning at: " + lbw + "%");
            }

        }

        // ------------------------------------------------------------------------------
        // Profiles
        // ------------------------------------------------------------------------------
        public abstract int ProfileCount();

        public virtual bool HasProfiles()
        {
            return true;
        }

        protected virtual int ParseProfile(byte[] packet)
        {
            if (packet[1] == 0x12 && packet[2] == 0x00 && packet[3] == 0x00)
            {
                return packet[11];
            }
            Logger.WriteLine(GetDisplayName() + ": Failed to decode active profile");
            return 1;
        }

        protected virtual int ParseDPIProfile(byte[] packet)
        {
            if (packet[1] == 0x12 && packet[2] == 0x00 && packet[3] == 0x00)
            {
                return packet[12];
            }
            Logger.WriteLine(GetDisplayName() + ": Failed to decode active profile");
            return 1;
        }

        protected virtual byte[] GetReadProfilePacket()
        {
            return new byte[] { 0x00, 0x12, 0x00 };
        }

        protected virtual byte[] GetUpdateProfilePacket(int profile)
        {
            return new byte[] { 0x00, 0x50, 0x02, (byte)profile };
        }

        public void ReadProfile()
        {
            if (!HasProfiles())
            {
                return;
            }

            byte[]? response = WriteForResponse(GetReadProfilePacket());
            if (response is null) return;

            Profile = ParseProfile(response);
            if (DPIProfileCount() > 1)
            {

                DpiProfile = ParseDPIProfile(response);
            }
            Logger.WriteLine(GetDisplayName() + ": Active Profile " + (Profile + 1)
                + ((DPIProfileCount() > 1 ? ", Active DPI Profile: " + DpiProfile : "")));
        }

        public void SetProfile(int profile)
        {
            if (!HasProfiles())
            {
                return;
            }

            if (profile > ProfileCount() || profile < 0)
            {
                Logger.WriteLine(GetDisplayName() + ": Profile:" + profile + " is invalid.");
                return;
            }

            WriteForResponse(GetUpdateProfilePacket(profile));
            FlushSettings();

            Logger.WriteLine(GetDisplayName() + ": Profile set to " + profile);
            this.Profile = profile;
        }

        // ------------------------------------------------------------------------------
        // Polling Rate and Angle Snapping
        // ------------------------------------------------------------------------------


        public virtual bool HasAngleSnapping()
        {
            return false;
        }
        public virtual bool HasAngleTuning()
        {
            return false;
        }

        public virtual string PollingRateDisplayString(PollingRate pollingRate)
        {
            return POLLING_RATES[(int)pollingRate];
        }

        public virtual int PollingRateCount()
        {
            return SupportedPollingrates().Length;
        }

        public virtual int PollingRateIndex(PollingRate pollingRate)
        {
            for (int i = 0; i < PollingRateCount(); ++i)
            {
                if (SupportedPollingrates()[i] == pollingRate)
                {
                    return i;
                }
            }
            return -1;
        }


        public virtual bool IsPollingRateSupported(PollingRate pollingRate)
        {
            return SupportedPollingrates().Contains(pollingRate);
        }

        public abstract PollingRate[] SupportedPollingrates();

        public virtual bool CanSetPollingRate()
        {
            return true;
        }

        protected virtual byte[] GetReadPollingRatePacket()
        {
            return new byte[] { 0x00, 0x12, 0x04, 0x00 };
        }

        protected virtual byte[] GetUpdatePollingRatePacket(PollingRate pollingRate)
        {
            return new byte[] { 0x00, 0x51, 0x31, 0x04, 0x00, (byte)pollingRate };
        }
        protected virtual byte[] GetUpdateAngleSnappingPacket(bool angleSnapping)
        {
            return new byte[] { 0x00, 0x51, 0x31, 0x06, 0x00, (byte)(angleSnapping ? 0x01 : 0x00) };
        }
        protected virtual byte[] GetUpdateAngleAdjustmentPacket(short angleAdjustment)
        {
            return new byte[] { 0x00, 0x51, 0x31, 0x0B, 0x00, (byte)(angleAdjustment & 0xFF), (byte)((angleAdjustment >> 8) & 0xFF) };
        }

        protected virtual PollingRate ParsePollingRate(byte[] packet)
        {
            if (packet[1] == 0x12 && packet[2] == 0x04 && packet[3] == 0x00)
            {
                return (PollingRate)packet[13];
            }

            return PollingRate.PR125Hz;
        }

        protected virtual bool ParseAngleSnapping(byte[] packet)
        {
            if (packet[1] == 0x12 && packet[2] == 0x04 && packet[3] == 0x00)
            {
                return packet[17] == 0x01;
            }

            return false;
        }

        protected virtual short ParseAngleAdjustment(byte[] packet)
        {
            if (packet[1] == 0x12 && packet[2] == 0x04 && packet[3] == 0x00)
            {
                return (short)(packet[20] << 8 | packet[19]);
            }

            return 0;
        }

        public void ReadPollingRate()
        {
            if (!CanSetPollingRate())
            {
                return;
            }

            byte[]? response = WriteForResponse(GetReadPollingRatePacket());
            if (response is null) return;

            PollingRate = ParsePollingRate(response);
            Logger.WriteLine(GetDisplayName() + ": Pollingrate: " + PollingRateDisplayString(PollingRate) + " (" + PollingRate + ")");

            if (HasAngleSnapping())
            {
                AngleSnapping = ParseAngleSnapping(response);
                if (HasAngleTuning())
                    AngleAdjustmentDegrees = ParseAngleAdjustment(response);

                Logger.WriteLine(GetDisplayName() + ": Angle Snapping enabled: " + AngleSnapping + ", Angle Adjustment: " + AngleAdjustmentDegrees + "°");
            }
        }

        public void SetPollingRate(PollingRate pollingRate)
        {
            if (!CanSetPollingRate())
            {
                return;
            }

            if (!IsPollingRateSupported(pollingRate))
            {
                Logger.WriteLine(GetDisplayName() + ": Pollingrate:" + pollingRate + " is not supported by this mouse.");
                return;
            }

            WriteForResponse(GetUpdatePollingRatePacket(pollingRate));
            FlushSettings();

            Logger.WriteLine(GetDisplayName() + ": Pollingrate set to " + PollingRateDisplayString(pollingRate));
            this.PollingRate = pollingRate;
        }

        public void SetAngleSnapping(bool angleSnapping)
        {
            if (!HasAngleSnapping())
            {
                return;
            }

            WriteForResponse(GetUpdateAngleSnappingPacket(angleSnapping));
            FlushSettings();

            Logger.WriteLine(GetDisplayName() + ": Angle Snapping set to " + angleSnapping);
            this.AngleSnapping = angleSnapping;
        }

        public void SetAngleAdjustment(short angleAdjustment)
        {
            if (!HasAngleTuning())
            {
                return;
            }

            if (angleAdjustment < -20 || angleAdjustment > 20)
            {
                Logger.WriteLine(GetDisplayName() + ": Angle Adjustment:" + angleAdjustment + " is outside of range [-20;20].");
                return;
            }

            WriteForResponse(GetUpdateAngleAdjustmentPacket(angleAdjustment));
            FlushSettings();

            Logger.WriteLine(GetDisplayName() + ": Angle Adjustment set to " + angleAdjustment);
            this.AngleAdjustmentDegrees = angleAdjustment;
        }

        // ------------------------------------------------------------------------------
        // DPI
        // ------------------------------------------------------------------------------
        public abstract int DPIProfileCount();
        public virtual bool HasDPIColors()
        {
            return false;
        }

        public virtual int DPIIncrements()
        {
            return 50;
        }

        public virtual bool CanChangeDPIProfile()
        {
            return DPIProfileCount() > 1;
        }

        public virtual int MaxDPI()
        {
            return 2000;
        }
        public virtual int MinDPI()
        {
            return 100;
        }

        public virtual bool HasXYDPI()
        {
            return false;
        }

        protected virtual byte[] GetChangeDPIProfilePacket(int profile)
        {
            return new byte[] { 0x00, 0x51, 0x31, 0x0A, 0x00, (byte)profile };
        }

        protected virtual byte[] GetChangeDPIProfilePacket2(int profile)
        {
            return new byte[] { 0x00, 0x51, 0x31, 0x09, 0x00, (byte)profile };
        }

        //profiles start to count at 1
        public virtual void SetDPIProfile(int profile)
        {
            if (!CanChangeDPIProfile())
            {
                return;
            }

            if (profile > DPIProfileCount() || profile < 1)
            {
                Logger.WriteLine(GetDisplayName() + ": DPI Profile:" + profile + " is invalid.");
                return;
            }

            //The first DPI profile is 1
            WriteForResponse(GetChangeDPIProfilePacket(profile));
            //For whatever reason that is required or the mouse will not store the change and reverts once you power it off.
            WriteForResponse(GetChangeDPIProfilePacket2(profile));
            FlushSettings();

            Logger.WriteLine(GetDisplayName() + ": DPI Profile set to " + profile);
            this.DpiProfile = profile;
        }

        protected virtual byte[] GetReadDPIPacket()
        {
            if (!HasXYDPI())
            {
                return new byte[] { 0x00, 0x12, 0x04, 0x00 };
            }

            return new byte[] { 0x00, 0x12, 0x04, 0x02 };
        }

        protected virtual byte[]? GetUpdateDPIPacket(AsusMouseDPI dpi, int profile)
        {
            if (dpi is null)
            {
                return null;
            }
            if (dpi.DPI > MaxDPI() || dpi.DPI < MinDPI())
            {
                return null;
            }
            ushort dpiEncoded = (ushort)((dpi.DPI - DPIIncrements()) / DPIIncrements());

            if (HasDPIColors())
            {
                return new byte[] { 0x00, 0x51, 0x31, (byte)(profile - 1), 0x00, (byte)(dpiEncoded & 0xFF), (byte)((dpiEncoded >> 8) & 0xFF), dpi.Color.R, dpi.Color.G, dpi.Color.B };
            }
            else
            {
                return new byte[] { 0x00, 0x51, 0x31, (byte)(profile - 1), 0x00, (byte)(dpiEncoded & 0xFF), (byte)((dpiEncoded >> 8) & 0xFF) };
            }

        }

        protected virtual void ParseDPI(byte[] packet)
        {
            if (packet[1] != 0x12 || packet[2] != 0x04 || (packet[3] != 0x02 && HasXYDPI()))
            {
                return;
            }

            for (int i = 0; i < DPIProfileCount(); ++i)
            {
                if (DpiSettings[i] is null)
                {
                    DpiSettings[i] = new AsusMouseDPI();
                }

                int offset = HasXYDPI() ? (5 + (i * 4)) : (5 + (i * 2));


                uint b1 = packet[offset];
                uint b2 = packet[offset + 1];

                DpiSettings[i].DPI = (uint)((b2 << 8 | b1) * DPIIncrements() + DPIIncrements());
            }
        }

        protected virtual byte[] GetReadDPIColorsPacket()
        {
            return new byte[] { 0x00, 0x12, 0x04, 0x03 };
        }

        protected virtual void ParseDPIColors(byte[] packet)
        {
            if (packet[1] != 0x12 || packet[2] != 0x04 || packet[3] != 0x03)
            {
                return;
            }

            for (int i = 0; i < DPIProfileCount(); ++i)
            {
                if (DpiSettings[i] is null)
                {
                    DpiSettings[i] = new AsusMouseDPI();
                }

                int offset = 5 + (i * 3);

                DpiSettings[i].Color = Color.FromArgb(packet[offset], packet[offset + 1], packet[offset + 2]);
            }
        }

        public void ReadDPI()
        {
            byte[]? response = WriteForResponse(GetReadDPIPacket());
            if (response is null) return;
            ParseDPI(response);

            if (HasDPIColors())
            {
                response = WriteForResponse(GetReadDPIColorsPacket());
                if (response is null) return;
                ParseDPIColors(response);
            }

            for (int i = 0; i < DPIProfileCount(); ++i)
            {
                Logger.WriteLine(GetDisplayName() + ": Read DPI Setting " + (i + 1) + ": " + DpiSettings[i].ToString());
            }

        }

        public void SetDPIForProfile(AsusMouseDPI dpi, int profile)
        {
            if (profile > DPIProfileCount() || profile < 1)
            {
                Logger.WriteLine(GetDisplayName() + ": DPI Profile:" + profile + " is invalid.");
                return;
            }

            byte[]? packet = GetUpdateDPIPacket(dpi, profile);
            if (packet == null)
            {
                Logger.WriteLine(GetDisplayName() + ": DPI setting for profile " + profile + " does not exist or is invalid.");
                return;
            }
            WriteForResponse(packet);
            FlushSettings();

            Logger.WriteLine(GetDisplayName() + ": DPI for profile " + profile + " set to " + DpiSettings[profile - 1].DPI);
            //this.DpiProfile = profile;
            this.DpiSettings[profile - 1] = dpi;
        }



        // ------------------------------------------------------------------------------
        // Lift-off Distance
        // ------------------------------------------------------------------------------

        public virtual bool HasLiftOffSetting()
        {
            return false;
        }

        protected virtual byte[] GetReadLiftOffDistancePacket()
        {
            return new byte[] { 0x00, 0x12, 0x06 };
        }

        //This also resets the "calibration" to default. There is no seperate command to only set the lift off distance
        protected virtual byte[] GetUpdateLiftOffDistancePacket(LiftOffDistance liftOffDistance)
        {
            return new byte[] { 0x00, 0x51, 0x35, 0xFF, 0x00, 0xFF, ((byte)liftOffDistance) };
        }

        protected virtual LiftOffDistance ParseLiftOffDistance(byte[] packet)
        {
            if (packet[1] != 0x12 || packet[2] != 0x06)
            {
                return LiftOffDistance.Low;
            }

            return (LiftOffDistance)packet[8];
        }

        public void ReadLiftOffDistance()
        {
            if (!HasLiftOffSetting())
            {
                return;
            }
            byte[]? response = WriteForResponse(GetReadLiftOffDistancePacket());
            if (response is null) return;

            LiftOffDistance = ParseLiftOffDistance(response);


            Logger.WriteLine(GetDisplayName() + ": Read Lift Off Setting: " + LiftOffDistance);
        }

        public void SetLiftOffDistance(LiftOffDistance liftOffDistance)
        {
            if (!HasLiftOffSetting())
            {
                return;
            }

            WriteForResponse(GetUpdateLiftOffDistancePacket(liftOffDistance));
            FlushSettings();

            Logger.WriteLine(GetDisplayName() + ": Set Liftoff Distance to " + liftOffDistance);
            this.LiftOffDistance = liftOffDistance;
        }

        // ------------------------------------------------------------------------------
        // RGB
        // ------------------------------------------------------------------------------

        public virtual bool HasRGB()
        {
            return false;
        }

        public virtual int MaxBrightness()
        {
            return 100;
        }

        //Override to remap lighting mode IDs.
        //From OpenRGB code it looks like some mice have different orders of the modes or do not support some modes at all.
        protected virtual byte IndexForLightingMode(LightingMode lightingMode)
        {
            return ((byte)lightingMode);
        }

        //Also override this for the reverse mapping
        protected virtual LightingMode LightingModeForIndex(byte lightingMode)
        {
            //We do not support other mods. we treat them as off. True off is actually 0xF0.
            if (lightingMode > 0x06)
            {
                return LightingMode.Off;
            }
            return ((LightingMode)lightingMode);
        }

        //And this if not all modes are supported
        public virtual bool IsLightingModeSupported(LightingMode lightingMode)
        {
            return true;
        }

        public virtual bool SupportsRandomColor(LightingMode lightingMode)
        {
            return lightingMode == LightingMode.Comet;
        }

        public virtual bool SupportsAnimationDirection(LightingMode lightingMode)
        {
            return lightingMode == LightingMode.Rainbow
                || lightingMode == LightingMode.Comet;
        }
        public virtual bool SupportsAnimationSpeed(LightingMode lightingMode)
        {
            return lightingMode == LightingMode.Rainbow;
        }

        public virtual bool SupportsColorSetting(LightingMode lightingMode)
        {
            return lightingMode == LightingMode.Static
                 || lightingMode == LightingMode.Breathing
                 || lightingMode == LightingMode.Comet
                 || lightingMode == LightingMode.React;
        }

        public virtual LightingZone[] SupportedLightingZones()
        {
            return new LightingZone[] { LightingZone.Logo };
        }

        public virtual int IndexForZone(LightingZone zone)
        {
            LightingZone[] lz = SupportedLightingZones();
            for (int i = 0; i < lz.Length; ++i)
            {
                if (lz[i] == zone)
                {
                    return i;
                }
            }
            return 0;
        }

        public virtual bool IsLightingZoned()
        {
            if (LightingSetting.Length < 2)
            {
                return false;
            }

            //Check whether all zones are the same or not
            for (int i = 1; i < LightingSetting.Length; ++i)
            {
                if (LightingSetting[i] is null
                   || LightingSetting[i - 1] is null
                   || !LightingSetting[i].Equals(LightingSetting[i - 1]))
                {
                    return true;
                }
            }
            return false;
        }

        public virtual bool IsLightingModeSupportedForZone(LightingMode lm, LightingZone lz)
        {
            if (lz == LightingZone.All)
            {
                return true;
            }

            return lm == LightingMode.Static
                || lm == LightingMode.Breathing
                || lm == LightingMode.ColorCycle
                || lm == LightingMode.React;
        }

        public virtual LightingSetting LightingSettingForZone(LightingZone zone)
        {
            if (zone == LightingZone.All)
            {
                //First zone is treated as ALL for reading purpose
                return LightingSetting[0];
            }

            return LightingSetting[IndexForZone(zone)];
        }

        protected virtual byte[] GetReadLightingModePacket(LightingZone zone)
        {
            int idx = 0;

            if (zone != LightingZone.All)
            {
                idx = IndexForZone(zone);
            }

            return new byte[] { 0x00, 0x12, 0x03, (byte)idx };
        }

        protected virtual byte[] GetUpdateLightingModePacket(LightingSetting lightingSetting, LightingZone zone)
        {
            if (lightingSetting.Brightness < 0 || lightingSetting.Brightness > MaxBrightness())
            {
                Logger.WriteLine(GetDisplayName() + ": Brightness " + lightingSetting.Brightness
                    + " is out of range [0;" + MaxBrightness() + "]. Setting to " + (MaxBrightness() / 4) + " .");

                lightingSetting.Brightness = MaxBrightness() / 4; // set t0 25% of max brightness
            }
            if (!IsLightingModeSupported(lightingSetting.LightingMode))
            {
                Logger.WriteLine(GetDisplayName() + ": Lighting Mode " + lightingSetting.LightingMode + " is not supported. Setting to Color Cycle ;)");
                lightingSetting.LightingMode = LightingMode.ColorCycle;
            }

            return new byte[] { 0x00, 0x51, 0x28, (byte)zone, 0x00,
                IndexForLightingMode(lightingSetting.LightingMode),
                (byte)lightingSetting.Brightness,
                lightingSetting.RGBColor.R, lightingSetting.RGBColor.G, lightingSetting.RGBColor.B,
                (byte)(SupportsAnimationDirection(lightingSetting.LightingMode) ? lightingSetting.AnimationDirection : 0x00),
                (byte)((lightingSetting.RandomColor && SupportsRandomColor(lightingSetting.LightingMode)) ? 0x01: 0x00),
                (byte)(SupportsAnimationSpeed(lightingSetting.LightingMode) ? lightingSetting.AnimationSpeed : 0x00)
            };
        }

        protected virtual LightingSetting? ParseLightingSetting(byte[] packet)
        {
            if (packet[1] != 0x12 || packet[2] != 0x03)
            {
                return null;
            }

            LightingSetting setting = new LightingSetting();

            setting.LightingMode = LightingModeForIndex(packet[5]);
            setting.Brightness = packet[6];

            setting.RGBColor = Color.FromArgb(packet[7], packet[8], packet[9]);


            setting.AnimationDirection = SupportsAnimationDirection(setting.LightingMode)
                ? (AnimationDirection)packet[11]
                : AnimationDirection.Clockwise;

            setting.RandomColor = SupportsRandomColor(setting.LightingMode) && packet[12] == 0x01;
            setting.AnimationSpeed = SupportsAnimationSpeed(setting.LightingMode)
                ? (AnimationSpeed)packet[13]
                : AnimationSpeed.Medium;

            //If the mouse reports an out of range value, which it does when the current setting has no speed option, chose medium as default
            if (setting.AnimationSpeed != AnimationSpeed.Fast
                && setting.AnimationSpeed != AnimationSpeed.Medium
                && setting.AnimationSpeed != AnimationSpeed.Slow)
            {
                setting.AnimationSpeed = AnimationSpeed.Medium;
            }

            return setting;
        }

        public void ReadLightingSetting()
        {
            if (!HasRGB())
            {
                return;
            }

            LightingZone[] lz = SupportedLightingZones();
            for (int i = 0; i < lz.Length; ++i)
            {
                byte[]? response = WriteForResponse(GetReadLightingModePacket(lz[i]));
                if (response is null) return;

                LightingSetting? ls = ParseLightingSetting(response);
                if (ls is null)
                {
                    Logger.WriteLine(GetDisplayName() + ": Failed to read RGB Setting for Zone " + lz[i].ToString());
                    continue;
                }

                Logger.WriteLine(GetDisplayName() + ": Read RGB Setting for Zone " + lz[i].ToString() + ": " + ls.ToString());
                LightingSetting[i] = ls;
            }
        }

        public void SetLightingSetting(LightingSetting lightingSetting, LightingZone zone)
        {
            if (!HasRGB() || lightingSetting is null)
            {
                return;
            }

            WriteForResponse(GetUpdateLightingModePacket(lightingSetting, zone));
            FlushSettings();

            Logger.WriteLine(GetDisplayName() + ": Set RGB Setting for zone " + zone.ToString() + ": " + lightingSetting.ToString());
            if (zone == LightingZone.All)
            {
                for (int i = 0; i < this.LightingSetting.Length; ++i)
                {
                    this.LightingSetting[i] = lightingSetting;
                }
            }
            else
            {
                this.LightingSetting[IndexForZone(zone)] = lightingSetting;
            }
        }

        protected virtual byte[] GetSaveProfilePacket()
        {
            return new byte[] { 0x00, 0x50, 0x03 };
        }

        public void FlushSettings()
        {
            WriteForResponse(GetSaveProfilePacket());

            Logger.WriteLine(GetDisplayName() + ": Settings Flushed ");
        }

        public override string? ToString()
        {
            return "";

        }


        public static string ByteArrayToString(byte[] packet)
        {
            StringBuilder hex = new StringBuilder(packet.Length * 2);
            foreach (byte b in packet)
                hex.AppendFormat("{0:x2} ", b);
            return hex.ToString();
        }
    }
}
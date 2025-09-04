using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using Cellario.DeviceBase;

namespace HRB
{
    public class ALPSDriver : SerialDeviceDriver
    {
        // Connection Parameters
        private const string _connectionPort = "Port";
        // Sealing Parameters
        private const string _parameterTemperature = "Temperature";
        private const string _parameterSealTime = "SealTime";
        private const string _parameterSealForce = "SealForce";
        private const string _parameterSealLength = "SealLength";

        // Connect operations
        private const string _operationConnect = "Connect";
        //seal operations
        private const string _operationStartSealing = "StartSealing";
        public string StartSealingOperationName => _operationStartSealing;

        // Default values
        private const int DefaultTemperature = 165; // °C
        private const decimal DefaultSealTime = 3.0M; // seconds (0-9.9)
        private const int DefaultSealForce = 50; // Kg (5-50)
        private const int DefaultSealLength = 125; // mm (110-128)

        // Parameter ranges
        private const decimal MinSealTime = 0.0M;
        private const decimal MaxSealTime = 9.9M;
        private const int MinSealForce = 5;
        private const int MaxSealForce = 50;
        private const int MinSealLength = 110;
        private const int MaxSealLength = 128;

        // Response codes
        private const string ResponseOK = "ok";
        private const string ResponseError = "er";

        // Serial port configuration
        private new const int BaudRate = 9600;
        private new const int DataBits = 8;
        private static readonly Parity PortParity = Parity.None;
        private static readonly StopBits PortStopBits = StopBits.One;
        private static readonly Handshake FlowControl = Handshake.None;
        private const int ReadTimeout = 5000;
        private const int WriteTimeout = 5000;

        // Status bit definitions
        private static readonly (int bit, string description)[] StatusBits = new[]
        {
            (0, "No Fail"),
            (1, "Error"),
            (2, "Busy"),
            (3, "Not At Seal Temperature"),
            (4, "Plate Not Present"),
            (5, "Not Initialised"),
            (6, "Force Sensor activated"),
            (7, "Park Mode")
        };

        private const int BusyBit = 2;
        private const int NotAtSealTempBit = 3;
        private const int DefaultStatusCheckTimeout = 300000; // 5 minutes in milliseconds
        private const int StatusCheckInterval = 1000; // 1 second in milliseconds

        private (bool success, byte status) GetDeviceStatus()
        {
            var (success, response, error) = SendCommand("?", "Getting device status");
            if (!success || string.IsNullOrEmpty(response))
            {
                LogMessage($"Failed to get device status: {error}");
                return (false, 0);
            }

            if (byte.TryParse(response, System.Globalization.NumberStyles.HexNumber, null, out byte status))
            {
                return (true, status);
            }

            LogMessage($"Failed to parse status response: {response}");
            return (false, 0);
        }

        private bool IsBitSet(byte status, int bitPosition)
        {
            return (status & (1 << bitPosition)) != 0;
        }

        private bool IsTemperatureReady()
        {
            if (!EnsurePortOpen())
                return false;

            _serialPort.Write("?\r");
            System.Threading.Thread.Sleep(150);
            string response = _serialPort.ReadExisting().Trim();

            if (string.IsNullOrEmpty(response))
                return false;

            if (byte.TryParse(response, System.Globalization.NumberStyles.HexNumber, null, out byte status))
            {
                return !IsBitSet(status, NotAtSealTempBit);
            }

            return false;
        }

        private void WaitForTemperatureReady(int timeoutMs = DefaultStatusCheckTimeout)
        {
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);

            while (!IsTemperatureReady())
            {
                if (DateTime.Now - startTime > timeout)
                {
                    throw new TimeoutException("Timeout waiting for temperature to reach target");
                }

                LogMessage("Temperature not ready, waiting...");
                System.Threading.Thread.Sleep(StatusCheckInterval);
            }
        }

        private readonly SerialPort _serialPort;
        private bool _isInitialized = false;

        public ALPSDriver() : base(new WrappedSerialPort())
        {
            _serialPort = new SerialPort();
            _serialPort.BaudRate = BaudRate;
            _serialPort.DataBits = DataBits;
            _serialPort.Parity = PortParity;
            _serialPort.StopBits = PortStopBits;
            _serialPort.Handshake = FlowControl;
            _serialPort.ReadTimeout = ReadTimeout;
            _serialPort.WriteTimeout = WriteTimeout;
            _serialPort.Encoding = System.Text.Encoding.ASCII;

            // Add Default Connection Parameters
            ConnectionParameters[_connectionPort] = "13";  // Enter port number only (e.g. "13" for COM13)

            // Add operations with default parameters
            var connectParameters = new SortedList();
            Operations.Add(_operationConnect, connectParameters);

            var sealingParameters = new SortedList
            {
                { _parameterTemperature, DefaultTemperature },
                { _parameterSealTime, DefaultSealTime },
                { _parameterSealForce, DefaultSealForce },
                { _parameterSealLength, DefaultSealLength }
            };
            Operations.Add(_operationStartSealing, sealingParameters);
        }

        private bool EnsurePortOpen()
        {
            if (_serialPort.IsOpen)
                return true;

            try
            {
                LogMessage($"Opening port {_serialPort.PortName}");
                _serialPort.Open();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                LogMessage($"Access denied to port {_serialPort.PortName}. Make sure no other program is using it.");
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to open port {_serialPort.PortName}: {ex.Message}");
                return false;
            }
        }

        private void ClosePort()
        {
            if (_serialPort.IsOpen)
            {
                LogMessage("Closing port");
                _serialPort.Close();
                System.Threading.Thread.Sleep(100); // Wait for port to close
            }
        }

        private (bool isBusy, string? error) CheckDeviceStatus()
        {
            if (!EnsurePortOpen())
                return (true, "Could not open port");

            _serialPort.Write("?\r");
            System.Threading.Thread.Sleep(150);
            string response = _serialPort.ReadExisting().Trim();

            if (string.IsNullOrEmpty(response))
                return (true, "No response from device");

            if (!byte.TryParse(response, System.Globalization.NumberStyles.HexNumber, null, out byte status))
                return (true, $"Invalid status response: {response}");

            bool isBusy = (status & (1 << BusyBit)) != 0;
            if (isBusy)
            {
                // Check which conditions are causing the busy state
                var conditions = new System.Text.StringBuilder();
                for (int i = 0; i < StatusBits.Length; i++)
                {
                    if ((status & (1 << i)) != 0 && i != 0) // Skip "No Fail" bit
                    {
                        if (conditions.Length > 0) conditions.Append(", ");
                        conditions.Append(StatusBits[i].description);
                    }
                }
                return (true, conditions.Length > 0 ? conditions.ToString() : "Device busy");
            }

            return (false, null);
        }

        private void WaitForDeviceReady(int timeoutMs = DefaultStatusCheckTimeout)
        {
            var startTime = DateTime.Now;
            var lastMessage = "";

            while (true)
            {
                var (isBusy, statusMessage) = CheckDeviceStatus();
                if (!isBusy)
                    return;

                if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
                {
                    throw new TimeoutException($"Timeout waiting for device to become ready. Last status: {statusMessage}");
                }

                // Only log if status message changed
                if (statusMessage != lastMessage)
                {
                    LogMessage($"Device status: {statusMessage}");
                    lastMessage = statusMessage ?? "";
                }

                System.Threading.Thread.Sleep(StatusCheckInterval);
            }
        }

        private (bool success, string? response, string? error) SendCommand(string command, string description)
        {
            try
            {
                LogMessage($"{description} - Sending command: {command}");

                if (!EnsurePortOpen())
                {
                    return (false, null, $"Could not open port {_serialPort.PortName}");
                }

                // Wait for device to be ready before sending command
                try
                {
                    LogMessage("Checking device status before sending command...");
                    WaitForDeviceReady();
                }
                catch (TimeoutException ex)
                {
                    return (false, null, ex.Message);
                }

                // Send command with CR
                LogMessage("Writing command to port");
                _serialPort.Write(command + "\r");

                // Wait for response
                LogMessage("Waiting for response...");
                System.Threading.Thread.Sleep(150);

                string response = _serialPort.ReadExisting().Trim();
                LogMessage($"Raw response: '{response}'");

                if (string.IsNullOrEmpty(response))
                {
                    return (false, null, "No response received from the device");
                }

                return (true, response, null);
            }
            catch (Exception ex)
            {
                return (false, null, $"Command failed: {ex.Message}");
            }
        }

        public new void Connect()
        {
            try
            {
                LogMessage("Starting connection sequence...");
                OpenConnection();
                InitializeDevice();
                currentCommand = _operationConnect;
                ExecuteOperation();
            }
            catch (Exception ex)
            {
                LogMessage($"Connection sequence failed: {ex.Message}");
                throw;
            }
        }

        public void StartSealing()
        {
            try
            {
                LogMessage("Starting sealing operation...");
                currentCommand = _operationStartSealing;
                ExecuteOperation();
            }
            catch (Exception ex)
            {
                LogMessage($"Sealing operation failed: {ex.Message}");
                throw;
            }
        }

        protected override void OpenConnection()
        {
            LogMessage("Starting OpenConnection...");

            // Get the COM port parameter
            if (!ConnectionParameters.ContainsKey(_connectionPort))
            {
                throw new Exception($"Connection Parameter not found: {_connectionPort}");
            }

            var comPortValue = ConnectionParameters[_connectionPort];
            if (comPortValue == null || string.IsNullOrWhiteSpace(comPortValue.ToString()))
            {
                throw new Exception($"Connection Parameter cannot be null or empty: {_connectionPort}");
            }

            var comPort = FormatComPort(comPortValue.ToString()!);
            _serialPort.PortName = comPort;

            LogMessage($"Using port: {comPort}");

            // Test if port exists
            if (!SerialPort.GetPortNames().Contains(comPort))
            {
                throw new Exception($"COM port {comPort} not found. Available ports: {string.Join(", ", SerialPort.GetPortNames())}");
            }

            // Test the connection
            var (success, _, error) = SendCommand("?", "Testing initial connection");
            if (!success)
            {
                throw new Exception($"Failed to establish connection on {comPort}: {error}");
            }

            LogMessage($"Connection established on {comPort}");
            CommandComplete();
        }

        protected override void InitializeDevice()
        {
            if (_isInitialized)
            {
                LogMessage("Device already initialized, skipping initialization");
                CommandComplete();
                return;
            }

            try
            {
                LogMessage("Starting device initialization...");
                
                // Send initialization command 'I' as per ALPS5000 protocol
                var (success, response, error) = SendCommand("I", "Initializing device");
                if (!success)
                {
                    throw new Exception($"Device initialization failed: {error}");
                }

                // Set initial sealing parameters
                SetSealingParameters();

                _isInitialized = true;
                LogMessage($"Device initialized successfully. Response: {response}");
                CommandComplete();
            }
            catch (Exception ex)
            {
                _isInitialized = false;
                LogMessage($"Initialization failed: {ex.Message}");
                throw;
            }
        }

        private void SetSealingParameters()
        {
            var parameters = Operations[_operationStartSealing] as SortedList;
            if (parameters == null)
            {
                throw new Exception("Sealing parameters not found");
            }

            // Set temperature
            var temp = Convert.ToInt32(parameters[_parameterTemperature]);
            var (success, _, error) = SendCommand($"A{temp:D3}", $"Setting temperature to {temp}°C");
            if (!success)
            {
                throw new Exception($"Failed to set temperature: {error}");
            }

            // Set sealing time
            var time = Convert.ToDecimal(parameters[_parameterSealTime]);
            if (time < MinSealTime || time > MaxSealTime)
            {
                throw new Exception($"Seal time must be between {MinSealTime} and {MaxSealTime} seconds");
            }
            int timeValue = (int)(time * 10);
            (success, _, error) = SendCommand($"B{timeValue:D2}", $"Setting seal time to {time} seconds");
            if (!success)
            {
                throw new Exception($"Failed to set seal time: {error}");
            }

            // Set sealing force
            var force = Convert.ToInt32(parameters[_parameterSealForce]);
            if (force < MinSealForce || force > MaxSealForce)
            {
                throw new Exception($"Seal force must be between {MinSealForce} and {MaxSealForce} Kg");
            }
            (success, _, error) = SendCommand($"PS={force}", $"Setting seal force to {force} Kg");
            if (!success)
            {
                throw new Exception($"Failed to set seal force: {error}");
            }

            // Set seal length
            var length = Convert.ToInt32(parameters[_parameterSealLength]);
            if (length < MinSealLength || length > MaxSealLength)
            {
                throw new Exception($"Seal length must be between {MinSealLength} and {MaxSealLength} mm");
            }
            (success, _, error) = SendCommand($"L={length}", $"Setting seal length to {length} mm");
            if (!success)
            {
                throw new Exception($"Failed to set seal length: {error}");
            }
        }

        protected override void ExecuteOperation()
        {
            switch (currentCommand)
            {
                case _operationConnect:
                    ExecuteConnect();
                    break;
                case _operationStartSealing:
                    ExecuteStartSealing();
                    break;
                default:
                    throw new Exception($"Error: Unrecognized Command: {currentCommand}");
            }
            CommandComplete();
        }

        public void Disconnect()
        {
            AbortDevice();
        }

        protected override void AbortDevice()
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                _isInitialized = false;
                LogMessage("Device connection aborted");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error during abort: {ex.Message}");
            }
        }

        private void ExecuteConnect()
        {
            try
            {
                // Get device status after connection
                var (success, response, error) = SendCommand("?", "Getting device status");
                if (!success)
                {
                    throw new Exception($"Failed to get device status: {error}");
                }

                // Parse status bits
                if (byte.TryParse(response, System.Globalization.NumberStyles.HexNumber, null, out byte status))
                {
                    LogMessage("\nStatus bits:");
                    for (int i = 0; i < StatusBits.Length; i++)
                    {
                        bool isSet = (status & (1 << i)) != 0;
                        if (isSet)
                        {
                            LogMessage($"- {StatusBits[i].description}");
                        }
                    }
                }

                LogMessage($"Connect command executed. Status: {response}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Connect command failed: {ex.Message}");
            }
        }

        private string FormatComPort(string portInput)
        {
            if (!int.TryParse(portInput, out int portNumber))
            {
                throw new Exception($"Port number must be a valid number, got: {portInput}");
            }
            return $"COM{portNumber}";
        }

        private void WaitForSealingComplete(int timeoutMs = 30000)
        {
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);

            while (true)
            {
                if (DateTime.Now - startTime > timeout)
                {
                    throw new TimeoutException("Timeout waiting for sealing operation to complete");
                }

                var (success, response, error) = SendCommand("?", "Checking sealing status");
                if (!success)
                {
                    throw new Exception($"Failed to check sealing status: {error}");
                }

                if (byte.TryParse(response, System.Globalization.NumberStyles.HexNumber, null, out byte status))
                {
                    // If device is not busy and no error bits are set, sealing is complete
                    bool isBusy = (status & (1 << BusyBit)) != 0;
                    bool hasError = (status & (1 << 1)) != 0;  // Error bit
                    
                    if (!isBusy)
                    {
                        if (hasError)
                        {
                            var errorDesc = new System.Text.StringBuilder("Sealing failed. Issues: ");
                            for (int i = 0; i < StatusBits.Length; i++)
                            {
                                if ((status & (1 << i)) != 0 && i != 0)
                                {
                                    errorDesc.Append(StatusBits[i].description).Append(", ");
                                }
                            }
                            throw new Exception(errorDesc.ToString().TrimEnd(' ', ','));
                        }
                        return; // Sealing completed successfully
                    }
                }

                System.Threading.Thread.Sleep(500); // Check every 500ms
            }
        }

        private void ExecuteStartSealing()
        {
            try
            {
                LogMessage("Checking device status before sealing...");
                WaitForDeviceReady();

                LogMessage("Verifying and setting sealing parameters...");
                SetSealingParameters();

                LogMessage("Waiting for temperature to stabilize...");
                WaitForTemperatureReady();

                // Start sealing
                var (success, response, error) = SendCommand("S", "Starting sealing operation");
                if (!success)
                {
                    throw new Exception($"Failed to start sealing: {error}");
                }

                if (response?.ToLower() == ResponseOK)
                {
                    LogMessage("Sealing operation started successfully");
                    LogMessage("Waiting for sealing operation to complete...");
                    WaitForSealingComplete();
                    LogMessage("Sealing operation completed successfully");
                }
                else if (response?.ToLower() == ResponseError)
                {
                    throw new Exception("Device rejected sealing command. Check temperature and device status.");
                }
                else
                {
                    throw new Exception($"Unexpected response: {response}");
                }
            }
            catch (TimeoutException ex)
            {
                throw new Exception($"Sealing operation timed out: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Sealing operation failed: {ex.Message}");
            }
        }
    }
}
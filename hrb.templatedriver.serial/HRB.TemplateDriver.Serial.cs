using System;
using System.Collections;
using Cellario.DeviceBase;
using System.IO.Ports;


namespace HRB
{
    public partial class TemplateDriver : SerialDeviceDriver
    {

        // =========================================================
        // Connection Parameters

        // Example
        private const string _connectionExample = "Example";

        // ExampleOptional
        private const string _connectionExampleOptional = "ExampleOptional";

        // Port
        private const string _connectionPort = "Port";

        // =========================================================
        // Operations

        // ExampleOpp
        private const string _operationExample = "ExampleOpp";

        // =========================================================
        // Parameters

        // ExampleParam
        private const string _parameterExample = "ExampleParam";

        public TemplateDriver() : this(new WrappedSerialPort()) {}

        public TemplateDriver(IWrappedSerialPort serialPort) : base(serialPort)
        {
            //Change the encoding of the messages here
            serialPort.Encoding = System.Text.Encoding.UTF8;
            // Add Default Connection Parameters
            ConnectionParameters[_connectionPort] = "9";
            ConnectionParameters[_connectionExample] = "Default Value";
            

            // Add Default Operation Parameters
            var operationParameters = new SortedList
            {
                {_parameterExample, "Default"}
            };
            Operations.Add(_operationExample, operationParameters);
        }

        [STAThread]
        public static void Main()
        {
            // Starts driver when running as a standalone. Not used when running as a .dll
            DeviceHelper.RunDriver(new TemplateDriver());
        }

        // These Methods override the methods in devicebase.
        protected override void OpenConnection()
        {

            // Set Serial communication setting these settings can be removed if you are using these defaults.
            BaudRate = 9600;
            StopBits = StopBits.None;
            DataBits = 8;
            Parity = Parity.None;

            // This is the message terminator that gets appended to the end of the command. 
            EndOfMessage = '\n';

            base.OpenConnection();
        }

        // Initializes a connection to the device. Overrides SerialDeviceDriver. 
        // Might also initialize the device in some way (eg. call a homing function)(Edit this line with device-specific info!)
        protected override void InitializeDevice()
        {
            // Get a required parameter. Throws exception if values are null, empty, or whitespace

            if (!ConnectionParameters.ContainsKey(_connectionExample) || String.IsNullOrWhiteSpace(ConnectionParameters[_connectionExample]?.ToString()))
            {
                throw new Exception($"Connection parameter cannot be null: {_connectionExample}");
            }
            var requiredParameter = ConnectionParameters[_connectionExample].ToString();

            LogMessage(DriverLogLevel.Debug, $"Connection Parameter Require = {requiredParameter}");

            // Get an optional parameter. Assigns to default value if parameter is null, empty, or whitespace

            var optionalParameter = "Default Value";

            if (ConnectionParameters.ContainsKey(_connectionExampleOptional) && !String.IsNullOrWhiteSpace(ConnectionParameters[_connectionExampleOptional]?.ToString()))
            {
                optionalParameter = ConnectionParameters[_connectionExampleOptional].ToString();
            }

            LogMessage(DriverLogLevel.Debug, $"Connection Parameter Optional = {optionalParameter}");

            // Run any initialization functions
            SendCommand("Initialize");

            string reply = GetReply();
            if (reply.Contains("Error"))
            {
                throw new Exception("Device Initilization failed");
            }

            CommandComplete();
        }

        /// Switch statement which calls device-specific methods. Overrides SerialDeviceDriver.
        protected override void ExecuteOperation()
        {
            // Parse and setup operation parameters, then send to operation

            var operationParameters = Operations[currentCommand] as SortedList;

            switch (currentCommand)
            {
                // Only call functions here, no logic
                case _operationExample:
                    DoSomething(operationParameters);
                    break;
                default:
                    throw new Exception($"Error: Unrecognized Command: {currentCommand}");
            }
            CommandComplete();
        }

        // Terminates connection to the device. Overrides SerialDeviceDriver.
        protected override void AbortDevice()
        {
            if (DeviceState == enumDeviceState.DoingOp)
            {
                SendCommand("Abort");
                string reply = GetReply();
                if (reply.Contains("Error"))
                {
                    throw new Exception("Device abort failed");
                }
            }
            base.AbortDevice();
        }

        // Replace this sample funciton with your own device-specific functions.
        public void DoSomething(SortedList operationParameters)
        {
            // Get required parameters. Throws exception if values are null, empty, or whitespace

            if (!operationParameters.ContainsKey(_parameterExample) || String.IsNullOrWhiteSpace(operationParameters[_parameterExample]?.ToString()))
            {
                throw new Exception($"Operation parameter cannot be null: {_parameterExample}");
            }

            var userInput = Int32.Parse(operationParameters[_parameterExample].ToString());

            // Use Component to communicate with device
            SendCommand($"DoSomething {userInput}");
            var deviceOutput = GetReply();

            // Check result of operation
            if(deviceOutput.Contains("Success") == false)
                throw new Exception($"Error in DoSomething, Device response: {deviceOutput}");

            // Logging should be done at the appropriate level, Debug, warning etc.
            LogMessage(DriverLogLevel.Info, "Something has been done");
        }
    }
}

using System;
using System.IO.Ports;

namespace alpsCli
{
	class Program
	{
		// Error code mappings
		private static readonly Dictionary<int, string> ErrorCodes = new()
		{
			{ 1, "VERTICAL_SHUTTLE_DOWN" },
			{ 2, "HEATER_UP" },
			{ 3, "SHUTTLE_IN" },
			{ 4, "CUTTER" },
			{ 5, "THERMOCOUPLE" },
			{ 6, "OVERHEATING" },
			{ 7, "NO_FOIL" },
			{ 8, "NO_PLATE" },
			{ 9, "FORCE_SENSOR" }
		};

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
		// Global variable for COM port
		private static string Com_Port = "COM4";

		// Serial port configuration
		private static readonly int BaudRate = 9600;
		private static readonly int DataBits = 8;
		private static readonly Parity PortParity = Parity.None;
		private static readonly StopBits StopBits = StopBits.One;
		private static readonly Handshake FlowControl = Handshake.None;
		private static readonly int ReadTimeout = 5000;
		private static readonly int WriteTimeout = 5000;

		/// <summary>
		/// Sends a command to the device and returns the response
		/// </summary>
		/// <param name="command">Command to send (without CR)</param>
		/// <param name="description">Description for logging</param>
		/// <returns>Tuple containing success flag, response (if successful), and error message (if failed)</returns>
		private static string FormatErrorResponse(string code)
		{
			if (int.TryParse(code, out int errorCode) && ErrorCodes.TryGetValue(errorCode, out string? errorDesc))
			{
				return $"Code: {code}, Error: {errorDesc}";
			}
			return $"Code: {code}, Error: Unknown error code";
		}

		private static string FormatTemperatureResponse(string response)
		{
			if (int.TryParse(response, out int temp))
			{
				return $"Temperature: {temp}°C";
			}
			return $"Invalid temperature value: {response}";
		}

		private static string FormatSealingTimeResponse(string response)
		{
			if (decimal.TryParse(response, out decimal time))
			{
				return $"Sealing time: {time:F1} seconds";
			}
			return $"Invalid sealing time value: {response}";
		}

		private static string FormatSealingForceResponse(string response)
		{
			if (int.TryParse(response, out int force))
			{
				return $"Sealing force: {force} Kg";
			}
			return $"Invalid sealing force value: {response}";
		}

		private static string FormatSealLengthResponse(string response)
		{
			if (int.TryParse(response, out int length))
			{
				return $"Seal length: {length} mm";
			}
			return $"Invalid seal length value: {response}";
		}

		private static string FormatDriveDistanceResponse(string response)
		{
			if (decimal.TryParse(response, out decimal distance))
			{
				return $"Drive distance: {distance:F3} mm";
			}
			return $"Invalid drive distance value: {response}";
		}

		private static (bool success, string? response, string? error) SendCommand(string command, string description)
		{
			try
			{
				using (SerialPort serialPort = new SerialPort(Com_Port, BaudRate, PortParity, DataBits, StopBits))
				{
					serialPort.Handshake = FlowControl;
					serialPort.Encoding = System.Text.Encoding.ASCII;
					serialPort.ReadTimeout = ReadTimeout;
					serialPort.WriteTimeout = WriteTimeout;

					serialPort.Open();
					Console.WriteLine($"{description} on {Com_Port}...");
					
					// Send command with CR
					serialPort.Write(command + "\r");
					
					// Wait briefly to allow device to respond
					System.Threading.Thread.Sleep(150);
					
					string response = serialPort.ReadExisting().Trim();
					serialPort.Close();
					
					if (string.IsNullOrEmpty(response))
					{
						return (false, null, "No response received from the device.");
					}
					
					return (true, response, null);
				}
			}
			catch (TimeoutException)
			{
				return (false, null, "Timed out waiting for response from the device.");
			}
			catch (Exception ex)
			{
				return (false, null, $"Error: {ex.Message}");
			}
		}

		static void Main(string[] args)
		{
			if (args.Length > 0)
			{
				ProcessCommand(args);
				return;
			}

			Console.WriteLine("alpsCli Interactive Mode. Type 'exit' to quit. Type 'help' for commands.");
			while (true)
			{
				Console.Write("> ");
				var input = Console.ReadLine();
				if (input == null)
					continue;
				if (input.Trim().ToLower() == "exit")
					break;
				if (string.IsNullOrWhiteSpace(input))
					continue;
				var cmdArgs = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				ProcessCommand(cmdArgs);
			}
		}

		static void ProcessCommand(string[] args)
		{
			if (args.Length == 0 || args[0] == "--help" || args[0] == "-h" || args[0].ToLower() == "help")
			{
				ShowHelp();
				return;
			}
			switch (args[0].ToLower())
			{
				case "setcom":
					SetComPort(args);
					break;
				case "showcom":
					ShowComPort();
					break;
				case "connect":
					Connect();
					break;
				case "status":
					GetStatus();
					break;
				case "temp":
					GetTemperature();
					break;
				case "settemp":
					SetTemperature(args);
					break;
				case "actualtemp":
					GetActualTemperature();
					break;
				case "seal":
					StartSealing();
					break;
				case "sealtime":
					if (args.Length > 1)
						SetSealingTime(args);
					else
						GetSealingTime();
					break;
				case "sealforce":
					if (args.Length > 1)
						SetSealingForce(args);
					else
						GetSealingForce();
					break;
				case "enableforce":
					EnableForceSensor();
					break;
				case "disableforce":
					DisableForceSensor();
					break;
				case "seallength":
					if (args.Length > 1)
						SetSealLength(args);
					else
						GetSealLength();
					break;
				case "driveon":
					if (args.Length > 1)
						SetDriveDistance(args);
					else
						GetDriveDistance();
					break;
				case "shuttlein":
					ShuttleIn();
					break;
				case "shuttleout":
					ShuttleOut();
					break;
				case "version":
					GetVersion();
					break;
				default:
					Console.WriteLine($"Unknown command: {args[0]}");
					ShowHelp();
					break;
			}
		}

		static void Connect()
		{
			var result = SendCommand("I", "Initializing device");
			if (result.success && result.response != null)
			{
				Console.WriteLine($"Response: {result.response}");
			}
			else if (result.error != null)
			{
				Console.WriteLine(result.error);
			}
			else
			{
				Console.WriteLine("Unknown error occurred");
			}
		}

		static void SetComPort(string[] args)
		{
			if (args.Length < 2)
			{
				Console.WriteLine("Usage: setcom [COM_PORT]");
				return;
			}
			Com_Port = args[1];
			Console.WriteLine($"COM port set to: {Com_Port}");
		}

		static void ShowComPort()
		{
			if (string.IsNullOrEmpty(Com_Port))
				Console.WriteLine("COM port is not set.");
			else
				Console.WriteLine($"Current COM port: {Com_Port}");
		}

		static void ShowHelp()
		{
			Console.WriteLine("ALPS5000 CLI - Command Line Tool");
			Console.WriteLine("\nDevice Setup:");
			Console.WriteLine("  setcom [COM_PORT] - Set the COM port (e.g., setcom COM3)");
			Console.WriteLine("  showcom           - Show the current COM port");
			Console.WriteLine("  connect           - Initialize the device");
			Console.WriteLine("  status            - Get device status");
			Console.WriteLine("\nTemperature Commands:");
			Console.WriteLine("  temp              - Get current temperature setpoint");
			Console.WriteLine("  settemp [TEMP]    - Set sealing temperature (in °C)");
			Console.WriteLine("  actualtemp        - Get actual sealing temperature");
			Console.WriteLine("\nSealing Commands:");
			Console.WriteLine("  seal              - Start sealing operation");
			Console.WriteLine("  sealtime [TIME]   - Get/Set sealing time (0-9.9 seconds)");
			Console.WriteLine("  sealforce [FORCE] - Get/Set sealing force (5-50 Kg)");
			Console.WriteLine("  seallength [LEN]  - Get/Set seal length (110-128 mm)");
			Console.WriteLine("\nForce Sensor Commands:");
			Console.WriteLine("  enableforce       - Enable force sensor");
			Console.WriteLine("  disableforce      - Disable force sensor");
			Console.WriteLine("\nShuttle Commands:");
			Console.WriteLine("  shuttlein         - Move shuttle inside");
			Console.WriteLine("  shuttleout        - Move shuttle to load position");
			Console.WriteLine("  driveon [DIST]    - Get/Set drive-on distance (mm)");
			Console.WriteLine("\nSystem Commands:");
			Console.WriteLine("  version           - Get software version");
			Console.WriteLine("  help              - Show this help message");
			Console.WriteLine("  exit              - Exit interactive mode");
		}

		static void GetStatus()
		{
			var result = SendCommand("?", "Getting device status");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}

			if (result.response == null)
			{
				Console.WriteLine("No response received");
				return;
			}

			Console.WriteLine($"Raw status: {result.response}");
			if (byte.TryParse(result.response, System.Globalization.NumberStyles.HexNumber, null, out byte status))
			{
				Console.WriteLine("\nStatus bits:");
				for (int i = 0; i < StatusBits.Length; i++)
				{
					bool isSet = (status & (1 << i)) != 0;
					if (isSet)
					{
						Console.WriteLine($"- {StatusBits[i].description}");
					}
				}
			}
			else
			{
				Console.WriteLine("Error: Could not parse status response as hexadecimal.");
			}
		}

		static void GetTemperature()
		{
			var result = SendCommand("C", "Reading current temperature");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine(FormatTemperatureResponse(result.response ?? ""));
		}

		static void StartSealing()
		{
			var result = SendCommand("S", "Starting sealing operation");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}

			if (result.response?.ToLower() == "ok")
			{
				Console.WriteLine("Sealing operation started successfully");
			}
			else if (result.response?.ToLower() == "er")
			{
				Console.WriteLine("Error: Unable to start sealing operation. Check device status.");
			}
			else
			{
				Console.WriteLine($"Unexpected response: {result.response}");
			}
		}

		static void SetTemperature(string[] args)
		{
			if (args.Length < 2 || !int.TryParse(args[1], out int temp) || temp < 0)
			{
				Console.WriteLine("Usage: settemp [TEMPERATURE] (temperature in °C)");
				return;
			}
			var result = SendCommand($"A{temp:D3}", $"Setting sealing temperature to {temp}°C");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine($"Sealing temperature set to {temp}°C");
		}

		static void GetActualTemperature()
		{
			var result = SendCommand("F", "Reading actual sealing temperature");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine(FormatTemperatureResponse(result.response ?? ""));
		}

		static void GetSealingTime()
		{
			var result = SendCommand("D", "Reading sealing time");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine(FormatSealingTimeResponse(result.response ?? ""));
		}

		static void SetSealingTime(string[] args)
		{
			if (args.Length < 2)
			{
				Console.WriteLine("Usage: sealtime [TIME] (0-9.9 seconds)");
				return;
			}

			if (!decimal.TryParse(args[1], out decimal time))
			{
				Console.WriteLine("Invalid time format. Please enter a number between 0 and 9.9");
				return;
			}

			if (time < 0 || time > (decimal)9.9)
			{
				Console.WriteLine("Time must be between 0 and 9.9 seconds");
				return;
			}
			
			// Convert time to 2-digit format (e.g., 2.5 seconds becomes "25")
			int timeValue = (int)(time * 10);
			
			var result = SendCommand($"B{timeValue:D2}", $"Setting sealing time to {time} seconds");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine($"Sealing time set to {time} seconds");
		}

		static void GetSealingForce()
		{
			var result = SendCommand("PS", "Reading sealing force");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine(FormatSealingForceResponse(result.response ?? ""));
		}

		static void SetSealingForce(string[] args)
		{
			if (args.Length < 2 || !int.TryParse(args[1], out int force) || force < 5 || force > 50)
			{
				Console.WriteLine("Usage: sealforce [FORCE] (5-50 Kg)");
				return;
			}
			var result = SendCommand($"PS={force}", $"Setting sealing force to {force} Kg");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine("Sealing force set successfully");
		}

		static void EnableForceSensor()
		{
			var result = SendCommand("FS=1", "Enabling force sensor");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine("Force sensor enabled");
		}

		static void DisableForceSensor()
		{
			var result = SendCommand("FS=0", "Disabling force sensor");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine("Force sensor disabled");
		}

		static void GetSealLength()
		{
			var result = SendCommand("SL", "Reading seal length");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine(FormatSealLengthResponse(result.response ?? ""));
		}

		static void SetSealLength(string[] args)
		{
			if (args.Length < 2 || !int.TryParse(args[1], out int length) || length < 110 || length > 128)
			{
				Console.WriteLine("Usage: seallength [LENGTH] (110-128 mm)");
				return;
			}
			var result = SendCommand($"L={length}", $"Setting seal length to {length} mm");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine("Seal length set successfully");
		}

		static void GetDriveDistance()
		{
			var result = SendCommand("DO", "Reading drive-on distance");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine(FormatDriveDistanceResponse(result.response ?? ""));
		}

		static void SetDriveDistance(string[] args)
		{
			if (args.Length < 2 || !decimal.TryParse(args[1], out decimal distance) || distance < 0)
			{
				Console.WriteLine("Usage: driveon [DISTANCE] (in mm)");
				return;
			}
			var result = SendCommand($"DO={distance:F3}", $"Setting drive-on distance to {distance} mm");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine("Drive-on distance set successfully");
		}

		static void ShuttleIn()
		{
			var result = SendCommand("SI", "Moving shuttle inside");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine("Shuttle moved inside");
		}

		static void ShuttleOut()
		{
			var result = SendCommand("SO", "Moving shuttle to load position");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine("Shuttle moved to load position");
		}

		static void GetVersion()
		{
			var result = SendCommand("V", "Reading software version");
			if (!result.success)
			{
				Console.WriteLine(result.error ?? "Unknown error occurred");
				return;
			}
			Console.WriteLine($"Software version: {result.response}");
		}

		static void Greet(string[] args)
		{
			if (args.Length < 2)
			{
				Console.WriteLine("Please provide a name to greet.");
				return;
			}
			Console.WriteLine($"Hello, {args[1]}!");
		}
	}
}

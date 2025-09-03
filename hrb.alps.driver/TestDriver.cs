using System;
using System.Collections;
using System.IO.Ports;
using HRB;

class TestDriver
{
    static void Main()
    {
        Console.WriteLine("ALPS Driver Test");
        Console.WriteLine("--------------");

        // Show available ports
        Console.WriteLine("\nAvailable COM ports:");
        foreach (var port in SerialPort.GetPortNames())
        {
            Console.WriteLine($"  - {port}");
        }

        try
        {
            // Create driver instance
            var driver = new ALPSDriver();

            // Get COM port from user
            Console.Write("\nEnter COM port to use (press Enter for COM4): ");
            string? comPort = Console.ReadLine()?.Trim().ToUpper();
            if (string.IsNullOrEmpty(comPort))
            {
                comPort = "COM4";
            }

            // Set the COM port
            driver.ConnectionParameters["Port"] = comPort;
            Console.WriteLine($"\nAttempting to connect to ALPS on {comPort}");

            // Connect to the device
            driver.Connect();
            Console.WriteLine("Device connected and initialized successfully");

            // Show default sealing parameters
            Console.WriteLine("\nDefault Sealing Parameters:");
            Console.WriteLine($"Temperature: 165°C");
            Console.WriteLine($"Seal Time: 3.0 seconds");
            Console.WriteLine($"Seal Force: 50 Kg");
            Console.WriteLine($"Seal Length: 125 mm");

            // Menu loop
            bool exit = false;
            while (!exit)
            {
                Console.WriteLine("\nCommands:");
                Console.WriteLine("1. Start Sealing (using current parameters)");
                Console.WriteLine("2. View Current Parameters");
                Console.WriteLine("3. Modify Parameters");
                Console.WriteLine("4. Disconnect and Exit");
                Console.Write("\nEnter command number: ");

                string? input = Console.ReadLine()?.Trim();
                Console.WriteLine();

                switch (input)
                {
                    case "1":
                        try
                        {
                            Console.WriteLine("Starting sealing operation...");
                            driver.StartSealing();
                            Console.WriteLine("Sealing operation completed successfully");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Sealing failed: {ex.Message}");
                        }
                        break;

                    case "2":
                        var parameters = driver.Operations["StartSealing"] as Hashtable;
                        if (parameters != null)
                        {
                            Console.WriteLine("Current Sealing Parameters:");
                            Console.WriteLine($"Temperature: {parameters["Temperature"]}°C");
                            Console.WriteLine($"Seal Time: {parameters["SealTime"]} seconds");
                            Console.WriteLine($"Seal Force: {parameters["SealForce"]} Kg");
                            Console.WriteLine($"Seal Length: {parameters["SealLength"]} mm");
                        }
                        break;

                    case "3":
                        ModifyParameters(driver);
                        break;

                    case "4":
                        exit = true;
                        break;

                    default:
                        Console.WriteLine("Invalid command. Please try again.");
                        break;
                }
            }

            // Clean up
            driver.Disconnect();
            Console.WriteLine("\nConnection closed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static void ModifyParameters(ALPSDriver driver)
    {
        var parameters = driver.Operations["StartSealing"] as Hashtable;
        if (parameters == null)
        {
            Console.WriteLine("Error: Could not access sealing parameters");
            return;
        }

        Console.WriteLine("Enter new values (press Enter to keep current value):");

        // Temperature
        Console.Write($"Temperature (current: {parameters["Temperature"]}°C): ");
        string? input = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(input) && int.TryParse(input, out int temp) && temp > 0)
        {
            parameters["Temperature"] = temp;
        }

        // Seal Time
        Console.Write($"Seal Time (current: {parameters["SealTime"]} seconds, range 0-9.9): ");
        input = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(input) && decimal.TryParse(input, out decimal time) && time >= 0 && time <= 9.9M)
        {
            parameters["SealTime"] = time;
        }

        // Seal Force
        Console.Write($"Seal Force (current: {parameters["SealForce"]} Kg, range 5-50): ");
        input = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(input) && int.TryParse(input, out int force) && force >= 5 && force <= 50)
        {
            parameters["SealForce"] = force;
        }

        // Seal Length
        Console.Write($"Seal Length (current: {parameters["SealLength"]} mm, range 110-128): ");
        input = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(input) && int.TryParse(input, out int length) && length >= 110 && length <= 128)
        {
            parameters["SealLength"] = length;
        }

        Console.WriteLine("\nParameters updated. Use 'View Current Parameters' to verify.");
    }
}
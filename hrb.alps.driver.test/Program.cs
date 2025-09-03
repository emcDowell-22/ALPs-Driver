using System;
using System.IO.Ports;
using HRB;

namespace ALPS.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ALPS Driver Test Program");
            Console.WriteLine("----------------------");

            // Show available ports
            Console.WriteLine("\nAvailable COM ports:");
            foreach (var port in SerialPort.GetPortNames())
            {
                Console.WriteLine($"  - {port}");
            }

            try
            {
                // Create an instance of the driver
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

                // Wait for user input before disconnecting
                Console.WriteLine("\nPress any key to disconnect...");
                Console.ReadKey();

                // Clean up
                driver.Disconnect();
                Console.WriteLine("Connection closed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
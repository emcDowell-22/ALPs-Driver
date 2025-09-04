using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace HRB
{
    class TestDriver
    {
        static void ShowDefaultParameters()
        {
            Console.WriteLine("\nDefault Parameters:");
            Console.WriteLine("Temperature: 165°C");
            Console.WriteLine("Seal Time: 3.0 seconds (range: 0-9.9)");
            Console.WriteLine("Seal Force: 50 Kg (range: 5-50)");
            Console.WriteLine("Seal Length: 125 mm (range: 110-128)");
        }

        static void ModifyParameters(ALPSDriver driver)
        {
            var parameters = driver.Operations[driver.StartSealingOperationName] as SortedList;
            if (parameters == null)
            {
                Console.WriteLine("Error: Could not access sealing parameters");
                return;
            }

            Console.WriteLine("\nCurrent Parameters:");
            foreach (DictionaryEntry param in parameters)
            {
                Console.WriteLine($"{param.Key}: {param.Value}");
            }

            Console.WriteLine("\nEnter parameter to modify (Temperature, SealTime, SealForce, SealLength) or press Enter to skip:");
            string? input = Console.ReadLine();
            
            while (!string.IsNullOrEmpty(input))
            {
                if (parameters.Contains(input))
                {
                    Console.WriteLine($"Enter new value for {input}:");
                    string? value = Console.ReadLine();
                    if (!string.IsNullOrEmpty(value))
                    {
                        try
                        {
                            if (input == "SealTime")
                            {
                                parameters[input] = decimal.Parse(value);
                            }
                            else
                            {
                                parameters[input] = int.Parse(value);
                            }
                            Console.WriteLine($"{input} updated to {value}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error updating parameter: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Invalid parameter name");
                }

                Console.WriteLine("\nEnter parameter to modify or press Enter to finish:");
                input = Console.ReadLine();
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("ALPS 5000 Driver Test Application");
            ShowDefaultParameters();

            var driver = new ALPSDriver();
            bool needsConnection = true;

            while (true)
            {
                try
                {
                    if (needsConnection)
                    {
                        Console.WriteLine("\nEnter COM port number (e.g., 13 for COM13):");
                        string? portInput = Console.ReadLine();
                        if (string.IsNullOrEmpty(portInput))
                        {
                            Console.WriteLine("Port number cannot be empty. Please try again.");
                            continue;
                        }

                        driver.ConnectionParameters["Port"] = portInput;

                        try
                        {
                            driver.Connect();
                            Console.WriteLine("Connection and initialization successful!");
                            needsConnection = false;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"\nError: {ex.Message}");
                            Console.WriteLine("Press Enter to try again or 'a' to abort...");
                            var key = Console.ReadKey(true);
                            if (key.KeyChar == 'a')
                            {
                                driver.RequestAbort();
                                Console.WriteLine("\nConnection aborted.");
                                continue;
                            }
                        }
                        continue;
                    }

                    Console.WriteLine("\nAvailable Commands:");
                    Console.WriteLine("1. Start Sealing");
                    Console.WriteLine("2. Modify Parameters");
                    Console.WriteLine("3. Show Current Parameters");
                    Console.WriteLine("4. Disconnect and Exit");
                    Console.WriteLine("\nEnter command number (or 'a' to abort):");

                    string? cmd = Console.ReadLine();
                    if (string.IsNullOrEmpty(cmd))
                        continue;

                    switch (cmd.ToLower())
                    {
                        case "1":
                            try
                            {
                                var sealingTask = Task.Run(() => driver.StartSealing());
                                
                                // Monitor for abort while sealing is in progress
                                while (!sealingTask.IsCompleted)
                                {
                                    if (Console.KeyAvailable)
                                    {
                                        var key = Console.ReadKey(true);
                                        if (key.KeyChar == 'a')
                                        {
                                            driver.RequestAbort();
                                            Console.WriteLine("\nAborting sealing operation...");
                                            break;
                                        }
                                    }
                                    Thread.Sleep(100);
                                }

                                try
                                {
                                    sealingTask.Wait(); // Wait for task to complete or throw
                                    Console.WriteLine("Sealing operation completed successfully!");
                                }
                                catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
                                {
                                    Console.WriteLine("\nSealing operation aborted.");
                                    needsConnection = true;
                                }
                                catch (AggregateException ex)
                                {
                                    Console.WriteLine($"\nError during sealing: {ex.InnerException?.Message}");
                                    needsConnection = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"\nError during sealing: {ex.Message}");
                                needsConnection = true;
                            }
                            break;

                        case "2":
                            ModifyParameters(driver);
                        break;

                    case "3":
                            var parameters = driver.Operations[driver.StartSealingOperationName] as SortedList;
                            if (parameters != null)
                            {
                                Console.WriteLine("\nCurrent Parameters:");
                                foreach (DictionaryEntry param in parameters)
                                {
                                    Console.WriteLine($"{param.Key}: {param.Value}");
                                }
                            }
                        break;

                    case "4":
                            driver.Disconnect();
                            return;

                        case "a":
                            driver.RequestAbort();
                            Console.WriteLine("\nOperation aborted. Need to reconnect...");
                            needsConnection = true;
                        break;

                    default:
                            Console.WriteLine("Invalid command");
                        break;
                }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
                    if (ex.Message.Contains("port", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
                    {
                        needsConnection = true;
                    }
                }
            }
        }
    }
}
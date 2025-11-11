using System.Text;

namespace Device_Recording_List
{
    internal class Program
    {
        private static List<Device> devices = new List<Device>();
        static int onlineDevices => devices.Count(d => d.Status == "Online");

        // Shared HTTP client (used elsewhere if needed)
        public static HttpClient client = new HttpClient();

        static async Task Main(string[] args)
        {
            await Run();
            Console.WriteLine("\n\n-----------\nPress any key to exit...");
            Console.ReadKey();
        }

        static async Task Run()
        {
            Console.WriteLine("Path of camera list:");
            string list = Console.ReadLine();
            list = list.Trim('"');

            Console.WriteLine("\nOutput path for CSV report:");
            string outputPath = Console.ReadLine();
            outputPath = outputPath.Trim('"');
            string filename = $"Device_Recording_Status_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            outputPath = Path.Combine(outputPath, filename);
            File.Create(outputPath).Close();

            //get our device list
            LoadDevicesFromCSV(list);

            //get all of the online devices
            await PokeDevices();
            Console.WriteLine($"-----------\n{onlineDevices}/{devices.Count} devices are online.");

            //get our recording status. We can't send a list of online devices since we want a full list
            // so GetRecordingStatus has to filter internally.
            GetRecordingStatus();

            StringBuilder csvOutput = new StringBuilder();
            csvOutput.AppendLine("Site Name,Device Name, Device Status, Recording Status, Recording VMS");
            foreach (var device in devices)
            {
                csvOutput.AppendLine(device.AboutCSV());
            }
            Console.WriteLine($"{devices.Count(d => d.RecordingStatus)}/{onlineDevices} online devices are recording.");

            File.WriteAllText(outputPath, csvOutput.ToString());
            Console.WriteLine($"Report {filename} written to {Path.GetDirectoryName(outputPath)}");
        }

        public static async Task PokeDevices()
        {
            Console.WriteLine($"-----------\nPoking {devices.Count} devices...");
            int i = 1;
            int totalLines = devices.Count;
            using (var progress = new ProgressBar())
            {
                foreach (var device in devices)
                {
                    progress.Report((double)i / totalLines);
                    await device.Poke();
                    i++;
                }
            }
        }

        public static void GetRecordingStatus()
        {
            Console.WriteLine($"-----------\nChecking recording status and apps {onlineDevices} on devices...");
            int i = 1;
            int totalLines = onlineDevices;
            using (var progress = new ProgressBar())
            {
                foreach (var device in devices)
                {
                    //this is kind of stupid; I'd rather filter the list first
                    // but this is cheap, dirty code for now
                    if (device.Status != "Online") continue;

                    progress.Report((double)i / totalLines);
                    device.RecordingStatus = device.CheckRecordingStatus().GetAwaiter().GetResult();
                    device.VMS = device.CheckVMS().GetAwaiter().GetResult();
                    i++;
                }
            }
        }

        private static void LoadDevicesFromCSV(string csvPath)
        {
            try
            {
                devices.Clear();
                string[] lines = File.ReadAllLines(csvPath);
                Console.WriteLine($"Loaded file {Path.GetFileName(csvPath)}");
                Console.Write("-----------\nParsing devices... ");

                using (var progress = new ProgressBar())
                {
                    int totalLines = lines.Length;
                    for (int i = 1; i < lines.Length; i++)
                    {
                        progress.Report((double)i / totalLines);
                        string line = lines[i];
                        if (string.IsNullOrWhiteSpace(line))
                            continue;
                        // CSV format: Site Name, Device Name, IP Address, Port, Username, Password
                        string[] parts = line.Split(',');
                        if (parts.Length >= 6)
                        {
                            // Create device without DeviceModel (retrieved later).
                            Device device = new Device(
                                parts[0].Trim(),
                                parts[1].Trim(),
                                parts[2].Trim(),
                                parts[3].Trim(),
                                parts[4].Trim(),
                                parts[5].Trim());
                            devices.Add(device);
                        }
                    }
                }

                Console.WriteLine($"Done. Parsed {devices.Count} devices.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading CSV: " + ex.Message);
            }
        }
    }
}

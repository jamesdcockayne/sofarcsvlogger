using CsvHelper;
using CsvHelper.Configuration;
using FluentModbus;
using System.Globalization;
using System.IO.Ports;

namespace sofarlogger;

public class Program
{
    internal static async Task Main()
    {
        Console.WriteLine("sofar logger running. Check output csv for data.");

        while (true)
        {

            try
            {
                Console.WriteLine("reading");

                // use custom COM port settings:
                using var client = new ModbusRtuClient()
                {
                    BaudRate = 9600,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                };

                client.Connect("/dev/ttyUSB0", ModbusEndianness.BigEndian);

                var reading =
                    new CSVReading
                    {
                        TimestampUtc = DateTime.UtcNow,
                        running_state = await GetRegisterAsync(client, 0x200),
                        batterySOC = await GetRegisterAsync(client, 0x210),
                        battery_power = SofarReadingToAbsoluteWatts(await GetRegisterAsync(client, 0x20d)),
                        battery_current = await GetRegisterAsync(client, 0x20c) / 100d,
                        battery_cycles = await GetRegisterAsync(client, 0x22c),
                        gridPower = SofarReadingToAbsoluteWatts(await GetRegisterAsync(client, 0x212)),
                        grid_freq = await GetRegisterAsync(client, 0x20c) / 100d,
                        grid_voltage = await GetRegisterAsync(client, 0x20c) / 10d,
                        consumptionWatts = await GetRegisterAsync(client, 0x213) * 10,
                        solarPVWatts = await GetRegisterAsync(client, 0x215) * 10,
                        solarPVAmps = await GetRegisterAsync(client, 0x236) / 100d,
                        today_generation = await GetRegisterAsync(client, 0x218) / 100d,
                        today_exported = await GetRegisterAsync(client, 0x219) / 100d,
                        today_purchase = await GetRegisterAsync(client, 0x21a) / 100d,
                        today_consumption = await GetRegisterAsync(client, 0x21b) / 100d,
                        inverter_temp = await GetRegisterAsync(client, 0x238),
                        inverterHS_temp = await GetRegisterAsync(client, 0x238),
                    };

                await LogCSVReadingAsync(reading);

                Console.WriteLine("Waiting");

                await Task.Delay(millisecondsDelay: 10000);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }
    }

    private static async Task LogCSVReadingAsync(CSVReading reading)
    {
        const string logName = "log.csv";

        bool logExists = File.Exists(logName);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            // Don't write the header again.
            HasHeaderRecord = !logExists,
        };
        using (var stream = File.Open(logName, logExists ? FileMode.Append : FileMode.CreateNew))
        using (var writer = new StreamWriter(stream))
        using (var csv = new CsvWriter(writer, config))
        {
            await csv.WriteRecordsAsync(new[] { reading });
        }
    } 

    private static async Task<ushort> GetRegisterAsync(ModbusClient client, ushort address)
    {
        Memory<ushort> register =
            await client
            .ReadHoldingRegistersAsync<ushort>(
                unitIdentifier: 0x01,
                startingAddress: address,
                count: 1);

        return register.Span[0];
    }

    private static int SofarReadingToAbsoluteWatts(int value)
    {
        if (value > 60000)
        {
            return (65535 - value) * 10;
        }

        return value * -1;
    }

    private class CSVReading
    {
        public DateTime TimestampUtc { get; init; }
        public int running_state { get; init; }
        public int batterySOC { get; init; }
        public int battery_cycles { get; init; }
        public int battery_power { get; init; }
        public int battery_voltage { get; init; }
        public double battery_current { get; init; }
        public int battery_temp { get; init; }
        public int gridPower { get; init; }
        public double grid_voltage { get; init; }
        public double grid_freq { get; init; }
        public int consumptionWatts { get; init; }
        public int solarPVWatts { get; init; }
        public double solarPVAmps { get; init; }
        public double today_generation { get; init; }
        public double today_exported { get; init; }
        public double today_purchase { get; init; }
        public double today_consumption { get; init; }
        public int inverter_temp { get; init; }
        public int inverterHS_temp { get; init; }
    }
}
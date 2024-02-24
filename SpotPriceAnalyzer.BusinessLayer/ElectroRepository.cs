using System.Net.Http.Headers;
using Tibber.Sdk;

namespace SpotPriceAnalyzer.BusinessLayer
{
    public class ElectroRepository
    {
        public async Task<Consumption> GetConsumption(string accessToken, DateOnly from, DateOnly to)
        {
            if (to < from)
            {
                throw new ArgumentOutOfRangeException(nameof(to), "End date cannot be earlier than start date.");
            }

            var fromDays = (to.ToDateTime(TimeOnly.MinValue) - from.ToDateTime(TimeOnly.MinValue)).Days;

            var userAgent = new ProductInfoHeaderValue("My-home-automation-system", "1.2");

            var client = new TibberApiClient(accessToken, userAgent);

            var basicData = await client.GetBasicData();
            var home = basicData.Data.Viewer.Homes.First();
            if (home is null || home.Id is null)
            {
                throw new FormatException("Home or home ID returned from Tibber is null");
            }

            var consumption = await client.GetHomeConsumption(home.Id.Value, EnergyResolution.Hourly, fromDays * 24);

            var dayConsumption = CalculateDayConsumption(consumption, fromDays);

            var averagePrice = dayConsumption.Average(c => c.AveragePrice);
            var totalConsumption = consumption.Sum(c => c.Consumption);
            var totalFixed = averagePrice * totalConsumption;
            var totalCostHourly = dayConsumption.Sum(c => c.CostWithHourlyPrice);

            return new Consumption()
            {
                DayConsumption = dayConsumption,
                TotalConsumption = consumption.Sum(c => c.Consumption),
                From = DateOnly.FromDateTime(DateTime.Now.AddDays(-fromDays)),
                To = DateOnly.FromDateTime(DateTime.Now),
            };
        }
        private static List<DayConsumption> CalculateDayConsumption(ICollection<ConsumptionEntry> consumptions, int days)
        {
            var dailyCosts = new List<DayConsumption>();

            var currentDate = DateTime.Now.Date;
            for (int i = 0; i < days; i++)
            {
                var dateCursor = currentDate.AddDays(-i);
                var dayConsumption = consumptions.Where(c => c.From?.Date == dateCursor).ToList();
                var currency = dayConsumption.First().Currency;

                if (dayConsumption.Count == 0)
                {
                    continue;
                }

                var averagePrice = dayConsumption.Average(c => c.UnitPrice);
                var totalConsumption = dayConsumption.Sum(c => c.Consumption);
                var costWithFixedPrice = averagePrice * totalConsumption;
                var costWithHourlyPrice = dayConsumption.Sum(c => c.UnitPrice * c.Consumption);

                dailyCosts.Add(new DayConsumption
                {
                    Date = DateOnly.FromDateTime(dateCursor),
                    Consumption = Math.Round(totalConsumption ?? 0, 1),
                    CostWithFixedPrice = Math.Round(costWithFixedPrice ?? 0, 1),
                    CostWithHourlyPrice = Math.Round(costWithHourlyPrice ?? 0, 1),
                    Currency = currency,
                    AveragePrice = Math.Round(averagePrice ?? 0, 1),
                });
            }

            return dailyCosts;
        }
    }

    public class Consumption
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public IEnumerable<DayConsumption> DayConsumption { get; set; } = Enumerable.Empty<DayConsumption>();
        public decimal? TotalConsumption { get; set; }
        public decimal? AveragePrice
        {
            get
            {
                return DayConsumption.Average(c => c.AveragePrice);
            }
        }
        public decimal? TotalFixed
        {
            get
            {
                return Math.Round(AveragePrice * TotalConsumption ?? 0, 1);
            }
        }
        public decimal? TotalCostHourly
        {
            get
            {
                return DayConsumption.Sum(c => c.CostWithHourlyPrice);
            }
        }
        public decimal? TotalSavings
        {
            get
            {
                return TotalFixed - TotalCostHourly;
            }
        }
    }

    public class DayConsumption
    {
        public DateOnly Date { get; set; }
        public decimal? Consumption { get; set; }
        public decimal? AveragePrice { get; set; }
        public decimal? CostWithFixedPrice { get; set; }
        public decimal? CostWithHourlyPrice { get; set; }
        public decimal? PriceDiffrence
        {
            get
            {
                return CostWithFixedPrice - CostWithHourlyPrice;
            }
        }
        public string Currency { get; set; } = string.Empty;
    }
}

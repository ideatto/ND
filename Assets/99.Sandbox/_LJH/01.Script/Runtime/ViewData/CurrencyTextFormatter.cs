using System.Collections.Generic;
using System;

public static class CurrencyTextFormatter
{
    private const decimal Man = 10_000m;
    private const decimal Eok = 100_000_000m;
    private const decimal Jo = 1_000_000_000_000m;
    private const decimal Gyeong = 10_000_000_000_000_000m;


    public static string Format(long amount)
    {
        if(amount == 0)
        {
            return "0";
        }

        decimal remaining = Math.Abs((decimal)amount);
        var parts = new List<string>();

        AddUnit(parts, ref remaining, Gyeong, "경");
        AddUnit(parts, ref remaining, Jo, "조");
        AddUnit(parts, ref remaining, Eok, "억");
        AddUnit(parts, ref remaining, Man, "만");

        if(remaining > 0)
        {
            parts.Add(remaining.ToString("0"));
        }

        string result = string.Join(" ", parts);

        return amount < 0 ? "-" + result : result;
    }

    private static void AddUnit(List<string> parts, ref decimal remaining, decimal unit, string suffix)
    {
        if(remaining < unit)
        {
            return;
        }

        decimal quantity = decimal.Truncate(remaining / unit);

        parts.Add(quantity.ToString("0") + suffix);
        remaining %= unit;
    }
}

namespace OMAXRuntimeCollector.Runtime;

public class RuntimeCalculator
{
    // =========================================================
    // CALCULATE RUNTIME
    // =========================================================

    public (TimeSpan Morning, TimeSpan Afternoon) Calculate(DateTime start, DateTime end)
    {
        TimeSpan morningRuntime = TimeSpan.Zero;
        TimeSpan afternoonRuntime = TimeSpan.Zero;

        if (end <= start)
        {
            return (morningRuntime, afternoonRuntime);
        }

        DateTime current = start;

        // -----------------------------------------------------
        // Process one calendar day at a time.
        //
        // This allows an execution to cross midnight.
        // -----------------------------------------------------

        while (current < end)
        {
            DateTime dayStart = current.Date;

            DateTime morningStart =
                dayStart.AddHours(5);

            DateTime afternoonStart =
                dayStart.AddHours(14);

            DateTime midnight =
                dayStart.AddDays(1);

            DateTime segmentEnd =
                Min(end, midnight);


            // =================================================
            // MORNING
            // 05:00 → 14:00
            // =================================================

            DateTime morningOverlapStart = Max(current, morningStart);
            DateTime morningOverlapEnd = Min(segmentEnd, afternoonStart);

            if (morningOverlapEnd > morningOverlapStart)
            {
                morningRuntime +=
                    morningOverlapEnd -
                    morningOverlapStart;
            }


            // =================================================
            // AFTERNOON
            // 14:00 → 00:00
            // =================================================

            DateTime afternoonOverlapStart = Max(current, afternoonStart);
            DateTime afternoonOverlapEnd = Min(segmentEnd, midnight);

            if (afternoonOverlapEnd > afternoonOverlapStart)
            {
                afternoonRuntime +=
                    afternoonOverlapEnd -
                    afternoonOverlapStart;
            }


            current = segmentEnd;
        }

        return (morningRuntime, afternoonRuntime);
    }


    // =========================================================
    // HELPERS
    // =========================================================

    private static DateTime Max(DateTime a, DateTime b)
    {
        return a > b ? a : b;
    }


    private static DateTime Min(DateTime a, DateTime b)
    {
        return a < b ? a : b;
    }
}
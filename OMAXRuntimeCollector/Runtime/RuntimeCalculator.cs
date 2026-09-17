namespace OMAXRuntimeCollector.Runtime;


/// <summary>
/// Calculates how much execution time falls within the collector's
/// morning and afternoon accounting periods.
/// </summary>
public class RuntimeCalculator
{
    
    
    /// <summary>
    /// Calculates the execution time that falls within each accounting period
    /// between the specified start and end timestamps.
    /// </summary>
    /// <param name="start">The beginning of the execution period.</param>
    /// <param name="end">The end of the execution period.</param>
    /// <returns>
    /// A tuple containing the runtime between 05:00 and 14:00 in
    /// <c>Morning</c>, and the runtime between 14:00 and midnight in
    /// <c>Afternoon</c>.
    /// </returns>
    public (TimeSpan Morning, TimeSpan Afternoon) Calculate(DateTime start, DateTime end)
    {
        TimeSpan morningRuntime = TimeSpan.Zero;
        TimeSpan afternoonRuntime = TimeSpan.Zero;

        if (end <= start)
            return (morningRuntime, afternoonRuntime);

        DateTime current = start;

        // Process one calendar day at a time so that executions crossing
        // midnight can be correctly split between separate accounting days.
        while (current < end)
        {
            DateTime dayStart = current.Date;
            DateTime morningStart = dayStart.AddHours(5);
            DateTime afternoonStart = dayStart.AddHours(14);
            DateTime midnight = dayStart.AddDays(1);
            DateTime segmentEnd = Min(end, midnight);


            // Morning accounting period: 05:00 → 14:00.
            DateTime morningOverlapStart = Max(current, morningStart);
            DateTime morningOverlapEnd = Min(segmentEnd, afternoonStart);

            if (morningOverlapEnd > morningOverlapStart)
            {
                morningRuntime += morningOverlapEnd - morningOverlapStart;
            }


            // Afternoon accounting period: 14:00 → midnight.
            DateTime afternoonOverlapStart = Max(current, afternoonStart);
            DateTime afternoonOverlapEnd = Min(segmentEnd, midnight);

            if (afternoonOverlapEnd > afternoonOverlapStart)
            {
                afternoonRuntime += afternoonOverlapEnd - afternoonOverlapStart;
            }
            
            current = segmentEnd;
        }

        return (morningRuntime, afternoonRuntime);
    }


    #region HELPERS

        private static DateTime Max(DateTime a, DateTime b)
        {
            return a > b ? a : b;
        }


        private static DateTime Min(DateTime a, DateTime b)
        {
            return a < b ? a : b;
        }

    #endregion
}
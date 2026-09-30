using OMAXRuntimeCollector.Runtime;

namespace OMAXRuntimeCollector.Tests.Runtime;

public class RuntimeCalculatorTests
{
    [Fact]
    public void ExecutionWithinMorning_IsCountedAsMorningRuntime()
    {
        var calculator = new RuntimeCalculator();
        var start = new DateTime(2026, 8, 25, 10, 0, 0);
        var end = new DateTime(2026, 8, 25, 12, 0, 0);

        var (morning, afternoon) = calculator.Calculate(start, end);

        Assert.Equal(TimeSpan.FromHours(2), morning);
        Assert.Equal(TimeSpan.Zero, afternoon);
    }


    [Fact]
    public void ExecutionCrossing14Hours_IsSplitCorrectly()
    {
        var calculator = new RuntimeCalculator();
        var start = new DateTime(2026, 8, 25, 13, 30, 0);
        var end = new DateTime(2026, 8, 25, 14, 30, 0);

        var (morning, afternoon) = calculator.Calculate(start, end);

        Assert.Equal(TimeSpan.FromMinutes(30), morning);
        Assert.Equal(TimeSpan.FromMinutes(30), afternoon);
    }


    [Fact]
    public void ExecutionBeforeFive_IsNotCounted()
    {
        var calculator = new RuntimeCalculator();
        var start = new DateTime(2026, 8, 25, 2, 0, 0);
        var end = new DateTime(2026, 8, 25, 4, 0, 0);

        var (morning, afternoon) = calculator.Calculate(start, end);

        Assert.Equal(TimeSpan.Zero, morning);
        Assert.Equal(TimeSpan.Zero, afternoon);
    }


    [Fact]
    public void ExecutionCrossingFive_StartsCountingAtFive()
    {
        var calculator = new RuntimeCalculator();
        var start = new DateTime(2026, 8, 25, 4, 0, 0);
        var end = new DateTime(2026, 8, 25, 6, 0, 0);

        var (morning, afternoon) = calculator.Calculate(start, end);
        
        Assert.Equal(TimeSpan.FromHours(1), morning);
        Assert.Equal(TimeSpan.Zero, afternoon);
    }


    [Fact]
    public void ExecutionInAfternoon_IsCountedAsAfternoonRuntime()
    {
        var calculator = new RuntimeCalculator();
        var start = new DateTime(2026, 8, 25, 15, 0, 0);
        var end = new DateTime(2026, 8, 25, 16, 0, 0);

        var (morning, afternoon) = calculator.Calculate(start, end);
        
        Assert.Equal(TimeSpan.Zero, morning);
        Assert.Equal(TimeSpan.FromHours(1), afternoon);
    }


    [Fact]
    public void ExecutionCrossingMidnight_IsCountedCorrectly()
    {
        var calculator = new RuntimeCalculator();
        var start = new DateTime(2026, 8, 25, 23, 30, 0);
        var end = new DateTime(2026, 8, 26, 0, 30, 0);

        var (morning, afternoon) = calculator.Calculate(start, end);
        
        Assert.Equal(TimeSpan.Zero, morning);
        Assert.Equal(TimeSpan.FromMinutes(30), afternoon);
    }


    [Fact]
    public void MultipleExecutions_AreAccumulated()
    {
        var calculator = new RuntimeCalculator();
        var first = calculator.Calculate(new DateTime(2026, 8, 25, 10, 0, 0), new DateTime(2026, 8, 25, 12, 0, 0));
        var second = calculator.Calculate(new DateTime(2026, 8, 25, 12, 15, 0), new DateTime(2026, 8, 25, 13, 15, 0));
        var third = calculator.Calculate(new DateTime(2026, 8, 25, 13, 30, 0), new DateTime(2026, 8, 25, 14, 30, 0));

        var morning = first.Morning + second.Morning + third.Morning;
        var afternoon = first.Afternoon + second.Afternoon + third.Afternoon;
        
        Assert.Equal(TimeSpan.FromHours(3.5), morning);
        Assert.Equal(TimeSpan.FromMinutes(30), afternoon);
    }
    
    [Fact]
    public void ExecutionEndingExactlyAt14_IsCountedAsMorningRuntime()
    {
        var calculator = new RuntimeCalculator();
        var start = new DateTime(2026, 8, 25, 13, 0, 0);
        var end = new DateTime(2026, 8, 25, 14, 0, 0);

        var (morning, afternoon) = calculator.Calculate(start, end);

        Assert.Equal(TimeSpan.FromHours(1), morning);
        Assert.Equal(TimeSpan.Zero, afternoon);
    }
    
    [Fact]
    public void ExecutionStartingExactlyAt14_IsCountedAsAfternoonRuntime()
    {
        var calculator = new RuntimeCalculator();
        var start = new DateTime(2026, 8, 25, 14, 0, 0);
        var end = new DateTime(2026, 8, 25, 15, 0, 0);

        var (morning, afternoon) = calculator.Calculate(start, end);

        Assert.Equal(TimeSpan.Zero, morning);
        Assert.Equal(TimeSpan.FromHours(1), afternoon);
    }
    
    [Fact]
    public void ExecutionStartingExactlyAtFive_IsCountedAsMorningRuntime()
    {
        var calculator = new RuntimeCalculator();
        var start = new DateTime(2026, 8, 25, 5, 0, 0);
        var end = new DateTime(2026, 8, 25, 6, 0, 0);

        var (morning, afternoon) = calculator.Calculate(start, end);

        Assert.Equal(TimeSpan.FromHours(1), morning);
        Assert.Equal(TimeSpan.Zero, afternoon);
    }
    
    [Fact]
    public void ExecutionWithZeroDuration_ReturnsZeroRuntime()
    {
        var calculator = new RuntimeCalculator();
        var timestamp = new DateTime(2026, 8, 25, 10, 0, 0);

        var (morning, afternoon) = calculator.Calculate(timestamp, timestamp);

        Assert.Equal(TimeSpan.Zero, morning);
        Assert.Equal(TimeSpan.Zero, afternoon);
    }
    
    [Fact]
    public void ExecutionEndingBeforeStart_ReturnsZeroRuntime()
    {
        var calculator = new RuntimeCalculator();
        var start = new DateTime(2026, 8, 25, 12, 0, 0);
        var end = new DateTime(2026, 8, 25, 10, 0, 0);

        var (morning, afternoon) = calculator.Calculate(start, end);

        Assert.Equal(TimeSpan.Zero, morning);
        Assert.Equal(TimeSpan.Zero, afternoon);
    }
    
    [Fact]
    public void ExecutionAcrossMultipleDays_IsCountedCorrectly()
    {
        var calculator = new RuntimeCalculator();
        var start = new DateTime(2026, 8, 25, 13, 0, 0);
        var end = new DateTime(2026, 8, 27, 15, 0, 0);

        var (morning, afternoon) = calculator.Calculate(start, end);
        
        Assert.Equal(TimeSpan.FromHours(19), morning);
        Assert.Equal(TimeSpan.FromHours(21), afternoon);
    }
}
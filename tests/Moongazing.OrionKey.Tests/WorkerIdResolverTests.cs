namespace Moongazing.OrionKey.Tests;

public class WorkerIdResolverTests
{
    [Fact]
    public void Resolve_ShouldUseEnvironmentVariable_WhenSet()
    {
        Environment.SetEnvironmentVariable("ORIONKEY_WORKER_ID", "42");
        try
        {
            var (workerId, source) = WorkerIdResolver.Resolve();
            Assert.Equal(42, workerId);
            Assert.Equal(WorkerIdSource.EnvironmentVariable, source);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ORIONKEY_WORKER_ID", null);
        }
    }

    [Fact]
    public void Resolve_ShouldFallBackToMachineHash_WhenEnvVarMissing()
    {
        Environment.SetEnvironmentVariable("ORIONKEY_WORKER_ID", null);
        var (workerId, source) = WorkerIdResolver.Resolve();
        Assert.InRange(workerId, 0, 1023);
        Assert.Equal(WorkerIdSource.MachineNameHash, source);
    }

    [Fact]
    public void Resolve_ShouldMaskMachineHashTo10Bits()
    {
        var (workerId, _) = WorkerIdResolver.Resolve();
        Assert.True(workerId <= 1023);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1024")]
    [InlineData("notanumber")]
    public void Resolve_ShouldFallBackToMachineHash_WhenEnvVarInvalid(string raw)
    {
        Environment.SetEnvironmentVariable("ORIONKEY_WORKER_ID", raw);
        try
        {
            var (_, source) = WorkerIdResolver.Resolve();
            Assert.Equal(WorkerIdSource.MachineNameHash, source);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ORIONKEY_WORKER_ID", null);
        }
    }
}

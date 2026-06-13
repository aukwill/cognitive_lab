namespace CognitiveRuntime.Core.Exceptions;

public sealed class ModeLoadException : Exception
{
    public ModeLoadException(string message)
        : base(message)
    {
    }

    public ModeLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class ModelProviderException : Exception
{
    public ModelProviderException(string message)
        : base(message)
    {
    }

    public ModelProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class RuntimeRunException : Exception
{
    public RuntimeRunException(string message, string outputDirectory, Exception innerException)
        : base(message, innerException)
    {
        OutputDirectory = outputDirectory;
    }

    public string OutputDirectory { get; }
}

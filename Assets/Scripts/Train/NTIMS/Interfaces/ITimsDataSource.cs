public interface ITimsDataSource
{
    float TransmissionIntervalSeconds { get; }

    void WriteTimsData(TimsCarTerminal terminal);
}

namespace BoardOil.Contracts.Support;

public sealed record DatabaseInfoDto(string Provider, string Version, string JournalMode, long SizeBytes);

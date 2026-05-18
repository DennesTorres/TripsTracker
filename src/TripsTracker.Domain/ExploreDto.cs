namespace TripsTracker.Domain;

public class ExploreLocationDto
{
    public required string City { get; init; }
    public string? StateName { get; init; }
    public required string CountryName { get; init; }
    public int CountryId { get; init; }
    public double Lat { get; init; }
    public double Lon { get; init; }
    public int UserCount { get; init; }
    public int PhotoCount { get; init; }
    public int CommentCount { get; init; }
}

public class ExploreContentDto
{
    public required List<PlacePhotoDto> Photos { get; init; }
    public required List<PlaceCommentDto> Comments { get; init; }
}

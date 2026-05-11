namespace TripsTracker.Domain;

public record ExploreLocationDto(
    string City,
    string? StateName,
    string CountryName,
    int CountryId,
    double Lat,
    double Lon,
    int UserCount,
    int PhotoCount,
    int CommentCount);

public record ExploreContentDto(
    List<PlacePhotoDto> Photos,
    List<PlaceCommentDto> Comments);
